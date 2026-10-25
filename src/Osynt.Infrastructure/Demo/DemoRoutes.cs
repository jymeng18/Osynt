using System.Text.Json;

namespace Osynt.Infrastructure.Demo;

internal static class DemoRoutes
{
    public static (int, object) Respond(string method, Uri uri, string body)
    {
        if (uri.AbsolutePath == "/v1/plant")
        {
            if (method != "POST")
                return (405, new
                {
                    error = "Use POST to plant a crop."
                });
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return (400, new
                    {
                        error = "Send a JSON object with crop and quantity."
                    });
                return (201, new
                {
                    message = "A little care goes a long way.",
                    planted = document.RootElement.Clone(),
                    watered = true
                });
            }
            catch (JsonException) { return (400, new { error = "Your seeds need valid JSON. Try {\"crop\":\"strawberry\",\"quantity\":12}." }); }
        }
        if (method is not ("GET" or "HEAD"))
            return (405, new
            {
                error = "This little path accepts GET and HEAD."
            });
        if (uri.AbsolutePath == "/v1/harvest")
        {
            var season = uri.Query.TrimStart('?').Split('&').Select(q => q.Split('=', 2))
                .FirstOrDefault(q => q.Length == 2 && q[0] == "season")?.ElementAt(1) ?? "spring";
            season = Uri.UnescapeDataString(season.Replace('+', ' '));
            var crops = season.ToLowerInvariant() switch
            {
                "summer" => new[] { new Crop("blueberry", 50, 13), new Crop("melon", 250, 12), new Crop("tomato", 60, 11) },
                "fall" or "autumn" => [new Crop("pumpkin", 320, 13), new Crop("cranberry", 75, 7), new Crop("yam", 160, 10)],
                "winter" => [new Crop("winter root", 70, 7), new Crop("crystal fruit", 150, 7)],
                _ => [new Crop("strawberry", 120, 8), new Crop("cauliflower", 175, 12), new Crop("parsnip", 35, 4)]
            };
            return (200, new
            {
                season,
                farm = "Sunbeam Acres",
                crops,
                total = crops.Length,
                message = "Good things take a little growing."
            });
        }
        if (uri.AbsolutePath == "/v1/villagers")
            return (200, new
            {
                villagers = new[] { new { name = "Maple", role = "beekeeper", favorite = "wildflower honey" }, new { name = "Clover", role = "gardener", favorite = "strawberries" } },
                message = "There is always room for a new friend."
            });
        if (uri.AbsolutePath == "/v1/health")
            return (200, new
            {
                status = "growing nicely",
                sunshine = true,
                version = "1.0.0"
            });
        return (404, new
        {
            error = "This path has not been planted yet.",
            paths = new[] { "/v1/harvest", "/v1/villagers", "/v1/plant", "/v1/health" }
        });
    }

    private sealed record Crop(string name, int sell_price, int growth_days);
}
