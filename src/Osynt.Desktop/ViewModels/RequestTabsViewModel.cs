using System.Collections.ObjectModel;
using System.Windows.Input;
using Osynt.Application.Workspaces;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

public sealed class RequestTabsViewModel : ObservableModel, IDisposable
{
    private readonly CollectionService _collections;
    private readonly WorkspaceStatus _status;
    private RequestTab _selected;

    public RequestTabsViewModel(CollectionService collections, WorkspaceStatus status)
    {
        _collections = collections;
        _status = status;
        _selected = new RequestTab(new RequestDocument()) { IsSelected = true };
        Tabs.Add(_selected);
        SelectCommand = new ActionCommand(p => { if (p is RequestTab tab) Selected = tab; });
        SaveCommand = new ActionCommand(_ => SaveSelected());
    }

    public event Action? Saved;
    public ObservableCollection<RequestTab> Tabs { get; } = [];
    public ICommand SelectCommand
    {
        get;
    }
    public ICommand SaveCommand
    {
        get;
    }
    public RequestTab Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value) || !Tabs.Contains(value))
                return;
            _selected.IsSelected = false;
            Set(ref _selected, value);
            value.IsSelected = true;
        }
    }

    public void NewRequest()
    {
        Add(new RequestDocument());
        _status.Notice = "New request";
    }

    public void Open(RequestDocument document)
    {
        var existing = Tabs.FirstOrDefault(t => t.Document.Id == document.Id);
        if (existing is not null)
            Selected = existing;
        else
            Add(document.Clone());
    }

    public void OpenHistory(RequestDocument document) => Add(document.Clone(newId: true), dirty: true);

    private void Add(RequestDocument document, bool dirty = false)
    {
        var tab = new RequestTab(document, dirty);
        Tabs.Add(tab);
        Selected = tab;
    }

    public void Close(RequestTab tab)
    {
        if (tab.IsDirty)
        {
            _status.Notice = "This request has unsaved changes. Save it before closing, or use Discard from the tab menu.";
            return;
        }
        Discard(tab);
    }

    public void Discard(RequestTab tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;
        var wasSelected = Selected == tab;
        Tabs.Remove(tab);
        tab.Dispose();
        if (Tabs.Count == 0)
            NewRequest();
        else if (wasSelected)
            Selected = Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
    }

    public void SaveSelected()
    {
        var tab = Selected;
        if (string.IsNullOrWhiteSpace(tab.Document.Name))
            tab.Document.Name = "Untitled request";
        if (!_status.TrySave(() => _collections.SaveRequest(tab.Document.Clone()), $"Saved {tab.Document.Name}"))
            return;
        tab.IsDirty = false;
        Saved?.Invoke();
    }

    public void DiscardDeleted(IReadOnlySet<Guid> requestIds)
    {
        foreach (var tab in Tabs.Where(t => requestIds.Contains(t.Document.Id)).ToArray())
            Discard(tab);
    }

    public void RenameOpenRequest(Guid requestId, string name)
    {
        foreach (var tab in Tabs.Where(t => t.Document.Id == requestId))
        {
            var wasDirty = tab.IsDirty;
            tab.Document.Name = name;
            tab.IsDirty = wasDirty;
        }
    }

    public void Dispose()
    {
        foreach (var tab in Tabs)
            tab.Dispose();
    }
}
