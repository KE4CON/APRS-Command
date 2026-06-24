# APRS Command Release Validation Report

This report records the Phase 15.10 validation run for portable test packages. It is not a public final release approval.

## Validation Summary

| Item | Result |
| --- | --- |
| Validation date | 2026-06-13T13:35:12Z |
| Application | APRS Command |
| Validation platform | macOS 26.5.1, Apple Silicon arm64 |
| Runtime identifier tested | `osx-arm64` |
| Runtime identifiers built | `win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64` |
| .NET SDK version | `10.0.203` |
| Git commit | `29ea7f5a4fa060f1e0e46baa002d72cf712bff62` |
| Actual solution path | `CrossPlatformAprs.sln` |
| Actual desktop project path | `src/Aprs.Desktop/Aprs.Desktop.csproj` |
| Actual test project path | `tests/Aprs.Tests/Aprs.Tests.csproj` |
| Actual desktop executable after publish | `Aprs.Desktop` on macOS/Linux, `Aprs.Desktop.exe` on Windows |
| Working tree note | Report updated with Phase 15.10 portable package rerun after Help and lower-right feature button UI fixes |
| Public release package created | No |
| Portable test package created | Yes |
| Live transmit performed | No |

## Validation Commands Run

```bash
dotnet --version
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj --no-build
./scripts/validate-release.sh
rm -rf artifacts/publish artifacts/packages artifacts/checksums
./scripts/package-all.sh
```

## macOS Apple Silicon Test Package Commands

The macOS Apple Silicon portable test package is created exactly at:

```text
artifacts/packages/APRS-Command-osx-arm64-test.tar.gz
```

The publish folder and checksum are:

```text
artifacts/publish/osx-arm64/
artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256
```

From the repository root:

```bash
ls -la artifacts/publish/osx-arm64/
ls -la artifacts/packages/APRS-Command-osx-arm64-test.tar.gz
ls -la artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256
cat artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256
```

Extract to the documented temporary test folder:

```bash
rm -rf /tmp/aprs-command-osx-arm64-test
mkdir -p /tmp/aprs-command-osx-arm64-test
tar -xzf artifacts/packages/APRS-Command-osx-arm64-test.tar.gz -C /tmp/aprs-command-osx-arm64-test
cd /tmp/aprs-command-osx-arm64-test/APRS-Command-osx-arm64
ls -la
```

Launch the actual app executable:

```bash
chmod +x ./Aprs.Desktop
chmod +x "./APRS Command.command"
xattr -dr com.apple.quarantine . 2>/dev/null || true
./Aprs.Desktop
```

Or launch the Finder-friendly portable-test command file:

```bash
open "./APRS Command.command"
```

Capture launch errors with:

```bash
./Aprs.Desktop 2>&1 | tee launch-error.txt
```

## Restore Result

Passed.

`dotnet restore` completed successfully and reported all projects up to date.

## Build Result

Passed.

`dotnet build --no-restore` completed successfully with:

```text
0 Warning(s)
0 Error(s)
```

## Test Result

Passed.

`dotnet test --no-build` completed successfully:

```text
Passed: 867
Failed: 0
Skipped: 0
```

The package scripts also ran the Release test suite successfully for each runtime:

```text
Passed: 867
Failed: 0
Skipped: 0
```

## Release Validation Script Result

Passed.

`./scripts/validate-release.sh` completed:

- .NET 10 SDK check passed.
- Restore/build/test passed.
- Required release docs check passed.
- Required publish/package script check passed.
- Help docs publish-copy configuration check passed.
- Obvious credential and unsafe transmit-enabled placeholder scan passed.

No hardware, live APRS-IS connection, weather device, internet credentials, or transmit path was required.

## Desktop Launch Result

Passed for automated smoke start.

`dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj --no-build` started APRS Command successfully and was stopped after 8 seconds. No live APRS-IS, RF/TNC, GPS, weather, REST API, WebSocket, file hook, plugin, replay, simulation, training, or transmit path was started during the smoke run.

## Help Viewer Result

Partially automated; manual visual click-through still recommended.

Validation confirmed:

- Published package includes the `docs/` folder used by in-app Help.
- Extracted package includes User Manual, Quick Start, Installation Guide, First-Run Setup, Safety and Transmit Guide, Troubleshooting, and Glossary.
- Existing Help view model tests passed as part of the full test suite.

Not run in this automated pass:

- Manual Help button click.
- Visual confirmation that each Help topic renders in the desktop window.

## UI Layout Result

Partially automated; manual visual inspection still recommended.

Validation confirmed:

- Source desktop app starts.
- Extracted package desktop app starts.
- Documentation and checklist describe the restored map-first layout.
- Tests covering desktop view models passed.
- Feature command tests confirm Messages, Objects, Weather, Events, Event Bus, Replay, RF Diag, and Alerts update the selected feature title, description, and content.
- The lower-right feature panel uses one explicit two-row, four-column command-driven button grid and keeps the map visible.
- Automated XAML checks confirm all eight lower-right feature labels are present exactly once, so Messages is not the only declared feature button.
- Manual pre-package validation confirmed Help works and all eight lower-right feature buttons are visible and should open/show their panels.

Not run in this automated pass:

- Manual visual confirmation of map, station list, packet monitor, Help viewer, and all eight lower-right feature buttons.
- Manual check for overlapping text at multiple window sizes.

## Safe Defaults Result

Passed for source/package documentation, validation script, and tests.

The validation checklist, safety guide, package `VERSION.txt`, and package contents preserve these safe-default statements:

- APRS-IS transmit disabled.
- RF transmit disabled.
- iGate disabled.
- Digipeater disabled.
- Beaconing disabled.
- Weather beaconing disabled.
- REST API disabled.
- WebSocket disabled.
- File hooks disabled.
- Plugin loading disabled.
- Replay, simulation, and training cannot transmit.

No live transmit occurred during validation.

## Documentation Result

Passed.

Validated documents include:

- `README.md`
- `docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md`
- `docs/BUILD_AND_PUBLISH.md`
- `docs/INSTALLER_AND_PACKAGE_PLAN.md`
- `docs/INSTALLATION_GUIDE.md`
- `docs/SAFETY_AND_TRANSMIT_GUIDE.md`
- `docs/TROUBLESHOOTING.md`
- `docs/GLOSSARY.md`
- `docs/RELEASE_NOTES_TEMPLATE.md`

The final checklist is linked from build/package planning docs. Documentation continues to use `APRS Command` as the user-facing application name.

## Portable Package Result

Passed.

Phase 15.10 created publish outputs under:

```text
artifacts/publish/win-x64/
artifacts/publish/osx-arm64/
artifacts/publish/osx-x64/
artifacts/publish/linux-x64/
artifacts/publish/linux-arm64/
```

Phase 15.10 created these portable packages:

```text
artifacts/packages/APRS-Command-win-x64.zip
artifacts/packages/APRS-Command-win-x64-test.zip
artifacts/packages/APRS-Command-osx-arm64.tar.gz
artifacts/packages/APRS-Command-osx-arm64-test.tar.gz
artifacts/packages/APRS-Command-osx-x64.tar.gz
artifacts/packages/APRS-Command-osx-x64-test.tar.gz
artifacts/packages/APRS-Command-linux-x64.tar.gz
artifacts/packages/APRS-Command-linux-x64-test.tar.gz
artifacts/packages/APRS-Command-linux-arm64.tar.gz
artifacts/packages/APRS-Command-linux-arm64-test.tar.gz
```

Approximate package sizes:

```text
APRS-Command-win-x64.zip: 45M
APRS-Command-win-x64-test.zip: 45M
APRS-Command-osx-arm64.tar.gz: 43M
APRS-Command-osx-arm64-test.tar.gz: 43M
APRS-Command-osx-x64.tar.gz: 45M
APRS-Command-osx-x64-test.tar.gz: 45M
APRS-Command-linux-x64.tar.gz: 43M
APRS-Command-linux-x64-test.tar.gz: 43M
APRS-Command-linux-arm64.tar.gz: 40M
APRS-Command-linux-arm64-test.tar.gz: 40M
```

## Checksum Result

Passed.

Phase 15.10 created and verified these SHA256 checksum files:

```text
artifacts/checksums/APRS-Command-win-x64.zip.sha256
artifacts/checksums/APRS-Command-win-x64-test.zip.sha256
artifacts/checksums/APRS-Command-osx-arm64.tar.gz.sha256
artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256
artifacts/checksums/APRS-Command-osx-x64.tar.gz.sha256
artifacts/checksums/APRS-Command-osx-x64-test.tar.gz.sha256
artifacts/checksums/APRS-Command-linux-x64.tar.gz.sha256
artifacts/checksums/APRS-Command-linux-x64-test.tar.gz.sha256
artifacts/checksums/APRS-Command-linux-arm64.tar.gz.sha256
artifacts/checksums/APRS-Command-linux-arm64-test.tar.gz.sha256
```

All expected package checksum files matched their generated package files.

The macOS Apple Silicon test package checksum from this retry was:

```text
da82bf666327852616b3449619870cba209a4774861c0dc66270bdedbbe10018  APRS-Command-osx-arm64-test.tar.gz
```

## Package Contents Verification

Passed.

Targeted package listing checks confirmed each expected package contains the APRS Command app executable files and offline documentation/help files.

Windows packages contained:

- `Aprs.Desktop.exe`.
- `Aprs.Desktop.dll`.
- `README.md`.
- `QUICK_START.md`.
- `INSTALLATION_GUIDE.md`.
- `SAFETY_AND_TRANSMIT_GUIDE.md`.
- `TROUBLESHOOTING.md`.
- Published `docs/` folder with offline Help files.
- `docs/USER_MANUAL.md`.
- `docs/QUICK_START.md`.
- `docs/INSTALLATION_GUIDE.md`.
- `docs/SAFETY_AND_TRANSMIT_GUIDE.md`.
- `docs/TROUBLESHOOTING.md`.
- `docs/GLOSSARY.md`.

macOS packages contained:

- `Aprs.Desktop`.
- `APRS Command.command`.
- `README.md`.
- `QUICK_START.md`.
- `INSTALLATION_GUIDE.md`.
- `SAFETY_AND_TRANSMIT_GUIDE.md`.
- `TROUBLESHOOTING.md`.
- Published `docs/` folder with 36 help/documentation files.
- `docs/USER_MANUAL.md`.
- `docs/QUICK_START.md`.
- `docs/INSTALLATION_GUIDE.md`.
- `docs/FIRST_RUN_SETUP.md`.
- `docs/SAFETY_AND_TRANSMIT_GUIDE.md`.
- `docs/TROUBLESHOOTING.md`.
- `docs/GLOSSARY.md`.

Linux and Raspberry Pi/Linux ARM64 packages contained:

- `Aprs.Desktop`.
- `README.md`.
- `QUICK_START.md`.
- `INSTALLATION_GUIDE.md`.
- `SAFETY_AND_TRANSMIT_GUIDE.md`.
- `TROUBLESHOOTING.md`.
- Published `docs/` folder with offline Help files.
- `docs/USER_MANUAL.md`.
- `docs/QUICK_START.md`.
- `docs/INSTALLATION_GUIDE.md`.
- `docs/SAFETY_AND_TRANSMIT_GUIDE.md`.
- `docs/TROUBLESHOOTING.md`.
- `docs/GLOSSARY.md`.

The release validation script found no obvious real credentials, APRS-IS passcodes, API tokens, signing secrets, private keys, or transmit-enabled placeholders in the repository docs, scripts, examples, or templates.

## Extracted Package Smoke Test

Passed for automated smoke start.

The current-platform test package was extracted to:

```text
/tmp/aprs-command-osx-arm64-test/APRS-Command-osx-arm64/
```

Validation confirmed:

- Extracted `Aprs.Desktop` has executable permissions.
- Extracted `APRS Command.command` has executable permissions.
- `./Aprs.Desktop` launched successfully from the extracted package.
- `"./APRS Command.command"` launched successfully from the extracted package.
- Package contents and docs/help files were present.
- No live APRS-IS, RF/TNC, GPS, weather, REST API, WebSocket, file hook, plugin, replay, simulation, training, or transmit path was started during the smoke run.

Manual visual checks still recommended:

- Confirm the map/default UI appears.
- Confirm Help is visible and opens the in-app Help viewer.
- Confirm only one lower-right feature button/control set is visible.
- Confirm Messages, Objects, Weather, Events, Event Bus, Replay, RF Diag, and Alerts are all visible together; Messages must not be the only visible feature button.
- Click Messages and confirm the Message Center / Messages view appears.
- Click Objects and confirm the Object Manager / Objects view appears.
- Click Weather and confirm the Weather view appears.
- Click Events and confirm the Decoded Event Log / Events view appears.
- Click Event Bus and confirm the Event Monitor / Event Bus view appears.
- Click Replay and confirm the Replay view appears.
- Click RF Diag and confirm the RF Diagnostics view appears.
- Click Alerts and confirm the Alert Rules / Alerts view appears.
- Confirm there is no duplicate gray feature button set.
- Confirm the failed crowded top navigation is not present.
- Confirm User Manual, Quick Start, Safety and Transmit Guide, Troubleshooting, and Glossary load in Help.
- Confirm docs render in the Help viewer.
- Confirm safe defaults in the UI.

## Packaging Issues Found And Fixed

During earlier package inspection, stale duplicate files from a previous `artifacts/publish/osx-arm64` output were found in the archive. The publish helper was updated to clean only the runtime-specific generated publish folder before `dotnet publish`:

```bash
rm -rf "$OUTPUT"
```

Phase 15.10 also found that the package helper produced standard package names but not all requested `-test` package names. The shared package helper now creates both the standard package and the `-test` package for every supported runtime, with matching checksums.

The shared publish, package, and validation scripts now validate that the discovered repository root contains:

- `CrossPlatformAprs.sln`.
- `README.md`.
- `src/`.
- `docs/`.
- `tests/`.
- `src/Aprs.Desktop/Aprs.Desktop.csproj`.

The scripts print the repository root, solution path, and desktop project path before building.

## Known Issues

- Full interactive Help click-through was not performed in this automated Codex pass.
- Full visual UI layout inspection at multiple window sizes was not performed in this automated Codex pass.
- Windows, macOS Intel, Linux x64, Linux ARM64, and Raspberry Pi package smoke tests were not run on their native platforms in this current-platform-only phase.
- macOS package is a portable test archive, not a signed/notarized `.app` or DMG. Use `./Aprs.Desktop` from Terminal or `open "./APRS Command.command"` from the extracted folder for unsigned portable testing.
- Public final release packages were not created.

## Next Actions

1. Commit the Phase 15.10 package-script, documentation, and validation-report updates.
2. Repeat package smoke validation on Windows x64, macOS Intel, Linux x64, Linux ARM64/Raspberry Pi if those packages will be offered.
3. Perform manual visual smoke tests for Help, map layout, station list, packet monitor, and safe-default UI indicators.
4. Produce release notes from `docs/RELEASE_NOTES_TEMPLATE.md`.
5. Replace the temporary macOS `.command` launcher with a signed/notarized `.app` bundle in final macOS packaging work.
6. Generate final public release packages only after cross-platform smoke tests pass.
