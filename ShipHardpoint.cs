using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// A mount point on a ship for a specific equipment category.
    /// </summary>
    public class ShipHardpoint
    {
        public string Id { get; set; } = string.Empty;
        public List<EquipmentType> AllowedEquipmentTypes { get; set; } = new List<EquipmentType>();
        public string MountedEquipmentId { get; set; } = string.Empty;
        public Vector3 LocalPosition { get; set; } = Vector3.Zero;
        public Vector3 LocalRotationDegrees { get; set; } = Vector3.Zero;
        public float VisualScale { get; set; } = 1f;

        public bool IsEmpty => string.IsNullOrWhiteSpace(MountedEquipmentId);

        public bool CanAccept(EquipmentDefinition equipment)
        {
            return equipment != null && AllowedEquipmentTypes != null && AllowedEquipmentTypes.Contains(equipment.EquipmentType);
        }

        public ShipHardpoint Clone()
        {
            return new ShipHardpoint
            {
                Id = Id,
                AllowedEquipmentTypes = AllowedEquipmentTypes != null
                    ? new List<EquipmentType>(AllowedEquipmentTypes)
                    : new List<EquipmentType>(),
                MountedEquipmentId = MountedEquipmentId,
                LocalPosition = LocalPosition,
                LocalRotationDegrees = LocalRotationDegrees,
                VisualScale = VisualScale
            };
        }
    }
}
