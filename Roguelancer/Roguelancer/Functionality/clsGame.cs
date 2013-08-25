using Roguelancer.Bloom;
using Roguelancer.Objects;
using Roguelancer.Particle;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    public class clsGame : Microsoft.Xna.Framework.Game {
        public clsGraphics graphics;
        public Settings settings;
        public camera camera;
        public clsInput input;
        public GameTime gameTime;
        public ShipCollection ships;
        public StationCollection stations;
        private clsStarfields stars;
        private camera cameraSnapshot;
        private clsBloomHandler bloom;
        private PlanetCollection planets;
        public clsDebugText debugText;
        //private clsParticleSystemHandler lParticleSystem;
        //private clsSound lEngineNoise;
        public clsGame() {
            settings = new Settings();
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            camera = new camera();
            graphics = new clsGraphics(this);
            stations = new StationCollection();
            graphics.Initialize(this);
            bloom = new clsBloomHandler(this);
            input = new clsInput();
            stars = new clsStarfields();
            ships = new ShipCollection(this);
            planets = new PlanetCollection();
            debugText = new clsDebugText();
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
            stations.Initialize(this);
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
            stations.LoadContent(this);
            debugText.LoadContent(this);
            debugText.Update(this);
            //lEngineNoise.soundPath = "engine";
            //lParticleSystem.LoadContent(this);
            base.LoadContent();
        }
        protected override void Update(GameTime _GameTime) {
            gameTime = _GameTime;
            stars.Update(this);
            input.Update(this);
            ships.Update(this);
            planets.Update(this);
            stations.Update(this);
            bloom.Update(true);
            if (input.lInputItems.lToggles.lCameraSnapshot == true) {
                input.lInputItems.lToggles.lCameraSnapshot = false;
                cameraSnapshot = camera;
            } else if (input.lInputItems.lToggles.lRevertCamera == true) {
                input.lInputItems.lToggles.lRevertCamera = false;
                camera = cameraSnapshot;
            }
            camera.UpdateCameraChaseTarget(this);
            camera.Update(this);
            debugText.lText = "Hi.";
            debugText.Update(this);
            //lParticleSystem.Update(_GameTime, lDebugText, lGraphics);
            //lEngineNoise.Update(this);
            base.Update(_GameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            gameTime = _GameTime;
            bloom.Draw();
            graphics.BeginSpriteBatch();
            graphics.Draw(this);
            debugText.Draw(this);
            stars.Draw(this);
            ships.Draw(this);
            planets.Draw(this);
            stations.Draw(this);
            graphics.EndSpriteBatch();
            //lParticleSystem.Draw(this);
            //lEngineNoise.Draw(this);
            base.Draw(_GameTime);
        }
    }
}