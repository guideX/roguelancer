using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Authoritative metadata for cargo reserved by an active mission.
    /// </summary>
    public sealed class MissionCargoReservation
    {
        public int MissionId { get; set; }
        public string CommodityId { get; set; } = string.Empty;
        public string CommodityName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int VolumePerUnit { get; set; }

        public MissionCargoReservation Clone() => new()
        {
            MissionId = MissionId,
            CommodityId = CommodityId,
            CommodityName = CommodityName,
            Quantity = Quantity,
            VolumePerUnit = VolumePerUnit
        };
    }

    /// <summary>
    /// The portion of one mission reservation removed by an authoritative
    /// confiscation. The cargo hold reports this fact; MissionManager decides
    /// whether the owning mission must fail.
    /// </summary>
    public sealed class MissionCargoConfiscation
    {
        public int MissionId { get; init; }
        public string CommodityId { get; init; } = string.Empty;
        public int Quantity { get; init; }
    }

    /// <summary>
    /// Manages a ship's cargo hold and commodity inventory
    /// </summary>
    public class CargoHold
    {
        private Dictionary<string, int> _commodities = new Dictionary<string, int>();
        private readonly Dictionary<int, MissionCargoReservation> _missionCargo = new();
        // Freight contracts reserve only the ordinary units already present in
        // the hold and keep a target here so future authoritative additions
        // can satisfy the remaining reservation automatically.
        private readonly Dictionary<int, MissionCargoReservation> _missionReservationTargets = new();
        
        public int MaxCapacity { get; private set; }
        public int UsedCapacity { get; private set; }
        public int AvailableCapacity => MaxCapacity - UsedCapacity;

        public CargoHold(int maxCapacity)
        {
            MaxCapacity = Math.Max(0, maxCapacity);
            UsedCapacity = 0;
        }

        /// <summary>
        /// Get the quantity of a specific commodity
        /// </summary>
        public int GetCommodityQuantity(string commodityName)
        {
            return _commodities.TryGetValue(commodityName, out int quantity) ? quantity : 0;
        }

        /// <summary>
        /// Get all commodities in cargo hold
        /// </summary>
        public Dictionary<string, int> GetAllCommodities()
        {
            return new Dictionary<string, int>(_commodities);
        }

        public IReadOnlyList<MissionCargoReservation> GetMissionCargoReservations()
        {
            return _missionCargo.Values.Select(reservation => reservation.Clone()).ToList();
        }

        public int GetMissionCargoQuantity(int missionId)
        {
            return _missionCargo.TryGetValue(missionId, out MissionCargoReservation reservation)
                ? reservation.Quantity
                : 0;
        }

        public bool HasMissionCargo(int missionId, string commodityId, int quantity)
        {
            if (missionId <= 0 || quantity <= 0 || !_missionCargo.TryGetValue(missionId, out MissionCargoReservation reservation))
            {
                return false;
            }

            return reservation.Quantity == quantity &&
                (string.IsNullOrWhiteSpace(commodityId) ||
                 string.Equals(reservation.CommodityId, commodityId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(reservation.CommodityName, commodityId, StringComparison.OrdinalIgnoreCase));
        }

        public int GetMissionReservedQuantity(string commodityName)
        {
            if (string.IsNullOrWhiteSpace(commodityName))
            {
                return 0;
            }

            long reserved = _missionCargo.Values
                .Where(reservation =>
                    string.Equals(reservation.CommodityName, commodityName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(reservation.CommodityId, commodityName, StringComparison.OrdinalIgnoreCase))
                .Sum(reservation => (long)Math.Max(0, reservation.Quantity));
            return (int)Math.Clamp(reserved, 0L, int.MaxValue);
        }

        public int GetSellableCommodityQuantity(string commodityName)
        {
            return Math.Max(0, GetCommodityQuantity(commodityName) - GetMissionReservedQuantity(commodityName));
        }

        /// <summary>
        /// Check if cargo hold can accommodate additional units of a commodity
        /// </summary>
        public bool CanFit(Commodity commodity, int quantity)
        {
            if (commodity == null || quantity <= 0 || commodity.VolumePerUnit <= 0)
            {
                return false;
            }

            long requiredSpace = (long)commodity.VolumePerUnit * quantity;
            return requiredSpace <= AvailableCapacity;
        }

        /// <summary>
        /// Add commodity to cargo hold
        /// </summary>
        public bool AddCommodity(Commodity commodity, int quantity)
        {
            if (commodity == null || string.IsNullOrWhiteSpace(commodity.Name) || quantity <= 0 || !CanFit(commodity, quantity))
                return false;

            if (_commodities.TryGetValue(commodity.Name, out int currentQuantity))
            {
                if ((long)currentQuantity + quantity > int.MaxValue)
                {
                    return false;
                }

                _commodities[commodity.Name] = currentQuantity + quantity;
            }
            else
            {
                _commodities[commodity.Name] = quantity;
            }

            UsedCapacity += commodity.VolumePerUnit * quantity;
            SatisfyMissionReservationTargets(commodity);
            return true;
        }

        /// <summary>
        /// Adds as much of an ordinary commodity as valid free capacity allows.
        /// This is the authoritative partial-pickup seam used by physical
        /// salvage; mission reservations remain accounted for in UsedCapacity.
        /// </summary>
        public bool TryAddCommodityPartial(Commodity commodity, int requestedQuantity, out int addedQuantity)
        {
            addedQuantity = 0;
            if (commodity == null || requestedQuantity <= 0 || commodity.VolumePerUnit <= 0)
            {
                return false;
            }

            int capacityQuantity = AvailableCapacity / commodity.VolumePerUnit;
            int quantity = Math.Min(requestedQuantity, Math.Max(0, capacityQuantity));
            if (quantity <= 0 || !AddCommodity(commodity, quantity))
            {
                return false;
            }

            addedQuantity = quantity;
            return true;
        }

        public bool TryAddMissionCommodityPartial(
            int missionId,
            Commodity commodity,
            int requestedQuantity,
            out int addedQuantity)
        {
            addedQuantity = 0;
            if (missionId <= 0 || commodity == null || requestedQuantity <= 0 || commodity.VolumePerUnit <= 0)
                return false;

            int capacityQuantity = AvailableCapacity / commodity.VolumePerUnit;
            int quantity = Math.Min(requestedQuantity, Math.Max(0, capacityQuantity));
            if (quantity <= 0 || !AddMissionCargoQuantity(missionId, commodity, quantity))
                return false;

            addedQuantity = quantity;
            return true;
        }

        /// <summary>
        /// Registers a freight contract's required ordinary quantity. Existing
        /// units are reserved immediately; later AddCommodity calls reserve
        /// only the remaining amount. The target itself does not add cargo.
        /// </summary>
        public bool RegisterFreightReservation(int missionId, Commodity commodity, int requiredQuantity)
        {
            if (missionId <= 0 || commodity == null || commodity.IsMissionCargo ||
                string.IsNullOrWhiteSpace(commodity.Id) || string.IsNullOrWhiteSpace(commodity.Name) ||
                requiredQuantity <= 0)
            {
                return false;
            }

            if (_missionCargo.TryGetValue(missionId, out MissionCargoReservation existingReservation))
            {
                if (!string.Equals(existingReservation.CommodityId, commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                    existingReservation.Quantity > requiredQuantity)
                {
                    return false;
                }

                _missionReservationTargets[missionId] = new MissionCargoReservation
                {
                    MissionId = missionId,
                    CommodityId = commodity.Id,
                    CommodityName = commodity.Name,
                    Quantity = requiredQuantity,
                    VolumePerUnit = commodity.VolumePerUnit
                };
                SatisfyMissionReservationTargets(commodity);
                return true;
            }

            if (_missionReservationTargets.ContainsKey(missionId))
            {
                return false;
            }

            _missionReservationTargets[missionId] = new MissionCargoReservation
            {
                MissionId = missionId,
                CommodityId = commodity.Id,
                CommodityName = commodity.Name,
                Quantity = requiredQuantity,
                VolumePerUnit = commodity.VolumePerUnit
            };
            SatisfyMissionReservationTargets(commodity);
            return true;
        }

        public int GetMissionReservationTargetQuantity(int missionId)
        {
            return _missionReservationTargets.TryGetValue(missionId, out MissionCargoReservation target)
                ? target.Quantity
                : 0;
        }

        /// <summary>Releases a reservation without removing its ordinary cargo.</summary>
        public bool ReleaseMissionCargoReservation(int missionId)
        {
            bool removed = _missionCargo.Remove(missionId);
            removed |= _missionReservationTargets.Remove(missionId);
            return removed;
        }

        public bool HasMissionReservationTarget(int missionId)
        {
            return _missionReservationTargets.ContainsKey(missionId);
        }

        private void SatisfyMissionReservationTargets(Commodity commodity)
        {
            if (commodity == null || string.IsNullOrWhiteSpace(commodity.Name))
            {
                return;
            }

            foreach (MissionCargoReservation target in _missionReservationTargets.Values
                         .Where(candidate => string.Equals(candidate.CommodityId, commodity.Id, StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                int reserved = GetMissionCargoQuantity(target.MissionId);
                int remaining = Math.Max(0, target.Quantity - reserved);
                if (remaining <= 0)
                {
                    continue;
                }

                int available = Math.Max(0, GetCommodityQuantity(commodity.Name) - GetMissionReservedQuantity(commodity.Name));
                int additional = Math.Min(remaining, available);
                if (additional <= 0)
                {
                    continue;
                }

                if (_missionCargo.TryGetValue(target.MissionId, out MissionCargoReservation reservation))
                {
                    reservation.Quantity = checked(reservation.Quantity + additional);
                    reservation.VolumePerUnit = commodity.VolumePerUnit;
                }
                else
                {
                    _missionCargo[target.MissionId] = new MissionCargoReservation
                    {
                        MissionId = target.MissionId,
                        CommodityId = commodity.Id,
                        CommodityName = commodity.Name,
                        Quantity = additional,
                        VolumePerUnit = commodity.VolumePerUnit
                    };
                }
            }
        }

        /// <summary>
        /// Adds package cargo and binds it to one mission. Normal trading APIs
        /// cannot remove this quantity; delivery must use RemoveMissionCargo.
        /// </summary>
        public bool AddMissionCargo(int missionId, Commodity commodity, int quantity)
        {
            if (missionId <= 0 || commodity == null || quantity <= 0 || _missionCargo.ContainsKey(missionId) ||
                _missionReservationTargets.ContainsKey(missionId) ||
                !CanFit(commodity, quantity))
            {
                return false;
            }

            if (!AddCommodity(commodity, quantity))
            {
                return false;
            }

            _missionCargo[missionId] = new MissionCargoReservation
            {
                MissionId = missionId,
                CommodityId = commodity.Id ?? string.Empty,
                CommodityName = commodity.Name ?? string.Empty,
                Quantity = quantity,
                VolumePerUnit = commodity.VolumePerUnit
            };
            return true;
        }

        /// <summary>
        /// Adds a physical mission drop to the ordinary commodity stack while
        /// increasing that mission's authoritative reservation. This is the
        /// pickup seam for cargo-interdiction pods; it never bypasses capacity.
        /// </summary>
        public bool AddMissionCargoQuantity(int missionId, Commodity commodity, int quantity)
        {
            if (missionId <= 0 || commodity == null || quantity <= 0 ||
                string.IsNullOrWhiteSpace(commodity.Id) || string.IsNullOrWhiteSpace(commodity.Name) ||
                !CanFit(commodity, quantity))
            {
                return false;
            }

            if (_missionCargo.TryGetValue(missionId, out MissionCargoReservation reservation))
            {
                if (!string.Equals(reservation.CommodityId, commodity.Id, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(reservation.CommodityName, commodity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!AddCommodity(commodity, quantity) ||
                    (long)reservation.Quantity + quantity > int.MaxValue)
                {
                    if (_commodities.TryGetValue(commodity.Name, out int current) && current >= quantity)
                    {
                        _commodities[commodity.Name] = current - quantity;
                        if (_commodities[commodity.Name] == 0)
                            _commodities.Remove(commodity.Name);
                        UsedCapacity -= commodity.VolumePerUnit * quantity;
                    }
                    return false;
                }

                reservation.Quantity += quantity;
                reservation.VolumePerUnit = commodity.VolumePerUnit;
                return true;
            }

            if (!AddCommodity(commodity, quantity))
                return false;

            _missionCargo[missionId] = new MissionCargoReservation
            {
                MissionId = missionId,
                CommodityId = commodity.Id,
                CommodityName = commodity.Name,
                Quantity = quantity,
                VolumePerUnit = commodity.VolumePerUnit
            };
            return true;
        }

        /// <summary>
        /// Removes exactly one mission's reserved package. This is intentionally
        /// separate from RemoveCommodity so market/trading code cannot bypass
        /// mission-cargo protection.
        /// </summary>
        public bool RemoveMissionCargo(int missionId, Commodity commodity, int quantity)
        {
            if (missionId <= 0 || commodity == null || quantity <= 0 ||
                !_missionCargo.TryGetValue(missionId, out MissionCargoReservation reservation) ||
                reservation.Quantity != quantity ||
                (!string.Equals(reservation.CommodityId, commodity.Id, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(reservation.CommodityName, commodity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (!_commodities.TryGetValue(reservation.CommodityName, out int currentQuantity) || currentQuantity < quantity)
            {
                return false;
            }

            _commodities[reservation.CommodityName] -= quantity;
            if (_commodities[reservation.CommodityName] == 0)
            {
                _commodities.Remove(reservation.CommodityName);
            }

            UsedCapacity -= reservation.VolumePerUnit * quantity;
            _missionCargo.Remove(missionId);
            _missionReservationTargets.Remove(missionId);
            return true;
        }

        public bool RemoveMissionCargoQuantity(int missionId, Commodity commodity, int quantity, out int removedQuantity)
        {
            removedQuantity = 0;
            if (missionId <= 0 || commodity == null || quantity <= 0 ||
                !_missionCargo.TryGetValue(missionId, out MissionCargoReservation reservation) ||
                (!string.Equals(reservation.CommodityId, commodity.Id, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(reservation.CommodityName, commodity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            int removable = Math.Min(quantity, reservation.Quantity);
            if (!_commodities.TryGetValue(reservation.CommodityName, out int currentQuantity) || currentQuantity < removable)
                return false;

            _commodities[reservation.CommodityName] = currentQuantity - removable;
            if (_commodities[reservation.CommodityName] == 0)
                _commodities.Remove(reservation.CommodityName);
            UsedCapacity -= reservation.VolumePerUnit * removable;
            reservation.Quantity -= removable;
            removedQuantity = removable;
            if (reservation.Quantity <= 0)
            {
                _missionCargo.Remove(missionId);
                _missionReservationTargets.Remove(missionId);
            }
            return true;
        }

        /// <summary>Jettisons only attributed mission units and never rewrites progress.</summary>
        public bool JettisonMissionCargo(int missionId, Commodity commodity, int quantity, out int removedQuantity) =>
            RemoveMissionCargoQuantity(missionId, commodity, quantity, out removedQuantity);

        /// <summary>Releases attribution while preserving legitimately owned surplus.</summary>
        public bool ConvertMissionCargoToOrdinary(int missionId)
        {
            return _missionCargo.Remove(missionId) | _missionReservationTargets.Remove(missionId);
        }

        /// <summary>
        /// Remove commodity from cargo hold
        /// </summary>
        public bool RemoveCommodity(Commodity commodity, int quantity)
        {
            if (commodity == null || quantity <= 0 || !_commodities.ContainsKey(commodity.Name))
                return false;

            int currentQuantity = _commodities[commodity.Name];
            int sellableQuantity = currentQuantity - GetMissionReservedQuantity(commodity.Name);
            if (sellableQuantity < quantity)
                return false;

            _commodities[commodity.Name] -= quantity;
            if (_commodities[commodity.Name] == 0)
            {
                _commodities.Remove(commodity.Name);
            }

            UsedCapacity -= commodity.VolumePerUnit * quantity;
            return true;
        }

        /// <summary>
        /// Removes several ordinary commodity stacks as one validated cargo
        /// operation. Enforcement and other service authorities use this
        /// seam so a failed validation cannot leave a partial confiscation.
        /// </summary>
        public bool TryRemoveCommodityBatch(IReadOnlyDictionary<string, int> requested)
        {
            if (requested == null || requested.Count == 0)
            {
                return false;
            }

            Dictionary<string, int> normalized = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> entry in requested)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0)
                {
                    return false;
                }

                normalized[entry.Key] = normalized.TryGetValue(entry.Key, out int existing)
                    ? checked(existing + entry.Value)
                    : entry.Value;
            }

            List<(Commodity Commodity, int Quantity)> removals = new();
            foreach (KeyValuePair<string, int> entry in normalized)
            {
                Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
                if (commodity == null || GetSellableCommodityQuantity(entry.Key) < entry.Value)
                {
                    return false;
                }

                removals.Add((commodity, entry.Value));
            }

            List<(Commodity Commodity, int Quantity)> completed = new();
            foreach ((Commodity Commodity, int Quantity) removal in removals)
            {
                if (RemoveCommodity(removal.Commodity, removal.Quantity))
                {
                    completed.Add(removal);
                    continue;
                }

                foreach ((Commodity Commodity, int Quantity) rollback in completed)
                {
                    AddCommodity(rollback.Commodity, rollback.Quantity);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Removes illegal cargo as a seizure rather than a market/trading
        /// removal. Unlike RemoveCommodity, this path may remove reserved
        /// mission units and updates those reservations in the same mutation.
        /// Legal cargo is never accepted by this API.
        /// </summary>
        public bool TryConfiscateContraband(
            IReadOnlyDictionary<string, int> requested,
            out IReadOnlyList<MissionCargoConfiscation> missionConfiscations)
        {
            missionConfiscations = Array.Empty<MissionCargoConfiscation>();
            if (requested == null || requested.Count == 0)
                return false;

            Dictionary<string, int> normalized = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> entry in requested)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0)
                    return false;

                Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
                if (commodity?.IsContraband != true || GetCommodityQuantity(commodity.Name) < entry.Value)
                    return false;

                try
                {
                    normalized[commodity.Name] = normalized.TryGetValue(commodity.Name, out int existing)
                        ? checked(existing + entry.Value)
                        : entry.Value;
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            // Every validation is complete before any stack or reservation is
            // mutated, keeping a multi-commodity seizure all-or-nothing.
            List<(Commodity Commodity, int Quantity)> removals = new();
            foreach (KeyValuePair<string, int> entry in normalized)
            {
                Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
                if (commodity == null || !commodity.IsContraband ||
                    !_commodities.TryGetValue(commodity.Name, out int current) || current < entry.Value)
                {
                    return false;
                }

                removals.Add((commodity, entry.Value));
            }

            List<MissionCargoConfiscation> confiscations = new();
            foreach ((Commodity Commodity, int Quantity) removal in removals)
            {
                if (!_commodities.TryGetValue(removal.Commodity.Name, out int currentQuantity) ||
                    currentQuantity < removal.Quantity)
                {
                    return false;
                }

                int remainingToRemove = removal.Quantity;
                foreach (MissionCargoReservation reservation in _missionCargo.Values
                             .Where(candidate => candidate != null &&
                                 (string.Equals(candidate.CommodityId, removal.Commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(candidate.CommodityName, removal.Commodity.Name, StringComparison.OrdinalIgnoreCase)))
                             .OrderBy(candidate => candidate.MissionId)
                             .ToList())
                {
                    if (remainingToRemove <= 0)
                        break;

                    int reservedRemoval = Math.Min(remainingToRemove, Math.Max(0, reservation.Quantity));
                    if (reservedRemoval <= 0)
                        continue;

                    reservation.Quantity -= reservedRemoval;
                    remainingToRemove -= reservedRemoval;
                    confiscations.Add(new MissionCargoConfiscation
                    {
                        MissionId = reservation.MissionId,
                        CommodityId = removal.Commodity.Id ?? string.Empty,
                        Quantity = reservedRemoval
                    });

                    if (reservation.Quantity <= 0)
                    {
                        _missionCargo.Remove(reservation.MissionId);
                        _missionReservationTargets.Remove(reservation.MissionId);
                    }
                }

                _commodities[removal.Commodity.Name] = currentQuantity - removal.Quantity;
                if (_commodities[removal.Commodity.Name] <= 0)
                    _commodities.Remove(removal.Commodity.Name);

                UsedCapacity -= removal.Commodity.VolumePerUnit * removal.Quantity;
                UsedCapacity = Math.Max(0, UsedCapacity);
            }

            missionConfiscations = confiscations;
            return true;
        }

        /// <summary>
        /// Remove every contraband commodity stack from the hold.
        /// Returns the removed stacks keyed by commodity id/name for logging.
        /// </summary>
        public Dictionary<string, int> RemoveContraband()
        {
            var removed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in GetAllCommodities())
            {
                Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
                if (commodity?.IsContraband != true || entry.Value <= 0)
                {
                    continue;
                }

                if (RemoveCommodity(commodity, entry.Value))
                {
                    removed[commodity.Id ?? commodity.Name] = entry.Value;
                }
            }

            return removed;
        }

        /// <summary>
        /// Clear all commodities (used when selling all cargo)
        /// </summary>
        public void Clear()
        {
            _commodities.Clear();
            _missionCargo.Clear();
            _missionReservationTargets.Clear();
            UsedCapacity = 0;
        }

        /// <summary>
        /// Transfer cargo to a new cargo hold (for ship changes)
        /// Returns false if cargo doesn't fit
        /// </summary>
        public bool TransferTo(CargoHold newCargoHold, Dictionary<Commodity, int> commodityRegistry)
        {
            if (newCargoHold == null || commodityRegistry == null)
            {
                return false;
            }

            // Check if everything fits
            long totalRequiredSpace = 0;
            foreach (var kvp in _commodities)
            {
                var commodity = commodityRegistry.FirstOrDefault(c => c.Key.Name == kvp.Key).Key;
                if (commodity != null)
                {
                    totalRequiredSpace += (long)commodity.VolumePerUnit * kvp.Value;
                }
            }

            if (totalRequiredSpace > newCargoHold.MaxCapacity)
                return false;

            // Transfer ordinary cargo first, excluding quantities already
            // reserved by missions. Then copy the reservations through the
            // mission-aware API so they remain protected after transfer.
            foreach (var kvp in _commodities)
            {
                var commodity = commodityRegistry.FirstOrDefault(c => c.Key.Name == kvp.Key).Key;
                if (commodity != null)
                {
                    int ordinaryQuantity = Math.Max(0, kvp.Value - GetMissionReservedQuantity(kvp.Key));
                    if (ordinaryQuantity > 0 && !newCargoHold.AddCommodity(commodity, ordinaryQuantity))
                    {
                        return false;
                    }
                }
            }

            foreach (MissionCargoReservation reservation in _missionCargo.Values)
            {
                Commodity commodity = commodityRegistry.Keys.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, reservation.CommodityId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Name, reservation.CommodityName, StringComparison.OrdinalIgnoreCase));
                if (commodity == null || !newCargoHold.AddMissionCargo(reservation.MissionId, commodity, reservation.Quantity))
                {
                    return false;
                }
            }

            foreach (MissionCargoReservation target in _missionReservationTargets.Values)
            {
                Commodity commodity = commodityRegistry.Keys.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, target.CommodityId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Name, target.CommodityName, StringComparison.OrdinalIgnoreCase));
                if (commodity == null || !newCargoHold.RegisterFreightReservation(target.MissionId, commodity, target.Quantity))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get total value of all cargo
        /// </summary>
        public int GetTotalValue(Dictionary<Commodity, int> commodityRegistry)
        {
            int totalValue = 0;
            foreach (var kvp in _commodities)
            {
                var commodity = commodityRegistry.FirstOrDefault(c => c.Key.Name == kvp.Key).Key;
                if (commodity != null)
                {
                    totalValue += commodity.BasePrice * GetSellableCommodityQuantity(kvp.Key);
                }
            }
            return totalValue;
        }

        /// <summary>
        /// Update max capacity (for ship changes)
        /// </summary>
        public void SetMaxCapacity(int newMaxCapacity)
        {
            MaxCapacity = Math.Max(0, newMaxCapacity);
        }
    }
}
