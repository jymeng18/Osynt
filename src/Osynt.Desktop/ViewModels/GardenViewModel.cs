using System.Text.Json;
using Osynt.Application.Garden;
using Osynt.Domain.Garden;

namespace Osynt.Desktop.ViewModels;

public sealed class GardenViewModel : ObservableModel
{
    private readonly GardenService _garden;
    private readonly WorkspaceStatus _status;
    private GardenState _state;
    private bool _isOpen;
    private string _lastGrowth = "Every successful request adds a little life.";
    public GardenViewModel(GardenService garden, WorkspaceStatus status)
    {
        _garden = garden;
        _status = status;
        _state = garden.GetState();
    }

    public bool UpdatePreferences(string atmosphere, bool reducedMotion)
    {
        if (!_status.TrySave(() => _garden.UpdatePreferences(atmosphere, reducedMotion)))
            return false;
        Refresh();
        return true;
    }
    public bool IsOpen
    {
        get => _isOpen; set
        {
            Set(ref _isOpen, value);
            Raise(nameof(ShowRequests));
        }
    }
    public bool ShowRequests => !IsOpen;
    public long Successes => _state.SuccessfulRequests;
    public int Level => GardenProgress.Level(Successes);
    public long Plants => GardenProgress.Plants(Successes);
    public string Stage => GardenProgress.Stage(Successes);
    public string Summary => $"Level {Level} · {Successes:N0} growth";
    public double LevelProgress => GardenProgress.LevelProgress(Successes);
    public string LastGrowth => _lastGrowth;
    public string Atmosphere => _state.Atmosphere;
    public bool ReducedMotion => _state.ReducedMotion;
    public string RenderMessage => JsonSerializer.Serialize(new
    {
        type = "state",
        successes = Successes,
        atmosphere = Atmosphere,
        reducedMotion = ReducedMotion
    });

    public void Refresh()
    {
        var state = _garden.GetState();
        var grew = state.SuccessfulRequests > Successes;
        _state = state;
        if (grew)
            _lastGrowth = GardenProgress.GrowthMessage(Successes);
        foreach (var property in new[] { nameof(Successes), nameof(Level), nameof(Plants), nameof(Stage), nameof(Summary),
            nameof(LevelProgress), nameof(LastGrowth), nameof(Atmosphere), nameof(ReducedMotion), nameof(RenderMessage) })
            Raise(property);
    }
}
