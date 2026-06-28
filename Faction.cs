using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Describes a faction in the game world.
    /// </summary>
    public class Faction
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Color Color { get; set; } = Color.White;
        public bool IsLawful { get; set; }
        public bool IsCriminal { get; set; }

        public override string ToString() => DisplayName;
    }
}
