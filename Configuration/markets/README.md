# Market Balancing Notes

These station markets are tuned for the first playable system so early trading has a few clear lanes without turning into a dynamic economy.

Intended routes:

- Fort Bush -> Newark Station: food rations, water, and H-fuel for a low-risk legal loop.
- Rochester Base -> Newark Station: construction materials and boron for a steady industrial run.
- Rochester Base -> Detroit Munitions: construction materials as a shorter industrial shuttle.
- Detroit Munitions -> Newark Station: engine components for a higher-value technical supply run.
- Buffalo Base -> Rochester Base: side arms or alien organisms for a risky contraband route.

Balancing rules:

- Keep same-station buy prices above sell prices so stations do not create self-arbitrage.
- Keep contraband clearly marked with `is_available: true` only where the market should actually stock it.
- Keep stocks moderate so early cargo runs feel meaningful without flooding the player with easy credits.

Phase 14 dynamic-market rules:

- `stock` is the configured normal/equilibrium stock. Runtime stock moves only
  through authoritative trades and elapsed-time recovery.
- Buy price uses `(baseline - current) / baseline` with a bounded response of
  35%, clamped to 65%-135% of the configured buy price.
- Sell price uses the same pressure with a bounded response of 50%, clamped to
  50%-150% of the configured sell price. A five-percent minimum spread (or the
  smaller configured spread) prevents local price inversion.
- A successful player buy also caps the immediate same-station sell price at
  that buy price, so price impact cannot create a risk-free round trip.
- Recovery is deterministic and lazy: one full baseline-stock amount recovers
  over 3,600 simulated seconds by default. Optional per-good fields are
  `minimum_stock`, `maximum_stock`, and `recovery_seconds`.
- Default practical stock capacity is four times baseline (at least baseline
  plus 100 units), capped at 1,000,000 units. Excess sales are rejected before
  cargo or credit state changes.
