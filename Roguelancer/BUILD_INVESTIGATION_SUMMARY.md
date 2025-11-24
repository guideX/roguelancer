# Content Pipeline Issues - Investigation Summary

## Problem Identified ?

You have **Content.mgcb** hardcoded with asset references that fail to build because:

1. **Old FBX formats** (FBX 6.0/6.1) - Not supported by MonoGame 3.8
2. **Texture compression issues** - Non-power-of-2 textures with DXT compression
3. **Old shader profiles** - Shaders using deprecated profiles

## What Was Fixed

### ? 1. Disabled Problematic FBX Files (20 files)
**Script**: `disable-problematic-content.ps1`

Commented out these in Content.mgcb:
- All old FBX format files (6.0, 6.1, old binary)
- Created backup: `Content.mgcb.backup`

### ? 2. Fixed Texture Compression (3 files)
**Script**: `fix-texture-compression.ps1`

Changed from `TextureFormat=Compressed` to `TextureFormat=Color`:
- PLANETS/ALIEN_PLANET/Render1.png
- PLANETS/ALIEN_PLANET2/Render1.png
- PLANETS/ALIEN_PLANET2/Render2.png

### ?? 3. Remaining Issue: Shader Profiles (4 files)

These shader files use old profiles and need updating:
- `Effects/Particle.fx` - uses `vs_2_0` profile
- `Effects/BloomExtract.fx` - missing profile declaration
- `Effects/GaussianBlur.fx` - missing profile declaration
- `Effects/BloomCombine.fx` - missing profile declaration

**Error**: "must be SM 4.0 level 9.1 or higher!"

## Current Build Status

**Content Built**: 48 succeeded ?  
**Content Failed**: 4 failed ??  
**Reason**: Shader profile issues

## Why This Happened

Your `models.ini` file (`config/models.ini`) is for **runtime configuration** only.  
The build process uses `Content.mgcb` which is **separate** and contains:
- Hardcoded references to all assets
- Import/processing settings for each asset
- No awareness of your models.ini settings

### The Two Systems:

| System | File | Purpose | When Used |
|--------|------|---------|-----------|
| **Build Pipeline** | Content.mgcb | What to compile into XNB | Build time |
| **Runtime Config** | models.ini | What to load & configure | Runtime |

**They don't talk to each other!**

## What You Need To Do

### Option 1: Quick Fix (Skip Shaders Temporarily)

If you don't need the bloom/particle effects right now:

```powershell
# Comment out the shader effects in Content.mgcb
# Then rebuild
dotnet build
```

### Option 2: Fix Shaders (Recommended)

The shader files need to be updated to use modern HLSL profiles:

**Old profiles** (XNA 4.0):
- `vs_2_0`, `ps_2_0`

**New profiles** (MonoGame/DirectX 11):
- `vs_4_0_level_9_1` or higher
- `ps_4_0_level_9_1` or higher

Would you like me to fix these shader files for you?

### Option 3: Convert FBX Files & Restore Everything

After you've converted all FBX files to modern format:

```powershell
# Convert FBX files
.\convert-fbx-blender.ps1  # or convert-fbx-files.ps1

# Restore original Content.mgcb
.\restore-content.ps1

# Fix shaders
# (I can create a script for this)

# Rebuild
dotnet build
```

## Scripts Created For You

| Script | Purpose |
|--------|---------|
| `check-fbx-versions.ps1` | Analyze which FBX files need conversion |
| `convert-fbx-blender.ps1` | Convert FBX using Blender (free) |
| `convert-fbx-files.ps1` | Convert FBX using Autodesk tool |
| `disable-problematic-content.ps1` | ? Disable bad content (already run) |
| `fix-texture-compression.ps1` | ? Fix texture issues (already run) |
| `restore-content.ps1` | Restore Content.mgcb from backup |

## Next Steps

1. **Decide on shaders**: Fix them or disable them?
2. **Eventually**: Convert all FBX files to modern format
3. **Then**: Restore full Content.mgcb and rebuild

## Quick Commands

```powershell
# See what needs converting
.\check-fbx-versions.ps1

# Build current state (shaders will still fail)
dotnet build

# Clean build
dotnet clean
dotnet build --no-incremental
```

## Understanding the Build Flow

```
dotnet build
    ?
MonoGame.Content.Builder.Task.targets
    ?
Reads Content.mgcb
    ?
For each #begin entry:
    ?? Import asset (FbxImporter, TextureImporter, etc.)
    ?? Process asset (ModelProcessor, TextureProcessor, etc.)
    ?? Output to bin/Windows/Content/*.xnb
    ?
If ANY asset fails ? Build FAILS
    ?
Code compilation (if content succeeded)
```

**Your models.ini is never read during build!**

## Files Missing "On Purpose"

If files are missing on purpose, you have 3 options:

### 1. Remove from Content.mgcb
Delete the `#begin` block for that asset.

### 2. Comment out in Content.mgcb
```
# DISABLED - Not using this
#begin SomeFile.fbx
#/importer:FbxImporter
#...
```

### 3. Use MGCB Editor
```powershell
dotnet mgcb-editor Content\Content.mgcb
```
Then exclude files visually.

---

**Bottom Line**: Content.mgcb controls what gets built. Models.ini controls what gets loaded. They're independent systems that need to be kept in sync manually!
