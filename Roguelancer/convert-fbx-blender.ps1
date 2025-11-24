# PowerShell script to convert FBX files using Blender
# This script requires Blender to be installed
# Download from: https://www.blender.org/download/

param(
    [string]$BlenderPath = "D:\Program Files\Blender Foundation\Blender 5.0\blender.exe"
)


# Check if Blender exists
if (-not (Test-Path $BlenderPath)) {
    Write-Host "ERROR: Blender not found at: $BlenderPath" -ForegroundColor Red
    Write-Host "Please download and install Blender from: https://www.blender.org/download/" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or specify the correct path using:" -ForegroundColor Yellow
    Write-Host ".\convert-fbx-blender.ps1 -BlenderPath 'C:\Path\To\blender.exe'" -ForegroundColor Yellow
    exit 1
}

# Create Blender Python script for batch conversion
$blenderScript = @'
import bpy
import sys
import os

# Get command line arguments
argv = sys.argv
argv = argv[argv.index("--") + 1:]  # Get all args after "--"

input_file = argv[0]
output_file = argv[1]

# Clear the default scene
bpy.ops.wm.read_factory_settings(use_empty=True)

# Import FBX
try:
    bpy.ops.import_scene.fbx(filepath=input_file)
    
    # Export as FBX 2013
    bpy.ops.export_scene.fbx(
        filepath=output_file,
        version='BIN7400',  # FBX 7.4 (2013)
        use_selection=False,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=False,
        object_types={'MESH', 'ARMATURE', 'EMPTY', 'LIGHT', 'CAMERA'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_tspace=False,
        use_custom_props=True,
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        armature_nodetype='NULL',
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=1.0,
        path_mode='AUTO',
        embed_textures=False,
        batch_mode='OFF',
        axis_forward='-Z',
        axis_up='Y'
    )
    print("SUCCESS: Converted successfully")
except Exception as e:
    print(f"ERROR: {str(e)}")
    sys.exit(1)
'@

$scriptPath = Join-Path $PSScriptRoot "blender_convert.py"
$blenderScript | Out-File -FilePath $scriptPath -Encoding UTF8

# Get all FBX files in Content directory
$contentPath = Join-Path $PSScriptRoot "Content"
$fbxFiles = Get-ChildItem -Path $contentPath -Filter "*.fbx" -Recurse

Write-Host "Found $($fbxFiles.Count) FBX files to convert" -ForegroundColor Green
Write-Host "This may take several minutes..." -ForegroundColor Yellow
Write-Host ""

$convertedCount = 0
$errorCount = 0

foreach ($fbxFile in $fbxFiles) {
    Write-Host "Converting: $($fbxFile.Name)" -ForegroundColor Cyan
    
    # Create backup
    $backupPath = $fbxFile.FullName + ".backup"
    if (-not (Test-Path $backupPath)) {
        Copy-Item $fbxFile.FullName $backupPath
        Write-Host "  Created backup: $($fbxFile.Name).backup" -ForegroundColor Gray
    }
    
    # Create temporary output path
    $tempOutput = $fbxFile.FullName + ".temp"
    
    # Convert using Blender
    $arguments = @(
        "--background"
        "--python", "`"$scriptPath`""
        "--", "`"$($fbxFile.FullName)`"", "`"$tempOutput`""
    )
    
    try {
        $output = & $BlenderPath $arguments 2>&1 | Out-String
        
        if ($output -match "SUCCESS:") {
            # Replace original with converted file
            Move-Item -Path $tempOutput -Destination $fbxFile.FullName -Force
            Write-Host "  ? Converted successfully" -ForegroundColor Green
            $convertedCount++
        } else {
            Write-Host "  ? Conversion failed" -ForegroundColor Red
            if (Test-Path $tempOutput) {
                Remove-Item $tempOutput
            }
            $errorCount++
            
            # Show error details
            if ($output -match "ERROR: (.+)") {
                Write-Host "    Error: $($matches[1])" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "  ? Error: $($_.Exception.Message)" -ForegroundColor Red
        if (Test-Path $tempOutput) {
            Remove-Item $tempOutput
        }
        $errorCount++
    }
    
    Write-Host ""
}

# Clean up script
Remove-Item $scriptPath -ErrorAction SilentlyContinue

Write-Host "==============================================" -ForegroundColor White
Write-Host "Conversion Summary:" -ForegroundColor White
Write-Host "  Total files: $($fbxFiles.Count)" -ForegroundColor White
Write-Host "  Converted: $convertedCount" -ForegroundColor Green
Write-Host "  Errors: $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
Write-Host "==============================================" -ForegroundColor White
Write-Host ""
Write-Host "Note: Original files have been backed up with .backup extension" -ForegroundColor Yellow
