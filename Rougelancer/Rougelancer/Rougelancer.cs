// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Rougelancer.Functionality;
using Rougelancer.Objects;
using Rougelancer.Particle;
using Rougelancer.Bloom;
using Rougelancer.Particle.System.ParticleSystems;
namespace Rougelancer {
    public class Rougelancer : Microsoft.Xna.Framework.Game {
        private clsParticleSystemHandler lParticleSystem;
        private clsStarfields lStars;
        private clsCamera lCamera;
        private clsShip lShip;
        private clsGraphics lGraphics;
        private clsInput lInput;
        private clsInputItems lInputItems;
        private clsDebugText lDebugText;
        private clsCamera lCameraSnapshot;
        private clsBloomHandler lBloom;
        public Rougelancer() {
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            lBloom = new clsBloomHandler(this);
            lStars = new clsStarfields();
            lInput = new clsInput();
            lCamera = new clsCamera();
            lShip = new clsShip(true);
            lGraphics = new clsGraphics();
            lInputItems = new clsInputItems();
            lGraphics.Init(this, 800, 600);
            lDebugText = new clsDebugText();
            lParticleSystem = new clsParticleSystemHandler(this);
        }
        protected override void Initialize() {
            base.Initialize();
            lCamera.Init(lGraphics, lShip);
            lInput.Init();
            lParticleSystem.Init();
        }
        protected override void LoadContent() {
            lGraphics.LoadContent();
            lStars.LoadContent(Content, lGraphics, lCamera);
            lShip.LoadContent("SHIPS\\PI_TRANSPORT\\PI_TRANSPORT", Content, lCamera);
            lDebugText.LoadContent("LucidaFont", lGraphics.lGDM.GraphicsDevice.Viewport, Content);
            lDebugText.Update(" ", lGraphics.lSpriteBatch);
            lBloom.LoadContent();
            lParticleSystem.LoadContent(Content);
        }
        protected override void Update(GameTime _GameTime) {
            lStars.Update(lCamera, lGraphics);
            lInputItems = lInput.Update(this, lDebugText, lGraphics.lSpriteBatch);
            lShip.Update(_GameTime, lGraphics, lInputItems, lDebugText, lCamera);
            lBloom.Update(true);
            if (lInput.lInputItems.lToggles.lCameraSnapshot == true) {
                lInput.lInputItems.lToggles.lCameraSnapshot = false;
                lCameraSnapshot = lCamera;
            } else if (lInput.lInputItems.lToggles.lRevertCamera == true) {
                lInput.lInputItems.lToggles.lRevertCamera = false;
                lCamera = lCameraSnapshot;
            }
            lCamera.UpdateCameraChaseTarget(lGraphics, lShip);
            lCamera.Update(_GameTime);
            lParticleSystem.Update(_GameTime, lDebugText, lGraphics);
            base.Update(_GameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            lBloom.Draw();
            lParticleSystem.Draw(lGraphics, lCamera, lShip);
            lGraphics.BeginSpriteBatch();
            lGraphics.Draw();
            lDebugText.Draw();
            lStars.Draw(lCamera);
            lShip.Draw(lCamera);
            lGraphics.EndSpriteBatch();
            base.Draw(_GameTime);
        }
    }
#if WINDOWS
    static class Program {
        static void Main(string[] args) {
            using (Rougelancer game = new Rougelancer()) {
                game.Run();
            }
        }
    }
#endif
}