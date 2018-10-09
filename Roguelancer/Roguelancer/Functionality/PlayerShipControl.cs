using Roguelancer.Interfaces;
using Roguelancer.Models;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Player Ship Control
    /// </summary>
    public class PlayerShipControl : IPlayerShipControl {
        #region "public properties"
        /// <summary>
        /// Player Ship Model
        /// </summary>
        public PlayerShipControlModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Player Ship Control
        /// </summary>
        public PlayerShipControl(RoguelancerGame game) {
            Model = new PlayerShipControlModel(game) {
                UseInput = true
            };
        }
        /// <summary>
        /// Update Model
        /// </summary>
        /// <param name="model"></param>
        /// <param name="game"></param>
        public void UpdateModel(GameModel model, RoguelancerGame game) {
            var rotationAmount = new Vector2(); // Create Vector for Rotation Amount
            if (Model.UseInput) rotationAmount = this.GetInputRotationAmount(game, model); // This model is using input
            model.Rotation = rotationAmount;
            model.UpdatePosition();
            this.CheckGoto(game);
            this.UpdateThrust(game, model);
            if (!game.Input.InputItems.Toggles.ToggleCamera) {
                this.UpdatePositionAndVelocity(game, model);
                //model.Update(game);
            }
        }
        #endregion
    }
}