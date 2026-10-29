using System.Text.Json;
using Osynt.Application.Workspaces;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunRecoveryChecks(string directory)
    {
        Check("Atomic workspace replacement retains a backup and recovers corrupt or missing primary files", () =>
        {
            var path = Path.Combine(directory, "recovery");
            var store = new WorkspaceStore(path);
            var state = StarterWorkspace.Create();
            state.Collections[0].Name = "Previous save";
            store.Save(state);
            state.Collections[0].Name = "Latest save";
            store.Save(state);
            Equal("Latest save", store.Load()!.Collections[0].Name);
            File.WriteAllText(store.FilePath, "broken primary");
            var recovered = store.Load()!;
            Equal("Previous save", recovered.Collections[0].Name);
            True(store.LoadNotice!.Contains("recovered"));
            store.Save(recovered);
            True(store.LoadNotice is null);
            Equal("broken primary", File.ReadAllText(Directory.GetFiles(path, "workspace-unreadable-*.json").Single()));
            True(File.ReadAllText(store.BackupFilePath).Contains("Previous save"));
            File.Delete(store.FilePath);
            Equal("Previous save", new WorkspaceStore(path).Load()!.Collections[0].Name);
            Equal(0, Directory.GetFiles(path, "*.tmp").Length);
        });
        Check("Failed workspace replacement leaves the primary intact and cleans its temporary file", () =>
        {
            var path = Path.Combine(directory, "failed-replacement");
            var store = new WorkspaceStore(path);
            var state = StarterWorkspace.Create();
            store.Save(state);
            var before = File.ReadAllText(store.FilePath);
            Directory.CreateDirectory(store.BackupFilePath);
            state.Collections.Clear();
            Throws<IOException>(() => store.Save(state));
            Equal(before, File.ReadAllText(store.FilePath));
            Equal(0, Directory.GetFiles(path, "*.tmp").Length);
        });
        Check("Ambiguous persisted identities are rejected without overwriting saved data", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "invalid-identities"));
            var state = StarterWorkspace.Create();
            store.Save(state);
            var before = File.ReadAllText(store.FilePath);
            state.Collections[1].Requests.Add(state.Collections[0].Requests[0].Clone());
            Throws<IOException>(() => store.Save(state));
            Equal(before, File.ReadAllText(store.FilePath));
            File.WriteAllText(store.FilePath, JsonSerializer.Serialize(state, WorkspaceJson.Options));
            True(store.Load() is null);
            True(store.LoadNotice is not null);
            state = StarterWorkspace.Create();
            state.Collections[1].Id = state.Collections[0].Id;
            Throws<IOException>(() => store.Save(state));
        });
        Check("Unrecoverable backups are left untouched when opening a fresh workspace", () =>
        {
            var path = Path.Combine(directory, "bad-backup");
            Directory.CreateDirectory(path);
            var store = new WorkspaceStore(path);
            File.WriteAllText(store.BackupFilePath, "broken backup");
            True(store.Load() is null);
            True(store.LoadNotice is not null);
            store.Save(StarterWorkspace.Create());
            Equal("broken backup", File.ReadAllText(store.BackupFilePath));
        });
    }
}
