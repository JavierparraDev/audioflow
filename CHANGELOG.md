# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Localization for French, German, Portuguese (Brazil), Italian, Japanese and
  Russian, in addition to English, Spanish and Turkish.
- `SECURITY.md` with the vulnerability reporting process.

### Changed

- The language selector labels now resolve through the resource system instead
  of hardcoded strings.

## [0.2.0] - 2026-09-13

### Added

- Per-application audio routing with a rule engine and a global default output.
- Native Windows 10/11 WPF UI with sidebar navigation and a dark theme.
- WASAPI device and audio-session detection with stable application identity.
- Session-scoped rules with exact per-application registry snapshot/restore.
- Crash recovery, the Session Guardian and Emergency Reset.
- Swappable routing backends (policy endpoint and virtual endpoint).
- CLI for diagnostics (`devices`, `sessions`, `plan`, `apply`, `verify`,
  `diagnostics`, `guardian`, `session`, `restore`, `cleanup`).
- Update checker against GitHub Releases and a standalone verified updater.
- Inno Setup installer, portable ZIP and a release workflow with checksums.
- English and Spanish localization.
- Unit tests plus Windows integration tests, with CI on Linux and Windows.

[Unreleased]: https://github.com/JavierparraDev/audioflow/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/JavierparraDev/audioflow/releases/tag/v0.2.0
