# Phase 66 — Ambient Rogue Raids

Ambient Rogue raids are owned by `AmbientPirateRaidManager`, which evaluates
only active, legal Phase 59 shipment manifests on a bounded cadence. A
shipment can receive one deterministic raid lifecycle, with at most two active
groups per system and one to three canonical Liberty Rogue raiders. The
opportunity score combines remaining manifest value, quantity, route risk,
destination shortage, current Rogue population, and Phase 65 escort
deterrence. A qualifying shipment waits a stable 10–30 seconds before spawn.

Raider hulls, weapons, movement, security response, distress, and cleanup stay
inside the existing NPC systems. The raid manager only owns assignment and
encounter state; it does not create missions, hidden rewards, or a second
combat AI. It uses a raid-scoped faction-target origin so a marked Rogue can
attack its assigned ordinary trader without changing the global Rogue/neutral
faction relationship matrix.

Under sustained pressure, the trader may surrender once. The shared Phase 56
25–75% deterministic quantity policy is applied to the live economic manifest
only after `LootManager` creates a real stolen `CargoPod`. The pod is available
to the player and can be collected by an assigned raider only at the normal
pickup radius. Raider hauls are limited to three commodity stacks; a destroyed
raider drops its remaining carried quantity as another stolen physical pod.
Recovered cargo is a temporary Rogue sink in this phase: there is no Rogue
delivery economy yet. The merchant keeps only the manifest remainder, so later
delivery, shortages, adaptive routing, and route risk see the real mutation.

Active and delayed raid state stores stable shipment/raider identities,
progress, surrender state, positions, and bounded hauls in the existing schema
12 save format. Load rebinds the existing economic shipment and reconstructs
only the saved surviving raiders; it never reruns qualification or duplicates
the group. System teardown/reset clears raid state without recording a risk
incident.
