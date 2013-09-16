using Microsoft.Xna.Framework;
using Roguelancer.Bloom;
using Roguelancer.Objects;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Settings;
namespace Roguelancer.Functionality {
    public class RoguelancerGame : Microsoft.Xna.Framework.Game {
        public GameGraphics graphics;
        public GameSettings settings;
        public GameCamera camera;
        public Input input;
        public GameTime gameTime;
        public DebugText debugText;
        public GameState gameState;
        public GameObjects objects;
        public GameCamera cameraSnapshot;
        private clsBloomHandler bloom;
        private GameMenu gameMenu;
        //private clsParticleSystemHandler particleSystem;
        //private clsSound lEngineNoise;
        public RoguelancerGame() {
            settings = new GameSettings();
            Content.RootDirectory = "Content";
            gameState = new GameState();
            IsMouseVisible = true;
            camera = new GameCamera();
            graphics = new GameGraphics(this);
            graphics.Initialize(this);
            bloom = new clsBloomHandler(this);
            input = new Input();
            debugText = new DebugText();
            objects = new GameObjects(this);
            gameMenu = new GameMenu();
            //particleSystem = new clsParticleSystemHandler(this);
            //lEngineNoise = new clsSound();
        }
        protected override void Initialize() {
            graphics.Initialize(this);
            camera.Initialize(this);
            bloom.Initialize();
            input.Initialize(this);
            objects.Initialize(this);
            gameMenu.Initialize(this);
            //particleSystem.Initialize();
            //lEngineNoise.Initialize(this);
            base.Initialize();
        }
        protected override void LoadContent() {
            camera.LoadContent(this);
            graphics.LoadContent(this);
            bloom.LoadContent();
            debugText.LoadContent(this);
            debugText.Update(this);
            objects.LoadContent(this);
            debugText.text = settings.menuText;
            gameMenu.LoadContent(this);
            //particleSystem.LoadContent(this);
            //lEngineNoise.soundPath = "engine";
            base.LoadContent();
        }
        protected override void Update(GameTime _GameTime) {
            gameTime = _GameTime;
            input.Update(this);
            bloom.Update(true);
            objects.Update(this);
            camera.UpdateCameraChaseTarget(this);
            camera.Update(this);
            debugText.Update(this);
            gameMenu.Update(this);
            //particleSystem.Update(_GameTime, debugText, graphics);
            base.Update(_GameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            gameTime = _GameTime;
            graphics.BeginSpriteBatch();
            bloom.Draw();
            if(gameState.currentGameState == GameState.GameStates.playing) {
                if(gameState.lastGameState != gameState.currentGameState) {
                    graphics.graphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                }
                graphics.Draw(this);
                //particleSystem.Draw(this);
                objects.Draw(this);
            } else if(gameState.currentGameState == GameState.GameStates.menu) {
                if(gameState.lastGameState != gameState.currentGameState) {
                    graphics.graphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
                }
                gameMenu.Draw(this);
                graphics.Draw(this);
            }
            debugText.Draw(this);
            graphics.EndSpriteBatch();
            base.Draw(_GameTime);
        }
    }
}