# PowerShell script to re-enable models in INI files after FBX conversion
# Run this AFTER you've converted all FBX files to modern format

param(
    [switch]$DryRun = $false  # Set to $true to see what would be changed without actually changing it
)

$iniFiles = @(
    "config\models.ini",
    "config\systems\new_york\ships.ini",
    "config\systems\new_york\planets.ini",
    "config\systems\texas\ships.ini",
    "config\systems\test_system\ships.ini",
    "config\systems\test_system2\ships.ini"
)

Write-Host "Re-enabling Models in INI Files" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
    Write-Host ""
}

$totalChanges = 0

foreach ($iniFile in $iniFiles) {
    $fullPath = Join-Path $PSScriptRoot $iniFile
    
    if (-not (Test-Path $fullPath)) {
        Write-Host "  [SKIP] $iniFile (not found)" -ForegroundColor Gray
        continue
    }
    
    Write-Host "Processing: $iniFile" -ForegroundColor White
    
    $content = Get-Content $fullPath -Raw
    $originalContent = $content
    
    # Count disabled sections
    $disabledCount = ([regex]::Matches($content, "# DISABLED")).Count
    
    if ($disabledCount -eq 0) {
        Write-Host "  [OK] No disabled models found" -ForegroundColor Green
        continue
    }
    
    Write-Host "  Found $disabledCount disabled section(s)" -ForegroundColor Yellow
    
    # Remove comment markers from disabled sections
    # Pattern: # DISABLED comment line followed by commented ini section
    $content = $content -replace "# DISABLED[^\r\n]*\r?\n#", ""
    $content = $content -replace "\r?\n#\[", "`r`n["
    $content = $content -replace "\r?\n#([a-z_]+)=", "`r`n`$1="
    
    # Change enabled=false to enabled=true
    $content = $content -replace "enabled=false", "enabled=true"
    
    if ($content -ne $originalContent) {
        $totalChanges++
        
        if (-not $DryRun) {
            # Create backup
            $backupPath = $fullPath + ".before-reenable"
            Copy-Item $fullPath $backupPath
            
            # Save changes
            $content | Set-Content $fullPath -NoNewline
            Write-Host "  [UPDATED] Models re-enabled (backup created)" -ForegroundColor Green
        } else {
            Write-Host "  [WOULD UPDATE] Models would be re-enabled" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  [OK] No changes needed" -ForegroundColor Green
    }
    
    Write-Host ""
}

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Summary:" -ForegroundColor White
Write-Host "  Files processed: $($iniFiles.Count)" -ForegroundColor White
Write-Host "  Files changed: $totalChanges" -ForegroundColor $(if ($totalChanges -gt 0) { "Yellow" } else { "Green" })

if ($DryRun) {
    Write-Host ""
    Write-Host "This was a DRY RUN - no files were actually modified" -ForegroundColor Yellow
    Write-Host "Run without -DryRun parameter to apply changes" -ForegroundColor Yellow
} elseif ($totalChanges -gt 0) {
    Write-Host ""
    Write-Host "? Models re-enabled successfully!" -ForegroundColor Green
    Write-Host "  Backups saved with .before-reenable extension" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor White
    Write-Host "  1. Verify FBX files are converted" -ForegroundColor Cyan
    Write-Host "  2. Restore Content.mgcb: .\restore-content.ps1" -ForegroundColor Cyan
    Write-Host "  3. Rebuild: dotnet build" -ForegroundColor Cyan
}
