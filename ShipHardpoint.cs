using System.Collections.Generic;
using System.Linq;

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
                MountedEquipmentId = MountedEquipmentId
            };
        }
    }
}
