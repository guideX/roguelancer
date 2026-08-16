# Phase 10 — Functional Bar Interior and Social NPC Foundation

Phase 10 adds a connected Bar to the existing developer station scene. The
room uses the existing station floor and collision world; entering it does not
create a second station session.

## Layout and interaction

- Bar bounds: `(-14.5, 0, 48.8)` to `(0, 4.25, 62.0)`.
- Approximate room size: 14 m wide, 13.2 m deep, 4.25 m high.
- Existing concourse placement and Bar sign/frame were retained.
- The entrance uses `StationDoorController` with `Closed`, `Opening`, `Open`,
  and `Closing` states. Phase 10 opens the door for the session; reset returns
  it to closed.
- The door panel animates upward and its player/camera collider clears only at
  the configured traversal threshold. The shared station camera collision
  query sees the same active collider state.
- Interaction remains nearest/facing/edge-triggered through
  `StationInteractionResolver`, so only one target can activate per key edge.

## Interior dressing

The compact room contains a long counter, rear shelving silhouette, register
terminal, glow strips, two table/bench groupings, a wall booth, and a visible
`BACK ROOM // OFFLINE` future-content hint. Major furniture and structural
pieces participate in player/camera collision; small decorative details do not.

The industrial concourse texture set is reused. The Bar uses the local
`Textures/Texturelabs_Metal_278S` texture with a darker purple/metal tint,
existing `glow_strip`, `door`, `structure`, `accent`, and floor textures. No
new assets were downloaded or added.

## Social NPCs and dialogue

The Bartender and three stationary temporary patrons all reuse the cached
`prototype-adam` CharacterAsset. The established GPU skinning, independent
animation clocks, deterministic idle offsets, centralized facing, and frustum
culling paths remain active. Gameplay identities are:

- Bartender — “Drinks are cheap. Trouble costs extra.”
- Rogue Pilot — “Heard the trade lanes are getting rough.”
- Dockhand — “You'd be surprised what comes through this bay.”
- Smuggler — “If you're looking for work, ask around.”

`StationDialogue` supplies bounded speaker/text/duration data. The only future
mission architecture is the Bartender's `HasFutureMissionHook` flag; no mission
system, shop, branching dialogue, or persistence was added.

## Validation

- `dotnet build Roguelancer.sln --no-restore -v:minimal`: passed, 0 errors.
- `--all-smoke`: 15 suites passed, 0 failed, including save, market, dock,
  ship, equipment, hardpoint, and Bar/social coverage.
- Bar/social smoke: 5 checks passed, 0 failed.
- GPU station run: 8 active characters, 8 visible, 0 culled, 172,032 bone
  upload bytes/frame, 0 dynamic vertex upload bytes/frame.
- Synthetic frustum sample: 8 active, 3 visible, 5 culled, 64,512 bone
  upload bytes/frame, 0 dynamic vertex upload bytes/frame.
- The runtime smoke/performance tooling was used in place of a manual remote
  keyboard walk-through. No runtime screenshot was captured in this pass.

