using Avalonia.Controls;
using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Desktop;
using Osynt.Desktop.Composition;
using Osynt.Desktop.Services;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunDeletionPreferenceChecks()
    {
        Check("Deletion preferences are saved with the deletion and survive later commits", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            True(workspace.Collections.ConfirmDeletion);
            var folder = workspace.Collections.GetCollections()[0];
            workspace.Collections.DeleteRequest(folder.Requests[0].Id, dontAskAgain: true);
            workspace.Environment.Update([new() { Key = "season", Value = "summer" }]);
            True(!new ApplicationWorkspace(store).Collections.ConfirmDeletion);
        });
        Check("A failed deletion cannot persist or apply Don't ask again", () =>
        {
            var store = new InMemoryWorkspaceStore { FailWrites = true };
            var workspace = new ApplicationWorkspace(store);
            var folder = workspace.Collections.GetCollections()[0];
            Throws<IOException>(() => workspace.Collections.DeleteRequest(folder.Requests[0].Id, dontAskAgain: true));
            Throws<IOException>(() => workspace.Collections.DeleteCollection(folder.Id, dontAskAgain: true));
            True(workspace.Collections.ConfirmDeletion);
            Equal(folder.Requests.Count, workspace.Collections.GetCollections()[0].Requests.Count);
        });
    }

    private static void RunDeletionPreferenceDesktopChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        var path = Path.Combine(directory, "deletion-preferences-ui");
        using var workspace = CreateWorkspace(path, executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        Check("Checking Don't ask again then cancelling keeps deletion confirmations enabled", () =>
        {
            var request = workspace.Library.Collections[0].Requests[0];
            var dialog = OpenDeleteMenu(window, RequestRow(window, request.Document.Id));
            dialog.FindControl<CheckBox>("DontAskAgain")!.IsChecked = true;
            Click(dialog, DialogButton(dialog, "Cancel"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            True(workspace.Library.ConfirmDeletion);
            True(workspace.Library.Collections.SelectMany(c => c.Requests).Any(r => r.Document.Id == request.Document.Id));
        });
        Check("Don't ask again skips the next request deletion confirmation", () =>
        {
            var request = workspace.Library.Collections[0].Requests[0];
            var dialog = OpenDeleteMenu(window, RequestRow(window, request.Document.Id));
            dialog.FindControl<CheckBox>("DontAskAgain")!.IsChecked = true;
            Click(dialog, DialogButton(dialog, "Delete"));
            PumpUntil(() => !workspace.Library.ConfirmDeletion);
            var next = workspace.Library.Collections[0].Requests[0];
            var deleting = new RequestDeleteDialogs(window, workspace.Library).DeleteRequestAsync(next);
            PumpUntil(() => deleting.IsCompleted);
            deleting.GetAwaiter().GetResult();
            Equal(0, window.OwnedWindows.Count);
            True(workspace.Library.Collections.SelectMany(c => c.Requests).All(r => r.Document.Id != next.Document.Id));
        });
        window.Close();
        Check("Don't ask again also skips folder confirmations after restarting", () =>
        {
            using var restarted = CreateWorkspace(path, executor, demo.BaseUrl);
            var owner = new MainWindow { DataContext = restarted };
            owner.Show();
            True(!restarted.Library.ConfirmDeletion);
            var folder = restarted.Library.Collections[0];
            var deleting = new RequestDeleteDialogs(owner, restarted.Library).DeleteCollectionAsync(folder);
            PumpUntil(() => deleting.IsCompleted);
            deleting.GetAwaiter().GetResult();
            Equal(0, owner.OwnedWindows.Count);
            True(restarted.Library.Collections.All(c => c.Id != folder.Id));
            owner.Close();
        });
    }
}
