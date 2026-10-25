using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class EnvironmentWindow : Window
{
    private readonly WorkspaceViewModel? _workspace;
    private readonly ObservableCollection<RequestPairViewModel> _variables = [];
    public EnvironmentWindow()
    {
        InitializeComponent();
        VariableList.ItemsSource = _variables;
    }
    public EnvironmentWindow(WorkspaceViewModel workspace) : this()
    {
        _workspace = workspace;
        foreach (var variable in workspace.Environment.Variables)
            _variables.Add(new()
            {
                Key = variable.Key,
                Value = variable.Value
            });
    }
    private void AddClicked(object? sender, RoutedEventArgs e) => _variables.Add(new());
    private void RemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RequestPairViewModel pair })
            _variables.Remove(pair);
    }
    private void ResetClicked(object? sender, RoutedEventArgs e)
    {
        if (_workspace?.Environment.DemoAvailable != true)
        {
            ErrorLabel.Text = "The local demo is unavailable. Try restarting Osynt.";
            return;
        }
        _variables.Clear();
        _variables.Add(new()
        {
            Key = "base_url",
            Value = _workspace.Environment.DemoBaseUrl
        });
        _variables.Add(new()
        {
            Key = "season",
            Value = "spring"
        });
    }
    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_workspace?.Environment.UpdateVariables(_variables.Select(v => v.ToModel())) == true)
                Close();
            else
                ErrorLabel.Text = "The environment could not be saved. Check the workspace file permissions.";
        }
        catch (ArgumentException ex) { ErrorLabel.Text = ex.Message; }
    }
    private void CancelClicked(object? sender, RoutedEventArgs e) => Close();
}
