using System;

namespace Roguelancer;

public enum StationDoorState
{
    Closed,
    Opening,
    Open,
    Closing,
}

/// <summary>
/// Reusable bounded door state used by station interiors. The Bar keeps its
/// door open for the station session; Closing remains available for a later
/// auto-close policy without changing collision or animation semantics.
/// </summary>
public sealed class StationDoorController
{
    private readonly float _animationSeconds;
    private readonly float _traversalProgress;

    public StationDoorController(float animationSeconds = 0.75f, float traversalProgress = 0.65f)
    {
        _animationSeconds = MathF.Max(0.01f, animationSeconds);
        _traversalProgress = Math.Clamp(traversalProgress, 0.0f, 1.0f);
    }

    public StationDoorState State { get; private set; } = StationDoorState.Closed;
    public float Progress { get; private set; }
    public bool IsOpen => State == StationDoorState.Open;
    public bool BlocksTraversal => Progress < _traversalProgress;
    public string ActionLabel => State switch
    {
        StationDoorState.Closed => "Press E to enter",
        StationDoorState.Opening => "Opening...",
        StationDoorState.Closing => "Closing...",
        _ => "Open",
    };

    public bool TryOpen()
    {
        if (State != StationDoorState.Closed) return false;
        State = StationDoorState.Opening;
        return true;
    }

    public bool TryClose()
    {
        if (State != StationDoorState.Open) return false;
        State = StationDoorState.Closing;
        return true;
    }

    public void Reset()
    {
        State = StationDoorState.Closed;
        Progress = 0.0f;
    }

    public void Update(float deltaSeconds)
    {
        float amount = MathF.Max(0.0f, deltaSeconds) / _animationSeconds;
        if (State == StationDoorState.Opening)
        {
            Progress = Math.Clamp(Progress + amount, 0.0f, 1.0f);
            if (Progress >= 1.0f) State = StationDoorState.Open;
        }
        else if (State == StationDoorState.Closing)
        {
            Progress = Math.Clamp(Progress - amount, 0.0f, 1.0f);
            if (Progress <= 0.0f) State = StationDoorState.Closed;
        }
    }
}
