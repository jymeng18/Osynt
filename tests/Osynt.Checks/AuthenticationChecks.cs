using System.Text;
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
    private static void RunAuthenticationChecks(string directory, RequestExecutor executor)
    {
        using var api = new AspNetApiFixture();
        var variables = new Dictionary<string, string> { ["token"] = "example-token", ["username"] = "Café", ["password"] = "secret:with:colons" };
        Check("Bearer and Basic authentication resolve variables and reach the server correctly", () =>
        {
            var bearer = executor.SendAsync(new()
            {
                Url = api.BaseUrl + "/auth",
                Authentication = new()
                {
                    Mode = RequestAuthentication.Bearer,
                    Token = "{{token}}"
                }
            }, variables).GetAwaiter().GetResult();
            Equal("Bearer example-token", bearer.Body);
            var basic = executor.SendAsync(new()
            {
                Url = api.BaseUrl + "/auth",
                Authentication = new()
                {
                    Mode = RequestAuthentication.Basic,
                    Username = "{{username}}",
                    Password = "{{password}}"
                }
            }, variables).GetAwaiter().GetResult();
            Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("Café:secret:with:colons")), basic.Body);
        });
        Check("Explicit and disabled Authorization rows override the helper without resolving unused credentials", () =>
        {
            var document = new RequestDocument
            {
                Url = api.BaseUrl,
                Authentication = new() { Mode = RequestAuthentication.Bearer, Token = "{{missing}}" },
                Headers = [new() { Key = "Authorization", Value = "Custom token" }]
            };
            using var custom = HttpRequestFactory.Create(document, variables);
            Equal("Custom token", custom.Headers.GetValues("Authorization").Single());
            document.Headers[0].Enabled = false;
            using var disabled = HttpRequestFactory.Create(document, variables);
            True(!disabled.Headers.Contains("Authorization"));
            document.Headers.Clear();
            document.Authentication.Mode = RequestAuthentication.None;
            using var none = HttpRequestFactory.Create(document, variables);
            True(none.Headers.Authorization is null);
        });
        Check("Invalid authentication input is rejected without echoing secrets in errors", () =>
        {
            foreach (var authentication in new RequestAuthentication[]
            {
                new() { Mode = RequestAuthentication.Bearer, Token = "" },
                new() { Mode = RequestAuthentication.Bearer, Token = "secret with spaces" },
                new() { Mode = RequestAuthentication.Bearer, Token = "secret\r\nInjected: yes" },
                new() { Mode = RequestAuthentication.Basic, Username = "secret:invalid" },
                new() { Mode = RequestAuthentication.Basic, Password = "secret\ninvalid" },
                new() { Mode = "Unknown" }
            })
            {
                try
                {
                    using var invalid = HttpRequestFactory.Create(new()
                    {
                        Url = api.BaseUrl,
                        Authentication = authentication
                    }, variables);
                    throw new InvalidOperationException("Expected invalid credentials to fail.");
                }
                catch (ArgumentException error) { True(!error.Message.Contains("secret")); }
            }
        });
        Check("Authentication edits stay isolated and persist through workspace and collection round trips", () =>
        {
            var model = new RequestDocument();
            using var tab = new RequestTab(model);
            tab.Document.AuthMode = RequestAuthentication.Basic;
            tab.Document.AuthUsername = "{{username}}";
            tab.Document.AuthPassword = "{{password}}";
            True(tab.IsDirty && tab.Document.BasicAuth);
            Equal(RequestAuthentication.None, model.Authentication.Mode);
            var clone = tab.Document.Clone();
            var copy = clone.Clone();
            copy.Authentication.Password = "changed";
            Equal("{{password}}", clone.Authentication.Password);
            var state = new WorkspaceState { Collections = [new() { Requests = [clone] }] };
            var store = new WorkspaceStore(Path.Combine(directory, "auth"));
            store.Save(state);
            Equal("{{password}}", store.Load()!.Collections[0].Requests[0].Authentication.Password);
            var serializer = new JsonCollectionSerializer();
            var imported = serializer.Deserialize(serializer.Serialize(state.Collections))[0].Requests[0];
            using var editor = new RequestEditorViewModel(imported);
            Equal(RequestAuthentication.Basic, editor.AuthMode);
            Equal("{{username}}", editor.AuthUsername);
            var old = serializer.Deserialize("[{\"Name\":\"Old\",\"Requests\":[{}]}]")[0].Requests[0];
            Equal(RequestAuthentication.None, old.Authentication.Mode);
            Throws<JsonException>(() => serializer.Deserialize("[{\"Name\":\"Bad\",\"Requests\":[{\"Authentication\":null}]}]"));
        });
    }
}
