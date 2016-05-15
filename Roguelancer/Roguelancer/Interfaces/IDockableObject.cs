using Roguelancer.Enum;
using Roguelancer.Models;
using Roguelancer.Objects;
using Roguelancer.Settings;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Dockable Object
    /// </summary>
    public interface IDockableObject {
        /// <summary>
        /// Dockable Object Model
        /// </summary>
        DockableObjectModel DockableObjectModel { get; set; }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        /// <param name="worldObject"></param>
        void Dock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject);
        /// <summary>
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        /// <param name="worldObject"></param>
        void UnDock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject);
        /// <summary>
        /// List Commodities For Sale
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modelType"></param>
        /// <param name="stationID"></param>
        void ListCommoditiesForSale(RoguelancerGame game, ModelType modelType, int stationID);
        /// <summary>
        /// Purchase Commodity
        /// </summary>
        /// <param name="game"></param>
        /// <param name="commodityID"></param>
        /// <param name="qty"></param>
        void PurchaseCommodity(RoguelancerGame game, int commodityID, int qty);
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        void Initialize(RoguelancerGame game, GameModel Model, int? stationID);
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        void LoadContent(RoguelancerGame game, GameModel Model, int? stationID);
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        void Update(RoguelancerGame game, GameModel Model, int? stationID);
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        void Draw(RoguelancerGame game, GameModel Model, int? stationID);
    }
}