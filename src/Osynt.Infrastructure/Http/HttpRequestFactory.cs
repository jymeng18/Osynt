using System.Text;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;

namespace Osynt.Infrastructure.Http;

public static class HttpRequestFactory
{
    public static HttpRequestMessage Create(RequestDocument document, IReadOnlyDictionary<string, string> variables)
    {
        if (document.TimeoutSeconds is < 1 or > 3600)
            throw new ArgumentException("Request timeout must be between 1 and 3600 seconds.");
        var resolved = RequestUrl.NormalizeAddress(TemplateResolver.Resolve(RequestUrl.Address(document.Url), variables));
        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Your URL needs to start with http:// or https://.");

        var builder = new UriBuilder(uri);
        var pairs = RequestUrl.EffectiveParameters(document.Url, document.Parameters)
            .Select(p => $"{Uri.EscapeDataString(TemplateResolver.Resolve(p.Key, variables))}={Uri.EscapeDataString(TemplateResolver.Resolve(p.Value, variables))}");
        builder.Query = string.Join("&", new[] { builder.Query.TrimStart('?') }.Concat(pairs).Where(q => q.Length > 0));

        var request = new HttpRequestMessage(new HttpMethod(document.Method), builder.Uri);
        try
        {
            var headers = document.Headers.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
                .Select(p => new KeyValuePair<string, string>(TemplateResolver.Resolve(p.Key.Trim(), variables),
                    TemplateResolver.Resolve(p.Value, variables))).ToArray();
            foreach (var pair in headers)
            {
                if (pair.Key.Length == 0 || pair.Key.Any(c => !IsHeaderCharacter(c)))
                    throw new ArgumentException($"The header name '{pair.Key}' is invalid. Use letters, digits, or HTTP token punctuation without spaces.");
                if (pair.Value.Contains('\r') || pair.Value.Contains('\n'))
                    throw new ArgumentException("Header names and values cannot contain line breaks.");
            }
            // An explicit disabled standard-header row suppresses that automatic header.
            var overrides = headers.Select(h => h.Key).Concat(document.Headers.Where(h => !h.Enabled).Select(h => h.Key.Trim()))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!overrides.Contains("Accept"))
                request.Headers.Accept.ParseAdd("*/*");
            if (!overrides.Contains("User-Agent"))
                request.Headers.UserAgent.ParseAdd("Osynt/0.2.0");
            if (!string.IsNullOrEmpty(document.Body))
                request.Content = new StringContent(TemplateResolver.Resolve(document.Body, variables), Encoding.UTF8, "application/json");

            foreach (var pair in headers)
            {
                var key = pair.Key;
                var value = pair.Value;
                if (!request.Headers.TryAddWithoutValidation(key, value))
                {
                    request.Content ??= new ByteArrayContent([]);
                    request.Content.Headers.Remove(key);
                    if (!request.Content.Headers.TryAddWithoutValidation(key, value))
                        throw new ArgumentException($"The header '{key}' could not be added.");
                }
            }
            return request;
        }
        catch { request.Dispose(); throw; }
    }

    private static bool IsHeaderCharacter(char c) => char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c);
}
