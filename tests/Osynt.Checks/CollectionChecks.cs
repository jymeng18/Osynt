using Osynt.Application.Workspaces;
using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunCollectionChecks(string directory)
    {
        Check("Renaming a folder preserves its identity, requests, and name across restarts", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "rename"));
            var workspace = new ApplicationWorkspace(store);
            var folder = workspace.Collections.GetCollections()[0];
            var requestIds = folder.Requests.Select(r => r.Id).ToArray();
            workspace.Collections.Rename(folder.Id, "  Renamed API  ");
            var restarted = new ApplicationWorkspace(store);
            var renamed = restarted.Collections.GetCollections().Single(c => c.Id == folder.Id);
            Equal("Renamed API", renamed.Name);
            True(requestIds.SequenceEqual(renamed.Requests.Select(r => r.Id)));
            var edited = renamed.Requests[0].Clone();
            edited.Name = "Updated request";
            restarted.Collections.SaveRequest(edited);
            Equal("Updated request", restarted.Collections.GetCollections().Single(c => c.Id == folder.Id).Requests[0].Name);
        });
        Check("New requests continue using the renamed default folder after restarting", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            workspace.Collections.SaveRequest(new RequestDocument());
            var folder = workspace.Collections.GetCollections().Single(c => c.Name == "My requests");
            workspace.Collections.Rename(folder.Id, "My API");
            var restarted = new ApplicationWorkspace(store);
            restarted.Collections.SaveRequest(new RequestDocument());
            Equal(3, restarted.Collections.GetCollections().Count);
            Equal(2, restarted.Collections.GetCollections().Single(c => c.Id == folder.Id).Requests.Count);
        });
        Check("Older folders without IDs can be renamed and retain their save destination", () =>
        {
            var path = Path.Combine(directory, "legacy-folder");
            Directory.CreateDirectory(path);
            var store = new WorkspaceStore(path);
            File.WriteAllText(store.FilePath, """{"Collections":[{"Name":"My requests","Requests":[]}]}""");
            var workspace = new ApplicationWorkspace(store);
            var folder = workspace.Collections.GetCollections().Single();
            True(folder.Id != Guid.Empty);
            workspace.Collections.Rename(folder.Id, "Legacy API");
            var restarted = new ApplicationWorkspace(store);
            restarted.Collections.SaveRequest(new RequestDocument());
            var renamed = restarted.Collections.GetCollections().Single();
            Equal(folder.Id, renamed.Id);
            Equal("Legacy API", renamed.Name);
            Equal(1, renamed.Requests.Count);
        });
        Check("Blank names, missing folders, and failed saves leave folder names unchanged", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            var folder = workspace.Collections.GetCollections()[0];
            Throws<ArgumentException>(() => workspace.Collections.Rename(folder.Id, "  "));
            Throws<InvalidOperationException>(() => workspace.Collections.Rename(Guid.NewGuid(), "Missing"));
            store.FailWrites = true;
            Throws<IOException>(() => workspace.Collections.Rename(folder.Id, "Failed rename"));
            Equal(folder.Name, workspace.Collections.GetCollections()[0].Name);
        });
        Check("Folders with the same name remain independent and imports get fresh folder IDs", () =>
        {
            var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore());
            var folders = workspace.Collections.GetCollections();
            workspace.Collections.Rename(folders[1].Id, folders[0].Name);
            Equal(folders[0].Name, workspace.Collections.GetCollections()[1].Name);
            workspace.Collections.Rename(folders[1].Id, "Second folder");
            Equal(folders[0].Name, workspace.Collections.GetCollections()[0].Name);
            workspace.Collections.Import(workspace.Collections.Export());
            var ids = workspace.Collections.GetCollections().Select(c => c.Id).ToList();
            Equal(ids.Count, ids.Distinct().Count());
        });
    }
}
