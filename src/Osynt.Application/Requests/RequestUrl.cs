using System.Text.RegularExpressions;
using Osynt.Domain.Requests;

namespace Osynt.Application.Requests;

/// <summary>One query model shared by the URL editor, parameter table, and HTTP adapter.</summary>
public static partial class RequestUrl
{
    public static string Address(string url)
    {
        var fragmentIndex = url.IndexOf('#');
        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0 || fragmentIndex >= 0 && queryIndex > fragmentIndex)
            return url;
        return url[..queryIndex] + (fragmentIndex >= 0 ? url[fragmentIndex..] : "");
    }

    public static IReadOnlyList<RequestPair> ParseParameters(string url)
    {
        var fragmentIndex = url.IndexOf('#');
        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0 || fragmentIndex >= 0 && queryIndex > fragmentIndex)
            return [];
        var query = url[(queryIndex + 1)..(fragmentIndex < 0 ? url.Length : fragmentIndex)];
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(part =>
        {
            var pair = part.Split('=', 2);
            return new RequestPair { Key = Decode(pair[0]), Value = pair.Length > 1 ? Decode(pair[1]) : "" };
        }).ToList();
    }

    // Parameter rows override the corresponding URL keys, including disabled rows.
    // Repeated keys remain repeated, while values represented in both places are sent once.
    public static IReadOnlyList<RequestPair> EffectiveParameters(string url, IEnumerable<RequestPair> rows)
    {
        var parameters = rows.Where(p => !string.IsNullOrWhiteSpace(p.Key)).ToList();
        var overridden = parameters.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        return ParseParameters(url).Where(p => !overridden.Contains(p.Key))
            .Concat(parameters.Where(p => p.Enabled)).ToList();
    }

    public static string WithParameters(string url, IEnumerable<RequestPair> parameters)
    {
        var address = Address(url);
        var fragmentIndex = address.IndexOf('#');
        var query = string.Join('&', parameters.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
            .Select(p => $"{EncodeTemplate(p.Key)}={EncodeTemplate(p.Value)}"));
        if (query.Length == 0)
            return address;
        return fragmentIndex < 0 ? $"{address}?{query}" : $"{address[..fragmentIndex]}?{query}{address[fragmentIndex..]}";
    }

    public static string NormalizeAddress(string address)
    {
        var trimmed = address.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Enter an API URL, such as http://localhost:8080/health.");
        return trimmed.Contains("://", StringComparison.Ordinal) ? trimmed
            : trimmed.StartsWith("//", StringComparison.Ordinal) ? "http:" + trimmed : "http://" + trimmed;
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
    private static string EncodeTemplate(string value) => string.Concat(TemplateToken().Split(value)
        .Select(part => part.StartsWith("{{", StringComparison.Ordinal) && part.EndsWith("}}", StringComparison.Ordinal)
            ? part : Uri.EscapeDataString(part)));

    [GeneratedRegex(@"(\{\{\s*[^{}\s]+\s*\}\})")]
    private static partial Regex TemplateToken();
}
