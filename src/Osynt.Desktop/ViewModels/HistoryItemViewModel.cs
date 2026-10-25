using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Desktop.ViewModels;

public sealed class HistoryItemViewModel(HistoryEntry entry)
{
    public RequestDocument Request => entry.Request;
    public string TimeLabel => entry.SentAt.ToLocalTime().ToString("HH:mm");
    public string StatusLabel => entry.StatusCode == 0 ? "ERR" : entry.StatusCode.ToString();
}
