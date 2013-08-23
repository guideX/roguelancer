using Roguelancer.Bloom;
using Roguelancer.Objects;
using Roguelancer.Particle;
//using Roguelancer.Particle.System.ParticleSystems;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    public class clsGame : Microsoft.Xna.Framework.Game {
        public clsGraphics graphics;
        public ShipCollection ships;
        public Settings settings;
        public clsCamera camera;
        public clsInput input;
        public GameTime gameTime;
        //public clsDebugText debugText;
        private clsStarfields stars;
        private clsCamera cameraSnapshot;
        private clsBloomHandler bloom;
        private PlanetCollection planets;
        //private clsParticleSystemHandler lParticleSystem;
        //private clsSound lEngineNoise;
        public clsGame() {
            settings = new Settings();
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            bloom = new clsBloomHandler(this);
            stars = new clsStarfields();
            input = new clsInput();
            camera = new clsCamera();
            //ship = new clsShip();
            ships = new ShipCollection();
            graphics = new clsGraphics();
            graphics.Initialize(this);
            //debugText = new clsDebugText();
            planets = new PlanetCollection();
            //lEngineNoise = new clsSound();
            //lParticleSystem = new clsParticleSystemHandler(this);
        }
        protected override void Initialize() {
            camera.Initialize(this);
            input.Initialize(this);
            ships.Initialize(this);
            //ship.Initialize(this);
            //ship.playerShipControl.useInput = true;
            planets.Initialize(this);
            //lParticleSystem.Initialize();
            //lEngineNoise.Initialize(this);
            base.Initialize();
        }
        protected override void LoadContent() {
            graphics.LoadContent(this);
            stars.LoadContent(this);
            ships.LoadContent(this);
            //ship.model.settings = settings.playerShip;
            //ship.model.modelPath = settings.shipTexture;
            //ship.LoadContent(this);
            //debugText.LoadContent(this);
            //debugText.Update(this);
            bloom.LoadContent();
            planets.LoadContent(this);
            //lEngineNoise.soundPath = "engine";
            //lParticleSystem.LoadContent(this);
            base.LoadContent();
        }
        protected override void Update(GameTime _GameTime) {
            gameTime = _GameTime;
            stars.Update(camera, graphics);
            input.Update(this);
            //ship.gameTime = _GameTime;
            //ship.model.gameTime = _GameTime;
            //ship.Update(this);
            ships.Update(this);
            planets.Update(this);
            bloom.Update(true);
            if (input.lInputItems.lToggles.lCameraSnapshot == true) {
                input.lInputItems.lToggles.lCameraSnapshot = false;
                cameraSnapshot = camera;
            } else if (input.lInputItems.lToggles.lRevertCamera == true) {
                input.lInputItems.lToggles.lRevertCamera = false;
                camera = cameraSnapshot;
            }
            camera.UpdateCameraChaseTarget(graphics, ships.GetPlayerShip());
            camera.gameTime = _GameTime;
            camera.Update(this);
            //debugText.lText = planets.debugText + " + " + ship.debugText;
            //debugText.Update(this);
            //lParticleSystem.Update(_GameTime, lDebugText, lGraphics);
            //lEngineNoise.Update(this);
            base.Update(_GameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            gameTime = _GameTime;
            bloom.Draw();
            graphics.BeginSpriteBatch();
            graphics.Draw(this);
            //debugText.Draw(this);
            stars.Draw(camera);
            ships.Draw(this);
            graphics.EndSpriteBatch();
            planets.Draw(this);
            //lParticleSystem.Draw(this);
            //lEngineNoise.Draw(this);
            base.Draw(_GameTime);
        }
    }
}