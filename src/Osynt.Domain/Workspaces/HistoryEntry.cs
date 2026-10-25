using Osynt.Domain.Requests;

namespace Osynt.Domain.Workspaces;

public sealed class HistoryEntry
{
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.Now;
    public RequestDocument Request { get; set; } = new();
    public int StatusCode
    {
        get; set;
    }
    public long ElapsedMilliseconds
    {
        get; set;
    }
}
