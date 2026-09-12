<#
.SYNOPSIS
    Emergency restore of AudioFlow's temporary Windows audio changes.

.DESCRIPTION
    Restores the persisted output endpoints that AudioFlow changed. Works
    independently of the AudioFlow UI/pipeline by invoking the AudioFlow CLI's
    'restore' command (which reads the session snapshot and only touches
    applications AudioFlow modified).

.EXAMPLE
    .\tools\emergency-restore.ps1
    .\tools\emergency-restore.ps1 -AudioFlowPath 'C:\Program Files\AudioFlow\audioflow.exe'
#>
param(
    [string] $AudioFlowPath
)

$ErrorActionPreference = 'Continue'

Write-Host 'AudioFlow Emergency Restore' -ForegroundColor Cyan
Write-Host ''

$sessionFile = Join-Path $env:APPDATA 'AudioFlow\session\session.json'

if (-not (Test-Path $sessionFile)) {
    Write-Host 'No AudioFlow session marker found. Windows audio is untouched.' -ForegroundColor Green
    exit 0
}

Write-Host "Session marker: $sessionFile"

$candidates = @()
if ($AudioFlowPath) { $candidates += $AudioFlowPath }
$candidates += (Join-Path $PSScriptRoot '..\artifacts\stage\app\audioflow.exe')
$candidates += (Join-Path $PSScriptRoot '..\src\AudioFlow.Console\bin\Release\net8.0-windows\win-x64\audioflow.exe')
$candidates += (Join-Path $env:LOCALAPPDATA 'Programs\AudioFlow\audioflow.exe')
$candidates += (Join-Path $env:ProgramFiles 'AudioFlow\audioflow.exe')
if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'AudioFlow\audioflow.exe') }

$exe = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $exe) {
    $command = Get-Command audioflow.exe -ErrorAction SilentlyContinue
    if ($command) { $exe = $command.Source }
}

if (-not $exe) {
    Write-Host 'AudioFlow CLI not found; cannot restore automatically.' -ForegroundColor Yellow
    Write-Host 'Install/repair AudioFlow, then run: audioflow restore'
    Write-Host 'Or reset the affected apps in Settings > System > Sound > Volume mixer > App volume and device preferences.'
    exit 2
}

Write-Host "Using: $exe"
Write-Host ''
& $exe restore
exit $LASTEXITCODE
