using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    /// <summary>
    /// Ship
    /// </summary>
    public class ShipObject : IGame, ISensorObject, IDockableShip {
        #region "public properties"
        /// <summary>
        /// Ship Model
        /// </summary>
        public ShipModel ShipModel { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Ship
        /// </summary>
        /// <param name="game"></param>
        public ShipObject(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
            if (ShipModel.PlayerShipControl.Model.UseInput) {
                ShipModel.PlayerShipControl = new PlayerShipControl(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                if (ShipModel.PlayerShipControl.Model.UseInput) {
                    ShipModel.PlayerShipControl.UpdateModel(Model, game);
                    //if (!game.Input.InputItems.Toggles.ToggleCamera) {
                    Model.Update(game);
                    //}
                } else {
                    Model.Update(game);
                }
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (!ShipModel.Docked) {
                Model.Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            ShipModel.Docked = false;
            Model.Dispose(game);
            Model = null;
            ShipModel = null;
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            ShipModel = new ShipModel(game);
            Model = new GameModel(game, null, null);
        }
        #endregion
    }
}