using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

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
        private Func<NpcShip, IReadOnlyList<SalvageDrop>> _economicSalvageResolver;
        private Func<NpcShip, string, int, int> _economicSalvageConsumed;
        private Func<NpcShip, string, int, MissionCargoDrop> _economicMissionCargoResolver;
        private Action<MissionCargoDrop> _economicCargoUnavailableCallback;

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

        public void ConfigureEconomicCargoCallbacks(
            Func<NpcShip, IReadOnlyList<SalvageDrop>> salvageResolver,
            Func<NpcShip, string, int, int> salvageConsumed,
            Func<NpcShip, string, int, MissionCargoDrop> economicMissionCargoResolver = null,
            Action<MissionCargoDrop> economicCargoUnavailable = null)
        {
            _economicSalvageResolver = salvageResolver;
            _economicSalvageConsumed = salvageConsumed;
            _economicMissionCargoResolver = economicMissionCargoResolver;
            _economicCargoUnavailableCallback = economicCargoUnavailable;
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
                    IsStolen = pod.IsStolen,
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
                pod.SetStolenProvenance(data.IsStolen);
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
        /// Captures every durable commodity pod. Existing mission-only save
        /// callers remain supported; this broader seam lets stolen extortion
        /// pods preserve provenance across a normal save/load.
        /// </summary>
        public List<SaveCargoPodData> CaptureCargoPods()
        {
            return _activePods
                .Where(pod => pod != null && !pod.IsDepleted && !pod.IsExpired &&
                    !string.IsNullOrWhiteSpace(pod.CommodityId) &&
                    TradeLaneStateSanitizer.IsFinite(pod.Position) &&
                    TradeLaneStateSanitizer.IsFinite(pod.Velocity))
                .Select(pod => new SaveCargoPodData
                {
                    MissionId = pod.MissionId,
                    MissionCargoSourceIndex = pod.MissionCargoSourceIndex,
                    CommodityId = pod.CommodityId,
                    Quantity = Math.Clamp(pod.Quantity, 1, 40),
                    IsStolen = pod.IsStolen,
                    AgeSeconds = MathHelper.Clamp(pod.AgeSeconds, 0f, pod.LifetimeSeconds),
                    Position = SaveVector3Data.From(pod.Position),
                    Velocity = SaveVector3Data.From(pod.Velocity),
                    SourceNpcName = pod.SourceNpcName ?? string.Empty
                })
                .ToList();
        }

        public int RestoreCargoPods(
            IEnumerable<SaveCargoPodData> savedPods,
            Func<int, bool> missionActivePredicate = null)
        {
            if (savedPods == null)
                return 0;

            int restored = 0;
            foreach (SaveCargoPodData data in savedPods)
            {
                if (data == null || data.Quantity <= 0 || string.IsNullOrWhiteSpace(data.CommodityId) ||
                    (data.MissionId > 0 && missionActivePredicate != null && !missionActivePredicate(data.MissionId)))
                    continue;

                Commodity commodity = CommodityCatalog.GetById(data.CommodityId);
                Vector3 position = data.Position?.ToVector3() ?? Vector3.Zero;
                Vector3 velocity = data.Velocity?.ToVector3() ?? Vector3.Zero;
                float age = float.IsNaN(data.AgeSeconds) || float.IsInfinity(data.AgeSeconds)
                    ? 0f
                    : Math.Max(0f, data.AgeSeconds);
                if (commodity == null || age >= CombatSalvageService.SalvageLifetimeSeconds ||
                    _activePods.Count >= CombatSalvageService.MaxLiveSalvageObjects ||
                    !TradeLaneStateSanitizer.IsFinite(position) || !TradeLaneStateSanitizer.IsFinite(velocity) ||
                    !IsSpawnPositionAvailable(position, null) ||
                    !CargoPod.TryCreate(commodity.Id, Math.Clamp(data.Quantity, 1, 40), position, velocity,
                        (float)CombatSalvageService.SalvageLifetimeSeconds, CombatSalvageService.PickupRadius,
                        out CargoPod pod))
                    continue;

                pod.SetStolenProvenance(data.IsStolen);
                pod.SetMissionCargoAttribution(data.MissionId, data.MissionCargoSourceIndex, data.SourceNpcName);
                pod.RestoreAge(age);
                _activePods.Add(pod);
                restored++;
            }

            return restored;
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

            MissionCargoReservation reservation = cargoHold.GetMissionCargoReservations()
                .FirstOrDefault(candidate => candidate.MissionId == missionId);
            if (!cargoHold.RemoveMissionCargoQuantity(missionId, commodity, quantity, out int removed) || removed != quantity)
                return false;

            createdPod.SetProvenance(reservation?.Provenance ?? CargoProvenance.Clean);
            createdPod.SetMissionCargoAttribution(missionId, -1, "Player jettison");
            _activePods.Add(createdPod);
            pod = createdPod;
            return true;
        }

        /// <summary>
        /// Converts every current Police-violating stack into bounded physical
        /// pods. The legacy method name is retained for Phase 52 callers;
        /// legal stolen cargo is now included and keeps its provenance.
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
                if (commodity == null || reservation.Quantity <= 0 ||
                    (commodity.IsContraband != true && !reservation.IsStolen))
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
                if (commodity == null)
                    continue;

                int stolenRemaining = Math.Min(
                    Math.Max(0, entry.Value),
                    cargoHold.GetSellableStolenCommodityQuantity(entry.Key));
                while (stolenRemaining > 0)
                {
                    int quantity = Math.Min(40, stolenRemaining);
                    Vector3 podPosition = position + new Vector3((stackIndex + 1) * 90f, 0f, 0f);
                    if (!TryJettisonOrdinaryCargo(
                            cargoHold,
                            commodity,
                            quantity,
                            podPosition,
                            velocity,
                            CargoProvenance.Stolen,
                            out _))
                    {
                        break;
                    }

                    removedQuantity += quantity;
                    podCount++;
                    stolenRemaining -= quantity;
                    stackIndex++;
                }

                if (commodity.IsContraband)
                {
                    int cleanRemaining = cargoHold.GetSellableCleanCommodityQuantity(entry.Key);
                    while (cleanRemaining > 0)
                    {
                        int quantity = Math.Min(40, cleanRemaining);
                        Vector3 podPosition = position + new Vector3((stackIndex + 1) * 90f, 0f, 0f);
                        if (!TryJettisonOrdinaryCargo(
                                cargoHold,
                                commodity,
                                quantity,
                                podPosition,
                                velocity,
                                CargoProvenance.Clean,
                                out _))
                            break;

                        removedQuantity += quantity;
                        podCount++;
                        cleanRemaining -= quantity;
                        stackIndex++;
                    }
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
            CargoProvenance provenance,
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

            bool removed = provenance == CargoProvenance.Stolen
                ? cargoHold.RemoveStolenCommodity(commodity, quantity)
                : cargoHold.RemoveCommodity(commodity, quantity);
            if (!removed)
                return false;

            createdPod.SetProvenance(provenance);
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
                missionPod.SetStolenProvenance(IsPlayerPiracySource(destroyedShip));
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

            IReadOnlyList<SalvageDrop> economicDrops = _economicSalvageResolver?.Invoke(destroyedShip);
            bool hasEconomicCargo = economicDrops != null;
            IReadOnlyList<SalvageDrop> regularDrops = _salvageService.EvaluateDestruction(destroyedShip);
            IReadOnlyList<SalvageDrop> drops = hasEconomicCargo
                ? regularDrops.Where(drop => drop != null && !drop.IsCommodity).Concat(economicDrops).ToList()
                : regularDrops;
            if (drops.Count == 0 || availableSlots <= 0)
            {
                if (hasEconomicCargo)
                {
                    foreach (SalvageDrop drop in economicDrops.Where(candidate => candidate?.IsCommodity == true))
                    {
                        MissionCargoDrop unavailable = _economicMissionCargoResolver?.Invoke(
                            destroyedShip,
                            drop.CommodityId,
                            drop.Quantity);
                        if (unavailable != null)
                            _economicCargoUnavailableCallback?.Invoke(unavailable);
                    }
                }
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
                    if (drop.IsCommodity)
                    {
                        MissionCargoDrop unavailable = _economicMissionCargoResolver?.Invoke(
                            destroyedShip,
                            drop.CommodityId,
                            drop.Quantity);
                        if (unavailable != null)
                            _economicCargoUnavailableCallback?.Invoke(unavailable);
                    }
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
                    if (drop.IsCommodity)
                    {
                        MissionCargoDrop unavailable = _economicMissionCargoResolver?.Invoke(
                            destroyedShip,
                            drop.CommodityId,
                            drop.Quantity);
                        if (unavailable != null)
                            _economicCargoUnavailableCallback?.Invoke(unavailable);
                    }
                    continue;
                }

                pod.SetSalvageSource(destroyedShip, drop.Tier);
                int economicQuantity = drop.Quantity;
                if (hasEconomicCargo && drop.IsCommodity)
                {
                    economicQuantity = Math.Clamp(
                        _economicSalvageConsumed?.Invoke(destroyedShip, drop.CommodityId, drop.Quantity) ?? 0,
                        0,
                        drop.Quantity);
                    if (economicQuantity <= 0)
                        continue;
                    if (economicQuantity < drop.Quantity)
                        pod.TakeQuantity(drop.Quantity - economicQuantity);
                }
                MissionCargoDrop economicMissionDrop = drop.IsCommodity
                    ? _economicMissionCargoResolver?.Invoke(destroyedShip, drop.CommodityId, economicQuantity)
                    : null;
                if (economicMissionDrop != null)
                {
                    pod.SetMissionCargoAttribution(
                        economicMissionDrop.MissionId,
                        economicMissionDrop.SourceIndex,
                        economicMissionDrop.SourceNpcName);
                    _missionCargoPodSpawnedCallback?.Invoke(pod);
                }
                if (drop.IsCommodity &&
                    (IsPlayerPiracySource(destroyedShip) || economicMissionDrop != null))
                    pod.SetStolenProvenance(true);
                _activePods.Add(pod);
                spawned++;
                log?.Invoke($"[SALVAGE] pod spawned: {pod.GetPayloadName()} x{pod.Quantity}");
            }

            return spawned;
        }

        public int SpawnSalvageForDestroyedNpc(NpcShip destroyedShip, Action<string> log = null) =>
            SpawnLootForDestroyedNpc(destroyedShip, log);

        /// <summary>
        /// Releases one canonical trader cargo stack into the ordinary
        /// physical loot system. The caller owns the NPC manifest mutation;
        /// this method only creates a real quantity-bearing CargoPod and
        /// reports the quantity that was actually released.
        /// </summary>
        public int SpawnExtortionCargo(
            NpcShip trader,
            string commodityId,
            int quantity,
            out int podCount,
            Action<string> log = null)
        {
            podCount = 0;
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            int safeQuantity = Math.Clamp(quantity, 1, 40);
            if (trader == null || trader.IsDestroyed || commodity == null || commodity.IsMissionCargo ||
                safeQuantity <= 0 || _activePods.Count >= CombatSalvageService.MaxLiveSalvageObjects)
            {
                return 0;
            }

            Vector3[] offsets =
            {
                new Vector3(110f, 0f, 0f),
                new Vector3(-110f, 35f, 0f),
                new Vector3(0f, -35f, 110f),
                new Vector3(0f, 20f, -110f)
            };
            for (int attempt = 0; attempt < offsets.Length; attempt++)
            {
                Vector3 position = trader.Position + offsets[attempt];
                if (!TradeLaneStateSanitizer.IsFinite(position) ||
                    !IsSpawnPositionAvailable(position, trader) ||
                    !CargoPod.TryCreate(
                        commodity.Id,
                        safeQuantity,
                        position,
                        trader.Velocity * 0.15f,
                        (float)CombatSalvageService.SalvageLifetimeSeconds,
                        CombatSalvageService.PickupRadius,
                        out CargoPod pod))
                {
                    continue;
                }

                pod.SetSalvageSource(trader, CombatSalvageTier.Standard);
                pod.SetStolenProvenance(true);
                MissionCargoDrop missionDrop = _economicMissionCargoResolver?.Invoke(trader, commodity.Id, safeQuantity);
                if (missionDrop != null)
                {
                    pod.SetMissionCargoAttribution(
                        missionDrop.MissionId,
                        missionDrop.SourceIndex,
                        missionDrop.SourceNpcName);
                    _missionCargoPodSpawnedCallback?.Invoke(pod);
                }
                _activePods.Add(pod);
                podCount = 1;
                log?.Invoke($"[PIRACY] cargo pod spawned: {pod.GetPayloadName()} x{pod.Quantity}");
                return safeQuantity;
            }

            return 0;
        }

        /// <summary>
        /// Releases a bounded stolen commodity pod from a destroyed or
        /// escaping NPC raider. Unlike SpawnExtortionCargo this accepts a
        /// destroyed source because the source's already-carried haul is the
        /// only quantity being reintroduced physically.
        /// </summary>
        public int SpawnStolenCargo(
            NpcShip source,
            string commodityId,
            int quantity,
            out int podCount,
            Action<string> log = null)
        {
            podCount = 0;
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            int safeQuantity = Math.Clamp(quantity, 1, 40);
            if (source == null || commodity == null || commodity.IsMissionCargo ||
                safeQuantity <= 0 || _activePods.Count >= CombatSalvageService.MaxLiveSalvageObjects)
                return 0;

            Vector3[] offsets =
            {
                new Vector3(110f, 0f, 0f),
                new Vector3(-110f, 35f, 0f),
                new Vector3(0f, -35f, 110f),
                new Vector3(0f, 20f, -110f)
            };
            for (int attempt = 0; attempt < offsets.Length; attempt++)
            {
                Vector3 position = source.Position + offsets[attempt];
                if (!TradeLaneStateSanitizer.IsFinite(position) ||
                    !IsSpawnPositionAvailable(position, source) ||
                    !CargoPod.TryCreate(
                        commodity.Id,
                        safeQuantity,
                        position,
                        source.Velocity * 0.15f,
                        (float)CombatSalvageService.SalvageLifetimeSeconds,
                        CombatSalvageService.PickupRadius,
                        out CargoPod pod))
                    continue;

                pod.SetSalvageSource(source, CombatSalvageTier.Standard);
                pod.SetStolenProvenance(true);
                _activePods.Add(pod);
                podCount = 1;
                log?.Invoke($"[PIRACY] raider haul dropped: {pod.GetPayloadName()} x{pod.Quantity}");
                return safeQuantity;
            }

            return 0;
        }

        /// <summary>
        /// Performs the same close-range physical pickup as the player path,
        /// but transfers only to the caller's bounded ambient raid haul. The
        /// pod is removed exactly once, so player/raider competition cannot
        /// duplicate cargo.
        /// </summary>
        public bool TryCollectCargoPodForNpc(
            NpcShip collector,
            CargoPod pod,
            int maximumQuantity,
            out string commodityId,
            out int collectedQuantity)
        {
            commodityId = string.Empty;
            collectedQuantity = 0;
            if (collector == null || collector.IsDestroyed || pod == null || pod.IsDepleted ||
                pod.IsExpired || !pod.IsStolen || !pod.IsWithinPickupRange(collector.Position) ||
                !_activePods.Contains(pod) || pod.PayloadType != CargoPodPayloadType.Commodity)
            {
                if (pod?.PayloadType == CargoPodPayloadType.Commodity)
                    commodityId = pod.CommodityId ?? string.Empty;
                return false;
            }

            Commodity commodity = pod.GetCommodity();
            if (commodity == null || maximumQuantity <= 0)
                return false;

            collectedQuantity = pod.TakeQuantity(Math.Min(maximumQuantity, pod.Quantity));
            if (collectedQuantity <= 0)
                return false;

            commodityId = commodity.Id;
            if (pod.IsDepleted)
                _activePods.Remove(pod);
            return true;
        }

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
                    ? cargoHold.TryAddMissionCommodityPartial(
                        pod.MissionId,
                        commodity,
                        pod.Quantity,
                        pod.Provenance,
                        out collectedQuantity)
                    : cargoHold.TryAddCommodityPartial(
                        commodity,
                        pod.Quantity,
                        pod.Provenance,
                        out collectedQuantity);
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

        private static bool IsPlayerPiracySource(NpcShip ship)
        {
            return ship != null && ship.WasDamagedByPlayer &&
                string.Equals(ship.FactionId, FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase) &&
                ship.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute;
        }
    }
}
