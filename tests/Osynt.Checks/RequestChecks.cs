using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Application.Workspaces;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;
using Osynt.Desktop;
using Osynt.Desktop.ViewModels;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunRequestChecks(DemoServer demo, RequestExecutor executor, Dictionary<string, string> variables)
    {
        Check("Variables resolve in URLs and values", () => Equal(demo.BaseUrl + "/summer", TemplateResolver.Resolve("{{ base_url }}/{{season}}", variables)));
        Check("Missing variables produce an actionable error", () => Throws<ArgumentException>(() => TemplateResolver.Resolve("{{missing}}", variables)));
        Check("Only HTTP and HTTPS URLs are accepted", () => Throws<ArgumentException>(() => HttpRequestFactory.Create(new() { Url = "file:///etc/passwd" }, variables)));
        Check("Query parameters preserve existing queries, fragments, duplicates, and escaping", () =>
        {
            using var request = HttpRequestFactory.Create(new()
            {
                Url = "https://example.com/path?first=1#anchor",
                Parameters = [new() { Key = "q", Value = "a & b" }, new() { Key = "q", Value = "two" }, new() { Enabled = false, Key = "skip", Value = "me" }]
            }, variables);
            Equal("?first=1&q=a%20%26%20b&q=two", request.RequestUri!.Query);
            Equal("#anchor", request.RequestUri.Fragment);
        });
        Check("JSON bodies and content headers are sent correctly", () =>
        {
            using var request = HttpRequestFactory.Create(new()
            {
                Method = "POST",
                Url = "{{base_url}}/v1/plant",
                Body = "{\"season\":\"{{season}}\"}",
                Headers = [new() { Key = "Content-Type", Value = "application/problem+json" }, new() { Key = "X-Season", Value = "{{season}}" }, new() { Key = "X-Skip", Value = "no", Enabled = false }]
            }, variables);
            Equal("application/problem+json", request.Content!.Headers.ContentType!.MediaType);
            Equal("{\"season\":\"summer\"}", request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            Equal("summer", request.Headers.GetValues("X-Season").Single());
            True(!request.Headers.Contains("X-Skip"));
        });
        Check("Header line breaks are rejected", () => Throws<ArgumentException>(() => HttpRequestFactory.Create(new() { Url = demo.BaseUrl, Headers = [new() { Key = "X-Test", Value = "x\r\nInjected: yes" }] }, variables)));
        Check("Local GET returns real seasonal data and headers", () =>
        {
            var response = executor.SendAsync(new()
            {
                Url = "{{base_url}}/v1/harvest",
                Parameters = [new() { Key = "season", Value = "{{season}}" }]
            }, variables).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            True(response.Body.Contains("blueberry"));
            True(response.Headers.Any(h => h.Key == "X-Osynt-Demo" && h.Value == "true"));
            Equal(Encoding.UTF8.GetByteCount(response.Body), (int)response.SizeBytes);
        });
        Check("Local POST returns 201 with the submitted crop", () =>
        {
            var response = executor.SendAsync(new()
            {
                Method = "POST",
                Url = "{{base_url}}/v1/plant",
                Body = "{\"crop\":\"strawberry\",\"quantity\":12}"
            }, variables).GetAwaiter().GetResult();
            Equal(201, response.StatusCode);
            True(response.Body.Contains("strawberry"));
        });
        Check("Invalid JSON and missing paths display HTTP error responses", () =>
        {
            Equal(400, executor.SendAsync(new()
            {
                Method = "POST",
                Url = demo.BaseUrl + "/v1/plant",
                Body = "oops"
            }, variables).GetAwaiter().GetResult().StatusCode);
            Equal(404, executor.SendAsync(new()
            {
                Url = demo.BaseUrl + "/missing"
            }, variables).GetAwaiter().GetResult().StatusCode);
        });
        Check("Response preview is bounded to 5 MB", () =>
        {
            using var largeClient = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', RequestExecutor.MaxResponseBytes + 500)) })));
            var response = new RequestExecutor(largeClient).SendAsync(new()
            {
                Url = "https://example.com"
            }, variables).GetAwaiter().GetResult();
            True(response.Truncated);
            Equal(RequestExecutor.MaxResponseBytes, response.Body.Length);
        });
        Check("Cancellation interrupts an in-flight request", () =>
        {
            using var slowClient = new HttpClient(new StubHandler(async token => { await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); }));
            using var cancellation = new CancellationTokenSource(40);
            Throws<OperationCanceledException>(() => new RequestExecutor(slowClient).SendAsync(new() { Url = "https://example.com" }, variables, cancellation.Token).GetAwaiter().GetResult());
        });
    }
}
