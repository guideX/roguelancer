# Controls

## Navigation

- `F1`: cycle hostile targets
- `F2`: cycle stations in distance order
- `Shift+F2`: cycle stations in reverse distance order
- `Ctrl+F2`: target the nearest dockable station
- `G`: start normal GOTO on the selected target
- On a fresh undocked start with no active mission, the HUD briefly shows:
  - `Press Ctrl+F2 to target nearest station`
  - `Press F3 for dock assist`

## Docking

- `F3`: dock or start dock assist
- If a dockable station is selected and the ship is already in range, `F3` docks immediately.
- If a dockable station is selected but the ship is out of range, `F3` starts a direct dock-assist approach to that station.
- If no station is selected, `F3` resolves and targets the nearest dockable station first, then starts dock assist.
- Dock assist HUD text now calls out the action more clearly:
  - `Press F3: Approach/Dock [Station Name]`
  - `Distance to dock range: X`
  - `Dock Assist: Approaching [Station Name]`
  - `Dock range in X`
  - `Press F3 to dock` when you are in range

## Targeting Notes

- The selected target box shows the current selection.
- The GOTO status panel shows the current route destination separately so dock assist and normal navigation are easier to tell apart.
