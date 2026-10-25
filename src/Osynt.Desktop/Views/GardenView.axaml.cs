using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Osynt.Desktop.Rendering;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class GardenView : UserControl
{
    private WorkspaceViewModel? _workspace;
    private WindowsGardenHost? _host;
    public GardenView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindWorkspace();
    }
    private void BindWorkspace()
    {
        if (_workspace is not null)
            _workspace.Garden.PropertyChanged -= GardenChanged;
        _workspace = DataContext as WorkspaceViewModel;
        if (_workspace is not null)
            _workspace.Garden.PropertyChanged += GardenChanged;
        RefreshSurface();
    }
    private void GardenChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GardenViewModel.IsOpen) or nameof(GardenViewModel.RenderMessage))
            RefreshSurface();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshSurface();
    }
    private void RefreshSurface()
    {
        if (_workspace is null || !OperatingSystem.IsWindows())
            return;
        if (_host is null && _workspace.Garden.IsOpen && TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.HandleDescriptor == "HWND")
        {
            _host = new WindowsGardenHost { Focusable = true };
            _host.MessageReceived += RendererMessage;
            _host.RendererFailed += (_, message) =>
            {
                _host.IsVisible = false;
                RendererFallback.IsVisible = true;
                RendererStatus.Text = message;
            };
            RendererContainer.Children.Add(_host);
        }
        _host?.SetActive(_workspace.Garden.IsOpen);
        _host?.Publish(_workspace.Garden.RenderMessage);
    }
    private void RendererMessage(object? sender, string message)
    {
        if (_workspace is null || !OperatingSystem.IsWindows())
            return;
        try
        {
            using var json = JsonDocument.Parse(message);
            if (!json.RootElement.TryGetProperty("type", out var type))
                return;
            switch (type.GetString())
            {
                case "ready":
                    RendererFallback.IsVisible = false;
                    _host?.Publish(_workspace.Garden.RenderMessage);
                    _host?.SetActive(_workspace.Garden.IsOpen);
                    break;
                case "preferences":
                    if (json.RootElement.TryGetProperty("atmosphere", out var atmosphere) &&
                        json.RootElement.TryGetProperty("reducedMotion", out var motion) &&
                        atmosphere.ValueKind == JsonValueKind.String && motion.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        if (!_workspace.Garden.UpdatePreferences(atmosphere.GetString()!, motion.GetBoolean()))
                            _host?.Publish(_workspace.Garden.RenderMessage);
                    }
                    break;
                case "back":
                    _workspace.Garden.IsOpen = false;
                    break;
                case "send":
                    _workspace.Sending.SendCommand.Execute(null);
                    break;
                case "renderer-error":
                    RendererStatus.Text = "The 3D renderer could not start. Your garden progress is still saved. Try restarting Osynt.";
                    RendererFallback.IsVisible = true;
                    if (_host is not null)
                        _host.IsVisible = false;
                    break;
            }
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            _workspace.Status.Notice = "The garden couldn't apply this setting. " + error.Message;
        }
    }
}
