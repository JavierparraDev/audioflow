# Live Routing — Physical Test Report

All results below were executed on real hardware. Nothing is inferred from an API
return value.

**Environment**

| Item | Value |
| ---- | ----- |
| OS | Windows 11 Home Single Language, build **26200** |
| .NET | 8.0.425 |
| Host | WSL2 Ubuntu 24.04 executing Windows binaries via interop |
| Audio source for tests | `powershell.exe` playing `C:\Windows\Media\Alarm01.wav` in a loop (`System.Media.SoundPlayer`) |
| Capture format | 16-bit PCM / 44100 Hz / stereo |
| Device mix format | 48000 Hz / 2 ch / 32-bit float (Extensible) |
| Silence threshold | peak < **0.001** (documented) |
| Measurement window | 3 s (endpoint verifier), 0.5 s per capture sample |

## Results

| Test | Application | Source | Target | Actual Result | Evidence | Status |
| ---- | ----------- | ------ | ------ | ------------- | -------- | ------ |
| Capture | powershell (WAV loop) | Process Loopback | (none) | 265,041 frames / 6 s, max peak **0.3554**, 601 packets, 0 silent | `loopback-capture` | **PASS** |
| Render | powershell (WAV loop) | Capture 44100/16/2 → mix 48000/32/2 | Speaker (Realtek) | Capture frames == render frames (353,241), 0 underruns, target peak **0.2010** | `live-route` | **PASS** |
| Duplication | powershell (WAV loop) | Process Loopback + re-render | Speaker (Realtek) | Target peak 0.2010 **and** original endpoint peak 0.5909 simultaneously | `live-route` verify | **FAIL (duplicated)** |
| Mute suppression | powershell (WAV loop) | Capture + `SetProcessMute(on)` | Speaker (Realtek) | Capture peak **0.0000** during mute; target peak 0.0000 | `live-route --mute` | **FAIL (mute kills capture)** |
| Spotify capture | Spotify.exe | Process Loopback | - | Not executed (Spotify not driven under controlled playback in this run) | - | **NOT TESTED** |
| Chrome capture | chrome.exe | Process Loopback | - | Not executed | - | **NOT TESTED** |
| Multi-app simultaneous | Spotify + Chrome | Process Loopback | Speakers + Headphones | Not executed (blocked by duplication) | - | **NOT TESTED** |
| Device disconnect | powershell | Capture → target | (disconnect) | Not executed | - | **NOT TESTED** |

## Raw evidence

### 1. Capture (Phase 3B)

```
Audio process PID=26108
Capturing process 26108 for 6s (16-bit PCM / 44100 / stereo)...
  peak=0.0027  rms=0.0010  frames=21609  packets=49   silent=0
  ...
  peak=0.1850  rms=0.0791  frames=110250 packets=250  silent=0
  ...
Frames captured : 265041
Packets         : 601
Max peak        : 0.3554
RESULT: AUDIO CAPTURED (peak above silence threshold).
```

### 2. Render to a chosen device (Phase 3C/3D)

```
Process 12788 -> Speaker (Realtek(R) Audio) for 8s
  [state] Starting
  [state] Rendering
  [state] Running
  capture peak=0.1806 rms=0.0899 frames=88200  | render frames=88200  buffered=132300B underruns=0
  ...
Capture frames: 353241  Render frames: 353241
Mix format    : 48000Hz/2ch/32bit/Extensible
Verifying real audio level on 'Speaker (Realtek(R) Audio)' (3s, pipeline still running)...
  0.2010  Speaker (Realtek(R) Audio)   <== target
  0.5909  Auriculares (HyperX Cloud III)
RESULT: target endpoint received audio.
```

The 0.5909 on the original endpoint is the duplicated original output.

### 3. Mute suppression attempt (critical)

```
Process 15440 -> Speaker (Realtek(R) Audio) for 6s
  original session muted (duplication suppression attempt)
  capture peak=0.0000 rms=0.0000 frames=22050  | render frames=22050  ...
  ...
Capture frames: 265041  Render frames: 265041
Verification: expected 'Speaker (Realtek(R) Audio)' peak=0.0000 signalOnExpected=False
  0.0000  Speaker (Realtek(R) Audio)   <== target
RESULT: target endpoint received NO audio.
```

Muting the original session silences the process loopback capture as well.

## Conclusion

- Process Loopback capture works for a real process.
- WASAPI re-render to a chosen device works, including format negotiation
  (44100/16 → 48000/32).
- The pipeline **duplicates** audio because the original output is not
  suppressed.
- Session mute is **not** a viable suppression method (it also mutes the
  capture).

See [ARCHITECTURE-DECISION-LIVE-ROUTING.md](ARCHITECTURE-DECISION-LIVE-ROUTING.md).
