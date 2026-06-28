using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Weapon system for NPC ships that allows them to fire projectiles at the player
    /// </summary>
    public class NpcWeaponSystem
    {
        private class NpcProjectile
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Damage;
            public NpcShip Owner;
        }

        private List<NpcProjectile> _projectiles = new();
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private Random _random = new();
        private ReputationManager _reputationManager;

        // NPC firing parameters
        private const float FireRange = 1200f;
        private const float ProjectileSpeed = 800f;
        private const float ProjectileDamage = 5f;
        private const float ProjectileLife = 2.5f;
        private const float FireCooldown = 1.2f;
        private const float AccuracySpread = 0.06f;

        // Per-NPC fire timers
        private Dictionary<NpcShip, float> _fireCooldowns = new();

        public NpcWeaponSystem(GraphicsDevice graphicsDevice, ReputationManager reputationManager = null)
        {
            _graphicsDevice = graphicsDevice;
            _reputationManager = reputationManager;
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void SetReputationManager(ReputationManager reputationManager)
        {
            _reputationManager = reputationManager;
        }

        /// <summary>
        /// Update NPC weapon system: fire at player if in range, update projectiles
        /// </summary>
        public void Update(GameTime gameTime, List<NpcShip> npcShips, Ship playerShip)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update existing projectiles
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var proj = _projectiles[i];
                proj.Position += proj.Velocity * deltaTime;
                proj.Life -= deltaTime;

                if (proj.Life <= 0)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                // Check collision with player
                float distToPlayer = Vector3.Distance(proj.Position, playerShip.Position);
                if (distToPlayer < playerShip.CollisionRadius + 5f)
                {
                    // Hit the player! Route through shields first
                    float hullDamage = proj.Damage;
                    if (playerShip.Shields != null)
                    {
                        hullDamage = playerShip.Shields.AbsorbDamage(proj.Damage);
                    }
                    if (hullDamage > 0f)
                    {
                        playerShip.Hull.TakeDamage(hullDamage);
                    }

                    _projectiles.RemoveAt(i);
                }
            }

            // NPC firing logic
            foreach (var npc in npcShips)
            {
                if (npc.IsDestroyed) continue;
                string factionId = FactionManager.NormalizeFactionId(npc.FactionId);
                if (_reputationManager != null && !_reputationManager.IsHostile(factionId)) continue;

                float distToPlayer = Vector3.Distance(npc.Position, playerShip.Position);
                if (distToPlayer > FireRange) continue;

                // Initialize cooldown if not tracked
                if (!_fireCooldowns.ContainsKey(npc))
                    _fireCooldowns[npc] = 0f;

                _fireCooldowns[npc] -= deltaTime;

                if (_fireCooldowns[npc] <= 0f)
                {
                    // Fire at player
                    FireAtTarget(npc, playerShip.Position);
                    _fireCooldowns[npc] = FireCooldown + (float)(_random.NextDouble() * 0.5);
                }
            }
        }

        private void FireAtTarget(NpcShip npc, Vector3 targetPosition)
        {
            Console.WriteLine($"[NPC AI] {npc.Name} ({npc.FactionId}) firing at player");
            Vector3 direction = Vector3.Normalize(targetPosition - npc.Position);

            // Add accuracy spread
            direction.X += (float)(_random.NextDouble() * 2 - 1) * AccuracySpread;
            direction.Y += (float)(_random.NextDouble() * 2 - 1) * AccuracySpread;
            direction.Z += (float)(_random.NextDouble() * 2 - 1) * AccuracySpread;
            direction = Vector3.Normalize(direction);

            var proj = new NpcProjectile
            {
                Position = npc.Position + npc.Forward * 15f,
                Velocity = direction * ProjectileSpeed + npc.Velocity * 0.3f,
                Life = ProjectileLife,
                MaxLife = ProjectileLife,
                Color = Color.Red,
                Damage = ProjectileDamage,
                Owner = npc
            };

            _projectiles.Add(proj);
        }

        /// <summary>
        /// Check projectile collisions with a specific position/radius (for hit impact effects)
        /// </summary>
        public List<HitInfo> CheckCollisions(Vector3 targetPos, float targetRadius, HullIntegrity hull, ShieldSystem shields)
        {
            var hits = new List<HitInfo>();

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var proj = _projectiles[i];
                float dist = Vector3.Distance(proj.Position, targetPos);

                if (dist < targetRadius + 5f)
                {
                    hits.Add(new HitInfo
                    {
                        Position = proj.Position,
                        Direction = Vector3.Normalize(proj.Velocity),
                        WeaponColor = proj.Color,
                        Damage = proj.Damage
                    });

                    _projectiles.RemoveAt(i);
                }
            }

            return hits;
        }

        /// <summary>
        /// Draw NPC projectiles
        /// </summary>
        public void Draw(Camera camera)
        {
            if (_projectiles.Count == 0) return;

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;

            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            // Derive camera vectors from view matrix
            Matrix viewInverse = Matrix.Invert(camera.View);
            Vector3 camRight = new Vector3(viewInverse.M11, viewInverse.M12, viewInverse.M13);
            Vector3 camUp = new Vector3(viewInverse.M21, viewInverse.M22, viewInverse.M23);

            foreach (var proj in _projectiles)
            {
                float lifeRatio = proj.Life / proj.MaxLife;
                float alpha = lifeRatio;
                float size = 3f;

                // Draw as a simple billboard quad
                Vector3 right = camRight * size;
                Vector3 up = camUp * size;

                Color c = proj.Color * alpha;

                var vertices = new VertexPositionColor[]
                {
                    new(proj.Position - right - up, c),
                    new(proj.Position + right - up, c),
                    new(proj.Position + right + up, c),
                    new(proj.Position - right - up, c),
                    new(proj.Position + right + up, c),
                    new(proj.Position - right + up, c)
                };

                _effect.World = Matrix.Identity;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 2);
                }
            }

            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
        }

        /// <summary>
        /// Remove cooldown tracking for destroyed NPCs
        /// </summary>
        public void RemoveNpc(NpcShip npc)
        {
            _fireCooldowns.Remove(npc);
        }
    }
}
