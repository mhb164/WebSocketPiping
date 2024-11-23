namespace WebSocketPiping;

public class Program
{
    public const string Version = "1.5";
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseHsts();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromMinutes(2)
        });

        app.MapGet("/", async (HttpContext httpContext, ILogger<Program> logger) =>
        {
            var remoteIpAddress = httpContext.Connection?.RemoteIpAddress?.ToString();
            if (httpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();

                using var handler = new WebSocketPipe(remoteIpAddress, webSocket, logger);
                await handler.HandleAsync();
            }
            else
            {
                await httpContext.Response.WriteAsync($"WSPipe v{Version} {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                logger?.LogInformation("{RemoteIpAddress}-{Method}-{Protocol} Handled", remoteIpAddress, httpContext.Request.Method, httpContext.Request.IsHttps ? "Https" : "Http");
            }
        });

        app.Run();
    }
}
