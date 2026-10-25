using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Osynt.Application.Workspaces;
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
            var workspace = new WorkspaceService(new WorkspaceStore(), new JsonCollectionSerializer(), _demo.BaseUrl);
            var viewModel = new WorkspaceViewModel(workspace, new RequestExecutor(_client));
            if (demoError is not null)
                viewModel.Notice = demoError;
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.Exit += (_, _) => { viewModel.Dispose(); _demo.Dispose(); _client.Dispose(); };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
