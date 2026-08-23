#nullable enable

using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// The live relationship an NPC faction currently presents toward the player.
    /// This is derived state and is never persisted with an individual NPC.
    /// </summary>
    public enum FactionDisposition
    {
        Friendly,
        Neutral,
        Hostile
    }

    /// <summary>
    /// Single authority for translating permanent reputation and temporary
    /// faction hostility into the small gameplay relationship model used by
    /// NPCs and contact presentation.
    /// </summary>
    public static class FactionDispositionEvaluator
    {
        public static FactionDisposition Evaluate(string? factionId, ReputationManager? reputationManager)
        {
            if (reputationManager == null)
                return FactionDisposition.Neutral;

            if (reputationManager.IsFactionCurrentlyHostile(factionId))
                return FactionDisposition.Hostile;

            return reputationManager.IsFriendly(factionId)
                ? FactionDisposition.Friendly
                : FactionDisposition.Neutral;
        }

        public static bool IsHostile(string? factionId, ReputationManager? reputationManager) =>
            Evaluate(factionId, reputationManager) == FactionDisposition.Hostile;

        public static string Format(FactionDisposition disposition) => disposition.ToString().ToUpperInvariant();

        public static Color GetColor(FactionDisposition disposition, Color fallback)
        {
            return disposition switch
            {
                FactionDisposition.Hostile => Color.IndianRed,
                FactionDisposition.Friendly => Color.LightGreen,
                _ => fallback
            };
        }
    }
}
