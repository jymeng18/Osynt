using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class MoveRequestWindow : Window
{
    private readonly LibraryViewModel? _library;
    private readonly Guid _requestId;

    private List<FolderChoice> _choices = [];
    private Guid? _sourceId;

    public sealed record FolderChoice(Guid Id, string Name, int Count, bool IsCurrent = false)
    {
        public string Detail => $"{Count} request{(Count == 1 ? "" : "s")}";
        public string Marker => IsCurrent ? "Current folder" : "";
        public override string ToString() => $"{Name} ({Count})";
    }

    public MoveRequestWindow() => InitializeComponent();

    public MoveRequestWindow(LibraryViewModel library, LibraryRequest request) : this()
    {
        _library = library;
        _requestId = request.Document.Id;
        RequestLabel.Text = request.Name;
        var collections = library.GetAllCollections();
        var current = collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == _requestId));
        _sourceId = current?.Id;
        SourceLabel.Text = $"Currently in {current?.Name ?? "Unknown folder"}";
        _choices = collections.Select(c => new FolderChoice(c.Id, c.Name, c.Requests.Count, c.Id == _sourceId)).ToList();
        FolderPicker.ItemsSource = _choices;
        Opened += (_, _) => FolderSearch.Focus();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (FolderPicker is null)
            return;
        var selected = FolderPicker.SelectedItem;
        var matches = _choices.Where(c => c.Name.Contains(FolderSearch.Text?.Trim() ?? "", StringComparison.OrdinalIgnoreCase)).ToList();
        FolderPicker.ItemsSource = matches;
        FolderPicker.SelectedItem = selected is FolderChoice choice && matches.Contains(choice) ? selected : null;
        EmptyLabel.IsVisible = matches.Count == 0;
    }

    private void DestinationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MoveButton is null || DestinationLabel is null)
            return;
        var choice = FolderPicker.SelectedItem as FolderChoice;
        MoveButton.IsEnabled = choice is not null && choice.Id != _sourceId;
        DestinationLabel.Text = MoveButton.IsEnabled ? $"Move to {choice!.Name}" : "Choose a different folder above.";
        ErrorLabel.IsVisible = false;
    }

    private void MoveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (FolderPicker.SelectedItem is not FolderChoice choice)
                throw new InvalidOperationException("Choose a destination folder.");
            if (choice.Id == _sourceId)
                return;
            if (_library?.MoveRequest(_requestId, choice.Id) == true)
            {
                Close();
                return;
            }
            ShowError("Could not save the move. Your request is still in its original folder.");
        }
        catch (InvalidOperationException error) { ShowError(error.Message); }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close();
}
