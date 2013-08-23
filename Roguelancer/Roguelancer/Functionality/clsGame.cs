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
        private clsStarfields stars;
        private clsCamera cameraSnapshot;
        private clsBloomHandler bloom;
        private PlanetCollection planets;
        //public clsDebugText debugText;
        //private clsParticleSystemHandler lParticleSystem;
        //private clsSound lEngineNoise;
        public clsGame() {
            // INITIALIZE SETTINGS
            settings = new Settings();
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            // INSTANCIATE GRAPHICS SYSTEM
            camera = new clsCamera();
            graphics = new clsGraphics(this);
            //graphics.Initialize(this);
            bloom = new clsBloomHandler(this);
            input = new clsInput();
            stars = new clsStarfields();
            // INSTANCIATE OBJECT CLASSES
            ships = new ShipCollection();
            planets = new PlanetCollection();
            //debugText = new clsDebugText();
            //lEngineNoise = new clsSound();
            //lParticleSystem = new clsParticleSystemHandler(this);
        }
        protected override void Initialize() {
            graphics.Initialize(this);
            camera.Initialize(this);
            
            bloom.Initialize();
            input.Initialize(this);
            stars.Initialize(this);
            ships.Initialize(this);
            planets.Initialize(this);
            //lParticleSystem.Initialize();
            //lEngineNoise.Initialize(this);
            base.Initialize();
        }
        protected override void LoadContent() {
            camera.LoadContent(this);
            graphics.LoadContent(this);
            stars.LoadContent(this);
            bloom.LoadContent();
            ships.LoadContent(this);
            planets.LoadContent(this);
            //ship.model.settings = settings.playerShip;
            //ship.model.modelPath = settings.shipTexture;
            //ship.LoadContent(this);
            //debugText.LoadContent(this);
            //debugText.Update(this);
            //lEngineNoise.soundPath = "engine";
            //lParticleSystem.LoadContent(this);
            base.LoadContent();
        }
        protected override void Update(GameTime _GameTime) {
            gameTime = _GameTime;
            stars.Update(this);
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
            stars.Draw(this);
            ships.Draw(this);
            graphics.EndSpriteBatch();
            planets.Draw(this);
            //lParticleSystem.Draw(this);
            //lEngineNoise.Draw(this);
            base.Draw(_GameTime);
        }
    }
}