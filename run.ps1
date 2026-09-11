<#
.SYNOPSIS
    Runs the AudioFlow console application on Windows.

.EXAMPLE
    .\run.ps1 devices
    .\run.ps1 sessions
#>
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

dotnet run --project src/AudioFlow.Console -c Release -- @Arguments
