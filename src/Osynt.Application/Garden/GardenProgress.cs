namespace Osynt.Application.Garden;

public sealed record GardenMilestone(long RequiredSuccesses, string Name, string Description);

public static class GardenProgress
{
    public static IReadOnlyList<GardenMilestone> Milestones
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        new GardenMilestone(1, "First plant", "First seed planted."),
        new GardenMilestone(5, "Wildflowers", "Wildflowers unlocked."),
        new GardenMilestone(12, "Orchard", "Fruit trees and butterflies unlocked."),
        new GardenMilestone(24, "Pond", "Pond and bridge unlocked."),
        new GardenMilestone(40, "Cottage", "Garden cottage unlocked."),
        new GardenMilestone(75, "Greenhouse", "Greenhouse unlocked."),
        new GardenMilestone(120, "Lanterns", "Lanterns unlocked."),
        new GardenMilestone(200, "More islands", "New islands unlocked.")
    });

    public static int Level(long successes) => 1 + (int)Math.Floor(Math.Sqrt(Math.Max(0, successes) / 5d));
    public static long NextLevelAt(long successes) => 5L * Level(successes) * Level(successes);
    public static long Plants(long successes) => (Math.Max(0, successes) + 2) / 3;
    public static GardenMilestone? NextMilestone(long successes) => Milestones.FirstOrDefault(m => m.RequiredSuccesses > successes);
    public static string Stage(long successes) => Milestones.LastOrDefault(m => m.RequiredSuccesses <= successes)?.Name ?? "No plants yet";
    public static double LevelProgress(long successes)
    {
        var level = Level(successes);
        var start = 5L * (level - 1) * (level - 1);
        return Math.Clamp((successes - start) * 100d / (NextLevelAt(successes) - start), 0, 100);
    }
    public static string GrowthMessage(long successes)
    {
        var milestone = Milestones.FirstOrDefault(m => m.RequiredSuccesses == successes);
        return milestone?.Description ?? (successes % 3) switch
        {
            1 => "Seed planted.",
            2 => "Plant growing.",
            _ => "Plant fully grown."
        };
    }
}
