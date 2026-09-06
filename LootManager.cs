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
        private Func<NpcShip, MissionCargoDrop> _missionCargoResolver;
        private Action<CargoPod> _missionCargoPodSpawnedCallback;
        private Action<CargoPod, int> _missionCargoPodCollectedCallback;
        private Action<CargoPod, int> _missionCargoPodExpiredCallback;
        private Action<MissionCargoDrop> _missionCargoUnavailableCallback;

        private bool _hasLastPlayerState;
        private Vector3 _lastPlayerPosition;
        private CargoHold _lastCargoHold;
        private ShipLoadout _lastLoadout;

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

        public void ConfigureMissionCargoCallbacks(
            Func<NpcShip, MissionCargoDrop> resolver,
            Action<CargoPod> podSpawned,
            Action<CargoPod, int> podCollected,
            Action<CargoPod, int> podExpired,
            Action<MissionCargoDrop> cargoUnavailable)
        {
            _missionCargoResolver = resolver;
            _missionCargoPodSpawnedCallback = podSpawned;
            _missionCargoPodCollectedCallback = podCollected;
            _missionCargoPodExpiredCallback = podExpired;
            _missionCargoUnavailableCallback = cargoUnavailable;
        }

        public List<SaveCargoPodData> CaptureMissionCargoPods()
        {
            var result = new List<SaveCargoPodData>();
            foreach (CargoPod pod in _activePods)
            {
                if (pod == null || !pod.IsMissionCargo || pod.IsDepleted || pod.IsExpired ||
                    string.IsNullOrWhiteSpace(pod.CommodityId) || pod.Quantity <= 0 ||
                    !TradeLaneStateSanitizer.IsFinite(pod.Position) ||
                    !TradeLaneStateSanitizer.IsFinite(pod.Velocity))
                    continue;

                result.Add(new SaveCargoPodData
                {
                    MissionId = pod.MissionId,
                    MissionCargoSourceIndex = pod.MissionCargoSourceIndex,
                    CommodityId = pod.CommodityId,
                    Quantity = Math.Clamp(pod.Quantity, 1, 40),
                    AgeSeconds = MathHelper.Clamp(pod.AgeSeconds, 0f, pod.LifetimeSeconds),
                    Position = SaveVector3Data.From(pod.Position),
                    Velocity = SaveVector3Data.From(pod.Velocity),
                    SourceNpcName = pod.SourceNpcName ?? string.Empty
                });
            }

            return result;
        }

        public int RestoreMissionCargoPods(
            IEnumerable<SaveCargoPodData> savedPods,
            Func<int, bool> missionActivePredicate = null)
        {
            if (savedPods == null)
                return 0;

            var restoredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int restored = 0;
            foreach (SaveCargoPodData data in savedPods)
            {
                if (data == null || data.MissionId <= 0 || data.Quantity <= 0 ||
                    (missionActivePredicate != null && !missionActivePredicate(data.MissionId)) ||
                    string.IsNullOrWhiteSpace(data.CommodityId))
                    continue;

                Commodity commodity = CommodityCatalog.GetById(data.CommodityId);
                Vector3 position = data.Position?.ToVector3() ?? Vector3.Zero;
                Vector3 velocity = data.Velocity?.ToVector3() ?? Vector3.Zero;
                float age = float.IsNaN(data.AgeSeconds) || float.IsInfinity(data.AgeSeconds)
                    ? 0f
                    : Math.Max(0f, data.AgeSeconds);
                string key = $"{data.MissionId}:{data.MissionCargoSourceIndex}";
                if (commodity == null || age >= CombatSalvageService.SalvageLifetimeSeconds ||
                    _activePods.Count >= CombatSalvageService.MaxLiveSalvageObjects ||
                    !restoredKeys.Add(key) || !TradeLaneStateSanitizer.IsFinite(position) ||
                    !TradeLaneStateSanitizer.IsFinite(velocity) ||
                    !IsSpawnPositionAvailable(position, null) ||
                    !CargoPod.TryCreate(
                        commodity.Id,
                        Math.Clamp(data.Quantity, 1, 40),
                        position,
                        velocity,
                        (float)CombatSalvageService.SalvageLifetimeSeconds,
                        CombatSalvageService.PickupRadius,
                        out CargoPod pod))
                    continue;

                pod.SetMissionCargoAttribution(data.MissionId, data.MissionCargoSourceIndex, data.SourceNpcName);
                pod.RestoreAge(age);
                _activePods.Add(pod);
                restored++;
            }

            return restored;
        }

        public void ReleaseMissionCargoAttribution(int missionId)
        {
            if (missionId <= 0)
                return;

            foreach (CargoPod pod in _activePods)
            {
                if (pod?.MissionId == missionId)
                    pod.ClearMissionCargoAttribution();
            }
        }

        /// <summary>
        /// Moves currently owned attributed mission cargo back into the
        /// physical loot system. The source index stays unset because this is
        /// a player jettison, not a second transport release.
        /// </summary>
        public bool TryJettisonMissionCargo(
            CargoHold cargoHold,
            int missionId,
            Commodity commodity,
            int quantity,
            Vector3 position,
            Vector3 velocity,
            out CargoPod pod)
        {
            pod = null;
            if (cargoHold == null || missionId <= 0 || commodity == null || quantity <= 0 || quantity > 40 ||
                !TradeLaneStateSanitizer.IsFinite(position) || !TradeLaneStateSanitizer.IsFinite(velocity) ||
                _activePods.Count >= CombatSalvageService.MaxLiveSalvageObjects ||
                !IsSpawnPositionAvailable(position, null) ||
                !CargoPod.TryCreate(
                    commodity.Id,
                    quantity,
                    position,
                    velocity,
                    (float)CombatSalvageService.SalvageLifetimeSeconds,
                    CombatSalvageService.PickupRadius,
                    out CargoPod createdPod))
            {
                return false;
            }

            if (!cargoHold.RemoveMissionCargoQuantity(missionId, commodity, quantity, out int removed) || removed != quantity)
                return false;

            createdPod.SetMissionCargoAttribution(missionId, -1, "Player jettison");
            _activePods.Add(createdPod);
            pod = createdPod;
            return true;
        }

        /// <summary>
        /// Converts every current contraband stack into bounded physical pods.
        /// Mission-attributed units keep their mission identity so pickup can
        /// restore the exact contract cargo; ordinary units remain ordinary.
        /// </summary>
        public int TryJettisonContraband(
            CargoHold cargoHold,
            Vector3 position,
            Vector3 velocity,
            out int podCount)
        {
            podCount = 0;
            if (cargoHold == null || !TradeLaneStateSanitizer.IsFinite(position) ||
                !TradeLaneStateSanitizer.IsFinite(velocity))
            {
                return 0;
            }

            int removedQuantity = 0;
            int stackIndex = 0;
            foreach (MissionCargoReservation reservation in cargoHold.GetMissionCargoReservations())
            {
                Commodity commodity = CommodityCatalog.GetByIdOrName(reservation.CommodityId);
                if (commodity?.IsContraband != true || reservation.Quantity <= 0)
                    continue;

                int remaining = reservation.Quantity;
                while (remaining > 0)
                {
                    int quantity = Math.Min(40, remaining);
                    Vector3 podPosition = position + new Vector3((stackIndex + 1) * 90f, 0f, 0f);
                    if (!TryJettisonMissionCargo(
                            cargoHold,
                            reservation.MissionId,
                            commodity,
                            quantity,
                            podPosition,
                            velocity,
                            out _))
                    {
                        break;
                    }

                    removedQuantity += quantity;
                    podCount++;
                    remaining -= quantity;
                    stackIndex++;
                }
            }

            foreach (KeyValuePair<string, int> entry in cargoHold.GetAllCommodities())
            {
                Commodity commodity = CommodityCatalog.GetByIdOrName(entry.Key);
                if (commodity?.IsContraband != true)
                    continue;

                int remaining = Math.Min(
                    Math.Max(0, entry.Value),
                    cargoHold.GetSellableCommodityQuantity(entry.Key));
                while (remaining > 0)
                {
                    int quantity = Math.Min(40, remaining);
                    Vector3 podPosition = position + new Vector3((stackIndex + 1) * 90f, 0f, 0f);
                    if (!TryJettisonOrdinaryCargo(
                            cargoHold,
                            commodity,
                            quantity,
                            podPosition,
                            velocity,
                            out _))
                    {
                        break;
                    }

                    removedQuantity += quantity;
                    podCount++;
                    remaining -= quantity;
                    stackIndex++;
                }
            }

            return removedQuantity;
        }

        private bool TryJettisonOrdinaryCargo(
            CargoHold cargoHold,
            Commodity commodity,
            int quantity,
            Vector3 position,
            Vector3 velocity,
            out CargoPod pod)
        {
            pod = null;
            if (cargoHold == null || commodity == null || quantity <= 0 || quantity > 40 ||
                _activePods.Count >= CombatSalvageService.MaxLiveSalvageObjects ||
                !IsSpawnPositionAvailable(position, null) ||
                !CargoPod.TryCreate(
                    commodity.Id,
                    quantity,
                    position,
                    velocity,
                    (float)CombatSalvageService.SalvageLifetimeSeconds,
                    CombatSalvageService.PickupRadius,
                    out CargoPod createdPod))
            {
                return false;
            }

            if (!cargoHold.RemoveCommodity(commodity, quantity))
                return false;

            _activePods.Add(createdPod);
            pod = createdPod;
            return true;
        }

        public int SpawnLootForDestroyedNpc(NpcShip destroyedShip, Action<string> log = null)
        {
            MissionCargoDrop missionDrop = _missionCargoResolver?.Invoke(destroyedShip);
            int spawned = 0;
            int availableSlots = CombatSalvageService.MaxLiveSalvageObjects - _activePods.Count;
            if (missionDrop != null && availableSlots > 0 &&
                missionDrop.MissionId > 0 && missionDrop.Quantity > 0 &&
                !string.IsNullOrWhiteSpace(missionDrop.CommodityId) &&
                TryFindSpawnPosition(destroyedShip, 0, out Vector3 missionPosition) &&
                CargoPod.TryCreate(
                    missionDrop.CommodityId,
                    Math.Clamp(missionDrop.Quantity, 1, 40),
                    missionPosition,
                    destroyedShip?.Velocity * 0.15f ?? Vector3.Zero,
                    (float)CombatSalvageService.SalvageLifetimeSeconds,
                    CombatSalvageService.PickupRadius,
                    out CargoPod missionPod))
            {
                missionPod.SetSalvageSource(destroyedShip, CombatSalvageTier.Standard);
                missionPod.SetMissionCargoAttribution(missionDrop.MissionId, missionDrop.SourceIndex, missionDrop.SourceNpcName);
                _activePods.Add(missionPod);
                spawned++;
                availableSlots--;
                _missionCargoPodSpawnedCallback?.Invoke(missionPod);
                log?.Invoke($"[SALVAGE] mission cargo pod spawned: {missionPod.GetPayloadName()} x{missionPod.Quantity}");
            }
            else if (missionDrop != null)
            {
                _missionCargoUnavailableCallback?.Invoke(missionDrop);
            }

            IReadOnlyList<SalvageDrop> drops = _salvageService.EvaluateDestruction(destroyedShip);
            if (drops.Count == 0 || availableSlots <= 0)
            {
                return spawned;
            }

            for (int i = 0; i < drops.Count && spawned < availableSlots; i++)
            {
                SalvageDrop drop = drops[i];
                Commodity commodity = drop.IsCommodity ? CommodityCatalog.GetById(drop.CommodityId) : null;
                EquipmentDefinition equipment = drop.IsEquipment ? EquipmentCatalog.GetById(drop.EquipmentId) : null;
                ConsumableEquipmentDefinition consumable = drop.IsConsumable
                    ? EquipmentCatalog.GetById(drop.ConsumableId) as ConsumableEquipmentDefinition
                    : null;
                if ((drop.IsCommodity && (commodity == null || drop.Quantity <= 0)) ||
                    (drop.IsEquipment && (equipment == null || drop.Quantity != 1)) ||
                    (drop.IsConsumable && (consumable == null || !consumable.IsValid || drop.Quantity <= 0)) ||
                    !TryFindSpawnPosition(destroyedShip, drop.StackIndex, out Vector3 position))
                {
                    continue;
                }

                Vector3 velocity = destroyedShip.Velocity * 0.15f;
                CargoPod pod;
                bool created = drop.IsEquipment
                    ? CargoPod.TryCreateEquipment(
                        equipment.Id,
                        position,
                        velocity,
                        (float)CombatSalvageService.SalvageLifetimeSeconds,
                        CombatSalvageService.PickupRadius,
                        out pod)
                    : drop.IsConsumable
                        ? CargoPod.TryCreateConsumable(
                            consumable.Id,
                            drop.Quantity,
                            position,
                            velocity,
                            (float)CombatSalvageService.SalvageLifetimeSeconds,
                            CombatSalvageService.PickupRadius,
                            out pod)
                        : CargoPod.TryCreate(
                            commodity.Id,
                            drop.Quantity,
                            position,
                            velocity,
                            (float)CombatSalvageService.SalvageLifetimeSeconds,
                            CombatSalvageService.PickupRadius,
                            out pod);
                if (!created)
                {
                    continue;
                }

                pod.SetSalvageSource(destroyedShip, drop.Tier);
                _activePods.Add(pod);
                spawned++;
                log?.Invoke($"[SALVAGE] pod spawned: {pod.GetPayloadName()} x{drop.Quantity}");
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
            _lastLoadout = playerShip?.Loadout;

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
            bool canPickup = cargoHold != null || playerShip?.Loadout != null;
            float detectionRangeSquared = DetectionRange * DetectionRange;
            float tractorRangeSquared = TractorActivationRange * TractorActivationRange;
            Dictionary<string, int> collectedByCommodity = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> collectedByEquipment = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> collectedByConsumable = new(StringComparer.OrdinalIgnoreCase);
            bool cargoWasFull = false;
            bool equipmentStorageWasFull = false;
            bool consumableStorageWasFull = false;

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
                    if (pod.IsMissionCargo)
                        _missionCargoPodExpiredCallback?.Invoke(pod, pod.Quantity);
                    _activePods.RemoveAt(i);
                    continue;
                }

                distanceSquared = Vector3.DistanceSquared(pod.Position, playerPosition);

                if (!canPickup || !pod.IsWithinPickupRange(playerPosition))
                {
                    continue;
                }

                if (pod.IsEquipment)
                {
                    EquipmentDefinition equipment = pod.GetEquipment();
                    if (equipment == null)
                    {
                        log?.Invoke($"[LOOT] unknown equipment skipped: {pod.EquipmentId}");
                        _activePods.RemoveAt(i);
                        continue;
                    }

                    if (playerShip?.Loadout == null)
                    {
                        continue;
                    }

                    if (playerShip.Loadout.AddOwnedEquipment(equipment, pod.Quantity))
                    {
                        int collectedEquipmentQuantity = pod.TakeQuantity(pod.Quantity);
                        if (collectedEquipmentQuantity > 0)
                        {
                            collectedByEquipment[equipment.Name] = collectedByEquipment.TryGetValue(equipment.Name, out int current)
                                ? current + collectedEquipmentQuantity
                                : collectedEquipmentQuantity;
                            log?.Invoke($"[SALVAGE] equipment collected: {equipment.Name} x{collectedEquipmentQuantity}");
                        }

                        if (pod.IsDepleted)
                        {
                            _activePods.RemoveAt(i);
                        }
                        else
                        {
                            pod.CargoFullNotified = false;
                        }
                    }
                    else if (!pod.CargoFullNotified)
                    {
                        pod.CargoFullNotified = true;
                        equipmentStorageWasFull = true;
                        log?.Invoke("[LOOT] equipment storage full");
                    }

                    continue;
                }

                if (pod.IsConsumable)
                {
                    ConsumableEquipmentDefinition consumable = pod.GetConsumable();
                    if (consumable == null || !consumable.IsValid)
                    {
                        log?.Invoke($"[LOOT] unknown consumable skipped: {pod.ConsumableId}");
                        _activePods.RemoveAt(i);
                        continue;
                    }

                    if (playerShip?.Loadout == null)
                    {
                        continue;
                    }

                    if (playerShip.Loadout.TryAddOwnedEquipmentPartial(consumable, pod.Quantity, out int collectedConsumableQuantity))
                    {
                        pod.TakeQuantity(collectedConsumableQuantity);
                        if (collectedConsumableQuantity > 0)
                        {
                            collectedByConsumable[consumable.Name] = collectedByConsumable.TryGetValue(consumable.Name, out int current)
                                ? current + collectedConsumableQuantity
                                : collectedConsumableQuantity;
                            log?.Invoke($"[SALVAGE] consumable collected: {consumable.Name} x{collectedConsumableQuantity}");
                        }

                        if (pod.IsDepleted)
                        {
                            _activePods.RemoveAt(i);
                        }
                        else
                        {
                            pod.CargoFullNotified = false;
                        }
                    }
                    else if (!pod.CargoFullNotified)
                    {
                        pod.CargoFullNotified = true;
                        consumableStorageWasFull = true;
                        log?.Invoke("[LOOT] consumable storage full");
                    }

                    continue;
                }

                Commodity commodity = pod.GetCommodity();
                if (commodity == null)
                {
                    log?.Invoke($"[LOOT] unknown cargo skipped: {pod.CommodityId}");
                    _activePods.RemoveAt(i);
                    continue;
                }

                if (cargoHold == null)
                {
                    continue;
                }

                int collectedQuantity;
                bool added = pod.IsMissionCargo
                    ? cargoHold.TryAddMissionCommodityPartial(pod.MissionId, commodity, pod.Quantity, out collectedQuantity)
                    : cargoHold.TryAddCommodityPartial(commodity, pod.Quantity, out collectedQuantity);
                if (added)
                {
                    pod.TakeQuantity(collectedQuantity);
                    if (pod.IsMissionCargo)
                        _missionCargoPodCollectedCallback?.Invoke(pod, collectedQuantity);
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

            if (collectedByCommodity.Count > 0 || collectedByEquipment.Count > 0 || collectedByConsumable.Count > 0)
            {
                List<string> parts = new();
                foreach (KeyValuePair<string, int> entry in collectedByCommodity)
                {
                    parts.Add($"{entry.Value} {entry.Key}");
                }

                foreach (KeyValuePair<string, int> entry in collectedByEquipment)
                {
                    parts.Add($"{entry.Value}x {entry.Key}");
                }

                foreach (KeyValuePair<string, int> entry in collectedByConsumable)
                {
                    parts.Add($"{entry.Value}x {entry.Key}");
                }

                LastPickupNotification = $"Salvaged: {string.Join(", ", parts)}";
                notificationManager?.ShowMessage(LastPickupNotification, 2f);
            }
            else if (cargoWasFull && (equipmentStorageWasFull || consumableStorageWasFull))
            {
                LastPickupNotification = "Cargo hold and equipment storage full";
                notificationManager?.ShowMessage(LastPickupNotification, 2f);
            }
            else if (equipmentStorageWasFull || consumableStorageWasFull)
            {
                LastPickupNotification = consumableStorageWasFull && !equipmentStorageWasFull
                    ? "Consumable storage full"
                    : "Equipment storage full";
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

            return GetNearestCargoPodHint(_lastPlayerPosition, _lastCargoHold, _lastLoadout);
        }

        public string GetNearestCargoPodHint(Vector3 playerPosition, CargoHold cargoHold = null, ShipLoadout loadout = null)
        {
            CargoPod nearestPod = GetNearestCargoPod(playerPosition);
            if (nearestPod == null)
            {
                return null;
            }

            if (nearestPod.IsConsumable)
            {
                ConsumableEquipmentDefinition consumable = nearestPod.GetConsumable();
                if (consumable == null)
                {
                    return "Hold P: Tractor Consumable";
                }

                if (loadout != null && loadout.CombatConsumables.GetAvailableCapacity(consumable.Id) <= 0)
                {
                    return "Consumable storage full";
                }

                float consumableDistance = Vector3.Distance(playerPosition, nearestPod.Position);
                return $"Hold P: Tractor {consumable.Name} x{nearestPod.Quantity}\n{FormatDistance(consumableDistance)}";
            }

            if (nearestPod.IsEquipment)
            {
                EquipmentDefinition equipment = nearestPod.GetEquipment();
                if (equipment == null)
                {
                    return "Hold P: Tractor Equipment";
                }

                if (loadout != null && loadout.AvailableOwnedEquipmentCapacity <= 0)
                {
                    return "Equipment storage full";
                }

                float equipmentDistance = Vector3.Distance(playerPosition, nearestPod.Position);
                return $"Hold P: Tractor {equipment.Name}\n{FormatDistance(equipmentDistance)}";
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
                EquipmentDefinition equipment = pod.GetEquipment();
                Color color = pod.IsConsumable
                    ? Color.LimeGreen
                    : equipment != null ? Color.Orange : commodity?.DisplayColor ?? Color.White;
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
            _lastLoadout = null;
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
            string name = pod?.GetPayloadName() ?? "unknown";
            return $"{name} x{pod?.Quantity ?? 0}";
        }
    }
}
