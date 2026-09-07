# Phase 61 — Market Consumption and Autonomous Recovery

Configured station markets consume ordinary, legal, available commodity stock
through `MarketManager`'s existing lazy elapsed-simulation-time update path.
Consumption never creates a parallel inventory or a persistent backorder.

## Policy

- A listing may set `consumption_rate_per_minute` in its market JSON.
- Missing rates use the deterministic default
  `0.25 + stock / 1200 + demand_level / 100`, rounded to milli-units and
  clamped to `0.25–2.0` units per economic minute.
- An explicit rate of `0` disables autonomous consumption for that listing.
- Invalid negative, non-finite, or above-maximum configured rates are rejected
  with the listing, while contraband and mission cargo are always excluded.
- Newark Diamonds is the first explicit durable zero-rate listing; old market
  files remain valid without the new field.

Consumption uses fixed-point milli-units and carries a bounded fractional
remainder through save/load. Each lazy consumption update processes at most
five economic minutes. If stock reaches zero, any remaining request is
discarded rather than becoming a backorder.

Transaction-driven market recovery remains the existing bounded stock recovery
behavior. A consumed listing stays demand-driven until a player/NPC stock
mutation supplies it again. Prices, shortage hysteresis, Emergency Supply,
player trading, exports, and Phase 59 shipments all read the same runtime
stock.

Phase 59 cargo selection adds a bounded shortage bonus of 50–250 score points
to the existing price-difference score. This changes cargo selection only;
route destinations and route topology remain authoritative.

Consumption remainder and recovery mode are additive market save fields. The
existing save schema remains version 11. Restored listings begin at the saved
simulation timestamp, so real-world time while the game is closed does not
drain stock.
