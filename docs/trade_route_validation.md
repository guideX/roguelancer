# Developer trade-route validation

Launch from the repository with:

```text
dotnet run --project Roguelancer.csproj -- --dev-trade-route
```

The explicit flag skips the normal default save load and starts a fresh,
isolated validation session docked on-foot at real Fort Bush in New York. It
uses the existing Scimitar (50 cargo units), 100,000 CR, empty ordinary and
mission cargo, the configured Fort Bush/Riverside runtime markets, and a 5x
validation-only travel multiplier. F6/F8 use the separate validation save at
the path printed by `TradeRouteValidationBootstrap.GetValidationSavePath()`;
the normal player save is not overwritten.

Controls:

1. Press `M` to open the real Market Opportunities UI, select Food Rations:
   Fort Bush → Riverside Station, and press `R` to create the normal Trade
   Plan. (`M` is a validation-only entry convenience; the board interaction
   remains available.)
2. Walk to the physical COMMODITY TRADER station interaction, press `E`, and
   buy Food Rations with the normal dealer. Return to the airlock and board
   the ship through the normal station flow.
3. Follow the active GOTO target. Press `F4` when in range of each real jump
   hole: New York → Texas, then Texas → California.
4. Dock Riverside normally, open the Commodity Trader, and sell the ordinary
   Food Rations cargo.

The console prints major lifecycle events and emits `[TRADE VALIDATION] PASS`
only after the real source purchase, both system transitions, Riverside dock,
destination sale, and Trade Plan completion have all been observed.

The focused regression suite is:

```text
dotnet run --no-build --project Roguelancer.csproj -- --trade-route-validation-smoke
```

Normal launch without `--dev-trade-route` retains the ordinary save/new-game
flow, market-intelligence discovery rules, cargo/credit state, and travel
speed.
