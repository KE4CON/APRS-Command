# APRS Command — Installation Guide

APRS Command ships as native installers and portable zip archives for all
supported platforms. **No administrator account or special permissions are
needed to run APRS Command** — only the installer itself requires admin to
write to Program Files on Windows.

> **Why no code-signing?** Code-signing certificates cost $300–500 per year.
> APRS Command is an open-source amateur radio project maintained voluntarily.
> We do not pay for certificates. The one-time bypass described below is safe
> and is standard practice for unsigned open-source software.

---

## System Requirements

APRS Command is a **64-bit desktop application** built on .NET 10.

| Platform | Minimum version | Architecture |
|---|---|---|
| Windows | Windows 10 or 11 (64-bit) | x64 |
| macOS | macOS 14 (Sonoma) or later | Apple Silicon (M1–M4) & Intel |
| Linux | Ubuntu 22.04+, Debian 12+, Fedora, RHEL 8+ | x64 / ARM64, with an X11 or Wayland desktop |
| Raspberry Pi | Raspberry Pi OS 64-bit (Bookworm) | Pi 3, 4, 5, 400, Zero 2 W — Pi 4/5 recommended |

- **Memory:** 2 GB minimum, 4 GB or more recommended. The live map is the most memory-hungry part; on a Raspberry Pi aim for 2 GB+ (the 1 GB Pi 3 and 512 MB Pi Zero 2 W run it, but the map feels slow).
- **Storage:** ~300 MB for the program, plus room for logs and the offline map cache (which can grow to hundreds of MB depending on the area and detail you save).
- **Display:** a desktop screen at 1280×768 or larger. APRS Command is a windowed desktop app — it does not run headless or on a phone/tablet OS.
- **No administrator account** is needed to run APRS Command; only the installer needs admin, to write into place.

**Not supported:** 32-bit operating systems (32-bit Windows, or the 32-bit Raspberry Pi OS / Linux); older 32-bit ARM boards (the original Pi, Pi 1, first Pi Zero / Zero W, and the early Pi 2 v1.1); Windows 8.1 or earlier; macOS 13 or earlier; Linux distributions from before 2022; phones and tablets; and headless servers with no graphical desktop.

> OS minimums follow the [.NET 10 supported-OS matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md). On Linux you also need glibc 2.27 or newer — any distribution from 2022 onward qualifies.

---

## macOS

### Installer (.dmg) — recommended

1. Download `APRSCommand-vX.Y.Z-macos-arm64.dmg` (Apple Silicon M1/M2/M3/M4)
   or `APRSCommand-vX.Y.Z-macos-x64.dmg` (Intel Mac).
2. Double-click the `.dmg` to mount it.
3. Drag **APRS Command.app** to your Applications folder.
4. **First launch only:** In Applications, **right-click** (or Control-click)
   **APRS Command**, choose **Open**, then click **Open** again in the dialog.
   macOS remembers the exception — subsequent launches work normally.

### Portable zip

```bash
unzip APRSCommand-vX.Y.Z-macos-arm64.zip
cd APRS-Command-osx-arm64
xattr -cr .          # remove quarantine flag set by macOS on downloaded files
./Aprs.Desktop
```

### Serial port access (hardware TNC)

Add your user to the `dialout` group, then log out and back in:

```bash
sudo dseditgroup -o edit -a "$USER" -t user dialout 2>/dev/null || true
```

---

## Windows

### Installer (.exe) — recommended

1. Download `APRSCommand-vX.Y.Z-windows-x64-Setup.exe`.
2. Double-click to run.
3. **First launch only:** If Windows SmartScreen shows "Windows protected your PC",
   click **More info**, then **Run anyway**.
4. APRS Command is installed to `C:\Program Files\APRS Command` with Start
   Menu and Desktop shortcuts.
5. Uninstall via Settings → Apps → APRS Command.

### Portable zip

1. Download `APRSCommand-vX.Y.Z-windows-x64.zip`.
2. Extract to any folder (e.g. `C:\Users\You\APRSCommand`).
3. Run `Aprs.Desktop.exe`. If SmartScreen appears, click More info → Run anyway.

### Serial port access (hardware TNC)

No extra configuration needed — Windows grants serial port access by default.

---

## Linux

### .deb (Debian, Ubuntu, Raspberry Pi OS, Linux Mint, and derivatives)

```bash
# x64 desktop or laptop
sudo dpkg -i aprs-command_X.Y.Z_amd64.deb

# ARM64 (Raspberry Pi 4/5 running 64-bit OS, etc.)
sudo dpkg -i aprs-command_X.Y.Z_arm64.deb

# Launch
aprs-command
```

APRS Command installs to `/opt/aprs-command/` with a launcher at
`/usr/local/bin/aprs-command` and a `.desktop` entry in your app menu.

### .rpm (Fedora, RHEL, CentOS, openSUSE, and derivatives)

```bash
sudo rpm -i aprs-command-X.Y.Z-1.x86_64.rpm    # x64
sudo rpm -i aprs-command-X.Y.Z-1.aarch64.rpm   # ARM64
aprs-command
```

### Portable .tar.gz (any Linux distribution)

```bash
tar -xzf APRSCommand-vX.Y.Z-linux-x64.tar.gz
cd APRS-Command-linux-x64
chmod +x Aprs.Desktop
./Aprs.Desktop
```

### Serial port access (hardware TNC)

On Linux your user must be in the `dialout` group to access serial ports:

```bash
sudo usermod -aG dialout "$USER"
# Log out and back in for the group change to take effect
```

### Raspberry Pi notes

- Use the `linux-arm64` package or archive
- Raspberry Pi OS 64-bit (Bookworm) is the tested platform
- The `.deb` is recommended: `sudo dpkg -i aprs-command_X.Y.Z_arm64.deb`
- GrayWolf (KISS-TCP) is the recommended RF backend for field operations

---

## Running from source

```bash
git clone https://github.com/KE4CON/APRS-Command.git
cd APRS-Command
dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

---

## Building installers locally

```bash
# macOS .app + .dmg (run on macOS, requires brew install create-dmg)
bash scripts/make-macos-app.sh osx-arm64
bash scripts/make-macos-app.sh osx-x64

# Windows .exe (run in PowerShell on Windows, requires NSIS)
pwsh scripts/make-windows-installer.ps1

# Linux .deb + .rpm (run on Linux, requires dpkg-dev and rpm-build)
bash scripts/make-linux-packages.sh linux-x64
bash scripts/make-linux-packages.sh linux-arm64
```

Output goes to `artifacts/installers/`.

---

## Package planning and release validation

The full installer and package strategy is documented in
[docs/INSTALLER_AND_PACKAGE_PLAN.md](docs/INSTALLER_AND_PACKAGE_PLAN.md).
Before producing public packages, complete the
[Final Release Validation Checklist](docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md).
