using System;
using System.Collections.Generic;
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

public static class StationInteractionResolver
{
    /// <summary>
    /// Keeps the station's nearest/facing/edge-triggered target rule reusable by
    /// the runtime and headless interaction smoke tests. The caller remains
    /// responsible for invoking only the returned action once per key edge.
    /// </summary>
    public static StationInteraction? FindNearest(
        IReadOnlyList<StationInteraction> interactions,
        Vector3 playerPosition,
        Vector3 forward)
    {
        StationInteraction? nearest = null;
        float nearestScore = float.MaxValue;
        foreach (StationInteraction interaction in interactions)
        {
            Vector3 offset = interaction.Position - playerPosition;
            float distanceSquared = offset.LengthSquared();
            if (distanceSquared > interaction.Radius * interaction.Radius) continue;

            float score = distanceSquared;
            offset.Y = 0.0f;
            if (offset.LengthSquared() > 0.0001f)
            {
                float facing = Vector3.Dot(Vector3.Normalize(offset), forward);
                if (facing < -0.25f) score += 0.35f;
            }

            if (score < nearestScore)
            {
                nearest = interaction;
                nearestScore = score;
            }
        }

        return nearest;
    }
}
