# PowerShell script to analyze FBX files and check their versions
# This helps identify which files need conversion

$contentPath = Join-Path $PSScriptRoot "Content"
$fbxFiles = Get-ChildItem -Path $contentPath -Filter "*.fbx" -Recurse

Write-Host "Analyzing $($fbxFiles.Count) FBX files..." -ForegroundColor Green
Write-Host ""

$oldFormatFiles = @()
$newFormatFiles = @()
$unknownFiles = @()

foreach ($fbxFile in $fbxFiles) {
    # Read first few lines to detect version
    $firstLine = Get-Content $fbxFile.FullName -First 1 -ErrorAction SilentlyContinue
    
    $relativePath = $fbxFile.FullName.Replace($PSScriptRoot + "\", "")
    
    if ($firstLine -match "FBX 6\.[0-1]") {
        Write-Host "[OLD] $relativePath" -ForegroundColor Red
        $oldFormatFiles += $relativePath
    }
    elseif ($firstLine -match "FBX 7\." -or $firstLine -match "Kaydara FBX Binary") {
        # Check if it's a modern FBX (might still be old binary)
        $header = Get-Content $fbxFile.FullName -TotalCount 3 -ErrorAction SilentlyContinue
        $headerText = $header -join " "
        
        if ($headerText -match "FBXHeaderVersion.*?([0-9]{4,})" -and [int]$matches[1] -ge 7400) {
            Write-Host "[OK]  $relativePath" -ForegroundColor Green
            $newFormatFiles += $relativePath
        }
        else {
            Write-Host "[OLD] $relativePath (Old binary format)" -ForegroundColor Red
            $oldFormatFiles += $relativePath
        }
    }
    else {
        Write-Host "[???] $relativePath (Unknown format)" -ForegroundColor Yellow
        $unknownFiles += $relativePath
    }
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor White
Write-Host "Analysis Summary:" -ForegroundColor White
Write-Host "  Total files: $($fbxFiles.Count)" -ForegroundColor White
Write-Host "  Modern format (OK): $($newFormatFiles.Count)" -ForegroundColor Green
Write-Host "  Old format (NEED CONVERSION): $($oldFormatFiles.Count)" -ForegroundColor Red
Write-Host "  Unknown format: $($unknownFiles.Count)" -ForegroundColor Yellow
Write-Host "==============================================" -ForegroundColor White

if ($oldFormatFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "Files that need conversion:" -ForegroundColor Yellow
    foreach ($file in $oldFormatFiles) {
        Write-Host "  - $file" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Run .\convert-fbx-files.ps1 or .\convert-fbx-blender.ps1 to convert them." -ForegroundColor Cyan
}
