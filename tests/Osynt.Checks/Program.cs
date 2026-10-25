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
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;
using Osynt.Desktop;
using Osynt.Desktop.ViewModels;

namespace Osynt.Checks;

/// <summary>A dependency-light integration runner. Any failed check exits with code 1.</summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;
    [STAThread]
    public static int Main(string[] args)
    {
        var directory = Path.Combine(Path.GetTempPath(), "osynt-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var demo = new DemoServer();
        demo.Start();
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var executor = new RequestExecutor(client);
        var variables = new Dictionary<string, string> { ["base_url"] = demo.BaseUrl, ["season"] = "summer" };

        Check("Variables resolve in URLs and values", () => Equal(demo.BaseUrl + "/summer", RequestExecutor.Resolve("{{ base_url }}/{{season}}", variables)));
        Check("Missing variables produce an actionable error", () => Throws<ArgumentException>(() => RequestExecutor.Resolve("{{missing}}", variables)));
        Check("Only HTTP and HTTPS URLs are accepted", () => Throws<ArgumentException>(() => RequestExecutor.BuildRequest(new() { Url = "file:///etc/passwd" }, variables)));
        Check("Query parameters preserve existing queries, fragments, duplicates, and escaping", () =>
        {
            using var request = RequestExecutor.BuildRequest(new() { Url = "https://example.com/path?first=1#anchor", Parameters = [new() { Key = "q", Value = "a & b" }, new() { Key = "q", Value = "two" }, new() { Enabled = false, Key = "skip", Value = "me" }] }, variables);
            Equal("?first=1&q=a%20%26%20b&q=two", request.RequestUri!.Query);
            Equal("#anchor", request.RequestUri.Fragment);
        });
        Check("JSON bodies and content headers are sent correctly", () =>
        {
            using var request = RequestExecutor.BuildRequest(new() { Method = "POST", Url = "{{base_url}}/v1/plant", Body = "{\"season\":\"{{season}}\"}", Headers = [new() { Key = "Content-Type", Value = "application/problem+json" }, new() { Key = "X-Season", Value = "{{season}}" }, new() { Key = "X-Skip", Value = "no", Enabled = false }] }, variables);
            Equal("application/problem+json", request.Content!.Headers.ContentType!.MediaType);
            Equal("{\"season\":\"summer\"}", request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            Equal("summer", request.Headers.GetValues("X-Season").Single());
            True(!request.Headers.Contains("X-Skip"));
        });
        Check("Header line breaks are rejected", () => Throws<ArgumentException>(() => RequestExecutor.BuildRequest(new() { Url = demo.BaseUrl, Headers = [new() { Key = "X-Test", Value = "x\r\nInjected: yes" }] }, variables)));
        Check("Local GET returns real seasonal data and headers", () =>
        {
            var response = executor.SendAsync(new() { Url = "{{base_url}}/v1/harvest", Parameters = [new() { Key = "season", Value = "{{season}}" }] }, variables).GetAwaiter().GetResult();
            Equal(200, response.StatusCode);
            True(response.Body.Contains("blueberry"));
            True(response.Headers.Any(h => h.Key == "X-Osynt-Demo" && h.Value == "true"));
            Equal(Encoding.UTF8.GetByteCount(response.Body), (int)response.SizeBytes);
        });
        Check("Local POST returns 201 with the submitted crop", () =>
        {
            var response = executor.SendAsync(new() { Method = "POST", Url = "{{base_url}}/v1/plant", Body = "{\"crop\":\"strawberry\",\"quantity\":12}" }, variables).GetAwaiter().GetResult();
            Equal(201, response.StatusCode); True(response.Body.Contains("strawberry"));
        });
        Check("Invalid JSON and missing paths display HTTP error responses", () =>
        {
            Equal(400, executor.SendAsync(new() { Method = "POST", Url = demo.BaseUrl + "/v1/plant", Body = "oops" }, variables).GetAwaiter().GetResult().StatusCode);
            Equal(404, executor.SendAsync(new() { Url = demo.BaseUrl + "/missing" }, variables).GetAwaiter().GetResult().StatusCode);
        });
        Check("Response preview is bounded to 5 MB", () =>
        {
            using var largeClient = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', RequestExecutor.MaxResponseBytes + 500)) })));
            var response = new RequestExecutor(largeClient).SendAsync(new() { Url = "https://example.com" }, variables).GetAwaiter().GetResult();
            True(response.Truncated); Equal(RequestExecutor.MaxResponseBytes, response.Body.Length);
        });
        Check("Cancellation interrupts an in-flight request", () =>
        {
            using var slowClient = new HttpClient(new StubHandler(async token => { await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); }));
            using var cancellation = new CancellationTokenSource(40);
            Throws<OperationCanceledException>(() => new RequestExecutor(slowClient).SendAsync(new() { Url = "https://example.com" }, variables, cancellation.Token).GetAwaiter().GetResult());
        });
        Check("Workspace requests and history survive a restart", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "persistence"));
            var state = StarterWorkspace.Create(); state.Collections[0].Requests[0].Name = "Saved crop";
            state.History.Add(new() { Request = state.Collections[0].Requests[0].Clone(), StatusCode = 200 });
            store.Save(state);
            var loaded = store.Load()!; Equal("Saved crop", loaded.Collections[0].Requests[0].Name); Equal(1, loaded.History.Count);
        });
        Check("Unreadable workspaces are preserved before the next save", () =>
        {
            var path = Path.Combine(directory, "corrupt"); Directory.CreateDirectory(path);
            var store = new WorkspaceStore(path); File.WriteAllText(store.FilePath, "{not json");
            var state = store.Load() ?? StarterWorkspace.Create(); True(store.LoadNotice is not null); store.Save(state);
            True(Directory.GetFiles(path, "workspace-unreadable-*.json").Length == 1);
        });
        Check("Editing a request leaves its saved collection unchanged until Save", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "editing"), executor, demo.BaseUrl);
            var original = workspace.Collections[0].Requests[0].Name;
            workspace.Selected.Document.Name = "My new crop";
            True(workspace.Selected.IsDirty); Equal(original, workspace.Collections[0].Requests[0].Name);
            workspace.SaveSelected(); True(!workspace.Selected.IsDirty); Equal("My new crop", workspace.Collections[0].Requests[0].Name);
        });
        Check("Unsaved tabs are protected and saved tabs close", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "tabs"), executor, demo.BaseUrl);
            workspace.NewRequest(); var tab = workspace.Selected;
            workspace.Close(tab); True(workspace.Tabs.Contains(tab));
            workspace.SaveSelected(); workspace.Close(tab); True(!workspace.Tabs.Contains(tab));
        });
        Check("Import/export round-trips collections and assigns fresh request IDs", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "import"), executor, demo.BaseUrl);
            var original = workspace.Collections[0].Requests[0].Document.Id;
            workspace.ImportCollections(workspace.ExportCollections());
            Equal(4, workspace.Collections.Count); True(original != workspace.Collections[2].Requests[0].Document.Id);
            Throws<JsonException>(() => workspace.ImportCollections("[{\"Name\":\"broken\",\"Requests\":null}]"));
            Equal(4, workspace.Collections.Count);
        });
        Check("Duplicate environment variable names do not overwrite the environment", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "environment"), executor, demo.BaseUrl);
            Throws<ArgumentException>(() => workspace.UpdateVariables([new() { Key = "base_url", Value = "one" }, new() { Key = "base_url", Value = "two" }]));
            Equal(demo.BaseUrl, workspace.Variables["base_url"]);
        });

        // Render the actual desktop window through Avalonia's Skia renderer, without a display server.
        AppBuilder.Configure<App>().UseSkia().WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        using var uiWorkspace = CreateWorkspace(Path.Combine(directory, "ui"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = uiWorkspace };
        window.Show();
        PumpUntil(() => !uiWorkspace.Selected.IsSending);
        Check("Desktop opens with a real successful local demo response", () =>
        {
            Equal(200, uiWorkspace.Selected.Response?.StatusCode ?? 0);
            True(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Get seasonal crops"));
            True(window.FindControl<TextBox>("UrlInput")!.Text!.Contains("{{base_url}}"));
        });
        Check("Desktop Send button executes POST and preserves response in its tab", () =>
        {
            var getTab = uiWorkspace.Selected;
            uiWorkspace.Open(uiWorkspace.Collections[0].Requests[2].Document);
            uiWorkspace.EditorCommand.Execute("Body"); Dispatcher.UIThread.RunJobs();
            var sendButton = window.FindControl<Button>("SendButton")!;
            var point = sendButton.TranslatePoint(new Point(sendButton.Bounds.Width / 2, sendButton.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            PumpUntil(() => !uiWorkspace.Selected.IsSending);
            Equal(201, uiWorkspace.Selected.Response?.StatusCode ?? 0);
            var postTab = uiWorkspace.Selected;
            uiWorkspace.Selected = getTab; Equal(200, uiWorkspace.Selected.Response?.StatusCode ?? 0);
            uiWorkspace.Selected = postTab; Equal(201, uiWorkspace.Selected.Response?.StatusCode ?? 0);
        });
        Check("Ctrl+N creates a request and Ctrl+S persists it", () =>
        {
            var count = uiWorkspace.Tabs.Count;
            window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control); window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs(); Equal(count + 1, uiWorkspace.Tabs.Count);
            uiWorkspace.Selected.Document.Name = "Keyboard garden";
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control); window.KeyReleaseQwerty(PhysicalKey.S, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs(); True(!uiWorkspace.Selected.IsDirty);
            uiWorkspace.DiscardTab(uiWorkspace.Selected);
        });
        Check("Search filters saved requests", () =>
        {
            uiWorkspace.Search = "villager"; Equal(1, uiWorkspace.Collections.Count); Equal("Meet the villagers", uiWorkspace.Collections[0].Requests[0].Name);
            uiWorkspace.Search = "";
        });
        Check("A response arriving after switching tabs stays with its original request", () =>
        {
            var sendingTab = uiWorkspace.Selected;
            var send = uiWorkspace.SendSelectedAsync();
            uiWorkspace.Selected = uiWorkspace.Tabs[0];
            PumpUntil(() => send.IsCompleted); send.GetAwaiter().GetResult();
            True(sendingTab.HasResponse); Equal(201, sendingTab.Response!.StatusCode);
            Equal(200, uiWorkspace.Selected.Response!.StatusCode);
        });
        if (args.Contains("--snapshot"))
        {
            var snapshotIndex = Array.IndexOf(args, "--snapshot");
            var path = snapshotIndex + 1 < args.Length ? Path.GetFullPath(args[snapshotIndex + 1]) : Path.GetFullPath("artifacts/osynt-preview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            uiWorkspace.Selected = uiWorkspace.Tabs[0]; uiWorkspace.EditorCommand.Execute("Params");
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The desktop did not render.");
            bitmap.Save(path); Console.WriteLine($"Preview saved: {path}");
            uiWorkspace.Selected = uiWorkspace.Tabs[1]; uiWorkspace.EditorCommand.Execute("Body");
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var postBitmap = window.CaptureRenderedFrame()!;
            postBitmap.Save(Path.Combine(Path.GetDirectoryName(path)!, "osynt-post-preview.png"));
        }
        window.Close();
        Console.WriteLine($"\n{_passed} checks passed; {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static WorkspaceViewModel CreateWorkspace(string path, RequestExecutor executor, string demoBaseUrl) =>
        new(new WorkspaceService(new WorkspaceStore(path), new JsonCollectionSerializer(), demoBaseUrl), executor);

    private static void PumpUntil(Func<bool> complete)
    {
        var timeout = Stopwatch.StartNew();
        while (!complete() && timeout.Elapsed < TimeSpan.FromSeconds(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        Dispatcher.UIThread.RunJobs(); True(complete(), "Timed out waiting for the desktop operation.");
    }
    private static void Check(string name, Action action)
    {
        try { action(); _passed++; Console.WriteLine($"PASS  {name}"); }
        catch (Exception ex) { _failed++; Console.WriteLine($"FAIL  {name}\n      {ex}"); }
    }
    private static void True(bool condition, string message = "Condition was false")
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; got {actual}"); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(cancellationToken); }
}
