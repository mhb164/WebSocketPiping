using Boilerplates;
using Microsoft.AspNetCore.Http;
using System.Text;
using WebSocketPiping;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var justHttps = false;
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    justHttps = true;
    app.UseHttpsRedirection();
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

app.MapGet("/", async (HttpContext httpContext, ILogger<Program> logger) =>
{
    var remoteIpAddress = httpContext.Connection?.RemoteIpAddress?.ToString();
    if (justHttps && !httpContext.Request.IsHttps)
    {
        await Results.Unauthorized().ExecuteAsync(httpContext);
        logger?.LogInformation("{RemoteIpAddress}-{Method}-{Protocol} Rejected!", remoteIpAddress, httpContext.Request.Method, httpContext.Request.IsHttps ? "Https" : "Http");
        return;
    }

    if (httpContext.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();

        using var handler = new WebSocketPipe(remoteIpAddress, webSocket, logger);
        await handler.HandleAsync();
        return;
    }

    var report = new StringBuilder();
    report.AppendLine($"WSPipe:");
    report.AppendLine($"  Version: {Aid.AppVersion} {(justHttps ? "(just Https)" : "")}");
    report.AppendLine($"  Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
    await httpContext.Response.WriteAsync(report.ToString());

    logger?.LogInformation("{RemoteIpAddress}-{Method}-{Protocol} Handled, justHttps:{JustHttps}", remoteIpAddress, httpContext.Request.Method, httpContext.Request.IsHttps ? "Https" : "Http", justHttps);
});

await app.RunAsync();
