using Roguelancer.Functionality;
namespace Roguelancer.Models {
    /// <summary>
    /// Player Ship Control Model
    /// </summary>
    public class PlayerShipControlModel {
        #region "values that change constantly"
        /// <summary>
        /// Current Target
        /// </summary>
        //public DockableObjectModel CurrentTarget { get; set; }
        public GameModel CurrentTarget { get; set; }
        /// <summary>
        /// Current Target Station Object
        /// </summary>
        //public StationObject CurrentTargetStationObject { get; set; }
        /// <summary>
        /// Last Target
        /// </summary>
        public string LastTarget { get; set; }
        /// <summary>
        /// Use Input
        /// </summary>
        public bool UseInput { get; set; }
        /// <summary>
        /// Use Auto Dock
        /// </summary>
        public bool UseAutoDock { get; set; }
        /// <summary>
        /// Auto Dock Step
        /// </summary>
        public int AutoDockStep { get; set; }
        /// <summary>
        /// Auto Dock Time
        /// </summary>
        //public DateTime? AutoDockTime { get; set; }
        #endregion
        #region "values loaded by ini file"
        /// <summary>
        /// Shake Value
        /// </summary>
        public float ShakeValue { get; set; }
        /// <summary>
        /// Update Direction X
        /// </summary>
        public float UpdateDirectionX { get; set; }
        /// <summary>
        /// Update Direction Y
        /// </summary>
        public float UpdateDirectionY { get; set; }
        /// <summary>
        /// Rotation X Left Add
        /// </summary>
        public float RotationXLeftAdd { get; set; }
        /// <summary>
        /// Rotation X Right Add
        /// </summary>
        public float RotationXRightAdd { get; set; }
        /// <summary>
        /// Rotation Y Up Add
        /// </summary>
        public float RotationYUpAdd { get; set; }
        /// <summary>
        /// Rotation Y Down Add
        /// </summary>
        public float RotationYDownAdd { get; set; }
        /// <summary>
        /// Rotation Rate
        /// </summary>
        public float RotationRate { get; set; }
        /// <summary>
        /// Mass
        /// </summary>
        public float Mass { get; set; }
        /// <summary>
        /// Thrust Force
        /// </summary>
        public float ThrustForce { get; set; }
        #endregion
        #region "const"
        /// <summary>
        /// Drag Factor
        /// </summary>
        public const float DragFactor = 0.97f;
        /// <summary>
        /// Max Thrust Amount
        /// </summary>
        public const float MaxThrustAmount = 0.3f;
        /// <summary>
        /// Max Thrust Afterburner Amount
        /// </summary>
        public const float MaxThrustAfterburnerAmount = 0.5f;
        /// <summary>
        /// thrust Add Speed
        /// </summary>
        public const float ThrustAddSpeed = 0.006f;
        /// <summary>
        /// Thrust After Burner Add Amount
        /// </summary>
        public const float ThrustAfterBurnerAddAmount = 0.1f;
        /// <summary>
        /// Thrust Slow Down Speed
        /// </summary>
        public const float ThrustSlowDownSpeed = 0.005f;
        /// <summary>
        /// Thrust Reverse Speed
        /// </summary>
        public const float ThrustReverseSpeed = -0.009f;
        /// <summary>
        /// Max thrust Reverse
        /// </summary>
        public const float MaxThrustReverse = -0.10f;
        /// <summary>
        /// Max Cruise Speed
        /// </summary>
        public const float MaxCruiseSpeed = 1.3f;
        /// <summary>
        /// Limit Altitude
        /// </summary>
        public const bool LimitAltitude = true;
        /// <summary>
        /// Thrust Min Not Zero
        /// </summary>
        public const float ThrustMinNotZero = .00001f;
        /// <summary>
        /// Additional Mouse Drag Factor X
        /// </summary>
        public const float AdditionalMouseDragFactorX = 0;
        /// <summary>
        /// Additional Mouse Drag Factor Y
        /// </summary>
        public const float AdditionalMouseDragFactorY = 0;
        /// <summary>
        /// Use Additional Mouse Drag Factor
        /// </summary>
        public const bool UseAdditionalMouseDragFactor = true;
        #endregion
        /// <summary>
        /// Player Ship Control Model
        /// </summary>
        public PlayerShipControlModel(RoguelancerGame game, int shipID = 1) {
            var rootDir = System.IO.Directory.GetCurrentDirectory() + @"\..\..\..\";
            var ini = rootDir + @"configuration\player\settings.ini";
            RotationRate = NativeMethods.ReadINIFloat(ini, "PlayerShip", "RotationRate", 1.5f);
            UpdateDirectionX = NativeMethods.ReadINIFloat(ini, "PlayerShip", "UpdateDirectionX", 2.0f);
            UpdateDirectionY = NativeMethods.ReadINIFloat(ini, "PlayerShip", "UpdateDirectionY", 2.0f);
            RotationXLeftAdd = NativeMethods.ReadINIFloat(ini, "PlayerShip", "RotationXLeftAdd", 1.0f);
            RotationXRightAdd = NativeMethods.ReadINIFloat(ini, "PlayerShip", "RotationXRightAdd", -1.0f);
            RotationYUpAdd = NativeMethods.ReadINIFloat(ini, "PlayerShip", "RotationYUpAdd", -1.0f);
            RotationYDownAdd = NativeMethods.ReadINIFloat(ini, "PlayerShip", "RotationYDownAdd", 1.0f);
            ShakeValue = NativeMethods.ReadINIFloat(ini, "PlayerShip", "ShakeValue", 0.8f);
            Mass = NativeMethods.ReadINIFloat(ini, "PlayerShip", "Mass", 1.0f);
            ThrustForce = NativeMethods.ReadINIFloat(ini, "PlayerShip", "ThrustForce", 24000.0f);
            //MaxCruiseSpeed = NativeMethods.ReadINIFloat(ini, "settings", "max_cruise_speed", 1.3f);
            //LimitAltitude = NativeMethods.ReadINIBool(ini, "settings", "limit_altitude", true);
            //MaxThrustReverse = NativeMethods.ReadINIFloat(ini, "settings", "max_thrust_reverse", -0.10f);
            //ThrustReverseSpeed = NativeMethods.ReadINIFloat(ini, "settings", "thrust_reverse_speed", -0.009f);
            //UpdateDirectionY = game.Settings.Model.PlayerShipUpdateDirectionY;
            //RotationXLeftAdd = game.Settings.Model.PlayerShipRotationXLeftAdd;
            //RotationXRightAdd = game.Settings.Model.PlayerShipRotationXRightAdd;
            //RotationYUpAdd = game.Settings.Model.PlayerShipRotationYUpAdd;
            //RotationYDownAdd = game.Settings.Model.PlayerShipRotationYDownAdd;
            //CurrentTarget = "Farpoint Station";
        }
    }
}