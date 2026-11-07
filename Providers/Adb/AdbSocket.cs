using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace fernien.Providers.Adb;

/// <summary>Yerel ADB sunucusuna (127.0.0.1:5037) açılan tek bir bağlantı.</summary>
internal sealed class AdbSocket : IDisposable
{
    public const int ServerPort = 5037;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    private AdbSocket(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    public NetworkStream Stream => _stream;

    /// <summary>Okunmayı bekleyen bayt sayısı.</summary>
    public int Available => _client.Client.Available;

    public static async Task<AdbSocket> ConnectAsync(CancellationToken ct)
    {
        var client = new TcpClient
        {
            NoDelay = true,
            ReceiveBufferSize = 1 << 20,
            SendBufferSize = 1 << 20
        };
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, ServerPort, ct).ConfigureAwait(false);
            return new AdbSocket(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>"0012host:version" biçiminde istek gönderir ve OKAY/FAIL yanıtını okur.</summary>
    public async Task SendRequestAsync(string request, CancellationToken ct)
    {
        byte[] payload = Encoding.UTF8.GetBytes(request);
        byte[] message = new byte[4 + payload.Length];
        Encoding.ASCII.GetBytes(payload.Length.ToString("X4", CultureInfo.InvariantCulture), message.AsSpan(0, 4));
        payload.CopyTo(message, 4);
        await _stream.WriteAsync(message, ct).ConfigureAwait(false);
        await ReadStatusAsync(ct).ConfigureAwait(false);
    }

    public async Task ReadStatusAsync(CancellationToken ct)
    {
        string status = await ReadAsciiAsync(4, ct).ConfigureAwait(false);
        if (status == "OKAY") return;
        if (status == "FAIL") throw new AdbException(await ReadLengthPrefixedStringAsync(ct).ConfigureAwait(false));
        throw new AdbException($"Beklenmeyen ADB yanıtı: {status}");
    }

    public async Task<string> ReadLengthPrefixedStringAsync(CancellationToken ct)
    {
        string hex = await ReadAsciiAsync(4, ct).ConfigureAwait(false);
        int length = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (length == 0) return "";
        byte[] buffer = new byte[length];
        await _stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer);
    }

    public async Task<string> ReadToEndAsync(CancellationToken ct)
    {
        using var memory = new MemoryStream();
        await _stream.CopyToAsync(memory, ct).ConfigureAwait(false);
        return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }

    private async Task<string> ReadAsciiAsync(int count, CancellationToken ct)
    {
        byte[] buffer = new byte[count];
        await _stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
        return Encoding.ASCII.GetString(buffer);
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}

/// <summary>ADB sunucusu veya cihaz tarafından döndürülen FAIL mesajı.</summary>
internal sealed class AdbException(string message) : Exception(message);
