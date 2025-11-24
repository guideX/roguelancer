# PowerShell script to fix texture compression issues in Content.mgcb
# Changes problematic textures from Compressed to Color format

$mgcbFile = Join-Path $PSScriptRoot "Content\Content.mgcb"

Write-Host "Fixing texture compression issues..." -ForegroundColor Cyan

if (-not (Test-Path $mgcbFile)) {
    Write-Host "ERROR: Content.mgcb not found" -ForegroundColor Red
    exit 1
}

$content = Get-Content $mgcbFile -Raw

# Create backup if not already backed up
$backupFile = $mgcbFile + ".backup"
if (-not (Test-Path $backupFile)) {
    Copy-Item $mgcbFile $backupFile
    Write-Host "Created backup" -ForegroundColor Green
}

# Fix texture compression for planet textures that aren't power-of-2
$problematicTextures = @(
    "PLANETS/ALIEN_PLANET/Render1.png",
    "PLANETS/ALIEN_PLANET2/Render1.png",
    "PLANETS/ALIEN_PLANET2/Render2.png"
)

$fixedCount = 0

foreach ($texture in $problematicTextures) {
    # Find the texture block and change TextureFormat from Compressed to Color
    $pattern = "(#begin $([regex]::Escape($texture))[\s\S]*?)/processorParam:TextureFormat=Compressed"
    
    if ($content -match $pattern) {
        $content = $content -replace $pattern, "`$1/processorParam:TextureFormat=Color"
        $fixedCount++
        Write-Host "  Fixed: $texture" -ForegroundColor Yellow
    }
}

if ($fixedCount -gt 0) {
    $content | Set-Content $mgcbFile -NoNewline
    Write-Host "`n? Fixed $fixedCount texture compression issues" -ForegroundColor Green
    Write-Host "Run 'dotnet build' to rebuild" -ForegroundColor Cyan
} else {
    Write-Host "`nNo texture compression issues found (already fixed)" -ForegroundColor Yellow
}
