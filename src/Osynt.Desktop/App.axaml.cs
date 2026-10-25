using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Osynt.Desktop.Composition;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class App : Avalonia.Application
{
    private DemoServer? _demo;
    private HttpClient? _client;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _demo = new DemoServer();
            string? demoError = null;
            try
            {
                _demo.Start();
            }
            catch (System.Net.Sockets.SocketException) { demoError = "The demo API could not start. You can still send requests to any HTTP API."; }
            _client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            var viewModel = WorkspaceComposition.Create(new WorkspaceStore(), new JsonCollectionSerializer(),
                new RequestExecutor(_client), _demo.BaseUrl);
            if (demoError is not null)
                viewModel.Status.Notice = demoError;
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.Exit += (_, _) => { viewModel.Dispose(); _demo.Dispose(); _client.Dispose(); };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
