namespace Roguelancer.Models {
    /// <summary>
    /// Star Settings Model
    /// </summary>
    public class StarSettingsModel  {
        /// <summary>
        /// Star Settings Model
        /// </summary>
        /// <param name="starsEnabled"></param>
        /// <param name="amountOfStarsPerSheet"></param>
        /// <param name="maxPositionX"></param>
        /// <param name="maxPositionY"></param>
        /// <param name="maxSize"></param>
        /// <param name="maxPositionIncrementY"></param>
        /// <param name="maxPositionStartingY"></param>
        /// <param name="numberOfStarSheets"></param>
        public StarSettingsModel(
                bool starsEnabled,
                int amountOfStarsPerSheet,
                long maxPositionX,
                long maxPositionY,
                int maxSize,
                long maxPositionIncrementY,
                long maxPositionStartingY,
                long numberOfStarSheets) {
            StarsEnabled = starsEnabled;
            AmountOfStarsPerSheet = amountOfStarsPerSheet;
            MaxPositionX = maxPositionX;
            MaxPositionY = maxPositionY;
            MaxPositionIncrementY = maxPositionIncrementY;
            MaxPositionStartingY = maxPositionStartingY;
            MaxSize = maxSize;
            NumberOfStarSheets = numberOfStarSheets;

        }
        /// <summary>
        /// Stars Enabled
        /// </summary>
        public bool StarsEnabled { get; set; }
        /// <summary>
        /// Amount of Stars Per Sheet
        /// </summary>
        public int AmountOfStarsPerSheet { get; set; }
        /// <summary>
        /// Max Position X
        /// </summary>
        public long MaxPositionX { get; set; }
        /// <summary>
        /// Max Position Y
        /// </summary>
        public long MaxPositionY { get; set; }
        /// <summary>
        /// Max Size
        /// </summary>
        public int MaxSize { get; set; }
        /// <summary>
        /// Max Position Increment Y
        /// </summary>
        public long MaxPositionIncrementY { get; set; }
        /// <summary>
        /// Max Position Starting Y
        /// </summary>
        public long MaxPositionStartingY { get; set; }
        /// <summary>
        /// Number of Star Sheets
        /// </summary>
        public long NumberOfStarSheets { get; set; }
    }
}