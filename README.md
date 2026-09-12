<div align="center">

<img src="docs/assets/banner.svg" alt="AudioFlow — per-application audio routing for Windows" width="100%" />

<br/>

**Decide where each application plays its audio on Windows.**

[![build](https://img.shields.io/github/actions/workflow/status/JavierparraDev/audioflow/build.yml?branch=main&label=build&logo=github)](https://github.com/JavierparraDev/audioflow/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/JavierparraDev/audioflow?include_prereleases&label=release&logo=github)](https://github.com/JavierparraDev/audioflow/releases)
[![license](https://img.shields.io/github/license/JavierparraDev/audioflow?label=license)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?logo=windows)](https://www.microsoft.com/windows)

[English](README.md) · [Español](README.es.md) · [Releases](https://github.com/JavierparraDev/audioflow/releases) · [Roadmap](docs/ROADMAP.md)

</div>

---

## What is AudioFlow?

AudioFlow lets you decide **where each application plays its audio**. The classic
setup: keep music on your **speakers** while games, voice chat and video stay on
your **headphones** — at the same time, without unplugging anything.

```
Spotify            ->  Speakers
Discord            ->  Headphones
Chrome / YouTube   ->  Headphones
Games              ->  Headphones
Everything else    ->  Headphones (default rule)
```

AudioFlow is a native **Windows 10/11** desktop app (WPF, dark UI) with a CLI for
diagnostics. Routing is **session-scoped**: when AudioFlow closes, Windows goes
back to its normal behavior and nothing is left behind.

## Features

| | |
|---|---|
| 🎧 **Device detection** | Speakers, headphones, Bluetooth, monitors and virtual devices. |
| 🔎 **Live sessions** | Detects applications producing audio in real time (WASAPI). |
| 🎯 **Stable identity** | Executable name + path hash + AUMID for Store apps. |
| 🧩 **Per-app rules** | Application rules plus a global default output. |
| 🔒 **Audio Lock** | Partial protection to keep non-authorized apps off a device. |
| 🌍 **Localized UI** | Modern dark WPF interface in **English and Spanish**. |
| ✅ **Verification** | Objective routing checks that measure the real level per endpoint. |
| 🧪 **Process Loopback** | Experimental module on the official Windows API, isolated from the MVP. |

## Requirements

- Windows 10 (build 20348+) or Windows 11
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- .NET 8 Desktop Runtime (to run the published app)

## Installation

**Installer (recommended):** download `AudioFlow-Setup-v<version>.exe` from
[Releases](https://github.com/JavierparraDev/audioflow/releases) and run it.
It installs to Program Files, creates a Start Menu shortcut and registers an
uninstaller. Your configuration in `%APPDATA%\AudioFlow` is never touched.

**Portable:** download `AudioFlow-v<version>-win-x64.zip`, extract it and run
`AudioFlow.exe`. Data is stored in a `data\` folder next to the app.

**From source:**

```powershell
git clone https://github.com/JavierparraDev/audioflow.git
cd audioflow
./install.ps1 -Launch      # local install, no admin
./install.ps1 -Release     # build installer + portable ZIP (needs Inno Setup)
```

See [docs/INSTALLATION.md](docs/INSTALLATION.md).

## Quick start

1. Run `publish/ui/AudioFlow.exe`.
2. Pick the **Speakers** and **Headphones** devices.
3. In **Applications**, press **+ Speakers** on the apps that must use the speakers (e.g. Spotify).
4. Optionally enable **Audio Lock** so other apps cannot use the speakers.
5. Press **Start** to apply the rules continuously.

CLI (diagnostics):

```powershell
audioflow devices        # list output endpoints
audioflow sessions       # list active audio sessions
audioflow plan           # show the resolved routing
audioflow apply          # apply the rules to active sessions
audioflow verify "Speakers"   # measure the real audio level per endpoint
audioflow loopback-probe <pid>  # experimental process loopback probe
audioflow version             # show the version (e.g. AudioFlow 0.2.0)
audioflow update --check      # check GitHub Releases for updates
audioflow routing             # routing backends + virtual endpoint status
```

## Table of contents

- [What is AudioFlow?](#what-is-audioflow)
- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Updates](#updates)
- [Session & safety](#session--safety)
- [Routing backends](#routing-backends)
- [Architecture](#architecture)
- [Routing limitations (read this)](#routing-limitations-read-this)
- [Audio Lock limitations](#audio-lock-limitations)
- [Process Loopback (experimental)](#process-loopback-experimental)
- [Testing](#testing)
- [Development](#development)
- [Contributing & forking](#contributing--forking)
- [License](#license)
- [Privacy](#privacy)

## Updates

AudioFlow checks GitHub Releases once at startup (configurable) and shows the
result in **Settings > Updates**. If a new version exists, **Update now**
downloads it, verifies its SHA-256, and hands it to the standalone
`AudioFlow.Updater.exe`, which backs up your settings, applies the update and
restarts AudioFlow. Updates never run while the app is running, and AudioFlow
keeps working normally when offline.

See [docs/UPDATES.md](docs/UPDATES.md).

## Session & safety

Routing is **session-scoped and fully ephemeral**. While AudioFlow is open, rules
live in memory and may be active; when AudioFlow closes, Windows returns to its
normal audio behavior and **nothing remains**: no rules file, no logs and no
audio registry change. AudioFlow snapshots the original state (including the
per-application audio registry) before changing anything and restores it exactly
on exit — even for applications that are no longer running. If AudioFlow crashes,
the next launch restores Windows audio **before** doing anything else and never
re-activates routing automatically.

```powershell
audioflow session   # ACTIVE | INACTIVE | STALE
audioflow restore   # RESTORE SUCCESS | RESTORE FAILED
audioflow cleanup   # remove every rule, log and registry change left behind
.\tools\emergency-restore.ps1   # restore without the UI
.\tools\cleanup.ps1             # clean up without the UI
```

See [docs/SESSION-RULES.md](docs/SESSION-RULES.md) and
[docs/CRASH-RECOVERY.md](docs/CRASH-RECOVERY.md).

A small independent **Session Guardian** (`AudioFlow.SessionGuardian.exe`)
monitors AudioFlow while it runs and restores Windows audio within seconds if
AudioFlow crashes, without waiting for a restart. Device disconnects are handled
by restoring only the affected applications. See
[docs/SESSION-GUARDIAN.md](docs/SESSION-GUARDIAN.md) and
[docs/DEVICE-RECOVERY.md](docs/DEVICE-RECOVERY.md).

```powershell
audioflow diagnostics   # session / guardian / devices / routes
audioflow guardian status
```

## Routing backends

AudioFlow routes through a swappable backend layer (`AudioFlow.Routing`):

- **Policy Endpoint** — sets the application's persisted output endpoint. No
  duplication; applies when the app restarts its audio stream. Available today.
- **Virtual Endpoint** — captures a virtual endpoint's loopback and renders it to
  the target. Live and duplication-free, but requires a virtual audio endpoint
  to be installed; otherwise it reports `BLOCKED`.

See [docs/ROUTING-BACKENDS.md](docs/ROUTING-BACKENDS.md) and
[docs/VIRTUAL-ENDPOINT-INTEGRATION.md](docs/VIRTUAL-ENDPOINT-INTEGRATION.md).

## Architecture

```
src/
├── AudioFlow.Models         Domain models
├── AudioFlow.Core           Devices, sessions, routing, verification
├── AudioFlow.Applications   Process manager + stable identification
├── AudioFlow.Rules          Rule engine + JSON persistence
├── AudioFlow.Configuration  App paths, settings, migrations (installed/portable)
├── AudioFlow.Updates        Version comparison, GitHub releases, checksums
├── AudioFlow.ProcessLoopback Experimental (official Process Loopback API)
├── AudioFlow.Updater        Standalone, verified updater process
├── AudioFlow.Console        CLI
└── AudioFlow.UI             WPF desktop app (localized)
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Routing limitations (read this)

AudioFlow uses the same internal Windows mechanism as
*Settings > System > Sound > Volume mixer > App volume and device preferences*
(`IAudioPolicyConfigFactory`).

- It sets the **persisted output endpoint** for a process.
- The change is applied when the application **(re)initializes its audio stream**.
- It does **not** move a live stream.

In our own Windows 11 testing, a controlled stream-restart experiment did **not**
move audio for the test process. Routing must therefore be considered
**PARTIAL**, not live. See [docs/AUDIO-ROUTING.md](docs/AUDIO-ROUTING.md) and
[docs/LIMITATIONS.md](docs/LIMITATIONS.md).

## Audio Lock limitations

Audio Lock **redirects the audio handled by AudioFlow**. Applications that use
their own endpoint, exclusive mode, or otherwise ignore the policy may bypass it.
It is **partial protection**, not a hard block.

## Process Loopback (experimental)

`src/AudioFlow.ProcessLoopback` uses the official Windows Process Loopback API
(`AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS`, Windows 10 build 20348+). It can activate
a per-process capture interface (verified on Windows 11), but it is not yet wired
into the MVP and does not re-render audio.

## Testing

```powershell
dotnet test tests/AudioFlow.Tests          # unit tests (any OS)
dotnet test tests/AudioFlow.Windows.Tests  # Windows integration (skipped elsewhere)
```

See [docs/TESTING.md](docs/TESTING.md) and [docs/TEST-REPORT.md](docs/TEST-REPORT.md).

## Development

- .NET 8, C#, WPF, NAudio.Wasapi.
- `dotnet build AudioFlow.sln -c Release`
- SDK pinned in [global.json](global.json).
- CI: [.github/workflows/build.yml](.github/workflows/build.yml).

## Contributing & forking

**Your feedback is what makes AudioFlow better.** 🚀

1. **Try it** — grab the latest [Release](https://github.com/JavierparraDev/audioflow/releases)
   or build from source, and use it with your real devices.
2. **Fork it** — press **Fork**, create a branch and make it yours. All the
   hardware, audio stacks and use cases out there are different from ours.
3. **Share improvements** — open a PR with your fix, new backend, device
   profile or translation. Small, focused PRs are easiest to review.
4. **Report** — found a device or app that does not route as expected? Open an
   [issue](https://github.com/JavierparraDev/audioflow/issues) with your Windows
   build, devices and steps to reproduce.

Please keep UI text localized (add keys to both `Strings.resx` and
`Strings.es.resx`) and add tests for behavior changes.

> **Fork, experiment, and send back what works.** Whether it is a new routing
> backend, a device-specific fix or a better UI, contributions are welcome.

## License

[MIT](LICENSE) - Copyright (c) 2026 Javier Parra.

Third-party components: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Privacy

AudioFlow does not collect passwords, personal files, browsing history or private
data. It only accesses audio devices, audio sessions and process information.
