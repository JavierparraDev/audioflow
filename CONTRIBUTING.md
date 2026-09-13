# Contributing to AudioFlow

First off, thank you for taking the time to contribute. AudioFlow is a
**beta, community-driven open-source project**: every test on real hardware,
bug report, translation and pull request makes it better for everyone.

> AudioFlow aims to be the complete, 100% functional, open-source
> per-application audio router for Windows. We are not there yet — see the
> [roadmap](docs/ROADMAP.md) and [known limitations](docs/LIMITATIONS.md).

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By
participating, you agree to uphold it.

## Ways to help (no code required)

| Area | What we need |
|---|---|
| **Physical testing** | Run AudioFlow on your hardware (Realtek, USB DACs, Bluetooth, Voicemeeter, HyperX, gaming headsets) and report what works. |
| **Bug reports** | A device or app that does not route as expected. Include your Windows build, devices and steps. |
| **Translations** | Add a language under `src/AudioFlow.UI/Resources/`. |
| **Documentation** | Fix anything unclear in `README.md`, `README.es.md` or `docs/`. |
| **UX feedback** | Screenshots, confusing labels, missing states. |

## Ways to help (code)

The highest-impact areas, roughly in order:

1. **Virtual audio endpoint** — the blocker for duplication-free *live* routing.
   Integrate a virtual cable/driver (or write our own) so AudioFlow can render to
   a null sink, capture it and re-render to the real device.
   See [docs/ARCHITECTURE-DECISION-LIVE-ROUTING.md](docs/ARCHITECTURE-DECISION-LIVE-ROUTING.md).
2. **Routing backends** — implement new `IAudioRoutingBackend` strategies.
3. **Interop review** — the COM/registry code in `AudioFlow.Core/WindowsAudio`
   deserves careful review.
4. **Reliability** — crash recovery, device reconnection, performance.
5. **UI / UX** — WPF views, accessibility, new languages.

Look for issues labelled [`good first issue`](https://github.com/JavierparraDev/audioflow/labels/good%20first%20issue)
and [`help wanted`](https://github.com/JavierparraDev/audioflow/labels/help%20wanted).

## Development setup

### Requirements

- Windows 10 (build 20348+) or Windows 11
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

### Build and run

```powershell
git clone https://github.com/JavierparraDev/audioflow.git
cd audioflow

./install.ps1 -Launch          # build + local install, no admin
# or manually:
dotnet build AudioFlow.sln -c Release
dotnet run --project src/AudioFlow.UI
```

### Tests

```powershell
dotnet test tests/AudioFlow.Tests          # unit tests (any OS)
dotnet test tests/AudioFlow.Windows.Tests  # Windows integration (skipped elsewhere)
```

Please run both before opening a pull request. CI runs the unit tests on Linux
and the integration tests on Windows.

## Project layout

```
src/
├── AudioFlow.Models         Domain models
├── AudioFlow.Core           Devices, sessions, routing, verification, interop
├── AudioFlow.Applications   Process manager + stable identification
├── AudioFlow.Rules          Rule engine (session-only, in-memory)
├── AudioFlow.Configuration  App paths, settings, migrations
├── AudioFlow.Updates        Version comparison, GitHub releases, checksums
├── AudioFlow.Session        Snapshot, restore, crash recovery, guardian
├── AudioFlow.Routing        Swappable routing backends
├── AudioFlow.LiveRouting    Process Loopback capture + WASAPI render
├── AudioFlow.ProcessLoopback Experimental
├── AudioFlow.Updater        Standalone verified updater
├── AudioFlow.Console        CLI
└── AudioFlow.UI             WPF desktop app (localized)
```

## Coding guidelines

- .NET 8, C#, nullable enabled, implicit usings, file-scoped namespaces.
- Follow the style of the surrounding code; keep methods small and focused.
- **UI text must be localized**: add the key to both `Strings.resx` (English)
  and `Strings.es.resx` (Spanish). Never hardcode user-facing strings.
- **Add tests** for behavior changes. Bug fixes should come with a regression
  test where possible.
- Do not add external dependencies without discussing it in an issue first.
- Keep the app **session-only**: it must leave no rules, logs or audio registry
  changes behind when it is not running.

## Commit and PR conventions

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(scope): add virtual endpoint backend
fix(session): restore policy for closed apps
docs(readme): clarify routing limitations
test(rules): cover in-memory storage
chore(ci): cache NuGet packages
```

Pull requests should be small and focused. In the description, include:

- **What** changed and **why**.
- **How you tested it** (OS build, devices, commands).
- Screenshots for UI changes.
- A reference to the related issue (`Closes #123`).

Use the provided [pull request template](.github/PULL_REQUEST_TEMPLATE.md).

## Adding a language

1. Copy `src/AudioFlow.UI/Resources/Strings.resx` to
   `Strings.<culture>.resx` (e.g. `Strings.fr.resx`).
2. Translate the `<value>` entries, keeping the `name` keys unchanged.
3. Register the language in the UI language list.

## Reporting bugs and security issues

- Bugs and feature requests: use the [issue templates](https://github.com/JavierparraDev/audioflow/issues/new/choose).
- Security issues: **do not open a public issue** — follow [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE).
