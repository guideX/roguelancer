using Roguelancer.Models;
using Roguelancer.Objects;
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
        /// <param name="dockTo"></param>
        void Dock(RoguelancerGame game, ShipObject ship, GameModel dockTo);
        /// <summary>
        /// Un Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        /// <param name="undockFrom"></param>
        void UnDock(RoguelancerGame game, ShipObject ship, GameModel undockFrom);
        /// <summary>
        /// List Commodities For Sale
        /// </summary>
        /// <param name="game"></param>
        /// <param name="modelType"></param>
        /// <param name="stationID"></param>
        void ListCommoditiesForSale(RoguelancerGame game, DockableObjectTypeModel dockableObjectType);
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
        void Initialize(RoguelancerGame game, GameModel Model, DockableObjectTypeModel dockableObjectType);
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        void LoadContent(RoguelancerGame game, GameModel Model, DockableObjectTypeModel dockableObjectType);
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        void Update(RoguelancerGame game, GameModel Model, DockableObjectTypeModel dockableObjectType);
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        void Draw(RoguelancerGame game, GameModel Model, DockableObjectTypeModel dockableObjectType);
    }
}