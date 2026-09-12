# Updates

AudioFlow can check GitHub Releases for new versions and update itself.

## How it works

1. On startup (if enabled) AudioFlow performs **one asynchronous** check against
   the GitHub Releases API. It never blocks startup and never polls continuously.
2. The check resolves the latest stable release and compares it with the
   installed version (semantic versioning; prereleases sort before releases).
3. **Settings > Updates** shows the current version, the latest version, the
   status and the last check time.
4. If an update is available, **Update now** appears.

## The update flow

When you press **Update now**:

1. AudioFlow downloads the installer asset.
2. It fetches `SHA256SUMS.txt` and resolves the expected hash.
3. It downloads the installer to a temporary folder and **verifies the SHA-256**.
4. It launches the standalone **`AudioFlow.Updater.exe`** and exits.
5. The updater waits for AudioFlow to close, backs up `%APPDATA%\AudioFlow`,
   re-verifies the checksum, runs the installer silently, and restarts AudioFlow.

Files are never replaced while AudioFlow is running.

## Safety

- The updater **refuses** to run an installer without a verified SHA-256.
- The download is deleted if verification fails.
- A backup of your configuration is created before applying an update.
- The updater logs to `%APPDATA%\AudioFlow\logs\updater.log`.
- If anything fails, the previous installation is left untouched.

## Offline behavior

If GitHub is unreachable, AudioFlow shows *"Unable to check for updates"* and
**keeps working normally**. Audio detection, routing, rules and the UI are never
blocked by update checking.

## Channels

The architecture supports `stable`, `beta` and `nightly` channels
(`AppSettings.UpdateChannel`). The default is `stable`. Beta/nightly releases are
GitHub prereleases.

## Privacy

Update checking makes only the requests required to read the public GitHub
Releases API. AudioFlow **never** sends audio information, application names,
device names, rules or personal data. There is no telemetry.

## Manual update

You can always download the latest installer from the
[Releases](https://github.com/JavierparraDev/audioflow/releases) page and run it
over your existing installation. Your configuration is preserved.

## Rollback

Releases are immutable. To roll back, download the previous version's installer
from the Releases page and run it; your configuration is preserved. If a
migration changed the configuration, a backup is available in
`%APPDATA%\AudioFlow\backup`.
