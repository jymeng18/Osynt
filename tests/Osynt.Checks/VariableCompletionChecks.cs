using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Checks.Fakes;
using Osynt.Desktop;
using Osynt.Desktop.Composition;
using Osynt.Desktop.Controls;
using Osynt.Desktop.Views;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunVariableCompletionChecks(string[] args)
    {
        Check("Variable suggestions support real typing, middle edits, substring search, dismissal, and mouse selection", () =>
        {
            using var workspace = WorkspaceComposition.Create(new InMemoryWorkspaceStore(), new JsonCollectionSerializer(),
                new StubRequestExecutor((_, _, _) => Task.FromResult(SuccessResponse())), "");
            workspace.Environment.UpdateVariables([new() { Key = "base_url", Value = "https://example.test" },
                new() { Key = "access_token", Value = "hidden-credential" }, new() { Key = "refresh_token", Value = "another-secret" }]);
            var window = new MainWindow { DataContext = workspace };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var editor = window.GetVisualDescendants().OfType<RequestEditorView>().Single();
            var view = editor.FindControl<VariableCompletionView>("CompletionView")!;
            var popup = view.FindControl<Popup>("VariablePopup")!;
            var list = view.FindControl<ListBox>("VariableSuggestions")!;
            var input = editor.FindControl<TextBox>("UrlInput")!;
            input.Focus();
            window.KeyTextInput("{{token");
            Dispatcher.UIThread.RunJobs();
            Equal(2, list.ItemCount);
            True(list.Items.Cast<VariableSuggestion>().All(v => !v.Preview.Contains("secret") && !v.Preview.Contains("credential")));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Equal("{{refresh_token}}", input.Text);
            input.Text = "{{base_url}}/health";
            input.CaretIndex = 5;
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Equal("{{base_url}}/health", input.Text);
            input.Text = "{{bas}/health";
            input.CaretIndex = 5;
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Equal("{{base_url}}/health", input.Text);
            input.Text = "{{token";
            input.CaretIndex = 7;
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            input.CaretIndex = 6;
            Dispatcher.UIThread.RunJobs();
            True(!popup.IsOpen);
            input.Text = "";
            input.CaretIndex = 0;
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            True(popup.IsOpen);
            Equal(3, list.ItemCount);
            // Click the second option, which must win over the keyboard selection.
            var item = (Control)list.ContainerFromIndex(1)!;
            var popupRoot = (TopLevel)TopLevel.GetTopLevel(list)!;
            var point = item.TranslatePoint(new Point(50, item.Bounds.Height / 2), popupRoot)!.Value;
            popupRoot.MouseDown(point, MouseButton.Left);
            popupRoot.MouseUp(point, MouseButton.Left);
            Equal("{{base_url}}", input.Text);
            workspace.Environment.UpdateVariables(workspace.Environment.Variables.Select(p => new Osynt.Domain.Requests.RequestPair { Key = p.Key, Value = p.Value })
                .Append(new Osynt.Domain.Requests.RequestPair { Key = "api/url", Value = "https://nested.test" }));
            input.Text = "{{api/url}}/health";
            input.CaretIndex = 5;
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Equal("{{api/url}}/health", input.Text);
            input.Text = "{{no_such_variable";
            input.CaretIndex = input.Text.Length;
            Dispatcher.UIThread.RunJobs();
            True(view.FindControl<TextBlock>("EmptySuggestions")!.IsVisible);
            input.Text = "{{";
            input.CaretIndex = 2;
            Dispatcher.UIThread.RunJobs();
            if (args.Contains("--snapshot"))
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var bitmap = window.CaptureRenderedFrame()!;
                bitmap.Save("/tmp/osynt-variable-redesign.png");
            }
            popup.IsOpen = false;
            workspace.Requests.Selected.IsDirty = false;
            window.Close();
        });
    }
}
