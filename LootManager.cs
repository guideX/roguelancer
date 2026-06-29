using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Tracks active cargo pods, spawns drops from destroyed ships, and handles simple tractor pickup.
    /// </summary>
    public sealed class LootManager : IDisposable
    {
        private enum LootDropProfile
        {
            None,
            Legal,
            Contraband
        }

        private const float TractorActivationRange = 1200f;
        private const float TractorAcceleration = 90f;
        private const float TractorMaxSpeed = 120f;
        private const float DetectionRange = 900f;
        private const float DefaultPickupRadius = 34f;
        private const float MinLifetimeSeconds = 45f;
        private const float MaxLifetimeBonusSeconds = 30f;

        private static readonly string[] LegalCargoPool =
        {
            "food-rations",
            "water",
            "h-fuel",
            "engine-components",
            "construction-materials",
            "luxury-goods",
            "medical-supplies",
            "diamonds",
            "boron",
            "consumer-goods"
        };

        private static readonly string[] ContrabandCargoPool =
        {
            "side-arms",
            "alien-organisms",
            "diamonds"
        };

        private readonly List<CargoPod> _activePods = new();
        private readonly Random _random;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;

        private bool _hasLastPlayerState;
        private Vector3 _lastPlayerPosition;
        private CargoHold _lastCargoHold;

        public LootManager(GraphicsDevice graphicsDevice = null, Random random = null, SpriteFont font = null, Texture2D pixel = null)
        {
            _graphicsDevice = graphicsDevice;
            _random = random ?? new Random();
            _font = font;
            _pixel = pixel;

            if (_graphicsDevice != null)
            {
                _effect = new BasicEffect(_graphicsDevice)
                {
                    VertexColorEnabled = true,
                    LightingEnabled = false
                };
            }
        }

        public IReadOnlyList<CargoPod> ActivePods => _activePods;

        public int SpawnLootForDestroyedNpc(NpcShip destroyedShip, Action<string> log = null)
        {
            if (destroyedShip == null || !destroyedShip.IsDestroyed)
            {
                return 0;
            }

            LootDropProfile profile = GetLootProfile(destroyedShip);
            if (profile == LootDropProfile.None)
            {
                return 0;
            }

            string[] pool = profile == LootDropProfile.Contraband ? ContrabandCargoPool : LegalCargoPool;
            string commodityId = pool[_random.Next(pool.Length)];
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity == null)
            {
                return 0;
            }

            int quantity = 1 + _random.Next(3);
            Vector3 position = destroyedShip.Position + RandomScatter(18f, 60f);
            Vector3 velocity = destroyedShip.Velocity * 0.2f + RandomScatter(0f, 28f);
            float lifetimeSeconds = MinLifetimeSeconds + (float)_random.NextDouble() * MaxLifetimeBonusSeconds;

            if (!CargoPod.TryCreate(commodity.Id, quantity, position, velocity, lifetimeSeconds, DefaultPickupRadius, out CargoPod pod))
            {
                return 0;
            }

            _activePods.Add(pod);
            log?.Invoke($"[LOOT] pod spawned: {commodity.Name} x{quantity}");
            return 1;
        }

        public void Update(GameTime gameTime, Ship playerShip, bool tractorActive, NotificationManager notificationManager = null, Action<string> log = null)
        {
            if (gameTime == null)
            {
                return;
            }

            _hasLastPlayerState = playerShip != null;
            _lastPlayerPosition = playerShip?.Position ?? Vector3.Zero;
            _lastCargoHold = playerShip?.CargoHold;

            if (_activePods.Count == 0)
            {
                return;
            }

            float deltaTime = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 playerPosition = _lastPlayerPosition;
            CargoHold cargoHold = _lastCargoHold;
            bool canPickup = tractorActive && cargoHold != null;
            float detectionRangeSquared = DetectionRange * DetectionRange;
            float tractorRangeSquared = TractorActivationRange * TractorActivationRange;

            for (int i = _activePods.Count - 1; i >= 0; i--)
            {
                CargoPod pod = _activePods[i];
                float distanceSquared = Vector3.DistanceSquared(pod.Position, playerPosition);

                if (!pod.DetectionNotified && distanceSquared <= detectionRangeSquared)
                {
                    pod.DetectionNotified = true;
                    notificationManager?.ShowMessage("Cargo pod detected", 2f);
                }

                if (tractorActive && distanceSquared <= tractorRangeSquared)
                {
                    pod.ApplyTractor(playerPosition, deltaTime, TractorAcceleration, TractorMaxSpeed, TractorActivationRange);
                }

                pod.Update(deltaTime);

                if (pod.IsExpired)
                {
                    log?.Invoke($"[LOOT] pod expired: {GetPodLabel(pod)}");
                    _activePods.RemoveAt(i);
                    continue;
                }

                distanceSquared = Vector3.DistanceSquared(pod.Position, playerPosition);

                if (!canPickup || !pod.IsWithinPickupRange(playerPosition))
                {
                    continue;
                }

                Commodity commodity = pod.GetCommodity();
                if (commodity == null)
                {
                    log?.Invoke($"[LOOT] unknown cargo skipped: {pod.CommodityId}");
                    _activePods.RemoveAt(i);
                    continue;
                }

                if (cargoHold.AddCommodity(commodity, pod.Quantity))
                {
                    string pickupText = $"Tractored {commodity.Name} x{pod.Quantity}";
                    notificationManager?.ShowMessage(pickupText, 2f);
                    log?.Invoke($"[LOOT] pod collected: {commodity.Name} x{pod.Quantity}");
                    _activePods.RemoveAt(i);
                    continue;
                }

                if (!pod.CargoFullNotified)
                {
                    pod.CargoFullNotified = true;
                    notificationManager?.ShowMessage("Cargo hold full", 2f);
                    log?.Invoke("[LOOT] cargo full");
                }
            }
        }

        public bool HasNearbyCargoPod()
        {
            return GetNearestCargoPod() != null;
        }

        public bool HasNearbyCargoPod(Vector3 playerPosition)
        {
            return GetNearestCargoPod(playerPosition) != null;
        }

        public CargoPod GetNearestCargoPod()
        {
            if (!_hasLastPlayerState)
            {
                return null;
            }

            return GetNearestCargoPod(_lastPlayerPosition);
        }

        public CargoPod GetNearestCargoPod(Vector3 playerPosition)
        {
            CargoPod nearestPod = null;
            float nearestDistanceSquared = TractorActivationRange * TractorActivationRange;

            for (int i = 0; i < _activePods.Count; i++)
            {
                CargoPod pod = _activePods[i];
                if (pod == null || pod.IsExpired)
                {
                    continue;
                }

                float distanceSquared = Vector3.DistanceSquared(pod.Position, playerPosition);
                if (distanceSquared > nearestDistanceSquared)
                {
                    continue;
                }

                if (nearestPod == null || distanceSquared < nearestDistanceSquared)
                {
                    nearestPod = pod;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearestPod;
        }

        public string GetNearestCargoPodHint()
        {
            if (!_hasLastPlayerState)
            {
                return null;
            }

            return GetNearestCargoPodHint(_lastPlayerPosition, _lastCargoHold);
        }

        public string GetNearestCargoPodHint(Vector3 playerPosition, CargoHold cargoHold = null)
        {
            CargoPod nearestPod = GetNearestCargoPod(playerPosition);
            if (nearestPod == null)
            {
                return null;
            }

            Commodity commodity = nearestPod.GetCommodity();
            if (commodity == null)
            {
                return "Hold P: Tractor Cargo";
            }

            if (cargoHold != null && !cargoHold.CanFit(commodity, nearestPod.Quantity))
            {
                return "Cargo hold full";
            }

            float distance = Vector3.Distance(playerPosition, nearestPod.Position);
            return $"Hold P: Tractor {commodity.Name} x{nearestPod.Quantity}\n{FormatDistance(distance)}";
        }

        public void DrawHUD(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null || _font == null || _pixel == null || _graphicsDevice == null || !_hasLastPlayerState)
            {
                return;
            }

            string hint = GetNearestCargoPodHint();
            if (string.IsNullOrWhiteSpace(hint))
            {
                return;
            }

            string[] lines = hint.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                return;
            }

            Viewport viewport = _graphicsDevice.Viewport;
            float maxLineWidth = 0f;
            float totalHeight = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                Vector2 lineSize = _font.MeasureString(lines[i]);
                maxLineWidth = Math.Max(maxLineWidth, lineSize.X);
                totalHeight += lineSize.Y;
            }

            int paddingX = 14;
            int paddingY = 10;
            int lineSpacing = 2;
            int boxW = (int)Math.Ceiling(maxLineWidth) + (paddingX * 2);
            int boxH = (int)Math.Ceiling(totalHeight) + ((lines.Length - 1) * lineSpacing) + (paddingY * 2);

            int boxX = Math.Max(20, viewport.Width - boxW - 20);
            int boxY = Math.Max(20, viewport.Height - boxH - 170);
            Rectangle panel = new Rectangle(boxX, boxY, boxW, boxH);

            bool cargoFull = string.Equals(hint, "Cargo hold full", StringComparison.OrdinalIgnoreCase);
            Color accent = cargoFull ? new Color(255, 140, 90) : new Color(90, 220, 255);
            float pulse = 0.75f + (0.25f * (float)Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * 6.0));
            accent *= pulse;

            spriteBatch.Draw(_pixel, panel, Color.Black * 0.72f);
            spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 2), accent);
            spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 2, panel.Width, 2), accent * 0.6f);
            spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, 2, panel.Height), accent * 0.6f);
            spriteBatch.Draw(_pixel, new Rectangle(panel.Right - 2, panel.Y, 2, panel.Height), accent * 0.6f);
            spriteBatch.Draw(_pixel, new Rectangle(panel.X + 12, panel.Y + 12, 8, 8), accent);

            int textY = panel.Y + paddingY;
            for (int i = 0; i < lines.Length; i++)
            {
                spriteBatch.DrawString(_font, lines[i], new Vector2(panel.X + paddingX + 14, textY), Color.White);
                textY += (int)_font.MeasureString(lines[i]).Y + lineSpacing;
            }
        }

        public void Draw(Matrix view, Matrix projection)
        {
            if (_graphicsDevice == null || _effect == null || _activePods.Count == 0)
            {
                return;
            }

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            foreach (CargoPod pod in _activePods)
            {
                Commodity commodity = pod.GetCommodity();
                Color color = commodity?.DisplayColor ?? Color.White;
                pod.Draw(_graphicsDevice, _effect, view, projection, color, 12f);
            }
        }

        public void Dispose()
        {
            _effect?.Dispose();
        }

        private LootDropProfile GetLootProfile(NpcShip ship)
        {
            string factionId = FactionManager.NormalizeFactionId(ship.FactionId);
            bool trafficConfigured = !string.IsNullOrWhiteSpace(ship.TrafficZoneId);

            if (trafficConfigured)
            {
                if (ship.TrafficBehavior == TrafficZoneBehaviorType.PirateAmbush)
                {
                    return LootDropProfile.Contraband;
                }

                if (ship.TrafficBehavior == TrafficZoneBehaviorType.LawfulPatrol)
                {
                    return LootDropProfile.None;
                }

                if (ship.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute)
                {
                    return LootDropProfile.Legal;
                }
            }

            if (IsPirateLike(factionId))
            {
                return LootDropProfile.Contraband;
            }

            if (IsLawfulLike(factionId))
            {
                return LootDropProfile.None;
            }

            if (IsTraderLike(factionId))
            {
                return LootDropProfile.Legal;
            }

            return LootDropProfile.Legal;
        }

        private static bool IsLawfulLike(string factionId)
        {
            return string.Equals(factionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(factionId, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPirateLike(string factionId)
        {
            return factionId.IndexOf("rogue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   factionId.IndexOf("pirate", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTraderLike(string factionId)
        {
            return string.Equals(factionId, FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase) ||
                   factionId.IndexOf("corporation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   factionId.IndexOf("junk", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Vector3 RandomScatter(float minDistance, float maxDistance)
        {
            float distance = minDistance + (float)_random.NextDouble() * Math.Max(0f, maxDistance - minDistance);
            return RandomUnitVector() * distance;
        }

        private Vector3 RandomUnitVector()
        {
            float y = (float)(_random.NextDouble() * 2.0 - 1.0);
            double angle = _random.NextDouble() * Math.PI * 2.0;
            float radial = (float)Math.Sqrt(Math.Max(0.0, 1.0 - (y * y)));
            return new Vector3(
                (float)(Math.Cos(angle) * radial),
                y,
                (float)(Math.Sin(angle) * radial));
        }

        private static string FormatDistance(float distance)
        {
            if (distance >= 1000f)
            {
                return $"{distance / 1000f:F1} km";
            }

            return $"{distance:F0} m";
        }

        private static string GetPodLabel(CargoPod pod)
        {
            Commodity commodity = pod?.GetCommodity();
            string name = commodity?.Name ?? pod?.CommodityId ?? "unknown";
            return $"{name} x{pod?.Quantity ?? 0}";
        }
    }
}
