namespace Osynt.Domain.Garden;

/// <summary>Lifetime progress, independent of the bounded request history and the renderer.</summary>
public sealed class GardenState
{
    public long SuccessfulRequests
    {
        get; set;
    }
    public string Atmosphere { get; set; } = "day";
    public bool ReducedMotion
    {
        get; set;
    }
    public DateTimeOffset? LastGrownAt
    {
        get; set;
    }

    public GardenState Clone() => new()
    {
        SuccessfulRequests = SuccessfulRequests,
        Atmosphere = Atmosphere,
        ReducedMotion = ReducedMotion,
        LastGrownAt = LastGrownAt
    };

    public void Grow(DateTimeOffset now)
    {
        SuccessfulRequests = checked(SuccessfulRequests + 1);
        LastGrownAt = now;
    }

    public void UpdatePreferences(string atmosphere, bool reducedMotion)
    {
        if (atmosphere is not ("day" or "dusk"))
            throw new ArgumentException("Choose day or dusk.");
        Atmosphere = atmosphere;
        ReducedMotion = reducedMotion;
    }
}
