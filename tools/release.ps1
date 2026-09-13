<#
.SYNOPSIS
    Builds a complete AudioFlow release: portable ZIP, Inno Setup installer,
    SHA256SUMS.txt and release.json.

.EXAMPLE
    .\tools\release.ps1
    .\tools\release.ps1 -Version 0.3.0 -SkipTests
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $Runtime = 'win-x64',
    [string] $Configuration = 'Release',
    [switch] $SkipTests,
    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $Version) {
    $props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
    if ($props -match '<Version>([^<]+)</Version>') {
        $Version = $Matches[1]
    } else {
        throw 'Could not read <Version> from Directory.Build.props.'
    }
}

Write-Host "AudioFlow release $Version ($Runtime)" -ForegroundColor Cyan

$artifacts = Join-Path $root 'artifacts'
Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
$stageApp = Join-Path $artifacts 'stage\app'
New-Item -ItemType Directory -Path $stageApp -Force | Out-Null

Write-Host 'Restoring and building...' -ForegroundColor Cyan
dotnet restore AudioFlow.sln
dotnet build AudioFlow.sln -c $Configuration --no-restore

if (-not $SkipTests) {
    Write-Host 'Running unit tests...' -ForegroundColor Cyan
    dotnet test tests/AudioFlow.Tests -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed. Release aborted.' }
}

Write-Host 'Publishing UI (self-contained single file)...' -ForegroundColor Cyan
dotnet publish src/AudioFlow.UI -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o $stageApp

Write-Host 'Publishing updater...' -ForegroundColor Cyan
dotnet publish src/AudioFlow.Updater -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $stageApp

# Portable ZIP (adds the portable marker so user data stays next to the app).
Write-Host 'Building portable ZIP...' -ForegroundColor Cyan
$portableDir = Join-Path $artifacts 'stage\portable'
New-Item -ItemType Directory -Path $portableDir -Force | Out-Null
Copy-Item (Join-Path $stageApp '*') $portableDir -Recurse -Force
Set-Content -Path (Join-Path $portableDir 'portable.txt') -Value 'AudioFlow portable mode. User data is stored in .\data.' -Encoding ascii

$zipName = "AudioFlow-v$Version-$Runtime.zip"
$zipPath = Join-Path $artifacts $zipName
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zipPath -Force

# Installer.
$installerPath = $null
if (-not $SkipInstaller) {
    Write-Host 'Building installer (Inno Setup)...' -ForegroundColor Cyan
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) { throw 'ISCC.exe (Inno Setup 6) was not found.' }

    & $iscc "/DMyAppVersion=$Version" "/DSourceDir=$stageApp" (Join-Path $root 'installer\AudioFlow.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE." }

    $installerPath = Join-Path $artifacts "AudioFlow-Setup-v$Version.exe"
}

# Checksums.
Write-Host 'Generating checksums...' -ForegroundColor Cyan
$sums = @()
if ($installerPath -and (Test-Path $installerPath)) {
    $sums += ((Get-FileHash $installerPath -Algorithm SHA256).Hash + '  ' + (Split-Path $installerPath -Leaf))
}
$sums += ((Get-FileHash $zipPath -Algorithm SHA256).Hash + '  ' + $zipName)
$sums | Set-Content -Path (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii

# release.json.
$installerHash = if ($installerPath -and (Test-Path $installerPath)) { (Get-FileHash $installerPath -Algorithm SHA256).Hash } else { $null }
$release = [ordered]@{
    version     = $Version
    channel     = 'stable'
    publishedAt = (Get-Date).ToUniversalTime().ToString('o')
    installer   = if ($installerPath) { Split-Path $installerPath -Leaf } else { $null }
    portable    = $zipName
    sha256      = if ($installerHash) { $installerHash } else { (Get-FileHash $zipPath -Algorithm SHA256).Hash }
}
$release | ConvertTo-Json | Set-Content -Path (Join-Path $artifacts 'release.json') -Encoding utf8

# Release notes.
$notes = @"
# AudioFlow $Version

## What's new
- See the commit history for this release.

## Bug fixes
- See the commit history for this release.

## Known limitations
- Per-application routing is **PARTIAL**: it sets the persisted output endpoint and is applied when the application (re)initializes its audio stream. It does not move a live stream.
- Audio Lock is partial protection; apps using their own endpoint or exclusive mode may bypass it.
- Process Loopback is **experimental** (activation verified, re-render not implemented).

## Installation
1. Download ``AudioFlow-Setup-v$Version.exe`` and run it.
2. Or use the portable ``AudioFlow-v$Version-$Runtime.zip``.

## Upgrade notes
- AudioFlow is session-only: it leaves no rules, logs or audio registry changes
  behind when it is not running. Legacy files from earlier versions are cleaned
  automatically on first launch.
- Verify downloads against ``SHA256SUMS.txt``.
"@
$notes | Set-Content -Path (Join-Path $artifacts 'RELEASE-NOTES.md') -Encoding utf8

Write-Host ''
Write-Host "Release $Version ready in $artifacts" -ForegroundColor Green
Get-ChildItem $artifacts -File | Select-Object Name, @{N='MB';E={[math]::Round($_.Length/1MB,2)}} | Format-Table
