# Phase 54 — Fugitive heat, Police pursuit, and escape resolution

Phase 54 adds one bounded current-system fugitive incident owned by
`PoliceFugitiveManager`. It is a state machine around the existing traffic,
targeting, distress, escalation, disengagement, communication, reputation,
and temporary-hostility services; it is not a second Police combat AI.

## Policy

- Heat 1 (`Pursuit`) starts from a Phase 53 enforcement refusal, timeout,
  enforcement-radius flight, trade-lane/docking bypass, or qualifying scanner
  attack.
- Heat 2 (`HotPursuit`) starts only when authoritative player damage is
  recorded against Liberty Police during an active incident. NPC-only damage
  does not escalate it. Destroying Police cannot exceed Heat 2.
- Police acquisition/reacquisition is bounded to 10,000 world units at Heat 1
  and 14,000 at Heat 2. Police outside that radius do not receive an
  omniscient player target, and no dedicated pursuit ship is spawned.
- Contact requires a live Liberty Police NPC with a valid player target that
  remains within the current bounded radius. Multiple officers share the one
  incident and one escape timer.
- Contact loss has a 0.25 second grace period. Heat 1 requires 15 continuous
  seconds without contact; Heat 2 requires 25. Reacquisition returns to
  pursuit and resets progress to zero.
- A 180 second incident bound is a final safety valve only after contact is
  gone. Active Police contact does not silently clear pursuit.

## Integration

`TrafficManager` calls the fugitive manager after ordinary patrol objectives
and before shared faction-combat acquisition. It sets the existing
`NpcPlayerTargetReason.FugitivePursuit`; ordinary NPC movement, weapons,
disengagement, distress, escalation, and communication remain authoritative.
The manager does not maintain one wanted record per NPC and does not spawn
reinforcements. The existing population cap and bounded escalation policy stay
in force.

The manager owns the pursuit-specific temporary hostility entry using
`TemporaryHostilityManager`. Escape/reset clears that entry through the
existing hostility lifecycle, without restoring permanent standing or making
an independently hostile Police faction friendly. Fugitive entries are omitted
from save capture and ignored on save apply, so no NPC references or hidden
escape countdowns persist.

Police-controlled lawful stations reject docking while the incident is active.
Rogue/non-lawful destinations continue through normal faction access rules;
lane departure or an allowed destination does not itself erase the incident.
Trade-lane transit has no specialized Police chase logic: if the normal lane
rules separate the player from Police, the ordinary contact-loss timer starts.
System changes, load/rebind, reset/world clear, and player death/teardown clear
the local transient incident. Permanent reputation and combat consequences
remain governed by their existing services.

## Player-facing presentation

- `LIBERTY POLICE PURSUIT`
- `LIBERTY POLICE — HOT PURSUIT`
- `POLICE CONTACT LOST | Stay clear: Ns`
- `POLICE REACQUIRED`
- `PURSUIT EVADED`

Messages use the existing notification/HUD paths and are presentation events,
not per-frame comms spam.

## Validation

- `dotnet build Roguelancer.csproj --no-restore`: passed.
- `dotnet run --no-build --project Roguelancer.csproj -- --police-fugitive-smoke`:
  65 passed, 0 failed, including deterministic refuse-and-escape,
  hot-pursuit, and smuggler-escape proofs.
- `--all-smoke`: 54 suites passed, 0 failed.
- Phase 53 enforcement: 30 passed, 0 failed.
- Phase 52 contraband/smuggling: 23 passed, 0 failed.
- Phase 47–51 lane hardening, disruption, defense, escort, and raid suites:
  all passed in the aggregate run.
