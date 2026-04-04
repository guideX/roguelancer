using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
        public int TradeInValue { get; set; } // 60% of purchase price
        
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
            TradeInValue = (int)(price * 0.6f); // 60% trade-in value
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
                15000
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
                DisplayColor = Color.Cyan
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
                45000
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
                ModelCorrectionRotation = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi) // Fix orientation: -90° pitch + 180° yaw
            };
        }

        /// <summary>
        /// Apply this ship's stats to a Ship instance
        /// </summary>
        public void ApplyToShip(Ship ship)
        {
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
            
            // Apply model correction rotation from ship definition
            ship.ModelRotationCorrection = ModelCorrectionRotation;
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
