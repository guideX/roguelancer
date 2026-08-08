# Phase 1K.8 - First Equipment Upgrade and Mounted Weapon Integration Test

## 1. Date and checkout context

Test date: 2026-08-07.

Repository: `D:\dev\Roguelancer\Roguelancer`.

Checkout context: branch `main`, commit `c3e5c2c` (`git rev-parse --short HEAD`). The working tree already contained the uncommitted Phase 1K.6 and Phase 1K.7 reports before this phase began.

Phase 1K.7 was treated as accepted and complete. Its known targeting/GOTO and completion-feedback concerns were carried forward without redesign.

## 2. Git status and preservation notes

The Phase 1K.6 and Phase 1K.7 reports were present before the test and were preserved unchanged. No source, configuration, gameplay, HUD, save-schema, or content files were changed. This report is the only repository file created by Phase 1K.8. No commit was created.

Final status:

```text
## main...github/main
?? docs/phase_1k_8_first_equipment_upgrade.md
```

The pre-transaction live save was backed up outside the repository at:

```text
C:\Users\guideX\AppData\Local\Roguelancer\Saves\phase_1k_8_backups\player_save_before_1k_8_20260807_194411.json
```

Additional successful-state backups were created outside the repository:

```text
C:\Users\guideX\AppData\Local\Roguelancer\Saves\phase_1k_8_backups\player_save_pulse_mounted_before_active_load.json
C:\Users\guideX\AppData\Local\Roguelancer\Saves\phase_1k_8_backups\player_save_pulse_mounted_after_remount.json
```

The final live save was left in the successful upgraded state: 1,383 CR, one owned Liberty Pulse Cannon, and one Liberty Pulse Cannon mounted on `PrimaryGunLeft`.

## 3. Baseline test results

The baseline was run before the live transaction.

| Command | Exact result |
|---|---|
| `dotnet build Roguelancer.sln --no-restore` | Build succeeded, 56 warnings, 0 errors |
| `dotnet run --no-build --project Roguelancer.csproj -- --ship-smoke` | `SHIP SMOKE RESULT: 4 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --save-smoke` | `SAVE SMOKE RESULT: 4 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke` | `ALL SMOKE RESULT: 12 suites passed, 0 failed` |

The baseline build warnings were the established non-blocking warnings: duplicate using directives, nullable-context warnings, hidden-member warnings, unused fields/events, and the high-DPI warning. Runtime smoke output also repeated established optional content warnings: missing planet assets, fallback station/wreck models, sun-effect loading errors, malformed tradelane JSON for two legacy files, and invalid Warthog model data.

No dedicated equipment, loadout, mounted-gun, or player-weapon smoke switch exists. The existing `--ship-smoke` covers ship/economy purchase paths, while `--save-smoke` covers save/load equipment reconstruction. The all-smoke run includes missile smoke but no dedicated primary-gun integration assertion, so the live test supplied the missing integration evidence without creating a new broad smoke framework.

## 4. Starting credits and ship

The successful Phase 1K.7 state loaded normally in the live client.

| Field | Starting observation |
|---|---|
| Current system | New York System |
| Starting location | Free flight near Rochester Base, approximately 64.4 km before normal docking |
| Station used for transaction | Rochester Base |
| Credits | 7,883 CR |
| Ship | Scimitar |
| Cargo | 0/50 |
| Hull | 100% in the live flight HUD |
| Shields | 100% in the live flight HUD; ship card reports a 50-point shield capacity |
| Active missions | 0 |
| Completed missions | 3 |
| Neutral Civilians reputation | 0.36 |

The player docked at Rochester Base through the normal dock-assist flow and entered the equipment dealer using the station UI `[3] Equipment` control.

## 5. Starting owned and mounted loadout

The starting owned equipment was:

- Liberty Light Laser x1
- Rogue Blaster x1
- Basic Missile Launcher x1
- Civilian Shield Generator x1
- Light Thruster x1
- Basic Scanner x1
- Basic Countermeasure Dropper x1

The starting mounted equipment was:

- `PrimaryGunLeft`: Liberty Light Laser
- `PrimaryGunRight`: Rogue Blaster
- `MissileRack`: Basic Missile Launcher
- `CountermeasureRack`: Basic Countermeasure Dropper
- `ShieldGenerator`: Civilian Shield Generator
- `Thruster`: Light Thruster
- `Scanner`: Basic Scanner

The selected upgrade candidates Liberty Light Laser and Rogue Blaster were already owned and mounted. Liberty Pulse Cannon was the only preferred affordable gun that was not already owned.

## 6. Starting weapon state: mounted equipment versus fallback

The starting active weapon was genuinely mounted equipment, not a fallback weapon.

Live HUD before purchase:

```text
Weapon: Liberty Light Laser
Mapped: LaserBolt
ROF 0.18s | EN 8
```

Code inspection of the equipment definition gives the complete starting profile:

| Profile field | Liberty Light Laser |
|---|---:|
| Equipment ID | `liberty_light_laser` |
| Weapon type | `LaserBolt` |
| Damage | 16 |
| Projectile speed | 2,200 |
| Refire rate | 0.18 seconds |
| Energy cost | 8 |
| Range | 5,000 |

`ShipLoadout.GetPrimaryMountedGun()` resolves the first mounted gun in hardpoint order, so the Light Laser on `PrimaryGunLeft` was the active primary profile. The fallback path was not involved.

## 7. Equipment dealer usability observations

The normal Rochester Base equipment screen was clear enough to complete the transaction:

- The title explicitly identified `ROCHESTER BASE EQUIPMENT`.
- Each row displayed equipment name, type, price, owned count, and mounted count.
- Selecting a row displayed weapon type, damage, projectile speed, refire rate, energy cost, range, description, compatible hardpoints, mounted hardpoint, mountable count, and sellable count.
- The current loadout panel showed the contents of `PrimaryGunLeft` and `PrimaryGunRight`.
- The bottom help text identified `Up/Down` selection, `Enter` buy/mount, `U` unmount, and `S` sell spare.
- The selected row was visibly highlighted.
- The distinction between owned and mounted was understandable once a row was selected.

The equipment list extends below the fixed viewport. Lower rows are not all visible at once and require keyboard navigation. This is the established dealer-layout overflow issue; it did not block selection, purchase, mounting, or verification.

The normal station control, screen title, selected-row highlight, and detailed weapon statistics were sufficient to choose an informed upgrade. The HUD itself exposes only weapon name, mapped type, refire rate, and energy cost, so it does not show the full damage, speed, or range comparison during flight.

## 8. Chosen upgrade and reason

Chosen equipment: **Liberty Pulse Cannon**.

Reason: the Light Laser and Rogue Blaster were already owned and mounted. The Pulse Cannon was the remaining preferred affordable gun, produced a clear profile difference, and still left a positive balance without changing prices or granting credits.

Code and dealer definition:

| Field | Value |
|---|---|
| Equipment ID | `liberty_pulse_cannon` |
| Equipment type | Gun |
| Weapon type | `BlueDonut` |
| Price | 6,500 CR |
| Damage | 24 |
| Projectile speed | 1,500 |
| Refire rate | 0.28 seconds |
| Energy cost | 18 |
| Range | 4,500 |

Compared with the starting Light Laser, the Pulse Cannon has 50% more listed damage but a slower cadence, lower projectile speed, higher energy cost, and slightly shorter range. It is a meaningful change rather than a strictly faster or universally stronger weapon.

## 9-11. Purchase price, credits, ownership, and mounting

### Purchase

The Pulse Cannon was purchased through the normal dealer UI with `Enter`.

| Measure | Before | After purchase | Delta |
|---|---:|---:|---:|
| Credits | 7,883 CR | 1,383 CR | -6,500 CR |
| Liberty Pulse Cannon owned | 0 | 1 | +1 |
| Liberty Pulse Cannon mounted | 0 | 0 | 0 |

The dealer immediately showed `Owned: 1`, `Mounted: 0`, and `[ENTER] Mount`. It did not silently mount the newly purchased item. The displayed price and credit delta matched exactly, and no existing equipment was removed.

### Mount

The Light Laser was unmounted from `PrimaryGunLeft` and the Pulse Cannon was mounted there through the normal controls. The Rogue Blaster remained mounted on `PrimaryGunRight`.

| Measure | Result |
|---|---|
| Pulse Cannon owned | 1 |
| Pulse Cannon mounted | 1 |
| Pulse Cannon hardpoint | `PrimaryGunLeft` |
| Light Laser owned | 1 |
| Light Laser mounted | 0 |
| Rogue Blaster mounted | 1 |
| Credits during mount/unmount | Unchanged at 1,383 CR |

The selected Pulse row showed `Owned: 1`, `Mounted: 1`, `Mountable: 0`, `Sellable: 0`. The dealer loadout showed `PrimaryGunLeft: Liberty Pulse Cannon` and `PrimaryGunRight: Rogue Blaster`. No duplicate ownership or duplicate mounted entry was observed.

## 12-14. Mount result, weapon profile, and HUD integration

The mounted profile changed from the Light Laser to the Pulse Cannon:

| Profile field | Before: Liberty Light Laser | After: Liberty Pulse Cannon |
|---|---:|---:|
| Weapon type | `LaserBolt` | `BlueDonut` |
| Damage | 16 | 24 |
| Projectile speed | 2,200 | 1,500 |
| Refire rate | 0.18 seconds | 0.28 seconds |
| Energy cost | 8 | 18 |
| Range | 5,000 | 4,500 |

After mounting, the live HUD showed:

```text
Weapon: Liberty Pulse Cannon
Mapped: BlueDonut
ROF 0.28s | EN 18
```

Code inspection shows that `SyncMountedGunWeaponProfile()` resolves the mounted equipment and applies its damage, projectile speed, lifetime/range, refire, and energy values to the `WeaponSystem` override. It also changes the runtime weapon type to `BlueDonut`. The HUD and runtime profile therefore agree with the equipment definition; this was not merely a visual dealer-state change.

## 15-16. Live firing and combat result

After normal quickload and again after the remount launch, the Pulse Cannon remained active in the flight HUD. A normal primary-fire input was held in live free flight near Rochester Base.

Live observation:

- The HUD continued to identify `Liberty Pulse Cannon` and `Mapped: BlueDonut`.
- Energy fell from 100% to 94% during the sustained fire sample and recovered after the trigger was released.
- Hull and shields remained at 100%.
- No exception, asset failure, fallback weapon message, or loss of the mounted weapon occurred.
- No target was engaged, so projectile impact, hit feedback, and damage attribution were not exercised.

This directly proves that the mounted Pulse profile reached the live firing path and applied an energy cost. The live sample did not establish combat damage against an NPC.

No natural hostile encounter occurred during this phase. A bounty mission or developer-injected encounter was not created solely to force combat. Combat effectiveness, target-hit feedback, and the weapon's practical damage advantage are therefore **untested live**. The result is sufficient for a qualified equipment integration pass because live firing and the active `WeaponSystem` profile were directly observed.

The accepted Phase 1K.6 targeting and aiming-feedback concern was not changed. No new targeting redesign was required for this bounded weapon test.

## 17. Mounted-item selling safety

With the Pulse Cannon still selected and mounted, the normal `S` sell-spare control was attempted.

Live result:

- Credits remained 1,383 CR.
- Pulse ownership remained x1.
- Pulse mounted state remained x1 on `PrimaryGunLeft`.
- The dealer continued to show `Sellable: 0` and `[U] Unmount`.

Code inspection confirms that `TrySellUnequippedEquipment` rejects a mounted item and uses the rejection message `Liberty Pulse Cannon is mounted or unavailable to sell.` The visible row state and unchanged save state prove that the mounted item could not be sold into an invalid loadout.

## 18-20. Active equipment save/load, ownership duplication, and mounted-state duplication

The active checkpoint was taken after purchase and mounting while docked at Rochester Base. The save was backed up before the reload.

Before quickload, the save payload contained:

- `player_credits: 1383`.
- One owned `liberty_pulse_cannon` entry with quantity 1.
- One mounted `liberty_pulse_cannon` entry on `PrimaryGunLeft`.
- The existing Light Laser still owned but not mounted.
- The existing Rogue Blaster still owned and mounted on `PrimaryGunRight`.
- Zero active missions and three completed missions.

The temporary visible change was an unmount of the Pulse Cannon. A normal quickload then restored the saved state into free flight near Rochester Base.

After load:

- Credits remained 1,383 CR.
- Exactly one Pulse Cannon remained owned.
- Exactly one Pulse Cannon remained mounted.
- The mounted hardpoint was still `PrimaryGunLeft`.
- The HUD again identified `Liberty Pulse Cannon / BlueDonut / ROF 0.28s / EN 18`.
- The Pulse Cannon remained friendly to the player by being player equipment; no mission or NPC state was involved.
- No duplicate inventory item, duplicate mounted entry, fallback weapon, or credit mutation occurred.
- Firing remained available after load.

The save payload persists equipment IDs, types, quantities, and mounted hardpoint IDs. The load path reconstructs the logical loadout from those saved IDs; exact in-memory object identity across a full reload is not claimed. This is a valid logical reconstruction with no duplication or exploitation.

## 21. Unmount/remount result

After preserving the successful mounted save, the player docked normally at Rochester Base, opened the equipment dealer, and selected the Pulse Cannon.

Unmount observation:

- Pulse Cannon changed to `Owned: 1`, `Mounted: 0`.
- `PrimaryGunLeft` became empty.
- The row showed `Mountable: 1`, `Sellable: 1`.
- Credits remained 1,383 CR.

Remount observation:

- Pressing `Enter` changed the row back to `Owned: 1`, `Mounted: 1`.
- `PrimaryGunLeft` again contained Liberty Pulse Cannon.
- `Mountable` returned to 0 and `Sellable` returned to 0.
- Credits remained 1,383 CR.
- The subsequent normal launch HUD again reported the Pulse Cannon profile.

Unmounting did not delete ownership, and remounting did not create an additional copy.

## 22. Optional sell-cycle result

An unequipped sell cycle was not attempted. The only newly purchased gun was the successful upgrade, and selling it would have required repurchasing it with the remaining 1,383 CR, which would have sacrificed the final upgraded state. The required mounted-item safety check was performed and passed. Unequipped-sale code behavior was inspected but not live-tested in this phase.

## 23. Blocking bugs

None found.

The normal equipment loop completed: dock, open dealer, purchase, mount, launch, fire, save/load, unmount, remount, and relaunch. Credits, ownership, mounting, HUD state, and runtime weapon profile remained coherent.

## 24-28. UX, weapon feedback, economy, missing feedback, and nice-to-have improvements

### Confusing UX

- The dealer list extends beyond the fixed viewport, so keyboard navigation is required to reach lower equipment rows.
- The first purchase did not auto-mount, which is safe and correct but requires the player to understand that a second `Enter` action is needed.
- The normal dealer controls are shown in a single footer and are understandable after reading it; the screen does not provide a separate purchase/mount confirmation panel.

### Weapon-feedback concerns

- The flight HUD identifies weapon name, mapped type, refire rate, and energy cost.
- It does not display damage, projectile speed, or range, so the full upgrade tradeoff must be learned from the dealer screen.
- No NPC hit occurred, so live hit flash, impact sound, and damage feedback for the Pulse Cannon remain untested.
- The accepted general aiming/acquisition concern remains non-blocking and was not modified.

### Economy and balance observations

- The transaction reduced credits from 7,883 CR to 1,383 CR, an exact 6,500 CR deduction.
- The purchase consumed approximately 82.5% of the available balance but was the only preferred non-owned affordable gun after inspecting the actual loadout.
- The remaining balance naturally failed the 2,600 CR Basic Mine Dropper purchase attempt without changing credits, inventory, or mounting.
- The Pulse Cannon's higher damage is balanced by slower refire, lower projectile speed, higher energy cost, and shorter range. The live test confirmed the energy cost path but did not assess damage effectiveness against an NPC.

### Missing feedback

- A dedicated, persistent purchase or mount confirmation would make the state transition more obvious; the dealer row state was the reliable confirmation during this test.
- The HUD does not show the full weapon statistics that appear in the dealer.
- There is no explicit live message for weapon damage or projectile behavior when firing into empty space.

### Nice-to-have improvements

- Add dealer scrolling or paging for the full equipment catalog.
- Add a concise purchase/mount/unmount toast with the resulting hardpoint and active weapon.
- Add optional damage, speed, and range information to the flight HUD or a loadout panel.
- Add a later focused live combat run for upgraded-gun hit feedback and damage comparison.

No improvement was implemented in this phase.

## 29. Files changed and why

Repository file created by this phase:

- `docs/phase_1k_8_first_equipment_upgrade.md` - required live validation report.

No gameplay code changed. No equipment, weapon, save, dealer, HUD, station, navigation, AI, or content file changed. Save backups were stored outside the repository.

## 30. Final regression results

The required post-run regression gate passed after the live test.

| Command | Exact result |
|---|---|
| `dotnet build Roguelancer.sln --no-restore` | `Build succeeded. 0 Warning(s), 0 Error(s)` |
| `dotnet run --no-build --project Roguelancer.csproj -- --ship-smoke` | `SHIP SMOKE RESULT: 4 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --save-smoke` | `SAVE SMOKE RESULT: 4 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke` | `ALL SMOKE RESULT: 12 suites passed, 0 failed` |

The final all-smoke component counts were:

- Save: 4/4
- Market: 7/7
- Missile: 4/4
- Countermeasure: 4/4
- Mine: 5/5
- Contraband: 6/6
- Traffic: 9/9
- Loot: 8/8
- Mission: 14/14
- Navigation: 7/7
- Dock: 9/9
- Ship: 4/4

No live hostile combat occurred, no combat code changed, and no missile path was exercised by the live client. Missile smoke still passed 4/4 inside all-smoke.

Final status showed the preserved Phase 1K.6 and Phase 1K.7 reports unchanged plus this new untracked report. No commit was created.

## 31. Recommended next parity phase

Phase 1K.9 should perform the first focused live combat and damage-feedback test using the mounted Liberty Pulse Cannon, preferably through a practical normal bounty opportunity. It should record target selection, projectile visibility, hit feedback, NPC damage, energy cadence, and survivability while continuing to treat the existing targeting/aiming concern as bounded UX work. No equipment, weapon, combat, or HUD redesign is warranted unless a reproducible live blocker appears.

## Final summary

- Station: Rochester Base, New York System.
- Starting credits: 7,883 CR.
- Purchased weapon: Liberty Pulse Cannon (`liberty_pulse_cannon`).
- Price: 6,500 CR.
- Ending credits: 1,383 CR; exact delta -6,500 CR.
- Starting weapon state: Liberty Light Laser was genuinely mounted on `PrimaryGunLeft`, not a fallback; Rogue Blaster was mounted on `PrimaryGunRight`.
- Mounted weapon result: Pulse Cannon mounted normally on `PrimaryGunLeft`; Light Laser remained owned but unmounted; HUD and `WeaponSystem` resolved the Pulse `BlueDonut` profile.
- Live firing/combat result: live firing passed with energy falling 100% to 94%; no natural hostile combat or NPC hit was observed, so combat effectiveness is untested.
- Save/load result: active equipment save/load passed with credits, ownership, mounted hardpoint, HUD profile, and firing preserved.
- Duplicate-item result: no duplicate owned item or mounted item; mounted sale was rejected safely; unmount/remount preserved one owned copy.
- Equipment progression verdict: qualified pass. The normal purchase-to-mounted-gameplay loop produced a real, persistent weapon integration result with no blocking defect.
- Smoke-test counts: ship 4/4, save 4/4, all-smoke 12/12 suites; all-smoke missile 4/4.
- Gameplay code changed: no.
- Report: `docs/phase_1k_8_first_equipment_upgrade.md`.
- Commit created: no.
