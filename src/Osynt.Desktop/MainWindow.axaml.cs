using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closingPrompt;
    private WorkspaceViewModel? Workspace => DataContext as WorkspaceViewModel;
    private static readonly FilePickerFileType JsonFile = new("JSON files") { Patterns = ["*.json"] };
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
            { e.Handled = true; await CloseRequestAsync(Workspace.Selected); }
        };
    }
    private void TitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is Button) return;
        if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else BeginMoveDrag(e);
    }
    private void MinimizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();
    private void DiscardTabClicked(object? sender, RoutedEventArgs e)
    { if (sender is MenuItem { Tag: RequestTab tab }) Workspace?.DiscardTab(tab); }
    private async void CloseTabClicked(object? sender, RoutedEventArgs e)
    { if (sender is Button { Tag: RequestTab tab }) await CloseRequestAsync(tab); }
    private async Task CloseRequestAsync(RequestTab tab)
    {
        if (Workspace is not { } workspace) return;
        if (tab.IsDirty)
        {
            var choice = await AskToSaveAsync($"Save “{tab.Document.Name}”?", "Give this request a home before closing it.");
            if (choice == "cancel") return;
            if (choice == "save") { workspace.Selected = tab; workspace.SaveSelected(); if (tab.IsDirty) return; }
        }
        workspace.DiscardTab(tab);
    }
    private async void WindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || Workspace is not { } workspace || workspace.Tabs.All(t => !t.IsDirty)) return;
        e.Cancel = true;
        if (_closingPrompt) return;
        _closingPrompt = true;
        try
        {
            var choice = await AskToSaveAsync("Tuck your requests away?", "Some requests have unsaved changes. Save them before heading out?");
            if (choice == "cancel") return;
            if (choice == "save")
            {
                var selected = workspace.Selected;
                foreach (var tab in workspace.Tabs.Where(t => t.IsDirty).ToArray())
                { workspace.Selected = tab; workspace.SaveSelected(); if (tab.IsDirty) { workspace.Selected = selected; return; } }
            }
            _allowClose = true;
            Close();
        }
        finally { _closingPrompt = false; }
    }
    private Task<string> AskToSaveAsync(string title, string description)
    {
        var dialog = new Window { Title = title, Width = 490, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush.Parse("#F9F7F1") };
        var panel = new StackPanel { Margin = new(28), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = Brush.Parse("#8D9680"), TextWrapping = TextWrapping.Wrap, LineHeight = 20 });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 10, 0, 0) };
        foreach (var (label, value) in new[] { ("Keep editing", "cancel"), ("Discard", "discard"), ("Save & close", "save") })
        {
            var button = new Button { Content = label };
            if (value == "save") button.Classes.Add("primary");
            button.Click += (_, _) => dialog.Close(value);
            buttons.Children.Add(button);
        }
        panel.Children.Add(buttons); dialog.Content = panel;
        return AwaitChoice();
        async Task<string> AwaitChoice() => await dialog.ShowDialog<string?>(this) ?? "cancel";
    }
    private async void EnvironmentClicked(object? sender, RoutedEventArgs e)
    {
        if (Workspace is not { } workspace) return;
        await new EnvironmentWindow(workspace).ShowDialog(this);
    }
    private async void CopyResponseClicked(object? sender, RoutedEventArgs e)
    {
        if (Workspace is not { Selected.HasResponse: true } workspace || Clipboard is null) return;
        try { await Clipboard.SetTextAsync(workspace.Selected.ResponseDisplay); workspace.Notice = "Response copied. A little discovery to take with you."; }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException) { workspace.Notice = "Could not reach the clipboard. Try Save response instead."; }
    }
    private async void SaveResponseClicked(object? sender, RoutedEventArgs e)
    {
        if (Workspace is not { Selected.Response: { } response } workspace) return;
        await SaveTextAsync("Save response", response.Body.TrimStart().StartsWith('{') || response.Body.TrimStart().StartsWith('[') ? "response.json" : "response.txt", workspace.Selected.ResponseDisplay);
    }
    private async void ExportClicked(object? sender, RoutedEventArgs e)
    { if (Workspace is { } workspace) await SaveTextAsync("Export Osynt collections", "osynt-collections.json", workspace.ExportCollections()); }
    private async Task SaveTextAsync(string title, string name, string text)
    {
        if (Workspace is not { } workspace) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new() { Title = title, SuggestedFileName = name, DefaultExtension = Path.GetExtension(name), FileTypeChoices = [JsonFile, FilePickerFileTypes.All] });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(text);
            workspace.Notice = $"Saved {file.Name}. A little piece of progress.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { workspace.Notice = $"Could not save this file: {ex.Message}"; }
    }
    private async void ImportClicked(object? sender, RoutedEventArgs e)
    {
        if (Workspace is not { } workspace) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Import Osynt collections", AllowMultiple = false, FileTypeFilter = [JsonFile] });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            workspace.ImportCollections(await reader.ReadToEndAsync());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { workspace.Notice = $"Could not import this file. Choose an Osynt collection JSON export. {ex.Message}"; }
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
            Grid.SetColumn(key, 1); row.Children.Add(key); panel.Children.Add(row);
        }
        var close = new Button { Content = "Happy exploring", HorizontalAlignment = HorizontalAlignment.Right, Classes = { "primary" } };
        close.Click += (_, _) => dialog.Close(); panel.Children.Add(close); dialog.Content = panel;
        await dialog.ShowDialog(this);
    }
}
