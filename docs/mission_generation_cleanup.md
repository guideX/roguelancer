# Mission Generation Cleanup

Phase 1H.1 tightened the mission-world binding layer so generated jobs stay grounded in the loaded world.

## Generation rules

- Delivery missions now pick destinations from the active station list provided by the world manager.
- Bounty missions now default to clearly hostile or criminal targets, with spawned targets bound to the mission at acceptance time.
- Escort missions are now world-bound contracts and their dock UI copy now shows the escort target, destination, risk, reward, and client/faction clearly.

## Player-facing text

- Mission summaries and detailed descriptions now spell out objective, destination or target, reward, risk, and client/faction.
- Active mission HUD text uses safe fallback wording when a target or destination has not been resolved yet.
- Job board rows now surface type, objective, payout, risk, and client/faction together.

## Legacy safety

- Saved missions continue to load without schema changes.
- Legacy delivery aliases still resolve through the world manager so older saves remain playable.
