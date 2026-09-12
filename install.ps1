<#
.SYNOPSIS
    Fast local/developer installation of AudioFlow (no admin required).

.DESCRIPTION
    Publishes AudioFlow to %LOCALAPPDATA%\Programs\AudioFlow, creates a Start
    Menu shortcut and optionally launches it. Use -Release to build the full
    release (installer + portable ZIP) instead.

.EXAMPLE
    .\install.ps1
    .\install.ps1 -Launch
    .\install.ps1 -Release
    .\install.ps1 -Clean
#>
[CmdletBinding()]
param(
    [switch] $Release,
    [switch] $Dev,
    [switch] $Launch,
    [switch] $Clean,
    [switch] $Force,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$root = $PSScriptRoot
Set-Location $root

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw 'AudioFlow runs on Windows only.'
}

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\AudioFlow'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\AudioFlow.lnk'

if ($Clean) {
    if (-not (Test-Path $installDir)) {
        Write-Host "Nothing to clean ($installDir)."
        return
    }

    if (-not $Force) {
        $answer = Read-Host "Remove local install at $installDir? [y/N]"
        if ($answer -notmatch '^[yY]') {
            Write-Host 'Cancelled.'
            return
        }
    }

    # Stop a running instance so its files can be removed.
    Get-Process -Name 'AudioFlow', 'AudioFlow.Updater' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800

    Remove-Item $installDir -Recurse -Force
    if (Test-Path $startMenu) { Remove-Item $startMenu -Force }
    Write-Host "Removed $installDir. Your rules in %APPDATA%\AudioFlow were kept." -ForegroundColor Green
    return
}

if ($Release) {
    Write-Host 'Building release artifacts (installer + portable ZIP)...' -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'tools\release.ps1')
    Write-Host ''
    Write-Host 'Release artifacts are in .\artifacts.' -ForegroundColor Green
    return
}

# Developer / local install.
Write-Host 'Installing AudioFlow (local, no admin)...' -ForegroundColor Cyan

& dotnet restore AudioFlow.sln
& dotnet build AudioFlow.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

& dotnet publish src/AudioFlow.UI -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $installDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

& dotnet publish src/AudioFlow.Updater -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $installDir
if ($LASTEXITCODE -ne 0) { throw 'Updater publish failed.' }

$exe = Join-Path $installDir 'AudioFlow.exe'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenu)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = 'AudioFlow'
$shortcut.Save()

Write-Host ''
Write-Host "Installed to: $installDir" -ForegroundColor Green
Write-Host "Start Menu  : $startMenu" -ForegroundColor Green
Write-Host 'User data   : %APPDATA%\AudioFlow (preserved across updates)' -ForegroundColor Green

if ($Launch) {
    Start-Process -FilePath $exe
    Write-Host 'Launched AudioFlow.'
}
