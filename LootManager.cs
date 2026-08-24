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
        private const float TractorActivationRange = 1200f;
        private const float TractorAcceleration = 90f;
        private const float TractorMaxSpeed = 120f;
        private const float DetectionRange = 900f;

        private readonly List<CargoPod> _activePods = new();
        private readonly CombatSalvageService _salvageService;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;
        private Func<IEnumerable<SpaceObject>> _worldObjectsProvider;

        private bool _hasLastPlayerState;
        private Vector3 _lastPlayerPosition;
        private CargoHold _lastCargoHold;

        public LootManager(
            GraphicsDevice graphicsDevice = null,
            Random random = null,
            SpriteFont font = null,
            Texture2D pixel = null,
            CombatSalvageService salvageService = null,
            Func<IEnumerable<SpaceObject>> worldObjectsProvider = null)
        {
            _graphicsDevice = graphicsDevice;
            // The legacy Random parameter remains source-compatible for the
            // early loot harness, but Phase 38 policy never consumes it.
            _salvageService = salvageService ?? new CombatSalvageService();
            _font = font;
            _pixel = pixel;
            _worldObjectsProvider = worldObjectsProvider;

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
        public CombatSalvageService SalvageService => _salvageService;
        public int ActiveSalvageCount => _activePods.Count;
        public string LastPickupNotification { get; private set; } = string.Empty;

        public void SetWorldObjectProvider(Func<IEnumerable<SpaceObject>> worldObjectsProvider)
        {
            _worldObjectsProvider = worldObjectsProvider;
        }

        public int SpawnLootForDestroyedNpc(NpcShip destroyedShip, Action<string> log = null)
        {
            IReadOnlyList<SalvageDrop> drops = _salvageService.EvaluateDestruction(destroyedShip);
            if (drops.Count == 0 || _activePods.Count >= CombatSalvageService.MaxLiveSalvageObjects)
            {
                return 0;
            }

            int spawned = 0;
            int availableSlots = CombatSalvageService.MaxLiveSalvageObjects - _activePods.Count;
            for (int i = 0; i < drops.Count && spawned < availableSlots; i++)
            {
                SalvageDrop drop = drops[i];
                Commodity commodity = CommodityCatalog.GetById(drop.CommodityId);
                if (commodity == null || drop.Quantity <= 0 ||
                    !TryFindSpawnPosition(destroyedShip, drop.StackIndex, out Vector3 position))
                {
                    continue;
                }

                Vector3 velocity = destroyedShip.Velocity * 0.15f;
                if (!CargoPod.TryCreate(
                        commodity.Id,
                        drop.Quantity,
                        position,
                        velocity,
                        (float)CombatSalvageService.SalvageLifetimeSeconds,
                        CombatSalvageService.PickupRadius,
                        out CargoPod pod))
                {
                    continue;
                }

                pod.SetSalvageSource(destroyedShip, drop.Tier);
                _activePods.Add(pod);
                spawned++;
                log?.Invoke($"[SALVAGE] pod spawned: {commodity.Name} x{drop.Quantity}");
            }

            return spawned;
        }

        public int SpawnSalvageForDestroyedNpc(NpcShip destroyedShip, Action<string> log = null) =>
            SpawnLootForDestroyedNpc(destroyedShip, log);

        public void Update(GameTime gameTime, Ship playerShip, bool tractorActive, NotificationManager notificationManager = null, Action<string> log = null)
        {
            LastPickupNotification = string.Empty;
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
            // Entering the close pickup radius is sufficient. The existing P
            // tractor control remains an optional convenience for approaching
            // a drop from farther away.
            bool canPickup = cargoHold != null;
            float detectionRangeSquared = DetectionRange * DetectionRange;
            float tractorRangeSquared = TractorActivationRange * TractorActivationRange;
            Dictionary<string, int> collectedByCommodity = new(StringComparer.OrdinalIgnoreCase);
            bool cargoWasFull = false;

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

                if (cargoHold.TryAddCommodityPartial(commodity, pod.Quantity, out int collectedQuantity))
                {
                    pod.TakeQuantity(collectedQuantity);
                    collectedByCommodity[commodity.Name] = collectedByCommodity.TryGetValue(commodity.Name, out int current)
                        ? current + collectedQuantity
                        : collectedQuantity;
                    log?.Invoke($"[SALVAGE] pod collected: {commodity.Name} x{collectedQuantity}");

                    if (pod.IsDepleted)
                    {
                        _activePods.RemoveAt(i);
                    }
                    else
                    {
                        // The remainder remains a physical object. It can be
                        // retried later when the player has free capacity.
                        pod.CargoFullNotified = false;
                    }

                    continue;
                }

                if (!pod.CargoFullNotified)
                {
                    pod.CargoFullNotified = true;
                    cargoWasFull = true;
                    log?.Invoke("[LOOT] cargo full");
                }
            }

            if (collectedByCommodity.Count > 0)
            {
                List<string> parts = new();
                foreach (KeyValuePair<string, int> entry in collectedByCommodity)
                {
                    parts.Add($"{entry.Value} {entry.Key}");
                }

                LastPickupNotification = $"Salvaged: {string.Join(", ", parts)}";
                notificationManager?.ShowMessage(LastPickupNotification, 2f);
            }
            else if (cargoWasFull)
            {
                LastPickupNotification = "Cargo hold full";
                notificationManager?.ShowMessage(LastPickupNotification, 2f);
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

            if (cargoHold != null && cargoHold.AvailableCapacity <= 0)
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

        public void Reset()
        {
            _activePods.Clear();
            _salvageService.Reset();
            _hasLastPlayerState = false;
            _lastPlayerPosition = Vector3.Zero;
            _lastCargoHold = null;
            LastPickupNotification = string.Empty;
        }

        private bool TryFindSpawnPosition(NpcShip destroyedShip, int stackIndex, out Vector3 position)
        {
            position = Vector3.Zero;
            if (destroyedShip == null)
            {
                return false;
            }

            // A few deterministic attempts give nearby objects a chance to
            // reserve the first candidate without introducing a physics or
            // spatial-index subsystem for a maximum of 32 transient drops.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int candidateIndex = stackIndex + (attempt * 2);
                Vector3 candidate = destroyedShip.Position + _salvageService.GetSpawnOffset(destroyedShip, candidateIndex);
                if (IsSpawnPositionAvailable(candidate, destroyedShip))
                {
                    position = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool IsSpawnPositionAvailable(Vector3 candidate, NpcShip destroyedShip)
        {
            foreach (CargoPod pod in _activePods)
            {
                if (pod == null || pod.IsExpired || Vector3.DistanceSquared(candidate, pod.Position) < 40f * 40f)
                {
                    return false;
                }
            }

            IEnumerable<SpaceObject> worldObjects = _worldObjectsProvider?.Invoke();
            if (worldObjects == null)
            {
                return true;
            }

            foreach (SpaceObject worldObject in worldObjects)
            {
                if (worldObject == null || ReferenceEquals(worldObject, destroyedShip) ||
                    worldObject is NpcShip npc && npc.IsDestroyed)
                {
                    continue;
                }

                float avoidanceRadius = MathHelper.Clamp(worldObject.Radius * 0.25f, 20f, 125f);
                if (Vector3.DistanceSquared(candidate, worldObject.Position) < avoidanceRadius * avoidanceRadius)
                {
                    return false;
                }
            }

            return true;
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
