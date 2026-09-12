<#
.SYNOPSIS
    Safe developer update for an existing AudioFlow Git clone.

.DESCRIPTION
    Fetches origin, fast-forwards when possible (never destroying local
    changes), then restores, builds and optionally tests/publishes.

.EXAMPLE
    .\tools\dev-update.ps1
    .\tools\dev-update.ps1 -Build
    .\tools\dev-update.ps1 -Test
    .\tools\dev-update.ps1 -Publish
    .\tools\dev-update.ps1 -All
#>
[CmdletBinding()]
param(
    [switch] $Build,
    [switch] $Test,
    [switch] $Publish,
    [switch] $All,
    [string] $Remote = 'origin',
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Get-VersionFromProps([string] $content) {
    $match = [regex]::Match($content, '<Version>([^<]+)</Version>')
    if ($match.Success) { return $match.Groups[1].Value }
    return '0.0.0'
}

function Write-Step([string] $text, [string] $status = 'OK') {
    Write-Host ("  [$status] $text") -ForegroundColor Green
}

Write-Host 'AudioFlow Developer Update' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path (Join-Path $root '.git'))) {
    throw 'This directory is not a Git repository.'
}

$branch = (& git rev-parse --abbrev-ref HEAD).Trim()
Write-Host "Current branch: $branch"
Write-Host "Fetching $Remote..."
& git fetch $Remote

$localVersion = Get-VersionFromProps (Get-Content (Join-Path $root 'Directory.Build.props') -Raw)
$remoteVersion = $localVersion
try {
    $remoteProps = & git show "$Remote/$branch`:Directory.Build.props" 2>$null
    if ($remoteProps) { $remoteVersion = Get-VersionFromProps ($remoteProps -join "`n") }
} catch {
    Write-Host "Could not read remote version for branch '$branch'." -ForegroundColor Yellow
}

Write-Host "Local version : $localVersion"
Write-Host "Remote version: $remoteVersion"
Write-Host ''

$dirty = & git status --porcelain
$runUpdate = $true
if ($Build -or $Test -or $Publish) { $runUpdate = $false }
if ($All) { $runUpdate = $true }

if ($dirty) {
    Write-Host 'Uncommitted changes detected. Skipping the Git update to protect your work.' -ForegroundColor Yellow
    Write-Host 'Commit or stash them, then re-run.' -ForegroundColor Yellow
    Write-Host ''
} elseif ($runUpdate) {
    Write-Host 'Updating...'
    & git merge --ff-only "$Remote/$branch"
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Fast-forward not possible. A manual merge or rebase is required.' -ForegroundColor Yellow
    } else {
        Write-Step 'Git updated'
    }
}

Write-Host ''
Write-Host 'Restoring...'
& dotnet restore AudioFlow.sln
Write-Step 'Restore'

Write-Host 'Building...'
& dotnet build AudioFlow.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Step 'Build'

$doTest = $Test -or $All -or (-not ($Build -or $Publish))
if ($doTest) {
    Write-Host 'Running unit tests...'
    & dotnet test tests/AudioFlow.Tests -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }
    Write-Step 'Unit tests'
}

$doPublish = $Publish -or $All
if ($doPublish) {
    Write-Host 'Publishing (win-x64, self-contained)...'
    & dotnet publish src/AudioFlow.UI -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o artifacts/dev
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Write-Step 'Publish (artifacts/dev/AudioFlow.exe)'
}

Write-Host ''
Write-Host 'AudioFlow updated successfully.' -ForegroundColor Green
