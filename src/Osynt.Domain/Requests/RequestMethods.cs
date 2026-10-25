namespace Osynt.Domain.Requests;

public static class RequestMethods
{
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" });
}
