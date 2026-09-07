# Phase 62 — Adaptive Trader Routing

Phase 62 adds bounded destination selection for ordinary ambient economic
traders. `TrafficManager` remains the owner of loaded traffic-zone topology;
`EconomicShipmentManager` owns shipment lifecycle and exposes that topology to
`AdaptiveTraderRoutingPlanner`. The planner ranks only configured
`TraderRoute` edges in the current loaded system. It does not pathfind through
the galaxy, use tradelane geometry as a second graph, or reroute an NPC that is
already in flight.

## Planning policy

At spawn time, the planner resolves the trader's current origin from its
configured route, validates route endpoints against real market stations, and
evaluates at most eight deterministic route candidates. A candidate must have
a real ordinary market at both ends, a non-self destination, finite endpoint
geometry, and a configured direct edge from the origin. The authored spawn
route is retained as a fallback candidate whenever valid.

For each legal commodity the bounded score is:

```text
clamp(destination buy quote - origin buy quote, -1000, 4000)
+ clamp(shortage bonus, 0, 250)
+ clamp(effective deficit * 5, 0, 150)
- clamp(route endpoint distance / 1000, 0, 250)
+ stable trader/route/commodity diversity bias (0..3)
```

The total is clamped to `[-1500, 5000]`. Shortage bonus is
`clamp(50 + (100 - stock percent) * 2, 50, 250)` only while the real shortage
has remaining effective deficit. Effective deficit is actual destination
baseline deficit minus active inbound committed cargo, clamped at zero.

Dynamic routing must beat the authored default by at least 25 score points;
otherwise the default route remains meaningful. If the default has no legal
shipment, the best valid configured opportunity is used. No random value is
used for planning or tie-breaking.

## Shipment and lifecycle

The selected route and commodity opportunity are chosen before stock mutation.
The existing Phase 59 manifest remains authoritative: one to three legal
commodity types, two to twelve total units, real origin debit, exact delivery,
partial piracy, bounded salvage, and one-time settlement. If the best
commodity has a remaining deficit, the next manifest is capped to that
remaining need (with the existing two-unit minimum), preventing relief-trader
herding without changing accepted Emergency Supply mission semantics.

`GetInboundQuantity(destination, commodity)` derives its value from active
remaining manifests. It therefore drops immediately when cargo is surrendered,
and becomes zero when a trader is destroyed, delivered, despawned, or torn down.
Pending save snapshots participate only during the short rebind window. Save
load restores the saved route ID, direction, position, progress, and manifest;
it never rescans or debits the origin a second time.

Mission-owned traffic is excluded by the existing mission ownership predicate.
Police, military, rogue combat, convoy, and mission traffic do not enter this
ordinary economic planner. Lawful manifests continue to exclude canonical
contraband and black-market routing is unchanged.

## Physical topology scope

The production configuration now has direct lawful edges for Fort Bush/Newark
and the Rochester/Newark, Rochester/Detroit, and Detroit/Newark market links.
The planner can choose among these loaded same-system edges. Existing fixed
cross-system trade routes remain owned by their existing navigation systems;
general multi-hop economic route planning is intentionally deferred.

## Verification

Run the focused suite with:

```text
dotnet run -- --phase62-smoke
```

The suite covers live price/shortage selection, topology validation and
candidate bounds, physical endpoint rebinding, fallback/no-debit behavior,
mission and contraband exclusion, inbound accounting through piracy and loss,
replacement opportunity, save/load without duplicate inbound or debit, exact
delivery, and scanner route presentation.
