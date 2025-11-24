# FBX Conversion Guide for Roguelancer

## Problem
Your FBX files are in old formats (FBX 6.0, 6.1) which are not supported by MonoGame 3.8.
MonoGame only supports FBX 2011, 2012, and 2013 formats.

## Solutions

### Option 1: Autodesk FBX Converter (Fastest)

1. **Download and Install Autodesk FBX Converter**
   - Download from: https://www.autodesk.com/developer-network/platform-technologies/fbx-converter-archives
   - Install to default location (or note your custom path)

2. **Run the Conversion Script**
   ```powershell
   cd D:\devgithub\Roguelancer\Roguelancer
   .\convert-fbx-files.ps1
   ```

   If installed to a custom location:
   ```powershell
   .\convert-fbx-files.ps1 -FbxConverterPath "C:\Your\Custom\Path\FbxConverter.exe"
   ```

### Option 2: Blender (Free, Slower but More Reliable)

1. **Download and Install Blender**
   - Download from: https://www.blender.org/download/
   - Install version 3.0 or higher

2. **Run the Conversion Script**
   ```powershell
   cd D:\devgithub\Roguelancer\Roguelancer
   .\convert-fbx-blender.ps1
   ```

   If installed to a custom location:
   ```powershell
   .\convert-fbx-blender.ps1 -BlenderPath "C:\Your\Custom\Path\blender.exe"
   ```

### Option 3: Manual Conversion with Blender

If the automated scripts don't work:

1. Open Blender
2. File ? Import ? FBX (.fbx)
3. Select your old FBX file
4. File ? Export ? FBX (.fbx)
5. In export settings, set "Format" to "FBX 7.4 binary" (FBX 2013)
6. Export and replace the original file

Repeat for each FBX file in the Content directory.

## Files That Need Conversion

Based on your Content.mgcb file, these FBX files need conversion:

**Bases:**
- BASES/CO_BASE_ICE_LARGE02/CO_BASE_ICE_LARGE02.fbx
- BASES/CO_BASE_ROCK_LARGE01/CO_BASE_ROCK_LARGE01.fbx
- BASES/CO_BASE_ROCK_LARGE02/CO_BASE_ROCK_LARGE02-2.fbx
- BASES/CO_BASE_ROCK_LARGE02/CO_BASE_ROCK_LARGE02.fbx
- BASES/PLAYERBASE_01/PLAYERBASE_01.fbx
- BASES/TRACK_RING/TRACK_RING.fbx

**Ships:**
- SHIPS/ASD52/ASD52.fbx
- SHIPS/NOMAD_BATTLESHIP/NOMAD_BATTLESHIP.fbx
- SHIPS/RH_GUNSHIP/RH_GUNSHIP.fbx
- SHIPS/BW_ELITE/BW_ELITE.fbx
- SHIPS/BORDER_WORLD/BW_ELITE/BW_ELITE.fbx
- SHIPS/CSV/CSV.fbx
- SHIPS/COCKPIT1.fbx
- Ship.fbx

**Planets:**
- PLANETS/ALIEN_PLANET/ALIEN_PLANET.fbx
- PLANETS/ALIEN_PLANET2/ALIEN_PLANET2.fbx
- Earth.fbx

**Other:**
- ASTEROIDS/BERYL/BERYL.fbx
- bullet.FBX
- docking_ring.fbx

## Other Issues Fixed

### Font Issue
Changed `HudFont.spritefont` from "Segoe UI Mono" (not commonly available) to "Consolas" (standard Windows font).

### Missing Texture
The file `SHIPS/ASD52/ASD52.fbx` references a texture at:
```
D:/devgithub/3D Meshes/ASD-52 Tyr - Red Hessian Very Heavy Fighter/rh_panel1.png
```

This appears to be an absolute path to a texture file outside your project. You'll need to:
1. Copy the texture file to your Content directory
2. Re-export the FBX with relative texture paths, or
3. Manually edit the texture reference

## Safety Notes

- Both scripts create `.backup` files before conversion
- Original files are preserved until conversion is verified
- To restore originals, rename `.backup` files back to `.fbx`

## After Conversion

Once conversion is complete:
1. Delete or remove `.backup` files if conversions are successful
2. Run `dotnet build` in your project directory
3. Verify that the content pipeline builds without errors

## Need Help?

If you encounter issues:
1. Check that the converter tool is properly installed
2. Verify you have write permissions to the Content directory
3. Try converting a single file manually first to verify the tool works
4. Check the error messages for specific file issues
