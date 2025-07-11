// Program.cs  (TargetFramework: net6.0 �̻�)
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// WebSocket �ɼ�?�ʿ信 ���� ����
builder.Services.AddLogging(c => c.AddConsole());
builder.Services.AddWebSockets(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
app.UseWebSockets();

var logger = app.Logger;
var clients = new List<WebSocket>();

// ===== WebSocket ��������Ʈ =====
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    lock (clients) clients.Add(socket);
    logger.LogInformation("Ŭ���̾�Ʈ �����: {Endpoint}", context.Connection.RemoteIpAddress);

    var buffer = new byte[1024 * 4];
    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var msg = Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count));
            logger.LogInformation("[{Endpoint}] {Message}", context.Connection.RemoteIpAddress, msg);

            // ��� Ŭ���̾�Ʈ���� ��ε�ĳ��Ʈ
            var data = Encoding.UTF8.GetBytes(msg);
            List<WebSocket> toRemove = new();
            lock (clients)
            {
                foreach (var ws in clients)
                {
                    if (ws.State != WebSocketState.Open || ws == socket) continue;
                    try
                    {
                        ws.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                    }
                    catch
                    {
                        toRemove.Add(ws);
                    }
                }
                // ���� ���� ����
                foreach (var dead in toRemove) clients.Remove(dead);
            }
        }
    }
    finally
    {
        lock (clients) clients.Remove(socket);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        logger.LogInformation("Ŭ���̾�Ʈ ����: {Endpoint}", context.Connection.RemoteIpAddress);
    }
});

// ��Ʈ���� ������ ���� Ȯ�ο� HTTP ����
app.MapGet("/", () => "MessengerServer WebSocket is running.");

// ---------- ��Ʈ ���� ----------
var portStr = Environment.GetEnvironmentVariable("PORT");
var port    = !string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out var p) ? p : 8080;
app.Urls.Add($"http://0.0.0.0:{port}");

logger.LogInformation("������ ���� ���� - ��Ʈ {Port}", port);
await app.RunAsync();
