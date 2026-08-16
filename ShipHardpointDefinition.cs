using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Ship-definition metadata for one physical equipment attachment point.
    /// Local positions are expressed in the rendered ship-local space after
    /// the shared ship model scale/orientation correction has been applied.
    /// This keeps attachment points model-relative without applying the ship
    /// correction twice in either renderer.
    /// </summary>
    public sealed class ShipHardpointDefinition
    {
        public string Id { get; }
        public IReadOnlyList<EquipmentType> AllowedEquipmentTypes { get; }
        public Vector3 LocalPosition { get; }
        public Vector3 LocalRotationDegrees { get; }
        public float VisualScale { get; }

        public ShipHardpointDefinition(
            string id,
            EquipmentType equipmentType,
            Vector3 localPosition,
            Vector3 localRotationDegrees = default,
            float visualScale = 1f)
            : this(id, new[] { equipmentType }, localPosition, localRotationDegrees, visualScale)
        {
        }

        public ShipHardpointDefinition(
            string id,
            IEnumerable<EquipmentType> allowedEquipmentTypes,
            Vector3 localPosition,
            Vector3 localRotationDegrees = default,
            float visualScale = 1f)
        {
            Id = id ?? string.Empty;
            AllowedEquipmentTypes = new List<EquipmentType>(allowedEquipmentTypes ?? Array.Empty<EquipmentType>());
            LocalPosition = localPosition;
            LocalRotationDegrees = localRotationDegrees;
            VisualScale = SanitizeScale(visualScale);
        }

        public ShipHardpoint ToRuntimeHardpoint()
        {
            return new ShipHardpoint
            {
                Id = Id,
                AllowedEquipmentTypes = new List<EquipmentType>(AllowedEquipmentTypes),
                LocalPosition = LocalPosition,
                LocalRotationDegrees = LocalRotationDegrees,
                VisualScale = VisualScale
            };
        }

        private static float SanitizeScale(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : value;
        }
    }
}
