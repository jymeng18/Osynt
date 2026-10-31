using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Desktop;
using Osynt.Desktop.Composition;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunRequestOrganizationChecks(string directory)
    {
        Check("Creating folders, duplicating, and moving requests preserve identity and independent payloads", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "organization"));
            var app = new ApplicationWorkspace(store);
            var source = app.Collections.GetCollections()[0];
            var request = source.Requests[0];
            request.BodyMode = RequestBodyModes.Form;
            request.FormFields = [new() { Key = "crop", Value = "Maple" }];
            request.Authentication = new()
            {
                Mode = RequestAuthentication.Bearer,
                Token = "{{token}}"
            };
            app.Collections.SaveRequest(request);
            var destination = app.Collections.Create("  Destination  ");
            var duplicate = app.Collections.DuplicateRequest(request.Id);
            True(duplicate.Id != request.Id);
            Equal(request.Name + " copy", duplicate.Name);
            duplicate.FormFields[0].Value = "edited outside service";
            app.Collections.MoveRequest(duplicate.Id, destination);
            var restarted = new ApplicationWorkspace(store);
            var moved = restarted.Collections.GetCollections().Single(c => c.Id == destination);
            Equal("Destination", moved.Name);
            Equal(duplicate.Id, moved.Requests.Single().Id);
            Equal("Maple", moved.Requests[0].FormFields[0].Value);
            Equal("{{token}}", moved.Requests[0].Authentication.Token);
            Equal(source.Requests.Count, restarted.Collections.GetCollections().Single(c => c.Id == source.Id).Requests.Count);
            moved.Requests[0].Name = "Saved after move";
            restarted.Collections.SaveRequest(moved.Requests[0]);
            Equal("Saved after move", restarted.Collections.GetCollections().Single(c => c.Id == destination).Requests.Single().Name);
        });
        Check("Invalid organization operations and failed writes preserve both folders", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var app = new ApplicationWorkspace(store);
            var source = app.Collections.GetCollections()[0];
            var destination = app.Collections.GetCollections()[1];
            var request = source.Requests[0];
            Throws<ArgumentException>(() => app.Collections.Create(" "));
            Throws<InvalidOperationException>(() => app.Collections.MoveRequest(request.Id, Guid.NewGuid()));
            Throws<InvalidOperationException>(() => app.Collections.DuplicateRequest(Guid.NewGuid()));
            app.Collections.MoveRequest(request.Id, source.Id);
            Equal(source.Requests.Count, app.Collections.GetCollections()[0].Requests.Count);
            store.FailWrites = true;
            Throws<IOException>(() => app.Collections.Create("Failed"));
            Throws<IOException>(() => app.Collections.DuplicateRequest(request.Id));
            Throws<IOException>(() => app.Collections.MoveRequest(request.Id, destination.Id));
            Equal(2, app.Collections.GetCollections().Count);
            Equal(source.Requests.Count, app.Collections.GetCollections()[0].Requests.Count);
            Equal(destination.Requests.Count, app.Collections.GetCollections()[1].Requests.Count);
        });
    }

    private static void RunRequestOrganizationDesktopChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        using var workspace = CreateWorkspace(Path.Combine(directory, "organization-ui"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        Check("Duplicate menu opens an independent saved copy while preserving original tab edits", () =>
        {
            var original = workspace.Library.Collections[0].Requests[0];
            workspace.Open(original.Document);
            var tab = workspace.Requests.Selected;
            tab.Document.Body = "unsaved original";
            InvokeRequestMenu(window, original.Document.Id, "Duplicate");
            var duplicate = workspace.Requests.Selected;
            True(duplicate.Document.Id != original.Document.Id && !duplicate.IsDirty);
            Equal(original.Name + " copy", duplicate.Document.Name);
            Equal(original.Document.Body, duplicate.Document.Body);
            Equal("unsaved original", tab.Document.Body);
        });
        Check("Move dialog lists unfiltered destinations and preserves unsaved edits during a move", () =>
        {
            var duplicate = workspace.Requests.Selected;
            var source = workspace.Library.Collections[0];
            var destination = workspace.Library.Collections[1];
            duplicate.Document.Body = "unsaved duplicate";
            workspace.Library.Search = "copy";
            Dispatcher.UIThread.RunJobs();
            InvokeRequestMenu(window, duplicate.Document.Id, "Move to folder…");
            var dialog = window.OwnedWindows.OfType<MoveRequestWindow>().Single();
            var picker = dialog.FindControl<ListBox>("FolderPicker")!;
            Equal(2, picker.Items.Count);
            True(!dialog.FindControl<Button>("MoveButton")!.IsEnabled);
            picker.SelectedItem = picker.Items.OfType<MoveRequestWindow.FolderChoice>().Single(c => c.IsCurrent);
            True(!dialog.FindControl<Button>("MoveButton")!.IsEnabled);
            dialog.FindControl<TextBox>("FolderSearch")!.Text = "no such folder";
            Dispatcher.UIThread.RunJobs();
            Equal(0, picker.Items.Count);
            True(dialog.FindControl<TextBlock>("EmptyLabel")!.IsVisible);
            dialog.FindControl<TextBox>("FolderSearch")!.Text = "";
            Dispatcher.UIThread.RunJobs();
            picker.SelectedItem = picker.Items.OfType<MoveRequestWindow.FolderChoice>().Single(c => c.Id == destination.Id);
            Click(dialog, DialogButton(dialog, "Move"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            Equal(duplicate, workspace.Requests.Selected);
            True(duplicate.IsDirty);
            Equal("unsaved duplicate", duplicate.Document.Body);
            True(!workspace.Library.GetAllCollections().Single(c => c.Id == source.Id).Requests.Any(r => r.Id == duplicate.Document.Id));
            Equal(duplicate.Document.Id, workspace.Library.Collections.Single().Requests.Single().Document.Id);
            workspace.Library.Search = "";
        });
        Check("New folder dialog creates a persistent empty destination and Cancel leaves it unchanged", () =>
        {
            var before = workspace.Library.Collections.Count;
            var dialog = new RenameItemWindow("New folder", "", workspace.Library.CreateCollection);
            _ = dialog.ShowDialog(window);
            Dispatcher.UIThread.RunJobs();
            dialog.FindControl<TextBox>("NameInput")!.Text = "New API folder";
            Click(dialog, DialogButton(dialog, "Save"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            Equal(before + 1, workspace.Library.Collections.Count);
            Equal(0, workspace.Library.Collections.Single(c => c.Name == "New API folder").Count);
            dialog = new RenameItemWindow("New folder", "", workspace.Library.CreateCollection);
            _ = dialog.ShowDialog(window);
            Dispatcher.UIThread.RunJobs();
            Click(dialog, DialogButton(dialog, "Cancel"));
            Equal(before + 1, workspace.Library.Collections.Count);
        });
        Check("Dragging a request highlights and expands the destination and preserves open edits", () =>
        {
            var source = workspace.Library.Collections.First(c => c.Requests.Count > 0);
            var destination = workspace.Library.Collections.First(c => c.Id != source.Id);
            var request = source.Requests[0];
            workspace.Open(request.Document);
            var tab = workspace.Requests.Selected;
            tab.Document.Body = "drag draft";
            destination.Expanded = false;
            Dispatcher.UIThread.RunJobs();
            var row = RequestRow(window, request.Document.Id);
            var target = window.GetVisualDescendants().OfType<Button>().Single(b => b.DataContext == destination && b.Classes.Contains("collection"));
            var start = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), window)!.Value;
            var end = target.TranslatePoint(new Point(40, target.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X + 10, start.Y), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            True(row.Classes.Contains("dragging"), "Drag did not start after crossing the pointer threshold.");
            var dragData = new DataTransfer();
            dragData.Add(DataTransferItem.CreateText(request.Name));
            window.DragDrop(end, Avalonia.Input.Raw.RawDragEventType.DragEnter, dragData, DragDropEffects.Move);
            window.DragDrop(end, Avalonia.Input.Raw.RawDragEventType.DragOver, dragData, DragDropEffects.Move);
            // Headless dispatcher timers advance when native input is delivered.
            Thread.Sleep(650);
            window.DragDrop(end, Avalonia.Input.Raw.RawDragEventType.DragOver, dragData, DragDropEffects.Move);
            Dispatcher.UIThread.RunJobs();
            True(destination.Expanded);
            True(target.GetVisualAncestors().OfType<StackPanel>().Any(p => p.Classes.Contains("drop-target")));
            window.DragDrop(end, Avalonia.Input.Raw.RawDragEventType.Drop, dragData, DragDropEffects.Move);
            window.MouseUp(end, MouseButton.Left);
            PumpUntil(() => workspace.Library.GetAllCollections().Single(c => c.Id == destination.Id).Requests.Any(r => r.Id == request.Document.Id));
            Equal(tab, workspace.Requests.Selected);
            Equal("drag draft", tab.Document.Body);
            True(tab.IsDirty);
        });
        foreach (var tab in workspace.Requests.Tabs)
            tab.IsDirty = false;
        window.Close();

        Check("Failed desktop moves leave the dialog open and the saved request in its original folder", () =>
        {
            var store = new InMemoryWorkspaceStore();
            using var failing = WorkspaceComposition.Create(store, new JsonCollectionSerializer(), executor, demo.BaseUrl);
            var owner = new MainWindow { DataContext = failing };
            owner.Show();
            var request = failing.Library.Collections[0].Requests[0];
            var destination = failing.Library.Collections[1];
            var dialog = new MoveRequestWindow(failing.Library, request);
            _ = dialog.ShowDialog(owner);
            Dispatcher.UIThread.RunJobs();
            dialog.FindControl<ListBox>("FolderPicker")!.SelectedItem = dialog.FindControl<ListBox>("FolderPicker")!.Items
                .OfType<MoveRequestWindow.FolderChoice>().Single(c => c.Id == destination.Id);
            store.FailWrites = true;
            Click(dialog, DialogButton(dialog, "Move"));
            True(dialog.FindControl<TextBlock>("ErrorLabel")!.IsEffectivelyVisible);
            True(failing.Library.Collections[0].Requests.Any(r => r.Document.Id == request.Document.Id));
            Click(dialog, DialogButton(dialog, "Cancel"));
            owner.Close();
        });
    }

    private static void InvokeRequestMenu(Window window, Guid requestId, string label)
    {
        var row = RequestRow(window, requestId);
        var menu = row.ContextMenu!;
        menu.Open(row);
        Dispatcher.UIThread.RunJobs();
        menu.Items.OfType<MenuItem>().Single(i => i.Header as string == label).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
