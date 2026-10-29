using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Checks.Fixtures;
using Osynt.Desktop;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunCurlChecks(RequestExecutor executor)
    {
        var serializer = new CurlRequestSerializer();
        var variables = new Dictionary<string, string> { ["host"] = "https://example.com", ["token"] = "test-token" };
        Check("cURL import supports continuations, attached flags, JSON, duplicate headers, Basic auth, and timeouts", () =>
        {
            var document = serializer.Deserialize("curl -sSL -XPOST \\\n --url 'https://example.com/items?tag=a&tag=b' -H 'X-Tag: first' -H'X-Tag: second' --json '{\"crop\":\"café\"}' -u 'user:pass:word' --max-time=90");
            Equal("POST", document.Method);
            Equal(90, document.TimeoutSeconds);
            Equal(RequestBodyModes.Json, document.BodyMode);
            Equal("pass:word", document.Authentication.Password);
            using var request = HttpRequestFactory.Create(document, variables);
            Equal("?tag=a&tag=b", request.RequestUri!.Query);
            Equal("first,second", string.Join(',', request.Headers.GetValues("X-Tag")));
            Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            Equal("application/json", request.Headers.Accept.ToString());
        });
        Check("Imported JSON and URL-encoded cURL requests reach a real API with the intended payload", () =>
        {
            using var api = new AspNetApiFixture();
            var json = serializer.Deserialize($"curl '{api.BaseUrl}/echo' --json '{{\"crop\":\"café\"}}'");
            var response = executor.SendAsync(json, variables).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            True(response.Body.Contains("café") || response.Body.Contains("caf\\u00E9"));
            var form = serializer.Deserialize($"curl '{api.BaseUrl}/form' --data-urlencode 'crop=café & ripe' -d crop=second");
            response = executor.SendAsync(form, variables).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            using var body = JsonDocument.Parse(response.Body);
            Equal("café & ripe", body.RootElement.GetProperty("crop")[0].GetString());
            Equal("second", body.RootElement.GetProperty("crop")[1].GetString());
        });
        Check("cURL --get appends data to existing query strings and keeps fragments and HEAD semantics", () =>
        {
            var document = serializer.Deserialize("curl -GI 'https://example.com/items?first=1#anchor' --data-urlencode 'q=a & b'");
            Equal("HEAD", document.Method);
            Equal("", document.Body);
            using var request = HttpRequestFactory.Create(document, variables);
            Equal("?first=1&q=a%20%26%20b", request.RequestUri!.Query);
            Equal("#anchor", request.RequestUri.Fragment);
        });
        Check("cURL export round-trips resolved wire data including quotes, newlines, Unicode, form fields, and auth", () =>
        {
            foreach (var document in new RequestDocument[]
            {
                new() { Method = "PATCH", Url = "{{host}}/items?q=hello", BodyMode = RequestBodyModes.Text,
                    Body = "café's $literal `data`\nsecond line", Headers = [new() { Key = "X-Quote", Value = "it's a test" },
                        new() { Key = "Content-Type", Value = "text/plain; charset=iso-8859-1" }],
                    Authentication = new() { Mode = RequestAuthentication.Bearer, Token = "{{token}}" } },
                new() { Method = "POST", Url = "{{host}}", BodyMode = RequestBodyModes.Form,
                    FormFields = [new() { Key = "crop", Value = "a & b" }, new() { Key = "crop", Value = "café" }] },
                new() { Url = "{{host}}", Headers = [new() { Key = "Accept", Enabled = false }, new() { Key = "User-Agent", Enabled = false }] }
            })
            {
                var command = serializer.SerializeAsync(document, variables).GetAwaiter().GetResult();
                True(!command.Contains("{{"));
                using var expected = HttpRequestFactory.Create(document, variables);
                using var actual = HttpRequestFactory.Create(serializer.Deserialize(command), variables);
                Equal(expected.Method, actual.Method);
                Equal(expected.RequestUri, actual.RequestUri);
                Equal(expected.Headers.ToString(), actual.Headers.ToString());
                Equal(expected.Content?.Headers.ToString(), actual.Content?.Headers.ToString());
                Equal(expected.Content is null ? "" : Convert.ToHexString(expected.Content.ReadAsByteArrayAsync().Result),
                    actual.Content is null ? "" : Convert.ToHexString(actual.Content.ReadAsByteArrayAsync().Result));
            }
        });
        Check("cURL parser rejects unsupported behavior and malformed input without reading files or executing commands", () =>
        {
            foreach (var command in new[] { "", "echo hello", "curl", "curl --url ''", "curl file:///etc/passwd",
                "curl https://example.com --data @/etc/passwd", "curl https://example.com --json @-",
                "curl https://example.com --data-urlencode key@file", "curl https://example.com --insecure",
                "curl https://example.com | cat", "curl https://example.com; echo hello", "curl \"https://$HOST\"",
                "curl https://example.com --header", "curl 'https://example.com", "curl https://a.test https://b.test",
                "curl https://example.com --max-time 0", "curl https://example.com --user user", "curl https://example.com\0" })
                Throws<ArgumentException>(() => serializer.Deserialize(command));
            var literal = serializer.Deserialize("curl https://example.com --data-raw '@literal' -H 'Content-Type:'");
            using var request = HttpRequestFactory.Create(literal, variables);
            Equal("@literal", request.Content!.ReadAsStringAsync().Result);
            True(request.Content.Headers.ContentType is null);
        });
    }

    private static void RunCurlDesktopChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        using var workspace = CreateWorkspace(Path.Combine(directory, "curl-ui"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        Check("cURL dialog imports a dirty tab without sending and preserves pasted text on validation errors", () =>
        {
            var before = workspace.Requests.Tabs.Count;
            var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Import cURL");
            Click(window, button);
            var dialog = window.OwnedWindows.OfType<ImportCurlWindow>().Single();
            var input = dialog.FindControl<TextBox>("CommandInput")!;
            input.Text = "curl --insecure https://example.com";
            Click(dialog, DialogButton(dialog, "Import"));
            True(dialog.FindControl<TextBlock>("ErrorLabel")!.IsEffectivelyVisible);
            Equal(before, workspace.Requests.Tabs.Count);
            Equal("curl --insecure https://example.com", input.Text);
            input.Text = $"curl '{demo.BaseUrl}/v1/health' -H 'X-Test: imported'";
            Click(dialog, DialogButton(dialog, "Import"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            Equal(before + 1, workspace.Requests.Tabs.Count);
            True(workspace.Requests.Selected.IsDirty);
            True(!workspace.Requests.Selected.IsSending && !workspace.Requests.Selected.HasResponse);
            Equal("imported", workspace.Requests.Selected.Document.Headers.Single().Value);
            var export = workspace.Commands.ExportCurlAsync().GetAwaiter().GetResult();
            True(export!.Contains(demo.BaseUrl));
            workspace.Requests.Discard(workspace.Requests.Selected);
        });
        window.Close();
    }
}
