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
