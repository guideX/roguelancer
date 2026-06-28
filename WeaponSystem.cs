using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Weapon types available
    /// </summary>
    public enum WeaponType
    {
        BlueDonut,      // Original ring-shaped projectile
        Fireball,       // Small explosive fireball
        QuickBlaster,   // Fast short-lived energy bolt
        ChargeBeam,     // Continuous beam that charges up
        LaserBolt,      // Star Wars-style laser bolt
        Wunderwafffle   // Chain lightning beam that drains energy and hull
    }

    /// <summary>
    /// Information about a weapon hit
    /// </summary>
    public struct HitInfo
    {
        public Vector3 Position;
        public Vector3 Direction;
        public Color WeaponColor;
        public WeaponType WeaponType;
        public float Damage;
    }

    /// <summary>
    /// Manages weapon projectiles (blaster bolts) and rendering
    /// </summary>
    public class WeaponSystem
    {
        private class Projectile
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Size;
            public float Damage;
            public WeaponType Type;
        }
        
        private class MuzzleFlash
        {
            public Vector3 Position;
            public float Life;
            public float MaxLife;
            public float Size;
        }

        private class ChargeBeam
        {
            public Vector3 StartPosition;
            public Vector3 Direction;
            public float ChargeTime;
            public float MaxChargeTime;
            public float FiringTime;
            public float MaxFiringTime;
            public bool IsFiring;
            public Color BeamColor;
        }
        
        private class LightningBeam
        {
            public Vector3 StartPosition;
            public List<Vector3> HitTargets;
            public float Life;
            public float MaxLife;
            public Color BeamColor;
            public List<Vector3> ChainPositions; // For visual chain lightning effect
            public float AnimationTime; // For crackling animation
            
            public LightningBeam()
            {
                HitTargets = new List<Vector3>();
                ChainPositions = new List<Vector3>();
            }
        }
        
        private readonly List<Projectile> _projectiles = new List<Projectile>(256);
        private readonly List<MuzzleFlash> _muzzleFlashes = new List<MuzzleFlash>(8);
        private readonly List<ChargeBeam> _chargeBeams = new List<ChargeBeam>(2);
        private readonly List<LightningBeam> _lightningBeams = new List<LightningBeam>(4);
        
        // Reference to all ships for chain lightning targeting
        private List<object> _allShips = new List<object>();
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Dictionary<WeaponType, Texture2D> _textures;
        
        // Current weapon type
        public WeaponType CurrentWeapon { get; set; } = WeaponType.BlueDonut;
        
        // Weapon stats by type
        private readonly Dictionary<WeaponType, WeaponStats> _weaponStats;
        private WeaponType? _weaponStatsOverrideType;
        private WeaponStats _weaponStatsOverride;

        // Refire tracking
        private float _refireTimer = 0f; // Time until next shot can be fired
        private bool _isFiring = false; // Track if weapon is actively firing
        
        // Energy system reference
        private ShipEnergy _energy;

        public class WeaponStats
        {
            public float WeaponDamage;
            public float Speed;
            public float Life;
            public float Range;
            public float Size;
            public Color Color;
            public float MuzzleFlashSize;
            public float RefireRate; // Time between shots in seconds
            public float EnergyCost; // Energy required per shot
        }
        
        // Batching buffers
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        
        public WeaponSystem(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };

            // Create textures for each weapon type
            _textures = new Dictionary<WeaponType, Texture2D>
            {
                { WeaponType.BlueDonut, CreateDonutTexture(graphicsDevice, 16) },
                { WeaponType.Fireball, CreateFireballTexture(graphicsDevice, 16) },
                { WeaponType.QuickBlaster, CreateBlasterTexture(graphicsDevice, 16) },
                { WeaponType.ChargeBeam, CreateBeamTexture(graphicsDevice, 16) },
                { WeaponType.LaserBolt, CreateLaserBoltTexture(graphicsDevice, 32) },
                { WeaponType.Wunderwafffle, CreateLightningTexture(graphicsDevice, 16) }
            };

            // Define stats for each weapon type
            _weaponStats = new Dictionary<WeaponType, WeaponStats>
            {
                {
                    WeaponType.BlueDonut,
                    new WeaponStats
                    {
                        WeaponDamage = 20f,
                        Speed = 1200f,
                        Life = 4.0f,
                        Size = 25f,
                        Color = new Color(100, 200, 255),
                        MuzzleFlashSize = 25f,
                        RefireRate = 0.25f, // 4 shots per second
                        EnergyCost = 33f, // Medium energy cost
                        Range = 4800f
                    }
                },
                {
                    WeaponType.Fireball,
                    new WeaponStats
                    {
                        WeaponDamage = 30f,
                        Speed = 600f,
                        Life = 2.5f,
                        Size = 35f,
                        Color = new Color(255, 150, 50),
                        MuzzleFlashSize = 40f,
                        RefireRate = 0.5f, // 2 shots per second (slower)
                        EnergyCost = 50f, // High energy cost (powerful weapon)
                        Range = 1500f
                    }
                },
                {
                    WeaponType.QuickBlaster,
                    new WeaponStats
                    {
                        WeaponDamage = 7f,
                        Speed = 1500f, // Much faster
                        Life = 1.0f,   // Short lived
                        Size = 25f,
                        Color = new Color(50, 255, 100), // Green energy
                        MuzzleFlashSize = 20f,
                        RefireRate = 0.1f, // 10 shots per second (very fast!)
                        EnergyCost = 15f, // Low energy cost (fast but weak)
                        Range = 1500f
                    }
                },
                {
                    WeaponType.ChargeBeam,
                    new WeaponStats
                    {
                        WeaponDamage = 10f, // Damage per frame while firing
                        Speed = 0f, // Beams don't move
                        Life = 1.5f, // Longer beam duration (was 0.5f)
                        Size = 120f, // Even thicker beam (was 80f)
                        Color = new Color(255, 255, 100), // Bright yellow beam (was purple)
                        MuzzleFlashSize = 60f,
                        RefireRate = 0f, // Not used for charge beam
                        EnergyCost = 60f, // High energy cost per second of firing
                        Range = 0f
                    }
                },
                {
                    WeaponType.LaserBolt,
                    new WeaponStats
                    {
                        WeaponDamage = 40f,
                        Speed = 2000f, // Very fast
                        Life = 1.2f,   // Medium range
                        Size = 40f,    // This will be stretched
                        Color = new Color(255, 255, 220), // Yellowish-white
                        MuzzleFlashSize = 25f,
                        RefireRate = 0.15f, // Fast refire
                        EnergyCost = 10f,
                        Range = 2400f
                    }
                },
                {
                    WeaponType.Wunderwafffle,
                    new WeaponStats
                    {
                        WeaponDamage = 5f, // Damage per frame to hull
                        Speed = 0f, // Instant beam
                        Life = 8.0f, // Electrocution duration
                        Size = 8f, // Thin crackling beam
                        Color = new Color(100, 150, 255), // Electric blue
                        MuzzleFlashSize = 40f,
                        RefireRate = 10f, // Long cooldown (10 seconds)
                        EnergyCost = 100f, // High initial energy cost
                        Range = 0f
                    }
                }
            };
        }
        
        public WeaponStats GetCurrentWeaponStats()
        {
            return CloneWeaponStats(GetEffectiveWeaponStats(CurrentWeapon));
        }

        public WeaponStats GetWeaponStats(WeaponType weaponType)
        {
            return CloneWeaponStats(GetBaseWeaponStats(weaponType));
        }

        public void SetWeaponProfileOverride(WeaponType weaponType, WeaponStats stats)
        {
            if (!Enum.IsDefined(typeof(WeaponType), weaponType) || stats == null)
            {
                ClearWeaponProfileOverride();
                return;
            }

            _weaponStatsOverrideType = weaponType;
            _weaponStatsOverride = CloneWeaponStats(stats);
        }

        public void ClearWeaponProfileOverride()
        {
            _weaponStatsOverrideType = null;
            _weaponStatsOverride = null;
        }

        private WeaponStats GetEffectiveWeaponStats(WeaponType weaponType)
        {
            if (_weaponStatsOverrideType.HasValue && _weaponStatsOverrideType.Value == weaponType && _weaponStatsOverride != null)
            {
                return _weaponStatsOverride;
            }

            return GetBaseWeaponStats(weaponType);
        }

        private WeaponStats GetBaseWeaponStats(WeaponType weaponType)
        {
            return _weaponStats.TryGetValue(weaponType, out var stats) ? stats : null;
        }

        private static WeaponStats CloneWeaponStats(WeaponStats stats)
        {
            if (stats == null)
            {
                return null;
            }

            return new WeaponStats
            {
                WeaponDamage = stats.WeaponDamage,
                Speed = stats.Speed,
                Life = stats.Life,
                Range = stats.Range,
                Size = stats.Size,
                Color = stats.Color,
                MuzzleFlashSize = stats.MuzzleFlashSize,
                RefireRate = stats.RefireRate,
                EnergyCost = stats.EnergyCost
            };
        }

        private Texture2D CreateDonutTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxDist = size / 4f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    
                    // Create DONUT/RING shape
                    float ringOuterRadius = 0.7f;
                    float ringInnerRadius = 0.4f;
                    float ringThickness = 0.3f;
                    
                    float a = 0f;
                    
                    if (d >= ringInnerRadius && d <= ringOuterRadius)
                    {
                        float distFromCenter = Math.Abs(d - (ringInnerRadius + ringOuterRadius) / 2f);
                        float normalizedDist = distFromCenter / (ringThickness / 2f);
                        a = 1f - MathHelper.Clamp(normalizedDist, 0f, 1f);
                        a = (float)Math.Pow(a, 4);
                    }
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }

        private Texture2D CreateLaserBoltTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float coreThickness = size / 8f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distY = Math.Abs(y - center.Y);
                    float a = 0f;

                    if (distY < coreThickness)
                    {
                        a = 1.0f; // Solid core
                    }
                    else
                    {
                        float falloff = (distY - coreThickness) / (size / 2f - coreThickness);
                        a = 1.0f - falloff;
                        a = (float)Math.Pow(a, 2);
                    }

                    // Fade out at the horizontal ends to prevent sharp cuts
                    float edgeFade = 1.0f;
                    float fadeZone = size * 0.1f;
                    if (x < fadeZone) edgeFade = x / fadeZone;
                    else if (x > size - fadeZone) edgeFade = (size - x) / fadeZone;

                    a *= edgeFade;

                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }

        private Texture2D CreateFireballTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxDist = size / 2f;
            
            Random rand = new Random(42); // Consistent seed for same texture each time
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    
                    // Fireball: bright hot center, flickering edges
                    float a = 1f - d;
                    a = (float)Math.Pow(a, 1.5); // Softer falloff than blaster
                    
                    // Add noise for organic fire look
                    float noise = (float)rand.NextDouble() * 0.3f;
                    a = MathHelper.Clamp(a + noise - 0.15f, 0f, 1f);
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetData(data);
            return tex;
        }

        private Texture2D CreateBlasterTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxDist = size / 2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    
                    // Tight, bright energy bolt
                    float a = 1f - d;
                    a = (float)Math.Pow(a, 4); // Very sharp falloff
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }

        private Texture2D CreateBeamTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxDist = size / 2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    
                    // Solid bright core with soft edges for continuous laser beam
                    float a;
                    if (d < 0.3f)
                    {
                        // Very bright center - solid core
                        a = 1.0f;
                    }
                    else if (d < 0.7f)
                    {
                        // Medium brightness - smooth transition
                        float t = (d - 0.3f) / 0.4f;
                        a = 1.0f - (t * 0.5f); // Goes from 1.0 to 0.5
                    }
                    else
                    {
                        // Soft outer glow
                        float t = (d - 0.7f) / 0.3f;
                        a = 0.5f * (1.0f - t); // Goes from 0.5 to 0
                    }
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }
        
        private Texture2D CreateLightningTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxDist = size / 2f;
            Random rand = new Random(123);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    
                    // Create jagged lightning bolt texture
                    float noise = (float)rand.NextDouble() * 0.5f;
                    float a = 0f;
                    
                    if (d < 0.2f + noise * 0.1f)
                    {
                        // Very bright crackling core
                        a = 1.0f;
                    }
                    else if (d < 0.5f)
                    {
                        // Electric glow
                        a = (0.5f - d) / 0.3f;
                        a = (float)Math.Pow(a, 1.5);
                    }
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }
        
        /// <summary>
        /// Start firing (call this when mouse button is pressed/held)
        /// </summary>
        public void StartFiring(Vector3 origin, Vector3 direction, Vector3 shipVelocity)
        {
            _isFiring = true;
            WeaponStats stats = GetCurrentWeaponStats() ?? GetBaseWeaponStats(CurrentWeapon);
            
            // Special handling for Wunderwafffle (chain lightning)
            if (CurrentWeapon == WeaponType.Wunderwafffle)
            {
                // Only fire if refire timer allows it
                if (_refireTimer <= 0f)
                {
                    // Check if we have enough energy
                    if (_energy != null && !_energy.TryConsume(stats.EnergyCost))
                    {
                        return; // Not enough energy
                    }
                    
                    // Create new lightning beam
                    LightningBeam lightning = new LightningBeam
                    {
                        StartPosition = origin,
                        Life = 0f,
                        MaxLife = stats.Life,
                        BeamColor = stats.Color,
                        AnimationTime = 0f
                    };
                    
                    _lightningBeams.Add(lightning);
                    _refireTimer = stats.RefireRate;
                    
                    Console.WriteLine($"⚡ WUNDERWAFFFLE FIRED! Chain lightning activated!");
                }
                return;
            }
            
            // Special handling for charge beam
            if (CurrentWeapon == WeaponType.ChargeBeam)
            {
                // Start charging a new beam or continue charging/firing existing one
                ChargeBeam beam = _chargeBeams.Count > 0 ? _chargeBeams[0] : null;
                if (beam == null)
                {
                    beam = new ChargeBeam
                    {
                        StartPosition = origin,
                        Direction = direction,
                        ChargeTime = 0f,
                        MaxChargeTime = 1.5f, // 1.5 seconds to charge
                        FiringTime = 0f,
                        MaxFiringTime = float.MaxValue, // Fire indefinitely while held
                        IsFiring = false,
                        BeamColor = stats?.Color ?? Color.White
                    };
                    _chargeBeams.Add(beam);
                    Console.WriteLine($"⚡ CHARGE BEAM: Started charging...");
                }
                else
                {
                    // Update beam position and direction while charging or firing
                    beam.StartPosition = origin;
                    beam.Direction = direction;
                }
                return; // Don't create projectile
            }

            // Regular projectile weapons: Check if refire timer allows firing AND we have energy
            if (_refireTimer <= 0f)
            {
                // Check if we have enough energy
                if (_energy != null && !_energy.TryConsume(stats.EnergyCost))
                {
                    // Not enough energy - don't fire
                    return;
                }
                
                FireProjectile(origin, direction, shipVelocity);
                _refireTimer = stats.RefireRate;
            }
        }

        /// <summary>
        /// Stop firing (call this when mouse button is released)
        /// </summary>
        public void StopFiring()
        {
            _isFiring = false;
            
            // Release charge beam if active
            if (CurrentWeapon == WeaponType.ChargeBeam && _chargeBeams.Count > 0)
            {
                Console.WriteLine($"⚡ CHARGE BEAM: Released! Beam stopped.");
                _chargeBeams.Clear();
            }
        }

        /// <summary>
        /// Legacy Fire method - now calls StartFiring for compatibility
        /// </summary>
        public void Fire(Vector3 origin, Vector3 direction, Vector3 shipVelocity)
        {
            StartFiring(origin, direction, shipVelocity);
        }

        /// <summary>
        /// Actually fire a projectile (internal method)
        /// </summary>
        private void FireProjectile(Vector3 origin, Vector3 direction, Vector3 shipVelocity)
        {
            WeaponStats stats = GetCurrentWeaponStats() ?? GetBaseWeaponStats(CurrentWeapon);
            if (stats == null)
            {
                Console.WriteLine($"[WEAPONS] Unable to fire {CurrentWeapon}: no weapon stats available.");
                return;
            }

            // Regular projectile weapons: Create DUAL projectiles from left and right
            // Calculate perpendicular offset vector (perpendicular to firing direction)
            Vector3 up = Vector3.Up;
            Vector3 right = Vector3.Cross(direction, up);
            
            // If direction is parallel to up, use a different reference
            if (right.LengthSquared() < 0.0001f)
            {
                right = Vector3.Cross(direction, Vector3.Forward);
            }
            right.Normalize();
            
            // Offset distance between the two shots (adjustable for visual effect)
            float spreadDistance = 6f; // Distance between left and right projectiles
            
            // Create two projectiles - one offset left, one offset right
            Vector3[] offsets = new[]
            {
                -right * spreadDistance, // Left projectile
                right * spreadDistance   // Right projectile
            };
            
            foreach (var offset in offsets)
            {
                Projectile projectile = new Projectile
                {
                    Position = origin + offset,
                    Velocity = direction * stats.Speed + shipVelocity,
                    Life = 0f,
                    MaxLife = stats.Range > 0f && stats.Speed > 0f ? stats.Range / stats.Speed : stats.Life,
                    Color = stats.Color,
                    Size = stats.Size,
                    Damage = stats.WeaponDamage,
                    Type = CurrentWeapon
                };
                
                _projectiles.Add(projectile);
            }
            
            // Add muzzle flash at center position
            MuzzleFlash flash = new MuzzleFlash
            {
                Position = origin,
                Life = 0f,
                MaxLife = 0.15f,
                Size = stats.MuzzleFlashSize
            };
            _muzzleFlashes.Add(flash);
            
            Console.WriteLine($"🔫 {CurrentWeapon} DUAL FIRED! Speed: {stats.Speed:F0}, RefireRate: {stats.RefireRate:F2}s");
        }

        public void ReleaseCharge(Vector3 origin, Vector3 direction)
        {
            StopFiring();
        }

        public bool IsCharging()
        {
            return CurrentWeapon == WeaponType.ChargeBeam && _chargeBeams.Count > 0 && !_chargeBeams[0].IsFiring;
        }

        public bool IsChargingOrFiring()
        {
            return CurrentWeapon == WeaponType.ChargeBeam && _chargeBeams.Count > 0;
        }

        public float GetChargeProgress()
        {
            if (_chargeBeams.Count == 0) return 0f;
            ChargeBeam beam = _chargeBeams[0];
            return MathHelper.Clamp(beam.ChargeTime / beam.MaxChargeTime, 0f, 1f);
        }

        /// <summary>
        /// Check collisions between projectiles and a ship, apply damage on hit.
        /// If shields are provided, damage hits shields first; remaining damage goes to hull.
        /// </summary>
        /// <returns>List of hits that occurred</returns>
        public List<HitInfo> CheckCollisions(Vector3 shipPosition, float shipRadius, HullIntegrity hull, ShieldSystem shields = null)
        {
            List<HitInfo> hits = new List<HitInfo>();
            
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = _projectiles[i];
                float distance = Vector3.Distance(p.Position, shipPosition);
                
                if (distance < shipRadius)
                {
                    // Hit! Apply damage based on weapon type
                    float damage = p.Damage > 0f
                        ? p.Damage
                        : (_weaponStats.TryGetValue(p.Type, out var stats) ? stats.WeaponDamage : 0f);
                    float hullBefore = hull.CurrentHull;
                    
                    // Route damage through shields first
                    float hullDamage = damage;
                    if (shields != null)
                    {
                        hullDamage = shields.AbsorbDamage(damage);
                    }
                    
                    // Apply remaining damage to hull
                    if (hullDamage > 0f)
                    {
                        hull.TakeDamage(hullDamage);
                    }
                    float hullAfter = hull.CurrentHull;
                    
                    // Record hit info
                    HitInfo hitInfo = new HitInfo
                    {
                        Position = p.Position,
                        Direction = Vector3.Normalize(p.Velocity),
                        WeaponColor = p.Color,
                        WeaponType = p.Type,
                        Damage = damage
                    };
                    hits.Add(hitInfo);
                    
                    // Remove projectile
                    _projectiles.RemoveAt(i);
                    
                    Console.WriteLine($"💥 HIT! Weapon: {p.Type}, Damage: {damage:F1}, Hull: {hullBefore:F1} → {hullAfter:F1} ({hull.HullPercentage * 100:F0}%)");
                }
            }
            
            return hits;
        }
        
        /// <summary>
        /// Check lightning beam collisions and apply energy/hull drain with chain lightning
        /// </summary>
        public void CheckLightningCollisions(float deltaTime)
        {
            if (_lightningBeams.Count == 0 || _allShips.Count == 0) return;
            
            WeaponStats stats = _weaponStats[WeaponType.Wunderwafffle];
            float chainRange = 500f; // Range to chain to nearby ships
            int maxChainTargets = 3; // Maximum number of chain targets
            
            foreach (LightningBeam lightning in _lightningBeams)
            {
                // Clear previous chain positions
                lightning.ChainPositions.Clear();
                lightning.HitTargets.Clear();
                
                // Find all ships within range and build chain
                List<object> potentialTargets = new List<object>();
                
                foreach (var ship in _allShips)
                {
                    // Check if it's an NPC ship (we'll need to access Position and other properties)
                    System.Reflection.PropertyInfo positionProp = ship.GetType().GetProperty("Position");
                    System.Reflection.PropertyInfo isDestroyedProp = ship.GetType().GetProperty("IsDestroyed");
                    
                    if (positionProp != null && isDestroyedProp != null)
                    {
                        Vector3 shipPos = (Vector3)positionProp.GetValue(ship);
                        bool isDestroyed = (bool)isDestroyedProp.GetValue(ship);
                        
                        if (!isDestroyed)
                        {
                            float distance = Vector3.Distance(lightning.StartPosition, shipPos);
                            if (distance < 2000f) // Initial targeting range
                            {
                                potentialTargets.Add(ship);
                            }
                        }
                    }
                }
                
                if (potentialTargets.Count == 0) continue;
                
                // Sort by distance from weapon origin
                potentialTargets.Sort((a, b) =>
                {
                    Vector3 posA = (Vector3)a.GetType().GetProperty("Position").GetValue(a);
                    Vector3 posB = (Vector3)b.GetType().GetProperty("Position").GetValue(b);
                    float distA = Vector3.Distance(lightning.StartPosition, posA);
                    float distB = Vector3.Distance(lightning.StartPosition, posB);
                    return distA.CompareTo(distB);
                });
                
                // Primary target
                var primaryTarget = potentialTargets[0];
                Vector3 primaryPos = (Vector3)primaryTarget.GetType().GetProperty("Position").GetValue(primaryTarget);
                lightning.HitTargets.Add(primaryPos);
                lightning.ChainPositions.Add(primaryPos);
                
                // Apply damage and energy drain to primary
                ApplyLightningDamage(primaryTarget, stats.WeaponDamage * deltaTime, deltaTime);
                
                // Chain to nearby ships
                int chainsApplied = 0;
                Vector3 lastChainPos = primaryPos;
                
                for (int i = 1; i < potentialTargets.Count && chainsApplied < maxChainTargets; i++)
                {
                    var chainTarget = potentialTargets[i];
                    Vector3 chainPos = (Vector3)chainTarget.GetType().GetProperty("Position").GetValue(chainTarget);
                    
                    float chainDist = Vector3.Distance(lastChainPos, chainPos);
                    if (chainDist < chainRange)
                    {
                        lightning.HitTargets.Add(chainPos);
                        lightning.ChainPositions.Add(chainPos);
                        
                        // Reduced damage for chain targets (50% of primary)
                        ApplyLightningDamage(chainTarget, stats.WeaponDamage * 0.5f * deltaTime, deltaTime * 0.5f);
                        
                        lastChainPos = chainPos;
                        chainsApplied++;
                        
                        Console.WriteLine($"⚡ CHAIN LIGHTNING! Target {i + 1} hit!");
                    }
                }
            }
        }
        
        /// <summary>
        /// Apply lightning damage (hull and energy drain) to a target.
        /// Routes damage through shields first if available.
        /// </summary>
        private void ApplyLightningDamage(object target, float hullDamage, float energyDrainAmount)
        {
            // Get hull, energy, and shield properties via reflection
            System.Reflection.PropertyInfo hullProp = target.GetType().GetProperty("Hull");
            System.Reflection.PropertyInfo energyProp = target.GetType().GetProperty("Energy");
            System.Reflection.PropertyInfo shieldsProp = target.GetType().GetProperty("Shields");

            // Route damage through shields first
            float remainingDamage = hullDamage;
            if (shieldsProp != null)
            {
                var shields = shieldsProp.GetValue(target);
                if (shields != null)
                {
                    var absorbMethod = shields.GetType().GetMethod("AbsorbDamage");
                    if (absorbMethod != null)
                    {
                        remainingDamage = (float)absorbMethod.Invoke(shields, new object[] { hullDamage });
                    }
                }
            }
            
            if (hullProp != null && remainingDamage > 0f)
            {
                var hull = hullProp.GetValue(target);
                if (hull != null)
                {
                    var takeDamageMethod = hull.GetType().GetMethod("TakeDamage");
                    if (takeDamageMethod != null)
                    {
                        takeDamageMethod.Invoke(hull, new object[] { remainingDamage });
                    }
                }
            }
            
            if (energyProp != null)
            {
                var energy = energyProp.GetValue(target);
                if (energy != null)
                {
                    // Drain energy (try to consume but don't check return value)
                    var tryConsumeMethod = energy.GetType().GetMethod("TryConsume");
                    if (tryConsumeMethod != null)
                    {
                        tryConsumeMethod.Invoke(energy, new object[] { energyDrainAmount * 20f }); // Drain 20x the rate
                    }
                }
            }
        }
        
        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update refire timer
            if (_refireTimer > 0f)
            {
                _refireTimer -= dt;
            }
            
            // Update projectiles
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = _projectiles[i];
                p.Life += dt;
                
                if (p.Life >= p.MaxLife)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }
                
                p.Position += p.Velocity * dt;
            }
            
            // Update muzzle flashes
            for (int i = _muzzleFlashes.Count - 1; i >= 0; i--)
            {
                MuzzleFlash flash = _muzzleFlashes[i];
                flash.Life += dt;
                
                if (flash.Life >= flash.MaxLife)
                {
                    _muzzleFlashes.RemoveAt(i);
                }
            }

            // Update charge beams
            for (int i = _chargeBeams.Count - 1; i >= 0; i--)
            {
                ChargeBeam beam = _chargeBeams[i];

                if (!beam.IsFiring)
                {
                    // Charging up
                    beam.ChargeTime += dt;
                    
                    // Auto-fire when fully charged!
                    if (beam.ChargeTime >= beam.MaxChargeTime)
                    {
                        beam.IsFiring = true;
                        beam.FiringTime = 0f;
                        Console.WriteLine($"⚡ CHARGE BEAM: FULLY CHARGED! AUTO-FIRING!");
                    }
                }
                else
                {
                    // Firing - consume energy continuously
                    WeaponStats stats = _weaponStats[WeaponType.ChargeBeam];
                    float energyNeeded = stats.EnergyCost * dt;
                    
                    if (_energy != null && !_energy.TryConsume(energyNeeded))
                    {
                        // Out of energy - stop firing
                        Console.WriteLine($"⚡ CHARGE BEAM: Out of energy! Beam stopped.");
                        _chargeBeams.RemoveAt(i);
                        continue;
                    }
                    
                    beam.FiringTime += dt;
                    // No timeout - fires until user releases mouse button or energy runs out
                }
            }
            
            // Update lightning beams (Wunderwafffle)
            for (int i = _lightningBeams.Count - 1; i >= 0; i--)
            {
                LightningBeam lightning = _lightningBeams[i];
                lightning.Life += dt;
                lightning.AnimationTime += dt;
                
                if (lightning.Life >= lightning.MaxLife)
                {
                    _lightningBeams.RemoveAt(i);
                    Console.WriteLine($"⚡ WUNDERWAFFFLE: Lightning dissipated");
                    continue;
                }
            }
        }
        
        public void Draw(Camera camera)
        {
            DrawProjectiles(camera);
            DrawMuzzleFlashes(camera);
            DrawChargeBeams(camera);
            DrawLightningBeams(camera);
        }
        
        private void DrawProjectiles(Camera camera)
        {
            int count = _projectiles.Count;
            if (count == 0) return;
            
            // Ensure buffers sized
            int requiredVertices = count * 4;
            int requiredIndices = count * 6;
            if (_vertexBuffer.Length < requiredVertices) _vertexBuffer = new VertexPositionColorTexture[requiredVertices];
            if (_indexBuffer.Length < requiredIndices) _indexBuffer = new short[requiredIndices];
            
            Vector3 camPos = camera.Position;
            
            // Build billboards
            for (int i = 0; i < count; i++)
            {
                Projectile p = _projectiles[i];
                
                float t = p.Life / p.MaxLife;
                float alpha = (1f - t * 0.7f);
                Color color = p.Color * alpha;
                
                // Billboard facing camera
                Vector3 forward = camPos - p.Position;
                if (forward.LengthSquared() < 0.00001f) forward = Vector3.Forward;
                forward.Normalize();
                
                Vector3 right = Vector3.Cross(Vector3.Up, forward);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right;
                else right.Normalize();
                
                Vector3 up = Vector3.Cross(forward, right);
                
                float size = p.Size;

                // Fireball specific: pulsing effect
                if (p.Type == WeaponType.Fireball)
                {
                    float pulse = (float)Math.Sin(p.Life * 20f) * 0.15f + 0.85f;
                    size *= pulse;
                }
                
                // Build quad
                int vBase = i * 4;
                int iBase = i * 6;

                if (p.Type == WeaponType.LaserBolt)
                {
                    // Laser bolt specific: stretch effect
                    float stretchFactor = 8f; // Make it long and thin
                    float pSize = size * 0.5f; // Make it thinner
                    Vector3 pRight = Vector3.Normalize(p.Velocity);
                    Vector3 pUp = Vector3.Cross(pRight, forward);
                    pRight *= size * stretchFactor;

                    _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(p.Position - pRight - pUp * pSize, color, new Vector2(0, 1));
                    _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(p.Position + pRight - pUp * pSize, color, new Vector2(1, 1));
                    _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(p.Position - pRight + pUp * pSize, color, new Vector2(0, 0));
                    _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(p.Position + pRight + pUp * pSize, color, new Vector2(1, 0));
                }
                else
                {
                    // Default billboard for other projectiles
                    _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(p.Position - right * size - up * size, color, new Vector2(0, 1));
                    _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(p.Position + right * size - up * size, color, new Vector2(1, 1));
                    _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(p.Position - right * size + up * size, color, new Vector2(0, 0));
                    _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(p.Position + right * size + up * size, color, new Vector2(1, 0));
                }
                
                _indexBuffer[iBase + 0] = (short)(vBase + 0);
                _indexBuffer[iBase + 1] = (short)(vBase + 1);
                _indexBuffer[iBase + 2] = (short)(vBase + 2);
                _indexBuffer[iBase + 3] = (short)(vBase + 2);
                _indexBuffer[iBase + 4] = (short)(vBase + 1);
                _indexBuffer[iBase + 5] = (short)(vBase + 3);
            }
            
            // Save render states
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];
            
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;

            // Group by texture type for batching
            foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
            {
                if (weaponType == WeaponType.ChargeBeam) continue; // Beams drawn separately

                var projectilesOfType = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    if (_projectiles[i].Type == weaponType)
                        projectilesOfType.Add(i);
                }

                if (projectilesOfType.Count == 0) continue;

                _effect.Texture = _textures[weaponType];
                
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertexBuffer, 0, count * 4,
                        _indexBuffer, 0, count * 2
                    );
                }
            }
            
            // Restore render states
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }

        private void DrawChargeBeams(Camera camera)
        {
            if (_chargeBeams.Count == 0) return;

            foreach (ChargeBeam beam in _chargeBeams)
            {
                if (!beam.IsFiring) continue; // Only draw when firing

                // Beam parameters - MUCH LONGER and THICKER for powerful laser
                float beamLength = 10000f; // Very long beam (was 5000f)
                float beamThickness = _weaponStats[WeaponType.ChargeBeam].Size * 1.5f; // Even thicker
                Vector3 endPosition = beam.StartPosition + beam.Direction * beamLength;

                // Intense pulse effect for powerful laser
                float pulse = (float)Math.Sin(beam.FiringTime * 40f) * 0.15f + 0.85f;
                Color beamColor = beam.BeamColor * pulse;

                // Draw beam as CONTINUOUS thick laser with multiple layers
                DrawContinuousBeam(camera, beam.StartPosition, endPosition, beamThickness, beamColor);
            }
        }

        private void DrawContinuousBeam(Camera camera, Vector3 start, Vector3 end, float thickness, Color color)
        {
            Vector3 camPos = camera.Position;
            Vector3 beamDirection = end - start;
            float beamLength = beamDirection.Length();
            beamDirection.Normalize();

            // Draw beam in 3 layers for depth: Core (bright), Middle (medium), Outer (soft glow)
            DrawBeamLayer(camera, start, end, beamDirection, thickness * 0.3f, color * 1.5f, 20); // Bright core
            DrawBeamLayer(camera, start, end, beamDirection, thickness * 0.7f, color, 15); // Middle layer
            DrawBeamLayer(camera, start, end, beamDirection, thickness, color * 0.4f, 10); // Outer glow
        }

        private void DrawBeamLayer(Camera camera, Vector3 start, Vector3 end, Vector3 beamDirection, float thickness, Color color, int segments)
        {
            Vector3 camPos = camera.Position;
            float beamLength = Vector3.Distance(start, end);
            float segmentLength = beamLength / segments;

            // Ensure buffers sized
            int requiredVertices = segments * 4;
            int requiredIndices = segments * 6;
            if (_vertexBuffer.Length < requiredVertices) _vertexBuffer = new VertexPositionColorTexture[requiredVertices];
            if (_indexBuffer.Length < requiredIndices) _indexBuffer = new short[requiredIndices];

            // Create continuous beam segments
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector3 segmentStart = start + beamDirection * (t * beamLength);
                Vector3 segmentEnd = start + beamDirection * ((t + 1f / segments) * beamLength);
                Vector3 segmentPos = (segmentStart + segmentEnd) * 0.5f;

                // Billboard perpendicular to beam direction
                Vector3 toCam = camPos - segmentPos;
                Vector3 right = Vector3.Cross(beamDirection, toCam);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right;
                else right.Normalize();

                // Make segments overlap slightly for continuous look
                float halfSegmentLength = segmentLength * 0.6f; // Overlap segments

                int vBase = i * 4;
                int iBase = i * 6;

                // Create quad stretched along beam direction
                _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(
                    segmentPos - right * thickness - beamDirection * halfSegmentLength, 
                    color, 
                    new Vector2(0, 0)
                );
                _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(
                    segmentPos + right * thickness - beamDirection * halfSegmentLength, 
                    color, 
                    new Vector2(1, 0)
                );
                _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(
                    segmentPos - right * thickness + beamDirection * halfSegmentLength, 
                    color, 
                    new Vector2(0, 1)
                );
                _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(
                    segmentPos + right * thickness + beamDirection * halfSegmentLength, 
                    color, 
                    new Vector2(1, 1)
                );

                _indexBuffer[iBase + 0] = (short)(vBase + 0);
                _indexBuffer[iBase + 1] = (short)(vBase + 1);
                _indexBuffer[iBase + 2] = (short)(vBase + 2);
                _indexBuffer[iBase + 3] = (short)(vBase + 2);
                _indexBuffer[iBase + 4] = (short)(vBase + 1);
                _indexBuffer[iBase + 5] = (short)(vBase + 3);
            }

            // Render beam layer
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];

            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            _effect.Texture = _textures[WeaponType.ChargeBeam];
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertexBuffer, 0, segments * 4,
                    _indexBuffer, 0, segments * 2
                );
            }

            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }
        
        private void DrawLightningBeams(Camera camera)
        {
            if (_lightningBeams.Count == 0) return;
            
            foreach (LightningBeam lightning in _lightningBeams)
            {
                // Calculate crackling animation offset
                float crackleOffset = (float)Math.Sin(lightning.AnimationTime * 30f) * 2f;
                
                // Pulsing effect
                float pulse = (float)Math.Sin(lightning.AnimationTime * 20f) * 0.3f + 0.7f;
                Color lightningColor = lightning.BeamColor * pulse;
                
                // Draw primary beam from start to first target
                if (lightning.ChainPositions.Count > 0)
                {
                    DrawLightningBolt(camera, lightning.StartPosition, lightning.ChainPositions[0], 
                                     8f, lightningColor, crackleOffset);
                }
                
                // Draw chain lightning between targets
                for (int i = 0; i < lightning.ChainPositions.Count - 1; i++)
                {
                    DrawLightningBolt(camera, lightning.ChainPositions[i], lightning.ChainPositions[i + 1],
                                     6f, lightningColor * 0.8f, crackleOffset);
                }
            }
        }
        
        private void DrawLightningBolt(Camera camera, Vector3 start, Vector3 end, float thickness, Color color, float crackleOffset)
        {
            Vector3 direction = end - start;
            float length = direction.Length();
            if (length < 0.1f) return;
            
            direction.Normalize();
            
            // Draw jagged lightning segments
            int segments = Math.Max(3, (int)(length / 100f)); // More segments for longer beams
            Vector3 currentPos = start;
            
            Random rand = new Random((int)(crackleOffset * 1000));
            
            for (int i = 0; i < segments; i++)
            {
                float t = (float)(i + 1) / segments;
                Vector3 targetPos = Vector3.Lerp(start, end, t);
                
                // Add random offset for jagged effect
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.Up);
                if (perpendicular.LengthSquared() < 0.0001f)
                    perpendicular = Vector3.Cross(direction, Vector3.Forward);
                perpendicular.Normalize();
                
                float randomOffset = ((float)rand.NextDouble() - 0.5f) * 20f;
                targetPos += perpendicular * randomOffset;
                
                // Draw segment with varying thickness
                float segmentThickness = thickness * (0.7f + (float)rand.NextDouble() * 0.6f);
                DrawBeamSegment(camera, currentPos, targetPos, segmentThickness, color);
                
                currentPos = targetPos;
            }
            
            // Draw glow layers
            DrawBeamSegment(camera, start, end, thickness * 2f, color * 0.3f);
        }
        
        private void DrawBeamSegment(Camera camera, Vector3 start, Vector3 end, float thickness, Color color)
        {
            Vector3 direction = end - start;
            float length = direction.Length();
            if (length < 0.1f) return;
            
            direction.Normalize();
            Vector3 camPos = camera.Position;
            
            // Billboard perpendicular to beam
            Vector3 toCam = camPos - start;
            Vector3 right = Vector3.Cross(direction, toCam);
            if (right.LengthSquared() < 0.0001f) right = Vector3.Right;
            else right.Normalize();
            
            // Create quad
            Vector3 mid = (start + end) * 0.5f;
            float halfLength = length * 0.5f;
            
            VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[4];
            vertices[0] = new VertexPositionColorTexture(mid - direction * halfLength - right * thickness, color, new Vector2(0, 0));
            vertices[1] = new VertexPositionColorTexture(mid + direction * halfLength - right * thickness, color, new Vector2(1, 0));
            vertices[2] = new VertexPositionColorTexture(mid - direction * halfLength + right * thickness, color, new Vector2(0, 1));
            vertices[3] = new VertexPositionColorTexture(mid + direction * halfLength + right * thickness, color, new Vector2(1, 1));
            
            short[] indices = new short[] { 0, 1, 2, 2, 1, 3 };
            
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _effect.Texture = _textures[WeaponType.Wunderwafffle];
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    vertices, 0, 4,
                    indices, 0, 2
                );
            }
            
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
        }
        
        private void DrawMuzzleFlashes(Camera camera)
        {
            int count = _muzzleFlashes.Count;
            if (count == 0) return;
            
            int requiredVertices = count * 4;
            int requiredIndices = count * 6;
            if (_vertexBuffer.Length < requiredVertices) _vertexBuffer = new VertexPositionColorTexture[requiredVertices];
            if (_indexBuffer.Length < requiredIndices) _indexBuffer = new short[requiredIndices];
            
            Vector3 camPos = camera.Position;
            
            for (int i = 0; i < count; i++)
            {
                MuzzleFlash flash = _muzzleFlashes[i];
                
                float t = flash.Life / flash.MaxLife;
                float alpha = 1f - t;
                Color color = new Color(255, 200, 100) * alpha;
                
                Vector3 forward = camPos - flash.Position;
                if (forward.LengthSquared() < 0.00001f) forward = Vector3.Forward;
                forward.Normalize();
                
                Vector3 right = Vector3.Cross(Vector3.Up, forward);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right;
                else right.Normalize();
                
                Vector3 up = Vector3.Cross(forward, right);
                
                float size = flash.Size * (1f + t * 0.5f);
                
                int vBase = i * 4;
                int iBase = i * 6;
                
                _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(flash.Position + (-right - up) * size, color, new Vector2(0, 1));
                _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(flash.Position + (right - up) * size, color, new Vector2(1, 1));
                _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(flash.Position + (-right + up) * size, color, new Vector2(0, 0));
                _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(flash.Position + (right + up) * size, color, new Vector2(1, 0));
                
                _indexBuffer[iBase + 0] = (short)(vBase + 0);
                _indexBuffer[iBase + 1] = (short)(vBase + 1);
                _indexBuffer[iBase + 2] = (short)(vBase + 2);
                _indexBuffer[iBase + 3] = (short)(vBase + 2);
                _indexBuffer[iBase + 4] = (short)(vBase + 1);
                _indexBuffer[iBase + 5] = (short)(vBase + 3);
            }
            
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];
            
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            
            _effect.Texture = _textures[WeaponType.Fireball]; // Use fireball texture for flashes
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertexBuffer, 0, count * 4,
                    _indexBuffer, 0, count * 2
                );
            }
            
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }
        
        /// <summary>
        /// Set the energy system for this weapon system
        /// </summary>
        public void SetEnergySystem(ShipEnergy energy)
        {
            _energy = energy;
        }
        
        /// <summary>
        /// Set ship references for chain lightning targeting
        /// This should be called from the game's Update method
        /// </summary>
        public void SetShipReferences(List<object> ships)
        {
            _allShips = ships;
        }
    }
}
