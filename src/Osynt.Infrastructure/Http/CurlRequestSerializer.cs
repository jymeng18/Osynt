using System.Globalization;
using System.Text;
using Osynt.Application.Abstractions;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;

namespace Osynt.Infrastructure.Http;

/// <summary>Converts a single literal POSIX cURL command without invoking a shell or reading files.</summary>
public sealed class CurlRequestSerializer : ICurlRequestSerializer
{
    public RequestDocument Deserialize(string command)
    {
        var tokens = Tokenize(command);
        if (tokens.Count == 0 || tokens[0] is not ("curl" or "curl.exe"))
            throw new ArgumentException("Paste a command beginning with curl or curl.exe.");
        var document = new RequestDocument { Name = "Imported cURL request", BodyMode = RequestBodyModes.Text };
        string? url = null;
        string? method = null;
        var get = false;
        var head = false;
        var hasData = false;
        var json = false;
        var data = new StringBuilder();
        for (var i = 1; i < tokens.Count; i++)
        {
            var option = tokens[i];
            string? inline = null;
            if (option.StartsWith("--", StringComparison.Ordinal) && option.IndexOf('=') is var equal && equal > 2)
            {
                inline = option[(equal + 1)..];
                option = option[..equal];
            }
            else if (option.Length > 2 && option[0] == '-' && option[1] != '-')
            {
                for (var letter = 1; letter < option.Length; letter++)
                {
                    var flag = option[letter];
                    if ("XHduAme".Contains(flag))
                    {
                        inline = letter + 1 < option.Length ? option[(letter + 1)..] : null;
                        option = "-" + flag;
                        break;
                    }
                    if (flag == 'G')
                        get = true;
                    else if (flag == 'I')
                        head = true;
                    else if (!"sSiLgq".Contains(flag))
                        throw new ArgumentException($"The cURL option -{flag} is not supported. Use separate flags for each option.");
                    if (letter == option.Length - 1)
                        option = "";
                }
                if (option.Length == 0)
                    continue;
            }
            string Value()
            {
                if (inline is not null)
                    return inline;
                if (++i >= tokens.Count)
                    throw new ArgumentException($"The {option} option needs a value.");
                return tokens[i];
            }
            void AddData(string value, string separator = "&")
            {
                if (hasData)
                    data.Append(separator);
                data.Append(value);
                hasData = true;
            }
            switch (option)
            {
                case "--url":
                    SetUrl(Value());
                    break;
                case "-X":
                case "--request":
                    method = Value().ToUpperInvariant();
                    break;
                case "-H":
                case "--header":
                    AddHeader(document, Value());
                    break;
                case "-A":
                case "--user-agent":
                    document.Headers.Add(new()
                    {
                        Key = "User-Agent",
                        Value = Value()
                    });
                    break;
                case "-e":
                case "--referer":
                    document.Headers.Add(new()
                    {
                        Key = "Referer",
                        Value = Value()
                    });
                    break;
                case "-u":
                case "--user":
                    var credentials = Value().Split(':', 2);
                    if (credentials.Length != 2)
                        throw new ArgumentException("Supply Basic credentials as username:password; interactive password prompts are not supported.");
                    document.Authentication = new()
                    {
                        Mode = RequestAuthentication.Basic,
                        Username = credentials[0],
                        Password = credentials[1]
                    };
                    break;
                case "--oauth2-bearer":
                    document.Authentication = new()
                    {
                        Mode = RequestAuthentication.Bearer,
                        Token = Value()
                    };
                    break;
                case "-m":
                case "--max-time":
                    if (!int.TryParse(Value(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds is < 1 or > 3600)
                        throw new ArgumentException("Osynt supports whole-second timeouts between 1 and 3600 seconds.");
                    document.TimeoutSeconds = seconds;
                    break;
                case "-d":
                case "--data":
                case "--data-ascii":
                case "--data-binary":
                    AddData(LiteralData(Value()));
                    break;
                case "--data-raw":
                    AddData(Value());
                    break;
                case "--data-urlencode":
                    AddData(EncodeData(Value()));
                    break;
                case "--json":
                    AddData(LiteralData(Value()), "");
                    json = true;
                    break;
                case "-G":
                case "--get":
                    if (inline is not null)
                        throw new ArgumentException("The --get option does not take a value.");
                    get = true;
                    break;
                case "-I":
                case "--head":
                    if (inline is not null)
                        throw new ArgumentException("The --head option does not take a value.");
                    head = true;
                    break;
                case "-L":
                case "--location":
                case "--compressed":
                case "-g":
                case "--globoff":
                case "-s":
                case "--silent":
                case "-S":
                case "--show-error":
                case "-i":
                case "--include":
                case "--no-progress-meter":
                case "-q":
                case "--disable":
                case "--basic":
                    if (inline is not null)
                        throw new ArgumentException($"The {option} option does not take a value.");
                    break;
                case "--":
                    while (++i < tokens.Count)
                        SetUrl(tokens[i]);
                    break;
                default:
                    if (option.StartsWith('-'))
                        throw new ArgumentException($"The cURL option {option} is not supported. Remove it or configure the request manually.");
                    SetUrl(option);
                    break;
            }
        }
        if (url is null)
            throw new ArgumentException("The cURL command needs one HTTP or HTTPS URL.");
        if (url.Contains("://", StringComparison.Ordinal) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("{{", StringComparison.Ordinal))
            throw new ArgumentException("Only HTTP and HTTPS URLs can be imported.");
        document.Url = url;
        document.Method = method ?? (head ? "HEAD" : get || !hasData ? "GET" : "POST");
        if (!RequestMethods.All.Contains(document.Method))
            throw new ArgumentException("Choose GET, POST, PUT, PATCH, DELETE, HEAD, or OPTIONS.");
        if (head && hasData && !get)
            throw new ArgumentException("Combine --head with --get to put data in the query string, or remove --head.");
        if (get && hasData)
        {
            var parameters = RequestUrl.ParseParameters(document.Url).Concat(RequestUrl.ParseParameters("https://query.local/?" + data));
            document.Url = RequestUrl.WithParameters(document.Url, parameters);
        }
        else if (hasData)
        {
            document.Body = data.ToString();
            if (!document.Headers.Any(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
                document.Headers.Add(new()
                {
                    Key = "Content-Type",
                    Value = json ? "application/json" : "application/x-www-form-urlencoded"
                });
        }
        if (json && !document.Headers.Any(h => h.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase)))
            document.Headers.Add(new()
            {
                Key = "Accept",
                Value = "application/json"
            });
        if (document.Headers.Any(h => h.Enabled && h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) &&
            h.Value.Split(';')[0].Trim() is var type && (type.Equals("application/json", StringComparison.OrdinalIgnoreCase) || type.EndsWith("+json", StringComparison.OrdinalIgnoreCase))))
            document.BodyMode = RequestBodyModes.Json;
        return document;

        void SetUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Enter a non-empty HTTP or HTTPS URL.");
            if (url is not null)
                throw new ArgumentException("Import one request at a time; this command contains more than one URL.");
            url = value;
        }
    }

    public async Task<string> SerializeAsync(RequestDocument document, IReadOnlyDictionary<string, string> variables)
    {
        using var request = HttpRequestFactory.Create(document, variables);
        var parts = new List<string> { "curl", "--globoff", "--location", "--compressed", "--request", Quote(document.Method),
            "--url", Quote(request.RequestUri!.AbsoluteUri), "--max-time", document.TimeoutSeconds.ToString(CultureInfo.InvariantCulture) };
        foreach (var header in request.Headers.Concat(request.Content?.Headers.AsEnumerable() ?? []))
            foreach (var value in header.Value)
            {
                parts.Add("--header");
                parts.Add(Quote(header.Key + (value.Length == 0 ? ";" : ": " + value)));
            }
        foreach (var name in new[] { "Accept", "User-Agent" })
            if (!request.Headers.Contains(name))
            {
                parts.Add("--header");
                parts.Add(Quote(name + ":"));
            }
        if (request.Content is not null)
        {
            parts.Add("--data-raw");
            parts.Add(Quote(Encoding.UTF8.GetString(await request.Content.ReadAsByteArrayAsync())));
        }
        return string.Join(" ", parts);
    }

    private static string Quote(string text) => text.Contains('\0')
        ? throw new ArgumentException("cURL command arguments cannot contain null characters.")
        : "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    private static string LiteralData(string value) => value.StartsWith('@')
        ? throw new ArgumentException("File and stdin references are not imported. Paste literal data using --data-raw instead.") : value;

    private static string EncodeData(string value)
    {
        var equal = value.IndexOf('=');
        if (equal >= 0)
            return (equal == 0 ? "" : value[..(equal + 1)]) + Uri.EscapeDataString(value[(equal + 1)..]);
        if (value.Contains('@'))
            throw new ArgumentException("File references in --data-urlencode are not supported. Use name=value.");
        return Uri.EscapeDataString(value);
    }

    private static void AddHeader(RequestDocument document, string value)
    {
        var colon = value.IndexOf(':');
        if (colon < 1)
        {
            if (value.EndsWith(';') && value.Length > 1)
                document.Headers.Add(new()
                {
                    Key = value[..^1],
                    Value = ""
                });
            else
                throw new ArgumentException("Use cURL headers in 'Name: value' form.");
            return;
        }
        document.Headers.Add(new()
        {
            Key = value[..colon].Trim(),
            Value = value[(colon + 1)..].TrimStart(),
            Enabled = value[(colon + 1)..].Length > 0
        });
    }

    private static List<string> Tokenize(string command)
    {
        if (command.Length > 5 * 1024 * 1024 || Encoding.UTF8.GetByteCount(command) > 5 * 1024 * 1024)
            throw new ArgumentException("The cURL command is too large. Import up to 5 MB of text.");
        if (command.Contains('\0'))
            throw new ArgumentException("cURL command arguments cannot contain null characters.");
        var tokens = new List<string>();
        var value = new StringBuilder();
        char quote = '\0';
        var started = false;
        for (var i = 0; i < command.Length; i++)
        {
            var c = command[i];
            if (quote != '\'' && c == '\\')
            {
                if (i + 1 == command.Length)
                    throw new ArgumentException("The command ends with an unfinished escape.");
                var next = command[++i];
                if (next == '\r' && i + 1 < command.Length && command[i + 1] == '\n')
                    i++;
                else if (next == '\n')
                {
                }
                else if (quote == '"' && next is not ('"' or '\\' or '$' or '`'))
                    value.Append('\\').Append(next);
                else
                    value.Append(next);
                started = started || value.Length > 0;
            }
            else if (c == quote && quote != '\0')
                quote = '\0';
            else if (quote == '\0' && c is '\'' or '"')
            {
                quote = c;
                started = true;
            }
            else if (quote == '\0' && char.IsWhiteSpace(c))
            {
                if (started)
                    tokens.Add(value.ToString());
                value.Clear();
                started = false;
            }
            else
            {
                if (quote != '\'' && c is '$' or '`' || quote == '\0' && ";|&<>()".Contains(c))
                    throw new ArgumentException("Paste one literal cURL command. Shell expansion, pipes, redirects, and command chaining are not supported.");
                value.Append(c);
                started = true;
            }
        }
        if (quote != '\0')
            throw new ArgumentException("The command has an unclosed quote.");
        if (started)
            tokens.Add(value.ToString());
        return tokens;
    }
}
