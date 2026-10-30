using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;

namespace Osynt.Desktop.Rendering;

/// <summary>A Windows-only WebGL surface; it never receives request bodies, headers, or credentials.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGardenHost : NativeControlHost
{
    private const string Origin = "https://garden.osynt.local";
    private nint _window;
    private long _generation;
    private CoreWebView2Controller? _controller;
    private string _latestState = "{\"type\":\"state\",\"successes\":0}";
    private bool _active = true;
    private bool _ready;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? RendererFailed;
    public WindowsGardenHost() => LayoutUpdated += (_, _) => ResizeBrowser();

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _window = CreateWindowEx(0, "STATIC", "Osynt Garden", 0x40000000 | 0x10000000 | 0x04000000 | 0x02000000,
            0, 0, 100, 100, parent.Handle, 0, 0, 0);
        if (_window == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var generation = ++_generation;
        _ = InitializeAsync(_window, generation);
        return new PlatformHandle(_window, "HWND");
    }

    private async Task InitializeAsync(nint window, long generation)
    {
        try
        {
            var folder = GardenAssets.Extract();
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Osynt", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            if (generation != _generation || _window != window)
                return;
            var controller = await environment.CreateCoreWebView2ControllerAsync(window);
            if (generation != _generation || _window != window)
            {
                controller.Close();
                return;
            }
            _controller = controller;
            controller.DefaultBackgroundColor = System.Drawing.Color.FromArgb(248, 247, 240);
            controller.IsVisible = _active;
            ResizeBrowser();
            var browser = controller.CoreWebView2;
            browser.Settings.AreDefaultContextMenusEnabled = false;
            browser.Settings.AreDevToolsEnabled = false;
            browser.Settings.AreDefaultScriptDialogsEnabled = false;
            browser.Settings.AreBrowserAcceleratorKeysEnabled = false;
            browser.Settings.IsStatusBarEnabled = false;
            browser.Settings.IsZoomControlEnabled = false;
            browser.SetVirtualHostNameToFolderMapping("garden.osynt.local", folder, CoreWebView2HostResourceAccessKind.DenyCors);
            browser.NavigationStarting += (_, e) =>
            {
                if (!IsLocalResource(e.Uri))
                    e.Cancel = true;
            };
            browser.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            browser.WebResourceRequested += (_, e) =>
            {
                if (!IsLocalResource(e.Request.Uri))
                    e.Response = environment.CreateWebResourceResponse(Stream.Null, 403, "External resources are disabled", "Content-Type: text/plain");
            };
            browser.WebMessageReceived += (_, e) =>
            {
                if (!IsLocalResource(e.Source))
                    return;
                _ready = true;
                MessageReceived?.Invoke(this, e.WebMessageAsJson);
            };
            browser.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess)
                    RendererFailed?.Invoke(this, "The bundled garden page could not be loaded.");
            };
            browser.ProcessFailed += (_, _) => RendererFailed?.Invoke(this, "The garden renderer stopped. Close and reopen Osynt to reconnect.");
            browser.Navigate(Origin + "/index.html");
        }
        catch (Exception error) when (error is COMException or WebView2RuntimeNotFoundException or DllNotFoundException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            if (generation == _generation)
                RendererFailed?.Invoke(this, "The 3D garden needs the Microsoft Edge WebView2 Runtime. Your requests and garden progress are still saved. " + error.Message);
        }
    }

    private static bool IsLocalResource(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
        parsed.Scheme == "https" && parsed.Host == "garden.osynt.local";

    public void Publish(string state)
    {
        _latestState = state;
        if (_ready)
            _controller?.CoreWebView2.PostWebMessageAsJson(_latestState);
    }
    public void SetActive(bool active)
    {
        _active = active;
        if (_controller is null)
            return;
        _controller.IsVisible = active;
        if (_ready)
            _controller.CoreWebView2.PostWebMessageAsJson(active ? "{\"type\":\"visibility\",\"visible\":true}" : "{\"type\":\"visibility\",\"visible\":false}");
    }
    internal async Task<string> CaptureDiagnosticsAsync(string path)
    {
        if (_controller is null)
            throw new InvalidOperationException("The renderer is not ready.");
        // This method is only available to the separate Windows integration probe.
        await _controller.CoreWebView2.ExecuteScriptAsync("window.garden.receive({type:'state',successes:75,atmosphere:'day',reducedMotion:true});window.garden.freeze(6);");
        var info = await _controller.CoreWebView2.ExecuteScriptAsync("window.garden.info()");
        await using var image = File.Create(path);
        await _controller.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
        var milestones = await _controller.CoreWebView2.ExecuteScriptAsync("""
            (() => {
                const button = document.getElementById('open-milestones');
                const hit = button.getBoundingClientRect();
                if (document.elementFromPoint(hit.x + hit.width / 2, hit.y + hit.height / 2) !== button) return false;
                button.click();
                const dialog = document.getElementById('milestones');
                const bounds = dialog.getBoundingClientRect();
                return dialog.open && getComputedStyle(dialog).display !== 'none'
                    && bounds.width > 0 && bounds.height > 0
                    && bounds.top >= 0 && bounds.bottom <= innerHeight;
            })()
            """);
        if (milestones != "true")
            throw new InvalidOperationException("The native Milestones button did not open a visible dialog.");
        await using var dialogImage = File.Create(Path.ChangeExtension(path, ".milestones.png"));
        await _controller.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, dialogImage);
        await _controller.CoreWebView2.ExecuteScriptAsync("document.getElementById('close-milestones').click()");
        return info;
    }
    private void ResizeBrowser()
    {
        if (_controller is null || _window == 0 || !GetClientRect(_window, out var rectangle))
            return;
        _controller.Bounds = new System.Drawing.Rectangle(0, 0, Math.Max(1, rectangle.Right), Math.Max(1, rectangle.Bottom));
        _controller.NotifyParentWindowPositionChanged();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
            ResizeBrowser();
    }
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        _controller?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
    }
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        ++_generation;
        _ready = false;
        _controller?.Close();
        _controller = null;
        _window = 0;
        DestroyWindow(control.Handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left; public int Top; public int Right; public int Bottom;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(int extendedStyle, string className, string name, int style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out NativeRectangle rectangle);
}
