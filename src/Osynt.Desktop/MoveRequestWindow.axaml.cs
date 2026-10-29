using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop;

public partial class MoveRequestWindow : Window
{
    private readonly LibraryViewModel? _library;
    private readonly Guid _requestId;

    public sealed record FolderChoice(Guid Id, string Name, int Count)
    {
        public override string ToString() => $"{Name} ({Count})";
    }

    public MoveRequestWindow() => InitializeComponent();

    public MoveRequestWindow(LibraryViewModel library, LibraryRequest request) : this()
    {
        _library = library;
        _requestId = request.Document.Id;
        RequestLabel.Text = request.Name;
        var collections = library.GetAllCollections();
        var choices = collections.Select(c => new FolderChoice(c.Id, c.Name, c.Requests.Count)).ToList();
        FolderPicker.ItemsSource = choices;
        var current = collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == _requestId));
        FolderPicker.SelectedItem = choices.FirstOrDefault(c => c.Id == current?.Id);
    }

    private void MoveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (FolderPicker.SelectedItem is not FolderChoice choice)
                throw new InvalidOperationException("Choose a destination folder.");
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
