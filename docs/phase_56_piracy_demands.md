# Phase 56 — Pirate Cargo Demands

Phase 56 adds a bounded `Y` action for extorting an ordinary free-roam trader.
The interaction is owned by `PirateCargoDemandService`; it binds one demand to
the trader's stable identity, responds deterministically within three seconds,
and resolves once. Eligible traffic is limited to `TraderRoute` ships in
`liberty_corporations` or `neutral_civilians` that are not destroyed, in lane
transit, mission-owned, or already engaged.

Trader manifests are transient and reconstructed from stable NPC identity,
faction, model, and route. They contain 1–3 canonical legal commodity types
and 2–12 total units. A successful demand removes a deterministic 25–75% of
the remaining manifest and asks `LootManager` to create ordinary quantity-
bearing `CargoPod` objects. Pickup therefore follows normal cargo capacity,
expiration, jettison, market, black-market, and Police-scan rules. Extortion
does not grant credits, create a piracy-only inventory, or bypass the global
32-object salvage limit.
If the live loot bound or all four nearby spawn positions reject a pod, that
stack stays in the manifest and the demand resolves without retrying or
creating a hidden payout.

Lawful reputation receives one small `PiracyDemand` penalty when the demand is
issued. Rogue reputation is not changed. Refusal preserves the manifest and
uses existing traffic fleeing state. Distress is routed through the existing
Police response service only when a nearby Police witness exists; any pursuit
uses the existing fugitive manager. No system-wide Police response is created
for an isolated encounter.

Active demands and consumed-target markers are runtime state. Save/load,
system transitions, and world resets cancel and clear them; physical pods keep
their existing loot lifecycle. Surrendered quantities are removed before the
target can be destroyed or despawned, preventing a second drop of the same
cargo. Ambient traffic does not yet maintain a permanent galaxy-wide cargo
history, and Phase 56 intentionally does not add stolen-goods provenance:
legal commodities remain legal, while canonical contraband (when supplied by
future eligible criminal traffic) remains contraband for the existing market,
black-market, and Police systems.
