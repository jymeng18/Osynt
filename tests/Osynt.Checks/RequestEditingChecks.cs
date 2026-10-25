using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Http;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunRequestEditingChecks()
    {
        Check("Pasting a new endpoint removes demo parameters from the old URL", () =>
        {
            using var editor = new RequestEditorViewModel(new()
            {
                Url = "{{base_url}}/v1/harvest",
                Parameters = [new() { Key = "season", Value = "{{season}}" }]
            });
            editor.Url = "http://localhost:8080/health";
            Equal(0, editor.Parameters.Count);
            using var request = HttpRequestFactory.Create(editor.Clone(), new Dictionary<string, string>());
            Equal("", request.RequestUri!.Query);
        });
        Check("Pasted query parameters appear in the table and edits update the URL", () =>
        {
            using var editor = new RequestEditorViewModel(new());
            editor.Url = "http://localhost:8080/items?page=1&name=Maple+Farm#section";
            Equal(2, editor.Parameters.Count);
            Equal("Maple Farm", editor.Parameters[1].Value);
            editor.Parameters[0].Value = "2";
            True(editor.Url.Contains("page=2&name=Maple%20Farm#section"));
            using var request = HttpRequestFactory.Create(editor.Clone(), new Dictionary<string, string>());
            Equal("?page=2&name=Maple%20Farm", request.RequestUri!.Query);
        });
        Check("Disabling and removing parameters updates the URL without losing other rows", () =>
        {
            using var editor = new RequestEditorViewModel(new()
            {
                Url = "http://localhost/items?page=1&sort=name"
            });
            editor.Parameters[0].Enabled = false;
            True(!editor.Url.Contains("page="));
            editor.Parameters[0].Enabled = true;
            True(editor.Url.Contains("page=1"));
            editor.Parameters.RemoveAt(1);
            Equal("http://localhost/items?page=1", editor.Url);
        });
        Check("Repeated query keys are sent once for each enabled row", () =>
        {
            using var editor = new RequestEditorViewModel(new()
            {
                Url = "http://localhost/items?tag=a&tag=b"
            });
            using var request = HttpRequestFactory.Create(editor.Clone(), new Dictionary<string, string>());
            Equal("?tag=a&tag=b", request.RequestUri!.Query);
        });
        Check("Template query values cannot inject additional query parameters", () =>
        {
            using var editor = new RequestEditorViewModel(new()
            {
                Url = "http://localhost/items?q={{term}}"
            });
            using var request = HttpRequestFactory.Create(editor.Clone(), new Dictionary<string, string> { ["term"] = "a&b = café" });
            Equal("?q=a%26b%20%3D%20caf%C3%A9", request.RequestUri!.Query);
        });
        Check("Local URLs without a scheme use HTTP and keep their port", () =>
        {
            using var request = HttpRequestFactory.Create(new()
            {
                Url = "localhost:8080/health"
            }, new Dictionary<string, string>());
            Equal("http://localhost:8080/health", request.RequestUri!.AbsoluteUri);
        });
        Check("Automatic headers respect explicit overrides and disabled rows", () =>
        {
            using var request = HttpRequestFactory.Create(new()
            {
                Url = "http://localhost:8080/health",
                Headers = [new() { Key = " Accept ", Value = "application/json" }, new() { Key = "User-Agent", Enabled = false }]
            }, new Dictionary<string, string>());
            Equal("application/json", request.Headers.Accept.ToString());
            True(!request.Headers.Contains("User-Agent"));
            True(request.Content is null);
        });
    }
}
