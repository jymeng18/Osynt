using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Core;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class EnvironmentWindow : Window
{
    private readonly WorkspaceViewModel? _workspace;
    private readonly ObservableCollection<RequestPair> _variables = [];
    public EnvironmentWindow()
    {
        InitializeComponent();
        VariableList.ItemsSource = _variables;
    }
    public EnvironmentWindow(WorkspaceViewModel workspace) : this()
    {
        _workspace = workspace;
        foreach (var variable in workspace.Variables) _variables.Add(new() { Key = variable.Key, Value = variable.Value });
    }
    private void AddClicked(object? sender, RoutedEventArgs e) => _variables.Add(new());
    private void RemoveClicked(object? sender, RoutedEventArgs e) { if (sender is Button { Tag: RequestPair pair }) _variables.Remove(pair); }
    private void ResetClicked(object? sender, RoutedEventArgs e)
    {
        if (_workspace is not { DemoAvailable: true }) { ErrorLabel.Text = "The local demo is unavailable. Try restarting Osynt."; return; }
        _variables.Clear(); _variables.Add(new() { Key = "base_url", Value = _workspace.DemoBaseUrl }); _variables.Add(new() { Key = "season", Value = "spring" });
    }
    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        try { _workspace?.UpdateVariables(_variables); Close(); }
        catch (ArgumentException ex) { ErrorLabel.Text = ex.Message; }
    }
    private void CancelClicked(object? sender, RoutedEventArgs e) => Close();
}
