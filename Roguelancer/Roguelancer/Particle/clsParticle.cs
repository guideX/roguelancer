// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Functionality;
using Roguelancer.Objects;
namespace Roguelancer.Particle {
    class clsParticleStarSheet {
        GraphicsDevice lGraphicsDevice;
        GameCamera lCamera;
        VertexPositionTexture[] lVerts;
        Color[] lVertexColorArray;
        VertexBuffer lParticleVertexBuffer;
        Vector3 lMaxPosition;
        int lMaxParticles;
        static Random lRnd = new Random();
        Effect lParticleEffect;
        Texture2D lParticleColorsTexture;
        public clsParticleStarSheet(GameGraphics _Graphics, Vector3 _MaxPosition, int _MaxParticles, Texture2D _ParticleColorsTexture, Effect _ParticleEffect, int _MaxSize, GameCamera _Camera) {
            lMaxParticles = _MaxParticles;
            lParticleEffect = _ParticleEffect;
            lParticleColorsTexture = _ParticleColorsTexture;
            lMaxPosition = _MaxPosition;
            lVerts = new VertexPositionTexture[lMaxParticles * 4];
            lVertexColorArray = new Color[lMaxParticles];
            Color[] colors = new Color[lParticleColorsTexture.Width * lParticleColorsTexture.Height];
            lParticleColorsTexture.GetData(colors);
            for (int i = 0; i < lMaxParticles; ++i) {
                //float size = (float)lRnd.NextDouble() * _MaxSize;
                float size = 10000f;
                Vector3 position = new Vector3(lRnd.Next(-(int)lMaxPosition.X, (int)lMaxPosition.X), lRnd.Next(-(int)lMaxPosition.Y, (int)lMaxPosition.Y), lMaxPosition.Z);
                lVerts[i * 4] = new VertexPositionTexture(position, new Vector2(0, 0));
                lVerts[(i * 4) + 1] = new VertexPositionTexture(new Vector3(position.X, position.Y + size, position.Z), new Vector2(0, 1));
                lVerts[(i * 4) + 2] = new VertexPositionTexture(new Vector3(position.X + size, position.Y, position.Z), new Vector2(1, 0));
                lVerts[(i * 4) + 3] = new VertexPositionTexture(new Vector3(position.X + size, position.Y + size, position.Z), new Vector2(1, 1));
                lVertexColorArray[i] = colors[(lRnd.Next(0, lParticleColorsTexture.Height) * lParticleColorsTexture.Width) + lRnd.Next(0, lParticleColorsTexture.Width)];
            }
            lParticleVertexBuffer = new VertexBuffer(_Graphics.graphicsDeviceManager.GraphicsDevice, typeof(VertexPositionTexture), lVerts.Length, BufferUsage.None);
        }
        public void Update(GameCamera _Camera, GraphicsDevice _GraphicsDevice) {
            lCamera = _Camera;
            lGraphicsDevice = _GraphicsDevice;
        }
        public void Draw() {
            lGraphicsDevice.SetVertexBuffer(lParticleVertexBuffer);
            for (int i = 0; i < lMaxParticles; ++i) {
                lParticleEffect.Parameters["WorldViewProjection"].SetValue(lCamera.view * lCamera.projection);
                lParticleEffect.Parameters["particleColor"].SetValue(lVertexColorArray[i].ToVector4());
                foreach (EffectPass _EffectPass in lParticleEffect.CurrentTechnique.Passes) {
                    _EffectPass.Apply();
                    lGraphicsDevice.DrawUserPrimitives<VertexPositionTexture>(PrimitiveType.TriangleStrip, lVerts, i * 4, 2);
                }
            }
        }
    }
    class clsParticleExplosion {
        VertexPositionTexture[] lVerts;
        Vector3[] lVertexDirectionArray;
        Color[] lVertexColorArray;
        VertexBuffer lParticleVertexBuffer;
        Vector3 lPosition;
        int lLifeLeft;
        int lNumParticlesPerRound;
        int lMaxParticles;
        static Random lRnd = new Random();
        int lRoundTime;
        int lTimeSinceLastRound = 0;
        GraphicsDevice lGraphicsDevice;
        Effect lParticleEffect;
        Texture2D lParticleColorsTexture;
        int lEndOfLiveParticlesIndex = 0;
        int lEndOfDeadParticlesIndex = 0;
        public bool IsDead {
            get { 
                return lEndOfDeadParticlesIndex == lMaxParticles; 
            }
        }
        public clsParticleExplosion(GraphicsDevice _GraphicsDevice, Vector3 _Position, int _LifeLeft, int _RoundTime, int _NumParticlesPerRound, int _MaxParticles, Texture2D _ParticleColorsTexture, Effect _ParticleEffect, int _MaxSize) {
            lPosition = _Position;
            lLifeLeft = _LifeLeft;
            lNumParticlesPerRound = _NumParticlesPerRound;
            lMaxParticles = _MaxParticles;
            lRoundTime = _RoundTime;
            lGraphicsDevice = _GraphicsDevice;
            lParticleEffect = _ParticleEffect;
            lParticleColorsTexture = _ParticleColorsTexture;
            InitializeParticleVertices(_MaxSize);
        }
        private void InitializeParticleVertices(int _MaxSize) {
            lVerts = new VertexPositionTexture[lMaxParticles * 4];
            lVertexDirectionArray = new Vector3[lMaxParticles];
            lVertexColorArray = new Color[lMaxParticles];
            Color[] colors = new Color[lParticleColorsTexture.Width * lParticleColorsTexture.Height];
            lParticleColorsTexture.GetData(colors);
            for (int i = 0; i < lMaxParticles; ++i) {
                float size = (float)lRnd.NextDouble() * _MaxSize;
                lVerts[i * 4] = new VertexPositionTexture(lPosition, new Vector2(0, 0));
                lVerts[(i * 4) + 1] = new VertexPositionTexture(new Vector3(lPosition.X, lPosition.Y + size, lPosition.Z), new Vector2(0, 1));
                lVerts[(i * 4) + 2] = new VertexPositionTexture(new Vector3(lPosition.X + size, lPosition.Y, lPosition.Z), new Vector2(1, 0));
                lVerts[(i * 4) + 3] = new VertexPositionTexture(new Vector3(lPosition.X + size, lPosition.Y + size, lPosition.Z), new Vector2(1, 1));
                Vector3 direction = new Vector3((float)lRnd.NextDouble() * 2 - 1,(float)lRnd.NextDouble() * 2 - 1,(float)lRnd.NextDouble() * 2 - 1);
                direction.Normalize();
                direction *= (float)lRnd.NextDouble();
                lVertexDirectionArray[i] = direction;
                lVertexColorArray[i] = colors[(lRnd.Next(0, lParticleColorsTexture.Height) * lParticleColorsTexture.Width) + lRnd.Next(0, lParticleColorsTexture.Width)];
            }
            lParticleVertexBuffer = new VertexBuffer(lGraphicsDevice, typeof(VertexPositionTexture), lVerts.Length, BufferUsage.None);
        }
        public void Update(GameTime _GameTime) {
            if (lLifeLeft > 0) {
                lLifeLeft -= _GameTime.ElapsedGameTime.Milliseconds;
            }
            lTimeSinceLastRound += _GameTime.ElapsedGameTime.Milliseconds;
            if (lTimeSinceLastRound > lRoundTime) {
                lTimeSinceLastRound -= lRoundTime;
                if (lEndOfLiveParticlesIndex < lMaxParticles) {
                    lEndOfLiveParticlesIndex += lNumParticlesPerRound;
                    if (lEndOfLiveParticlesIndex > lMaxParticles)
                        lEndOfLiveParticlesIndex = lMaxParticles;
                }
                if (lLifeLeft <= 0) {
                    if (lEndOfDeadParticlesIndex < lMaxParticles) {
                        lEndOfDeadParticlesIndex += lNumParticlesPerRound;
                        if (lEndOfDeadParticlesIndex > lMaxParticles) {
                            lEndOfDeadParticlesIndex = lMaxParticles;
                        }
                    }
                }
            }
            for (int i = lEndOfDeadParticlesIndex; i < lEndOfLiveParticlesIndex; ++i) {
                lVerts[i * 4].Position += lVertexDirectionArray[i];
                lVerts[(i * 4) + 1].Position += lVertexDirectionArray[i];
                lVerts[(i * 4) + 2].Position += lVertexDirectionArray[i];
                lVerts[(i * 4) + 3].Position += lVertexDirectionArray[i];
            }
        }
        public void Draw(GameCamera _Camera) {
            lGraphicsDevice.SetVertexBuffer(lParticleVertexBuffer);
            if (lEndOfLiveParticlesIndex - lEndOfDeadParticlesIndex > 0) {
                for (int i = lEndOfDeadParticlesIndex; i < lEndOfLiveParticlesIndex; ++i) {
                    lParticleEffect.Parameters["WorldViewProjection"].SetValue(_Camera.view * _Camera.projection);
                    lParticleEffect.Parameters["particleColor"].SetValue(lVertexColorArray[i].ToVector4());
                    foreach (EffectPass _EffectPass in lParticleEffect.CurrentTechnique.Passes) {
                        _EffectPass.Apply();
                        lGraphicsDevice.DrawUserPrimitives<VertexPositionTexture>(PrimitiveType.TriangleStrip, lVerts, i * 4, 2);
                    }
                }
            }
        }
    }
}