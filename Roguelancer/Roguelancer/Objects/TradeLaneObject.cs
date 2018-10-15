using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects.Base;
using System.Collections.Generic;
using System;
namespace Roguelancer.Objects {
    /// <summary>
    /// Trade Lane
    /// </summary>
    public class TradeLaneObject : DockableObject, IGame, IDockable, ITradeLaneSensorObject {
        #region "public properties"
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public List<TradeLaneModel> Models { get; set; }
        public DockableObjectTypeModel DockableObjectType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public TradeLaneObject(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.UpdatePosition();
                Models[i].Model.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Models = new List<TradeLaneModel>();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        public void Dispose(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.Dispose(game);
            }
        }
        #endregion
    }
}