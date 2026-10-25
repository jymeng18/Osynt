using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Desktop;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunFolderDesktopChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        using var workspace = CreateWorkspace(Path.Combine(directory, "folder-ui"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        Check("A folder can be renamed from its context menu while preserving collapsed state", () =>
        {
            var folder = workspace.Library.Collections[0];
            folder.Expanded = false;
            Dispatcher.UIThread.RunJobs();
            var header = window.GetVisualDescendants().OfType<Button>()
                .Single(b => b.Classes.Contains("collection") && ReferenceEquals(b.DataContext, folder));
            var menu = header.ContextMenu!;
            menu.Open(header);
            Dispatcher.UIThread.RunJobs();
            menu.Items.OfType<MenuItem>().Single(i => i.Header as string == "Rename").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.Close();
            var dialog = window.OwnedWindows.OfType<RenameItemWindow>().Single();
            var input = dialog.FindControl<TextBox>("NameInput")!;
            Equal(folder.Name, input.Text);
            input.Text = "  Renamed folder  ";
            Click(dialog, dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Save"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            var renamed = workspace.Library.Collections.Single(c => c.Id == folder.Id);
            Equal("Renamed folder", renamed.Name);
            True(!renamed.Expanded);
            Equal(folder.Requests.Count, renamed.Requests.Count);
        });
        Check("Blank folder names show an error and Cancel preserves the existing name", () =>
        {
            var folder = workspace.Library.Collections[0];
            var dialog = new RenameItemWindow("Rename folder", folder.Name,
                name => workspace.Library.RenameCollection(folder.Id, name));
            var show = dialog.ShowDialog(window);
            Dispatcher.UIThread.RunJobs();
            dialog.FindControl<TextBox>("NameInput")!.Text = "  ";
            Click(dialog, dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Save"));
            True(!show.IsCompleted);
            True(dialog.FindControl<TextBlock>("ErrorLabel")!.IsEffectivelyVisible);
            Click(dialog, dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Cancel"));
            PumpUntil(() => show.IsCompleted);
            Equal(folder.Name, workspace.Library.Collections[0].Name);
        });
        window.Close();
    }
}
