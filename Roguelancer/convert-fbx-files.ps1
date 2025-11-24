# PowerShell script to convert old FBX files to FBX 2013 format
# This script requires Autodesk FBX Converter to be installed
# Download from: https://www.autodesk.com/developer-network/platform-technologies/fbx-converter-archives

param(
    [string]$FbxConverterPath = "C:\Program Files\Autodesk\FBX\FBX Converter\2013.3\bin\FbxConverter.exe"
)

# Check if FBX Converter exists
if (-not (Test-Path $FbxConverterPath)) {
    Write-Host "ERROR: FBX Converter not found at: $FbxConverterPath" -ForegroundColor Red
    Write-Host "Please download and install Autodesk FBX Converter from:" -ForegroundColor Yellow
    Write-Host "https://www.autodesk.com/developer-network/platform-technologies/fbx-converter-archives" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or specify the correct path using:" -ForegroundColor Yellow
    Write-Host ".\convert-fbx-files.ps1 -FbxConverterPath 'C:\Path\To\FbxConverter.exe'" -ForegroundColor Yellow
    exit 1
}

# Get all FBX files in Content directory
$contentPath = Join-Path $PSScriptRoot "Content"
$fbxFiles = Get-ChildItem -Path $contentPath -Filter "*.fbx" -Recurse

Write-Host "Found $($fbxFiles.Count) FBX files to convert" -ForegroundColor Green
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
    
    # Convert to FBX 2013
    $outputPath = $fbxFile.FullName
    $arguments = @(
        "`"$($fbxFile.FullName)`""
        "`"$outputPath`""
        "/sffFBX"
        "/dffFBX"
        "/v"
        "/l"
        "/f201300"  # FBX 2013
    )
    
    try {
        $process = Start-Process -FilePath $FbxConverterPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
        
        if ($process.ExitCode -eq 0) {
            Write-Host "  ? Converted successfully" -ForegroundColor Green
            $convertedCount++
        } else {
            Write-Host "  ? Conversion failed (Exit code: $($process.ExitCode))" -ForegroundColor Red
            $errorCount++
        }
    } catch {
        Write-Host "  ? Error: $($_.Exception.Message)" -ForegroundColor Red
        $errorCount++
    }
    
    Write-Host ""
}

Write-Host "==============================================" -ForegroundColor White
Write-Host "Conversion Summary:" -ForegroundColor White
Write-Host "  Total files: $($fbxFiles.Count)" -ForegroundColor White
Write-Host "  Converted: $convertedCount" -ForegroundColor Green
Write-Host "  Errors: $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
Write-Host "==============================================" -ForegroundColor White
Write-Host ""
Write-Host "Note: Original files have been backed up with .backup extension" -ForegroundColor Yellow
