using System.Runtime.Versioning;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Osynt.Desktop.Rendering;

namespace Osynt.WindowsProbe;

internal static class Program
{
    internal static string OutputPath { get; private set; } = "";
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("This probe runs on Windows.");
            return 1;
        }
        OutputPath = args.Length == 1 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "osynt-windows-garden.png");
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        return AppBuilder.Configure<ProbeApplication>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}

[SupportedOSPlatform("windows")]
public sealed class ProbeApplication : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            // Exercise the actual native host offscreen, without opening a user-facing app window.
            var host = new WindowsGardenHost();
            var window = new Window
            {
                Width = 756,
                Height = 480,
                ShowInTaskbar = false,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Position = new PixelPoint(-24000, -24000),
                SystemDecorations = SystemDecorations.None,
                Content = host,
                Title = "Osynt renderer verification"
            };
            lifetime.MainWindow = window;
            var finished = false;
            void Fail(string message)
            {
                if (finished)
                    return;
                finished = true;
                Console.Error.WriteLine(message);
                File.WriteAllText(Program.OutputPath + ".json", JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = message
                }));
                lifetime.Shutdown(1);
            }
            host.RendererFailed += (_, message) => Fail(message);
            host.MessageReceived += async (_, message) =>
            {
                try
                {
                    using var payload = JsonDocument.Parse(message);
                    if (!payload.RootElement.TryGetProperty("type", out var type))
                        return;
                    if (type.GetString() == "renderer-error")
                    {
                        Fail(message);
                        return;
                    }
                    if (type.GetString() != "ready" || finished)
                        return;
                    var info = await host.CaptureDiagnosticsAsync(Program.OutputPath);
                    using var result = JsonDocument.Parse(info);
                    if (!result.RootElement.GetProperty("renderer").GetString()!.Contains("WebGL") ||
                        result.RootElement.GetProperty("successes").GetInt32() != 75)
                    {
                        Fail("The native renderer did not report a valid 3D frame.");
                        return;
                    }
                    File.WriteAllText(Program.OutputPath + ".json", info);
                    Console.WriteLine("PASS Native Windows WebView2, WebGL scene, visible Milestones dialog, bundled assets, and single-file loader");
                    Console.WriteLine(info);
                    finished = true;
                    lifetime.Shutdown(0);
                }
                catch (Exception error) { Fail(error.ToString()); }
            };
            DispatcherTimer.RunOnce(() => Fail("Windows garden initialization timed out."), TimeSpan.FromSeconds(45));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
