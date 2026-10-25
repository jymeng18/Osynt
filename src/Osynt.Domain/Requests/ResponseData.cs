namespace Osynt.Domain.Requests;

public sealed record ResponseData(int StatusCode, string Reason, long ElapsedMilliseconds,
    long SizeBytes, string Body, IReadOnlyList<KeyValuePair<string, string>> Headers, bool Truncated = false)
{
    public bool IsSuccess => StatusCode is >= 200 and < 400;
}
