using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Lightweight floating cargo pod that can be tractored back into the player's hold.
    /// </summary>
    public sealed class CargoPod
    {
        public string CommodityId { get; }
        public int Quantity { get; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float LifetimeSeconds { get; }
        public float AgeSeconds { get; private set; }
        public float PickupRadius { get; }
        public bool CargoFullNotified { get; set; }
        public bool DetectionNotified { get; set; }

        public bool IsExpired => AgeSeconds >= LifetimeSeconds;

        private CargoPod(string commodityId, int quantity, Vector3 position, Vector3 velocity, float lifetimeSeconds, float pickupRadius)
        {
            CommodityId = commodityId;
            Quantity = quantity;
            Position = position;
            Velocity = velocity;
            LifetimeSeconds = lifetimeSeconds;
            PickupRadius = pickupRadius;
        }

        public static bool TryCreate(string commodityId, int quantity, Vector3 position, Vector3 velocity, float lifetimeSeconds, float pickupRadius, out CargoPod pod)
        {
            pod = null;

            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity == null || quantity <= 0 || lifetimeSeconds <= 0f || pickupRadius <= 0f)
            {
                return false;
            }

            pod = new CargoPod(commodity.Id, quantity, position, velocity, lifetimeSeconds, pickupRadius);
            return true;
        }

        public Commodity GetCommodity()
        {
            return CommodityCatalog.GetById(CommodityId);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime <= 0f || IsExpired)
            {
                return;
            }

            AgeSeconds += deltaTime;
            Position += Velocity * deltaTime;

            // Gentle drag keeps the pod readable without letting it drift forever.
            Velocity *= MathHelper.Clamp(1f - (deltaTime * 0.05f), 0.92f, 1f);
        }

        public void ApplyTractor(Vector3 playerPosition, float deltaTime, float tractorAcceleration, float maxTractorSpeed, float tractorRange)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 toPlayer = playerPosition - Position;
            float distanceSquared = toPlayer.LengthSquared();
            if (distanceSquared < 0.0001f)
            {
                Velocity = Vector3.Zero;
                return;
            }

            Vector3 direction = Vector3.Normalize(toPlayer);
            float distance = (float)Math.Sqrt(distanceSquared);
            float pullStrength = MathHelper.Clamp(1f - (distance / Math.Max(1f, tractorRange)), 0f, 1f);

            Velocity += direction * (tractorAcceleration * pullStrength * deltaTime);

            float maxSpeed = Math.Max(15f, maxTractorSpeed * Math.Max(0.35f, pullStrength));
            float speedSquared = Velocity.LengthSquared();
            if (speedSquared > maxSpeed * maxSpeed)
            {
                Velocity = Vector3.Normalize(Velocity) * maxSpeed;
            }
        }

        public bool IsWithinPickupRange(Vector3 playerPosition)
        {
            float distanceSquared = Vector3.DistanceSquared(Position, playerPosition);
            return distanceSquared <= PickupRadius * PickupRadius;
        }

        public void Draw(GraphicsDevice device, BasicEffect effect, Matrix view, Matrix projection, Color color, float size = 14f)
        {
            if (device == null || effect == null)
            {
                return;
            }

            effect.World = Matrix.Identity;
            effect.View = view;
            effect.Projection = projection;
            effect.VertexColorEnabled = true;
            effect.LightingEnabled = false;

            float pulse = 0.75f + (0.25f * (float)Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * 6.0));
            Color markerColor = color * pulse;

            float half = Math.Max(6f, size * 0.85f);
            Vector3[] corners =
            {
                Position + new Vector3(-half, -half, -half),
                Position + new Vector3( half, -half, -half),
                Position + new Vector3(-half,  half, -half),
                Position + new Vector3( half,  half, -half),
                Position + new Vector3(-half, -half,  half),
                Position + new Vector3( half, -half,  half),
                Position + new Vector3(-half,  half,  half),
                Position + new Vector3( half,  half,  half)
            };

            VertexPositionColor[] vertices =
            {
                new VertexPositionColor(corners[0], markerColor),
                new VertexPositionColor(corners[1], markerColor),
                new VertexPositionColor(corners[1], markerColor),
                new VertexPositionColor(corners[3], markerColor),
                new VertexPositionColor(corners[3], markerColor),
                new VertexPositionColor(corners[2], markerColor),
                new VertexPositionColor(corners[2], markerColor),
                new VertexPositionColor(corners[0], markerColor),

                new VertexPositionColor(corners[4], markerColor),
                new VertexPositionColor(corners[5], markerColor),
                new VertexPositionColor(corners[5], markerColor),
                new VertexPositionColor(corners[7], markerColor),
                new VertexPositionColor(corners[7], markerColor),
                new VertexPositionColor(corners[6], markerColor),
                new VertexPositionColor(corners[6], markerColor),
                new VertexPositionColor(corners[4], markerColor),

                new VertexPositionColor(corners[0], markerColor),
                new VertexPositionColor(corners[4], markerColor),
                new VertexPositionColor(corners[1], markerColor),
                new VertexPositionColor(corners[5], markerColor),
                new VertexPositionColor(corners[2], markerColor),
                new VertexPositionColor(corners[6], markerColor),
                new VertexPositionColor(corners[3], markerColor),
                new VertexPositionColor(corners[7], markerColor)
            };

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, 12);
            }

            float dot = Math.Max(2.5f, size * 0.18f);
            VertexPositionColor[] dotVertices =
            {
                new VertexPositionColor(Position - Vector3.Right * dot, Color.White * pulse),
                new VertexPositionColor(Position + Vector3.Right * dot, Color.White * pulse),
                new VertexPositionColor(Position - Vector3.Up * dot, Color.White * pulse),
                new VertexPositionColor(Position + Vector3.Up * dot, Color.White * pulse),
                new VertexPositionColor(Position - Vector3.Forward * dot, Color.White * pulse),
                new VertexPositionColor(Position + Vector3.Forward * dot, Color.White * pulse)
            };

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList, dotVertices, 0, 3);
            }
        }
    }
}
