using Osynt.Infrastructure.Persistence;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Osynt.Infrastructure.Demo;

/// <summary>A loopback-only sample API. No external service or account needed.</summary>
public sealed class DemoServer : IDisposable
{
    private TcpListener? _listener;
    private readonly CancellationTokenSource _stop = new();
    public string BaseUrl { get; private set; } = "";
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        _ = AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(_stop.Token);
                _ = HandleAsync(client);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException) { }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await using var stream = client.GetStream();
                // Read bytes directly so a buffered text reader cannot consume part of the body.
                using var header = new MemoryStream();
                var single = new byte[1];
                while (header.Length < 16384)
                {
                    if (await stream.ReadAsync(single, timeout.Token) == 0)
                        return;
                    header.WriteByte(single[0]);
                    var bytes = header.GetBuffer();
                    var length = (int)header.Length;
                    if (length >= 4 && bytes[length - 4] == 13 && bytes[length - 3] == 10 && bytes[length - 2] == 13 && bytes[length - 1] == 10)
                        break;
                }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                var requestLine = lines[0].Split(' ');
                if (requestLine.Length < 2)
                    return;
                var uri = new Uri(BaseUrl + requestLine[1]);
                var method = requestLine[0];
                var contentLength = lines.FirstOrDefault(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))?.Split(':', 2)[1].Trim();
                var bodyLength = int.TryParse(contentLength, out var size) ? Math.Clamp(size, 0, 1024 * 1024) : 0;
                var body = new byte[bodyLength];
                await stream.ReadExactlyAsync(body, timeout.Token);
                var (status, payload) = DemoRoutes.Respond(method, uri, Encoding.UTF8.GetString(body));
                var json = JsonSerializer.Serialize(payload, WorkspaceJson.Options);
                var responseBytes = Encoding.UTF8.GetBytes(json);
                var reason = status switch
                {
                    200 => "OK",
                    201 => "Created",
                    400 => "Bad Request",
                    405 => "Method Not Allowed",
                    _ => "Not Found"
                };
                var responseHeader = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {responseBytes.Length}\r\nX-Powered-By: a little sunshine\r\nX-Osynt-Demo: true\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(responseHeader, timeout.Token);
                if (method != "HEAD")
                    await stream.WriteAsync(responseBytes, timeout.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException or UriFormatException) { }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener?.Stop();
        _stop.Dispose();
    }
}
