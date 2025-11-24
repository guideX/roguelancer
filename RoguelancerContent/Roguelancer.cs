// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Rougelancer.Functionality;
using Rougelancer.Objects;
namespace Rougelancer {
    public class Rougelancer : Microsoft.Xna.Framework.Game {
        clsCamera lCamera;
        clsShip lShip;
        //clsShip[] lNPCShip = new clsShip[128];
        //clsShip lNPCShip;
        clsGround lGround;
        clsGraphics lGraphics;
        clsInput lInput;
        clsInputItems lInputItems;
        clsDebugText lDebugText;
        clsCamera lCameraSnapshot;
        //clsStarField lStarField;
        public Rougelancer() {
            lInput = new clsInput();
            lCamera = new clsCamera();
            lShip = new clsShip(true);
            //lNPCShip = new clsShip(false);
            lGround = new clsGround();
            lGraphics = new clsGraphics();
            lInputItems = new clsInputItems();
            //lStarField = new clsStarField();
            lGraphics.Init(this, 1290, 1024);
            lDebugText = new clsDebugText();
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }
        protected override void Initialize() {
            base.Initialize();
            lCamera.Init(lGraphics, lShip);
            lInput.Init();
            //lStarField.Init(lGraphics, this);
        }
        protected override void LoadContent() {
            lGraphics.LoadContent();
            lShip.LoadContent("SHIPS\\PI_TRANSPORT\\PI_TRANSPORT", Content);
            //lNPCShip.LoadContent("SHIPS\\PI_TRANSPORT\\PI_TRANSPORT", Content);
            lGround.LoadContent("Ground", Content);
            lDebugText.LoadContent("LucidaFont", Content, lGraphics);
            //lStarField.LoadContent(Content, lGraphics);
            lDebugText.Update("Uninitialized");
        }
        protected override void Update(GameTime gameTime) {
            lInputItems = lInput.Update(this, lCamera, lCameraSnapshot, lDebugText);
            lShip.Update(gameTime, lGraphics, lInputItems, lDebugText );
            //lNPCShip.Update(gameTime, lGraphics, lInputItems);
            lCamera.UpdateCameraChaseTarget(lGraphics, lShip);
            lCamera.Update(gameTime);
            lDebugText.Update(lShip.lText);
            //lStarField.Update(this, lCamera);
            base.Update(gameTime);
        }
        protected override void Draw(GameTime _GameTime) {
            lGraphics.BeginSpriteBatch();
            lGraphics.Draw();
            lDebugText.Draw(lGraphics);
            lShip.Draw(lCamera);
            //lNPCShip.Draw(lCamera);
            lGround.Draw(Matrix.Identity, lCamera);
            //lStarField.Draw(lGraphics);
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