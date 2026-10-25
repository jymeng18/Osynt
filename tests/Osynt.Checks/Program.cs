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

/// <summary>A dependency-light integration runner. Any failed check exits with code 1.</summary>
internal static partial class Program
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

        RunRequestChecks(demo, executor, variables);
        RunWorkspaceChecks(directory, executor, demo);
        RunApplicationChecks();
        RunDesktopChecks(directory, executor, demo, args);
        Console.WriteLine($"\n{_passed} checks passed; {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static WorkspaceViewModel CreateWorkspace(string path, RequestExecutor executor, string demoBaseUrl) =>
        new(new WorkspaceService(new WorkspaceStore(path), new JsonCollectionSerializer(), demoBaseUrl), executor);

    private static void PumpUntil(Func<bool> complete)
    {
        var timeout = Stopwatch.StartNew();
        while (!complete() && timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
        True(complete(), "Timed out waiting for the desktop operation.");
    }
    private static void Check(string name, Action action)
    {
        try
        {
            action();
            _passed++;
            Console.WriteLine($"PASS  {name}");
        }
        catch (Exception ex) { _failed++; Console.WriteLine($"FAIL  {name}\n      {ex}"); }
    }
    private static void True(bool condition, string message = "Condition was false")
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; got {actual}");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }
    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(cancellationToken);
    }
}
