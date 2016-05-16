﻿// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Models {
    /// <summary>
    /// Station Price Model
    /// </summary>
    public class StationPriceModel {
        /// <summary>
        /// Station Price Id
        /// </summary>
        public int StationPriceID { get; set; }
        /// <summary>
        /// Star System Id
        /// </summary>
        public int StarSystemId { get; set; }
        /// <summary>
        /// Station
        /// </summary>
        public int StationId { get; set; }
        /// <summary>
        /// Commodities Id
        /// </summary>
        public int CommoditiesId { get; set; }
        /// <summary>
        /// Price
        /// </summary>
        public decimal Price { get; set; }
        /// <summary>
        /// Selling
        /// </summary>
        public decimal Selling { get; set; }
        /// <summary>
        /// Is Selling
        /// </summary>
        public bool IsSelling { get; set; }
        /// <summary>
        /// Qty
        /// </summary>
        public int Qty { get; set; }
        /// <summary>
        /// Station
        /// </summary>
        //public Station Station { get; set; }
        /// <summary>
        /// Commodity
        /// </summary>
        //public CommodityModel Commodity { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        public Texture2D Image { get; set; }
        /// <summary>
        /// Badge Model
        /// </summary>
        public Texture2D ImageContainer { get; set; }
    }
}