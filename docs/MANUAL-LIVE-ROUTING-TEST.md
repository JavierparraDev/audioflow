# Manual Live Routing Test

Use this to verify AudioFlow live routing on a real Windows machine with real
applications. Do not mark a test PASS without measuring the endpoints.

## Prerequisites

- Windows 10 build 20348+ or Windows 11.
- Two output devices you can distinguish (e.g. Speakers and Headphones).
- The AudioFlow CLI (`audioflow.exe`) or a build of the solution.
- A way to hear or measure both devices (or use the `verify` command).

## Silence threshold

Peak below **0.001** is treated as silence. Measure for at least 3 seconds.

## Identify a process

```powershell
audioflow sessions          # shows PID + Application ID per app
```

## TEST A — Spotify → Speakers

1. Play music in Spotify.
2. Note Spotify's PID from `audioflow sessions`.
3. Run:

   ```powershell
   audioflow live-route <spotifyPid> "Speakers" 15
   ```

4. Read the endpoint peaks printed at the end.

Expected: target Speakers peak > 0.001.
Note: the original device will also have audio (duplication) — see
[ARCHITECTURE-DECISION-LIVE-ROUTING.md](ARCHITECTURE-DECISION-LIVE-ROUTING.md).

## TEST B — Chrome / YouTube → Headphones

1. Play a YouTube video in Chrome.
2. Find the **audio-producing** Chrome process:

   ```powershell
   audioflow sessions
   ```

   Chrome may have several processes; pick the one with an active audio session.
3. Run:

   ```powershell
   audioflow live-route <chromePid> "Headphones" 15
   ```

Expected: target Headphones peak > 0.001.

## TEST C — Spotify + Chrome simultaneously

1. Play Spotify and YouTube.
2. Start two pipelines (two terminals):

   ```powershell
   audioflow live-route <spotifyPid> "Speakers" 30
   audioflow live-route <chromePid>  "Headphones" 30
   ```

Expected: Speakers receives Spotify, Headphones receives Chrome.
Remember both originals still play (duplication).

## TEST D — Live move

1. Play Spotify and start a pipeline to Speakers.
2. While it plays, verify the target; then stop and start a pipeline to
   Headphones.

Expected: the pipeline can move to another device (it restarts the renderer).
Document whether the audible result is acceptable given duplication.

## TEST E — Device disconnect

1. Start a pipeline to a device.
2. Disconnect that device.
3. Observe the pipeline state and logs.

Expected: the pipeline reports a failure/disconnect and does not crash or leak.
Record the exact message.

## Capturing the raw capture (no render)

```powershell
audioflow loopback-capture <pid> 10
```

Expected: `Max peak` > 0.001 while audio plays.

## Muting the original (duplication experiment)

```powershell
audioflow mute-pid <pid> on
# observe capture peak drops to 0
audioflow mute-pid <pid> off
```

This demonstrates that session mute silences the capture.
