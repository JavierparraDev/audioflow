# Known Limitations

AudioFlow is honest about what it can and cannot do today.

## 1. Routing is not live (the main limitation)

AudioFlow uses `IAudioPolicyConfigFactory` (the same internal interface as the
Windows *App volume and device preferences* page) to set the **persisted output
endpoint** for a process.

- It applies when the application **(re)initializes its audio stream**.
- It does **not** move a currently playing stream.
- In a controlled stream-restart test on Windows 11, the audio did **not** move.
- The interface is **undocumented** and may change between Windows builds.

Result: routing is **PARTIAL**. The official, documented alternative is Process
Loopback (see below).

## 2. Audio Lock is partial protection

Audio Lock redirects the audio that AudioFlow manages. It is **not** a hard
block:

- Applications that select their own endpoint can bypass it.
- Applications using WASAPI **exclusive mode** can bypass it.
- Applications that ignore the persisted policy can bypass it.

## 3. Per-application endpoint choice is inherently per-process

The persisted endpoint is keyed by process id, so it cannot be set before the
process exists. Combined with limitation 1, this makes it unreliable for apps
that keep a long-lived audio stream.

## 4. Application identification can collide

The primary key is `exe:<file name>`. Two different applications with the same
executable name share a key. A `PathHash` (SHA-256 prefix of the path) is stored
and used as a secondary match, but legacy rules may not have it.

## 5. Endpoint metering is a mix, not per-app

`AudioOutputVerifier` measures the **whole endpoint mix**. If another app is
playing on the same device, the measurement is not isolated. Digital silence
(paused track) yields ~0 even when routing is correct.

## 6. Process Loopback is experimental

`AudioFlow.ProcessLoopback` can **activate** the official per-process capture
interface (verified on Windows 11 build 26200). It does **not** yet:

- re-render captured audio to a chosen endpoint;
- mute the original session (audio would be duplicated);
- capture DRM / protected content.

## 7. Other edge cases

- Device removal while routing: the rule is kept; the app falls back when the
  device returns.
- Exclusive-mode games may not be affected.
- `MMDeviceCollection` (NAudio) is not `IDisposable`; its COM object is released
  by the GC.
