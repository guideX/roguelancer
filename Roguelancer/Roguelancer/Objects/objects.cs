using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
using Roguelancer.Particle;
using Microsoft.Xna.Framework;
namespace Roguelancer.Objects {
    public class GameObjects {
        public ShipCollection ships;
        private TradeLaneCollection tradeLaneCollection;
        private StarSystem starSystem;
        //private Starfields starFields;
        public GameObjects(RoguelancerGame _Game) {
            starSystem = new StarSystem(_Game);
            ships = new ShipCollection(_Game);
            tradeLaneCollection = new TradeLaneCollection(_Game);
            //starFields = new Starfields();
        }
        public void Initialize(RoguelancerGame _Game) {
            starSystem.Initialize(_Game);
            ships.Initialize(_Game);
            tradeLaneCollection.Initialize(_Game);
            //starFields.Initialize(_Game);
        }
        public void LoadContent(RoguelancerGame _Game) {
            starSystem.LoadContent(_Game);
            ships.LoadContent(_Game);
            tradeLaneCollection.LoadContent(_Game);
            //starFields.LoadContent(_Game);
        }
        public void Update(RoguelancerGame _Game) {
            starSystem.Update(_Game);
            ships.Update(_Game);
            tradeLaneCollection.Update(_Game);
            //starFields.Update(_Game);
        }
        public void Draw(RoguelancerGame _Game) {
            starSystem.Draw(_Game);
            ships.Draw(_Game);
            tradeLaneCollection.Draw(_Game);
            //starFields.Draw(_Game);
        }
        public void Reset(RoguelancerGame _Game) {
            ships.Reset(_Game);
            starSystem.Reset(_Game);
        }
        /*public void Object_Click(RoguelancerGame game) {
            float mouseX = game.input.lInputItems.mouse.lVector.X;
            float mouseY = game.input.lInputItems.mouse.lVector.Y;
            Vector3 nearsource = new Vector3(mouseX, mouseY, 0f);
            Vector3 farsource = new Vector3((float)mouseX, (float)mouseY, 1f);
            Matrix world = Matrix.CreateTranslation(0, 0, 0);
            Vector3 nearPoint = game.GraphicsDevice.Viewport.Unproject(nearsource, game.camera.projection, game.camera.view, world);
            Vector3 farPoint = game.GraphicsDevice.Viewport.Unproject(farsource, game.camera.projection, game.camera.view, world);
            Vector3 direction = farPoint - nearPoint;
            direction.Normalize();
            Ray pickRay = new Ray(nearPoint, direction);
        }*/
    }
}