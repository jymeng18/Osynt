namespace Osynt.Application.Garden;

public sealed record GardenMilestone(long RequiredSuccesses, string Name, string Description);

public static class GardenProgress
{
    public static IReadOnlyList<GardenMilestone> Milestones
    {
        get;
    } = Array.AsReadOnly(new[]
    {
        new GardenMilestone(1, "First roots", "Your first seed takes root."),
        new GardenMilestone(5, "Wildflower meadow", "Wildflowers find their way into your garden."),
        new GardenMilestone(12, "A little orchard", "Fruit trees and butterflies settle in."),
        new GardenMilestone(24, "Still waters", "A pond, lily pads, and a little stone bridge."),
        new GardenMilestone(40, "Home, grown", "A cozy cottage with a light in the window."),
        new GardenMilestone(75, "Glass & greenery", "A greenhouse for your growing collection."),
        new GardenMilestone(120, "Lantern season", "Warm lanterns light up your island."),
        new GardenMilestone(200, "Beyond the meadow", "New little islands join your garden.")
    });

    public static int Level(long successes) => 1 + (int)Math.Floor(Math.Sqrt(Math.Max(0, successes) / 5d));
    public static long NextLevelAt(long successes) => 5L * Level(successes) * Level(successes);
    public static long Plants(long successes) => (Math.Max(0, successes) + 2) / 3;
    public static GardenMilestone? NextMilestone(long successes) => Milestones.FirstOrDefault(m => m.RequiredSuccesses > successes);
    public static string Stage(long successes) => Milestones.LastOrDefault(m => m.RequiredSuccesses <= successes)?.Name ?? "A fresh beginning";
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
            1 => "A new seed takes root.",
            2 => "Your newest plant is reaching for the sun.",
            _ => "Another little bloom, grown from your work."
        };
    }
}
