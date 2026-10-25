using System.Net.Sockets;

namespace Osynt.Infrastructure.Http;

internal static class RequestFailureMessages
{
    public static HttpRequestException Explain(HttpRequestException error, Uri address)
    {
        var cause = error.GetBaseException();
        var message = cause is SocketException { SocketErrorCode: SocketError.ConnectionRefused }
            ? $"Nothing is listening at {address.Host}:{address.Port}.\nCheck that your API is running and use the port from its “Now listening on” URL."
            : error.HttpRequestError == HttpRequestError.SecureConnectionError
                ? "The HTTPS certificate could not be verified.\nA development certificate trusted in WSL may not be trusted in Windows. Use the API's HTTP address or trust its certificate in Windows."
                : error.HttpRequestError == HttpRequestError.NameResolutionError
                    ? $"The host “{address.Host}” could not be found. Check the hostname and your connection."
                    : error.Message;
        return new HttpRequestException($"{message}\n\n{address}", error, error.StatusCode);
    }
}
