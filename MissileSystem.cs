using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Lightweight missile runtime for mounted launcher ordnance.
    /// </summary>
    public class MissileSystem
    {
        private class MissileProjectile
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Speed;
            public float Life;
            public float MaxLife;
            public float Damage;
            public float TurnRate;
            public float Radius;
            public Color Color;
            public NpcShip Target;
            public int? SpoofedCountermeasureId;
        }

        private readonly List<MissileProjectile> _missiles = new List<MissileProjectile>(64);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private float _launchCooldownRemaining;

        private const float DefaultMissileDamage = 40f;
        private const float DefaultMissileSpeed = 850f;
        private const float DefaultMissileTurnRate = 1.8f;
        private const float DefaultMissileLifetime = 5f;
        private const float DefaultMissileRadius = 10f;
        private const float DefaultLaunchCooldown = 0.85f;

        public MissileSystem(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public bool TryFire(Ship ship, EquipmentDefinition launcher, NpcShip target, Vector3 launchOrigin, out string message)
        {
            message = string.Empty;

            if (ship == null)
            {
                message = "No ship available.";
                return false;
            }

            if (launcher == null)
            {
                message = "No missile launcher mounted.";
                return false;
            }

            if (_launchCooldownRemaining > 0f)
            {
                message = $"Missile launcher cooling down ({_launchCooldownRemaining:F1}s).";
                return false;
            }

            Vector3 forward = ship.Forward;
            if (forward.LengthSquared() < 0.0001f)
            {
                forward = Vector3.Forward;
            }
            else
            {
                forward.Normalize();
            }

            float damage = ResolvePositiveStat(launcher.MissileDamage, DefaultMissileDamage);
            float speed = ResolvePositiveStat(launcher.MissileSpeed, DefaultMissileSpeed);
            float turnRate = ResolvePositiveStat(launcher.MissileTurnRate, DefaultMissileTurnRate);
            float lifetime = ResolvePositiveStat(launcher.MissileLifetime, DefaultMissileLifetime);

            _missiles.Add(new MissileProjectile
            {
                Position = launchOrigin,
                Velocity = forward * speed + ship.Velocity,
                Speed = speed,
                Life = 0f,
                MaxLife = lifetime,
                Damage = damage,
                TurnRate = turnRate,
                Radius = DefaultMissileRadius,
                Color = new Color(255, 180, 70),
                Target = target != null && !target.IsDestroyed ? target : null
            });

            _launchCooldownRemaining = DefaultLaunchCooldown;
            string lockState = target != null && !target.IsDestroyed ? "Locked" : "Dumbfire";
            message = $"Missile launched: {launcher.Name} ({lockState})";
            Console.WriteLine($"[MISSILE] Fired {launcher.Name} | {lockState}");
            return true;
        }

        public List<HitInfo> Update(GameTime gameTime, IReadOnlyList<NpcShip> npcShips)
        {
            return Update(gameTime, npcShips, null);
        }

        public List<HitInfo> Update(GameTime gameTime, IReadOnlyList<NpcShip> npcShips, CountermeasureSystem countermeasureSystem)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            List<HitInfo> hits = new List<HitInfo>();

            if (_launchCooldownRemaining > 0f)
            {
                _launchCooldownRemaining = Math.Max(0f, _launchCooldownRemaining - deltaTime);
            }

            for (int i = _missiles.Count - 1; i >= 0; i--)
            {
                MissileProjectile missile = _missiles[i];
                missile.Life += deltaTime;

                if (missile.Life >= missile.MaxLife)
                {
                    Console.WriteLine("[MISSILE] Missile timeout");
                    _missiles.RemoveAt(i);
                    continue;
                }

                Vector3 currentDirection = missile.Velocity;
                if (currentDirection.LengthSquared() < 0.0001f)
                {
                    currentDirection = Vector3.Forward;
                }
                else
                {
                    currentDirection.Normalize();
                }

                Vector3 desiredDirection = currentDirection;
                if (missile.Target != null && !missile.Target.IsDestroyed)
                {
                    Vector3 toTarget = missile.Target.Position - missile.Position;
                    if (toTarget.LengthSquared() > 0.0001f)
                    {
                        desiredDirection = Vector3.Normalize(toTarget);
                    }

                    if (countermeasureSystem != null && countermeasureSystem.TryGetBestSpoofTarget(missile.Position, missile.Target, missile.Damage, out CountermeasureSystem.CountermeasureDecoy spoofTarget))
                    {
                        Vector3 toCountermeasure = spoofTarget.Position - missile.Position;
                        if (toCountermeasure.LengthSquared() > 0.0001f)
                        {
                            desiredDirection = Vector3.Normalize(toCountermeasure);
                        }

                        if (missile.SpoofedCountermeasureId != spoofTarget.Id)
                        {
                            Console.WriteLine($"[MISSILE] Spoofed toward countermeasure #{spoofTarget.Id} ({spoofTarget.SourceEquipmentName})");
                        }

                        missile.SpoofedCountermeasureId = spoofTarget.Id;
                    }
                    else
                    {
                        missile.SpoofedCountermeasureId = null;
                    }
                }
                else
                {
                    missile.SpoofedCountermeasureId = null;
                }

                float blend = MathHelper.Clamp(missile.TurnRate * deltaTime, 0f, 1f);
                Vector3 guidedDirection = Vector3.Lerp(currentDirection, desiredDirection, blend);
                if (guidedDirection.LengthSquared() < 0.0001f)
                {
                    guidedDirection = desiredDirection;
                }
                if (guidedDirection.LengthSquared() < 0.0001f)
                {
                    guidedDirection = Vector3.Forward;
                }
                guidedDirection.Normalize();

                missile.Velocity = guidedDirection * missile.Speed;
                missile.Position += missile.Velocity * deltaTime;

                bool removed = false;
                if (npcShips != null)
                {
                    for (int npcIndex = 0; npcIndex < npcShips.Count; npcIndex++)
                    {
                        NpcShip npc = npcShips[npcIndex];
                        if (npc == null || npc.IsDestroyed)
                        {
                            continue;
                        }

                        float collisionRadius = npc.Radius + missile.Radius;
                        float distance = Vector3.Distance(missile.Position, npc.Position);
                        if (distance > collisionRadius)
                        {
                            continue;
                        }

                        float hullDamage = missile.Damage;
                        if (npc.Shields != null)
                        {
                            hullDamage = npc.Shields.AbsorbDamage(missile.Damage);
                        }

                        if (hullDamage > 0f)
                        {
                            npc.Hull.TakeDamage(hullDamage);
                        }

                        Vector3 impactDirection = missile.Velocity;
                        if (impactDirection.LengthSquared() < 0.0001f)
                        {
                            impactDirection = Vector3.Forward;
                        }
                        else
                        {
                            impactDirection.Normalize();
                        }

                        hits.Add(new HitInfo
                        {
                            Position = missile.Position,
                            Direction = impactDirection,
                            WeaponColor = missile.Color,
                            WeaponType = WeaponType.Fireball,
                            Damage = missile.Damage
                        });

                        Console.WriteLine($"[MISSILE] Hit {npc.Name} for {missile.Damage:F1}");
                        _missiles.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }

                if (removed)
                {
                    continue;
                }
            }

            return hits;
        }

        public void Draw(Camera camera)
        {
            if (camera == null || _missiles.Count == 0)
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

            foreach (var missile in _missiles)
            {
                float lifeRatio = missile.MaxLife > 0f ? missile.Life / missile.MaxLife : 1f;
                float alpha = MathHelper.Clamp(1f - lifeRatio, 0.15f, 1f);
                Color color = missile.Color * alpha;

                Vector3 right = camRight * missile.Radius * 1.5f;
                Vector3 up = camUp * missile.Radius * 0.8f;

                VertexPositionColor[] vertices = new VertexPositionColor[]
                {
                    new VertexPositionColor(missile.Position - right - up, color),
                    new VertexPositionColor(missile.Position + right - up, color),
                    new VertexPositionColor(missile.Position + right + up, color),
                    new VertexPositionColor(missile.Position - right + up, color)
                };

                short[] indices = new short[] { 0, 1, 2, 0, 2, 3 };
                _effect.World = Matrix.Identity;
                foreach (var pass in _effect.CurrentTechnique.Passes)
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
    }
}
