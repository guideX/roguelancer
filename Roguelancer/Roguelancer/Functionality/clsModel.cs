// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class clsModel : IGame {
        public enum ModelMode {
            unknown = 0,
            ship = 1,
            planet = 2,
            station = 3
        }
        public ModelMode modelMode { get; set; }
        public float currentThrust { get; set; }
        public Vector3 velocity { get; set; }
        public Vector2 rotationAmount { get; set; }
        public Vector3 up;
        public Vector3 right;
        public Vector3 position;
        public Vector3 direction;
        public Matrix world;
        public float minimumAltitude = 350.0f;
        private Model model;
        public SettingsModelObject settings { get; set; }
        public clsModel() {
            velocity = Vector3.Zero;
            position = new Vector3(0, minimumAltitude, 0);
            up = Vector3.Up;
            right = Vector3.Right;
            currentThrust = 0.0f;
            direction = Vector3.Forward;
        }
        public void Initialize(clsGame _Game) {
        }
        public void LoadContent(clsGame _Game) {
            model = _Game.Content.Load<Model>(settings.modelPath);
            if(settings.startupPosition != null) {
                position = settings.startupPosition;
            } else {
                position = new Vector3(0, 0, 0);
            }
        }
        public void UpdatePosition() {
            Matrix rotationMatrix = Matrix.CreateFromAxisAngle(right, rotationAmount.Y) * Matrix.CreateRotationY(rotationAmount.X);
            direction = Vector3.TransformNormal(direction, rotationMatrix);
            up = Vector3.TransformNormal(up, rotationMatrix);
            direction.Normalize();
            up.Normalize();
            right = Vector3.Cross(direction, up);
            up = Vector3.Cross(right, direction);
        }
        public void Update(clsGame _Game) {
            if(_Game.input.lInputItems.lToggles.lToggleCamera == false) {
                world = Matrix.Identity;
                world.Forward = direction;
                world.Up = up;
                world.Right = right;
                world.Translation = position;
            }
        }
        public void Draw(clsGame _Game) {
            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);
            foreach(ModelMesh modelMesh in model.Meshes) {
                foreach(BasicEffect basicEffect in modelMesh.Effects) {
                    switch(modelMode) {
                        case (ModelMode.ship):
                            basicEffect.Alpha = 1;
                            basicEffect.EnableDefaultLighting();
                            basicEffect.World = transforms[modelMesh.ParentBone.Index] * world;
                            basicEffect.View = _Game.camera.view;
                            basicEffect.Projection = _Game.camera.projection;
                            break;
                        default:
                            basicEffect.Alpha = 1;
                            basicEffect.EnableDefaultLighting();
                            basicEffect.World = transforms[modelMesh.ParentBone.Index] * world;
                            basicEffect.View = _Game.camera.view;
                            basicEffect.Projection = _Game.camera.projection;
                            break;
                    }
                }
                modelMesh.Draw();
            }
        }
    }
}