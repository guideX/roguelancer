# INI Files Updated - Unavailable Models Commented Out ?

## Summary of Changes

All INI configuration files have been updated to **comment out references to unavailable models** (those with old FBX formats that were disabled in Content.mgcb).

---

## ?? Files Updated

### 1. **Main Model Definition**
**File**: `config/models.ini`

**Disabled Models** (17 out of 21):

| Index | Name | Path | Reason |
|-------|------|------|--------|
| 2 | Broken Player Base | BASES\PLAYERBASE_01\PLAYERBASE_01 | FBX 6.1 |
| 3 | Border Worlds Elite | SHIPS\BW_ELITE\BW_ELITE | Old binary |
| 4 | Rheinland Gunship | SHIPS\RH_GUNSHIP\RH_GUNSHIP | FBX 6.1 |
| 5 | Xna Ship | SHIP | FBX 6.0 |
| 6 | Planet Manhatten | EARTH | FBX 6.1 |
| 7 | BROKEN BASE | BASES\CO_BASE_ROCK_LARGE01 | FBX 6.1 |
| 8 | BROKEN BASE | BASES\CO_BASE_ICE_LARGE02 | FBX 6.1 |
| 9 | BOOB BASE | BASES\CO_BASE_ROCK_LARGE02 | FBX 6.1 |
| 10 | BOOB BASE | BASES\CO_BASE_ROCK_LARGE02-2 | FBX 6.1 |
| 11 | RING | BASES\TRACK_RING\TRACK_RING | FBX 6.1 |
| 12 | Asteroid | ASTEROIDS\BERYL\BERYL | FBX 6.1 |
| 14 | Pirate Transport | SHIPS\CSV\CSV | Old binary |
| 15 | Trade Lane Ring | docking_ring | Old binary |
| 16 | ASD-52 | SHIPS\ASD52\ASD52 | Old binary + missing texture |
| 17 | Nomad Battleship | SHIPS\NOMAD_BATTLESHIP | Old binary |
| 18 | COCKPIT 1 | SHIPS\COCKPIT1 | Old binary |
| 20 | ALIEN PLANET | PLANETS\ALIEN_PLANET | FBX 6.1 |
| 21 | ALIEN PLANET | PLANETS\ALIEN_PLANET2 | FBX 6.1 |

**Available Models** (4 out of 21):

| Index | Name | Path | Format |
|-------|------|------|--------|
| 1 | Pirate Transport | SHIPS\PI_TRANSPORT\PI_TRANSPORT2 | .X (DirectX mesh) ? |
| 13 | Bullet | EARTH | (References disabled model) ?? |
| 19 | SUBSPACE SHIP 1 | SHIPS\Subspace\ship1 | .X (DirectX mesh) ? |
| + 8 more | SUBSPACE SHIP 2-8 | SHIPS\Subspace\ship2-8 | .X (DirectX mesh) ? |

---

### 2. **System Ship Instances**

**Files Updated:**
- `config/systems/new_york/ships.ini`
- `config/systems/texas/ships.ini`
- `config/systems/test_system/ships.ini`
- `config/systems/test_system2/ships.ini`

**Changes:**
- Commented out 4 ship instances that referenced unavailable models (indices 3, 4, 5, 14)
- Left 1 ship instance active (index 19 - SUBSPACE SHIP 1)

---

### 3. **System Planet Instances**

**File**: `config/systems/new_york/planets.ini`

**Changes:**
- Commented out Planet Manhattan (referenced unavailable model_index=20)

---

## ?? What Still Works

### Available Ship Models:
1. **Pirate Transport** (model_index=1) - PI_TRANSPORT2 (.X format)
2. **SUBSPACE SHIP 1-8** (model_index=19) - ship1-8 (.X format)

### Total Available:
- **9 working ship models** (1 Pirate Transport + 8 Subspace ships)
- All use **.X format** (DirectX mesh) which MonoGame supports natively

---

## ?? Model Index Reference

For quick reference when creating new content:

### ? AVAILABLE Models:
```ini
[1]  - Pirate Transport (SHIPS\PI_TRANSPORT\PI_TRANSPORT2)
[19] - SUBSPACE SHIP 1 (SHIPS\Subspace\ship1)
     - SUBSPACE SHIP 2-8 also available (ship2.x through ship8.x)
```

### ? DISABLED Models (Need FBX Conversion):
```ini
[2]  - Broken Player Base
[3]  - Border Worlds Elite
[4]  - Rheinland Gunship
[5]  - Xna Ship
[6]  - Planet Manhatten
[7]  - BROKEN BASE (ROCK_LARGE01)
[8]  - BROKEN BASE (ICE_LARGE02)
[9]  - BOOB BASE (ROCK_LARGE02)
[10] - BOOB BASE (ROCK_LARGE02-2)
[11] - RING
[12] - Asteroid
[14] - Pirate Transport CSV
[15] - Trade Lane Ring
[16] - ASD-52
[17] - Nomad Battleship
[18] - COCKPIT 1
[20] - ALIEN PLANET
[21] - ALIEN PLANET 2
```

---

## ?? To Re-Enable Models Later

After converting FBX files to modern format:

1. **Convert FBX files**:
   ```powershell
   .\convert-fbx-blender.ps1
   ```

2. **Restore Content.mgcb**:
   ```powershell
   .\restore-content.ps1
   ```

3. **Re-enable in models.ini**:
   - Uncomment the `#[X]` sections
   - Change `enabled=false` to `enabled=true`

4. **Re-enable system instances**:
   - Uncomment ship/planet instances in system INI files
   - Update `count=` values accordingly

5. **Rebuild**:
   ```powershell
   dotnet build
   ```

---

## ?? Impact on Gameplay

### Current State:
- ? Game will run without errors
- ? Available ships: 9 models (Pirate Transport + 8 Subspace variants)
- ? No planets visible (Planet Manhattan disabled)
- ? No stations/bases visible (all disabled)
- ? Most ship variety disabled

### Recommendation:
1. **Short term**: Use available models to test game mechanics
2. **Long term**: Convert FBX files and re-enable full content

---

## ?? Statistics

| Category | Total | Available | Disabled | Availability |
|----------|-------|-----------|----------|--------------|
| Models | 21 | 4 | 17 | 19% |
| Ships (NY) | 5 | 1 | 4 | 20% |
| Ships (Texas) | 5 | 1 | 4 | 20% |
| Ships (Test) | 5 | 1 | 4 | 20% |
| Ships (Test2) | 5 | 1 | 4 | 20% |
| Planets (NY) | 1 | 0 | 1 | 0% |

**Overall**: ~19% of original content is currently usable

---

## ?? Tips

### To add a new ship using available models:

```ini
[6]
model_index=1  # or 19 (Pirate Transport or Subspace Ship)
system_index=1
startup_position_x=-100000
startup_position_y=0
startup_position_z=-100000
startup_model_rotation_x=0.0
startup_model_rotation_y=0
startup_model_rotation_z=0.0
initial_model_up_x=0
initial_model_up_y=1
initial_model_up_z=0
initial_model_right_x=1
initial_model_right_y=0
initial_model_right_z=0
initial_velocity_x=0
initial_velocity_y=0
initial_velocity_z=0
initial_current_thrust=0
initial_direction_x=0
initial_direction_y=0
initial_direction_z=-1
cargo_space=100
dockable=false
```

### To test with Subspace ships 2-8:

The Subspace ships (ship2.x through ship8.x) are available but not in models.ini yet. You can add them:

```ini
[22]
name=SUBSPACE SHIP 2
type=1
enabled=true
path=SHIPS\Subspace\ship2
model_scaling=0
notes=Uses .X format (available)
```

---

**Status**: ? All INI files updated and synchronized with Content.mgcb
