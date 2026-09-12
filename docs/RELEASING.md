# Releasing

## Versioning

The version is defined **once** in `Directory.Build.props`:

```xml
<Version>0.2.0</Version>
```

It feeds the executable metadata, the UI, the CLI (`audioflow version`), the
installer and the updater. To release, bump it and commit.

Semantic versioning: `MAJOR.MINOR.PATCH` (prereleases like `0.3.0-beta.1` are
allowed).

## Local release

Requirements: Windows + .NET SDK 8 + Inno Setup 6.

```powershell
.\tools\release.ps1                 # tests + publish + portable + installer + checksums
.\tools\release.ps1 -SkipTests      # skip tests (not recommended)
.\tools\release.ps1 -SkipInstaller  # only the portable ZIP
```

Artifacts are written to `artifacts/`:

- `AudioFlow-Setup-v<version>.exe`
- `AudioFlow-v<version>-win-x64.zip`
- `SHA256SUMS.txt`
- `release.json`
- `RELEASE-NOTES.md`

The publish is **self-contained** and **single-file** (ReadyToRun/trimming are
not enabled: trimming breaks WPF/reflection/localization, so it is intentionally
avoided).

## CI release

`.github/workflows/release.yml` runs on tags matching `v*`:

1. checkout
2. setup .NET 8
3. install Inno Setup
4. unit tests
5. `tools/release.ps1 -Version <tag>`
6. upload artifacts
7. create the GitHub Release (prerelease if the tag contains `-`)

A release is **not** published if the tests fail.

## Creating a release

```bash
# bump Directory.Build.props, commit, then:
git tag v0.3.0
git push origin v0.3.0
```

The workflow builds and publishes the release with the installer, portable ZIP,
checksums and release notes.

## Release notes

`tools/release.ps1` generates `RELEASE-NOTES.md` with:

- What's new
- Bug fixes
- Known limitations (including the PARTIAL routing limitation)
- Installation
- Upgrade notes

## Signing (future)

The architecture is ready for code signing: the installer and the updater verify
SHA-256, and the updater refuses unverified packages. When a certificate is
available, sign `AudioFlow.exe`, `AudioFlow.Updater.exe` and the installer, and
publish the certificate thumbprint in the release.
