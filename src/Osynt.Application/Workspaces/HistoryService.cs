using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Application.Workspaces;

public sealed class HistoryService(WorkspaceSession session, TimeProvider clock)
{
    public const int HistoryLimit = 100;
    public IReadOnlyList<HistoryEntry> GetHistory() =>
        session.Read(state => state.History.Select(h => h.Clone()).ToList());

    public void Record(RequestDocument request, int statusCode, long elapsedMilliseconds)
    {
        var now = clock.GetLocalNow();
        session.Commit(state =>
        {
            state.History.Insert(0, new HistoryEntry
            {
                SentAt = now,
                Request = request.Clone(),
                StatusCode = statusCode,
                ElapsedMilliseconds = elapsedMilliseconds
            });
            if (statusCode is >= 200 and < 300)
                state.Garden!.Grow(now);
            if (state.History.Count > HistoryLimit)
                state.History.RemoveRange(HistoryLimit, state.History.Count - HistoryLimit);
        });
    }

    public void Clear() => session.Commit(state => state.History.Clear());
}
