using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Defines a purchasable ship with its stats and properties
    /// </summary>
    public class ShipDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ModelPath { get; set; }
        public int Price { get; set; }
        public int TradeInValue { get; set; } // 50% of purchase price

        /// <summary>
        /// Explicit physical mount metadata. An empty list selects the
        /// generic fallback layout in ShipLoadout.
        /// </summary>
        public IReadOnlyList<ShipHardpointDefinition> HardpointDefinitions { get; set; } = new List<ShipHardpointDefinition>();

        public bool HasExplicitHardpointMetadata => HardpointDefinitions != null && HardpointDefinitions.Count > 0;

        public string HardpointLayoutSource => HasExplicitHardpointMetadata ? "ShipDefinition" : "GenericFallback";
        
        // Ship Stats
        public float MaxSpeed { get; set; } = 250f;
        public float MaxReverseSpeed { get; set; } = 150f;
        public float CruiseSpeed { get; set; } = 600f;
        public float AfterburnerSpeed { get; set; } = 500f;
        public float Acceleration { get; set; } = 150f;
        public float TurnSpeed { get; set; } = 1.5f;
        public float MaxHull { get; set; } = 100f;
        public float MaxEnergy { get; set; } = 200f;
        public float MaxShields { get; set; } = 50f;
        public int CargoCapacity { get; set; } = 50;
        
        // Model correction rotation
        public Matrix ModelCorrectionRotation { get; set; } = Matrix.Identity;
        
        // UI Display
        public Color DisplayColor { get; set; } = Color.White;
        
        // Runtime model reference (loaded from content)
        public Model Model { get; set; }

        public ShipDefinition(string name, string description, string modelPath, int price)
        {
            Name = name;
            Description = description;
            ModelPath = modelPath;
            Price = price;
            TradeInValue = (int)(price * 0.5f); // 50% trade-in value keeps trade-ins fair without enabling abuse
        }

        /// <summary>
        /// Create a light fighter ship definition (Scimitar)
        /// </summary>
        public static ShipDefinition CreateScimitar()
        {
            return new ShipDefinition(
                "Scimitar",
                "Light Fighter - Fast and agile",
                "SHIPS/scimitar/Scimitar2",
                12000
            )
            {
                MaxSpeed = 250f,
                MaxReverseSpeed = 150f,
                CruiseSpeed = 600f,
                AfterburnerSpeed = 500f,
                Acceleration = 150f,
                TurnSpeed = 1.5f,
                MaxHull = 100f,
                MaxEnergy = 200f,
                MaxShields = 50f,
                CargoCapacity = 50,
                DisplayColor = Color.Cyan,
                HardpointDefinitions = CreateScimitarHardpoints()
            };
        }

        /// <summary>
        /// Create a heavy transport ship definition
        /// </summary>
        public static ShipDefinition CreateTransport()
        {
            return new ShipDefinition(
                "Pirate Transport",
                "Heavy Transport - High cargo, slow",
                "SHIPS/PI_TRANSPORT/PI_TRANSPORT",
                24000
            )
            {
                MaxSpeed = 180f,
                MaxReverseSpeed = 100f,
                CruiseSpeed = 500f,
                AfterburnerSpeed = 350f,
                Acceleration = 100f,
                TurnSpeed = 0.8f,
                MaxHull = 250f,
                MaxEnergy = 300f,
                MaxShields = 100f,
                CargoCapacity = 200,
                DisplayColor = Color.Yellow,
                ModelCorrectionRotation = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi), // Fix orientation: -90 deg pitch + 180 deg yaw
                HardpointDefinitions = CreateTransportHardpoints()
            };
        }

        private static IReadOnlyList<ShipHardpointDefinition> CreateScimitarHardpoints()
        {
            // Coordinates are in the displayed ship-local metres used by the
            // shared attachment transform. They sit on the wing roots and
            // forward centerline of the imported Scimitar hull.
            return new List<ShipHardpointDefinition>
            {
                new("PrimaryGunLeft", EquipmentType.Gun, new Vector3(-4.20f, 0.80f, 0.45f)),
                new("PrimaryGunRight", EquipmentType.Gun, new Vector3(4.20f, 0.80f, 0.45f)),
                new("MissileRack", EquipmentType.MissileLauncher, new Vector3(0.00f, 0.65f, -1.05f)),
                new("MineRack", EquipmentType.MineDropper, new Vector3(0.00f, 0.20f, 1.45f)),
                new("CountermeasureRack", EquipmentType.CountermeasureDropper, new Vector3(0.00f, 1.55f, 2.25f)),
                new("ShieldGenerator", EquipmentType.ShieldGenerator, Vector3.Zero),
                new("Thruster", EquipmentType.Thruster, new Vector3(0.00f, 0.00f, 4.65f)),
                new("Scanner", EquipmentType.Scanner, new Vector3(0.00f, 0.55f, -0.75f)),
                new("TractorBeam", EquipmentType.TractorBeam, new Vector3(0.00f, -0.15f, -2.10f))
            };
        }

        private static IReadOnlyList<ShipHardpointDefinition> CreateTransportHardpoints()
        {
            // The transport has one centerline gun and one centerline launcher
            // rather than the Scimitar's paired fighter guns. Utility mounts
            // remain available so the Phase 8 gameplay systems stay intact.
            return new List<ShipHardpointDefinition>
            {
                new("TransportGun", EquipmentType.Gun, new Vector3(0.00f, 1.65f, -3.20f)),
                new("TransportLauncher", EquipmentType.MissileLauncher, new Vector3(0.00f, 1.10f, -2.45f)),
                new("TransportMineRack", EquipmentType.MineDropper, new Vector3(0.00f, 0.55f, 3.10f)),
                new("CountermeasureRack", EquipmentType.CountermeasureDropper, new Vector3(0.00f, 2.20f, 4.35f)),
                new("ShieldGenerator", EquipmentType.ShieldGenerator, Vector3.Zero),
                new("Thruster", EquipmentType.Thruster, new Vector3(0.00f, 0.00f, 8.50f)),
                new("Scanner", EquipmentType.Scanner, new Vector3(0.00f, 1.00f, -2.00f)),
                new("TractorBeam", EquipmentType.TractorBeam, new Vector3(0.00f, 0.20f, -4.10f))
            };
        }

        /// <summary>
        /// Apply this ship's stats to a Ship instance
        /// </summary>
        public void ApplyToShip(Ship ship)
        {
            ship.DisplayName = Name;
            ship.ModelPath = ModelPath;
            ship.MaxSpeed = MaxSpeed;
            ship.MaxReverseSpeed = MaxReverseSpeed;
            ship.CruiseSpeed = CruiseSpeed;
            ship.AfterburnerSpeed = AfterburnerSpeed;
            ship.Acceleration = Acceleration;
            ship.TurnSpeed = TurnSpeed;
            
            // Reset hull to max for new ship using the new SetHull method
            ship.SetHull(MaxHull);
            
            // Reset energy to max for new ship
            ship.InitializeEnergy(MaxEnergy);
            
            // Reset shields for new ship
            ship.InitializeShields(MaxShields);
            
            // Update cargo hold capacity
            ship.CargoHold.SetMaxCapacity(CargoCapacity);
            
            // Apply model
            ship.Model = Model;
            ship.RefreshCollisionRadiusFromModel();
            
            // Apply model correction rotation from ship definition
            ship.ModelRotationCorrection = ModelCorrectionRotation;

            // Reconfigure the existing authoritative loadout in-place at the
            // ship boundary. Ownership is preserved and mounts are remapped
            // deterministically by ShipLoadout.
            ship.ApplyHardpointLayout(HardpointDefinitions);
        }

        /// <summary>
        /// Get a formatted stat comparison string
        /// </summary>
        public string GetStatsString()
        {
            return $"Speed: {MaxSpeed:F0} | Hull: {MaxHull:F0} | Shields: {MaxShields:F0} | Cargo: {CargoCapacity}";
        }
    }
}

