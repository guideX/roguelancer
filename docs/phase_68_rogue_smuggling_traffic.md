# Phase 68 — Rogue Smuggling Traffic & Live Criminal Supply

Phase 68 adds a bounded ambient criminal logistics path. A configured Rogue
`TraderRoute` now spawns a real Rogue NPC carrier, reserves exact contraband
from an existing criminal market, moves through the normal NPC route behavior,
and either delivers that manifest to another criminal market or loses it to
physical salvage.

## Architecture and ownership

- `MarketManager` remains the sole authority for station stock, black-market
  listings, criminal supply removal/receipt, and price refresh.
- `RogueSmugglingManager` owns only the bounded shipment lifecycle and the
  relationship between a shipment and its real carrier. It does not maintain
  another inventory or credit balance.
- `TrafficManager` owns spawning, population limits, NPC registration,
  faction identity, combat integration, route-arrival events, and normal
  carrier retirement.
- `NpcShip` owns physical movement, damage, destruction, stable identity, and
  the existing `TraderRoute` endpoint behavior.
- `LootManager` remains the physical `CargoPod` authority. Phase 68 supplies
  its existing economic salvage callbacks; it does not create a parallel loot
  path.
- `PlayerTargetScanService` reads the same authoritative manifest and route
  identity used by the shipment manager.
- `SaveGameData` persists active Phase 68 manifests and carrier route state.

Phase 68 routes are marked explicitly with `is_rogue_smuggling_route` so they
are excluded from ordinary lawful ambient shipment attachment. This keeps
Phase 66 accounting and Phase 68 accounting disjoint.

## Shipment lifecycle

1. After the bounded 45-second interval, `RogueSmugglingManager` selects the
   deterministic eligible route if fewer than two smuggling shipments are
   active.
2. `TrafficManager` spawns a normal `NpcShip` on that route using the existing
   `Transport Ship Alpha` definition, Rogue faction identity, normal NPC
   registration, and normal route movement.
3. The manager chooses up to two eligible contraband listings from the source
   criminal market. It verifies destination capacity and removes the exact
   quantities through `MarketManager.TryRemoveCriminalSupply` before attaching
   those same `TraderCargoStack` objects to the carrier.
4. The carrier is marked as a Rogue smuggler for presentation and lifecycle
   lookup, but remains an ordinary attackable, targetable, scannable NPC.
5. The existing route endpoint event calls the manager. A valid destination
   receives every remaining contraband stack through
   `MarketManager.TryAddCriminalSupply`; the receipt is all-or-nothing and the
   shipment is settled once before the carrier is retired normally.
6. A destroyed carrier is marked lost. Its remaining manifest is passed to
   the existing destruction salvage path and becomes physical commodity pods.
   No destination receipt occurs. Loot consumption and finalization make the
   lost manifest non-reusable.
7. A non-destruction retirement or timeout is also marked lost. World teardown
   restores the committed remaining stock to the source according to the
   existing transient-world reset semantics.

The configured route is Buffalo Base (`buffalo_base`) to Rochester Base
(`rochester_base`) in New York. Both locations already have criminal market
configuration; no warehouse or hidden pirate inventory was added.

## Conservation

For every active shipment, the source debit is made once and equals the sum of
the carrier's initial stack quantities:

`source debit == carrier initial manifest`

On successful arrival, the remaining manifest is transferred exactly once:

`source decrease == carrier cargo == destination increase`

On destruction:

`source decrease == physical dropped cargo + any later explicitly modeled loss`

and destination increase remains zero. Delivery preflights every stack before
mutating destination stock, and shipment identity plus settlement history
prevent repeated endpoint ticks from paying twice.

## Combat, scanner, and trade-lane behavior

The carrier uses `liberty_rogues` faction identity and the existing NPC faction
combat acquisition path. Liberty Police or another hostile lawful NPC can
acquire it with the same combat targeting rules used for other NPCs; no
special police shooting path was added. Player damage and destruction use the
normal `NpcShip` and `LootManager` paths.

Scanner output is derived from the live manifest. Phase 68 stacks are reported
as contraband and are not marked stolen. A Rogue carrier is not automatically
treated as a Phase 67 stolen-haul carrier.

The smuggler is an existing `TraderRoute` NPC and is intentionally not a
second Rogue-only lane implementation. The Buffalo–Rochester route currently
has no matching configured `TradeLaneManager` endpoint pair, so it uses normal
free-space trader-route movement while sharing the same NPC traffic, combat,
destruction, and transit guards. If a compatible trade-lane transition is
introduced later, the existing `NpcShip.IsTradeLaneTransit` behavior remains
authoritative.

## Save/load and reset

The save schema is now version 13. Active shipments persist stable identity,
route and station IDs, direction, exact initial/remaining stacks, carrier
position, velocity, and traffic age. Loading rebinds the saved carrier by
identity and reconstructs the saved manifest without removing source stock a
second time. If a saved carrier cannot be restored, remaining committed stock
is returned to its source during rebind completion.

Delivered and lost shipments are terminal and are not regenerated. Reset,
system teardown, and new-world setup clear transient shipment references and
restore active committed source stock; they do not deliver cargo, award
credits, or leave a carrier or manifest available to the next session.

## Boundedness and determinism

The layer is capped at two active shipments, two commodity types per shipment,
eight total units per shipment, a 45-second spawn interval, a 480-second
carrier lifetime, and two saved shipments. Route and commodity ordering is
stable, and the shipment seed is derived from stable carrier identity, route,
source, and destination IDs.

## Phase 67 integration

Lawful ambient cargo remains owned by `EconomicShipmentManager`; Rogue loot
delivery remains owned by its Phase 67 shipment state. Phase 68 carriers are
not attachable to that lawful shipment manager, and their scanner snapshots
use `IsStolen = false`. If a Rogue later picks up a contraband pod through the
existing physical loot path, the existing criminal delivery policy remains the
canonical receipt boundary. Legal stolen goods still resolve through the Phase
57 fence-only policy.

## Smoke coverage

`--phase68-smoke` runs the live production proof first: the initialized
`TrafficManager` spawns the configured Rogue carrier from the real Buffalo and
Rochester objects, reserves canonical contraband, physically moves the carrier
to its endpoint, and verifies the real destination black-market stock increase.
It then runs 14 focused checks covering source reservation, carrier manifest,
scanner output, arrival and idempotence, lawful combat acquisition,
destruction drops, no-credit-on-loss, transit save/load, post-load arrival and
destruction, reset, Phase 57 fencing, and Phase 67 provenance separation.

Known limitation: the current Buffalo–Rochester route is a free-space
`TraderRoute`, not a newly authored trade lane or convoy. Phase 68 deliberately
does not add route-planning UI, escort AI, new commodities, or a second
economy.
