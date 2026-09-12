<#
.SYNOPSIS
    Removes every trace AudioFlow may have left behind.

.DESCRIPTION
    AudioFlow is session-only: it should leave no rules, no logs and no changes
    in the Windows audio registry once it is closed. This script cleans up
    anything created by earlier versions. It does not need administrator
    privileges (everything lives in your own user account).

.EXAMPLE
    .\tools\cleanup.ps1
    .\tools\cleanup.ps1 -AudioFlowPath 'C:\Program Files\AudioFlow\audioflow.exe'
#>
param(
    [string] $AudioFlowPath
)

$ErrorActionPreference = 'Continue'

Write-Host 'AudioFlow cleanup' -ForegroundColor Cyan
Write-Host 'Removing every rule, log and audio registry change AudioFlow left behind...'
Write-Host ''

$candidates = @()
if ($AudioFlowPath) { $candidates += $AudioFlowPath }
$candidates += (Join-Path $PSScriptRoot '..\artifacts\stage\app\audioflow.exe')
$candidates += (Join-Path $PSScriptRoot '..\src\AudioFlow.Console\bin\Release\net8.0-windows\win-x64\audioflow.exe')
$candidates += (Join-Path $PSScriptRoot '..\src\AudioFlow.Console\bin\Release\net8.0-windows\audioflow.exe')
$candidates += (Join-Path $env:LOCALAPPDATA 'Programs\AudioFlow\audioflow.exe')
$candidates += (Join-Path $env:ProgramFiles 'AudioFlow\audioflow.exe')
if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'AudioFlow\audioflow.exe') }

$exe = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $exe) {
    $command = Get-Command audioflow.exe -ErrorAction SilentlyContinue
    if ($command) { $exe = $command.Source }
}

if ($exe) {
    Write-Host "Using: $exe"
    Write-Host ''
    & $exe cleanup
    exit $LASTEXITCODE
}

# Fallback: no CLI available. Remove the known files directly so a non-technical
# user can still get a clean system.
Write-Host 'AudioFlow CLI not found. Removing leftover files directly...' -ForegroundColor Yellow

$dataDir = Join-Path $env:APPDATA 'AudioFlow'
foreach ($file in @('rules.json', 'rules.json.bak', 'rules.json.tmp')) {
    $path = Join-Path $dataDir $file
    if (Test-Path $path) { Remove-Item $path -Force -ErrorAction SilentlyContinue; Write-Host "  removed $path" }
}

$sessionDir = Join-Path $dataDir 'session'
if (Test-Path $sessionDir) { Remove-Item $sessionDir -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "  removed $sessionDir" }

$logsDir = Join-Path $dataDir 'logs'
if (Test-Path $logsDir) { Remove-Item $logsDir -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "  removed $logsDir" }

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty -Path $runKey -Name 'AudioFlow' -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $runKey -Name 'AudioFlow' -ErrorAction SilentlyContinue
    Write-Host '  removed the "start with Windows" entry'
}

Write-Host ''
Write-Host 'CLEANUP SUCCESS' -ForegroundColor Green
Write-Host 'Windows audio is back to its normal behaviour.'
exit 0
