using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Application.Workspaces;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;
using Osynt.Desktop;
using Osynt.Desktop.ViewModels;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void Click(Window window, Control control)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void RunDesktopChecks(string directory, RequestExecutor executor, DemoServer demo, string[] args)
    {
        // Render the actual desktop window through Avalonia's Skia renderer, without a display server.
        AppBuilder.Configure<App>().UseSkia().WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        using var uiWorkspace = CreateWorkspace(Path.Combine(directory, "ui"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = uiWorkspace };
        window.Show();
        PumpUntil(() => !uiWorkspace.Requests.Selected.IsSending);
        Check("Desktop opens with a blank GET and no demo parameters or body", () =>
        {
            Equal("GET", uiWorkspace.Requests.Selected.Document.Method);
            Equal("", uiWorkspace.Requests.Selected.Document.Url);
            Equal(0, uiWorkspace.Requests.Selected.Document.Parameters.Count);
            Equal(0, uiWorkspace.Requests.Selected.Document.Headers.Count);
            Equal("", uiWorkspace.Requests.Selected.Document.Body);
            True(!uiWorkspace.Requests.Selected.HasResponse);
        });
        Check("Desktop sample GET sends successfully when requested", () =>
        {
            var initial = uiWorkspace.Requests.Selected;
            uiWorkspace.Open(uiWorkspace.Library.Collections[0].Requests[0].Document);
            uiWorkspace.Requests.Discard(initial);
            Dispatcher.UIThread.RunJobs();
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SendButton"));
            PumpUntil(() => !uiWorkspace.Requests.Selected.IsSending);
            Equal(200, uiWorkspace.Requests.Selected.Response?.StatusCode ?? 0);
            True(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Get seasonal crops"));
            True(window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "UrlInput").Text!.Contains("{{base_url}}"));
        });
        Check("Timeout control edits the selected request and follows tab selection", () =>
        {
            var original = uiWorkspace.Requests.Selected;
            var input = window.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "TimeoutInput");
            input.Value = 75;
            Dispatcher.UIThread.RunJobs();
            Equal(75, original.Document.TimeoutSeconds);
            True(original.IsDirty);
            uiWorkspace.NewRequest();
            Dispatcher.UIThread.RunJobs();
            Equal(30m, input.Value!.Value);
            uiWorkspace.Requests.Discard(uiWorkspace.Requests.Selected);
            uiWorkspace.Requests.Selected = original;
            original.Document.TimeoutSeconds = 30;
            uiWorkspace.Requests.SaveSelected();
        });
        Check("Desktop Send button executes POST and preserves response in its tab", () =>
        {
            var getTab = uiWorkspace.Requests.Selected;
            uiWorkspace.Open(uiWorkspace.Library.Collections[0].Requests[2].Document);
            uiWorkspace.Editor.EditorCommand.Execute("Body");
            Dispatcher.UIThread.RunJobs();
            var sendButton = window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SendButton");
            Click(window, sendButton);
            PumpUntil(() => !uiWorkspace.Requests.Selected.IsSending);
            Equal(201, uiWorkspace.Requests.Selected.Response?.StatusCode ?? 0);
            var postTab = uiWorkspace.Requests.Selected;
            uiWorkspace.Requests.Selected = getTab;
            Equal(200, uiWorkspace.Requests.Selected.Response?.StatusCode ?? 0);
            uiWorkspace.Requests.Selected = postTab;
            Equal(201, uiWorkspace.Requests.Selected.Response?.StatusCode ?? 0);
        });
        Check("Body format picker changes the editor and form controls preserve drafts", () =>
        {
            var original = uiWorkspace.Requests.Selected;
            uiWorkspace.NewRequest();
            uiWorkspace.Editor.EditorCommand.Execute("Body");
            Dispatcher.UIThread.RunJobs();
            var picker = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "BodyModePicker");
            picker.SelectedItem = RequestBodyModes.Form;
            Dispatcher.UIThread.RunJobs();
            True(uiWorkspace.Requests.Selected.Document.FormBody);
            var add = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "+  Add form field");
            True(add.IsEffectivelyVisible);
            Click(window, add);
            var field = uiWorkspace.Requests.Selected.Document.FormFields.Single();
            field.Key = "crop";
            field.Value = "Maple";
            picker.SelectedItem = RequestBodyModes.None;
            Dispatcher.UIThread.RunJobs();
            True(!uiWorkspace.Requests.Selected.HasBody);
            picker.SelectedItem = RequestBodyModes.Form;
            Dispatcher.UIThread.RunJobs();
            Equal("Maple", uiWorkspace.Requests.Selected.Document.FormFields.Single().Value);
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.CommandParameter == field));
            Equal(0, uiWorkspace.Requests.Selected.Document.FormFields.Count);
            uiWorkspace.Requests.Discard(uiWorkspace.Requests.Selected);
            uiWorkspace.Requests.Selected = original;
        });
        Check("Authentication picker binds credentials to the selected tab and masks secrets", () =>
        {
            var original = uiWorkspace.Requests.Selected;
            uiWorkspace.NewRequest();
            uiWorkspace.Editor.EditorCommand.Execute("Auth");
            Dispatcher.UIThread.RunJobs();
            var picker = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "AuthModePicker");
            picker.SelectedItem = RequestAuthentication.Bearer;
            Dispatcher.UIThread.RunJobs();
            var token = window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "BearerTokenInput");
            True(token.IsEffectivelyVisible && token.PasswordChar != default(char));
            token.Text = "{{api_token}}";
            Dispatcher.UIThread.RunJobs();
            Equal("{{api_token}}", uiWorkspace.Requests.Selected.Document.BearerToken);
            picker.SelectedItem = RequestAuthentication.Basic;
            Dispatcher.UIThread.RunJobs();
            True(!token.IsEffectivelyVisible);
            var username = window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "AuthUsernameInput");
            True(username.IsEffectivelyVisible);
            username.Text = "{{username}}";
            Dispatcher.UIThread.RunJobs();
            Equal("{{username}}", uiWorkspace.Requests.Selected.Document.AuthUsername);
            uiWorkspace.Requests.Discard(uiWorkspace.Requests.Selected);
            uiWorkspace.Requests.Selected = original;
        });
        Check("Ctrl+N creates a request and Ctrl+S persists it", () =>
        {
            var count = uiWorkspace.Requests.Tabs.Count;
            window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Equal(count + 1, uiWorkspace.Requests.Tabs.Count);
            Equal("", uiWorkspace.Requests.Selected.Document.Url);
            Equal(0, uiWorkspace.Requests.Selected.Document.Parameters.Count);
            uiWorkspace.Requests.Selected.Document.Name = "Keyboard garden";
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.S, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            True(!uiWorkspace.Requests.Selected.IsDirty);
            uiWorkspace.Requests.Discard(uiWorkspace.Requests.Selected);
        });
        Check("Search filters saved requests", () =>
        {
            uiWorkspace.Library.Search = "villager";
            Equal(1, uiWorkspace.Library.Collections.Count);
            Equal("Meet the villagers", uiWorkspace.Library.Collections[0].Requests[0].Name);
            uiWorkspace.Library.Search = "";
        });
        Check("Keyboard tab navigation wraps without losing edits and Ctrl+L selects the request URL", () =>
        {
            var original = uiWorkspace.Requests.Selected;
            uiWorkspace.NewRequest();
            var first = uiWorkspace.Requests.Selected;
            first.Document.Url = "https://example.com/first";
            uiWorkspace.NewRequest();
            var second = uiWorkspace.Requests.Selected;
            second.Document.Url = "https://example.com/second";
            uiWorkspace.Editor.EditorCommand.Execute("Body");
            Dispatcher.UIThread.RunJobs();
            window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "BodyInput").Focus();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Equal(uiWorkspace.Requests.Tabs[0], uiWorkspace.Requests.Selected);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            Equal(second, uiWorkspace.Requests.Selected);
            True(first.IsDirty && second.IsDirty);
            Equal("https://example.com/first", first.Document.Url);
            window.KeyPressQwerty(PhysicalKey.L, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.L, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            var url = window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "UrlInput");
            True(url.IsFocused);
            Equal(second.Document.Url.Length, Math.Abs(url.SelectionEnd - url.SelectionStart));
            uiWorkspace.Requests.Discard(second);
            uiWorkspace.Requests.Discard(first);
            uiWorkspace.Requests.Selected = original;
        });
        Check("Sidebar shows actionable no-results text and hides it when the search clears", () =>
        {
            uiWorkspace.Library.Search = "no-request-matches-this";
            Dispatcher.UIThread.RunJobs();
            var title = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "LibraryEmptyTitle");
            True(title.IsEffectivelyVisible);
            Equal("No matching requests", title.Text);
            uiWorkspace.Library.Search = "";
            Dispatcher.UIThread.RunJobs();
            True(!title.IsEffectivelyVisible);
        });
        Check("Sidebar and tab buttons navigate existing requests", () =>
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var getButton = window.GetVisualDescendants().OfType<Button>().Single(b => b.CommandParameter is LibraryRequest r && r.Name == "Get seasonal crops");
            Click(window, getButton);
            Equal("Get seasonal crops", uiWorkspace.Requests.Selected.Document.Name);
            var postButton = window.GetVisualDescendants().OfType<Button>().Single(b => b.CommandParameter is RequestTab t && t.Document.Method == "POST");
            Click(window, postButton);
            Equal("POST", uiWorkspace.Requests.Selected.Document.Method);
            Equal(2, uiWorkspace.Requests.Tabs.Count);
        });
        Check("Parameter controls add, edit, disable, and remove query rows", () =>
        {
            uiWorkspace.Requests.Selected = uiWorkspace.Requests.Tabs[0];
            uiWorkspace.Editor.EditorCommand.Execute("Params");
            Dispatcher.UIThread.RunJobs();
            var add = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "+  Add parameter");
            Click(window, add);
            Equal(2, uiWorkspace.Requests.Selected.Document.Parameters.Count);
            var pair = uiWorkspace.Requests.Selected.Document.Parameters[1];
            pair.Key = "extra";
            pair.Value = "a little test";
            pair.Enabled = false;
            using var message = HttpRequestFactory.Create(uiWorkspace.Requests.Selected.Document.Clone(), uiWorkspace.Environment.Variables);
            True(!message.RequestUri!.Query.Contains("extra"));
            var remove = window.GetVisualDescendants().OfType<Button>().Single(b => b.CommandParameter == pair);
            Click(window, remove);
            Equal(1, uiWorkspace.Requests.Selected.Document.Parameters.Count);
            uiWorkspace.Requests.SaveSelected();
            uiWorkspace.Requests.Selected = uiWorkspace.Requests.Tabs[1];
        });
        Check("A response arriving after switching tabs stays with its original request", () =>
        {
            var sendingTab = uiWorkspace.Requests.Selected;
            var send = uiWorkspace.Sending.SendSelectedAsync();
            uiWorkspace.Requests.Selected = uiWorkspace.Requests.Tabs[0];
            PumpUntil(() => send.IsCompleted);
            send.GetAwaiter().GetResult();
            True(sendingTab.HasResponse);
            Equal(201, sendingTab.Response!.StatusCode);
            Equal(200, uiWorkspace.Requests.Selected.Response!.StatusCode);
        });
        Check("Successful responses without content explicitly show an empty body message", () =>
        {
            var previous = uiWorkspace.Requests.Selected.Response;
            uiWorkspace.Requests.Selected.Response = new(200, "OK", 1, 0, "", []);
            Dispatcher.UIThread.RunJobs();
            True(window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "No response body").IsEffectivelyVisible);
            Equal("200 OK", uiWorkspace.Requests.Selected.Status);
            uiWorkspace.Requests.Selected.Response = previous;
        });
        Check("The compact window keeps request controls and response status on screen", () =>
        {
            window.Width = 980;
            window.Height = 640;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            foreach (var name in new[] { "UrlInput", "SendButton", "RequestName" })
            {
                var control = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
                var origin = control.TranslatePoint(new Point(), window)!.Value;
                True(origin.X >= 0 && origin.Y >= 0 && origin.X + control.Bounds.Width <= window.Width && origin.Y + control.Bounds.Height <= window.Height);
            }
            window.Width = 1280;
            window.Height = 840;
        });
        Check("Garden navigation preserves requests and a real demo success grants growth", () =>
        {
            var previousTab = uiWorkspace.Requests.Selected;
            var previousGrowth = uiWorkspace.Garden.Successes;
            var open = window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "OpenGardenButton");
            Click(window, open);
            True(uiWorkspace.Garden.IsOpen);
            var demoButton = window.GetVisualDescendants().OfType<Button>().Single(c => c.Command == uiWorkspace.SendGardenDemoCommand);
            Click(window, demoButton);
            PumpUntil(() => !uiWorkspace.Requests.Selected.IsSending);
            Equal(200, uiWorkspace.Requests.Selected.Response!.StatusCode);
            Equal(previousGrowth + 1, uiWorkspace.Garden.Successes);
            True(uiWorkspace.Garden.IsOpen);
            var demoTab = uiWorkspace.Requests.Selected;
            uiWorkspace.CloseGardenCommand.Execute(null);
            True(uiWorkspace.Garden.ShowRequests);
            uiWorkspace.Requests.Discard(demoTab);
            uiWorkspace.Requests.Selected = previousTab;
        });
        if (args.Contains("--snapshot"))
        {
            var snapshotIndex = Array.IndexOf(args, "--snapshot");
            var path = snapshotIndex + 1 < args.Length ? Path.GetFullPath(args[snapshotIndex + 1]) : Path.GetFullPath("artifacts/osynt-preview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            uiWorkspace.Requests.Selected = uiWorkspace.Requests.Tabs[0];
            uiWorkspace.Editor.EditorCommand.Execute("Params");
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The desktop did not render.");
            bitmap.Save(path);
            Console.WriteLine($"Preview saved: {path}");
            uiWorkspace.Requests.Selected = uiWorkspace.Requests.Tabs[1];
            uiWorkspace.Editor.EditorCommand.Execute("Body");
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var postBitmap = window.CaptureRenderedFrame()!;
            postBitmap.Save(Path.Combine(Path.GetDirectoryName(path)!, "osynt-post-preview.png"));
            window.Width = 980;
            window.Height = 640;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var compactBitmap = window.CaptureRenderedFrame()!;
            compactBitmap.Save(Path.Combine(Path.GetDirectoryName(path)!, "osynt-compact-preview.png"));
        }
        window.Close();
        RunTabCloseChecks(directory, executor, demo);
        RunFolderDesktopChecks(directory, executor, demo);
        RunDeletionDesktopChecks(directory, executor, demo);
        RunRequestRenameDesktopChecks(directory, executor, demo);
        RunRequestOrganizationDesktopChecks(directory, executor, demo);
        RunCurlDesktopChecks(directory, executor, demo);
        RunResponseInspectionDesktopChecks(directory, executor, demo);
        RunDeletionPreferenceDesktopChecks(directory, executor, demo);
        RunUsabilityChecks(directory, executor, demo);
    }
}
