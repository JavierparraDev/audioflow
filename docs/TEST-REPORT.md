# Test Report

This report contains **only results that were actually executed**. Anything that
could not be executed is marked `NOT TESTED`.

## Environment

| Item | Value |
| ---- | ----- |
| OS | Windows 11 Home Single Language, build **26200** |
| Agent host | WSL2 Ubuntu 24.04 (commands executed on Windows via interop) |
| .NET SDK | 8.0.425 (installed on both Linux and Windows) |
| NAudio | NAudio.Wasapi 2.2.1 |
| Test frameworks | xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1 |

### Audio devices observed

- Speaker (Realtek(R) Audio) - active
- Auriculares (HyperX Cloud III) - active
- FxSound Speakers (FxSound Audio Enhancer) - active (system default at test time)
- LG FHD (NVIDIA High Definition Audio) - active
- NGENUITY virtual devices (HyperX) - several
- Salida digital (High Definition Audio Device) - not present

### Applications observed producing audio

- `FxSound.exe` (virtual audio enhancer) - detected with an active session
- `Spotify.exe` - detected with an active session (`aumid:SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify`)
- `powershell.exe` (playing a system WAV) - used for the controlled routing test

## Unit tests

Command: `dotnet test tests/AudioFlow.Tests -c Release` (Linux)

```
Passed! - Failed: 0, Passed: 26, Skipped: 0, Total: 26
```

## Windows integration tests

Command: `dotnet test tests/AudioFlow.Windows.Tests -c Release` (Windows 11)

```
Correctas! - Con error: 0, Superado: 7, Omitido: 0, Total: 7
```

Covers device enumeration, default endpoint, session enumeration, session
monitor, application identity and endpoint peak measurement.

## Results table

| Test | Expected | Actual | Status |
| ---- | -------- | ------ | ------ |
| Device detection | Detect devices | 13 endpoints enumerated with id/name/state | **PASS** |
| Spotify detection | Detected | `Spotify.exe`, pid, active session, AUMID | **PASS** |
| Discord detection | Detected | Not running during the session | **NOT TESTED** |
| Chrome detection | Detected | Not running during the session | **NOT TESTED** |
| Spotify -> Speakers (live) | Speakers | API write accepted+verified; live stream not moved | **PARTIAL** |
| Discord -> Headphones | Headphones | - | **NOT TESTED** |
| Chrome -> Headphones | Headphones | - | **NOT TESTED** |
| Unknown -> Headphones | Headphones | Rule engine resolves to default (unit-tested) | **PARTIAL** |
| Audio Lock | Only Spotify on speakers | Logic verified; hard block not guaranteed | **PARTIAL** |
| Stream restart | Correct endpoint | Controlled test: audio did **not** move | **FAIL** |
| Process Loopback | Working | Capture interface activated successfully | **PASS (activation)** / re-render **NOT TESTED** |
| Routing verification tooling | Measure real level | Measured peaks: FxSound 0.254, LG FHD 0.347 | **PASS** |

## Routing verification detail

`audioflow verify "FxSound Speakers"` while a system WAV played in a loop:

```
0.0000  NGENUITY - Chat (HyperX Virtual Audio Device)
0.0000  Speaker (Realtek(R) Audio)
0.0000  Auriculares (HyperX Cloud III)
0.2540  FxSound Speakers (FxSound Audio Enhancer)   <== expected
0.3467  LG FHD (NVIDIA High Definition Audio)
RESULT: PARTIAL - signal on the expected endpoint, but also on another device.
```

This proves the verifier measures the **real** audio level of each endpoint
(objective evidence), not just an API return value.

## Stream restart experiment (critical)

Procedure:

1. Start audio in `powershell.exe` (system WAV, looping).
2. `audioflow route-pid <pid> "Speaker (Realtek"` -> `success=True verified=True`.
3. Stop the stream, then restart it in the **same** process.
4. `audioflow verify "Speaker (Realtek"`.

Result: the audio **did not** move to the Speaker endpoint; it stayed on FxSound
Speakers / LG FHD.

```
LIVE STREAM:    FAIL  (expected - the API is not live)
STREAM RESTART: FAIL  (in this environment, for this process)
```

Conclusion: the persisted-endpoint method must be considered **PARTIAL**. It
cannot be presented as a reliable live router. This is why Process Loopback
(official API) is the planned path for real routing.

## Audio Lock

Logic is unit-tested (allowed app stays on the locked device; non-allowed app is
redirected to the fallback). Physically enforcing a hard block is **not**
guaranteed because apps using their own endpoint or exclusive mode can bypass the
policy. Status: **PARTIAL**.

## Process Loopback

`audioflow loopback-probe <pid>` on Windows 11 build 26200:

```
Supported on this OS: True
activated: True
message  : Process loopback IAudioClient activated successfully (interface acquired).
```

Status: **EXPERIMENTAL** - activation verified, capture/re-render not implemented.

## Performance

Not formally benchmarked. Observations:

- Device enumeration: a few milliseconds.
- Session enumeration: a few milliseconds (13 endpoints).
- Session monitor refresh: 1 s interval; event-driven wake on device changes.
- Endpoint peak measurement: configurable window (default 3 s for the CLI).

## Known failures / limitations

- Stream restart routing FAIL (see above).
- Audio Lock is partial.
- Discord/Chrome/Game physical routing not tested (apps not available/playing
  under controlled conditions in this environment).
- Process Loopback capture not implemented.

## Installer, portable and updates (this phase)

Environment: Windows 11 build 26200, Inno Setup 6.7.3, .NET SDK 8.0.425.

| Test | Expected | Actual | Status |
| ---- | -------- | ------ | ------ |
| Release pipeline (`tools/release.ps1`) | Artifacts built | Tests 53/53; Setup 84.67 MB; ZIP 91.47 MB; SHA256SUMS; release.json | **PASS** |
| Portable single-file app | Launches | Window created | **PASS** |
| Installer silent per-user install | Files + shortcut | `AudioFlow.exe`, `AudioFlow.Updater.exe`, Start Menu shortcut | **PASS** |
| Installed app launch | Window created | Window created | **PASS** |
| Uninstaller | Removes binaries | Exit 0, binaries removed | **PASS** |
| Uninstall preserves `%APPDATA%\AudioFlow` | Preserved | Marker preserved (silent) | **PASS** |
| Updater refuses installer without SHA-256 | Exit non-zero | Exit code 3, logged | **PASS** |
| Updater portable apply | Files copied, data kept | Binary copied; `data\rules.json` and `portable.txt` preserved | **PASS** |
| Configuration backup before update | Backup created | `%APPDATA%\AudioFlow\backup\<timestamp>` | **PASS** |
| `audioflow version` | Prints version | `AudioFlow 0.2.0` | **PASS** |
| `audioflow update --check` | Contacts GitHub | "No release published yet" (real API call) | **PASS** |
| `install.ps1` (dev) | Local install | Installed + shortcut + window | **PASS** |
| `install.ps1 -Clean -Force` | Removes install | Removed, kept user data | **PASS** |
| `tools/dev-update.ps1 -Build` | Update + build | Fetch, version compare, restore, build | **PASS** |
| End-to-end self-update from a published release | Download + apply | No release published yet | **NOT TESTED** |
| Code signing | Signed binaries | No certificate available | **NOT TESTED** |

The release workflow (`release.yml`) was not executed on GitHub Actions from this
environment; it is validated by running the same `tools/release.ps1` locally.
