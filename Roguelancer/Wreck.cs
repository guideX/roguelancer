using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roguelancer
{
    /// <summary>
    /// A drifting wreck, the remains of a destroyed ship
    /// </summary>
    public class Wreck
    {
        public Vector3 Position { get; set; }
        public Matrix Orientation { get; private set; }
        public Model Model { get; set; }
        public float Scale { get; set; } = 1.0f;

        private Vector3 _angularVelocity;

        public Wreck(Vector3 position, Matrix orientation, Model model)
        {
            Position = position;
            Orientation = orientation;
            Model = model;

            // Give it a random slow spin
            _angularVelocity = new Vector3(
                (float)(System.Random.Shared.NextDouble() - 0.5) * 0.1f,
                (float)(System.Random.Shared.NextDouble() - 0.5) * 0.1f,
                (float)(System.Random.Shared.NextDouble() - 0.5) * 0.1f
            );
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Apply angular velocity to orientation
            Matrix rotation = Matrix.CreateFromYawPitchRoll(_angularVelocity.Y * deltaTime, _angularVelocity.X * deltaTime, _angularVelocity.Z * deltaTime);
            Orientation *= rotation;
        }

        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null) return;

            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            Matrix world = Matrix.CreateScale(Scale) * modelCorrection * Orientation * Matrix.CreateTranslation(Position);

            foreach (ModelMesh mesh in Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = world;
                    effect.View = view;
                    effect.Projection = projection;

                    effect.EnableDefaultLighting();
                    effect.PreferPerPixelLighting = true;
                    effect.SpecularPower = 16f;
                    effect.Alpha = 1.0f;

                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = new Vector3(0.7f, 0.7f, 0.7f); // Dimmer light for wrecks
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.1f, 0.1f, 0.1f);
                    effect.AmbientLightColor = new Vector3(0.1f, 0.1f, 0.1f);
                }
                mesh.Draw();
            }
        }
    }
}
