using System;

namespace Roguelancer;

/// <summary>
/// Shared stable-identity boundary for bounded NPC interactions. Presentation
/// names remain free to change; cargo, scanning, and demand use this value.
/// </summary>
public static class NpcIdentity
{
    public static string GetStableIdentity(NpcShip npc)
    {
        if (npc == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(npc.StableIdentity)
            ? npc.Name ?? string.Empty
            : npc.StableIdentity;
    }
}
