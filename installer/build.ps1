# NetPulse Local Build Script for Setup & Update package
param (
    [string]$Version = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$PublishDir = Join-Path $RootDir "publish"
$OutputDir = Join-Path $ScriptDir "output"

# Auto-detect version from NetPulse.csproj if not passed
if ([string]::IsNullOrWhiteSpace($Version)) {
    $CsprojPath = Join-Path $RootDir "NetPulse\NetPulse.csproj"
    if (Test-Path $CsprojPath) {
        [xml]$proj = Get-Content $CsprojPath
        $Version = $proj.Project.PropertyGroup.Version
    }
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "1.0.0"
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Building NetPulse v$Version ($Configuration)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Clean previous build folders
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# 2. Publish .NET application
Write-Host "`n[1/3] Publishing .NET application (win-x64 self-contained)..." -ForegroundColor Yellow
dotnet publish "$RootDir\NetPulse\NetPulse.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed!"
    exit 1
}

# 3. Locate Inno Setup Compiler (ISCC.exe)
Write-Host "`n[2/3] Locating Inno Setup Compiler..." -ForegroundColor Yellow
$IsccPath = $null
$PossiblePaths = @(
    "ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)

foreach ($path in $PossiblePaths) {
    if (Get-Command $path -ErrorAction SilentlyContinue) {
        $IsccPath = (Get-Command $path).Source
        break
    } elseif (Test-Path $path) {
        $IsccPath = $path
        break
    }
}

if (-not $IsccPath) {
    Write-Warning "Inno Setup (ISCC.exe) was not found on your system."
    Write-Host "You can install it with: choco install innosetup -y" -ForegroundColor White
    Write-Host "Or download it from: https://jrsoftware.org/isdl.php" -ForegroundColor White
    Write-Host "The compiled app is ready in: $PublishDir" -ForegroundColor Green
    exit 0
}

Write-Host "Found ISCC at: $IsccPath" -ForegroundColor Green

# 4. Compile Inno Setup Script
Write-Host "`n[3/3] Compiling installer with Inno Setup..." -ForegroundColor Yellow
$IssFile = Join-Path $ScriptDir "NetPulse.iss"

& "$IsccPath" `
    "/DMyAppVersion=$Version" `
    "/DSourceDir=$PublishDir" `
    "/DOutputDir=$OutputDir" `
    "$IssFile"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed!"
    exit 1
}

$VersionedExe = Join-Path $OutputDir "NetPulse-Setup-v$Version.exe"
$GenericExe = Join-Path $OutputDir "NetPulse-Setup.exe"

$FoundExe = $null
if (Test-Path $VersionedExe) {
    $FoundExe = $VersionedExe
    Copy-Item $VersionedExe $GenericExe -Force
} elseif (Test-Path $GenericExe) {
    $FoundExe = $GenericExe
}

if ($FoundExe) {
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host " SUCCESS! Installer ready at:" -ForegroundColor Green
    Write-Host " $FoundExe" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Error "Installer was not found in $OutputDir!"
    exit 1
}
