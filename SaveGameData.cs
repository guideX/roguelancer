using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roguelancer
{
    /// <summary>
    /// Versioned save schema for single-player progression.
    /// </summary>
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 13;

        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonPropertyName("player_credits")]
        public int PlayerCredits { get; set; }

        [JsonPropertyName("nanobots")]
        public int Nanobots { get; set; }

        [JsonPropertyName("shield_batteries")]
        public int ShieldBatteries { get; set; }

        [JsonPropertyName("current_system_index")]
        public int CurrentSystemIndex { get; set; } = 1;

        [JsonPropertyName("current_ship_name")]
        public string CurrentShipName { get; set; } = string.Empty;

        [JsonPropertyName("player_position")]
        public SaveVector3Data PlayerPosition { get; set; } = new SaveVector3Data();

        [JsonPropertyName("player_velocity")]
        public SaveVector3Data PlayerVelocity { get; set; } = new SaveVector3Data();

        [JsonPropertyName("player_forward")]
        public SaveVector3Data PlayerForward { get; set; } = new SaveVector3Data(0f, 0f, -1f);

        [JsonPropertyName("owned_equipment")]
        public List<SaveOwnedEquipmentData> OwnedEquipment { get; set; } = new();

        [JsonPropertyName("mounted_equipment")]
        public List<SaveMountedEquipmentData> MountedEquipment { get; set; } = new();

        [JsonPropertyName("cargo")]
        public List<SaveCargoItemData> Cargo { get; set; } = new();

        [JsonPropertyName("faction_reputation")]
        public List<SaveFactionReputationData> FactionReputation { get; set; } = new();

        [JsonPropertyName("temporary_hostility")]
        public List<SaveTemporaryHostilityData> TemporaryHostility { get; set; } = new();

        [JsonPropertyName("active_missions")]
        public List<SaveMissionData> ActiveMissions { get; set; } = new();

        [JsonPropertyName("completed_missions")]
        public List<SaveMissionData> CompletedMissions { get; set; } = new();

        [JsonPropertyName("physical_mission_cargo_pods")]
        public List<SaveCargoPodData> PhysicalMissionCargoPods { get; set; } = new();

        [JsonPropertyName("physical_cargo_pods")]
        public List<SaveCargoPodData> PhysicalCargoPods { get; set; } = new();

        [JsonPropertyName("station_markets")]
        public List<SaveMarketStateData> StationMarkets { get; set; } = new();

        [JsonPropertyName("economic_shipments")]
        public List<SaveEconomicShipmentData> EconomicShipments { get; set; } = new();

        /// <summary>
        /// Phase 68 active criminal logistics snapshots. Terminal shipments
        /// are intentionally absent because their cargo is already delivered,
        /// physically dropped, or explicitly lost.
        /// </summary>
        [JsonPropertyName("rogue_smuggling_shipments")]
        public List<SaveRogueSmugglingShipmentData> RogueSmugglingShipments { get; set; } = new();

        /// <summary>
        /// Optional Phase 66/67 snapshots. Keeping this field additive preserves
        /// schema version 12 compatibility with older saves.
        /// </summary>
        [JsonPropertyName("ambient_pirate_raids")]
        public List<SaveAmbientPirateRaidData> AmbientPirateRaids { get; set; } = new();

        [JsonPropertyName("trade_route_risks")]
        public List<SaveTradeRouteRiskData> TradeRouteRisks { get; set; } = new();

        [JsonPropertyName("market_elapsed_ms")]
        public long MarketElapsedMilliseconds { get; set; }

        [JsonPropertyName("market_intelligence")]
        public List<SaveMarketIntelligenceData> MarketIntelligence { get; set; } = new();

        /// <summary>
        /// Optional player-created trade route. Null keeps older saves fully
        /// compatible and intentionally stores no UI formatting strings.
        /// </summary>
        [JsonPropertyName("trade_plan")]
        public SaveTradePlanData TradePlan { get; set; }
    }

    /// <summary>
    /// Serializable 3D vector payload.
    /// </summary>
    public sealed class SaveVector3Data
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        public SaveVector3Data()
        {
        }

        public SaveVector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static SaveVector3Data From(Vector3 value)
        {
            return new SaveVector3Data(Sanitize(value.X), Sanitize(value.Y), Sanitize(value.Z));
        }

        public Vector3 ToVector3(Vector3 fallback = default)
        {
            return new Vector3(
                Sanitize(X, fallback.X),
                Sanitize(Y, fallback.Y),
                Sanitize(Z, fallback.Z));
        }

        private static float Sanitize(float value, float fallback = 0f)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    /// <summary>
    /// Owned equipment entry stored in a save file.
    /// </summary>
    public sealed class SaveOwnedEquipmentData
    {
        [JsonPropertyName("equipment_id")]
        public string EquipmentId { get; set; } = string.Empty;

        [JsonPropertyName("equipment_type")]
        public EquipmentType EquipmentType { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Mounted equipment entry stored in a save file.
    /// </summary>
    public sealed class SaveMountedEquipmentData
    {
        [JsonPropertyName("hardpoint_id")]
        public string HardpointId { get; set; } = string.Empty;

        [JsonPropertyName("equipment_id")]
        public string EquipmentId { get; set; } = string.Empty;

        [JsonPropertyName("equipment_type")]
        public EquipmentType EquipmentType { get; set; }
    }

    /// <summary>
    /// Cargo stack entry stored in a save file.
    /// </summary>
    public sealed class SaveCargoItemData
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("mission_id")]
        public int MissionId { get; set; }

        [JsonPropertyName("mission_bound")]
        public bool MissionBound { get; set; }

        [JsonPropertyName("stolen")]
        public bool IsStolen { get; set; }
    }

    /// <summary>Durable snapshot for a physical mission cargo pod.</summary>
    public sealed class SaveCargoPodData
    {
        [JsonPropertyName("mission_id")]
        public int MissionId { get; set; }

        [JsonPropertyName("mission_cargo_source_index")]
        public int MissionCargoSourceIndex { get; set; } = -1;

        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("stolen")]
        public bool IsStolen { get; set; }

        [JsonPropertyName("age_seconds")]
        public float AgeSeconds { get; set; }

        [JsonPropertyName("position")]
        public SaveVector3Data Position { get; set; } = new();

        [JsonPropertyName("velocity")]
        public SaveVector3Data Velocity { get; set; } = new();

        [JsonPropertyName("source_npc_name")]
        public string SourceNpcName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reputation entry stored in a save file.
    /// </summary>
    public sealed class SaveFactionReputationData
    {
        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        [JsonPropertyName("standing")]
        public float Standing { get; set; }
    }

    /// <summary>
    /// Short-lived faction aggression stored as remaining simulation seconds.
    /// No wall-clock timestamp is serialized.
    /// </summary>
    public sealed class SaveTemporaryHostilityData
    {
        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("remaining_seconds")]
        public float RemainingSeconds { get; set; }
    }

    /// <summary>
    /// Mission entry stored in a save file.
    /// </summary>
    public sealed class SaveMissionData
    {
        [JsonPropertyName("definition_id")]
        public string DefinitionId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("mission_id")]
        public int MissionId { get; set; }

        [JsonPropertyName("type")]
        public MissionType Type { get; set; }

        [JsonPropertyName("difficulty")]
        public MissionDifficulty Difficulty { get; set; }

        [JsonPropertyName("status")]
        public MissionStatus Status { get; set; }

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonPropertyName("reward")]
        public int Reward { get; set; }

        [JsonPropertyName("time_limit")]
        public float TimeLimit { get; set; }

        [JsonPropertyName("elapsed_time")]
        public float ElapsedTime { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("offered_by")]
        public string OfferedBy { get; set; } = string.Empty;

        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        [JsonPropertyName("minimum_employer_reputation")]
        public float? MinimumEmployerReputation { get; set; }

        [JsonPropertyName("maximum_employer_reputation")]
        public float? MaximumEmployerReputation { get; set; }

        [JsonPropertyName("objective_complete")]
        public bool ObjectiveComplete { get; set; }

        [JsonPropertyName("target_location")]
        public string TargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("target_system_index")]
        public int TargetSystemIndex { get; set; }

        [JsonPropertyName("target_count")]
        public int TargetCount { get; set; }

        [JsonPropertyName("current_progress")]
        public int CurrentProgress { get; set; }

        [JsonPropertyName("required_progress")]
        public int RequiredProgress { get; set; }

        [JsonPropertyName("objective_radius")]
        public int ObjectiveRadius { get; set; } = 500;

        [JsonPropertyName("origin_station_id")]
        public string OriginStationId { get; set; } = string.Empty;

        [JsonPropertyName("origin_station_name")]
        public string OriginStationName { get; set; } = string.Empty;

        [JsonPropertyName("origin_system_index")]
        public int OriginSystemIndex { get; set; }

        [JsonPropertyName("accepted_at_utc")]
        public string AcceptedAtUtc { get; set; } = string.Empty;

        [JsonPropertyName("reward_paid")]
        public bool RewardPaid { get; set; }

        [JsonPropertyName("reputation_reward")]
        public float ReputationReward { get; set; }

        [JsonPropertyName("reputation_reward_applied")]
        public bool ReputationRewardApplied { get; set; }

        [JsonPropertyName("target_position")]
        public SaveVector3Data TargetPosition { get; set; }

        [JsonPropertyName("source_station_name")]
        public string SourceStationName { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_id")]
        public string DestinationStationId { get; set; } = string.Empty;

        [JsonPropertyName("package_id")]
        public string PackageId { get; set; } = string.Empty;

        [JsonPropertyName("package_quantity")]
        public int PackageQuantity { get; set; }

        [JsonPropertyName("package_volume")]
        public int PackageVolume { get; set; }

        [JsonPropertyName("mission_cargo_loaded")]
        public bool MissionCargoLoaded { get; set; }

        [JsonPropertyName("delivered_quantity")]
        public int DeliveredQuantity { get; set; }

        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("required_quantity")]
        public int RequiredQuantity { get; set; }

        [JsonPropertyName("issued_cargo_quantity")]
        public int IssuedCargoQuantity { get; set; }

        [JsonPropertyName("smuggling_stage")]
        public ContrabandSmugglingStage SmugglingStage { get; set; } = ContrabandSmugglingStage.EnRoute;

        [JsonPropertyName("smuggling_police_detected")]
        public bool SmugglingPoliceDetected { get; set; }

        [JsonPropertyName("smuggling_jettisoned_quantity")]
        public int SmugglingJettisonedQuantity { get; set; }

        [JsonPropertyName("target_lane_id")]
        public string TargetLaneId { get; set; } = string.Empty;

        [JsonPropertyName("target_segment_id")]
        public string TargetSegmentId { get; set; } = string.Empty;

        [JsonPropertyName("target_ring_index")]
        public int TargetRingIndex { get; set; } = -1;

        [JsonPropertyName("hold_duration_seconds")]
        public float HoldDurationSeconds { get; set; }

        [JsonPropertyName("hold_progress_seconds")]
        public float HoldProgressSeconds { get; set; }

        [JsonPropertyName("player_disruption_observed")]
        public bool PlayerDisruptionObserved { get; set; }

        [JsonPropertyName("last_qualified_disruption_at_seconds")]
        public double LastQualifiedDisruptionAtSeconds { get; set; } = -1d;

        [JsonPropertyName("police_reputation_penalty")]
        public float PoliceReputationPenalty { get; set; }

        [JsonPropertyName("police_consequence_applied")]
        public bool PoliceConsequenceApplied { get; set; }

        [JsonPropertyName("security_response_triggered")]
        public bool SecurityResponseTriggered { get; set; }

        [JsonPropertyName("failure_reason")]
        public string FailureReason { get; set; } = string.Empty;

        [JsonPropertyName("defense_stage")]
        public TradeLaneDefenseStage DefenseStage { get; set; } = TradeLaneDefenseStage.EnRoute;

        [JsonPropertyName("defense_attack_force_size")]
        public int DefenseAttackForceSize { get; set; }

        [JsonPropertyName("defense_attackers_remaining")]
        public int DefenseAttackersRemaining { get; set; }

        [JsonPropertyName("defense_activation_radius")]
        public float DefenseActivationRadius { get; set; }

        [JsonPropertyName("defense_failure_hold_seconds")]
        public float DefenseFailureHoldSeconds { get; set; }

        [JsonPropertyName("defense_failure_hold_progress_seconds")]
        public float DefenseFailureHoldProgressSeconds { get; set; }

        [JsonPropertyName("defense_activation_started")]
        public bool DefenseActivationStarted { get; set; }

        [JsonPropertyName("convoy_route_id")]
        public string ConvoyRouteId { get; set; } = string.Empty;

        [JsonPropertyName("convoy_route_lane_id")]
        public string ConvoyRouteLaneId { get; set; } = string.Empty;

        [JsonPropertyName("convoy_route_segment_id")]
        public string ConvoyRouteSegmentId { get; set; } = string.Empty;

        [JsonPropertyName("convoy_route_direction")]
        public TradeLaneDirection ConvoyRouteDirection { get; set; } = TradeLaneDirection.Forward;

        [JsonPropertyName("convoy_faction_id")]
        public string ConvoyFactionId { get; set; } = string.Empty;

        [JsonPropertyName("convoy_hostile_faction_id")]
        public string ConvoyHostileFactionId { get; set; } = string.Empty;

        [JsonPropertyName("convoy_ship_archetype")]
        public string ConvoyShipArchetype { get; set; } = string.Empty;

        [JsonPropertyName("convoy_route_ring_index")]
        public int ConvoyRouteRingIndex { get; set; } = -1;

        [JsonPropertyName("convoy_encounter_ring_index")]
        public int ConvoyEncounterRingIndex { get; set; } = -1;

        [JsonPropertyName("convoy_ship_count")]
        public int ConvoyShipCount { get; set; }

        [JsonPropertyName("convoy_required_survivors")]
        public int ConvoyRequiredSurvivors { get; set; } = 1;

        [JsonPropertyName("convoy_survivors")]
        public int ConvoySurvivors { get; set; }

        [JsonPropertyName("convoy_destroyed_count")]
        public int ConvoyDestroyedCount { get; set; }

        [JsonPropertyName("convoy_destroyed_mask")]
        public int ConvoyDestroyedMask { get; set; }

        [JsonPropertyName("convoy_arrived_count")]
        public int ConvoyArrivedCount { get; set; }

        [JsonPropertyName("convoy_arrived_mask")]
        public int ConvoyArrivedMask { get; set; }

        [JsonPropertyName("convoy_attack_force_size")]
        public int ConvoyAttackForceSize { get; set; }

        [JsonPropertyName("convoy_attackers_remaining")]
        public int ConvoyAttackersRemaining { get; set; }

        [JsonPropertyName("convoy_stage")]
        public ConvoyEscortStage ConvoyStage { get; set; } = ConvoyEscortStage.Rendezvous;

        [JsonPropertyName("convoy_rendezvous_radius")]
        public float ConvoyRendezvousRadius { get; set; }

        [JsonPropertyName("convoy_abandonment_radius")]
        public float ConvoyAbandonmentRadius { get; set; }

        [JsonPropertyName("convoy_abandonment_grace_seconds")]
        public float ConvoyAbandonmentGraceSeconds { get; set; }

        [JsonPropertyName("convoy_abandonment_progress_seconds")]
        public float ConvoyAbandonmentProgressSeconds { get; set; }

        [JsonPropertyName("convoy_arrival_radius")]
        public float ConvoyArrivalRadius { get; set; }

        [JsonPropertyName("convoy_route_started")]
        public bool ConvoyRouteStarted { get; set; }

        [JsonPropertyName("convoy_encounter_activated")]
        public bool ConvoyEncounterActivated { get; set; }

        [JsonPropertyName("convoy_encounter_resolved")]
        public bool ConvoyEncounterResolved { get; set; }

        [JsonPropertyName("convoy_encounter_spawn_attempted")]
        public bool ConvoyEncounterSpawnAttempted { get; set; }

        [JsonPropertyName("convoy_rendezvous_position")]
        public SaveVector3Data ConvoyRendezvousPosition { get; set; }

        [JsonPropertyName("convoy_encounter_position")]
        public SaveVector3Data ConvoyEncounterPosition { get; set; }

        [JsonPropertyName("convoy_destination_position")]
        public SaveVector3Data ConvoyDestinationPosition { get; set; }

        [JsonPropertyName("economic_escort")]
        public bool EconomicEscort { get; set; }

        [JsonPropertyName("economic_shipment_trader_identity")]
        public string EconomicShipmentTraderIdentity { get; set; } = string.Empty;

        [JsonPropertyName("economic_shipment_route_id")]
        public string EconomicShipmentRouteId { get; set; } = string.Empty;

        [JsonPropertyName("economic_shipment_value")]
        public int EconomicShipmentValue { get; set; }

        [JsonPropertyName("economic_route_risk")]
        public int EconomicRouteRisk { get; set; }

        [JsonPropertyName("economic_shortage_label")]
        public string EconomicShortageLabel { get; set; } = string.Empty;

        [JsonPropertyName("economic_offer_expires_ms")]
        public long EconomicOfferExpiresMilliseconds { get; set; }

        [JsonPropertyName("economic_interdiction")]
        public bool EconomicInterdiction { get; set; }

        [JsonPropertyName("interdiction_source_available_quantity")]
        public int InterdictionSourceAvailableQuantity { get; set; }

        [JsonPropertyName("interdiction_released_quantity")]
        public int InterdictionReleasedQuantity { get; set; }

        [JsonPropertyName("interdiction_cargo_lost_quantity")]
        public int InterdictionCargoLostQuantity { get; set; }

        [JsonPropertyName("interdiction_cargo_recovered_quantity")]
        public int InterdictionCargoRecoveredQuantity { get; set; }

        [JsonPropertyName("interdiction_remaining_possible_quantity")]
        public int InterdictionRemainingPossibleQuantity { get; set; }

        [JsonPropertyName("interdiction_stage")]
        public ShipmentInterdictionStage InterdictionStage { get; set; } = ShipmentInterdictionStage.Intercept;

        [JsonPropertyName("interdiction_target_destroyed")]
        public bool InterdictionTargetDestroyed { get; set; }

        [JsonPropertyName("interdiction_target_delivered")]
        public bool InterdictionTargetDelivered { get; set; }

        [JsonPropertyName("raid_route_id")]
        public string RaidRouteId { get; set; } = string.Empty;

        [JsonPropertyName("raid_route_lane_id")]
        public string RaidRouteLaneId { get; set; } = string.Empty;

        [JsonPropertyName("raid_route_segment_id")]
        public string RaidRouteSegmentId { get; set; } = string.Empty;

        [JsonPropertyName("raid_route_direction")]
        public TradeLaneDirection RaidRouteDirection { get; set; } = TradeLaneDirection.Forward;

        [JsonPropertyName("raid_convoy_faction_id")]
        public string RaidConvoyFactionId { get; set; } = string.Empty;

        [JsonPropertyName("raid_ship_archetype")]
        public string RaidShipArchetype { get; set; } = string.Empty;

        [JsonPropertyName("raid_route_ring_index")]
        public int RaidRouteRingIndex { get; set; } = -1;

        [JsonPropertyName("raid_interception_ring_index")]
        public int RaidInterceptionRingIndex { get; set; } = -1;

        [JsonPropertyName("raid_ship_count")]
        public int RaidShipCount { get; set; }

        [JsonPropertyName("raid_destroyed_count")]
        public int RaidDestroyedCount { get; set; }

        [JsonPropertyName("raid_escaped_count")]
        public int RaidEscapedCount { get; set; }

        [JsonPropertyName("raid_destroyed_mask")]
        public int RaidDestroyedMask { get; set; }

        [JsonPropertyName("raid_escaped_mask")]
        public int RaidEscapedMask { get; set; }

        [JsonPropertyName("raid_cargo_released_mask")]
        public int RaidCargoReleasedMask { get; set; }

        [JsonPropertyName("raid_cargo_lost_quantity")]
        public int RaidCargoLostQuantity { get; set; }

        [JsonPropertyName("raid_cargo_released_quantity")]
        public int RaidCargoReleasedQuantity { get; set; }

        [JsonPropertyName("raid_cargo_recovered_quantity")]
        public int RaidCargoRecoveredQuantity { get; set; }

        [JsonPropertyName("raid_commodity_id")]
        public string RaidCommodityId { get; set; } = string.Empty;

        [JsonPropertyName("raid_required_quantity")]
        public int RaidRequiredQuantity { get; set; }

        [JsonPropertyName("raid_total_allocated_quantity")]
        public int RaidTotalAllocatedQuantity { get; set; }

        [JsonPropertyName("raid_remaining_possible_quantity")]
        public int RaidRemainingPossibleQuantity { get; set; }

        [JsonPropertyName("raid_cargo_allocation")]
        public List<int> RaidCargoAllocation { get; set; } = new();

        [JsonPropertyName("raid_stage")]
        public ConvoyRaidStage RaidStage { get; set; } = ConvoyRaidStage.EnRoute;

        [JsonPropertyName("raid_route_started")]
        public bool RaidRouteStarted { get; set; }

        [JsonPropertyName("raid_interception_activated")]
        public bool RaidInterceptionActivated { get; set; }

        [JsonPropertyName("raid_interception_position")]
        public SaveVector3Data RaidInterceptionPosition { get; set; }

        [JsonPropertyName("raid_destination_position")]
        public SaveVector3Data RaidDestinationPosition { get; set; }
    }

    /// <summary>
    /// Runtime station market snapshot stored in a save file.
    /// </summary>
    public sealed class SaveMarketStateData
    {
        [JsonPropertyName("station_key")]
        public string StationKey { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("listings")]
        public List<SaveMarketListingData> Listings { get; set; } = new();
    }

    /// <summary>
    /// Active ambient trader shipment snapshot. Historical deliveries are not
    /// retained; only bounded in-flight manifests needed for rebind are saved.
    /// </summary>
    public sealed class SaveEconomicShipmentData
    {
        [JsonPropertyName("trader_identity")]
        public string TraderIdentity { get; set; } = string.Empty;

        [JsonPropertyName("route_id")]
        public string RouteId { get; set; } = string.Empty;

        [JsonPropertyName("origin_station_id")]
        public string OriginStationId { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_id")]
        public string DestinationStationId { get; set; } = string.Empty;

        [JsonPropertyName("origin_station_name")]
        public string OriginStationName { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_name")]
        public string DestinationStationName { get; set; } = string.Empty;

        [JsonPropertyName("initial_quantity")]
        public int InitialQuantity { get; set; }

        [JsonPropertyName("remaining_quantity")]
        public int RemainingQuantity { get; set; }

        [JsonPropertyName("route_toward_end")]
        public bool RouteTowardEnd { get; set; }

        [JsonPropertyName("position")]
        public SaveVector3Data Position { get; set; } = new();

        [JsonPropertyName("velocity")]
        public SaveVector3Data Velocity { get; set; } = new();

        [JsonPropertyName("traffic_age_seconds")]
        public float TrafficAgeSeconds { get; set; }

        [JsonPropertyName("stacks")]
        public List<SaveEconomicShipmentStackData> Stacks { get; set; } = new();

        [JsonPropertyName("escort_mission_id")]
        public int EscortMissionId { get; set; }

        [JsonPropertyName("escort_offer_issued")]
        public bool EscortOfferIssued { get; set; }

        [JsonPropertyName("escort_offer_expires_ms")]
        public long EscortOfferExpiresMilliseconds { get; set; }

        [JsonPropertyName("escort_offer_expired")]
        public bool EscortOfferExpired { get; set; }

        [JsonPropertyName("interdiction_mission_id")]
        public int InterdictionMissionId { get; set; }

        [JsonPropertyName("interdiction_offer_issued")]
        public bool InterdictionOfferIssued { get; set; }

        [JsonPropertyName("interdiction_offer_expires_ms")]
        public long InterdictionOfferExpiresMilliseconds { get; set; }

        [JsonPropertyName("interdiction_offer_expired")]
        public bool InterdictionOfferExpired { get; set; }

        /// <summary>
        /// Durable decision bit for the one-shot ambient security lifecycle.
        /// A true value is intentionally saved even when no guard spawned, so
        /// loading cannot re-run qualification or create replacements.
        /// </summary>
        [JsonPropertyName("security_assignment_decided")]
        public bool SecurityAssignmentDecided { get; set; }

        [JsonPropertyName("security_members")]
        public List<SaveShipmentSecurityMemberData> SecurityMembers { get; set; } = new();

        [JsonPropertyName("ambient_raid_attempted")]
        public bool AmbientRaidAttempted { get; set; }

        [JsonPropertyName("ambient_raid_state")]
        public AmbientPirateRaidState AmbientRaidState { get; set; }

        [JsonPropertyName("ambient_raid_surrendered")]
        public bool AmbientRaidSurrendered { get; set; }
    }

    /// <summary>
    /// Stable snapshot of one ambient security member. Runtime NPC references
    /// are deliberately absent; the member is rebound from its identity after
    /// the parent economic shipment has been reconstructed.
    /// </summary>
    public sealed class SaveShipmentSecurityMemberData
    {
        [JsonPropertyName("stable_identity")]
        public string StableIdentity { get; set; } = string.Empty;

        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        [JsonPropertyName("archetype_name")]
        public string ArchetypeName { get; set; } = string.Empty;

        [JsonPropertyName("model_path")]
        public string ModelPath { get; set; } = string.Empty;

        [JsonPropertyName("loadout_tier")]
        public NpcLoadoutTier LoadoutTier { get; set; } = NpcLoadoutTier.Standard;

        [JsonPropertyName("alive")]
        public bool Alive { get; set; }

        [JsonPropertyName("formation_offset")]
        public SaveVector3Data FormationOffset { get; set; } = new();

        [JsonPropertyName("position")]
        public SaveVector3Data Position { get; set; } = new();

        [JsonPropertyName("velocity")]
        public SaveVector3Data Velocity { get; set; } = new();
    }

    public sealed class SaveTradeRouteRiskData
    {
        [JsonPropertyName("route_id")]
        public string RouteId { get; set; } = string.Empty;

        [JsonPropertyName("direction")]
        public TradeLaneDirection Direction { get; set; } = TradeLaneDirection.Forward;

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("last_processed_ms")]
        public long LastProcessedMilliseconds { get; set; }

        [JsonPropertyName("incident_keys")]
        public List<string> IncidentKeys { get; set; } = new();
    }

    public sealed class SaveEconomicShipmentStackData
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("initial_quantity")]
        public int InitialQuantity { get; set; }

        [JsonPropertyName("remaining_quantity")]
        public int RemainingQuantity { get; set; }
    }

    /// <summary>
    /// Active Phase 68 carrier snapshot. It is separate from the lawful
    /// EconomicShipment schema so Phase 67/66 state cannot cross-settle it.
    /// </summary>
    public sealed class SaveRogueSmugglingShipmentData
    {
        [JsonPropertyName("shipment_identity")]
        public string ShipmentIdentity { get; set; } = string.Empty;

        [JsonPropertyName("route_id")]
        public string RouteId { get; set; } = string.Empty;

        [JsonPropertyName("origin_station_id")]
        public string OriginStationId { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_id")]
        public string DestinationStationId { get; set; } = string.Empty;

        [JsonPropertyName("origin_station_name")]
        public string OriginStationName { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_name")]
        public string DestinationStationName { get; set; } = string.Empty;

        [JsonPropertyName("initial_quantity")]
        public int InitialQuantity { get; set; }

        [JsonPropertyName("remaining_quantity")]
        public int RemainingQuantity { get; set; }

        [JsonPropertyName("route_toward_end")]
        public bool RouteTowardEnd { get; set; }

        [JsonPropertyName("settlement")]
        public RogueSmugglingShipmentSettlement Settlement { get; set; } = RogueSmugglingShipmentSettlement.Active;

        [JsonPropertyName("position")]
        public SaveVector3Data Position { get; set; } = new();

        [JsonPropertyName("velocity")]
        public SaveVector3Data Velocity { get; set; } = new();

        [JsonPropertyName("traffic_age_seconds")]
        public float TrafficAgeSeconds { get; set; }

        [JsonPropertyName("stacks")]
        public List<SaveRogueSmugglingStackData> Stacks { get; set; } = new();
    }

    public sealed class SaveRogueSmugglingStackData
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("initial_quantity")]
        public int InitialQuantity { get; set; }

        [JsonPropertyName("remaining_quantity")]
        public int RemainingQuantity { get; set; }
    }

    public enum AmbientPirateRaidState
    {
        None = 0,
        Delayed = 1,
        Active = 2,
        Resolved = 3,
        ReturningWithLoot = 4,
        Delivered = 5,
        Lost = 6
    }

    /// <summary>Durable state for one bounded autonomous Rogue raid.</summary>
    public sealed class SaveAmbientPirateRaidData
    {
        [JsonPropertyName("shipment_identity")]
        public string ShipmentIdentity { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public AmbientPirateRaidState State { get; set; }

        [JsonPropertyName("delay_remaining_seconds")]
        public float DelayRemainingSeconds { get; set; }

        [JsonPropertyName("elapsed_seconds")]
        public float ElapsedSeconds { get; set; }

        [JsonPropertyName("pressure_seconds")]
        public float PressureSeconds { get; set; }

        [JsonPropertyName("recent_pressure_seconds")]
        public float RecentPressureSeconds { get; set; }

        [JsonPropertyName("surrender_evaluated")]
        public bool SurrenderEvaluated { get; set; }

        [JsonPropertyName("surrendered_quantity")]
        public int SurrenderedQuantity { get; set; }

        [JsonPropertyName("recovered_quantity")]
        public int RecoveredQuantity { get; set; }

        [JsonPropertyName("recovery_elapsed_seconds")]
        public float RecoveryElapsedSeconds { get; set; }

        [JsonPropertyName("return_elapsed_seconds")]
        public float ReturnElapsedSeconds { get; set; }

        [JsonPropertyName("raiders")]
        public List<SaveAmbientPirateRaiderData> Raiders { get; set; } = new();
    }

    /// <summary>Durable identity/haul snapshot for one assigned raider.</summary>
    public sealed class SaveAmbientPirateRaiderData
    {
        [JsonPropertyName("stable_identity")]
        public string StableIdentity { get; set; } = string.Empty;

        [JsonPropertyName("archetype_name")]
        public string ArchetypeName { get; set; } = string.Empty;

        [JsonPropertyName("model_path")]
        public string ModelPath { get; set; } = string.Empty;

        [JsonPropertyName("loadout_tier")]
        public NpcLoadoutTier LoadoutTier { get; set; } = NpcLoadoutTier.Standard;

        [JsonPropertyName("alive")]
        public bool Alive { get; set; }

        [JsonPropertyName("escaped")]
        public bool Escaped { get; set; }

        [JsonPropertyName("position")]
        public SaveVector3Data Position { get; set; } = new();

        [JsonPropertyName("velocity")]
        public SaveVector3Data Velocity { get; set; } = new();

        [JsonPropertyName("haul")]
        public List<SaveAmbientPirateHaulData> Haul { get; set; } = new();

        [JsonPropertyName("receiver_station_id")]
        public string ReceiverStationId { get; set; } = string.Empty;

        [JsonPropertyName("receiver_station_name")]
        public string ReceiverStationName { get; set; } = string.Empty;

        [JsonPropertyName("delivery_settled")]
        public bool DeliverySettled { get; set; }

        [JsonPropertyName("delivery_lost")]
        public bool DeliveryLost { get; set; }
    }

    public sealed class SaveAmbientPirateHaulData
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Individual station market listing snapshot stored in a save file.
    /// </summary>
    public sealed class SaveMarketListingData
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        /// <summary>
        /// Legacy Phase 13 field retained for source compatibility. Prices are
        /// derived from current stock and station configuration in Phase 14 and
        /// are intentionally not serialized.
        /// </summary>
        [JsonIgnore]
        public int BuyPrice { get; set; }

        /// <summary>
        /// Legacy Phase 13 field retained for source compatibility. See
        /// <see cref="BuyPrice"/>.
        /// </summary>
        [JsonIgnore]
        public int SellPrice { get; set; }

        [JsonPropertyName("stock")]
        public int Stock { get; set; }

        [JsonPropertyName("demand_level")]
        public int DemandLevel { get; set; }

        [JsonPropertyName("is_available")]
        public bool IsAvailable { get; set; }

        [JsonPropertyName("recovery_remainder_ms")]
        public long RecoveryRemainderMilliseconds { get; set; }

        [JsonPropertyName("immediate_sell_price_ceiling")]
        public int ImmediateSellPriceCeiling { get; set; }

        /// <summary>
        /// Bounded relative age for a derived shortage. Stock remains the
        /// authority; this only keeps a mature shortage from restarting its
        /// board cooldown after a save/load.
        /// </summary>
        [JsonPropertyName("shortage_age_ms")]
        public long ShortageAgeMilliseconds { get; set; }

        [JsonPropertyName("consumption_remainder")]
        public long ConsumptionRemainder { get; set; }

        /// <summary>
        /// Older saves did not contain this field. The initializer preserves
        /// their legacy transaction-recovery behavior on load.
        /// </summary>
        [JsonPropertyName("recovery_enabled")]
        public bool RecoveryEnabled { get; set; } = true;
    }

    /// <summary>
    /// A player's last observed quote. This is deliberately separate from the
    /// authoritative runtime market snapshot above.
    /// </summary>
    public sealed class SaveMarketIntelligenceData
    {
        [JsonPropertyName("station_id")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("system_index")]
        public int SystemIndex { get; set; }

        [JsonPropertyName("station_position")]
        public SaveVector3Data StationPosition { get; set; } = new();

        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("stock")]
        public int Stock { get; set; }

        [JsonPropertyName("buy_price")]
        public int BuyPrice { get; set; }

        [JsonPropertyName("sell_price")]
        public int SellPrice { get; set; }

        [JsonPropertyName("baseline_stock")]
        public int BaselineStock { get; set; }

        [JsonPropertyName("demand_level")]
        public int DemandLevel { get; set; }

        [JsonPropertyName("market_condition")]
        public string MarketCondition { get; set; } = string.Empty;

        [JsonPropertyName("observed_at_ms")]
        public long ObservedAtMilliseconds { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    public sealed class SaveTradePlanData
    {
        [JsonPropertyName("source_station_id")]
        public string SourceStationId { get; set; } = string.Empty;

        [JsonPropertyName("source_station_name")]
        public string SourceStationName { get; set; } = string.Empty;

        [JsonPropertyName("source_system_index")]
        public int SourceSystemIndex { get; set; }

        [JsonPropertyName("destination_station_id")]
        public string DestinationStationId { get; set; } = string.Empty;

        [JsonPropertyName("destination_station_name")]
        public string DestinationStationName { get; set; } = string.Empty;

        [JsonPropertyName("destination_system_index")]
        public int DestinationSystemIndex { get; set; }

        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("commodity_name")]
        public string CommodityName { get; set; } = string.Empty;

        [JsonPropertyName("source_buy_price")]
        public int SourceBuyPrice { get; set; }

        [JsonPropertyName("destination_sell_price")]
        public int DestinationSellPrice { get; set; }

        [JsonPropertyName("source_observed_at_ms")]
        public long SourceObservedAtMilliseconds { get; set; }

        [JsonPropertyName("destination_observed_at_ms")]
        public long DestinationObservedAtMilliseconds { get; set; }

        [JsonPropertyName("stage")]
        public TradePlanStage Stage { get; set; }

        [JsonPropertyName("route_distance_units")]
        public float RouteDistanceUnits { get; set; }

        [JsonPropertyName("route_hops")]
        public int RouteHops { get; set; }

        [JsonPropertyName("opportunity_score")]
        public int OpportunityScore { get; set; }

        [JsonPropertyName("suggested_quantity")]
        public int SuggestedQuantity { get; set; }

        [JsonPropertyName("initial_ordinary_quantity")]
        public int InitialOrdinaryQuantity { get; set; }

        [JsonPropertyName("acquired_quantity")]
        public int AcquiredQuantity { get; set; }

        [JsonPropertyName("purchased_quantity")]
        public int PurchasedQuantity { get; set; }

        [JsonPropertyName("sold_quantity")]
        public int SoldQuantity { get; set; }

        [JsonPropertyName("purchased_cost")]
        public long PurchasedCost { get; set; }

        [JsonPropertyName("sold_proceeds")]
        public long SoldProceeds { get; set; }

        [JsonPropertyName("average_source_purchase_price")]
        public int AverageSourcePurchasePrice { get; set; }

        [JsonPropertyName("actual_source_buy_price")]
        public int ActualSourceBuyPrice { get; set; }

        [JsonPropertyName("actual_destination_sell_price")]
        public int ActualDestinationSellPrice { get; set; }

        [JsonPropertyName("cargo_acquired")]
        public bool CargoAcquired { get; set; }

        [JsonPropertyName("ambiguous_provenance")]
        public bool HasAmbiguousProvenance { get; set; }
    }
}
