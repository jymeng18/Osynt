using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Osynt.Desktop.ViewModels;

public sealed class LibraryCollection(string name, IEnumerable<LibraryRequest> requests) : ObservableModel
{
    private bool _expanded = true;
    public string Name { get; } = name;
    public ObservableCollection<LibraryRequest> Requests { get; } = new(requests);
    public int Count => Requests.Count;
    public bool Expanded { get => _expanded; set { Set(ref _expanded, value); Raise(nameof(Chevron)); } }
    public string Chevron => Expanded ? "⌄" : "›";
    public ICommand ToggleCommand => new ActionCommand(_ => Expanded = !Expanded);
}

