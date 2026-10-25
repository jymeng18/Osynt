using System.Text;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;

namespace Osynt.Infrastructure.Http;

public static class HttpRequestFactory
{
    public static HttpRequestMessage Create(RequestDocument document, IReadOnlyDictionary<string, string> variables)
    {
        var resolved = TemplateResolver.Resolve(document.Url.Trim(), variables);
        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Your URL needs to start with http:// or https://.");

        var builder = new UriBuilder(uri);
        var pairs = document.Parameters.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
            .Select(p => $"{Uri.EscapeDataString(TemplateResolver.Resolve(p.Key, variables))}={Uri.EscapeDataString(TemplateResolver.Resolve(p.Value, variables))}");
        builder.Query = string.Join("&", new[] { builder.Query.TrimStart('?') }.Concat(pairs).Where(q => q.Length > 0));

        var request = new HttpRequestMessage(new HttpMethod(document.Method), builder.Uri);
        try
        {
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
