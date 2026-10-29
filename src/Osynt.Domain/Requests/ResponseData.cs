namespace Osynt.Domain.Requests;

public sealed record ResponseData(int StatusCode, string Reason, long ElapsedMilliseconds,
    long SizeBytes, string Body, IReadOnlyList<KeyValuePair<string, string>> Headers, bool Truncated = false, byte[]? BinaryBody = null)
{
    public bool IsSuccess => StatusCode is >= 200 and < 400;
    public bool IsBinary => BinaryBody is not null;
    public string ContentType => Headers.FirstOrDefault(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? "";
}
