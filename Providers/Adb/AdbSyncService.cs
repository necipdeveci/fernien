using System.Buffers.Binary;
using System.Text;
using fernien.Core;

namespace fernien.Providers.Adb;

internal sealed record SyncStat(string Name, uint Mode, long Size, DateTime ModifiedUtc)
{
    public bool IsDirectory => (Mode & 0xF000) == 0x4000;
    public bool IsSymlink => (Mode & 0xF000) == 0xA000;
}

/// <summary>
/// ADB "sync:" protokolü: dosya listeleme, stat, gönderme (SEND) ve alma (RECV).
/// Cihaz destekliyorsa 64‑bit boyutlu v2 komutları (LIS2/STA2) kullanılır.
/// Bir hata sonrasında bağlantı tekrar kullanılmamalıdır.
/// </summary>
internal sealed class AdbSyncService : IDisposable
{
    public const int MaxChunkSize = 64 * 1024;

    private readonly AdbSocket _socket;
    private readonly bool _v2;
    private readonly byte[] _buffer = new byte[8 + MaxChunkSize];
    private readonly byte[] _header = new byte[128];

    private AdbSyncService(AdbSocket socket, bool v2)
    {
        _socket = socket;
        _v2 = v2;
    }

    private Stream Stream => _socket.Stream;

    public static async Task<AdbSyncService> OpenAsync(AdbClient client, string serial, bool useV2, CancellationToken ct)
    {
        var socket = await client.OpenTransportAsync(serial, ct).ConfigureAwait(false);
        try
        {
            await socket.SendRequestAsync("sync:", ct).ConfigureAwait(false);
            return new AdbSyncService(socket, useV2);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async Task<SyncStat?> StatAsync(string path, CancellationToken ct)
    {
        string name = RemotePath.GetName(path);
        if (_v2)
        {
            await SendCommandAsync("STA2", path, ct).ConfigureAwait(false);
            byte[] h = await ReadHeaderAsync(72, ct).ConfigureAwait(false);
            ExpectId(h, "STA2");
            if (U32(h, 4) != 0) return null; // errno
            return new SyncStat(name, U32(h, 24), I64(h, 40), FromUnix(I64(h, 56)));
        }

        await SendCommandAsync("STAT", path, ct).ConfigureAwait(false);
        byte[] r = await ReadHeaderAsync(16, ct).ConfigureAwait(false);
        ExpectId(r, "STAT");
        uint mode = U32(r, 4), size = U32(r, 8), time = U32(r, 12);
        if (mode == 0 && size == 0 && time == 0) return null;
        return new SyncStat(name, mode, size, FromUnix(time));
    }

    public async Task<List<SyncStat>> ListAsync(string path, CancellationToken ct)
    {
        await SendCommandAsync(_v2 ? "LIS2" : "LIST", path, ct).ConfigureAwait(false);
        int headerSize = _v2 ? 76 : 20;
        string entryId = _v2 ? "DNT2" : "DENT";
        var list = new List<SyncStat>();

        while (true)
        {
            byte[] h = await ReadHeaderAsync(headerSize, ct).ConfigureAwait(false);
            string id = Id(h);
            if (id == "DONE") return list;
            if (id != entryId) throw new AdbException($"Beklenmeyen listeleme yanıtı: {id}");

            bool error;
            uint mode;
            long size, mtime;
            int nameLength;
            if (_v2)
            {
                error = U32(h, 4) != 0;
                mode = U32(h, 24);
                size = I64(h, 40);
                mtime = I64(h, 56);
                nameLength = (int)U32(h, 72);
            }
            else
            {
                error = false;
                mode = U32(h, 4);
                size = U32(h, 8);
                mtime = U32(h, 12);
                nameLength = (int)U32(h, 16);
            }

            if (nameLength is < 0 or > 16 * 1024) throw new AdbException("Geçersiz dosya adı uzunluğu.");
            byte[] nameBytes = new byte[nameLength];
            await Stream.ReadExactlyAsync(nameBytes, ct).ConfigureAwait(false);
            string name = Encoding.UTF8.GetString(nameBytes);

            if (error || name is "." or "..") continue;
            list.Add(new SyncStat(name, mode, size, FromUnix(mtime)));
        }
    }

    /// <summary>Akışı cihazdaki <paramref name="remotePath"/> konumuna yazar (üst klasörler otomatik oluşur).</summary>
    public async Task PushAsync(Stream source, string remotePath, int mode, DateTime modifiedUtc, Action<long>? progress, CancellationToken ct)
    {
        await SendCommandAsync("SEND", $"{remotePath},{mode}", ct).ConfigureAwait(false);

        byte[] buffer = _buffer;
        WriteId(buffer, 0, "DATA");
        while (true)
        {
            int read = await ReadFullAsync(source, buffer.AsMemory(8, MaxChunkSize), ct).ConfigureAwait(false);
            if (read == 0) break;
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), read);
            await Stream.WriteAsync(buffer.AsMemory(0, 8 + read), ct).ConfigureAwait(false);
            progress?.Invoke(read);

            // Cihaz aktarım sırasında yanıt gönderdiyse bu bir hatadır (ör. izin reddedildi); boşuna devam etme.
            if (_socket.Available > 0) break;
        }

        byte[] done = new byte[8];
        WriteId(done, 0, "DONE");
        long unix = Math.Clamp(new DateTimeOffset(DateTime.SpecifyKind(modifiedUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(), 0, uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(done.AsSpan(4), (uint)unix);
        await Stream.WriteAsync(done, ct).ConfigureAwait(false);
        await ReadSyncStatusAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Cihazdaki dosyayı <paramref name="destination"/> akışına yazar.</summary>
    public async Task PullAsync(string remotePath, Stream destination, Action<long>? progress, CancellationToken ct)
    {
        await SendCommandAsync("RECV", remotePath, ct).ConfigureAwait(false);
        while (true)
        {
            await Stream.ReadExactlyAsync(_buffer.AsMemory(0, 8), ct).ConfigureAwait(false);
            string id = Id(_buffer);
            int length = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(4));

            if (id == "DONE") return;
            if (id == "FAIL") throw new AdbException(await ReadStringAsync(length, ct).ConfigureAwait(false));
            if (id != "DATA" || length is < 0 or > MaxChunkSize) throw new AdbException($"Beklenmeyen veri yanıtı: {id}");

            await Stream.ReadExactlyAsync(_buffer.AsMemory(8, length), ct).ConfigureAwait(false);
            await destination.WriteAsync(_buffer.AsMemory(8, length), ct).ConfigureAwait(false);
            progress?.Invoke(length);
        }
    }

    private async Task ReadSyncStatusAsync(CancellationToken ct)
    {
        byte[] h = await ReadHeaderAsync(8, ct).ConfigureAwait(false);
        string id = Id(h);
        int length = (int)U32(h, 4);
        if (id == "OKAY") return;
        if (id == "FAIL") throw new AdbException(await ReadStringAsync(length, ct).ConfigureAwait(false));
        throw new AdbException($"Beklenmeyen yanıt: {id}");
    }

    private async Task SendCommandAsync(string id, string argument, CancellationToken ct)
    {
        byte[] arg = Encoding.UTF8.GetBytes(argument);
        byte[] message = new byte[8 + arg.Length];
        WriteId(message, 0, id);
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(4), arg.Length);
        arg.CopyTo(message, 8);
        await Stream.WriteAsync(message, ct).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadHeaderAsync(int size, CancellationToken ct)
    {
        await Stream.ReadExactlyAsync(_header.AsMemory(0, size), ct).ConfigureAwait(false);
        return _header;
    }

    private async Task<string> ReadStringAsync(int length, CancellationToken ct)
    {
        if (length is <= 0 or > 64 * 1024) return "Bilinmeyen cihaz hatası";
        byte[] data = new byte[length];
        await Stream.ReadExactlyAsync(data, ct).ConfigureAwait(false);
        return Encoding.UTF8.GetString(data);
    }

    private static async Task<int> ReadFullAsync(Stream source, Memory<byte> buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await source.ReadAsync(buffer[total..], ct).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static void ExpectId(byte[] header, string expected)
    {
        string id = Id(header);
        if (id != expected) throw new AdbException($"Beklenmeyen yanıt: {id}");
    }

    private static string Id(byte[] data) => Encoding.ASCII.GetString(data, 0, 4);
    private static void WriteId(byte[] data, int offset, string id) => Encoding.ASCII.GetBytes(id, data.AsSpan(offset, 4));
    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
    private static long I64(byte[] data, int offset) => BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset));

    private static DateTime FromUnix(long seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(seconds, 0, 253_402_300_799)).UtcDateTime;

    public void Dispose() => _socket.Dispose();
}
