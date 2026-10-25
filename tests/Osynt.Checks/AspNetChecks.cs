using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Osynt.Checks.Fixtures;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Http;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunAspNetChecks(RequestExecutor executor)
    {
        using var api = new AspNetApiFixture();
        Check("ASP.NET health endpoint works with a default GET and no parameters", () =>
        {
            var response = executor.SendAsync(new()
            {
                Url = api.BaseUrl + "/health"
            }, new Dictionary<string, string>()).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            Equal("", response.Body);
            using var tab = new RequestTab(new() { Url = api.BaseUrl + "/health" }) { Response = response };
            True(tab.HasResponse && tab.EmptyResponseBody && !tab.ShowResponseBody);
        });
        Check("ASP.NET query binding receives synchronized parameters once", () =>
        {
            using var editor = new RequestEditorViewModel(new()
            {
                Url = api.BaseUrl + "/query?name=Maple&count=2"
            });
            editor.Parameters[0].Value = "Maple & Clover";
            var response = executor.SendAsync(editor.Clone(), new Dictionary<string, string>()).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            using var body = JsonDocument.Parse(response.Body);
            Equal("Maple & Clover", body.RootElement.GetProperty("name").GetString());
            Equal(2, body.RootElement.GetProperty("count").GetInt32());
        });
        Check("ASP.NET receives automatic headers and no GET body", () =>
        {
            var response = executor.SendAsync(new()
            {
                Url = api.BaseUrl + "/headers"
            }, new Dictionary<string, string>()).GetAwaiter().GetResult();
            using var body = JsonDocument.Parse(response.Body);
            Equal("*/*", body.RootElement.GetProperty("accept").GetString());
            True(body.RootElement.GetProperty("userAgent").GetString()!.StartsWith("Osynt/"));
            True(!body.RootElement.GetProperty("hasBody").GetBoolean());
        });
        Check("ASP.NET binds a JSON body without manually configured headers", () =>
        {
            var response = executor.SendAsync(new()
            {
                Url = api.BaseUrl + "/echo",
                Method = "POST",
                Body = "{\"crop\":\"parsnip\"}"
            }, new Dictionary<string, string>()).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            True(response.Body.Contains("parsnip"));
        });
        Check("HTTP redirects are followed automatically", () =>
        {
            var response = executor.SendAsync(new()
            {
                Url = api.BaseUrl + "/redirect"
            }, new Dictionary<string, string>()).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
        });
        Check("Connection refusal reports the actual host and port", () =>
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            try
            {
                executor.SendAsync(new()
                {
                    Url = $"http://127.0.0.1:{port}/health"
                }, new Dictionary<string, string>()).GetAwaiter().GetResult();
                throw new InvalidOperationException("Expected a connection failure.");
            }
            catch (HttpRequestException error)
            {
                True(error.Message.Contains($"127.0.0.1:{port}"));
                True(error.Message.Contains("Nothing is listening"));
            }
        });
    }
}
