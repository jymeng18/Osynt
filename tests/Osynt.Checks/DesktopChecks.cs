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
        PumpUntil(() => !uiWorkspace.Selected.IsSending);
        Check("Desktop opens with a blank GET and no demo parameters or body", () =>
        {
            Equal("GET", uiWorkspace.Selected.Document.Method);
            Equal("", uiWorkspace.Selected.Document.Url);
            Equal(0, uiWorkspace.Selected.Document.Parameters.Count);
            Equal(0, uiWorkspace.Selected.Document.Headers.Count);
            Equal("", uiWorkspace.Selected.Document.Body);
            True(!uiWorkspace.Selected.HasResponse);
        });
        Check("Desktop sample GET sends successfully when requested", () =>
        {
            var initial = uiWorkspace.Selected;
            uiWorkspace.Open(uiWorkspace.Collections[0].Requests[0].Document);
            uiWorkspace.DiscardTab(initial);
            Dispatcher.UIThread.RunJobs();
            Click(window, window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SendButton"));
            PumpUntil(() => !uiWorkspace.Selected.IsSending);
            Equal(200, uiWorkspace.Selected.Response?.StatusCode ?? 0);
            True(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Get seasonal crops"));
            True(window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "UrlInput").Text!.Contains("{{base_url}}"));
        });
        Check("Desktop Send button executes POST and preserves response in its tab", () =>
        {
            var getTab = uiWorkspace.Selected;
            uiWorkspace.Open(uiWorkspace.Collections[0].Requests[2].Document);
            uiWorkspace.EditorCommand.Execute("Body");
            Dispatcher.UIThread.RunJobs();
            var sendButton = window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SendButton");
            Click(window, sendButton);
            PumpUntil(() => !uiWorkspace.Selected.IsSending);
            Equal(201, uiWorkspace.Selected.Response?.StatusCode ?? 0);
            var postTab = uiWorkspace.Selected;
            uiWorkspace.Selected = getTab;
            Equal(200, uiWorkspace.Selected.Response?.StatusCode ?? 0);
            uiWorkspace.Selected = postTab;
            Equal(201, uiWorkspace.Selected.Response?.StatusCode ?? 0);
        });
        Check("Ctrl+N creates a request and Ctrl+S persists it", () =>
        {
            var count = uiWorkspace.Tabs.Count;
            window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Equal(count + 1, uiWorkspace.Tabs.Count);
            Equal("", uiWorkspace.Selected.Document.Url);
            Equal(0, uiWorkspace.Selected.Document.Parameters.Count);
            uiWorkspace.Selected.Document.Name = "Keyboard garden";
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.S, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            True(!uiWorkspace.Selected.IsDirty);
            uiWorkspace.DiscardTab(uiWorkspace.Selected);
        });
        Check("Search filters saved requests", () =>
        {
            uiWorkspace.Search = "villager";
            Equal(1, uiWorkspace.Collections.Count);
            Equal("Meet the villagers", uiWorkspace.Collections[0].Requests[0].Name);
            uiWorkspace.Search = "";
        });
        Check("Sidebar and tab buttons navigate existing requests", () =>
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var getButton = window.GetVisualDescendants().OfType<Button>().Single(b => b.CommandParameter is LibraryRequest r && r.Name == "Get seasonal crops");
            Click(window, getButton);
            Equal("Get seasonal crops", uiWorkspace.Selected.Document.Name);
            var postButton = window.GetVisualDescendants().OfType<Button>().Single(b => b.CommandParameter is RequestTab t && t.Document.Method == "POST");
            Click(window, postButton);
            Equal("POST", uiWorkspace.Selected.Document.Method);
            Equal(2, uiWorkspace.Tabs.Count);
        });
        Check("Parameter controls add, edit, disable, and remove query rows", () =>
        {
            uiWorkspace.Selected = uiWorkspace.Tabs[0];
            uiWorkspace.EditorCommand.Execute("Params");
            Dispatcher.UIThread.RunJobs();
            var add = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "+  Add parameter");
            Click(window, add);
            Equal(2, uiWorkspace.Selected.Document.Parameters.Count);
            var pair = uiWorkspace.Selected.Document.Parameters[1];
            pair.Key = "extra";
            pair.Value = "a little test";
            pair.Enabled = false;
            using var message = HttpRequestFactory.Create(uiWorkspace.Selected.Document.Clone(), uiWorkspace.Variables);
            True(!message.RequestUri!.Query.Contains("extra"));
            var remove = window.GetVisualDescendants().OfType<Button>().Single(b => b.CommandParameter == pair);
            Click(window, remove);
            Equal(1, uiWorkspace.Selected.Document.Parameters.Count);
            uiWorkspace.SaveSelected();
            uiWorkspace.Selected = uiWorkspace.Tabs[1];
        });
        Check("A response arriving after switching tabs stays with its original request", () =>
        {
            var sendingTab = uiWorkspace.Selected;
            var send = uiWorkspace.SendSelectedAsync();
            uiWorkspace.Selected = uiWorkspace.Tabs[0];
            PumpUntil(() => send.IsCompleted);
            send.GetAwaiter().GetResult();
            True(sendingTab.HasResponse);
            Equal(201, sendingTab.Response!.StatusCode);
            Equal(200, uiWorkspace.Selected.Response!.StatusCode);
        });
        if (args.Contains("--snapshot"))
        {
            var snapshotIndex = Array.IndexOf(args, "--snapshot");
            var path = snapshotIndex + 1 < args.Length ? Path.GetFullPath(args[snapshotIndex + 1]) : Path.GetFullPath("artifacts/osynt-preview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            uiWorkspace.Selected = uiWorkspace.Tabs[0];
            uiWorkspace.EditorCommand.Execute("Params");
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The desktop did not render.");
            bitmap.Save(path);
            Console.WriteLine($"Preview saved: {path}");
            uiWorkspace.Selected = uiWorkspace.Tabs[1];
            uiWorkspace.EditorCommand.Execute("Body");
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var postBitmap = window.CaptureRenderedFrame()!;
            postBitmap.Save(Path.Combine(Path.GetDirectoryName(path)!, "osynt-post-preview.png"));
        }
        window.Close();
    }
}
