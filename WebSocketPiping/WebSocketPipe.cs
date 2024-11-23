using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketPiping;

public sealed class WebSocketPipe : IDisposable
{
    private readonly string RemoteIpAddress;
    private readonly WebSocket _source;
    private readonly ILogger? _logger;

    public WebSocketPipe(string? remoteIpAddress, WebSocket? source, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteIpAddress);
        ArgumentNullException.ThrowIfNull(source);

        RemoteIpAddress = remoteIpAddress;
        _source = source;
        _logger = logger;
    }

    private Socket? _destinationSocket;

    public Guid Key { get; private set; }
    public string? Host { get; private set; }
    public ushort Port { get; private set; }
    public bool KeepAlive { get; private set; }

    public string Name => $"[{RemoteIpAddress}-{Key}] {Host}:{Port}{(KeepAlive ? " (KeepAlive)" : "")}";

    public async Task HandleAsync()
    {
        _logger?.LogInformation("{RemoteIpAddress}> New WebSocket connected!", RemoteIpAddress);
        try
        {

            var buffer = new byte[8192];
            var receiveResult = await _source.ReceiveAsync(buffer, CancellationToken.None);

            var startCommand = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            var startCommandParameters = startCommand.Split(",");

            Key = Guid.Parse(startCommandParameters[0]);
            Host = startCommandParameters[1];
            Port = ushort.Parse(startCommandParameters[2]);
            KeepAlive = startCommandParameters[3] == "true";

            _logger?.LogInformation("{Name}> Handle started", Name);

            await Connect();

            if (_destinationSocket == null || !_destinationSocket.Connected)
                return;

            _logger?.LogInformation("{Name}> Connected", Name);

            new Thread(StartDataReceive) { IsBackground = true }.Start();

            await _source.SendAsync(Encoding.UTF8.GetBytes("OK"), WebSocketMessageType.Text, true, CancellationToken.None);

            receiveResult = await _source.ReceiveAsync(buffer, CancellationToken.None);

            while (!receiveResult.CloseStatus.HasValue)
            {
                var data = new byte[receiveResult.Count];
                Array.Copy(buffer, data, receiveResult.Count);

                await _destinationSocket.SendAsync(data);
                receiveResult = await _source.ReceiveAsync(buffer, CancellationToken.None);
            }

            await _source.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "{Name}> HandleAsync failed!", Name);
        }
        finally
        {
            Dispose();
        }

        _logger?.LogInformation("{Name}> Handle finished", Name);
    }

    private async Task Connect()
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(Host);
            var destinationEndPoint = new IPEndPoint(hostEntry.AddressList[0], Port);
            _destinationSocket = new Socket(destinationEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            if (KeepAlive)
                _destinationSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);

            await _destinationSocket.ConnectAsync(destinationEndPoint);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "{Name}> Connect failed!", Name);
            Dispose();
        }
    }

    private async void StartDataReceive()
    {
        _logger?.LogInformation("{Name}> DataReceive started", Name);
        var buffer = new byte[8192];
        try
        {
            var receiveCount = await _destinationSocket!.ReceiveAsync(buffer, CancellationToken.None);
            while (receiveCount > 0)
            {
                var data = new byte[receiveCount];
                Array.Copy(buffer, data, receiveCount);

                await _source.SendAsync(data, WebSocketMessageType.Binary, false, CancellationToken.None);

                receiveCount = await _destinationSocket!.ReceiveAsync(buffer, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "{Name}> StartDataReceive failed!", Name);
            Dispose();
        }
    }

    public void Dispose()
    {
        try { _destinationSocket?.Shutdown(SocketShutdown.Both); } catch { }
        try { _destinationSocket?.Dispose(); } catch { }
        try { _source?.Dispose(); } catch { }
    }
}

