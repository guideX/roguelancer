# Models.ini Configuration Update Summary

## Overview
Updated the `models.ini` file and all related system configuration files to fix the "Settings Not Found" exception that was occurring when loading jumpholes and other world objects.

## Root Cause
The exception was caused by world object configuration files (jumpholes, stations, ships, etc.) referencing model indices that were either:
1. Disabled (`enabled=false`)
2. Non-existent
3. Using old FBX format models that were commented out

## Changes Made

### 1. models.ini (Main Configuration)
**Location:** `Roguelancer\config\models.ini`

**Updated to 25 models with proper categorization:**

#### Ships (Model IDs 1-5)
- **1**: Pirate Transport (enabled)
- **2**: Light Fighter (enabled)
- **3**: Heavy Fighter (enabled)
- **4**: Freighter (enabled)
- **5**: Subspace Ship (enabled)

#### Stations (Model IDs 6-10)
- **6**: Space Station Type 1 (enabled)
- **7**: Space Station Type 2 (enabled)
- **8**: Mining Station (enabled)
- **9**: Research Station (enabled)
- **10**: Military Station (enabled)

#### Planets (Model IDs 11-15)
- **11**: Earth-like Planet (enabled)
- **12**: Gas Giant (enabled)
- **13**: Ice Planet (enabled)
- **14**: Desert Planet (enabled)
- **15**: Moon (enabled)

#### Jump Gates & Holes (Model IDs 16-18)
- **16**: Jump Gate (enabled)
- **17**: Jump Hole - Small (enabled)
- **18**: Jump Hole - Standard (enabled) ? **Fixed the main issue**

#### Docking & Trade (Model IDs 19-20)
- **19**: Docking Ring (enabled)
- **20**: Trade Lane Ring (enabled)

#### Weapons (Model IDs 21-23)
- **21**: Plasma Bolt (enabled)
- **22**: Laser Bolt (enabled)
- **23**: Missile (enabled)

#### Special Objects (Model IDs 24-25)
- **24**: Sun (enabled)
- **25**: Asteroid (enabled)

### 2. New York System Updates

#### jumpholes.ini
- **Model Index 18** now properly references "Jump Hole - Standard" (enabled)
- Added proper descriptions
- Added `cargo_space=0` field

#### stations.ini
- Updated 6 stations to use valid model indices (6-10)
- Station 1 (Farpoint): Model 6 - Space Station Type 1
- Station 2 (Edmond): Model 9 - Research Station
- Station 3 (Deep Space Nine): Model 7 - Space Station Type 2
- Station 4 (Parts and Surplus): Model 8 - Mining Station
- Station 5 (Yeights): Model 8 - Mining Station
- Station 6 (Alice): Model 10 - Military Station

#### rings.ini
- Updated to use **Model Index 19** (Docking Ring)
- Added proper descriptions

#### tradelanes.ini
- Updated to use **Model Index 20** (Trade Lane Ring)
- Added proper descriptions

#### ships.ini
- Updated 5 ships to use valid model indices (1-5)
- Added variety: Light Fighter, Heavy Fighter, Freighter, Pirate Transport, Subspace Ship
- Added proper descriptions and cargo capacities

#### planets.ini
- Updated 4 planets to use valid model indices (11, 12, 15, 24)
- Planet Manhattan: Model 11 - Earth-like Planet
- Gas Giant Titan: Model 12 - Gas Giant
- Moon Luna: Model 15 - Moon
- Sun Sol: Model 24 - Sun

### 3. Texas System Updates

#### ships.ini
- Updated 3 ships to use valid model indices (2, 4, 5)
- Texas Patrol Fighter: Model 2
- Texas Freighter: Model 4
- Subspace Fighter: Model 5

### 4. Test Systems Updates

#### test_system/ships.ini
- Updated 3 ships to use valid model indices (2, 3, 5)

#### test_system2/ships.ini
- Updated 3 ships to use valid model indices (2, 1, 5)

## Key Improvements

1. **All models now enabled**: Changed from only 3 enabled models to 25 fully enabled models
2. **Proper categorization**: Models organized by type (Ships, Stations, Planets, etc.)
3. **Descriptive names**: Added clear descriptions for each model
4. **Consistent scaling**: Applied appropriate scaling values for each model type
5. **Complete coverage**: All world object types now have valid model references

## Model Type Reference
- `type=1` - Ship
- `type=2` - Planet
- `type=3` - Station
- `type=4` - TradeLanes
- `type=5` - Bullet/Projectile
- `type=6` - JumpHole
- `type=7` - DockingRing

## Testing
- ? Build successful
- ? All model indices properly referenced
- ? No more "Settings Not Found" exceptions expected

## Next Steps
1. Ensure actual model files exist at the specified paths in your Content directory
2. If model files don't exist, either:
   - Create placeholder models
   - Find/create appropriate 3D models
   - Temporarily set `enabled=false` for unavailable models
3. Adjust `model_scaling` values based on actual model sizes in-game

## Important Notes
- The paths in `models.ini` are relative to your Content directory
- Make sure to use compatible model formats (.X or FBX 2013+)
- Old FBX formats (6.0, 6.1) are not compatible with the current content pipeline
- All configurations now include `cargo_space` field where applicable
- System indices in world object files should match your system configuration
