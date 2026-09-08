# Phase 64 — Dynamic Shipment Interdiction

Phase 64 adds the Rogue `Shipment Interdiction` mission. It is an outlaw
counterpart to Phase 63's dynamic escort demand, but it binds to an already
moving Phase 59 `EconomicShipment` instead of creating a replacement convoy.

## Policy

- Rogue intelligence is same-system only. The contact sees at most 16 active
  shipment candidates and publishes at most 3 offers.
- A shipment qualifies at `2,500 CR` remaining manifest value or a critical
  destination shortage. Ranking is deterministic: current manifest value,
  route risk, critical-shortage bonus (`1,500`), and bounded remaining
  quantity/progress weight.
- Offers last 60 seconds and are revalidated against the live shipment,
  route, trader, manifest, commodity, and lock state at acceptance.
- Accepted interdiction locks the shipment against escort attachment; accepted
  escort locks it against interdiction. A qualifying live shipment suppresses
  synthetic Phase 51 raid offers for that Rogue board refresh.
- The trader continues its Phase 59/62 route after acceptance. Waypoints and
  scanner cargo follow the live trader and shared manifest.

## Cargo and economy

The mission commodity is selected from the current real manifest by value,
quantity, then commodity id. Required quantity is difficulty-derived and
bounded to the live stack and eight units. Reward is bounded to 3,000–30,000
credits and combines a contract premium, canonical cargo value, route risk,
and a critical-shortage urgency premium.

Phase 56 demand is authorized only for the accepted interdiction target. A
compliant demand removes units from the real manifest and spawns ordinary
physical stolen pods; the mission counts only picked-up mission-attributed
units. Destroying the trader settles the shipment lost and exposes bounded
salvage from the remaining real manifest. No mission-only cargo copy is
created.

Mission attribution and stolen provenance remain separate `CargoHold` fields.
Capacity, jettison/re-pickup, pod expiry, Police confiscation, and black-market
reservation rules therefore remain authoritative. Turn-in removes exactly the
required attributed quantity, pays once, applies Rogue reputation once, and
releases any surplus as ordinary stolen cargo.

The destination receives only the manifest remainder if the trader survives;
destroyed shipments deliver nothing. Route risk records the existing
extortion/destruction incident and the mission adds no duplicate incident.

## Persistence and verification

Schema 12 stores the shipment identity/route lock and durable interdiction
stage, source/released/lost/recovered quantities, remaining possible quantity,
and destroyed/delivered state. Physical cargo pods continue to use the normal
Phase 51 save/rebind path.

Focused coverage is available with:

```text
dotnet run --project Roguelancer.csproj -- --phase64-smoke
```

The smoke suite proves bounded local intelligence, real target binding,
opposing ownership locks, nonlethal physical extortion, destruction salvage,
exact Rogue turn-in, stale offers, and mission field reconstruction.
