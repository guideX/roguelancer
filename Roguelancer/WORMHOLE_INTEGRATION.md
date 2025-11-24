# Wormhole Concept Integration

## Overview
Integrated the wormhole concept from `D:\bkup\wormhole-concept\` into the Roguelancer game project to replace missing jump hole models. Currently using a temporary placeholder model while the proper wormhole FBX export is prepared.

## Current Status
? Build successful
? Model configuration updated
? Wormhole concept files copied to project
?? Using temporary placeholder (Subspace ship1 model)
?? Next step: Export Portal.blend to FBX with corrected texture paths

## Changes Made

### 1. Model Configuration (`config\models.ini`)
Updated jump hole entries [17] and [18] to use a working model:

**Entry [17] - Jump Hole - Small:**
- Name: Jump Hole - Small
- Description: Small Unstable Jump Hole
- Path: `SHIPS\Subspace\ship1` (temporary)
- Model Scaling: 2.5
- Notes: Temporary using ship model - wormhole concept pending proper FBX export from Blender

**Entry [18] - Jump Hole - Standard:**
- Name: Jump Hole - Standard
- Description: Standard Jump Hole
- Path: `SHIPS\Subspace\ship1` (temporary)
- Model Scaling: 3.5
- Notes: Temporary using ship model - wormhole concept pending proper FBX export from Blender

Both entries use different scaling (2.5x and 3.5x) to provide size variety.

### 2. Content Directory Structure
Created and populated directory structure:
```
Roguelancer\Content\Models\OBJECTS\WORMHOLE\
??? WORMHOLE.fbx          (Placeholder - has texture path issues)
??? PortalWalls.png       (Texture file - ready to use)
??? source\
?   ??? Portal.blend      (Original Blender source file - 1.75 MB)
??? textures\
?   ??? PortalWalls.png   (Original texture - 715 KB)
??? export_wormhole.py    (Blender export script for future use)
```

### 3. Texture Files
Added wormhole texture to root Content directory:
- `Content\PortalWalls.png` - Ready for FBX import
- `Content\PortalWalls_0.png` - Variant for FBX external reference

## Known Issues

### FBX Texture Path Problem
The existing `jumphole.fbx` file has hardcoded absolute texture paths:
```
C:/Users/DarthguideX/Desktop/wormhole-concept/source/textures/PortalWalls_0.xnb
```

This causes build failures because:
1. The path is absolute (not relative to Content directory)
2. MonoGame Content Pipeline requires external references to be in the root Content directory
3. The original FBX was likely created on a different machine with different paths

### Solution Required
To properly integrate the wormhole concept, you need to:

1. **Export from Blender with correct settings:**
   - Open `Portal.blend` in Blender
   - File ? Export ? FBX (.fbx)
   - **Important Settings:**
     - Path Mode: **Copy**
     - Embed Textures: **No** (let MonoGame handle textures)
     - Or use Path Mode: **Relative** 
   - Export to: `Content\Models\OBJECTS\WORMHOLE\WORMHOLE.fbx`

2. **Alternative: Use the Python script:**
   ```bash
   cd "D:\devgithub\Roguelancer\Roguelancer\Content\Models\OBJECTS\WORMHOLE"
   blender --background --python export_wormhole.py
   ```

3. **Manual texture reference fix (if needed):**
   - Open the FBX in a text editor that supports binary (not recommended)
   - Or use Blender to re-link textures and re-export
   - Or use Autodesk FBX Converter to reset texture paths

## Temporary Workaround
Currently using `SHIPS\Subspace\ship1` model as a placeholder because:
- ? It's a working .X format model already in the content pipeline
- ? Build compiles successfully
- ? Provides functional jump holes for testing
- ?? Not visually appropriate (it's a ship, not a wormhole)
- ?? Lacks the intended wormhole aesthetic

## Wormhole Concept Details
The original wormhole concept includes:
- **Blender Source:** `Portal.blend` (1.75 MB)
  - Located: `Content\Models\OBJECTS\WORMHOLE\source\Portal.blend`
- **Texture:** `PortalWalls.png` (715 KB, PNG format)
  - Original: `textures\PortalWalls.png`
  - Copied to: `Content\PortalWalls.png`

## Next Steps to Complete Integration

### Step 1: Export Proper FBX
```bash
# Option A: Use Blender GUI
1. Open Blender
2. File ? Open ? Navigate to:
   D:\devgithub\Roguelancer\Roguelancer\Content\Models\OBJECTS\WORMHOLE\source\Portal.blend
3. File ? Export ? FBX (.fbx)
4. Set Export Path: D:\devgithub\Roguelancer\Roguelancer\Content\Models\OBJECTS\WORMHOLE\WORMHOLE.fbx
5. In FBX Export settings:
   - Path Mode: "Copy" or "Strip Path"
   - Embed Textures: OFF
6. Click Export FBX

# Option B: Use Blender Command Line (if Blender is in PATH)
blender --background "D:\devgithub\Roguelancer\Roguelancer\Content\Models\OBJECTS\WORMHOLE\source\Portal.blend" --python "D:\devgithub\Roguelancer\Roguelancer\Content\Models\OBJECTS\WORMHOLE\export_wormhole.py"
```

### Step 2: Update Content Pipeline
Once the FBX is properly exported, add to `Content.mgcb`:
```ini
#begin Models/OBJECTS/WORMHOLE/WORMHOLE.fbx
/importer:FbxImporter
/processor:ModelProcessor
/processorParam:ColorKeyColor=0,0,0,0
/processorParam:ColorKeyEnabled=False
/processorParam:DefaultEffect=BasicEffect
/processorParam:GenerateMipmaps=True
/processorParam:GenerateTangentFrames=False
/processorParam:PremultiplyTextureAlpha=True
/processorParam:PremultiplyVertexColors=True
/processorParam:ResizeTexturesToPowerOfTwo=False
/processorParam:RotationX=0
/processorParam:RotationY=0
/processorParam:RotationZ=0
/processorParam:Scale=1
/processorParam:SwapWindingOrder=False
/processorParam:TextureFormat=Color
/build:Models/OBJECTS/WORMHOLE/WORMHOLE.fbx
```

### Step 3: Update models.ini
```ini
[17]
name=Jump Hole - Small
description=Small Unstable Jump Hole (Wormhole Concept)
type=6
enabled=true
path=OBJECTS\WORMHOLE\WORMHOLE
model_scaling=2.5
notes=New wormhole concept model

[18]
name=Jump Hole - Standard
description=Standard Jump Hole (Wormhole Concept)
type=6
enabled=true
path=OBJECTS\WORMHOLE\WORMHOLE
model_scaling=3.5
notes=New wormhole concept model - larger scale
```

### Step 4: Build and Test
```bash
# Build the project
dotnet build

# Test in-game:
- Load a game
- Navigate to a system with jump holes (New York, Texas, etc.)
- Verify wormhole model appears correctly
- Test jump hole functionality (approach, dock, jump)
- Verify texture is properly applied
```

## Impact on Game Systems
The wormhole model is used by:
- **Jump Hole Objects:** All jump holes in all star systems
- **Model Type:** Type 6 (Jump Gates & Holes)
- **World Objects:** Configured via `jumpholes.ini` files in each system directory:
  - `config\systems\new_york\jumpholes.ini`
  - `config\systems\texas\jumpholes.ini`
  - `config\systems\test_system\jumpholes.ini`
  - `config\systems\test_system2\jumpholes.ini`

## Testing Checklist
- [x] Build project successfully
- [ ] Export proper FBX from Blender
- [ ] Add FBX to Content Pipeline
- [ ] Update models.ini to use new wormhole path
- [ ] Rebuild project
- [ ] Load game and navigate to jump hole
- [ ] Verify wormhole model appears correctly (not ship model)
- [ ] Verify texture is properly applied
- [ ] Test jump hole functionality (approach, dock, jump)
- [ ] Test both small (2.5x) and standard (3.5x) scale variations
- [ ] Verify performance with multiple wormholes visible

## Additional Enhancements (Future)
Consider these improvements once the base integration is complete:
- **Animation:** Add rotation or pulsing effects to the wormhole
- **Particle Effects:** Add swirling particles at the entrance
- **Lighting:** Add glow effects around the wormhole
- **Sound Effects:** Add ambient sound when near wormholes
- **Visual Variations:** Create different textures for different wormhole types
- **Instability Effects:** Add visual distortion for "unstable" jump holes

## Files Modified
- ? `Roguelancer\config\models.ini` - Updated entries [17] and [18]
- ? `Roguelancer\Content\Content.mgcb` - Removed problematic entries
- ? Created `Roguelancer\Content\Models\OBJECTS\WORMHOLE\` directory structure
- ? Copied wormhole concept files from `D:\bkup\wormhole-concept\`
- ? Created export script: `export_wormhole.py`
- ? Created documentation: `WORMHOLE_INTEGRATION.md`

## Summary
The wormhole concept has been integrated into the project structure, but requires proper FBX export from Blender to complete the integration. Currently using a temporary placeholder model to ensure the game builds and runs. The wormhole Blender file and textures are ready and waiting for proper export.
