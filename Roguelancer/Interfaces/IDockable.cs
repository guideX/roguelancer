﻿using Roguelancer.Models;
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// IDockable
    /// </summary>
    public interface IDockable : IGame {
        /// <summary>
        /// Station Model
        /// </summary>
        StationModel StationModel { get; set; }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        /// <param name="dockTo"></param>
        void Dock(RoguelancerGame game, ShipObject ship, GameModel dockTo);
        /// <summary>
        /// Un-Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        /// <param name="undockFrom"></param>
        void UnDock(RoguelancerGame game, ShipObject ship, GameModel undockFrom);
        /// <summary>
        /// Commodities for Sale
        /// </summary>
        /// <param name="game"></param>
        /// <param name="id"></param>
        /// <param name="modelType"></param>
        /// <returns></returns>
        //List<StationPriceModel> CommoditiesForSale(RoguelancerGame game, int id, ModelType modelType);
    }
}