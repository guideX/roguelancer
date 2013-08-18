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
        //Vector3 origin;
        //float yaw, pitch;
        //float distance = (float)0;
        //Vector3 orbitOffset = Vector3.UnitX * distance;
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
            foreach(ModelMesh mesh in model.Meshes) {
                foreach(BasicEffect _Effect in mesh.Effects) {
                    _Effect.EnableDefaultLighting();
                    _Effect.World = transforms[mesh.ParentBone.Index];
                    //_Effect.View = _Game.lCamera.lView;
                    //_Effect.Projection = _Game.lCamera.lProjection;
                }
                mesh.Draw();
            }
        }
    }
}