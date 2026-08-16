using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Roguelancer;

public readonly record struct StationBarLayout(
    Vector3 Minimum,
    Vector3 Maximum,
    float CeilingHeight,
    Vector3 DoorPosition)
{
    public float Width => Maximum.X - Minimum.X;
    public float Depth => Maximum.Z - Minimum.Z;
    public bool IsCompactSocialRoom => Width >= 12.0f && Width <= 16.0f && Depth >= 10.0f && Depth <= 14.0f && CeilingHeight >= 3.5f && CeilingHeight <= 4.5f;
}

public sealed record StationSocialNpcDefinition(
    string Id,
    string DisplayName,
    Vector3 Position,
    float YawDegrees,
    float InteractionRadius,
    StationDialogue Dialogue,
    float IdleOffset,
    StationNpcInteractionRole InteractionRole = StationNpcInteractionRole.Dialogue,
    bool HasFutureMissionHook = false);

/// <summary>
/// Bounded Phase 10 social-role data. The shared asset id is intentionally
/// explicit so future role additions cannot accidentally introduce per-NPC
/// model parsing or a story-character dependency.
/// </summary>
public static class StationBarSocial
{
    public const string SharedCharacterAssetId = "prototype-adam";

    public static readonly StationBarLayout Layout = new(
        new Vector3(-14.5f, 0.0f, 48.8f),
        new Vector3(0.0f, 4.25f, 62.0f),
        4.25f,
        new Vector3(-7.25f, 0.0f, 42.0f));

    public static IReadOnlyList<StationSocialNpcDefinition> CreateRoles()
    {
        return new[]
        {
            new StationSocialNpcDefinition(
                "bartender",
                "Bartender",
                new Vector3(-7.25f, 0.0f, 60.0f),
                180.0f,
                2.3f,
                new StationDialogue("Bartender", "Drinks are cheap. Trouble costs extra."),
                2.61f,
                HasFutureMissionHook: true),
            new StationSocialNpcDefinition(
                "rogue-pilot",
                "Rogue Pilot",
                new Vector3(-11.4f, 0.0f, 52.7f),
                35.0f,
                2.3f,
                new StationDialogue("Rogue Pilot", "Heard the trade lanes are getting rough."),
                3.17f),
            new StationSocialNpcDefinition(
                "dockhand",
                "Dockhand",
                new Vector3(-1.55f, 0.0f, 54.2f),
                -90.0f,
                2.3f,
                new StationDialogue("Dockhand", "You'd be surprised what comes through this bay."),
                3.83f),
            new StationSocialNpcDefinition(
                "smuggler",
                "Smuggler",
                new Vector3(-4.15f, 0.0f, 57.0f),
                145.0f,
                2.3f,
                new StationDialogue("Smuggler", "If you're looking for work, ask around."),
                4.41f),
        };
    }
}
