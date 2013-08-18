// Roguelancer Planet
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Objects {
    public class clsPlanet : intGame {
        public Matrix lWorld;
        public string modelPath { get; set; }
        public Vector3 startPosition { get; set; }
        private Model model;
        private Vector3 modelPosition;
        float modelRotation = 0.0f;
        public void Initialize(clsGame _Game) {
            modelPosition = new Vector3();
        }
        public void LoadContent(clsGame _Game) {
            model = _Game.Content.Load<Model>(modelPath);
            modelPosition = startPosition;
        }
        public void Update(clsGame _Game) {
            // Planets do not update
        }
        public void Draw(clsGame _Game) {
            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);
            foreach(ModelMesh modelMesh in model.Meshes) {
                foreach(BasicEffect basicEffect in modelMesh.Effects) {
                    basicEffect.EnableDefaultLighting();
                    basicEffect.World = transforms[modelMesh.ParentBone.Index] * Matrix.CreateRotationY(modelRotation) * Matrix.CreateTranslation(modelPosition);
                    basicEffect.View = Matrix.CreateLookAt(_Game.lCamera.lPosition, Vector3.Zero, Vector3.Up);
                    basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45.0f), _Game.lCamera.lAspectRatio, 1.0f, 10.0f);
                }
                modelMesh.Draw();
            }
        }
    }
}