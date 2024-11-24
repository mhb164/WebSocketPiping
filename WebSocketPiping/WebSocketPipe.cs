using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketPiping;
public sealed class WebSocketPipe : IDisposable
{
    public readonly string RemoteIpAddress;
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

    private Socket? _destination;
    public PipeInfo? Info { get; private set; }
    public string Name => $"[{RemoteIpAddress}]{Info}";

    public async Task HandleAsync()
    {
        _logger?.LogInformation("{RemoteIpAddress}> New WebSocket connected!", RemoteIpAddress);

        try
        {
            var pipeInfoText = await GetPipeInfoText();

            Info = PipeInfo.Parse(pipeInfoText);

            _logger?.LogInformation("{Name}> Pipe info received, start piping...", Name);

            _destination = await CreateDestination(Info);

            if (_destination == null || !_destination.Connected)
                return;

            _logger?.LogInformation("{Name}> Pipe destination connected", Name);

            await _source.SendAsync(Encoding.UTF8.GetBytes("OK"), WebSocketMessageType.Text, true, CancellationToken.None);

            StartDestinationToSource();
            var receiveResult = await SourceToDestination();

            if (receiveResult?.CloseStatus is not null)
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

    private async Task<string> GetPipeInfoText()
    {
        var buffer = new byte[1024];
        var receiveResult = await _source.ReceiveAsync(buffer, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
    }

    private static async Task<Socket> CreateDestination(PipeInfo? info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var address = await info.GetHostAddress();
        var endPoint = new IPEndPoint(address, info.Port);

        var socket = default(Socket);
        try
        {
            socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (info.KeepAlive)
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);

            await socket.ConnectAsync(endPoint);

            return socket;
        }
        catch
        {
            Dispose(socket);
            throw;
        }
    }

    private void StartDestinationToSource()
    {
        new Thread(async () =>
        {
            ArgumentNullException.ThrowIfNull(_destination);

            _logger?.LogInformation("{Name}> Destination -> Source started", Name);
            var buffer = new byte[8192];

            try
            {
                var receiveCount = await _destination.ReceiveAsync(buffer, CancellationToken.None);
                while (receiveCount > 0)
                {
                    var data = new byte[receiveCount];
                    Array.Copy(buffer, data, receiveCount);

                    await _source.SendAsync(data, WebSocketMessageType.Binary, false, CancellationToken.None);

                    receiveCount = await _destination.ReceiveAsync(buffer, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}> Destination -> Source failed!", Name);
                Dispose();
            }
        })
        { IsBackground = true }.Start();
    }

    private async Task<WebSocketReceiveResult> SourceToDestination()
    {
        ArgumentNullException.ThrowIfNull(_destination);

        _logger?.LogInformation("{Name}> Source -> Destination started", Name);

        var buffer = new byte[8192];
        var receiveResult = await _source.ReceiveAsync(buffer, CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            var data = new byte[receiveResult.Count];
            Array.Copy(buffer, data, receiveResult.Count);

            await _destination.SendAsync(data);
            receiveResult = await _source.ReceiveAsync(buffer, CancellationToken.None);
        }

        return receiveResult;
    }

    public void Dispose()
    {
        Dispose(_destination);
    }

    private static void Dispose(Socket? socket)
    {
        if (socket is null)
            return;

        try { socket.Shutdown(SocketShutdown.Both); } catch { /*Just need to make sure*/ }
        try { socket.Dispose(); } catch { /*Just need to make sure*/ }
    }
}

