# PowerShell script to restore Content.mgcb from backup
# Use this after converting FBX files to modern format

$mgcbFile = Join-Path $PSScriptRoot "Content\Content.mgcb"
$backupFile = $mgcbFile + ".backup"

Write-Host "Restoring Content.mgcb from backup..." -ForegroundColor Cyan

if (-not (Test-Path $backupFile)) {
    Write-Host "ERROR: Backup file not found at: $backupFile" -ForegroundColor Red
    Write-Host "Cannot restore without backup file." -ForegroundColor Red
    exit 1
}

# Create a backup of current state (just in case)
$tempBackup = $mgcbFile + ".temp"
if (Test-Path $mgcbFile) {
    Copy-Item $mgcbFile $tempBackup
    Write-Host "Created temporary backup of current state" -ForegroundColor Gray
}

# Restore from backup
Copy-Item $backupFile $mgcbFile -Force
Write-Host "? Successfully restored Content.mgcb from backup" -ForegroundColor Green

# Clean up temp backup
if (Test-Path $tempBackup) {
    Remove-Item $tempBackup
}

Write-Host "`nNext step: Run 'dotnet build' to rebuild with all models" -ForegroundColor Cyan
