using Osynt.Infrastructure.Persistence;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Osynt.Infrastructure.Demo;

/// <summary>A loopback-only sample API. No external service or account needed.</summary>
public sealed class DemoServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    public string BaseUrl { get; private set; } = "";
    public void Start()
    {
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
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
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
                var (status, payload) = CreateResponse(method, uri, Encoding.UTF8.GetString(body));
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

    private static (int, object) CreateResponse(string method, Uri uri, string body)
    {
        if (uri.AbsolutePath == "/v1/plant")
        {
            if (method != "POST")
                return (405, new
                {
                    error = "Use POST to plant a crop."
                });
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return (400, new
                    {
                        error = "Send a JSON object with crop and quantity."
                    });
                return (201, new
                {
                    message = "A little care goes a long way.",
                    planted = document.RootElement.Clone(),
                    watered = true
                });
            }
            catch (JsonException) { return (400, new { error = "Your seeds need valid JSON. Try {\"crop\":\"strawberry\",\"quantity\":12}." }); }
        }
        if (method is not ("GET" or "HEAD"))
            return (405, new
            {
                error = "This little path accepts GET and HEAD."
            });
        if (uri.AbsolutePath == "/v1/harvest")
        {
            var season = uri.Query.TrimStart('?').Split('&').Select(q => q.Split('=', 2))
                .FirstOrDefault(q => q.Length == 2 && q[0] == "season")?.ElementAt(1) ?? "spring";
            season = Uri.UnescapeDataString(season.Replace('+', ' '));
            var crops = season.ToLowerInvariant() switch
            {
                "summer" => new[] { new Crop("blueberry", 50, 13), new Crop("melon", 250, 12), new Crop("tomato", 60, 11) },
                "fall" or "autumn" => [new Crop("pumpkin", 320, 13), new Crop("cranberry", 75, 7), new Crop("yam", 160, 10)],
                "winter" => [new Crop("winter root", 70, 7), new Crop("crystal fruit", 150, 7)],
                _ => [new Crop("strawberry", 120, 8), new Crop("cauliflower", 175, 12), new Crop("parsnip", 35, 4)]
            };
            return (200, new
            {
                season,
                farm = "Sunbeam Acres",
                crops,
                total = crops.Length,
                message = "Good things take a little growing."
            });
        }
        if (uri.AbsolutePath == "/v1/villagers")
            return (200, new
            {
                villagers = new[] { new { name = "Maple", role = "beekeeper", favorite = "wildflower honey" }, new { name = "Clover", role = "gardener", favorite = "strawberries" } },
                message = "There is always room for a new friend."
            });
        if (uri.AbsolutePath == "/v1/health")
            return (200, new
            {
                status = "growing nicely",
                sunshine = true,
                version = "1.0.0"
            });
        return (404, new
        {
            error = "This path has not been planted yet.",
            paths = new[] { "/v1/harvest", "/v1/villagers", "/v1/plant", "/v1/health" }
        });
    }

    private sealed record Crop(string name, int sell_price, int growth_days);
    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
    }
}
