using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Osynt.Desktop.Services;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closingPrompt;
    private WorkspaceViewModel? Workspace => DataContext as WorkspaceViewModel;
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (Workspace is { DemoAvailable: true } workspace && workspace.Variables.GetValueOrDefault("base_url") == workspace.DemoBaseUrl && workspace.Selected.Document.Url.StartsWith("{{base_url}}", StringComparison.Ordinal))
                await workspace.SendSelectedAsync();
        };
        Closing += WindowClosing;
        KeyDown += async (_, e) =>
        {
            if ((e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) && e.Key == Key.W && Workspace is not null)
            {
                e.Handled = true;
                await new RequestCloseDialogs(this, Workspace).CloseRequestAsync(Workspace.Selected);
            }
        };
    }
    private void TitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.Source is Control control && (control is Button || control.GetVisualAncestors().Any(v => v is Button)))
            return;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            BeginMoveDrag(e);
    }
    private void MinimizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();
    private async void WindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || Workspace is not { } workspace || workspace.Tabs.All(t => !t.IsDirty))
            return;
        e.Cancel = true;
        if (_closingPrompt)
            return;
        _closingPrompt = true;
        try
        {
            if (!await new RequestCloseDialogs(this, workspace).CanCloseWorkspaceAsync())
                return;
            _allowClose = true;
            Close();
        }
        finally { _closingPrompt = false; }
    }
    private async void HelpClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window { Title = "A few little shortcuts", Width = 420, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new(30), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = "A few little shortcuts", FontSize = 23, FontWeight = FontWeight.SemiBold });
        foreach (var (label, shortcut) in new[] { ("Send / cancel request", "Ctrl / ⌘ + Enter"), ("Save request", "Ctrl / ⌘ + S"), ("New request", "Ctrl / ⌘ + N"), ("Close request tab", "Ctrl / ⌘ + W") })
        {
            var row = new Grid { ColumnDefinitions = new("*,Auto") };
            row.Children.Add(new TextBlock { Text = label, FontSize = 12 });
            var key = new TextBlock { Text = shortcut, FontSize = 11, Foreground = Brush.Parse("#899B73") };
            Grid.SetColumn(key, 1);
            row.Children.Add(key);
            panel.Children.Add(row);
        }
        var close = new Button { Content = "Happy exploring", HorizontalAlignment = HorizontalAlignment.Right, Classes = { "primary" } };
        close.Click += (_, _) => dialog.Close();
        panel.Children.Add(close);
        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }
}
