# Security Policy

AudioFlow is a Windows desktop application that interacts with the Windows audio
stack through COM interop, modifies per-application audio settings while it runs,
and ships an installer plus a self-updater. We take reports about these areas
seriously.

## Supported versions

Only the latest published release receives security fixes.

| Version | Supported |
| ------- | --------- |
| Latest release | :white_check_mark: |
| Older releases | :x: |

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Use GitHub's private vulnerability reporting:

1. Go to the **Security** tab of this repository.
2. Click **Report a vulnerability**.
3. Describe the issue, the affected version, your Windows build, and a minimal
   reproduction if possible.

If the private report form is not available to you, contact the maintainer
directly through their GitHub profile and ask for a private channel. Do not
include exploit details in a public issue or discussion.

## What to expect

- Acknowledgement of your report within a few days.
- An assessment and, when confirmed, a fix and coordinated disclosure.
- Credit in the release notes if you want it.

## Scope

In scope:

- The installer and the standalone updater (integrity/verification bypass,
  unsafe extraction, path handling).
- COM interop and registry snapshot/restore logic in `AudioFlow.Core`.
- Anything that lets a local process escalate privileges or persist changes
  after AudioFlow exits.

Out of scope:

- Issues that require an already-compromised machine or administrator rights.
- Bugs in third-party dependencies (please report those upstream, though we
  welcome a heads-up).
- Missing hardening that does not have a concrete security impact.

## Project security posture

- No telemetry: the only network call is the update check against GitHub
  Releases.
- Session-only by design: AudioFlow restores the per-application audio registry
  on exit and leaves no rules, logs or registry changes behind.
- Release artifacts are built in CI and published with SHA-256 checksums.
