# Phase 53 — Police fines, confiscation, and surrender

Police cargo scans remain completion-time, read-only inspections until a
contraband finding is made. A finding opens one transient, world-level demand
owned by `PoliceScanSystem`; the smuggling mission is only an observer of the
result.

## Policy

- Response window: 8 seconds.
- Enforcement escape radius: 4,200 m from the scanning officer.
- Fine: `ceil(500 CR + 25% of canonical commodity base value)`, clamped to
  500–10,000 CR.
- Compliance requires the full fine. Insufficient credits leave the demand
  unresolved; the player must refuse, flee, or time out. Credits never go
  negative.
- Compliance removes all illegal commodity units still aboard, including
  mission-attributed units, while preserving legal cargo, equipment, and
  consumables that are not canonical contraband commodities.
- The fine and the finding are snapshot-based. Jettisoning after detection can
  reduce what is confiscated but does not clear or reduce the fine.
- Compliance applies one bounded Police standing consequence (`-0.03`) and
  does not start temporary hostility. Refusal retains the established bounded
  Police standing consequence (`-0.20`) and starts temporary hostility;
  timeout, flight, trade-lane entry, docking bypass, or hostile scanner
  destruction use that same refusal/combat path without a parallel wanted
  system.

`FactionBribeService` remains separate. A fine is an immediate Police cargo
enforcement transaction; a bribe is a voluntary standing-repair transaction.

Unresolved demand state is transient and is cleared by reset, load/rebind,
docking transition, lane transit, and scanner teardown. Completed seizures are
durable through the normal cargo and mission save data because the cargo hold
mutation updates mission reservations before the mission is failed.
