# AudioFlow

> **Professional per-application audio routing for Windows.**

AudioFlow lets you decide **where each application plays its audio**. The primary
use case:

```
Spotify            ->  Speakers
Discord            ->  Headphones
Chrome / YouTube   ->  Headphones
Games              ->  Headphones
Everything else    ->  Headphones (default rule)
```

[Español](README.es.md)

---

## Features

- Detects every output device (speakers, headphones, Bluetooth, monitors, virtual devices).
- Detects applications that are producing audio in real time (WASAPI audio sessions).
- Stable application identification (executable name + path hash + AUMID for Store apps).
- Per-application rules and a global default output.
- **Audio Lock** (partial protection) to keep non-authorized apps off a device.
- Modern dark WPF UI with **English and Spanish** localization.
- Objective routing verification (measures the real level on each endpoint).
- Experimental **Process Loopback** module (official API, isolated from the MVP).

## Requirements

- Windows 10 (build 20348+) or Windows 11
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- .NET 8 Desktop Runtime (to run the published app)

## Installation

Download a published build, or build from source:

```powershell
git clone https://github.com/JavierparraDev/audioflow.git
cd audioflow
./publish.ps1            # produces publish/ui/AudioFlow.exe and publish/cli/audioflow.exe
```

## Usage

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
```

## Architecture

```
src/
├── AudioFlow.Models         Domain models
├── AudioFlow.Core           Devices, sessions, routing, verification
├── AudioFlow.Applications   Process manager + stable identification
├── AudioFlow.Rules          Rule engine + JSON persistence
├── AudioFlow.ProcessLoopback Experimental (official Process Loopback API)
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

## Contributing

Issues and pull requests are welcome. Please keep UI text localized (add keys to
both `Strings.resx` and `Strings.es.resx`) and add tests for behavior changes.

## License

[MIT](LICENSE) - Copyright (c) 2026 Javier Parra.

Third-party components: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Privacy

AudioFlow does not collect passwords, personal files, browsing history or private
data. It only accesses audio devices, audio sessions and process information.
