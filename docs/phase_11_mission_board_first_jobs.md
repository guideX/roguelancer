# Phase 11 — Mission Board, Mission State Foundation & First Real Jobs

Date: 2026-08-16
Checkout: D:\dev\Roguelancer\Roguelancer
Baseline: Phase 10 commit 2d909d45a32d11eb58f3fe28af4fb6fdd965589e

## Scope

The Bar now contains a physical procedural mission terminal beside the
entrance wall. It uses the existing on-foot interaction resolver and opens an
overlay while the station scene remains loaded. Input is edge-triggered and
the overlay consumes movement input until it closes.

Phase 11 uses a fixed two-job catalog:

- **Patrol Sweep** — reach the patrol marker outside the originating station;
  reward 1,500 credits.
- **Rogue Hunt** — destroy three mission-designated rogue ships; reward 4,000
  credits.

The authoritative state machine is Available -> Accepted -> InProgress ->
Completed -> Rewarded, with Failed as the bounded invalid-target path. Only
one mission may be active. Objective completion never pays immediately: the
player must return to the recorded origin station and claim the reward at the
board. Reward state is marked before the credit mutation and is removed from
the unclaimed queue after a successful transaction.

## Existing systems reused

The implementation extends the existing MissionManager, mission waypoint HUD,
station/session interaction layer, player credits, faction reputation, save
schema, station docking flow, NPC destruction callbacks, projectile damage
paths, and existing station dealer overlay pattern. Player attribution is
marked by the real gun, missile, mine, and lightning damage paths. Mission
rogue ships use the existing NpcShip and traffic behavior infrastructure.

## Persistence

Save schema version 2 stores definition ID, lifecycle state, progress, required
count, objective metadata, target position, origin station identity,
acceptance timestamp, and reward-paid state. Active and completed-unclaimed
missions are restored through the existing player save file; no separate
mission save file was added.

## Validation

- Build: 0 errors; existing repository warnings remain.
- Mission smoke: 7 passed, 0 failed.
- Save smoke: 4 passed, 0 failed.
- All smoke: 15 suites passed, 0 failed.
- git diff --check: passed.
- Performance diagnostics include mission manager/world/waypoint sections.
  A mission-active flight run measured each section at approximately
  0.006 ms, 0.006 ms, and 0.004 ms average respectively.

Manual traversal and screenshot capture were not available in the remote
session. The automated proof covers board metadata, acceptance, objective
simulation, real destruction callbacks, credit transactions, save/load
round-trips, and regression suites.
