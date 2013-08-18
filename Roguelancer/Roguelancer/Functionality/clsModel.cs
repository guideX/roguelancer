// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class clsModel {
        public Matrix lWorld;
        private Model lModel;
        public void Init(string _Path, ContentManager _Content) {
            lModel = _Content.Load<Model>(_Path);
        }
        public void Draw(clsCamera _Camera) {
            Matrix[] _Transforms = new Matrix[lModel.Bones.Count];
            lModel.CopyAbsoluteBoneTransformsTo(_Transforms);
            foreach (ModelMesh _Mesh in lModel.Meshes) {
                foreach (BasicEffect _Effect in _Mesh.Effects) {
                    _Effect.Alpha = 1;
                    _Effect.EnableDefaultLighting();
                    _Effect.World = _Transforms[_Mesh.ParentBone.Index] * lWorld;
                    _Effect.View = _Camera.lView;
                    _Effect.Projection = _Camera.lProjection;
                }
                _Mesh.Draw();
            }
        }
    }
}