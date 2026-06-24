# APRS Command Installer and Package Plan

This plan describes how APRS Command should be packaged for first-user testing and later releases. It is a planning document only. It does not create final installers, signed packages, release archives, signing identities, notarization credentials, or package-manager manifests.

Before producing public packages, complete `docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md`. The checklist covers build/test validation, desktop launch, first-run setup, Help/docs, package contents, checksums, platform smoke tests, and safe transmit-disabled defaults.

## Packaging Goals

- Preserve APRS Command safe defaults.
- Keep first releases simple, repeatable, and easy to inspect.
- Use self-contained publish output for first-user testing unless a platform package explicitly chooses framework-dependent output later.
- Keep logs, configuration, map cache, exports, replay data, training data, file hooks, and plugins in user-writable application data folders.
- Keep final installers separate from the repeatable publish scripts.
- Include offline help documentation under the published `docs/` folder.

## Planned Package Types

| Platform | Runtime identifier | First safe package | Later package placeholders |
| --- | --- | --- | --- |
| Windows x64 | `win-x64` | portable self-contained folder or ZIP | MSI, MSIX |
| macOS Apple Silicon | `osx-arm64` | portable app folder or app bundle test build | DMG, signed/notarized app |
| macOS Intel | `osx-x64` | portable app folder or app bundle test build | DMG, signed/notarized app |
| Linux x64 | `linux-x64` | portable self-contained folder or `tar.gz` | AppImage, deb, rpm |
| Linux ARM64 | `linux-arm64` | portable self-contained folder or `tar.gz` | AppImage/deb if practical |
| Raspberry Pi 5 / Linux ARM64 | `linux-arm64` | portable folder or `tar.gz` | desktop launcher, optional autostart placeholder |

## Packaging Metadata Placeholders

- Application name: `APRS Command`
- Executable name placeholder: `Aprs.Desktop` until a final executable rename is explicitly scheduled.
- Version placeholder: `0.0.0-dev` until release versioning is finalized.
- Publisher/author placeholder: `TBD`
- Description: `Cross-platform amateur-radio APRS desktop client with receive-first safe defaults.`
- Icon placeholder: `aprs-command` or `aprs-command.png` when original artwork is added.
- License placeholder: `TBD`; do not invent a final license until one is chosen.
- Website/support placeholder: `TBD`
- Documentation/help location: published `docs/` folder beside the application.

## Windows x64 Plan

Recommended first package:

1. Run `./scripts/publish-win-x64.sh` or `.\scripts\publish-win-x64.ps1`.
2. Verify `artifacts/publish/win-x64/`.
3. Run `./scripts/package-win-x64.sh` or `.\scripts\package-win-x64.ps1`.
4. Verify `artifacts/packages/APRS-Command-win-x64.zip`.
5. Verify `artifacts/checksums/APRS-Command-win-x64.zip.sha256`.
6. Confirm `docs/` is inside the ZIP for offline Help.

Future installer placeholders:

- MSI installer for traditional desktop deployment.
- MSIX installer if Windows Store-style identity and sandboxing become useful.
- Desktop shortcut named `APRS Command`.
- Start Menu shortcut named `APRS Command`.
- Uninstall entry with application name `APRS Command`.

Windows user data should live under the user application data location prepared by first-run setup. Logs, configuration, map cache, exports, replay files, training files, file hooks, and plugins must not be stored in Program Files.

## macOS Plan

Recommended first package:

1. Run `./scripts/publish-osx-arm64.sh` for Apple Silicon.
2. Run `./scripts/publish-osx-x64.sh` for Intel while Intel support remains planned.
3. Verify the publish folder and offline `docs/`.
4. Run `./scripts/package-osx-arm64.sh` or `./scripts/package-osx-x64.sh`.
5. Verify `APRS-Command-osx-arm64.tar.gz` or `APRS-Command-osx-x64.tar.gz`.
6. For Apple Silicon test validation, also verify `APRS-Command-osx-arm64-test.tar.gz`.
7. Verify the matching SHA256 checksum file.
8. Extract the package, run `xattr -dr com.apple.quarantine .` if needed, and launch `./Aprs.Desktop` from Terminal or `open "./APRS Command.command"` for Finder-style testing.

Future package placeholders:

- `.app` bundle with `Info.plist`.
- DMG package.
- Apple Silicon build: `osx-arm64`.
- Intel build: `osx-x64`.
- Signing and notarization workflow.
- The portable archive includes `APRS Command.command` only as a temporary unsigned-test launcher. A real `.app` bundle should replace it in final macOS packaging.

Do not add real signing certificates, Apple IDs, passwords, app-specific passwords, notarization keys, or keychain exports to the repository.

Unsigned test builds may trigger Gatekeeper warnings. Documentation should tell users to run only builds they trust.

macOS user data should live under the user Application Support location prepared by first-run setup. Logs, configuration, map cache, exports, replay files, training files, file hooks, and plugins stay user-writable.

## Linux x64 and Linux ARM64 Plan

Recommended first package:

1. Run `./scripts/publish-linux-x64.sh` or `./scripts/publish-linux-arm64.sh`.
2. Verify `artifacts/publish/<runtime-identifier>/`.
3. Run `./scripts/package-linux-x64.sh` or `./scripts/package-linux-arm64.sh`.
4. Verify `APRS-Command-linux-x64.tar.gz` or `APRS-Command-linux-arm64.tar.gz`.
5. Verify the matching SHA256 checksum file.
6. Confirm `docs/` is inside the package for offline Help.

Future package placeholders:

- AppImage.
- deb package.
- rpm package.
- `.desktop` launcher file.
- icon installation under an appropriate icon theme path.

Linux serial access may require membership in a distribution-specific serial group such as `dialout`, `uucp`, or `tty`. Documentation should avoid recommending root execution as a normal solution.

Linux user data should live under the user application data location or an XDG-compatible equivalent. Logs, configuration, map cache, exports, replay files, training files, file hooks, and plugins must stay user-writable.

## Raspberry Pi 5 / Linux ARM64 Plan

Raspberry Pi 5 uses the `linux-arm64` publish output.

Recommended first package:

1. Run `./scripts/publish-linux-arm64.sh`.
2. Run `./scripts/package-linux-arm64.sh`.
3. Extract `artifacts/packages/APRS-Command-linux-arm64.tar.gz` to reliable storage.
4. Prefer SSD or high-quality external storage when using large map caches.
5. Use the included `.desktop` template as a launcher starting point.
6. Keep serial/USB permission notes visible for TNC and GPS setup.

Optional future placeholders:

- desktop launcher.
- user-level autostart entry.
- Raspberry Pi setup notes for serial devices and storage.

Autostart must not enable APRS-IS transmit, RF transmit, iGate, digipeater, beaconing, weather beaconing, file hooks, plugins, REST API, or WebSocket by itself.

## Desktop Launcher Placeholders

Placeholder templates live under `packaging/templates/`:

- `aprs-command.desktop.template` for Linux desktop launchers.
- `macos-info-plist-notes.md` for future macOS app bundle metadata.
- `windows-shortcuts.md` for Windows Desktop and Start Menu shortcut notes.
- `release-notes-template.md` for later release notes.

These are not final package files.

## Portable Package Scripts

Portable packages are created by:

```bash
./scripts/package-win-x64.sh
./scripts/package-osx-arm64.sh
./scripts/package-osx-x64.sh
./scripts/package-linux-x64.sh
./scripts/package-linux-arm64.sh
./scripts/package-all.sh
```

The shared implementation is `scripts/package-runtime.sh`. It runs the publish script, stages the output, copies README/Quick Start/Installation/Safety/Troubleshooting files, includes the published `docs/` folder for in-app Help, includes packaging templates, writes `VERSION.txt`, creates the package archive, and writes a SHA256 checksum.

## Release Folder Structure

Release automation should use:

```text
artifacts/
  publish/
  packages/
  checksums/
  release-notes/
```

The repository ignores `artifacts/`. Release packages, checksums, and generated release notes should be produced by release automation and should not be committed unless a future task explicitly changes that policy.

## Checksums and Release Notes Plan

Each release should include:

- SHA256 checksums for each package.
- release version number.
- build date.
- target runtime identifier.
- package type.
- known issues.
- upgrade notes.
- safe default statement.
- documentation/help location.

Release notes should clearly state:

```text
APRS Command does not transmit by default. APRS-IS transmit, RF transmit, iGate, digipeater, beaconing, weather beaconing, REST API, WebSocket, file hooks, and plugin loading are disabled by default. Replay, simulation, and training cannot transmit.
```

## Safety Defaults For Packaged Builds

Packaging must preserve:

- APRS-IS transmit disabled.
- RF transmit disabled.
- iGate disabled.
- digipeater disabled.
- beaconing disabled.
- weather beaconing disabled.
- object transmit disabled until configured.
- message transmit disabled until configured.
- REST API disabled.
- WebSocket disabled.
- file hooks disabled.
- plugin loading disabled.
- replay, simulation, and training cannot transmit.

No package, launcher, installer, post-install script, autostart entry, or example configuration may enable transmit or bypass centralized safety checks.

## Credential and Signing Safety

Packaging files must not contain:

- APRS-IS passcodes.
- API tokens.
- weather service credentials.
- GitHub tokens.
- signing certificates.
- signing passwords.
- Apple notarization credentials.
- private keys.
- personal callsigns unless clearly fake examples such as `N0CALL`.

Use placeholders such as `TBD`, `N0CALL`, `SIM001`, `TESTWX`, and `OBJTEST` where examples are needed.
