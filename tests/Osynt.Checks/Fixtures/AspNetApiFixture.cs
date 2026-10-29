using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Osynt.Checks.Fixtures;

internal sealed class AspNetApiFixture : IDisposable
{
    private readonly WebApplication _application;
    public string BaseUrl
    {
        get;
    }
    public AspNetApiFixture()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        _application = builder.Build();
        _application.MapGet("/health", () => Results.Ok());
        _application.MapGet("/query", (string name, int count) => Results.Json(new { name, count }));
        _application.MapGet("/headers", (HttpContext context) => Results.Json(new
        {
            accept = context.Request.Headers.Accept.ToString(),
            userAgent = context.Request.Headers.UserAgent.ToString(),
            hasBody = context.Request.ContentLength is > 0
        }));
        _application.MapPost("/echo", (JsonElement payload) => Results.Json(payload));
        _application.MapPost("/text", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            return Results.Json(new
            {
                contentType = context.Request.ContentType,
                body = await reader.ReadToEndAsync()
            });
        });
        _application.MapPost("/form", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            return Results.Json(form.ToDictionary(p => p.Key, p => p.Value.ToArray()));
        });
        _application.MapGet("/redirect", () => Results.Redirect("/health"));
        _application.MapGet("/auth", (HttpContext context) => Results.Text(context.Request.Headers.Authorization.ToString()));
        _application.StartAsync().GetAwaiter().GetResult();
        BaseUrl = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }
    public void Dispose() => _application.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
