<#
.SYNOPSIS
    Publishes AudioFlow (UI + Console) as framework-dependent win-x64 builds.
    Output: .\publish\ui and .\publish\cli
#>
param(
    [string] $Runtime = 'win-x64',
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Write-Host "Publishing AudioFlow.UI..."
dotnet publish src/AudioFlow.UI -c $Configuration -r $Runtime --self-contained false -o publish/ui

Write-Host "Publishing AudioFlow.Console..."
dotnet publish src/AudioFlow.Console -c $Configuration -r $Runtime --self-contained false -o publish/cli

Write-Host ""
Write-Host "Done:"
Write-Host "  UI : publish/ui/AudioFlow.exe"
Write-Host "  CLI: publish/cli/audioflow.exe"
