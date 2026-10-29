namespace Osynt.Domain.Requests;

public sealed class RequestDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled request";
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "";
    public string Body { get; set; } = "";
    public string BodyMode { get; set; } = RequestBodyModes.Json;
    public List<RequestPair> FormFields { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 30;
    public List<RequestPair> Parameters { get; set; } = [];
    public List<RequestPair> Headers { get; set; } = [];
    public RequestDocument Clone(bool newId = false) => new()
    {
        Id = newId ? Guid.NewGuid() : Id,
        Name = Name,
        Method = Method,
        Url = Url,
        Body = Body,
        BodyMode = BodyMode,
        FormFields = FormFields.Select(p => p.Clone()).ToList(),
        TimeoutSeconds = TimeoutSeconds,
        Parameters = Parameters.Select(p => p.Clone()).ToList(),
        Headers = Headers.Select(p => p.Clone()).ToList()
    };
}
