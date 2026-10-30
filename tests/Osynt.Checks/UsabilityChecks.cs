using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Desktop;
using Osynt.Desktop.ViewModels;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunUsabilityChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        using var workspace = CreateWorkspace(Path.Combine(directory, "usability"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace, Width = 980, Height = 640 };
        window.Show();
        Check("Adding a parameter focuses the new key, including rows beyond the visible editor", () =>
        {
            for (var i = 0; i < 8; i++)
                Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "+  Add parameter"));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var key = window.GetVisualDescendants().OfType<TextBox>()
                .Single(c => c.DataContext == workspace.Requests.Selected.Document.Parameters.Last() && Grid.GetColumn(c) == 1);
            True(key.IsFocused);
            var scroll = key.GetVisualAncestors().OfType<ScrollViewer>().First();
            var position = key.TranslatePoint(new Point(), scroll)!.Value;
            True(position.Y >= 0 && position.Y + key.Bounds.Height <= scroll.Bounds.Height + 1);
        });
        Check("Clearing library search restores results and keeps keyboard focus in search", () =>
        {
            workspace.Library.Search = "nothing-matches-this";
            Dispatcher.UIThread.RunJobs();
            Click(window, window.GetVisualDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetName(b) == "Clear request search"));
            Equal("", workspace.Library.Search);
            True(!workspace.Library.EmptyLibrary);
            True(window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "SearchBox").IsFocused);
        });
        Check("Overflowing tabs reveal the selected request and its close button on keyboard navigation", () =>
        {
            for (var i = 0; i < 12; i++)
            {
                workspace.NewRequest();
                workspace.Requests.Selected.Document.Name = $"A long request name number {i}";
            }
            void AssertSelectedVisible()
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(c => c.Name == "TabScroll");
                var tab = window.GetVisualDescendants().OfType<Button>()
                    .Single(b => b.CommandParameter == workspace.Requests.Selected);
                var row = (Control)tab.Parent!;
                var position = row.TranslatePoint(new Point(), scroll)!.Value;
                True(position.X >= -1 && position.X + row.Bounds.Width <= scroll.Viewport.Width + 1,
                    $"Selected tab is outside the visible strip: {position.X}, {row.Bounds.Width}, {scroll.Viewport.Width}");
            }
            AssertSelectedVisible();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            Equal(workspace.Requests.Tabs[0], workspace.Requests.Selected);
            AssertSelectedVisible();
        });
        foreach (var tab in workspace.Requests.Tabs)
            tab.IsDirty = false;
        window.Close();
    }
}
