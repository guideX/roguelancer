# Phase 57 — Stolen Cargo Provenance

Phase 57 keeps commodity legality and ownership provenance separate.

## Runtime model

`CargoHold` retains the existing total quantity keyed by commodity name and
adds one bounded stolen-quantity aggregate per commodity. Mission reservations
remain separate and carry the same `CargoProvenance` value, so clean and stolen
copies of one commodity can coexist without per-unit objects.

`CargoPod` carries the same provenance. Phase 56 extortion pods are marked
stolen at spawn, and commodity salvage from a player-destroyed Liberty
Corporations trader is also marked stolen. Other equipment, ordinary loot,
player-jettisoned clean goods, purchases, and mission-granted smuggling cargo
remain clean.

## Commerce and law

Lawful markets sell only clean, unreserved quantities. Black markets keep their
canonical contraband listings and dynamically expose only currently owned,
unreserved stolen legal commodities. Those legal fence entries are sink-only;
they use 60% of the local clean-market sell quote and do not become black-market
stock. Contraband sales retain the existing black-market price and do not stack
an additional stolen premium.

Police findings are commodity-level and classify each finding as contraband,
stolen, or both. A clean legal quantity is preserved when stolen copies are
confiscated. The Phase 53 fine remains `ceil(500 + 25% of detected violation
value)`, clamped to 500–10,000 CR, with quantities that are both contraband and
stolen counted once. Refusal continues through the existing fugitive path.

## Persistence

Owned cargo, mission reservation provenance, and durable commodity pod
provenance have explicit `stolen` save fields. The generic physical pod snapshot
also preserves non-mission extortion pods. Jettison/re-pickup, save/load, and
ship cargo transfer do not launder cargo. Fencing, Police confiscation, and
accepted mission delivery remain the legitimate sinks that remove player-owned
stolen state.
