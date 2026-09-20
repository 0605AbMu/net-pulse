<#
.SYNOPSIS
    NetPulse Release Build Script
.DESCRIPTION
    Compiles NetPulse into a self-contained, single-file Windows executable (win-x64)
    that requires no prerequisites (.NET runtime is bundled inside).
#>

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot "dist"
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  NetPulse Release Build & Packaging" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 0. Determine Application Version
$projectPath = "$PSScriptRoot\NetPulse\NetPulse.csproj"
[xml]$proj = Get-Content $projectPath
$appVer = $Version
if ([string]::IsNullOrWhiteSpace($appVer)) {
    $appVer = $proj.Project.PropertyGroup.Version
}
if ([string]::IsNullOrWhiteSpace($appVer)) { $appVer = "1.1.0" }
$appVer = $appVer.Trim().TrimStart('v')

$numVer = ($appVer -replace '^([0-9]+(?:\.[0-9]+)*).*$', '$1')
if ($numVer -notmatch '^[0-9]+(\.[0-9]+){1,3}$') {
    $numVer = "1.0.0"
}

Write-Host "Application Version: $appVer (Assembly: $numVer)" -ForegroundColor Green

# 1. Ensure output directory exists and is clean
if (Test-Path $OutputDir) {
    Write-Host "[1/4] Cleaning previous output: $OutputDir" -ForegroundColor Yellow
    Remove-Item -Path "$OutputDir\*" -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "[1/4] Creating output directory: $OutputDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# 2. Kill any lingering NetPulse process that could lock binaries
Stop-Process -Name "NetPulse" -Force -ErrorAction SilentlyContinue

# 3. Publish self-contained single-file executable
Write-Host "[2/4] Publishing self-contained single-file executable ($Runtime)..." -ForegroundColor Yellow

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:Version=$appVer",
    "-p:AssemblyVersion=$numVer",
    "-p:FileVersion=$numVer",
    "-p:InformationalVersion=$appVer"
)

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# 4. Copy published executable to dist folder
Write-Host "[3/4] Copying artifacts to $OutputDir..." -ForegroundColor Yellow
$publishedExe = "$PSScriptRoot\NetPulse\bin\$Configuration\net10.0-windows\$Runtime\publish\NetPulse.exe"

if (-not (Test-Path $publishedExe)) {
    Write-Error "Published executable not found at: $publishedExe"
    exit 1
}

$destExe = "$OutputDir\NetPulse-v$appVer-$Runtime.exe"
Copy-Item -Path $publishedExe -Destination $destExe -Force
Copy-Item -Path $publishedExe -Destination "$OutputDir\NetPulse.exe" -Force

# 5. Summary
$fileInfo = Get-Item $destExe
$sizeMb = [Math]::Round($fileInfo.Length / 1MB, 2)

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "  BUILD SUCCEEDED!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Output: $destExe" -ForegroundColor White
Write-Host "Size:   $sizeMb MB" -ForegroundColor White
Write-Host "Type:   Self-Contained Single-File (Ready to ship!)" -ForegroundColor White
Write-Host ""
