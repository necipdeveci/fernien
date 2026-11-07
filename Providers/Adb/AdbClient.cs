using System.Globalization;

namespace fernien.Providers.Adb;

internal sealed record AdbDeviceEntry(string Serial, string State, string? Model, string? Product, bool IsUsb);

/// <summary>ADB sunucusunun "host:" servisleri ve cihaza yönlendirilmiş bağlantılar.</summary>
internal sealed class AdbClient
{
    public async Task<int> GetServerVersionAsync(CancellationToken ct)
    {
        using var socket = await AdbSocket.ConnectAsync(ct).ConfigureAwait(false);
        await socket.SendRequestAsync("host:version", ct).ConfigureAwait(false);
        string hex = await socket.ReadLengthPrefixedStringAsync(ct).ConfigureAwait(false);
        return int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<AdbDeviceEntry>> GetDevicesAsync(CancellationToken ct)
    {
        using var socket = await AdbSocket.ConnectAsync(ct).ConfigureAwait(false);
        await socket.SendRequestAsync("host:devices-l", ct).ConfigureAwait(false);
        return ParseDevices(await socket.ReadLengthPrefixedStringAsync(ct).ConfigureAwait(false));
    }

    /// <summary>"SERIAL  device usb:1-1 product:x model:Pixel_7 device:y transport_id:1" satırlarını çözer.</summary>
    public static List<AdbDeviceEntry> ParseDevices(string text)
    {
        var result = new List<AdbDeviceEntry>();
        foreach (string rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = rawLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string? model = null, product = null;
            bool usb = false;
            foreach (string part in parts.Skip(2))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                string key = part[..colon], value = part[(colon + 1)..];
                switch (key)
                {
                    case "model": model = value.Replace('_', ' '); break;
                    case "product": product = value; break;
                    case "usb": usb = true; break;
                }
            }
            result.Add(new AdbDeviceEntry(parts[0], parts[1], model, product, usb));
        }
        return result;
    }

    public async Task<HashSet<string>> GetFeaturesAsync(string serial, CancellationToken ct)
    {
        using var socket = await AdbSocket.ConnectAsync(ct).ConfigureAwait(false);
        await socket.SendRequestAsync($"host-serial:{serial}:features", ct).ConfigureAwait(false);
        string text = await socket.ReadLengthPrefixedStringAsync(ct).ConfigureAwait(false);
        return new HashSet<string>(text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Belirtilen cihaza bağlanmış bir soket döndürür; sonraki istekler doğrudan cihaza gider.</summary>
    public async Task<AdbSocket> OpenTransportAsync(string serial, CancellationToken ct)
    {
        var socket = await AdbSocket.ConnectAsync(ct).ConfigureAwait(false);
        try
        {
            await socket.SendRequestAsync($"host:transport:{serial}", ct).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async Task<string> ShellAsync(string serial, string command, CancellationToken ct)
    {
        using var socket = await OpenTransportAsync(serial, ct).ConfigureAwait(false);
        await socket.SendRequestAsync("shell:" + command, ct).ConfigureAwait(false);
        return await socket.ReadToEndAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Cihaz listesi her değiştiğinde <paramref name="onChanged"/> çağrılır. Bağlantı kopana kadar döner.</summary>
    public async Task TrackDevicesAsync(Action onChanged, CancellationToken ct)
    {
        using var socket = await AdbSocket.ConnectAsync(ct).ConfigureAwait(false);
        await socket.SendRequestAsync("host:track-devices", ct).ConfigureAwait(false);
        while (!ct.IsCancellationRequested)
        {
            await socket.ReadLengthPrefixedStringAsync(ct).ConfigureAwait(false);
            onChanged();
        }
    }
}
