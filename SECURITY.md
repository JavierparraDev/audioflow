# Security Policy

## Supported versions

AudioFlow is in **beta**. Security fixes are applied to the latest release and
to `main`.

| Version | Supported |
|---|---|
| 0.2.x (latest beta) | :white_check_mark: |
| < 0.2 | :x: |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, use GitHub's private vulnerability reporting:

1. Go to the [Security tab](https://github.com/JavierparraDev/audioflow/security).
2. Click **Report a vulnerability**.
3. Describe the issue, the impact, and the steps to reproduce it.

You can expect an acknowledgement within a few days. We will keep you informed
of the progress and credit you in the fix unless you prefer to remain anonymous.

## Scope

AudioFlow runs with the privileges of the current user and touches sensitive
areas of Windows:

- **COM interop** with `IAudioPolicyConfigFactory` and `IPolicyConfig`
  (`AudioFlow.Core/WindowsAudio`).
- **Registry writes** under
  `HKCU\...\LowRegistry\Audio\PolicyConfig\PropertyStore` and the legacy
  `HKCU\...\CurrentVersion\Run` key.
- **Process inspection** to identify applications producing audio.
- **Network access** only to the GitHub Releases API for update checks.

Issues that are especially interesting to us:

- Elevation of privilege or arbitrary code execution.
- Writing to registry keys or files outside the documented locations.
- Path or executable-name spoofing that makes AudioFlow act on the wrong process.
- Tampering with the updater (checksum/signature bypass).
- Any data exfiltration.

## Out of scope

- Windows itself, NAudio, or third-party drivers and virtual audio cables.
- The documented [limitations](docs/LIMITATIONS.md): partial routing, Audio Lock
  being bypassable, exclusive-mode apps, and the experimental Process Loopback.
- The absence of code signing on beta builds (tracked on the roadmap).

## Safe harbour

We will not pursue legal action against researchers who report issues in good
faith, avoid privacy violations and service disruption, and give us reasonable
time to fix the issue before public disclosure.
