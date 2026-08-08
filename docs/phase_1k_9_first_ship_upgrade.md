# Phase 1K.9 — First Earned Ship Upgrade and Persistence Test

## 1. Date and checkout context

Date: 2026-08-08. Checkout: `D:\dev\Roguelancer\Roguelancer`, branch `main`, baseline commit unchanged. Phase 1K.8 was treated as accepted and complete. The intended Phase 1K.8 live save was loaded before progression.

Evidence labels used below: **Live observation**, **Automated proof**, **Code inspection**, **Inference**, and **Untested**.

## 2. Git status and preservation notes

**Live observation / repository check:** The repository was clean before work: `## main...github/main`. Phase 1K.5 through Phase 1K.8 reports and all existing user work were preserved. No gameplay code, configuration, save architecture, economy, or UI code changed. A separate TortoiseGit window briefly surfaced an unrelated push dialog; it was aborted before completion. No commit or push was made.

Save backups were written outside the repository under `C:\Users\guideX\AppData\Local\Roguelancer\Saves\phase_1k_9_backups`, including:

- `player_save_before_1k_9_20260808_063753.json`
- `player_save_before_pirate_purchase_20260808_0829.json`
- `player_save_after_pirate_purchase_station.json`
- `player_save_after_purchase_flying.json`
- `player_save_final_pirate_flying.json`

## 3. Baseline test results

**Automated proof:** Baseline commands were run before the live progression.

| Command | Result |
| --- | --- |
| `dotnet build Roguelancer.sln --no-restore` | Succeeded, 56 warnings, 0 errors |
| `--ship-smoke` | 4 passed, 0 failed |
| `--save-smoke` | 4 passed, 0 failed |
| `--mission-smoke` | 14 passed, 0 failed |
| `--market-smoke` | 7 passed, 0 failed |
| `--all-smoke` | 12 suites passed, 0 failed |

The 56 build warnings were existing warning classes: duplicate `using` directives, nullable-annotation context warnings, hidden members, unused fields/events/variables, and WFO0003 high-DPI configuration. Runtime smoke output also retained the known missing planet/station assets, missing SunEffect, malformed legacy tradelane JSON, and invalid Warthog model-index messages.

There is no separate equipment/loadout smoke command in this checkout. Equipment/loadout coverage was performed live and through ship/save smoke coverage.

## 4. Starting player state

**Live observation:** The Phase 1K.8 save loaded as the expected Scimitar state at Rochester Base.

- Credits: 1,383 CR.
- Ship: Scimitar.
- Cargo: empty, 0/50 used.
- Hull, shields, and energy: full; Scimitar maxima are 100 hull, 50 shields, and 200 energy.
- Owned equipment: Liberty Light Laser x1, Rogue Blaster x1, Basic Missile Launcher x1, Civilian Shield Generator x1, Light Thruster x1, Basic Scanner x1, Basic Countermeasure Dropper x1, Liberty Pulse Cannon x1.
- Mounted equipment: Pulse Cannon on `PrimaryGunLeft`; Rogue Blaster on `PrimaryGunRight`; missile launcher, countermeasure dropper, shield generator, thruster, and scanner on their expected hardpoints.
- Pulse Cannon: exactly one owned logical copy, mounted normally, active HUD profile.
- Relevant reputation: Neutral Civilians approximately +0.36 at the starting checkpoint.
- Active missions: 0.
- Completed missions: 3.

## 5. Starting credits

**Live observation:** 1,383 CR.

## 6. Required upgrade amount

**Code inspection and live UI:** Current definitions and the ship dealer agreed on Scimitar price 12,000 CR, Scimitar trade-in value 6,000 CR, Pirate Transport price 24,000 CR, and net upgrade cost 18,000 CR. The required additional credits from the starting state were therefore 16,617 CR.

## 7. Progression activity ledger

All entries were performed through normal gameplay. No save editing, credit injection, altered reward, altered price, developer purchase method, duplicate transaction, or repeated save/load reward attempt was used.

| Activity | Origin | Destination/target | Type / difficulty / faction | Reward or profit | Credits after | Navigation / combat |
| --- | --- | --- | --- | ---: | ---: | --- |
| Engine Components x2 delivery | Rochester Base | Fort Bush | Delivery / moderate / neutral-civilian board | +2,383 | 3,766 | GOTO and dock assist clear; no combat; observed neutral-civilian reputation increase of +0.12 |
| Food Rations x20 trade | Fort Bush | Newark Station | Legal commodity trade | +600 profit; buy 85 CR, sell 115 CR | 4,366 | Normal market buy/sell; no combat |
| Refugee Transport escort | Newark Station | Battleship Missouri | Escort / moderate / neutral-civilian board | 0; 2,449 listed reward | 4,366 | Route/objective reacquisition was unreliable; mission failed; no combat |
| Luxury Goods x1 delivery | Battleship Missouri | Trenton Outpost | Delivery / moderate / board mission | +2,470 | 6,836 | Navigation completed normally; no combat |
| Outcast Smuggler bounty | Trenton Outpost | Natural hostile target | Bounty / moderate / Liberty Rogues target | 0; 2,448 listed reward | 6,836 | Natural hostile encounter; pursuit/combat was inconclusive and mission failed |
| H-Fuel Cells x1 delivery | Trenton Outpost | Battleship Missouri | Delivery / low / board mission | +1,452 | 8,288 | Navigation completed normally; no combat |
| Engine Components delivery | Battleship Missouri | West Point | Delivery / moderate / board mission | +2,404 | 10,692 | Navigation completed normally; no combat |
| Retained Luxury Goods x1 sale | Battleship Missouri | Local market | Legitimately retained cargo liquidation after failed mission | +1,200 | 11,892 | Normal sale; cargo was not spawned or duplicated |
| Construction Materials x1 delivery | Battleship Missouri | Newark Station | Delivery / moderate / board mission | +2,322 | 14,214 | Navigation completed normally; no combat |
| Luxury Goods x1 delivery | Newark Station | Detroit Munitions | Delivery / moderate / board mission | 0; 2,796 listed reward | 14,214 | Mission timed out; cargo remained legitimately aboard |
| Construction Materials x3-volume delivery | Detroit Munitions | West Point | Delivery / moderate / board mission | 0; 2,952 listed reward | 14,214 | Mission target was lost during jump-hole/docking approach; mission failed |
| Military Hardware delivery | Ouray Base | Pueblo Station | Delivery / high risk / board mission | +3,213 | 17,427 | Short local Colorado route; completed normally; no combat |
| Retained Luxury Goods x1 sale | Pueblo Station | Fallback market | Legitimately retained cargo liquidation | +1,200 | 18,627 | Normal sale; no exploit |

The final cargo was one legitimate Construction Materials unit with volume 3. The two Luxury Goods sales were only liquidation of cargo retained after failed deliveries.

## 8. Total credits earned

**Live observation / arithmetic:** Net credits earned were 17,244 CR: 14,244 CR from six completed mission rewards, 600 CR legal trade profit, and 2,400 CR from two legitimate retained-cargo sales. Final pre-purchase credits were 18,627 CR.

## 9. Total progression duration

**Live observation:** Approximately 1 hour 35–40 minutes of wall-clock time from the resumed 1K.8 client session at about 07:13 to the purchase at about 08:48–08:50. Active piloting was intermittent because normal GOTO, tradelane, and docking waits dominated several legs.

## 10. Mission/trade variety

**Live observation:** The run used deliveries, one legal food trade, an escort attempt, a bounty attempt, normal station markets, jump-hole travel, and visits to Rochester, Fort Bush, Newark, Battleship Missouri, Trenton, West Point, Detroit, Ouray, Pueblo, and Cheyenne. It included both successful and failed missions, a natural hostile encounter, and varied cargo. No activity was repeated through a reward or transaction exploit.

## 11. Progression-feel verdict

**Inference from live play:** Qualified pass. The 18,000 CR upgrade was achievable through ordinary play and the rewards were meaningful, but travel time and autopilot/objective failures dominated the experience. Repeating delivery work became somewhat monotonous, and the 6,500 CR Pulse Cannon purchase materially delayed the ship goal from 7,883 CR in Phase 1K.8 to 1,383 CR here. The goal still produced a clear sense of progress and did not appear practically impossible. No rebalance was made.

## 12. Pulse Cannon live-combat result

**Live observation:** A natural Trenton Outcast Smuggler encounter exercised the mounted weapon opportunity, but combat effectiveness was inconclusive: the target did not show readable damage, the player shields fell from 100% to 50%, and the player engines were disabled before the mission failed. Hit readability, effective damage, lead difficulty, and comparison against the Light Laser could not be reliably scored from that fight.

After the ship purchase, controlled non-combat firing verified the weapon path without manufacturing an encounter: the HUD identified `Liberty Pulse Cannon`, mapped `BlueDonut`, and showed `ROF 0.28s | EN 18`; a visible blue ring projectile appeared and energy changed from 100% to 94%, then regenerated to 96%. This confirms post-purchase profile, muzzle/projectile path, and energy consumption. Hostile combat effectiveness remains untested/inconclusive.

## 13. Pre-purchase ship/loadout/cargo snapshot

**Live observation:** At Pueblo immediately before purchase:

- Credits: 18,627 CR.
- Current ship: Scimitar.
- Scimitar trade-in: 6,000 CR.
- Pirate Transport displayed price: 24,000 CR.
- Displayed net cost: 18,000 CR.
- Cargo: Construction Materials x1, volume 3; 3/50 used.
- Owned equipment: the same eight logical equipment entries from Phase 1K.8, each quantity 1.
- Mounted equipment: Pulse Cannon left, Rogue Blaster right, missile launcher, countermeasure dropper, civilian shield generator, light thruster, and scanner.
- Reputation: Liberty Police +0.59400004, Liberty Navy +0.846, Liberty Rogues -0.7700001, Liberty Corporations +0.54, Bounty Hunters +0.5220001, Junkers +0.036, Neutral Civilians +0.738.
- Active missions: 0; completed missions: 14.
- Station: Pueblo Station.
- Hull was visibly reduced after the earlier bounty attempt; shields and energy were full at the purchase checkpoint. The ship purchase reset the new ship to its definition maxima as designed.

## 14. Ship dealer usability

**Live observation:** The ship dealer was reachable from the normal station UI and was easy to identify. It displayed the current ship, Pirate Transport, price, trade-in, net cost, and comparison rows. The current ship was explicit, and cargo capacity was visible. A replacement warning was implicit in the purchase flow but not especially prominent. Acceleration, turn rate, cruise speed, and afterburner speed were not shown in the comparison panel.

## 15. Pirate Transport displayed price

**Live observation:** 24,000 CR.

## 16. Scimitar trade-in value

**Live observation:** 6,000 CR for the pre-purchase Scimitar.

## 17. Expected versus actual purchase cost

**Live observation / arithmetic:** Expected deduction was `24,000 - 6,000 = 18,000 CR`. Actual deduction was exactly 18,000 CR.

## 18. Credits before/after purchase

**Live observation:** 18,627 CR immediately before purchase; 627 CR immediately after. Credits decreased exactly once.

## 19. Cargo transfer result

**Live observation:** Construction Materials x1 remained aboard, with no loss or duplication. Used capacity stayed 3 while capacity changed from 50 to 200; the HUD changed from 3/50 to 3/200.

## 20. Equipment inventory transfer result

**Live observation:** All eight owned equipment entries survived with quantity 1. Liberty Pulse Cannon remained owned exactly once, Liberty Light Laser remained owned, and no other equipment vanished or duplicated.

## 21. Mounted equipment transfer result

**Live observation:** All seven mounted hardpoints remained valid. Pulse Cannon stayed on `PrimaryGunLeft`, Rogue Blaster stayed on `PrimaryGunRight`, and all non-gun equipment remained mounted. The HUD and actual WeaponSystem profile continued to identify the Pulse Cannon after the ship swap and after save/load.

## 22. Pirate Transport runtime stats

| Stat | Scimitar definition / dealer | Pirate Transport definition / live result |
| --- | ---: | ---: |
| Max speed | 250 | 180, live HUD reached 180 |
| Reverse speed | 150 | 100, live HUD reached 100 |
| Cruise speed | 600 | 500, live HUD reached 500 |
| Afterburner speed | 500 | 350, live HUD reached 350 |
| Acceleration | 150 | 100, code-applied runtime value |
| Turn speed | 1.5 | 0.8, code-applied runtime value |
| Hull | 100 | 250, live hangar/runtime display |
| Energy | 200 | 300, code-applied runtime maximum; live bar restored full |
| Shields | 50 | 100, live hangar/runtime display |
| Cargo | 50 | 200, live HUD and hangar display |

**Code inspection:** `ShipDefinition.ApplyToShip` applies the selected definition to the runtime ship. **Live observation:** The HUD and hangar confirmed the speed, hull, shields, and cargo values after purchase and after load. The numeric energy maximum and acceleration/turn values are not separately exposed in the UI, so those specific numbers are code-backed runtime evidence rather than direct HUD readings.

## 23. Pirate Transport rendering/orientation

**Live observation:** The `SHIPS/PI_TRANSPORT/PI_TRANSPORT` model loaded and remained visible after launch and quickload. Its large transport silhouette, twin engine effects, scale, and camera framing were usable. It was not upside down or backward, no Scimitar remained attached, and projectiles originated from a sensible forward position. The source definition’s model-correction rotation was active. Some fallback station/asteroid scenes were very bright or visually sparse, but the ship itself was not a missing-model fallback.

## 24. Flight-feel comparison

**Live observation / inference:** The Pirate Transport was immediately perceptibly slower, heavier, and less agile than the Scimitar while remaining controllable. Forward acceleration reached 180, reverse reached 100, cruise reached 500, and afterburner reached 350. Turning responded but felt slower than the Scimitar’s 1.5 turn value. The lower speed and handling felt intentional rather than broken, and the larger ship remained usable for normal flight.

## 25. Docking/GOTO result

**Live observation:** GOTO engaged with `AUTOPILOT [DockingApproach]`, and dock assist engaged with Cheyenne Asteroid Field. The transport eventually converged from approximately 17 km to 1 km and then docked normally. After flying-save quickload, the Pirate Transport launched, flew, and docked again; the hangar displayed `Your Ship: Pirate Transport` with 180 speed, 250 hull, 100 shields, and 200 cargo.

**Confusing behavior:** GOTO/dock assist could approach very slowly when the ship began poorly aligned; manual cruise at 500 was needed for a practical long approach. This is a handling/UX concern, not a ship-change blocker.

## 26. Post-purchase weapon result

**Live observation:** Pulse Cannon remained mounted on `PrimaryGunLeft`, showed the correct HUD profile, produced a visible blue projectile, consumed energy, and did not duplicate the mount. The post-purchase controlled firing check passed. The separate natural hostile combat effectiveness result remains inconclusive as documented in section 12.

## 27. Save payload evidence

**Automated proof / live save inspection:** The flying save at `phase_1k_9_backups\player_save_after_purchase_flying.json` contained:

- `player_credits`: 627.
- `current_ship_name`: `Pirate Transport`.
- Cargo: `construction-materials` quantity 1.
- Active missions: 0; completed missions: 14.
- Eight owned equipment entries with quantity 1.
- Seven mounted equipment entries, including Pulse Cannon on `PrimaryGunLeft`.
- Unchanged faction standings, including Neutral Civilians +0.738.
- Position and velocity while in flight.

**Code inspection:** Hull, shields, and energy are not serialized as independent save fields. On load, the saved ship definition is reapplied and the runtime ship is rebuilt; this explains the full definition maxima observed after loading.

## 28. Upgraded-ship save/load result

**Live observation:** Quicksave was performed while flying the Pirate Transport under GOTO. The ship then moved for an obvious temporary state change. Quickload restored the same Pirate Transport at the saved position/velocity with 627 CR, 3/200 cargo, the Pulse Cannon HUD profile, full ship bars, and no Scimitar model. The loaded ship then flew, used dock assist, and docked normally. This proves runtime reconstruction rather than merely JSON round-trip.

## 29. Duplicate/refund/exploit checks

**Live observation:**

- Purchase charged exactly once.
- Re-entering the ship dealer showed Pirate Transport `(OWNED)` and `Already Owned`.
- Pressing purchase on the already-owned Pirate Transport did not change 627 CR.
- Reload did not restore Scimitar, refund 18,000 CR, or duplicate cargo/equipment.
- Cargo remained one Construction Materials entry.
- Pulse Cannon remained one owned logical copy and one mount.
- The Scimitar row was no longer current; its displayed zero cost reflected the new transport’s 12,000 CR trade-in against the 12,000 CR Scimitar price, not a second trade-in award.
- Equipment mounting and selling state remained coherent.

## 30. Blocking bugs

**Live observation:** None in the legitimate ship-upgrade or persistence path. The ship could be purchased, launched, flown, fired, docked, saved, loaded, and docked again.

## 31. Confusing UX

**Live observation:** Mission timers and long GOTO/tradelane routing can outlast the listed mission effort. Mission targets can be lost during jump-hole or docking approach. The ship dealer communicates speed, hull, shields, and cargo but not handling or acceleration. The replacement warning is limited. Fallback markets can show a commodity as unavailable even when legitimately retained cargo needs liquidation.

## 32. Economy/balance concerns

**Inference:** The goal is achievable and did not require an exploit, but the first Pulse Cannon purchase substantially delayed the ship upgrade. Successful delivery rewards felt proportionate individually; failed long-route missions paid nothing and created a large time cost. No economy change is recommended from this single run.

## 33. Ship handling concerns

**Live observation / inference:** The transport’s slower, heavier feel was clear and still usable. Dock assist was practical once aligned but inefficient from a poor heading. This is worth follow-up for navigation usability, not rebalance during this phase.

## 34. Missing feedback

**Live observation:** The dealer could expose acceleration, turn rate, cruise, and afterburner differences. A stronger “this replaces your current ship” warning and explicit equipment/cargo transfer statement would reduce uncertainty. The mission UI could better communicate route viability and objective loss.

## 35. Nice-to-have improvements

- Add handling and propulsion fields to ship-dealer comparison.
- Explain cargo/equipment persistence and ship replacement in the purchase confirmation.
- Make GOTO/dock-assist route state and expected approach time clearer.
- Give the market UI a clear retained-cargo liquidation path when a commodity is unavailable.
- Make the market smoke harness independent of the player’s current system; see section 37.

## 36. Files changed and why

Only `docs/phase_1k_9_first_ship_upgrade.md` was added, to record this parity phase. No gameplay code changed. Save backups and screenshots were stored outside the repository.

## 37. Final regression results

**Automated proof:** Final build and smoke results:

| Command | Result |
| --- | --- |
| `dotnet build Roguelancer.sln --no-restore` | Succeeded, 0 warnings, 0 errors |
| `--ship-smoke` | 4 passed, 0 failed |
| `--save-smoke` | 4 passed, 0 failed |
| `--mission-smoke` | 14 passed, 0 failed |
| `--missile-smoke` | 4 passed, 0 failed |
| `--market-smoke` | 0 passed, 7 failed when run against the live upgraded save in Colorado |
| `--all-smoke` | 12 suites passed, 0 failed |

The standalone market smoke failure is a known harness context issue, not a live purchase failure: the upgraded save is validly in Colorado, while `MarketSmokeTest` resolves early-route stations that only exist in the New York system. The all-smoke runner reported all 12 suites green because its internal ordering/isolation did not reproduce the standalone live-save context. This should be fixed or isolated before treating the automated market gate as universally green. No gameplay code was changed to mask it.

## 38. Recommended next parity phase

Phase 1K.10 should target real hostile combat effectiveness and weapon readability with a naturally aligned encounter, while also making the market smoke harness system-context independent. GOTO/dock-assist approach time is a secondary usability follow-up.

## Final verdict

**Qualified pass.** The first earned ship upgrade milestone passed: 17,244 CR was earned legitimately, Pirate Transport purchase economics were exact, cargo/equipment/mounts persisted, runtime stats and rendering applied, flight and docking worked, Pulse Cannon live firing worked, and upgraded save/load restored the transport without duplication or refund. Natural hostile combat remains inconclusive, and standalone market smoke requires a context-aware harness fix.

