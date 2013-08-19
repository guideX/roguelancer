// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class clsModel : IGame {
        public enum DrawMode {
            unknown = 0,
            mainModel = 1,
            planet = 2
        }
        public Vector2 rotationAmount { get; set; }
        public Vector3 up;
        public Vector3 right;
        public Vector3 position;
        public Vector3 direction;
        public DrawMode drawMode { get; set; }
        public Matrix world;
        public string modelPath { get; set; }
        public Vector3 modelScaling { get; set; }
        public Vector3 modelRotation { get; set; }
        public Vector3 startPosition { get; set; }
        public float minimumAltitude = 350.0f;
        private Model model;
        public clsModel() {
            drawMode = DrawMode.unknown;
            position = new Vector3(0, minimumAltitude, 0);
            up = Vector3.Up;
            right = Vector3.Right;
        }
        public void Initialize(clsGame _Game) {
            
        }
        public void LoadContent(clsGame _Game) {
            model = _Game.Content.Load<Model>(modelPath);
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
            world = Matrix.Identity;
            world.Forward = direction;
            world.Up = up;
            world.Right = right;
            world.Translation = position;           
        }
        public void Draw(clsGame _Game) {
            if(drawMode == DrawMode.unknown) {
                return;
            }
            Matrix[] _Transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(_Transforms);
            foreach(ModelMesh modelMesh in model.Meshes) {
                foreach(BasicEffect basicEffect in modelMesh.Effects) {
                    switch(drawMode) {
                        case DrawMode.mainModel:
                            basicEffect.Alpha = 1;
                            basicEffect.EnableDefaultLighting();
                            basicEffect.World = _Transforms[modelMesh.ParentBone.Index] * world;
                            basicEffect.View = _Game.camera.lView;
                            basicEffect.Projection = _Game.camera.lProjection;
                            break;
                        case DrawMode.planet:
                            basicEffect.Alpha = 1;
                            basicEffect.EnableDefaultLighting();
                            basicEffect.World = _Transforms[modelMesh.ParentBone.Index] * world;
                            basicEffect.View = _Game.camera.lView;
                            basicEffect.Projection = _Game.camera.lProjection;
                            break;
                    } 
                }
                modelMesh.Draw();
            }
        }
    }
}