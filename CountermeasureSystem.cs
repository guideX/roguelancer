using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Lightweight countermeasure runtime for mounted decoys and chaff.
    /// </summary>
    public class CountermeasureSystem
    {
        public sealed class CountermeasureDecoy
        {
            public int Id { get; }
            public string SourceEquipmentId { get; }
            public string SourceEquipmentName { get; }
            public Vector3 Position { get; private set; }
            public Vector3 Velocity { get; private set; }
            public float Life { get; private set; }
            public float MaxLife { get; }
            public float AttractionRadius { get; }
            public float Strength { get; }
            public float Radius { get; }

            public CountermeasureDecoy(
                int id,
                string sourceEquipmentId,
                string sourceEquipmentName,
                Vector3 position,
                Vector3 velocity,
                float maxLife,
                float attractionRadius,
                float strength,
                float radius)
            {
                Id = id;
                SourceEquipmentId = sourceEquipmentId ?? string.Empty;
                SourceEquipmentName = sourceEquipmentName ?? string.Empty;
                Position = position;
                Velocity = velocity;
                MaxLife = maxLife;
                AttractionRadius = attractionRadius;
                Strength = strength;
                Radius = radius;
            }

            public float LifeRatio => MaxLife > 0f ? Life / MaxLife : 1f;
            public bool IsExpired => Life >= MaxLife;

            public void Update(float deltaTime)
            {
                if (deltaTime <= 0f)
                {
                    return;
                }

                Life += deltaTime;
                Position += Velocity * deltaTime;
            }
        }

        private readonly List<CountermeasureDecoy> _countermeasures = new List<CountermeasureDecoy>(16);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private int _nextId = 1;
        private float _cooldownRemaining;

        private const float DefaultCountermeasureLife = 4f;
        private const float DefaultCountermeasureAttractionRadius = 1200f;
        private const float DefaultCountermeasureStrength = 2f;
        private const float DefaultCountermeasureCooldown = 5f;
        private const float DefaultCountermeasureRadius = 6f;
        private const float DefaultCountermeasureSpawnOffset = 24f;
        private const float DefaultCountermeasureSpawnVelocity = 180f;

        public CountermeasureSystem(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            if (_graphicsDevice != null)
            {
                _effect = new BasicEffect(graphicsDevice)
                {
                    VertexColorEnabled = true,
                    LightingEnabled = false
                };
            }
        }

        public bool IsCoolingDown => _cooldownRemaining > 0f;
        public float CooldownRemaining => _cooldownRemaining;
        public IReadOnlyList<CountermeasureDecoy> ActiveCountermeasures => _countermeasures;

        public bool TryDeploy(Ship ship, EquipmentDefinition dropper, out string message)
        {
            message = string.Empty;

            if (ship == null)
            {
                message = "No ship available.";
                return false;
            }

            if (dropper == null || dropper.EquipmentType != EquipmentType.CountermeasureDropper)
            {
                message = "No countermeasure dropper mounted.";
                return false;
            }

            if (_cooldownRemaining > 0f)
            {
                message = $"Countermeasure dropper cooling down ({_cooldownRemaining:F1}s).";
                return false;
            }

            float life = ResolvePositiveStat(dropper.CountermeasureLife, DefaultCountermeasureLife);
            float attractionRadius = ResolvePositiveStat(dropper.CountermeasureAttractionRadius, DefaultCountermeasureAttractionRadius);
            float strength = ResolvePositiveStat(dropper.CountermeasureStrength, DefaultCountermeasureStrength);
            float cooldown = ResolvePositiveStat(dropper.CountermeasureCooldown, DefaultCountermeasureCooldown);

            Vector3 forward = GetSafeDirection(ship.Forward, Vector3.Forward);
            Vector3 up = GetSafeDirection(ship.Up, Vector3.Up);
            Vector3 spawnPosition = ship.Position - forward * DefaultCountermeasureSpawnOffset + up * 2f;
            Vector3 spawnVelocity = ship.Velocity - forward * DefaultCountermeasureSpawnVelocity + up * 12f;

            var decoy = new CountermeasureDecoy(
                _nextId++,
                dropper.Id,
                dropper.Name,
                spawnPosition,
                spawnVelocity,
                life,
                attractionRadius,
                strength,
                DefaultCountermeasureRadius);

            _countermeasures.Add(decoy);
            _cooldownRemaining = cooldown;

            message = $"Countermeasure deployed: {dropper.Name}";
            Console.WriteLine($"[COUNTERMEASURE] Deployed {dropper.Name} | life {life:F1}s | cd {cooldown:F1}s");
            return true;
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaTime);
            }

            for (int i = _countermeasures.Count - 1; i >= 0; i--)
            {
                CountermeasureDecoy countermeasure = _countermeasures[i];
                countermeasure.Update(deltaTime);

                if (!countermeasure.IsExpired)
                {
                    continue;
                }

                Console.WriteLine($"[COUNTERMEASURE] Expired #{countermeasure.Id} ({countermeasure.SourceEquipmentName})");
                _countermeasures.RemoveAt(i);
            }
        }

        public bool TryGetBestSpoofTarget(
            Vector3 missilePosition,
            NpcShip originalTarget,
            float missileDamage,
            out CountermeasureDecoy spoofTarget)
        {
            spoofTarget = null;

            if (originalTarget == null || originalTarget.IsDestroyed || _countermeasures.Count == 0)
            {
                return false;
            }

            float originalDistance = Vector3.Distance(missilePosition, originalTarget.Position);
            float originalStrength = Math.Max(1f, originalTarget.Radius * 0.1f);
            float originalScore = originalStrength / Math.Max(originalDistance, 1f);

            float bestScore = originalScore;
            for (int i = 0; i < _countermeasures.Count; i++)
            {
                CountermeasureDecoy candidate = _countermeasures[i];
                float distance = Vector3.Distance(missilePosition, candidate.Position);
                if (distance > candidate.AttractionRadius)
                {
                    continue;
                }

                float adjustedStrength = candidate.Strength / (1f + Math.Max(0f, missileDamage) * 0.01f);
                float score = adjustedStrength / Math.Max(distance, 1f);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                spoofTarget = candidate;
            }

            return spoofTarget != null;
        }

        public void Draw(Camera camera)
        {
            if (camera == null || _effect == null || _countermeasures.Count == 0)
            {
                return;
            }

            Matrix viewInverse = Matrix.Invert(camera.View);
            Vector3 camRight = new Vector3(viewInverse.M11, viewInverse.M12, viewInverse.M13);
            Vector3 camUp = new Vector3(viewInverse.M21, viewInverse.M22, viewInverse.M23);

            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRasterizer = _graphicsDevice.RasterizerState;

            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            foreach (CountermeasureDecoy countermeasure in _countermeasures)
            {
                float lifeRatio = countermeasure.LifeRatio;
                float alpha = MathHelper.Clamp(1f - lifeRatio, 0.2f, 1f);
                float pulse = 0.7f + 0.3f * (float)Math.Sin(countermeasure.Life * 12f);
                Color color = new Color(90, 255, 220) * (alpha * pulse);

                float size = Math.Max(4f, countermeasure.Radius * 2f);
                Vector3 right = camRight * size;
                Vector3 up = camUp * size;

                VertexPositionColor[] vertices = new VertexPositionColor[]
                {
                    new VertexPositionColor(countermeasure.Position - right - up, color),
                    new VertexPositionColor(countermeasure.Position + right - up, color),
                    new VertexPositionColor(countermeasure.Position + right + up, color),
                    new VertexPositionColor(countermeasure.Position - right + up, color)
                };

                short[] indices = new short[] { 0, 1, 2, 0, 2, 3 };
                _effect.World = Matrix.Identity;
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        vertices,
                        0,
                        4,
                        indices,
                        0,
                        2);
                }
            }

            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRasterizer;
        }

        private static float ResolvePositiveStat(float candidate, float fallback)
        {
            return candidate > 0f && !float.IsNaN(candidate) && !float.IsInfinity(candidate) ? candidate : fallback;
        }

        private static Vector3 GetSafeDirection(Vector3 vector, Vector3 fallback)
        {
            if (vector.LengthSquared() < 0.0001f)
            {
                return fallback;
            }

            vector.Normalize();
            return vector;
        }
    }
}
