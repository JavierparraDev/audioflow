<#
.SYNOPSIS
    Publishes a self-contained-free (framework-dependent) win-x64 build.
    Output: .\artifacts\audioflow
#>
param(
    [string] $Runtime = 'win-x64',
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

dotnet publish src/AudioFlow.Console -c $Configuration -r $Runtime --self-contained false -o artifacts/audioflow
Write-Host "Published to artifacts/audioflow"
