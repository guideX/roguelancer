# PowerShell script to comment out problematic FBX files in Content.mgcb
# This allows the build to succeed while you convert the old FBX files

$mgcbFile = Join-Path $PSScriptRoot "Content\Content.mgcb"

# List of problematic FBX files (old format)
$problematicFiles = @(
    "BASES/CO_BASE_ICE_LARGE02/CO_BASE_ICE_LARGE02.fbx",
    "BASES/CO_BASE_ROCK_LARGE01/CO_BASE_ROCK_LARGE01.fbx",
    "BASES/CO_BASE_ROCK_LARGE02/CO_BASE_ROCK_LARGE02-2.fbx",
    "BASES/CO_BASE_ROCK_LARGE02/CO_BASE_ROCK_LARGE02.fbx",
    "BASES/PLAYERBASE_01/PLAYERBASE_01.fbx",
    "BASES/TRACK_RING/TRACK_RING.fbx",
    "ASTEROIDS/BERYL/BERYL.fbx",
    "SHIPS/ASD52/ASD52.fbx",
    "SHIPS/NOMAD_BATTLESHIP/NOMAD_BATTLESHIP.fbx",
    "SHIPS/RH_GUNSHIP/RH_GUNSHIP.fbx",
    "SHIPS/BW_ELITE/BW_ELITE.fbx",
    "SHIPS/BORDER_WORLD/BW_ELITE/BW_ELITE.fbx",
    "SHIPS/CSV/CSV.fbx",
    "SHIPS/COCKPIT1.fbx",
    "PLANETS/ALIEN_PLANET/ALIEN_PLANET.fbx",
    "PLANETS/ALIEN_PLANET2/ALIEN_PLANET2.fbx",
    "Earth.fbx",
    "Ship.fbx",
    "docking_ring.fbx",
    "bullet.FBX"
)

Write-Host "Reading Content.mgcb..." -ForegroundColor Cyan

if (-not (Test-Path $mgcbFile)) {
    Write-Host "ERROR: Content.mgcb not found at: $mgcbFile" -ForegroundColor Red
    exit 1
}

$content = Get-Content $mgcbFile -Raw

# Create backup
$backupFile = $mgcbFile + ".backup"
if (-not (Test-Path $backupFile)) {
    Copy-Item $mgcbFile $backupFile
    Write-Host "Created backup: $($backupFile)" -ForegroundColor Green
}

$modifiedContent = $content
$commentedCount = 0

foreach ($file in $problematicFiles) {
    # Find the #begin block for this file
    $pattern = "(?s)(#begin $([regex]::Escape($file)).*?(?=#begin|$))"
    
    if ($modifiedContent -match $pattern) {
        $block = $matches[1]
        
        # Check if already commented
        if ($block -notmatch "^#\s*DISABLED") {
            # Comment out the entire block
            $commentedBlock = "# DISABLED - Old FBX format`r`n" + ($block -split "`r?`n" | ForEach-Object { "#$_" }) -join "`r`n"
            $modifiedContent = $modifiedContent -replace [regex]::Escape($block), $commentedBlock
            $commentedCount++
            Write-Host "  Commented out: $file" -ForegroundColor Yellow
        }
    }
}

if ($commentedCount -gt 0) {
    $modifiedContent | Set-Content $mgcbFile -NoNewline
    Write-Host "`n? Successfully commented out $commentedCount problematic files" -ForegroundColor Green
    Write-Host "You can now build the project!" -ForegroundColor Green
    Write-Host "`nTo restore: Copy $($backupFile) back to Content.mgcb" -ForegroundColor Gray
} else {
    Write-Host "`nNo files needed to be commented out (already disabled or not found)" -ForegroundColor Yellow
}

Write-Host "`nNext steps:" -ForegroundColor White
Write-Host "1. Run: dotnet build" -ForegroundColor Cyan
Write-Host "2. Convert FBX files using convert-fbx-blender.ps1 or convert-fbx-files.ps1" -ForegroundColor Cyan
Write-Host "3. Restore the Content.mgcb from backup" -ForegroundColor Cyan
Write-Host "4. Rebuild with all models" -ForegroundColor Cyan
