using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class EnvironmentWindow : Window
{
    private readonly WorkspaceViewModel? _workspace;
    private readonly ObservableCollection<RequestPairViewModel> _variables = [];
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _loading;
    private bool _dirty;
    public EnvironmentWindow()
    {
        InitializeComponent();
        VariableList.ItemsSource = _variables;
        _saveTimer.Tick += (_, _) => Flush();
        _variables.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
                foreach (RequestPairViewModel pair in e.OldItems)
                    pair.PropertyChanged -= VariableChanged;
            if (e.NewItems is not null)
                foreach (RequestPairViewModel pair in e.NewItems)
                    pair.PropertyChanged += VariableChanged;
            QueueSave();
        };
        Closing += (_, e) => { if (!Flush()) e.Cancel = true; };
        Closed += (_, _) => _saveTimer.Stop();
    }
    public EnvironmentWindow(WorkspaceViewModel workspace) : this()
    {
        _workspace = workspace;
        _loading = true;
        foreach (var variable in workspace.Environment.Variables)
            _variables.Add(new()
            {
                Key = variable.Key,
                Value = variable.Value
            });
        _loading = false;
    }
    private void VariableChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();
    private void QueueSave()
    {
        if (_loading || _workspace is null)
            return;
        _dirty = true;
        SaveStatus.Text = "Saving…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }
    private bool Flush()
    {
        _saveTimer.Stop();
        if (!_dirty || _workspace is null)
            return true;
        try
        {
            if (!_workspace.Environment.UpdateVariables(_variables.Select(v => v.ToModel())))
            {
                ErrorLabel.Text = "Could not save. Your edits are still here; retry when the workspace is writable.";
                SaveStatus.Text = "Not saved";
                RetrySave.IsVisible = true;
                return false;
            }
            _dirty = false;
            ErrorLabel.Text = "";
            SaveStatus.Text = "All changes saved";
            RetrySave.IsVisible = false;
            return true;
        }
        catch (ArgumentException ex) { ErrorLabel.Text = ex.Message; SaveStatus.Text = "Not saved"; RetrySave.IsVisible = true; return false; }
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
        _loading = true;
        foreach (var pair in _variables)
            pair.PropertyChanged -= VariableChanged;
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
        _loading = false;
        QueueSave();
    }
    private void RetryClicked(object? sender, RoutedEventArgs e) => Flush();
    private void DoneClicked(object? sender, RoutedEventArgs e) => Close();
}
