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
            if (!document.Headers.Any(h => h.Key.Trim().Equals("Accept", StringComparison.OrdinalIgnoreCase)))
                request.Headers.Accept.ParseAdd("*/*");
            if (!document.Headers.Any(h => h.Key.Trim().Equals("User-Agent", StringComparison.OrdinalIgnoreCase)))
                request.Headers.UserAgent.ParseAdd("Osynt/0.2.0");
            if (!string.IsNullOrEmpty(document.Body))
                request.Content = new StringContent(TemplateResolver.Resolve(document.Body, variables), Encoding.UTF8, "application/json");

            foreach (var pair in document.Headers.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key)))
            {
                var key = TemplateResolver.Resolve(pair.Key.Trim(), variables);
                var value = TemplateResolver.Resolve(pair.Value, variables);
                if (key.Contains('\r') || key.Contains('\n') || value.Contains('\r') || value.Contains('\n'))
                    throw new ArgumentException("Header names and values cannot contain line breaks.");
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

}
