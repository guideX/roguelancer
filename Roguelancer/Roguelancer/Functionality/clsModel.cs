// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class clsModel : intGame {
        public enum DrawMode {
            unknown = 0,
            mainModel = 1,
            planet = 2
        }
        public DrawMode drawMode { get; set; }
        public Matrix world;
        public string modelPath { get; set; }
        public Vector3 modelScaling { get; set; }
        public Vector3 modelRotation { get; set; }
        public Vector3 startPosition { get; set; }
        private Vector3 modelPosition;
        private Model model;
        public clsModel() {
            drawMode = DrawMode.unknown;
        }
        public void Initialize(clsGame _Game) {
            model = _Game.Content.Load<Model>(modelPath);
        }
        public void LoadContent(clsGame _Game) {}
        public void Update(clsGame _Game) {}
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
                            basicEffect.View = _Game.lCamera.lView;
                            basicEffect.Projection = _Game.lCamera.lProjection;
                            break;
                        case DrawMode.planet:
                            basicEffect.Alpha = 1;
                            basicEffect.EnableDefaultLighting();
                            basicEffect.World = _Transforms[modelMesh.ParentBone.Index] * world;
                            basicEffect.View = _Game.lCamera.lView;
                            basicEffect.Projection = _Game.lCamera.lProjection;
                            break;
                    } 
                }
                modelMesh.Draw();
            }
        }
    }
}