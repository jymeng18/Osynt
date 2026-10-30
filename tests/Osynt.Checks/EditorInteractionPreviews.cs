using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Desktop.ViewModels;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void CaptureEditorInteractionPreviews(Window window, WorkspaceViewModel workspace, string directory)
    {
        void Render(string name)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame()!;
            bitmap.Save(Path.Combine(directory, name + ".png"));
        }
        void Hover(Control control)
        {
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
        }
        var bodyTab = window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Command == workspace.Editor.EditorCommand && b.CommandParameter as string == "Body");
        var headersTab = window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Command == workspace.Editor.EditorCommand && b.CommandParameter as string == "Headers");
        workspace.Editor.EditorCommand.Execute("Body");
        window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "UrlInput").Focus();
        Hover(headersTab);
        Render("tabs-body-selected-headers-hover");
        window.MouseMove(new Point(10, 10));
        bodyTab.Focus(NavigationMethod.Tab);
        Render("tabs-body-keyboard-focus");
        var body = window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "BodyInput");
        body.Focus();
        Hover(body);
        Render("body-editor-focus");
        Click(window, headersTab);
        var header = window.GetVisualDescendants().OfType<TextBox>()
            .First(c => c.IsEffectivelyVisible && c.Classes.Contains("cell"));
        Hover(header);
        Render("header-cell-hover");
        header.Focus();
        Render("header-cell-focus");
        var responseHeaders = window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Command == workspace.Editor.ResponsePaneCommand && b.CommandParameter as string == "Headers");
        responseHeaders.Focus(NavigationMethod.Tab);
        Hover(responseHeaders);
        Render("response-headers-keyboard-focus");
        Click(window, responseHeaders);
        Render("response-headers-selected");
    }
}
