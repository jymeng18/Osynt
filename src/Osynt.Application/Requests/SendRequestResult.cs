using Osynt.Domain.Requests;

namespace Osynt.Application.Requests;

public sealed record SendRequestResult(
    RequestDocument Request, ResponseData? Response, string? Error, string? PersistenceError);
