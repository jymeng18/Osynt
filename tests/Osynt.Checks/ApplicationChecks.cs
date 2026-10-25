using Osynt.Application.Workspaces;
using Osynt.Checks.Fakes;
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
            var application = typeof(WorkspaceService).Assembly.GetReferencedAssemblies();
            True(application.All(a => !a.Name!.StartsWith("Avalonia") && a.Name is not ("Osynt.Infrastructure" or "Osynt.Desktop")));
        });
        Check("Failed saves leave application state unchanged and can be retried", () =>
        {
            var store = new InMemoryWorkspaceStore(StarterWorkspace.Create());
            var workspace = new WorkspaceService(store, new JsonCollectionSerializer(), "http://127.0.0.1:10001");
            var request = workspace.Collections[0].Requests[0].Clone();
            var original = request.Name;
            request.Name = "A retryable change";
            store.FailWrites = true;
            Throws<IOException>(() => workspace.SaveRequest(request));
            Equal(original, workspace.Collections[0].Requests[0].Name);
            store.FailWrites = false;
            workspace.SaveRequest(request);
            Equal("A retryable change", store.State!.Collections[0].Requests[0].Name);
        });
        Check("Custom localhost environments survive restarting the app", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new WorkspaceService(store, new JsonCollectionSerializer(), "http://127.0.0.1:10001");
            workspace.UpdateVariables([new() { Key = "base_url", Value = "http://localhost:5057" }]);
            var restarted = new WorkspaceService(store, new JsonCollectionSerializer(), "http://127.0.0.1:10002");
            Equal("http://localhost:5057", restarted.Variables["base_url"]);
        });
        Check("The local demo environment follows its new port after a restart", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new WorkspaceService(store, new JsonCollectionSerializer(), "http://127.0.0.1:10001");
            workspace.UpdateVariables([new() { Key = "base_url", Value = workspace.DemoBaseUrl }]);
            var restarted = new WorkspaceService(store, new JsonCollectionSerializer(), "http://127.0.0.1:10002");
            Equal(restarted.DemoBaseUrl, restarted.Variables["base_url"]);
        });
        Check("Application history keeps the latest 100 independent snapshots", () =>
        {
            var workspace = new WorkspaceService(new InMemoryWorkspaceStore(), new JsonCollectionSerializer(), "");
            var request = new RequestDocument();
            for (var index = 0; index < 105; index++)
            {
                request.Name = $"Request {index}";
                workspace.RecordHistory(request, 200, index);
            }
            request.Name = "Edited later";
            Equal(100, workspace.History.Count);
            Equal("Request 104", workspace.History[0].Request.Name);
            Equal("Request 5", workspace.History[^1].Request.Name);
        });
    }
}
