using System.ComponentModel;
using System.Windows.Input;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

/// <summary>Coordinates navigation and refreshes between independent workspace features.</summary>
public sealed class WorkspaceViewModel : IDisposable
{
    public WorkspaceViewModel(RequestTabsViewModel requests, RequestSendingViewModel sending,
        RequestPaneViewModel editor, LibraryViewModel library, EnvironmentViewModel environment,
        GardenViewModel garden, WorkspaceStatus status)
    {
        Requests = requests;
        Sending = sending;
        Editor = editor;
        Library = library;
        Environment = environment;
        Garden = garden;
        Status = status;
        OpenGardenCommand = new ActionCommand(_ => Garden.IsOpen = true);
        CloseGardenCommand = new ActionCommand(_ => Garden.IsOpen = false);
        SendGardenDemoCommand = new ActionCommand(_ => _ = SendGardenDemoAsync());
        NewRequestCommand = new ActionCommand(_ => NewRequest());
        OpenRequestCommand = new ActionCommand(p => { if (p is LibraryRequest request) Open(request.Document); });
        OpenHistoryCommand = new ActionCommand(p => { if (p is HistoryItemViewModel entry) Requests.OpenHistory(entry.Request); });
        Requests.PropertyChanged += SelectionChanged;
        Requests.Saved += Library.RefreshCollections;
        Library.RequestsDeleted += Requests.DiscardDeleted;
        Library.RequestRenamed += Requests.RenameOpenRequest;
        Sending.Completed += RefreshAfterSend;
        Library.Select(Requests.Selected.Document.Id);
    }

    public RequestTabsViewModel Requests
    {
        get;
    }
    public RequestSendingViewModel Sending
    {
        get;
    }
    public RequestPaneViewModel Editor
    {
        get;
    }
    public LibraryViewModel Library
    {
        get;
    }
    public EnvironmentViewModel Environment
    {
        get;
    }
    public GardenViewModel Garden
    {
        get;
    }
    public WorkspaceStatus Status
    {
        get;
    }
    public ICommand OpenGardenCommand
    {
        get;
    }
    public ICommand CloseGardenCommand
    {
        get;
    }
    public ICommand SendGardenDemoCommand
    {
        get;
    }
    public ICommand NewRequestCommand
    {
        get;
    }
    public ICommand OpenRequestCommand
    {
        get;
    }
    public ICommand OpenHistoryCommand
    {
        get;
    }

    public void NewRequest()
    {
        Garden.IsOpen = false;
        Requests.NewRequest();
    }

    public void Open(RequestDocument document)
    {
        Requests.Open(document);
        Garden.IsOpen = false;
    }

    private void SelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RequestTabsViewModel.Selected))
            Library.Select(Requests.Selected.Document.Id);
    }

    private void RefreshAfterSend()
    {
        Library.RefreshHistory();
        Garden.Refresh();
    }

    private async Task SendGardenDemoAsync()
    {
        if (!Environment.DemoAvailable)
        {
            Status.Notice = "The local demo is unavailable. You can send a request to your own API instead.";
            return;
        }
        if (Requests.Selected.IsSending)
            return;
        var url = Environment.DemoBaseUrl + "/v1/health";
        var existing = Requests.Tabs.FirstOrDefault(t => t.Document.Name == "Garden health check" && t.Document.Url == url);
        if (existing is null)
            Requests.Open(new RequestDocument { Name = "Garden health check", Url = url });
        else
            Requests.Selected = existing;
        await Sending.SendSelectedAsync();
    }

    public void Dispose()
    {
        Requests.PropertyChanged -= SelectionChanged;
        Requests.Saved -= Library.RefreshCollections;
        Library.RequestsDeleted -= Requests.DiscardDeleted;
        Library.RequestRenamed -= Requests.RenameOpenRequest;
        Sending.Completed -= RefreshAfterSend;
        Requests.Dispose();
    }
}
