# Installation

AudioFlow runs on **Windows 10 (build 20348+) and Windows 11**. There are three
ways to install it.

## 1. Installer (recommended)

1. Download `AudioFlow-Setup-v<version>.exe` from the
   [Releases](https://github.com/JavierparraDev/audioflow/releases) page.
2. Run it and follow the wizard.
3. Launch AudioFlow from the Start Menu (or the optional desktop shortcut).

The installer:

- installs to `C:\Program Files\AudioFlow` (admin) or a per-user location if you
  choose so;
- creates a Start Menu shortcut and an optional desktop shortcut;
- registers a proper uninstaller in *Apps & features*;
- supports upgrades over an existing installation;
- **never touches** your configuration in `%APPDATA%\AudioFlow`.

## 2. Portable ZIP

1. Download `AudioFlow-v<version>-win-x64.zip`.
2. Extract it anywhere (a USB drive works).
3. Run `AudioFlow.exe`.

The ZIP contains a `portable.txt` marker, so AudioFlow stores its data in a
`data\` folder next to the executable instead of `%APPDATA%`. Portable and
installed configurations are kept separate.

## 3. Build from source (developers)

Requirements: Windows, [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/JavierparraDev/audioflow.git
cd audioflow
.\install.ps1 -Launch      # local install, no admin, creates a Start Menu shortcut
```

Or build a full release (installer + portable ZIP):

```powershell
.\install.ps1 -Release     # requires Inno Setup 6
```

## Uninstall

- Installed: use *Settings > Apps > Installed apps > AudioFlow > Uninstall*, or
  the Start Menu uninstaller.
- The uninstaller asks whether to remove your rules and settings. The default is
  to **keep** them. A silent uninstall always keeps them.
- Portable: just delete the folder.

## User data location

| Mode | Location |
| ---- | -------- |
| Installed | `%APPDATA%\AudioFlow` |
| Portable | `<app folder>\data` |

Contents: `rules.json`, `settings.json`, `logs\`, `backup\`. These survive
upgrades and reinstalls.

## Requirements

- Windows 10 build 20348+ or Windows 11, x64.
- No administrator rights are required for the portable or per-user install.
- No .NET SDK is required to run a published release (self-contained).
