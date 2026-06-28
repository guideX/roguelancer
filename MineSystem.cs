using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Lightweight runtime for deployed proximity mines.
    /// </summary>
    public class MineSystem
    {
        public sealed class MineDetonation
        {
            public MineDetonation(int mineId, Vector3 position, string sourceEquipmentName, IReadOnlyList<HitInfo> hits)
            {
                MineId = mineId;
                Position = position;
                SourceEquipmentName = sourceEquipmentName ?? string.Empty;
                Hits = hits ?? Array.Empty<HitInfo>();
            }

            public int MineId { get; }
            public Vector3 Position { get; }
            public string SourceEquipmentName { get; }
            public IReadOnlyList<HitInfo> Hits { get; }
        }

        private sealed class ActiveMine
        {
            public int Id;
            public string SourceEquipmentId;
            public string SourceEquipmentName;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Damage;
            public float TriggerRadius;
            public float BlastRadius;
            public float ArmDelay;
            public bool IsArmed;
        }

        private readonly List<ActiveMine> _mines = new List<ActiveMine>(32);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private int _nextId = 1;
        private float _cooldownRemaining;

        private const float DefaultMineDamage = 100f;
        private const float DefaultMineTriggerRadius = 85f;
        private const float DefaultMineBlastRadius = 220f;
        private const float DefaultMineLifetime = 16f;
        private const float DefaultMineCooldown = 5f;
        private const float DefaultMineArmDelay = 0.75f;
        private const float DefaultMineRadius = 6f;
        private const float DefaultMineSpawnOffset = 22f;
        private const float DefaultMineSpawnUpOffset = 2f;
        private const float DefaultMineSpawnVelocity = 40f;

        public MineSystem(GraphicsDevice graphicsDevice)
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

        public bool TryDeploy(Ship ship, EquipmentDefinition dropper, out string message)
        {
            message = string.Empty;

            if (ship == null)
            {
                message = "No ship available.";
                return false;
            }

            if (dropper == null || dropper.EquipmentType != EquipmentType.MineDropper)
            {
                message = "No mine dropper mounted.";
                Console.WriteLine("[MINE] No mine dropper mounted.");
                return false;
            }

            if (_cooldownRemaining > 0f)
            {
                message = $"Mine dropper cooling down ({_cooldownRemaining:F1}s).";
                Console.WriteLine($"[MINE] Cooldown blocked ({_cooldownRemaining:F1}s remaining).");
                return false;
            }

            float damage = ResolvePositiveStat(dropper.MineDamage, DefaultMineDamage);
            float triggerRadius = ResolvePositiveStat(dropper.MineTriggerRadius, DefaultMineTriggerRadius);
            float blastRadius = ResolvePositiveStat(dropper.MineBlastRadius, DefaultMineBlastRadius);
            if (blastRadius < triggerRadius)
            {
                blastRadius = triggerRadius;
            }

            float lifetime = ResolvePositiveStat(dropper.MineLifetime, DefaultMineLifetime);
            float cooldown = ResolvePositiveStat(dropper.MineCooldown, DefaultMineCooldown);
            float armDelay = ResolvePositiveStat(dropper.MineArmDelay, DefaultMineArmDelay);

            Vector3 forward = GetSafeDirection(ship.Forward, Vector3.Forward);
            Vector3 up = GetSafeDirection(ship.Up, Vector3.Up);
            float spawnOffset = Math.Max(DefaultMineSpawnOffset, ship.CollisionRadius + 8f);
            Vector3 position = ship.Position - forward * spawnOffset + up * DefaultMineSpawnUpOffset;
            Vector3 velocity = ship.Velocity - forward * DefaultMineSpawnVelocity;

            _mines.Add(new ActiveMine
            {
                Id = _nextId++,
                SourceEquipmentId = dropper.Id,
                SourceEquipmentName = dropper.Name,
                Position = position,
                Velocity = velocity,
                Life = 0f,
                MaxLife = lifetime,
                Damage = damage,
                TriggerRadius = triggerRadius,
                BlastRadius = blastRadius,
                ArmDelay = armDelay,
                IsArmed = false
            });

            _cooldownRemaining = cooldown;
            message = $"Mine deployed: {dropper.Name}";
            Console.WriteLine($"[MINE] Deployed #{_nextId - 1} ({dropper.Name}) | arm {armDelay:F1}s | life {lifetime:F1}s | cd {cooldown:F1}s");
            return true;
        }

        public List<MineDetonation> Update(GameTime gameTime, IReadOnlyList<NpcShip> npcShips, Func<NpcShip, bool> hostilePredicate = null)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            List<MineDetonation> detonations = new List<MineDetonation>();

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaTime);
            }

            for (int i = _mines.Count - 1; i >= 0; i--)
            {
                ActiveMine mine = _mines[i];
                mine.Life += deltaTime;
                mine.Position += mine.Velocity * deltaTime;

                if (!mine.IsArmed && mine.Life >= mine.ArmDelay)
                {
                    mine.IsArmed = true;
                    Console.WriteLine($"[MINE] Armed #{mine.Id} ({mine.SourceEquipmentName})");
                }

                if (mine.IsArmed && npcShips != null && npcShips.Count > 0)
                {
                    NpcShip triggerTarget = FindTriggerTarget(mine, npcShips, hostilePredicate);
                    if (triggerTarget != null)
                    {
                        detonations.Add(DetonateMine(i, mine, npcShips));
                        continue;
                    }
                }

                if (mine.Life >= mine.MaxLife)
                {
                    Console.WriteLine($"[MINE] Expired #{mine.Id} ({mine.SourceEquipmentName})");
                    _mines.RemoveAt(i);
                }
            }

            return detonations;
        }

        public void Draw(Camera camera)
        {
            if (camera == null || _effect == null || _mines.Count == 0)
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

            foreach (ActiveMine mine in _mines)
            {
                float lifeRatio = mine.MaxLife > 0f ? mine.Life / mine.MaxLife : 1f;
                float alpha = MathHelper.Clamp(1f - lifeRatio, 0.25f, 1f);
                float pulse = mine.IsArmed ? 0.75f + 0.25f * (float)Math.Sin(mine.Life * 10f) : 0.5f;
                Color color = mine.IsArmed
                    ? new Color(255, 220, 120) * (alpha * pulse)
                    : new Color(255, 140, 60) * (alpha * pulse);

                float size = mine.IsArmed ? 4f : 3f;
                Vector3 right = camRight * size;
                Vector3 up = camUp * size;

                VertexPositionColor[] vertices = new VertexPositionColor[]
                {
                    new VertexPositionColor(mine.Position - right - up, color),
                    new VertexPositionColor(mine.Position + right - up, color),
                    new VertexPositionColor(mine.Position + right + up, color),
                    new VertexPositionColor(mine.Position - right + up, color)
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

        private MineDetonation DetonateMine(int index, ActiveMine mine, IReadOnlyList<NpcShip> npcShips)
        {
            List<HitInfo> hits = new List<HitInfo>();

            for (int npcIndex = 0; npcIndex < npcShips.Count; npcIndex++)
            {
                NpcShip npc = npcShips[npcIndex];
                if (npc == null || npc.IsDestroyed)
                {
                    continue;
                }

                float distance = Vector3.Distance(mine.Position, npc.Position);
                if (distance > mine.BlastRadius)
                {
                    continue;
                }

                float distanceRatio = mine.BlastRadius > 0f ? MathHelper.Clamp(distance / mine.BlastRadius, 0f, 1f) : 1f;
                float damageScale = 1f - distanceRatio;
                float damage = mine.Damage * damageScale;
                if (damage <= 0f)
                {
                    continue;
                }

                float hullDamage = damage;
                if (npc.Shields != null)
                {
                    hullDamage = npc.Shields.AbsorbDamage(damage);
                }

                if (hullDamage > 0f)
                {
                    npc.Hull.TakeDamage(hullDamage);
                }

                Vector3 impactDirection = npc.Position - mine.Position;
                if (impactDirection.LengthSquared() < 0.0001f)
                {
                    impactDirection = Vector3.Up;
                }
                else
                {
                    impactDirection.Normalize();
                }

                hits.Add(new HitInfo
                {
                    Position = mine.Position,
                    Direction = impactDirection,
                    WeaponColor = new Color(255, 210, 100),
                    WeaponType = WeaponType.Fireball,
                    Damage = damage
                });
            }

            Console.WriteLine($"[MINE] Detonated #{mine.Id} ({mine.SourceEquipmentName}) | hits {hits.Count}");
            _mines.RemoveAt(index);

            return new MineDetonation(mine.Id, mine.Position, mine.SourceEquipmentName, hits);
        }

        private NpcShip FindTriggerTarget(ActiveMine mine, IReadOnlyList<NpcShip> npcShips, Func<NpcShip, bool> hostilePredicate)
        {
            for (int i = 0; i < npcShips.Count; i++)
            {
                NpcShip npc = npcShips[i];
                if (npc == null || npc.IsDestroyed)
                {
                    continue;
                }

                if (hostilePredicate != null && !hostilePredicate(npc))
                {
                    continue;
                }

                if (Vector3.Distance(mine.Position, npc.Position) <= mine.TriggerRadius)
                {
                    return npc;
                }
            }

            return null;
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
