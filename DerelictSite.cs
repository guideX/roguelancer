using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// One bounded salvage line carried by a curated derelict site.
    /// Commodity identity is always canonical (<see cref="CommodityCatalog"/>);
    /// quantity is a finite deterministic amount, never a rerolling range.
    /// </summary>
    public sealed class DerelictSalvageLine
    {
        public string CommodityId { get; }
        public int Quantity { get; }

        public DerelictSalvageLine(string commodityId, int quantity)
        {
            CommodityId = commodityId?.Trim() ?? string.Empty;
            Quantity = Math.Clamp(quantity, 0, 40);
        }
    }

    /// <summary>
    /// Immutable definition of one curated discoverable derelict site.
    /// This is a content definition, not a loot/inventory authority: once
    /// recovered, actual items travel through <see cref="LootManager"/> and
    /// <see cref="CargoPod"/> pickup exactly like any other physical salvage.
    /// </summary>
    public sealed class DerelictSite
    {
        public string SiteId { get; }
        public int SystemIndex { get; }
        public Vector3 Position { get; }
        public float DetectionRange { get; }
        public float IdentificationRange { get; }
        public float RecoveryRange { get; }
        public string DisplayName { get; }
        public string ModelPath { get; }
        public IReadOnlyList<DerelictSalvageLine> Salvage { get; }

        public DerelictSite(
            string siteId,
            int systemIndex,
            Vector3 position,
            float detectionRange,
            float identificationRange,
            float recoveryRange,
            string displayName,
            string modelPath,
            IReadOnlyList<DerelictSalvageLine> salvage)
        {
            SiteId = siteId?.Trim() ?? string.Empty;
            SystemIndex = systemIndex;
            Position = position;
            DetectionRange = Math.Max(1f, detectionRange);
            IdentificationRange = Math.Max(1f, identificationRange);
            RecoveryRange = Math.Max(1f, recoveryRange);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Abandoned spacecraft" : displayName.Trim();
            ModelPath = string.IsNullOrWhiteSpace(modelPath) ? "MODELS/wreck" : modelPath.Trim();
            Salvage = (salvage ?? Array.Empty<DerelictSalvageLine>())
                .Where(line => line != null && !string.IsNullOrWhiteSpace(line.CommodityId) && line.Quantity > 0)
                .ToList();
        }
    }

    /// <summary>
    /// Static curated derelict collection. Phase 87 ships exactly one fully
    /// functional site in the starting New York system; the dictionary shape
    /// lets later phases add curated sites without copying gameplay logic.
    /// </summary>
    public static class DerelictCatalog
    {
        public const string FirstSiteId = "derelict_ny_manhattan_drift";

        // Detection 6000m: beyond the 4000m NPC scanner and 900m pod notice
        // so exploration is rewarded, but far below the 25000m map extent so
        // there is no system-wide announcement. Identification 3000m reveals
        // the wreck identity. Recovery 600m matches docking-scale proximity
        // (stations dock inside ~500-900m) and requires deliberate approach;
        // the 200m CargoPod pickup radius then governs physical collection.
        public const float DefaultDetectionRange = 6000f;
        public const float DefaultIdentificationRange = 3000f;
        public const float DefaultRecoveryRange = 600f;

        private static readonly Dictionary<string, DerelictSite> _sites =
            new(StringComparer.OrdinalIgnoreCase)
            {
                {
                    FirstSiteId,
                    new DerelictSite(
                        FirstSiteId,
                        systemIndex: 1,
                        // Open western drift: ~15.5km from the nearest
                        // station, ~15.1km from the nearest trade-lane
                        // segment (all six New York lanes checked),
                        // ~16.3km from the Manhattan spawn, and far from
                        // all four New York jump holes (+/-36-48km).
                        position: new Vector3(-14000f, 500f, -8000f),
                        detectionRange: DefaultDetectionRange,
                        identificationRange: DefaultIdentificationRange,
                        recoveryRange: DefaultRecoveryRange,
                        displayName: "Abandoned spacecraft",
                        modelPath: "MODELS/wreck",
                        salvage: new List<DerelictSalvageLine>
                        {
                            // All canonical, Clean, non-contraband catalog
                            // entries. Total volume 6*1 + 4*1 + 2*3 = 16,
                            // well inside the starter 50-capacity hold so a
                            // normal pickup succeeds, while a full hold still
                            // exercises the partial-pickup path.
                            new DerelictSalvageLine("boron", 6),
                            new DerelictSalvageLine("consumer-goods", 4),
                            new DerelictSalvageLine("construction-materials", 2),
                        })
                },
            };

        public static IReadOnlyCollection<DerelictSite> Sites => _sites.Values;

        public static DerelictSite GetById(string siteId)
        {
            if (string.IsNullOrWhiteSpace(siteId))
                return null;
            return _sites.TryGetValue(siteId.Trim(), out DerelictSite site) ? site : null;
        }

        public static IReadOnlyList<DerelictSite> GetForSystem(int systemIndex)
        {
            return _sites.Values
                .Where(site => site != null && site.SystemIndex == systemIndex)
                .OrderBy(site => site.SiteId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>
    /// World-space representation of a curated derelict. This is an ordinary
    /// <see cref="SpaceObject"/> (targetable, bracketed, mapped) and never an
    /// <see cref="NpcShip"/>: it has no AI, weapons, shields, trade-lane
    /// transit, docking, missions, or Police behavior. Visuals reuse the
    /// existing wreck model path already loaded by the game.
    /// </summary>
    public sealed class DerelictWreckObject : SpaceObject
    {
        public string SiteId { get; }

        public DerelictWreckObject(DerelictSite site)
            : base(site?.DisplayName ?? "Abandoned spacecraft", site?.Position ?? Vector3.Zero, 200f)
        {
            SiteId = site?.SiteId ?? string.Empty;
            ModelPath = site?.ModelPath ?? "MODELS/wreck";
        }
    }
}
