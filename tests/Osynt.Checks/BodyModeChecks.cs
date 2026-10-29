using System.Text.Json;
using Osynt.Checks.Fixtures;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunBodyModeChecks(string directory, RequestExecutor executor)
    {
        using var api = new AspNetApiFixture();
        var variables = new Dictionary<string, string> { ["message"] = "café & berries=ripe + sweet" };
        Check("Plain text bodies arrive as UTF-8 text with resolved variables", () =>
        {
            var response = executor.SendAsync(new()
            {
                Method = "POST",
                Url = api.BaseUrl + "/text",
                BodyMode = RequestBodyModes.Text,
                Body = "Hello {{message}}"
            }, variables).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            using var body = JsonDocument.Parse(response.Body);
            Equal("text/plain; charset=utf-8", body.RootElement.GetProperty("contentType").GetString());
            Equal("Hello " + variables["message"], body.RootElement.GetProperty("body").GetString());
        });
        Check("Form requests preserve duplicate keys, Unicode, disabled fields, and encoded template values", () =>
        {
            var response = executor.SendAsync(new()
            {
                Method = "POST",
                Url = api.BaseUrl + "/form",
                BodyMode = RequestBodyModes.Form,
                FormFields = [new() { Key = "crop", Value = "{{message}}" }, new() { Key = "crop", Value = "two" },
                    new() { Key = "empty", Value = "" }, new() { Key = "skip", Value = "{{missing}}", Enabled = false }]
            }, variables).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            using var body = JsonDocument.Parse(response.Body);
            Equal(variables["message"], body.RootElement.GetProperty("crop")[0].GetString());
            Equal("two", body.RootElement.GetProperty("crop")[1].GetString());
            Equal("", body.RootElement.GetProperty("empty")[0].GetString());
            True(!body.RootElement.TryGetProperty("skip", out _));
        });
        Check("No-body mode ignores retained drafts and custom content types still override defaults", () =>
        {
            using var none = HttpRequestFactory.Create(new()
            {
                Url = api.BaseUrl,
                BodyMode = RequestBodyModes.None,
                Body = "{{missing}}",
                FormFields = [new() { Key = "unused", Value = "{{missing}}" }]
            }, variables);
            True(none.Content is null);
            using var custom = HttpRequestFactory.Create(new()
            {
                Url = api.BaseUrl,
                BodyMode = RequestBodyModes.Text,
                Body = "<hello />",
                Headers = [new() { Key = "Content-Type", Value = "application/xml" }]
            }, variables);
            Equal("application/xml", custom.Content!.Headers.ContentType!.MediaType);
        });
        Check("Body format and fields survive persistence, export, and independent editor clones", () =>
        {
            using var tab = new RequestTab(new());
            tab.Document.BodyMode = RequestBodyModes.Form;
            tab.Document.FormFields.Add(new()
            {
                Key = "name",
                Value = "Maple"
            });
            tab.IsDirty = false;
            tab.Document.FormFields[0].Value = "Clover";
            True(tab.IsDirty && tab.HasBody);
            var request = tab.Document.Clone();
            request.FormFields[0].Value = "Different";
            Equal("Clover", tab.Document.FormFields[0].Value);
            var state = new WorkspaceState { Collections = [new() { Requests = [request] }] };
            var store = new WorkspaceStore(Path.Combine(directory, "body-formats"));
            store.Save(state);
            Equal(RequestBodyModes.Form, store.Load()!.Collections[0].Requests[0].BodyMode);
            var serializer = new JsonCollectionSerializer();
            var imported = serializer.Deserialize(serializer.Serialize(state.Collections))[0].Requests[0];
            Equal("Different", imported.FormFields[0].Value);
            using var restored = new RequestEditorViewModel(imported);
            True(restored.FormBody);
            Equal("Different", restored.FormFields[0].Value);
            tab.Document.FormFields[0].Enabled = false;
            True(!tab.HasBody);
        });
        Check("Older collections default to JSON and unknown body modes fail validation", () =>
        {
            var serializer = new JsonCollectionSerializer();
            var old = serializer.Deserialize("[{\"Name\":\"Old\",\"Requests\":[{}]}]")[0].Requests[0];
            Equal(RequestBodyModes.Json, old.BodyMode);
            Equal(0, old.FormFields.Count);
            Throws<JsonException>(() => serializer.Deserialize("[{\"Name\":\"Bad\",\"Requests\":[{\"BodyMode\":\"Unknown\"}]}]"));
            Throws<ArgumentException>(() => HttpRequestFactory.Create(new() { Url = api.BaseUrl, BodyMode = "Unknown" }, variables));
        });
    }
}
