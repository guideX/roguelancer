# Phase 67 — Rogue loot escape and criminal delivery

Phase 67 extends the Phase 66 ambient raid without adding a pirate-only
warehouse, hidden inventory, wallet, or teleport destination. A raid still
targets one real, non-mission `EconomicShipment` and the shipment manifest is
the only authority for cargo removed from the trader.

## Runtime flow

1. Phase 66 schedules and activates a bounded raid using real `NpcShip`
   raiders. The trader's surrender callback first creates a real stolen
   `CargoPod`; only the quantity physically released is removed from the
   economic manifest.
2. Assigned raiders seek only non-mission stolen commodity pods whose source
   is the attacked trader. Pickup uses the existing close-range NPC cargo
   callback, so a player and a Rogue compete for the same pod rather than
   duplicating it.
3. After recovery or the bounded 15-second recovery window, raiders with haul
   enter `ReturningWithLoot`. Each carrier is assigned the nearest eligible
   existing criminal market station, ordered deterministically by distance,
   station id, and station name.
4. The carrier flies there in ordinary space using the existing
   `TrafficEncounterState.Fleeing` movement. Ambient raiders have no trade-lane
   endpoints and are never teleported or routed through a hidden pirate
   inventory. The 150-second return timer is simulation time.
5. Within 500 metres of the receiver, the carrier submits every saved haul
   stack through `MarketManager.TryAddCriminalSupply`. Delivery is exact and
   all-or-nothing per carrier: the haul is cleared only after the market
   authority accepts every stack. A receiver becoming unavailable can select a
   later deterministic eligible station; if none can accept the exact haul,
   the carrier drops it physically or records a bounded loss.

## Market policy

The receiver is an existing market station with a configured eligible faction
(`Liberty Rogues` or `Junkers`) and a configured contraband listing. Contraband
is added to that station's canonical shared listing stock and prices refresh
immediately. Legal stolen goods use the established Phase 57 fence-only sink:
the fence accepts the receipt, but legal stolen cargo is not silently turned
into clean or black-market stock.

No player reputation, credits, or cargo hold are mutated by NPC receipt. The
same market listing object is used by ordinary trade, black-market trade, and
criminal delivery.

## Failure and persistence rules

- Destroying a carrier never delivers its haul. The exact remaining quantity
  is released as stolen physical pods when capacity permits; unavailable pod
  capacity is recorded as loss and the in-memory haul is cleared.
- Carrier combat remains possible. Player targeting, police interception, and
  ordinary NPC combat can interrupt the return. If the carrier survives and
  combat ends in cruising, the return objective is reasserted.
- Save data stores the raid state, recovery/return timers, stable carrier
  identity, exact haul stacks, receiver identity, and settled/lost flags. Load
  rebinds the physical carrier and does not spawn a second pod or credit a
  receiver twice.
- System teardown is explicitly a loss boundary for pending Rogue haul. It
  retires carriers and clears their transient haul without market credit;
  physical cargo and economic shipment teardown remain owned by their normal
  managers.

The production scope is intentionally same-system and bounded. Cross-system
criminal routes require a future explicit jump-hole/travel authority rather
than an implicit teleport.
