using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    /// <summary>
    /// Debug Text
    /// </summary>
    public class DebugTextObject : IDebugText {
        #region "public properties"
        /// <summary>
        /// Debug Text Model
        /// </summary>
        public DebugTextModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Debug Text
        /// </summary>
        public DebugTextObject() {
            Model = new DebugTextModel {
                Text = ""
            };
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.Font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.Model.Font);
            Model.FontPosition = new Vector2(game.GraphicsDevice.Viewport.Width / 2, game.Graphics.Model.GraphicsDeviceManager.GraphicsDevice.Viewport.Height / 2);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (Model.ShowEnabled) {
                if (Model.TimerEnabled) {
                    Model.CurrentShowTime++;
                    if (Model.CurrentShowTime > Model.ShowTime) {
                        Model.ShowEnabled = false;
                        Model.CurrentShowTime = 0;
                        Model.Text = "";
                    }
                }
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (Model.ShowEnabled) {
                var fontOrigin = Model.Font.MeasureString(Model.Text) / 2;
                game.Graphics.Model.SpriteBatch.DrawString(Model.Font, Model.Text, Model.FontPosition, Color.White, 0, fontOrigin, 3.0f, SpriteEffects.None, 0.5f);
            }
        }
        #endregion
    }
}