# Understanding Content Pipeline vs models.ini

## The Problem

You have **TWO separate configuration systems** that aren't synchronized:

### 1. `Content.mgcb` (MonoGame Content Pipeline)
- **Purpose**: Tells the build system which assets to compile
- **Location**: `Roguelancer\Content\Content.mgcb`
- **When it runs**: During every build (before code compilation)
- **What it does**: Converts FBX, textures, fonts, etc. into XNB binary format

### 2. `models.ini` (Your Runtime Configuration)
- **Purpose**: Tells your game which models to load at runtime
- **Location**: `Roguelancer\config\models.ini`
- **When it runs**: When your game starts
- **What it does**: Configures which models are enabled/disabled and their properties

## Why Build Fails

The `Content.mgcb` file contains **hardcoded references** to all FBX files, including:
- ? Files with old FBX formats (6.0, 6.1) that can't be imported
- ? Files you may have marked `enabled=false` in models.ini
- ? Files that might not exist anymore

**Even if you disable a model in `models.ini`, the build still tries to compile it because `Content.mgcb` doesn't know about your runtime config!**

## Current Build Errors

These files are failing because they use **old FBX formats**:

| File | Format | Status |
|------|--------|--------|
| BASES/CO_BASE_ICE_LARGE02/CO_BASE_ICE_LARGE02.fbx | FBX 6.1 | ? Too old |
| BASES/CO_BASE_ROCK_LARGE01/CO_BASE_ROCK_LARGE01.fbx | FBX 6.1 | ? Too old |
| BASES/TRACK_RING/TRACK_RING.fbx | FBX 6.1 | ? Too old |
| SHIPS/ASD52/ASD52.fbx | Old binary | ? Missing texture |
| SHIPS/BW_ELITE/BW_ELITE.fbx | Old binary | ? Too old |
| SHIPS/CSV/CSV.fbx | Old binary | ? Too old |
| SHIPS/NOMAD_BATTLESHIP/NOMAD_BATTLESHIP.fbx | Old binary | ? Too old |
| Ship.fbx | FBX 6.0 | ? Too old |
| Earth.fbx | FBX 6.1 | ? Too old |
| And 11 more... | Various | ? Too old |

## Solutions

### Quick Fix: Disable Problematic Files Temporarily

```powershell
cd D:\devgithub\Roguelancer\Roguelancer
.\disable-problematic-content.ps1
dotnet build
```

This will:
1. Comment out problematic FBX files in Content.mgcb
2. Create a backup (Content.mgcb.backup)
3. Allow your project to build successfully
4. Your game will still run, just without those specific models

### Permanent Fix: Convert FBX Files

```powershell
# Option 1: Using Blender (Free)
.\convert-fbx-blender.ps1

# Option 2: Using Autodesk FBX Converter
.\convert-fbx-files.ps1

# After conversion, restore Content.mgcb
.\restore-content.ps1
dotnet build
```

## How the Content Pipeline Works

```
Build Process:
???????????????????????????????????????????????????????
? 1. dotnet build (or Visual Studio Build)           ?
?    ?                                                 ?
? 2. MonoGame.Content.Builder.Task.targets executes   ?
?    ?                                                 ?
? 3. Reads Content.mgcb file                          ?
?    ?                                                 ?
? 4. For each #begin entry:                           ?
?    - Find the source file (e.g., Ship.fbx)          ?
?    - Run importer (e.g., FbxImporter)               ?
?    - Run processor (e.g., ModelProcessor)           ?
?    - Output to Content/bin/Windows/Content/*.xnb    ?
?    ?                                                 ?
? 5. If ANY file fails, entire build fails            ?
?    ?                                                 ?
? 6. Code compilation begins (if content succeeded)   ?
???????????????????????????????????????????????????????
```

## Managing Content.mgcb

### Using MonoGame Content Pipeline Editor (MGCB Editor)

The proper way to edit Content.mgcb:

```powershell
cd D:\devgithub\Roguelancer\Roguelancer
dotnet mgcb-editor Content\Content.mgcb
```

This opens a GUI where you can:
- ? Add/remove content files
- ? Configure import/processing parameters
- ? See what will be built
- ? Exclude files without deleting them

### Manual Editing

You can also edit `Content.mgcb` in any text editor. To exclude a file:

**Before:**
```
#begin SHIPS/OLD_SHIP/OLD_SHIP.fbx
/importer:FbxImporter
/processor:ModelProcessor
/build:SHIPS/OLD_SHIP/OLD_SHIP.fbx
```

**After (commented out):**
```
# DISABLED - Not using this model
#begin SHIPS/OLD_SHIP/OLD_SHIP.fbx
#/importer:FbxImporter
#/processor:ModelProcessor
#/build:SHIPS/OLD_SHIP/OLD_SHIP.fbx
```

Or simply delete the entire block.

## Synchronizing models.ini and Content.mgcb

If you want your `models.ini` to control what gets built, you have two options:

### Option 1: Manual Sync (Current Approach)
- Edit Content.mgcb to match models.ini manually
- Keep them in sync whenever you change models

### Option 2: Dynamic Content Pipeline (Advanced)
Create a pre-build script that reads models.ini and generates Content.mgcb automatically. This requires:
1. Custom build task
2. Parsing models.ini
3. Generating/updating Content.mgcb before build

## Common Questions

**Q: Why does models.ini exist if Content.mgcb already defines models?**
A: 
- `Content.mgcb` = Build-time: What to compile
- `models.ini` = Runtime: What to load and how to configure

**Q: If I delete a model from Content.mgcb, will it break my game?**
A: Only if your code tries to load that model. You'll get a runtime error when loading.

**Q: Can I have models in Content.mgcb that aren't in models.ini?**
A: Yes! They'll be compiled but not loaded by your game (wasted space).

**Q: Can I have models in models.ini that aren't in Content.mgcb?**
A: No - the build will succeed, but your game will crash trying to load missing content.

## Recommended Workflow

1. **Development Phase**: Keep Content.mgcb minimal
   ```powershell
   # Only include assets you're actively using
   .\disable-problematic-content.ps1
   ```

2. **When Adding New Models**:
   - Add to Content folder
   - Add to Content.mgcb using MGCB Editor
   - Add to models.ini
   - Build and test

3. **Before Release**:
   - Convert all FBX files to modern format
   - Re-enable all content in Content.mgcb
   - Verify all models.ini entries have corresponding content

## Quick Reference Scripts

```powershell
# Check FBX versions
.\check-fbx-versions.ps1

# Temporarily disable problematic content
.\disable-problematic-content.ps1

# Convert FBX files (choose one)
.\convert-fbx-blender.ps1          # Free
.\convert-fbx-files.ps1             # Autodesk

# Restore all content after conversion
.\restore-content.ps1

# Force content rebuild
dotnet clean
dotnet build --no-incremental

# Open content editor
dotnet mgcb-editor Content\Content.mgcb
```

## File Locations Summary

| File | Purpose | When Used |
|------|---------|-----------|
| `Content\Content.mgcb` | Build pipeline config | Build time |
| `config\models.ini` | Runtime model config | Runtime |
| `Content\bin\Windows\Content\*.xnb` | Compiled assets | Runtime |
| `Content\**\*.fbx` | Source models | Build time |

---

**Bottom Line**: You need to either convert the old FBX files OR temporarily disable them in Content.mgcb to get your build working!
