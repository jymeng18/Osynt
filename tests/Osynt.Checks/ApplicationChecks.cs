using Osynt.Application.Workspaces;
using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunApplicationChecks()
    {
        Check("Domain and application assemblies preserve inward dependencies", () =>
        {
            var domain = typeof(RequestDocument).Assembly.GetReferencedAssemblies();
            True(domain.All(a => !a.Name!.StartsWith("Osynt.") && !a.Name.StartsWith("Avalonia")));
            var application = typeof(WorkspaceSession).Assembly.GetReferencedAssemblies();
            True(application.All(a => !a.Name!.StartsWith("Avalonia") && a.Name is not ("Osynt.Infrastructure" or "Osynt.Desktop" or "System.Net.Http")));
            var infrastructure = typeof(WorkspaceStore).Assembly.GetReferencedAssemblies();
            True(infrastructure.All(a => !a.Name!.StartsWith("Avalonia") && a.Name != "Osynt.Desktop"));
        });
        Check("Failed saves leave application state unchanged and can be retried", () =>
        {
            var store = new InMemoryWorkspaceStore(StarterWorkspace.Create());
            var workspace = new ApplicationWorkspace(store, "http://127.0.0.1:10001");
            var request = workspace.Collections.GetCollections()[0].Requests[0].Clone();
            var original = request.Name;
            request.Name = "A retryable change";
            store.FailWrites = true;
            Throws<IOException>(() => workspace.Collections.SaveRequest(request));
            Equal(original, workspace.Collections.GetCollections()[0].Requests[0].Name);
            store.FailWrites = false;
            workspace.Collections.SaveRequest(request);
            Equal("A retryable change", store.State!.Collections[0].Requests[0].Name);
        });
        Check("Custom localhost environments survive restarting the app", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store, "http://127.0.0.1:10001");
            workspace.Environment.Update([new() { Key = "base_url", Value = "http://localhost:5057" }]);
            var restarted = new ApplicationWorkspace(store, "http://127.0.0.1:10002");
            Equal("http://localhost:5057", restarted.Environment.GetVariables()["base_url"]);
        });
        Check("The local demo environment follows its new port after a restart", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store, "http://127.0.0.1:10001");
            workspace.Environment.Update([new() { Key = "base_url", Value = workspace.Environment.DemoBaseUrl }]);
            var restarted = new ApplicationWorkspace(store, "http://127.0.0.1:10002");
            Equal(restarted.Environment.DemoBaseUrl, restarted.Environment.GetVariables()["base_url"]);
        });
        Check("Application history keeps the latest 100 independent snapshots", () =>
        {
            var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore(), "");
            var request = new RequestDocument();
            for (var index = 0; index < 105; index++)
            {
                request.Name = $"Request {index}";
                workspace.History.Record(request, 200, index);
            }
            request.Name = "Edited later";
            Equal(100, workspace.History.GetHistory().Count);
            Equal("Request 104", workspace.History.GetHistory()[0].Request.Name);
            Equal("Request 5", workspace.History.GetHistory()[^1].Request.Name);
        });
        Check("Read results cannot mutate workspace collections, history, environment, or garden", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            workspace.History.Record(new RequestDocument { Name = "Recorded" }, 200, 10);
            var collections = workspace.Collections.GetCollections();
            var original = collections[0].Requests[0].Name;
            collections[0].Requests[0].Name = "Changed outside a commit";
            collections[0].Requests.Clear();
            workspace.History.GetHistory()[0].Request.Name = "Changed outside a commit";
            ((IDictionary<string, string>)workspace.Environment.GetVariables())["season"] = "winter";
            workspace.Garden.GetState().SuccessfulRequests = 999;
            Equal(original, workspace.Collections.GetCollections()[0].Requests[0].Name);
            Equal("Recorded", workspace.History.GetHistory()[0].Request.Name);
            Equal("spring", workspace.Environment.GetVariables()["season"]);
            Equal(1L, workspace.Garden.GetState().SuccessfulRequests);
            Equal(original, store.State!.Collections[0].Requests[0].Name);
        });
        Check("Feature services commit to the same current state and roll back failed changes", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            workspace.Environment.Update([new() { Key = "season", Value = "summer" }]);
            var request = new RequestDocument { Name = "Saved" };
            workspace.Collections.SaveRequest(request);
            workspace.History.Record(request, 201, 12);
            workspace.Garden.UpdatePreferences("dusk", true);
            store.FailWrites = true;
            Throws<IOException>(() => workspace.History.Clear());
            Throws<IOException>(() => workspace.Garden.UpdatePreferences("day", false));
            Throws<IOException>(() => workspace.Environment.Update([new() { Key = "season", Value = "winter" }]));
            Throws<IOException>(() => workspace.Collections.Import(workspace.Collections.Export()));
            var restarted = new ApplicationWorkspace(store);
            Equal("summer", restarted.Environment.GetVariables()["season"]);
            Equal(3, restarted.Collections.GetCollections().Count);
            Equal(request.Id, restarted.Collections.GetCollections()[2].Requests.Single().Id);
            Equal(1, restarted.History.GetHistory().Count);
            Equal(1L, restarted.Garden.GetState().SuccessfulRequests);
            Equal("dusk", restarted.Garden.GetState().Atmosphere);
            Equal("dusk", workspace.Garden.GetState().Atmosphere);
            Equal("summer", workspace.Environment.GetVariables()["season"]);
            Equal(1, workspace.History.GetHistory().Count);
            Equal(3, workspace.Collections.GetCollections().Count);
        });
    }
}
