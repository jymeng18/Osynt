using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

public sealed class LibraryRequest(RequestDocument document) : ObservableModel
{
    private bool _selected;
    public RequestDocument Document { get; } = document;
    public string Name => Document.Name;
    public string Method => Document.Method;
    public string MethodColor => Method switch { "GET" => "#6E8C50", "POST" => "#BD9652", "DELETE" => "#B87966", _ => "#9A85AA" };
    public bool IsSelected
    {
        get => _selected; set => Set(ref _selected, value);
    }
}

