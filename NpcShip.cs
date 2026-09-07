using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;

namespace Roguelancer
{
    /// <summary>
    /// Compact ambient traffic behaviors used by the encounter manager.
    /// </summary>
    public enum TrafficZoneBehaviorType
    {
        LawfulPatrol,
        TraderRoute,
        PirateAmbush,
        StationTraffic
    }

    /// <summary>
    /// Compact encounter states layered on top of the ambient traffic behavior.
    /// </summary>
    public enum TrafficEncounterState
    {
        Cruising,
        Fleeing,
        AttackingTrader,
        AttackingPlayer,
        InterceptingPirate,
        AttackingFactionNpc
    }

    public enum NpcPlayerTargetReason
    {
        None,
        FactionDisposition,
        PlayerInitiatedAggression,
        FugitivePursuit
    }

    public enum FactionCombatTargetOrigin
    {
        OrdinaryAcquisition,
        DistressResponse,
        EscalationResponse,
        MissionObjective
    }

    /// <summary>
    /// Authoritative source of the destroying hull hit for one NPC instance.
    /// This is transient runtime combat state and is not save data.
    /// </summary>
    public enum NpcDestructionSource
    {
        Unknown,
        Player,
        Npc,
        Environment
    }

    /// <summary>
    /// NPC ship with simple patrol behavior
    /// </summary>
    public class NpcShip : SpaceObject
    {
        public Matrix Orientation => Matrix.CreateFromQuaternion(_rotation);
        public Vector3 Velocity { get; set; }
        public float Speed { get; private set; }
        public Matrix ModelRotationCorrection { get; set; } = Matrix.Identity;
        public string ModelPath { get; set; }
        public string FactionId { get; set; }
        /// <summary>Stable identity for bounded world interactions.</summary>
        public string StableIdentity { get; private set; } = string.Empty;
        /// <summary>
        /// Durable-definition snapshot for the equipment this NPC carried.
        /// Runtime NPC projectiles remain separate from this metadata so a
        /// destruction event can safely derive canonical salvage IDs.
        /// </summary>
        public ShipLoadout Loadout { get; private set; } = ShipLoadout.CreateStarterLoadout(false);
        public TrafficZoneBehaviorType TrafficBehavior { get; private set; } = TrafficZoneBehaviorType.LawfulPatrol;
        public string TrafficZoneId { get; private set; } = string.Empty;
        public float TrafficLifetimeSeconds { get; set; } = 0f;
        public float TrafficAgeSeconds { get; private set; } = 0f;
        public float TrafficCruiseSpeed { get; private set; } = 160f;
        public float TrafficActivationRange { get; private set; } = 6500f;
        public float TrafficLoiterRadius { get; private set; } = 900f;
        public Vector3? TrafficRouteStart { get; private set; }
        public Vector3? TrafficRouteEnd { get; private set; }
        public bool IsTradeLaneTransit { get; private set; }
        public string TradeLaneId { get; private set; } = string.Empty;
        public TradeLaneDirection? TradeLaneDirection { get; private set; }
        public int TradeLaneRingIndex { get; private set; } = -1;
        public bool IsMissionHoldPosition { get; private set; }
        public TrafficEncounterState EncounterState { get; private set; } = TrafficEncounterState.Cruising;
        public Vector3? EncounterTargetPosition { get; private set; }
        public Vector3? EncounterEscapePosition { get; private set; }
        public NpcShip FactionCombatTarget { get; private set; }
        public FactionCombatTargetOrigin FactionCombatTargetOrigin { get; private set; } = FactionCombatTargetOrigin.OrdinaryAcquisition;
        public bool IsTrafficEngaged => EncounterState != TrafficEncounterState.Cruising;
        public NpcPlayerTargetReason PlayerTargetReason { get; private set; } = NpcPlayerTargetReason.None;
        public bool HasPlayerTarget => EncounterState == TrafficEncounterState.AttackingPlayer && PlayerTargetReason != NpcPlayerTargetReason.None;
        public bool HasFactionDerivedPlayerTarget => HasPlayerTarget && PlayerTargetReason == NpcPlayerTargetReason.FactionDisposition;
        public bool HasPlayerInitiatedRetaliationTarget => HasPlayerTarget && PlayerTargetReason == NpcPlayerTargetReason.PlayerInitiatedAggression;

        // The legacy NpcShip-only update path retains its original local
        // activation-range guard. TrafficManager enables this transient flag
        // through FactionCombatDisengagementService so Phase 35 can own the
        // wider soft/hard pursuit leash without persisting combat state.
        internal bool IsFactionCombatDisengagementManaged { get; private set; }

        // Hull integrity
        public HullIntegrity Hull { get; private set; }
        public bool IsDestroyed => Hull.IsDestroyed;
        public bool WasDamagedByPlayer { get; private set; }
        public int PlayerDamageSequence { get; private set; }
        public float LastPlayerDamage { get; private set; }
        public bool HasPlayerAggressionProvenance { get; private set; }
        public bool WasFactionHostileBeforePlayerAggression { get; private set; }
        public NpcDestructionSource DestructionSource { get; private set; } = NpcDestructionSource.Unknown;
        public bool WasAliveImmediatelyBeforeDestruction { get; private set; }

        private NpcDestructionSource _pendingDestructionSource = NpcDestructionSource.Unknown;

        /// <summary>
        /// Transient provenance for ships created by a faction distress
        /// response. This is intentionally not part of save data and is used
        /// to keep reinforcement combat from recursively requesting waves.
        /// </summary>
        public bool IsDistressReinforcement { get; private set; }
        public string DistressReinforcementEncounterId { get; private set; } = string.Empty;

        /// <summary>
        /// Transient provenance for ships created by the Phase 34 second-stage
        /// response. Keeping this separate from ordinary distress provenance
        /// lets both services reject responder-generated combat explicitly.
        /// </summary>
        public bool IsEscalationReinforcement { get; private set; }
        public string EscalationReinforcementEncounterId { get; private set; } = string.Empty;
        public bool IsFactionTransientReinforcement => IsDistressReinforcement || IsEscalationReinforcement;

        public float CombatConsumableCooldownRemaining { get; private set; }

        /// <summary>
        /// Marks the authoritative player damage source used by bounded
        /// combat consequence attribution. It is intentionally one-way for
        /// the lifetime of an NPC instance.
        /// </summary>
        public bool MarkDamagedByPlayer(float damage = 1f)
        {
            if (IsDestroyed || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return false;

            WasDamagedByPlayer = true;
            LastPlayerDamage = damage;
            PlayerDamageSequence = PlayerDamageSequence == int.MaxValue ? 1 : PlayerDamageSequence + 1;
            return true;
        }

        /// <summary>
        /// Applies hull damage through the existing NPC damage boundary while
        /// preserving the source used if this hit causes destruction. Shield
        /// absorption remains the caller's responsibility.
        /// </summary>
        public bool ApplyDamage(float damage, NpcDestructionSource source)
        {
            if (IsDestroyed || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return false;

            _pendingDestructionSource = source;
            try
            {
                return Hull.TakeDamage(damage);
            }
            finally
            {
                _pendingDestructionSource = NpcDestructionSource.Unknown;
            }
        }

        internal void MarkDistressReinforcement(string encounterId)
        {
            IsDistressReinforcement = true;
            DistressReinforcementEncounterId = encounterId ?? string.Empty;
        }

        internal void ClearDistressReinforcementProvenance()
        {
            IsDistressReinforcement = false;
            DistressReinforcementEncounterId = string.Empty;
        }

        internal void MarkEscalationReinforcement(string encounterId)
        {
            IsEscalationReinforcement = true;
            EscalationReinforcementEncounterId = encounterId ?? string.Empty;
        }

        internal void ClearEscalationReinforcementProvenance()
        {
            IsEscalationReinforcement = false;
            EscalationReinforcementEncounterId = string.Empty;
        }

        internal bool CapturePlayerAggressionProvenance(bool wasFactionHostile)
        {
            if (HasPlayerAggressionProvenance)
                return false;

            HasPlayerAggressionProvenance = true;
            WasFactionHostileBeforePlayerAggression = wasFactionHostile;
            return true;
        }

        internal void ResetPlayerCombatAttribution()
        {
            WasDamagedByPlayer = false;
            PlayerDamageSequence = 0;
            LastPlayerDamage = 0f;
            HasPlayerAggressionProvenance = false;
            WasFactionHostileBeforePlayerAggression = false;
        }

        // Shield system
        public ShieldSystem Shields { get; private set; }

        // Weapon energy is independent from the player's movement energy and
        // is bound to the NPC's canonical mounted powerplant.
        public WeaponEnergy WeaponEnergy { get; private set; }
        public ThrusterEnergy ThrusterEnergy { get; private set; }
        public bool IsAfterburnerActive { get; private set; }
        public CruiseDrive CruiseDrive { get; } = new CruiseDrive();
        public bool IsCruiseActive => CruiseDrive.IsActive;
        public bool IsCruiseCharging => CruiseDrive.IsCharging;
        public float CruiseChargeProgress => CruiseDrive.ChargeProgress;
        public string CruiseHudText => CruiseDrive.GetHudText();

        // Combat pursuit keeps the established afterburn behavior through
        // 10km; intrinsic cruise begins only on genuinely long legs.
        private const float NpcCruiseStartDistance = 11000f;
        private const float NpcCruiseExitDistance = 4500f;
        
        // Event to signal when the ship is destroyed
        public event Action<NpcShip> OnDestroyed;

        // Presentation-only observation hook. The communication layer may
        // listen for a new player target without gaining authority over the
        // target itself.
        public event Action<NpcShip> PlayerTargetAcquired;

        /// <summary>
        /// Fires before shield absorption for qualifying combat hits so lane
        /// transit can eject safely even when shields absorb the whole hit.
        /// </summary>
        public event Action<NpcShip, float, NpcDestructionSource, bool> CombatDamageReceived;

        private float _patrolRadius;
        private Vector3 _patrolCenter;
        private float _patrolAngle;
        private float _patrolSpeed;
        private float _bobPhase;
        private float _bobSpeed;
        private float _trafficRouteHoldTimer;
        private bool _trafficRouteTowardEnd = true;
        private Vector3 _missionHoldAnchor;
        private string _stableIdentitySeed = string.Empty;
        private Quaternion _rotation = Quaternion.Identity; // Use Quaternion instead of Matrix
        
        public Vector3 Forward => Vector3.Transform(Vector3.Forward, _rotation);
        public Vector3 Up => Vector3.Transform(Vector3.Up, _rotation);
        public Vector3 Right => Vector3.Transform(Vector3.Right, _rotation);
        
        public NpcShip(string name, Vector3 startPosition, Vector3 patrolCenter, float patrolRadius, float patrolSpeed, string factionId = null)
            : base(name, startPosition, 10f)
        {
            _patrolCenter = patrolCenter;
            _patrolRadius = patrolRadius;
            _patrolSpeed = patrolSpeed;
            FactionId = FactionManager.NormalizeFactionId(factionId);
            _stableIdentitySeed = BuildStableIdentitySeed(name, startPosition, FactionId);
            StableIdentity = _stableIdentitySeed;
            // Stable presentation phase: traffic direction and movement must
            // remain repeatable across smoke runs and new sessions.
            int visualSeed = 17;
            foreach (char character in name ?? string.Empty)
                visualSeed = unchecked(visualSeed * 31 + character);
            visualSeed = unchecked(visualSeed + (int)(startPosition.X * 3f) + (int)(startPosition.Z * 5f));
            float normalizedSeed = (Math.Abs(visualSeed) % 10000) / 10000f;
            _bobPhase = normalizedSeed * MathHelper.TwoPi;
            _bobSpeed = 0.3f + normalizedSeed * 0.4f;
            
            // Initialize hull integrity
            Hull = new HullIntegrity(75f); // NPCs start with 75 hull points
            Hull.OnDestroyed += () =>
            {
                IsAfterburnerActive = false;
                CruiseDrive.Reset();
                WasAliveImmediatelyBeforeDestruction = true;
                DestructionSource = _pendingDestructionSource;
                Console.WriteLine($"NPC SHIP '{Name}' DESTROYED!");
                OnDestroyed?.Invoke(this);
            };
            
            Console.WriteLine($"[NPC] {name} created with Hull: {Hull.CurrentHull}/{Hull.MaxHull}, IsDestroyed: {Hull.IsDestroyed}");
            
            // NPC shield state is bound to the deterministic canonical loadout
            // policy. The factory supplies a modest defensive shield even for
            // unarmed civilian traffic where appropriate.
            Shields = new ShieldSystem();
            SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                name,
                FactionId,
                null,
                TrafficZoneBehaviorType.LawfulPatrol,
                NpcLoadoutTier.Standard));
            
            // Initial orientation facing toward patrol center
            Vector3 toCenter = patrolCenter - startPosition;
            
            // Handle case where patrol center equals start position (static ships)
            if (toCenter.LengthSquared() < 0.0001f)
            {
                // Default to facing forward for static ships
                _rotation = Quaternion.Identity;
            }
            else
            {
                toCenter = Vector3.Normalize(toCenter);
                _rotation = CreateRotationFromDirection(toCenter);
            }
            
            // Calculate initial patrol angle
            Vector3 offset = startPosition - patrolCenter;
            _patrolAngle = (float)Math.Atan2(offset.X, offset.Z);
        }

        public void ConfigureTrafficBehavior(
            TrafficZoneBehaviorType behaviorType,
            string trafficZoneId,
            Vector3 anchorPoint,
            float loiterRadius,
            float cruiseSpeed,
            float activationRange = 6500f,
            Vector3? routeStart = null,
            Vector3? routeEnd = null)
        {
            TrafficBehavior = behaviorType;
            TrafficZoneId = trafficZoneId ?? string.Empty;
            _patrolCenter = anchorPoint;
            TrafficLoiterRadius = Math.Max(100f, loiterRadius);
            TrafficCruiseSpeed = Math.Max(20f, cruiseSpeed);
            TrafficActivationRange = Math.Max(100f, activationRange);
            TrafficRouteStart = routeStart;
            TrafficRouteEnd = routeEnd;
            _trafficRouteHoldTimer = 0f;
            StableIdentity = string.IsNullOrWhiteSpace(TrafficZoneId)
                ? _stableIdentitySeed
                : $"{_stableIdentitySeed}|traffic:{TrafficZoneId}";
            ClearEncounterState();

            if (routeStart.HasValue && routeEnd.HasValue)
            {
                _trafficRouteTowardEnd = Vector3.DistanceSquared(Position, routeStart.Value) <= Vector3.DistanceSquared(Position, routeEnd.Value);
                if (Vector3.DistanceSquared(Position, routeStart.Value) < 25f)
                {
                    _trafficRouteTowardEnd = true;
                }
                else if (Vector3.DistanceSquared(Position, routeEnd.Value) < 25f)
                {
                    _trafficRouteTowardEnd = false;
                }
            }
        }

        public void SetLoadout(ShipLoadout loadout)
        {
            Loadout = loadout ?? ShipLoadout.CreateStarterLoadout(false);
            CombatConsumableCooldownRemaining = 0f;
            CruiseDrive.Reset();
            RefreshShieldFromLoadout();
            RefreshWeaponEnergyFromLoadout();
            RefreshThrusterFromLoadout();
        }

        internal void StartCombatConsumableCooldown(float seconds)
        {
            CombatConsumableCooldownRemaining = Math.Max(
                CombatConsumableCooldownRemaining,
                float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Math.Max(0f, seconds));
        }

        public void ResetCombatConsumableState()
        {
            CombatConsumableCooldownRemaining = 0f;
        }

        public void RefreshShieldFromLoadout()
        {
            ShieldEquipmentDefinition shield = Loadout?.GetMountedShield();
            Shields = shield == null
                ? new ShieldSystem()
                : new ShieldSystem(shield);
        }

        public void RefreshWeaponEnergyFromLoadout()
        {
            PowerplantEquipmentDefinition powerplant = Loadout?.GetMountedPowerplant();
            WeaponEnergy = new WeaponEnergy();
            WeaponEnergy.Configure(powerplant, restoreFull: true);
        }

        public void RefreshThrusterFromLoadout()
        {
            ThrusterEquipmentDefinition thruster = Loadout?.GetMountedThruster();
            ThrusterEnergy = new ThrusterEnergy();
            ThrusterEnergy.Configure(thruster, restoreFull: true);
            IsAfterburnerActive = false;
        }

        /// <summary>
        /// Authoritative NPC damage boundary. Shield-only positive hostile
        /// hits disrupt cruise before absorption; direct test/environmental
        /// Hull.TakeDamage calls remain intentionally non-attributed.
        /// </summary>
        public bool ApplyCombatDamage(float damage, NpcDestructionSource source, bool hostile = true)
        {
            if (IsDestroyed || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return false;

            CruiseDrive.DisruptByDamage(damage, hostile);
            try
            {
                CombatDamageReceived?.Invoke(this, damage, source, hostile);
            }
            catch
            {
                // Optional observers cannot interrupt authoritative damage.
            }
            float hullDamage = Shields?.AbsorbDamage(damage) ?? damage;
            if (hullDamage > 0f)
            {
                ApplyDamage(hullDamage, source);
            }

            return true;
        }

        public void SetEncounterState(
            TrafficEncounterState encounterState,
            Vector3? targetPosition = null,
            Vector3? escapePosition = null,
            NpcPlayerTargetReason playerTargetReason = NpcPlayerTargetReason.None)
        {
            if (encounterState != TrafficEncounterState.AttackingFactionNpc)
            {
                FactionCombatTarget = null;
                FactionCombatTargetOrigin = FactionCombatTargetOrigin.OrdinaryAcquisition;
            }

            EncounterState = encounterState;
            EncounterTargetPosition = targetPosition;
            EncounterEscapePosition = escapePosition;
            if (!targetPosition.HasValue && !escapePosition.HasValue)
            {
                CruiseDrive.Cancel(CruiseCancellationReason.TargetInvalid);
            }
            PlayerTargetReason = encounterState == TrafficEncounterState.AttackingPlayer
                ? playerTargetReason
                : NpcPlayerTargetReason.None;
        }

        public void SetPlayerTarget(Vector3 targetPosition, NpcPlayerTargetReason reason)
        {
            bool wasPlayerTarget = HasPlayerTarget;
            SetEncounterState(TrafficEncounterState.AttackingPlayer, targetPosition, null, reason);
            if (wasPlayerTarget)
                return;

            try
            {
                PlayerTargetAcquired?.Invoke(this);
            }
            catch
            {
                // Communications are optional presentation. A failed
                // observer must never interrupt authoritative targeting.
            }
        }

        public bool SetFactionCombatTarget(
            NpcShip target,
            bool preserveExistingEncounterState = false,
            FactionCombatTargetOrigin targetOrigin = FactionCombatTargetOrigin.OrdinaryAcquisition)
        {
            bool validTarget = targetOrigin == FactionCombatTargetOrigin.MissionObjective
                ? NpcFactionCombatTargeting.IsValidMissionTarget(this, target)
                : NpcFactionCombatTargeting.IsValidHostileTarget(this, target);
            if (!validTarget)
                return false;

            FactionCombatTarget = target;
            FactionCombatTargetOrigin = targetOrigin;
            if (!preserveExistingEncounterState || EncounterState == TrafficEncounterState.Cruising ||
                EncounterState == TrafficEncounterState.AttackingFactionNpc)
            {
                EncounterState = TrafficEncounterState.AttackingFactionNpc;
                EncounterTargetPosition = target.Position;
                EncounterEscapePosition = null;
                PlayerTargetReason = NpcPlayerTargetReason.None;
            }
            else
            {
                // Legacy police/pirate encounter states remain authoritative
                // for movement while the explicit faction target drives NPC
                // weapon selection.
                EncounterTargetPosition = target.Position;
            }

            return true;
        }

        public bool HasValidFactionCombatTarget(float? maxDistance = null) =>
            FactionCombatTargetOrigin == FactionCombatTargetOrigin.MissionObjective
                ? NpcFactionCombatTargeting.IsValidMissionTarget(this, FactionCombatTarget, maxDistance)
                : NpcFactionCombatTargeting.IsValidHostileTarget(this, FactionCombatTarget, maxDistance);

        public void ClearFactionCombatTarget()
        {
            bool wasFactionCombat = EncounterState == TrafficEncounterState.AttackingFactionNpc;
            bool wasLegacyNpcCombat = EncounterState == TrafficEncounterState.AttackingTrader ||
                EncounterState == TrafficEncounterState.InterceptingPirate;

            FactionCombatTarget = null;
            FactionCombatTargetOrigin = FactionCombatTargetOrigin.OrdinaryAcquisition;
            IsFactionCombatDisengagementManaged = false;
            if (wasFactionCombat || wasLegacyNpcCombat)
            {
                CruiseDrive.Cancel(CruiseCancellationReason.TargetInvalid);
                EncounterState = TrafficEncounterState.Cruising;
                EncounterTargetPosition = null;
                EncounterEscapePosition = null;
                PlayerTargetReason = NpcPlayerTargetReason.None;
            }
        }

        public void ClearEncounterState()
        {
            IsAfterburnerActive = false;
            CruiseDrive.Cancel(CruiseCancellationReason.TargetInvalid);
            EncounterState = TrafficEncounterState.Cruising;
            EncounterTargetPosition = null;
            EncounterEscapePosition = null;
            PlayerTargetReason = NpcPlayerTargetReason.None;
            FactionCombatTarget = null;
            FactionCombatTargetOrigin = FactionCombatTargetOrigin.OrdinaryAcquisition;
            IsFactionCombatDisengagementManaged = false;
        }

        internal void SetFactionCombatDisengagementManaged(bool managed)
        {
            IsFactionCombatDisengagementManaged = managed;
        }

        public FactionDisposition GetPlayerDisposition(ReputationManager reputationManager) =>
            FactionDispositionEvaluator.Evaluate(FactionId, reputationManager);

        /// <summary>
        /// Returns whether the existing combat weapon loop may use the player
        /// as this NPC's current target. Reputation-derived targets are live;
        /// they are invalidated as soon as the relationship recovers.
        /// </summary>
        public bool HasValidPlayerTarget(ReputationManager reputationManager)
        {
            if (!HasPlayerTarget)
                return false;

            if (reputationManager == null)
                return true;

            // A fugitive pursuit is a bounded world incident rather than a
            // permanent standing change. Its owner refreshes temporary
            // hostility while the incident is active, so the existing
            // disengagement service can retain/reacquire this target without
            // inventing a second combat authority.
            if (PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit)
                return reputationManager.IsTemporarilyHostile(FactionId);

            return FactionDispositionEvaluator.IsHostile(FactionId, reputationManager);
        }
        
        public void Update(GameTime gameTime, DamageSmokeParticles damageSmoke, Ship playerShip = null, ReputationManager reputationManager = null)
        {
            if (IsDestroyed)
            {
                IsAfterburnerActive = false;
                CruiseDrive.Reset();
                ThrusterEnergy?.Advance(gameTime, afterburnRequested: false, shipAlive: false);
                return;
            }

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            CruiseDrive.Advance(deltaTime, shipAlive: true, docked: false, validMovement: true);

            // Weapon energy belongs to the ship runtime. Destroyed ships are
            // never updated and therefore never regenerate.
            WeaponEnergy?.Update(gameTime, shipAlive: true);

            if (TrafficLifetimeSeconds > 0f || !string.IsNullOrWhiteSpace(TrafficZoneId))
            {
                TrafficAgeSeconds += deltaTime;
            }

            // Update shield regeneration
            Shields?.Update(gameTime);

            CombatConsumableCooldownRemaining = Math.Max(
                0f,
                CombatConsumableCooldownRemaining - Math.Max(0f, deltaTime));
            CombatConsumableService.TryUseNpcConsumable(this, out _);

            // Emit damage smoke if hull is low
            DamageStage damageStage = DamageStage.None;
            if (Hull.HullPercentage <= 0.75f && Hull.HullPercentage > 0.50f)
            {
                damageStage = DamageStage.Light;
            }
            else if (Hull.HullPercentage <= 0.50f && Hull.HullPercentage > 0.25f)
            {
                damageStage = DamageStage.Heavy;
            }
            else if (Hull.HullPercentage <= 0.25f && Hull.HullPercentage > 0)
            {
                damageStage = DamageStage.Critical;
            }

            if (damageStage != DamageStage.None)
            {
                damageSmoke?.Emit(Position - Forward * 15, Velocity, damageStage);
            }

            if (IsTradeLaneTransit)
            {
                IsAfterburnerActive = false;
                CruiseDrive.Cancel(CruiseCancellationReason.IncompatibleFlight);
                ThrusterEnergy?.Advance(deltaTime, afterburnRequested: false, shipAlive: true);
                return;
            }

            if (IsMissionHoldPosition)
            {
                Position = _missionHoldAnchor;
                Velocity = Vector3.Zero;
                Speed = 0f;
                IsAfterburnerActive = false;
                CruiseDrive.Cancel(CruiseCancellationReason.IncompatibleFlight);
                ThrusterEnergy?.Advance(deltaTime, afterburnRequested: false, shipAlive: true);
                return;
            }

            bool factionTargetInvalid = FactionCombatTarget != null &&
                (!IsFactionCombatDisengagementManaged
                    ? !HasValidFactionCombatTarget(TrafficActivationRange)
                    : !HasValidFactionCombatTarget());
            if (factionTargetInvalid)
            {
                ClearFactionCombatTarget();
            }
            else if (FactionCombatTarget != null)
            {
                EncounterTargetPosition = FactionCombatTarget.Position;
            }

            UpdatePlayerDispositionTarget(playerShip, reputationManager);

            switch (EncounterState)
            {
                case TrafficEncounterState.Fleeing:
                    UpdateFleeBehavior(deltaTime);
                    return;
                case TrafficEncounterState.AttackingTrader:
                case TrafficEncounterState.AttackingPlayer:
                case TrafficEncounterState.InterceptingPirate:
                case TrafficEncounterState.AttackingFactionNpc:
                    UpdateEngagementBehavior(deltaTime);
                    return;
            }

            switch (TrafficBehavior)
            {
                case TrafficZoneBehaviorType.TraderRoute:
                    UpdateTraderRouteBehavior(deltaTime);
                    break;
                case TrafficZoneBehaviorType.PirateAmbush:
                    UpdatePirateAmbushBehavior(deltaTime, playerShip, reputationManager);
                    break;
                case TrafficZoneBehaviorType.StationTraffic:
                    UpdateCircularPatrolBehavior(deltaTime, TrafficLoiterRadius * 0.45f, TrafficCruiseSpeed * 0.75f, 0.9f);
                    break;
                default:
                    UpdateCircularPatrolBehavior(deltaTime, _patrolRadius, Math.Max(TrafficCruiseSpeed, 200f), _patrolSpeed);
                    break;
            }
        }
        
        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null || IsDestroyed) return;
            
            // Match player ship rendering exactly: scale, correction, then orientation
            Matrix modelScale = Matrix.CreateScale(0.1f);
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            Matrix world = modelScale * modelCorrection * ModelRotationCorrection * Orientation * Matrix.CreateTranslation(Position);
            
            foreach (ModelMesh mesh in Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = world;
                    effect.View = view;
                    effect.Projection = projection;
                    
                    effect.EnableDefaultLighting();
                    effect.PreferPerPixelLighting = true;
                    effect.SpecularPower = 16f;
                    effect.Alpha = 1.0f;
                    
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.9f, 1.0f);
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.5f, 0.5f, 0.6f);
                    effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.25f);
                }
                
                mesh.Draw();
            }
        }
        
        private Quaternion CreateRotationFromDirection(Vector3 direction)
        {
            Vector3 forward = Vector3.Normalize(direction);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));
            if (right.LengthSquared() < 0.01f) right = Vector3.Right;
            Vector3 up = Vector3.Cross(forward, right);
            
            Matrix rotationMatrix = new Matrix(
                right.X, right.Y, right.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                forward.X, forward.Y, forward.Z, 0f,
                0f, 0f, 0f, 1f
            );
            
            return Quaternion.CreateFromRotationMatrix(rotationMatrix);
        }

        public void SetFacing(Vector3 direction)
        {
            if (direction.LengthSquared() < 0.0001f || float.IsNaN(direction.LengthSquared()))
                return;
            direction.Normalize();
            _rotation = CreateRotationFromDirection(direction);
        }

        private void UpdateCircularPatrolBehavior(float deltaTime, float radius, float cruiseSpeed, float patrolSpeed)
        {
            _patrolAngle += patrolSpeed * deltaTime;
            _bobPhase += _bobSpeed * deltaTime;

            float bobHeight = (float)Math.Sin(_bobPhase) * (radius * 0.2f);
            Vector3 targetPosition = _patrolCenter + new Vector3(
                (float)Math.Sin(_patrolAngle) * radius,
                bobHeight,
                (float)Math.Cos(_patrolAngle) * radius
            );

            MoveTowardTarget(targetPosition, cruiseSpeed, deltaTime, 1.5f);
        }

        private void UpdateTraderRouteBehavior(float deltaTime)
        {
            if (!TrafficRouteStart.HasValue || !TrafficRouteEnd.HasValue)
            {
                UpdateCircularPatrolBehavior(deltaTime, _patrolRadius, Math.Max(TrafficCruiseSpeed, 160f), _patrolSpeed);
                return;
            }

            Vector3 target = _trafficRouteTowardEnd ? TrafficRouteEnd.Value : TrafficRouteStart.Value;
            float distance = Vector3.Distance(Position, target);
            if (distance <= 180f)
            {
                _trafficRouteHoldTimer += deltaTime;
                Speed = MathHelper.Lerp(Speed, 40f, deltaTime * 2f);
                if (_trafficRouteHoldTimer >= 1.0f)
                {
                    _trafficRouteTowardEnd = !_trafficRouteTowardEnd;
                    _trafficRouteHoldTimer = 0f;
                }
                IsAfterburnerActive = false;
                ThrusterEnergy?.Advance(deltaTime, afterburnRequested: false, shipAlive: true);
            }
            else
            {
                _trafficRouteHoldTimer = 0f;
                MoveTowardTarget(target, TrafficCruiseSpeed, deltaTime, 1.2f);
                return;
            }

            Velocity = Forward * Speed;
            Position += Velocity * deltaTime;
        }

        private void UpdateFleeBehavior(float deltaTime)
        {
            Vector3 fleeTarget;
            if (EncounterEscapePosition.HasValue)
            {
                fleeTarget = EncounterEscapePosition.Value;
            }
            else if (TrafficRouteStart.HasValue && TrafficRouteEnd.HasValue)
            {
                if (EncounterTargetPosition.HasValue)
                {
                    fleeTarget = Vector3.DistanceSquared(EncounterTargetPosition.Value, TrafficRouteStart.Value) >= Vector3.DistanceSquared(EncounterTargetPosition.Value, TrafficRouteEnd.Value)
                        ? TrafficRouteStart.Value
                        : TrafficRouteEnd.Value;
                }
                else
                {
                    fleeTarget = Vector3.DistanceSquared(Position, TrafficRouteStart.Value) >= Vector3.DistanceSquared(Position, TrafficRouteEnd.Value)
                        ? TrafficRouteStart.Value
                        : TrafficRouteEnd.Value;
                }
            }
            else
            {
                Vector3 awayDirection = EncounterTargetPosition.HasValue ? Position - EncounterTargetPosition.Value : Forward;
                if (awayDirection.LengthSquared() < 0.0001f)
                {
                    awayDirection = Vector3.Forward;
                }
                else
                {
                    awayDirection = Vector3.Normalize(awayDirection);
                }

                fleeTarget = Position + awayDirection * Math.Max(1800f, TrafficCruiseSpeed * 8f);
            }

            MoveTowardTarget(fleeTarget, TrafficCruiseSpeed * 1.45f, deltaTime, 2.35f);
        }

        private void UpdateEngagementBehavior(float deltaTime)
        {
            Vector3 targetPosition = EncounterTargetPosition ?? TrafficRouteEnd ?? TrafficRouteStart ?? _patrolCenter;
            float speed = Math.Max(TrafficCruiseSpeed * 1.2f, 160f);

            if (EncounterState == TrafficEncounterState.InterceptingPirate)
            {
                speed = Math.Max(speed, 220f);
            }

            MoveTowardTarget(targetPosition, speed, deltaTime, 2.0f);
        }

        private void UpdatePlayerDispositionTarget(Ship playerShip, ReputationManager reputationManager)
        {
            bool isPlayerTarget = EncounterState == TrafficEncounterState.AttackingPlayer;
            bool factionHostile = FactionDispositionEvaluator.IsHostile(FactionId, reputationManager);
            bool playerAttackRetaliation = WasDamagedByPlayer &&
                reputationManager?.IsTemporarilyHostile(FactionId) == true;
            bool fugitivePursuit = PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit &&
                reputationManager?.IsTemporarilyHostile(FactionId) == true;

            // A null reputation manager is retained as a compatibility path
            // for the older ambient traffic harness. Production gameplay has
            // an authoritative manager and therefore uses live disposition.
            bool legacyPlayerTarget = reputationManager == null && isPlayerTarget;
            bool canTargetPlayer = factionHostile || playerAttackRetaliation || fugitivePursuit || legacyPlayerTarget;

            if (isPlayerTarget)
            {
                if (!canTargetPlayer)
                {
                    ClearEncounterState();
                    return;
                }

                EncounterTargetPosition = playerShip?.Position ?? EncounterTargetPosition;
                if (PlayerTargetReason == NpcPlayerTargetReason.None)
                {
                    PlayerTargetReason = playerAttackRetaliation
                        ? NpcPlayerTargetReason.PlayerInitiatedAggression
                        : NpcPlayerTargetReason.FactionDisposition;
                }

                return;
            }

            if (playerShip == null || !canTargetPlayer || IsTrafficEngaged)
                return;

            float activationRange = Math.Max(100f, TrafficActivationRange);
            if (Vector3.DistanceSquared(Position, playerShip.Position) > activationRange * activationRange)
                return;

            NpcPlayerTargetReason targetReason = playerAttackRetaliation && reputationManager?.IsHostile(FactionId) != true
                ? NpcPlayerTargetReason.PlayerInitiatedAggression
                : NpcPlayerTargetReason.FactionDisposition;
            SetPlayerTarget(playerShip.Position, targetReason);
        }

        private void UpdatePirateAmbushBehavior(float deltaTime, Ship playerShip, ReputationManager reputationManager)
        {
            bool isHostile = reputationManager == null || reputationManager.IsFactionCurrentlyHostile(FactionId);
            if (playerShip != null && isHostile)
            {
                float distanceToPlayer = Vector3.Distance(Position, playerShip.Position);
                if (distanceToPlayer <= TrafficActivationRange)
                {
                    MoveTowardTarget(playerShip.Position, TrafficCruiseSpeed * 1.15f, deltaTime, 1.7f);
                    return;
                }
            }

            UpdateCircularPatrolBehavior(deltaTime, TrafficLoiterRadius, Math.Max(80f, TrafficCruiseSpeed * 0.55f), 0.45f);
        }

        private void MoveTowardTarget(Vector3 targetPosition, float cruiseSpeed, float deltaTime, float maxTurnRate)
        {
            Vector3 toTarget = targetPosition - Position;
            float distanceToTarget = toTarget.Length();
            if (!IsFinitePositive(distanceToTarget))
            {
                IsAfterburnerActive = false;
                CruiseDrive.Cancel(CruiseCancellationReason.TargetInvalid);
                ThrusterEnergy?.Advance(deltaTime, afterburnRequested: false, shipAlive: !IsDestroyed);
                return;
            }

            if (CruiseDrive.IsChargingOrActive && distanceToTarget <= NpcCruiseExitDistance)
            {
                CruiseDrive.Cancel(CruiseCancellationReason.TargetInvalid);
            }
            else if (!CruiseDrive.IsChargingOrActive && ShouldUseCruise(distanceToTarget))
            {
                if (CruiseDrive.TryActivate(shipAlive: !IsDestroyed, docked: false, validMovement: true))
                {
                    IsAfterburnerActive = false;
                }
            }

            bool shouldAfterburn = !CruiseDrive.IsChargingOrActive && ShouldUseAfterburner(distanceToTarget);
            IsAfterburnerActive = ThrusterEnergy?.Advance(
                deltaTime,
                afterburnRequested: shouldAfterburn,
                shipAlive: !IsDestroyed) == true;

            if (CruiseDrive.IsChargingOrActive)
            {
                IsAfterburnerActive = false;
            }

            float commandedSpeed = cruiseSpeed;
            if (CruiseDrive.IsActive)
            {
                commandedSpeed = CruiseDrive.GetEffectiveSpeed(cruiseSpeed);
            }
            else if (IsAfterburnerActive)
            {
                commandedSpeed *= MathHelper.Clamp(
                    ThrusterEnergy.AfterburnSpeedMultiplier,
                    1f,
                    3f);
            }

            if (distanceToTarget > 1f)
            {
                Vector3 desiredDirection = Vector3.Normalize(toTarget);

                Vector3 currentForward = Forward;
                Vector3 rotationAxis = Vector3.Cross(currentForward, desiredDirection);
                float rotationAxisLength = rotationAxis.Length();

                if (rotationAxisLength > 0.0001f)
                {
                    rotationAxis /= rotationAxisLength;
                    float angle = (float)Math.Acos(MathHelper.Clamp(Vector3.Dot(currentForward, desiredDirection), -1f, 1f));
                    float turnAngle = Math.Min(angle, maxTurnRate * deltaTime);

                    Quaternion rotationDelta = Quaternion.CreateFromAxisAngle(rotationAxis, turnAngle);
                    _rotation = rotationDelta * _rotation;
                    _rotation.Normalize();
                }

                float targetSpeed = Math.Min(distanceToTarget * 20f, commandedSpeed);
                float acceleration = CruiseDrive.IsActive
                    ? Math.Max(20f, cruiseSpeed * CruiseDrive.AccelerationMultiplier)
                    : Math.Max(20f, cruiseSpeed * 2f);
                Speed = ApproachSpeed(Speed, targetSpeed, acceleration, deltaTime);
            }
            else
            {
                Speed = ApproachSpeed(Speed, 0f, Math.Max(20f, cruiseSpeed * 2f), deltaTime);
            }

            Velocity = Forward * Speed;
            Position += Velocity * deltaTime;
        }

        private bool ShouldUseAfterburner(float distanceToTarget)
        {
            if (!IsFinitePositive(distanceToTarget) || !IsFinitePositive(TrafficCruiseSpeed))
            {
                return false;
            }

            if (EncounterState == TrafficEncounterState.Fleeing)
            {
                return distanceToTarget > 300f;
            }

            bool engaged = EncounterState == TrafficEncounterState.AttackingTrader ||
                           EncounterState == TrafficEncounterState.AttackingPlayer ||
                           EncounterState == TrafficEncounterState.InterceptingPirate ||
                           EncounterState == TrafficEncounterState.AttackingFactionNpc;
            if (!engaged)
            {
                return false;
            }

            float weaponRange = NpcEquipmentLoadoutFactory.GetFiringRange(Loadout);
            float activationDistance = Math.Max(1000f, weaponRange * 1.35f);
            return distanceToTarget > activationDistance;
        }

        private bool ShouldUseCruise(float distanceToTarget)
        {
            if (!IsFinitePositive(distanceToTarget) || distanceToTarget < NpcCruiseStartDistance)
            {
                return false;
            }

            bool hasNavigationContext = EncounterTargetPosition.HasValue ||
                EncounterEscapePosition.HasValue ||
                TrafficRouteStart.HasValue ||
                TrafficRouteEnd.HasValue;
            return hasNavigationContext;
        }

        private static float ApproachSpeed(float current, float target, float acceleration, float deltaTime)
        {
            if (float.IsNaN(current) || float.IsInfinity(current)) current = 0f;
            if (float.IsNaN(target) || float.IsInfinity(target)) target = 0f;
            if (float.IsNaN(acceleration) || float.IsInfinity(acceleration) || acceleration <= 0f) acceleration = 1f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return current;

            float step = acceleration * Math.Min(deltaTime, 60f);
            return current + MathHelper.Clamp(target - current, -step, step);
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static string BuildStableIdentitySeed(string name, Vector3 position, string factionId)
        {
            return string.Join("|",
                name ?? string.Empty,
                FactionManager.NormalizeFactionId(factionId),
                position.X.ToString("R", CultureInfo.InvariantCulture),
                position.Y.ToString("R", CultureInfo.InvariantCulture),
                position.Z.ToString("R", CultureInfo.InvariantCulture));
        }

        public void SetTradeLaneTransit(bool inTransit, string laneId = null, TradeLaneDirection? direction = null, int ringIndex = -1)
        {
            IsTradeLaneTransit = inTransit;
            if (inTransit)
            {
                TradeLaneId = laneId ?? string.Empty;
                TradeLaneDirection = direction;
                TradeLaneRingIndex = ringIndex;
                IsAfterburnerActive = false;
                CruiseDrive.Cancel(CruiseCancellationReason.IncompatibleFlight);
            }
            else
            {
                TradeLaneId = string.Empty;
                TradeLaneDirection = null;
                TradeLaneRingIndex = -1;
                IsAfterburnerActive = false;
            }
        }

        public void SetMissionHoldPosition(bool hold, Vector3? anchor = null)
        {
            IsMissionHoldPosition = hold;
            if (hold)
            {
                _missionHoldAnchor = anchor ?? Position;
                Position = _missionHoldAnchor;
                Velocity = Vector3.Zero;
                Speed = 0f;
                ClearEncounterState();
            }
        }
    }
}
