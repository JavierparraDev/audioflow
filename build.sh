#!/usr/bin/env bash
#
# Builds AudioFlow. Works on Windows, Linux and macOS.
# On Linux/macOS the Windows-targeted projects are cross-compiled using
# EnableWindowsTargeting (they can be built but not executed there).
#
set -euo pipefail

cd "$(dirname "$0")"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

DOTNET="${DOTNET:-dotnet}"
if ! command -v "$DOTNET" >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
    DOTNET="$HOME/.dotnet/dotnet"
fi

"$DOTNET" build AudioFlow.sln -c Release "$@"
