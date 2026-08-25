using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Runtime NPC projectile simulation. Weapon identity and combat stats are
    /// read once from the NPC's mounted canonical loadout and then cached for
    /// the lifetime of that NPC.
    /// </summary>
    public class NpcWeaponSystem
    {
        private sealed class NpcProjectile
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Damage;
            public string WeaponId;
            public NpcShip Owner;
            public NpcShip Target;
        }

        private sealed class NpcWeaponProfile
        {
            public List<WeaponEquipmentDefinition> Weapons { get; } = new();
            public float FiringRange { get; set; }
            public float RefireRate { get; set; }
        }

        private readonly List<NpcProjectile> _projectiles = new();
        private readonly Dictionary<NpcShip, float> _fireCooldowns = new();
        private readonly Dictionary<NpcShip, NpcWeaponProfile> _weaponProfiles = new();
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Random _random = new();
        private ReputationManager _reputationManager;

        private const float AccuracySpread = 0.06f;

        public int ActiveProjectileCount => _projectiles.Count;

        /// <summary>
        /// Raised once per canonical mounted gun fired by an NPC. This is
        /// useful for diagnostics and smoke tests without exposing projectiles.
        /// </summary>
        public event Action<NpcShip, NpcShip, string, WeaponEquipmentDefinition> NpcWeaponFired;

        /// <summary>
        /// Raised at the authoritative NPC projectile hit point, before the
        /// target hull callback can remove the ship from traffic.
        /// </summary>
        public event Action<NpcShip, NpcShip, float> NpcShipDamaged;

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
        /// Updates existing projectiles and fires each valid mounted NPC gun
        /// when its target is within the shortest range of the mounted set.
        /// </summary>
        public void Update(GameTime gameTime, List<NpcShip> npcShips, Ship playerShip)
        {
            if (gameTime == null || npcShips == null)
            {
                return;
            }

            float deltaTime = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);

            // Update existing projectiles.
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                NpcProjectile projectile = _projectiles[i];
                projectile.Position += projectile.Velocity * deltaTime;
                projectile.Life -= deltaTime;

                if (projectile.Life <= 0f)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                if (projectile.Target != null)
                {
                    if (projectile.Target.IsDestroyed)
                    {
                        _projectiles.RemoveAt(i);
                        continue;
                    }

                    float distanceToNpc = Vector3.Distance(projectile.Position, projectile.Target.Position);
                    if (distanceToNpc < projectile.Target.Radius + 5f)
                    {
                        ApplyNpcDamage(projectile.Owner, projectile.Target, projectile.Damage);
                        _projectiles.RemoveAt(i);
                    }

                    continue;
                }

                // Player-targeted projectiles retain the established path.
                if (playerShip != null)
                {
                    float distanceToPlayer = Vector3.Distance(projectile.Position, playerShip.Position);
                    if (distanceToPlayer < playerShip.CollisionRadius + 5f)
                    {
                        playerShip.ApplyCombatDamage(projectile.Damage, hostile: true);

                        _projectiles.RemoveAt(i);
                    }
                }
            }

            // NPC firing logic.
            foreach (NpcShip npc in npcShips)
            {
                if (npc == null || npc.IsDestroyed)
                {
                    continue;
                }

                if (npc.IsTradeLaneTransit || npc.CruiseDrive.BlocksStandardWeapons)
                {
                    // Lane travelers and active-cruise NPCs both obey the
                    // explicit standard-weapon restriction.
                    continue;
                }

                NpcWeaponProfile profile = GetWeaponProfile(npc);
                if (profile == null || profile.Weapons.Count == 0 || profile.FiringRange <= 0f)
                {
                    continue;
                }

                NpcShip factionTarget = npc.FactionCombatTarget;
                bool hasFactionTarget = factionTarget != null &&
                    npc.HasValidFactionCombatTarget(profile.FiringRange);
                bool hasPlayerTarget = !hasFactionTarget &&
                    playerShip != null &&
                    (_reputationManager == null || npc.HasValidPlayerTarget(_reputationManager));

                if (!hasFactionTarget && !hasPlayerTarget)
                {
                    continue;
                }

                Vector3 targetPosition = hasFactionTarget ? factionTarget.Position : playerShip.Position;
                if (Vector3.Distance(npc.Position, targetPosition) > profile.FiringRange)
                {
                    continue;
                }

                if (!_fireCooldowns.ContainsKey(npc))
                {
                    _fireCooldowns[npc] = 0f;
                }

                _fireCooldowns[npc] -= deltaTime;
                if (_fireCooldowns[npc] > 0f)
                {
                    continue;
                }

                int fired = FireAtTarget(
                    npc,
                    targetPosition,
                    hasFactionTarget ? factionTarget : null,
                    profile);
                _fireCooldowns[npc] = fired > 0 ? profile.RefireRate : 0.1f;
            }
        }

        private int FireAtTarget(
            NpcShip npc,
            Vector3 targetPosition,
            NpcShip target,
            NpcWeaponProfile profile)
        {
            string targetLabel = target == null ? "player" : $"{target.Name} ({target.FactionId})";
            Console.WriteLine($"[NPC AI] {npc.Name} ({npc.FactionId}) firing at {targetLabel}");

            Vector3 toTarget = targetPosition - npc.Position;
            if (toTarget.LengthSquared() < 0.0001f)
            {
                return 0;
            }

            Vector3 baseDirection = Vector3.Normalize(toTarget);
            int fired = 0;
            for (int weaponIndex = 0; weaponIndex < profile.Weapons.Count; weaponIndex++)
            {
                WeaponEquipmentDefinition weapon = profile.Weapons[weaponIndex];
                if (!NpcEquipmentLoadoutFactory.IsValidNpcWeapon(weapon))
                {
                    continue;
                }

                // The NPC volley preserves mounted-gun order. Each gun owns
                // one authoritative spend attempt immediately before its
                // projectile is created; an empty pool blocks only that gun.
                if (npc.WeaponEnergy == null || !npc.WeaponEnergy.TrySpend(weapon.EnergyCost))
                {
                    continue;
                }

                Vector3 direction = baseDirection;
                direction.X += (float)(_random.NextDouble() * 2d - 1d) * AccuracySpread;
                direction.Y += (float)(_random.NextDouble() * 2d - 1d) * AccuracySpread;
                direction.Z += (float)(_random.NextDouble() * 2d - 1d) * AccuracySpread;
                direction = Vector3.Normalize(direction);

                float sideOffset = (weaponIndex - ((profile.Weapons.Count - 1) * 0.5f)) * 3f;
                float life = MathHelper.Clamp(weapon.Range / weapon.ProjectileSpeed, 0.1f, 10f);
                NpcProjectile projectile = new()
                {
                    Position = npc.Position + npc.Forward * 15f + npc.Right * sideOffset,
                    Velocity = direction * weapon.ProjectileSpeed + npc.Velocity * 0.3f,
                    Life = life,
                    MaxLife = life,
                    Color = GetProjectileColor(weapon.WeaponType),
                    Damage = weapon.Damage,
                    WeaponId = weapon.Id,
                    Owner = npc,
                    Target = target
                };

                _projectiles.Add(projectile);
                fired++;
                NpcWeaponFired?.Invoke(npc, target, weapon.Id, weapon);
            }

            return fired;
        }

        private NpcWeaponProfile GetWeaponProfile(NpcShip npc)
        {
            if (_weaponProfiles.TryGetValue(npc, out NpcWeaponProfile cached))
            {
                return cached;
            }

            NpcWeaponProfile profile = new();
            foreach (WeaponEquipmentDefinition mountedWeapon in npc.Loadout?.GetMountedGuns() ?? Array.Empty<WeaponEquipmentDefinition>())
            {
                EquipmentDefinition canonical = EquipmentCatalog.GetById(mountedWeapon?.Id);
                if (canonical is WeaponEquipmentDefinition canonicalWeapon &&
                    NpcEquipmentLoadoutFactory.IsValidNpcWeapon(canonicalWeapon))
                {
                    profile.Weapons.Add(canonicalWeapon);
                }
            }

            profile.FiringRange = NpcEquipmentLoadoutFactory.GetFiringRange(npc.Loadout);
            profile.RefireRate = NpcEquipmentLoadoutFactory.GetRefireRate(npc.Loadout);
            if (profile.RefireRate <= 0f)
            {
                profile.RefireRate = 0.1f;
            }

            _weaponProfiles[npc] = profile;
            return profile;
        }

        private static Color GetProjectileColor(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.LaserBolt => Color.LightGoldenrodYellow,
                WeaponType.BlueDonut => Color.CornflowerBlue,
                WeaponType.QuickBlaster => Color.LimeGreen,
                WeaponType.Fireball => Color.OrangeRed,
                _ => Color.Red
            };
        }

        private void ApplyNpcDamage(NpcShip attacker, NpcShip target, float damage)
        {
            if (attacker == null || target == null || target.IsDestroyed ||
                float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
            {
                return;
            }

            NpcShipDamaged?.Invoke(attacker, target, damage);

            target.ApplyCombatDamage(damage, NpcDestructionSource.Npc, hostile: true);
        }

        /// <summary>
        /// Check projectile collisions with a specific position/radius (for
        /// hit impact effects). Targeted NPC projectiles are removed by the
        /// authoritative target path above before they reach this helper.
        /// </summary>
        public List<HitInfo> CheckCollisions(Vector3 targetPos, float targetRadius, HullIntegrity hull, ShieldSystem shields)
        {
            List<HitInfo> hits = new();
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                NpcProjectile projectile = _projectiles[i];
                float distance = Vector3.Distance(projectile.Position, targetPos);
                if (distance < targetRadius + 5f)
                {
                    hits.Add(new HitInfo
                    {
                        Position = projectile.Position,
                        Direction = Vector3.Normalize(projectile.Velocity),
                        WeaponColor = projectile.Color,
                        Damage = projectile.Damage
                    });

                    _projectiles.RemoveAt(i);
                }
            }

            return hits;
        }

        /// <summary>
        /// Draw NPC projectiles.
        /// </summary>
        public void Draw(Camera camera)
        {
            if (_projectiles.Count == 0 || camera == null || _graphicsDevice == null || _effect == null)
            {
                return;
            }

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            BlendState oldBlend = _graphicsDevice.BlendState;
            DepthStencilState oldDepth = _graphicsDevice.DepthStencilState;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            Matrix viewInverse = Matrix.Invert(camera.View);
            Vector3 cameraRight = new(viewInverse.M11, viewInverse.M12, viewInverse.M13);
            Vector3 cameraUp = new(viewInverse.M21, viewInverse.M22, viewInverse.M23);

            foreach (NpcProjectile projectile in _projectiles)
            {
                float lifeRatio = projectile.Life / projectile.MaxLife;
                float size = 3f;
                Vector3 right = cameraRight * size;
                Vector3 up = cameraUp * size;
                Color color = projectile.Color * lifeRatio;

                VertexPositionColor[] vertices =
                {
                    new(projectile.Position - right - up, color),
                    new(projectile.Position + right - up, color),
                    new(projectile.Position + right + up, color),
                    new(projectile.Position - right - up, color),
                    new(projectile.Position + right + up, color),
                    new(projectile.Position - right + up, color)
                };

                _effect.World = Matrix.Identity;
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 2);
                }
            }

            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
        }

        /// <summary>
        /// Remove cooldown and cached weapon state for a retired NPC.
        /// </summary>
        public void RemoveNpc(NpcShip npc)
        {
            _fireCooldowns.Remove(npc);
            _weaponProfiles.Remove(npc);
        }
    }
}
