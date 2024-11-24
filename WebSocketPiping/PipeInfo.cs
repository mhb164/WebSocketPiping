using System.Net;

namespace WebSocketPiping;

public class PipeInfo
{
    public readonly Guid Key;
    public readonly string Host;
    public readonly ushort Port;
    public readonly bool KeepAlive;


    private PipeInfo(Guid key, string host, ushort port, bool keepAlive)
    {
        Key = key;
        Host = host;
        Port = port;
        KeepAlive = keepAlive;

        Text = $"[{Key.ToString("N").ToUpper()}] {Host}:{Port}{(KeepAlive ? " (KeepAlive)" : "")}";
    }

    public string Text { get; private set; }

    public static PipeInfo Parse(string input)
    {
        var inputParameters = input.Split(",");
        if (inputParameters.Length != 4)
            throw new ArgumentException("PipeInfo parameters is wrong!", nameof(input));

        if (!Guid.TryParse(inputParameters[0], out var key))
            throw new ArgumentException("PipeInfo.Key parameter[0] is wrong!", nameof(input));

        if (string.IsNullOrWhiteSpace(inputParameters[1]))
            throw new ArgumentException("PipeInfo.Host parameter[1] is wrong!", nameof(input));
        var host = inputParameters[1];

        if (!ushort.TryParse(inputParameters[2], out var port))
            throw new ArgumentException("PipeInfo.Port parameter[2] is wrong!", nameof(input));

        if (!bool.TryParse(inputParameters[3], out var keepAlive))
            throw new ArgumentException("PipeInfo.KeepAlive parameter[3] is wrong!", nameof(input));

        return new PipeInfo(key, host, port, keepAlive);
    }

    public async Task<IPAddress> GetHostAddress()
    {
        var hostEntry = await Dns.GetHostEntryAsync(Host);
        var resolvedAddress = hostEntry?.AddressList?.FirstOrDefault();
        if (resolvedAddress is null)
            throw new WebSocketPipeException($"Cannot resolve {Host}!");


        Text = $"[{Key.ToString("N").ToUpper()}] {Host}({resolvedAddress}):{Port}{(KeepAlive ? " (KeepAlive)" : "")}";
        return resolvedAddress;
    }

    public override string ToString() => Text;
}

