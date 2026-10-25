using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunDeletionChecks(string directory)
    {
        Check("Deleting a request persists, keeps its folder, and preserves history and garden progress", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "delete-request"));
            var workspace = new ApplicationWorkspace(store);
            var folder = workspace.Collections.GetCollections()[0];
            var request = folder.Requests[0];
            workspace.History.Record(request, 200, 10);
            workspace.Collections.DeleteRequest(request.Id);
            var restarted = new ApplicationWorkspace(store);
            var remaining = restarted.Collections.GetCollections().Single(c => c.Id == folder.Id);
            Equal(folder.Requests.Count - 1, remaining.Requests.Count);
            True(remaining.Requests.All(r => r.Id != request.Id));
            Equal(request.Id, restarted.History.GetHistory().Single().Request.Id);
            Equal(1L, restarted.Garden.GetState().SuccessfulRequests);
        });
        Check("Deleting a folder removes all its requests and preserves folders with the same name", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            var folders = workspace.Collections.GetCollections();
            workspace.Collections.Rename(folders[1].Id, folders[0].Name);
            var removed = workspace.Collections.DeleteCollection(folders[0].Id);
            True(folders[0].Requests.Select(r => r.Id).ToHashSet().SetEquals(removed));
            var restarted = new ApplicationWorkspace(store);
            Equal(folders[1].Id, restarted.Collections.GetCollections().Single().Id);
        });
        Check("Deleting the default folder allows new requests to be saved to a new folder", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            workspace.Collections.SaveRequest(new RequestDocument());
            var folder = workspace.Collections.GetCollections().Single(c => c.Name == "My requests");
            workspace.Collections.Rename(folder.Id, "My API");
            workspace.Collections.DeleteCollection(folder.Id);
            True(store.State!.DefaultCollectionId is null);
            var restarted = new ApplicationWorkspace(store);
            var request = new RequestDocument();
            restarted.Collections.SaveRequest(request);
            var replacement = restarted.Collections.GetCollections().Single(c => c.Name == "My requests");
            True(replacement.Id != folder.Id);
            Equal(request.Id, replacement.Requests.Single().Id);
        });
        Check("Deleting the last request leaves an empty folder that can also be deleted", () =>
        {
            var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore());
            var folder = workspace.Collections.GetCollections()[1];
            workspace.Collections.DeleteRequest(folder.Requests.Single().Id);
            Equal(0, workspace.Collections.GetCollections().Single(c => c.Id == folder.Id).Requests.Count);
            Equal(0, workspace.Collections.DeleteCollection(folder.Id).Count);
            True(workspace.Collections.GetCollections().All(c => c.Id != folder.Id));
        });
        Check("Failed request and folder deletions leave the saved workspace unchanged", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            workspace.Collections.SaveRequest(new RequestDocument());
            var folder = workspace.Collections.GetCollections().Single(c => c.Name == "My requests");
            store.FailWrites = true;
            Throws<IOException>(() => workspace.Collections.DeleteRequest(folder.Requests.Single().Id));
            Throws<IOException>(() => workspace.Collections.DeleteCollection(folder.Id));
            Equal(folder.Requests.Single().Id, workspace.Collections.GetCollections().Single(c => c.Id == folder.Id).Requests.Single().Id);
            Equal(folder.Id, store.State!.DefaultCollectionId);
        });
    }
}
