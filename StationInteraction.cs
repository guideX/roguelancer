using System;
using Microsoft.Xna.Framework;

namespace Roguelancer;

/// <summary>
/// Minimal station interaction primitive. It intentionally contains only the
/// data needed for proximity prompts and one edge-triggered action.
/// </summary>
public sealed class StationInteraction
{
    public StationInteraction(string id, Vector3 position, float radius, string displayLabel, string actionLabel, Action action)
    {
        Id = id;
        Position = position;
        Radius = radius;
        DisplayLabel = displayLabel;
        ActionLabel = actionLabel;
        Action = action;
    }

    public string Id { get; }
    public Vector3 Position { get; }
    public float Radius { get; }
    public string DisplayLabel { get; }
    public string ActionLabel { get; }
    public Action Action { get; }

    public bool IsInRange(Vector3 playerPosition)
    {
        return Vector3.DistanceSquared(playerPosition, Position) <= Radius * Radius;
    }
}
