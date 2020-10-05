namespace Roguelancer.Enum {
    /// <summary>
    /// Game Camera Mode Enum
    /// </summary>
    public enum GameCameraModeEnum {
        /// <summary>
        /// Mode 0
        /// </summary>
        DogfightingMode = 0, // Use mouse to change direction model lags behind
        /// <summary>
        /// Mode 1
        /// </summary>
        ExperimentalMode = 1, // Fucked up twichy (Do Not Use!)
        /// <summary>
        /// Mode 2
        /// </summary>
        StandardMode = 2 // Use mouse to change direction smoothly
    }
}