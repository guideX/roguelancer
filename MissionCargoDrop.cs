using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Authoritative cargo released by one convoy transport. The source index
    /// is stable for the life of a mission and prevents duplicate release.
    /// </summary>
    public sealed class MissionCargoDrop
    {
        public int MissionId { get; set; }
        public int SourceIndex { get; set; } = -1;
        public string CommodityId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string SourceNpcName { get; set; } = string.Empty;
        public Vector3 SourcePosition { get; set; }
    }
}
