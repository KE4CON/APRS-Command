# APRS Command Final Release Validation Checklist

Use this checklist before producing public APRS Command release packages. This is a release readiness, safety, documentation, and packaging validation pass only. It must not add features, enable transmit, require hardware, or require credentials.

Record the release version, commit, date, and validator before approving packages.

```text
Release version:
Git commit:
Validation date:
Validator:
Platforms checked:
Known issues approved by:
```

## Source And Build Validation

- [ ] `dotnet --version` reports a .NET 10 SDK.
- [ ] Repository root is confirmed by `test -f CrossPlatformAprs.sln && test -f README.md && test -d src && test -d docs && test -d tests`.
- [ ] Actual solution path is recorded as `CrossPlatformAprs.sln`.
- [ ] Actual desktop project path is recorded as `src/Aprs.Desktop/Aprs.Desktop.csproj`.
- [ ] Actual test project path is recorded as `tests/Aprs.Tests/Aprs.Tests.csproj`.
- [ ] `dotnet restore` succeeds from the repository root.
- [ ] `dotnet build` succeeds from the repository root.
- [ ] `dotnet test` succeeds from the repository root.
- [ ] `dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj` opens APRS Command.
- [ ] No test requires live APRS-IS, RF, TNC, Direwolf, AGWPE, serial hardware, GPS hardware, weather hardware, internet access, API credentials, APRS-IS passcodes, signing keys, or private tokens.
- [ ] The repository builds without changing the target framework or transmit behavior.
- [ ] `./scripts/validate-release.sh` succeeds on the release source tree.

## Unit Test Validation

- [ ] APRS parser tests pass.
- [ ] Station database, aging, trail, tactical label, and object manager tests pass.
- [ ] APRS-IS, TCP KISS, Serial KISS, Direwolf, and AGWPE tests pass using fake/test transports only.
- [ ] Beacon formatter, beacon scheduler, SmartBeaconing, weather beacon, iGate, and digipeater tests pass without live transmit.
- [ ] GPS, gpsd, weather driver, weather formatter, and weather display tests pass without hardware or internet.
- [ ] REST API, WebSocket, file hook, plugin/driver framework, replay, simulation, training, alerts, geofence, and log tests pass with safe defaults.
- [ ] Documentation, build, publish, installer, package, portable package, and final release validation tests pass.

## Desktop Launch Validation

- [ ] APRS Command launches from source with `dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj`.
- [ ] The main window title says `APRS Command`.
- [ ] The application opens without prompting for real credentials.
- [ ] Closing the desktop app exits cleanly.
- [ ] Startup does not attempt APRS-IS, RF/TNC, GPS, weather, REST API, WebSocket, file hook, plugin, replay, simulation, or training connections.

## First-Run Setup Validation

- [ ] First-run setup is visible or accessible from Settings.
- [ ] Application data, logs, packet logs, event logs, map cache, exports, file hooks, plugins, backups, training, and replay folders are user-writable paths.
- [ ] Placeholder configuration files contain fake/sample values only.
- [ ] Example callsigns are fake, such as `N0CALL`, `SIM001`, `TESTWX`, and `OBJTEST`.
- [ ] First-run setup does not enable APRS-IS transmit, RF transmit, beaconing, iGate, digipeater, object transmit, message transmit, weather beaconing, REST API, WebSocket, file hooks, or plugin loading.

## UI Layout Validation

- [ ] The restored original map-first layout is present.
- [ ] The map is visible at startup.
- [ ] The station list is readable on the right side.
- [ ] Station status and selected station details are readable.
- [ ] The packet monitor is visible below the map.
- [ ] Messages, Objects, Weather, Events, Event Bus, Replay, RF Diag, and Alerts buttons are accessible in the restored original lower-right feature panel.
- [ ] All eight lower-right feature buttons are visible together at normal desktop/laptop sizes; Messages is not the only visible feature button.
- [ ] There is only one lower-right feature navigation/control set.
- [ ] Clicking Messages visibly shows the Message Center / Messages panel.
- [ ] Clicking Objects visibly shows the Object Manager / Objects panel.
- [ ] Clicking Weather visibly shows the Weather panel.
- [ ] Clicking Events visibly shows the Decoded Event Log / Events panel.
- [ ] Clicking Event Bus visibly shows the Event Monitor / Event Bus panel.
- [ ] Clicking Replay visibly shows the Replay panel.
- [ ] Clicking RF Diag visibly shows the RF Diagnostics panel.
- [ ] Clicking Alerts visibly shows the Alert Rules / Alerts panel.
- [ ] Each feature button updates the visible feature title, description, and content area.
- [ ] The Help button or menu opens the in-app Help viewer.
- [ ] There is no crowded failed top-navigation layout.
- [ ] No obvious text overlaps, clipped labels, or unusable controls appear at normal desktop sizes.

## In-App Help Validation

- [ ] Help viewer opens from the desktop app.
- [ ] User Manual loads.
- [ ] Quick Start loads.
- [ ] Installation Guide loads.
- [ ] First-Run Setup loads.
- [ ] Safety and Transmit Guide loads.
- [ ] Troubleshooting loads.
- [ ] Glossary loads.
- [ ] A missing help file shows a friendly message and does not crash the app.
- [ ] Help search/filter works for common terms such as `transmit`, `APRS-IS`, `weather`, and `troubleshooting`.
- [ ] Published and packaged builds include the `docs/` folder for offline Help.

## Documentation Validation

- [ ] README links to the User Manual, Quick Start, Installation Guide, First-Run Setup, Safety and Transmit Guide, Troubleshooting, Glossary, and Developer Guide.
- [ ] Build and publish documentation links to this final release validation checklist.
- [ ] Packaging preparation documentation links to this final release validation checklist.
- [ ] Installer and package planning documentation links to this final release validation checklist.
- [ ] Documentation consistently uses `APRS Command` for the user-facing application name.
- [ ] Documentation does not describe the failed crowded top-navigation layout.
- [ ] Safety documentation clearly states that transmit is disabled by default.
- [ ] Extension documentation consistently refers to APRS Command public data contracts, APRS Command REST API, APRS Command WebSocket event streams, APRS Command file import/export hooks, and APRS Command plugin/driver framework.
- [ ] Docs and examples do not contain real APRS-IS passcodes, API tokens, signing secrets, private credentials, GitHub tokens, weather credentials, or personal secrets.

## Package Script Validation

- [ ] `scripts/publish-runtime.sh` exists and restores, builds, tests, cleans the runtime-specific generated publish folder, and publishes.
- [ ] `scripts/package-runtime.sh` exists and stages publish output, docs, templates, release notes, and checksums.
- [ ] Shared publish/package scripts validate the repository root and print the solution and desktop project path before building.
- [ ] Shared publish/package scripts use `src/Aprs.Desktop/Aprs.Desktop.csproj`, not a folder shorthand, for publish commands.
- [ ] Runtime-specific publish scripts exist for `win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`, and `linux-arm64`.
- [ ] Runtime-specific portable package scripts exist for `win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`, and `linux-arm64`.
- [ ] Windows PowerShell helper scripts exist for Windows users where provided.
- [ ] Scripts do not contain real passcodes, API tokens, signing secrets, private credentials, GitHub tokens, weather credentials, or private keys.
- [ ] Scripts do not enable transmit, beaconing, iGate, digipeater, REST API, WebSocket, file hooks, or plugin loading.
- [ ] Package names use `APRS Command` / `APRS-Command` consistently.

## Portable Package Contents Validation

Validate every generated package:

- [ ] Windows x64 ZIP: `artifacts/packages/APRS-Command-win-x64.zip`.
- [ ] Windows x64 test ZIP: `artifacts/packages/APRS-Command-win-x64-test.zip`.
- [ ] macOS Apple Silicon tar.gz support: `artifacts/packages/APRS-Command-osx-arm64.tar.gz`.
- [ ] macOS Apple Silicon test tar.gz: `artifacts/packages/APRS-Command-osx-arm64-test.tar.gz`.
- [ ] macOS Intel tar.gz support: `artifacts/packages/APRS-Command-osx-x64.tar.gz`.
- [ ] macOS Intel test tar.gz: `artifacts/packages/APRS-Command-osx-x64-test.tar.gz`.
- [ ] Linux x64 tar.gz: `artifacts/packages/APRS-Command-linux-x64.tar.gz`.
- [ ] Linux x64 test tar.gz: `artifacts/packages/APRS-Command-linux-x64-test.tar.gz`.
- [ ] Linux ARM64 tar.gz: `artifacts/packages/APRS-Command-linux-arm64.tar.gz`.
- [ ] Linux ARM64 test tar.gz: `artifacts/packages/APRS-Command-linux-arm64-test.tar.gz`.
- [ ] Raspberry Pi/Linux ARM64 notes are included in docs.

For each package:

- [ ] Executable or app files are included.
- [ ] `docs/` help files are included.
- [ ] README is included.
- [ ] Quick Start is included.
- [ ] Safety and Transmit Guide is included.
- [ ] Troubleshooting guide is included.
- [ ] Release notes template or release notes are included.
- [ ] Packaging templates or platform notes are included where expected.
- [ ] No credentials, passcodes, tokens, signing keys, private keys, or private callsigns are included.
- [ ] SHA256 checksum is generated under `artifacts/checksums/`.
- [ ] Package filename uses `APRS-Command`.
- [ ] Current-platform test packages, when produced before public release, use a `-test` package filename and matching checksum.
- [ ] Extracted package opens APRS Command without enabling transmit.
- [ ] macOS portable packages include executable `Aprs.Desktop` and Finder-friendly `APRS Command.command`.
- [ ] macOS unsigned portable launch instructions mention executable permissions, quarantine removal, Terminal launch, and launch-error capture.

### macOS Apple Silicon Portable Test Validation

From the repository root:

```bash
ls -la artifacts/publish/osx-arm64/
ls -la artifacts/packages/APRS-Command-osx-arm64-test.tar.gz
ls -la artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256
cat artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256
```

Extract the package to the documented test location:

```bash
rm -rf /tmp/aprs-command-osx-arm64-test
mkdir -p /tmp/aprs-command-osx-arm64-test
tar -xzf artifacts/packages/APRS-Command-osx-arm64-test.tar.gz -C /tmp/aprs-command-osx-arm64-test
cd /tmp/aprs-command-osx-arm64-test/APRS-Command-osx-arm64
ls -la
```

Validate the exact launch files:

```bash
test -x ./Aprs.Desktop
test -x "./APRS Command.command"
```

Launch the actual app executable:

```bash
xattr -dr com.apple.quarantine . 2>/dev/null || true
./Aprs.Desktop
```

Or launch the portable-test command file:

```bash
open "./APRS Command.command"
```

Capture launch errors if startup fails:

```bash
./Aprs.Desktop 2>&1 | tee launch-error.txt
```

## Safety Defaults Validation

Verify these defaults in code, UI, docs, and packaged output:

- [ ] APRS-IS transmit disabled.
- [ ] RF transmit disabled.
- [ ] iGate disabled.
- [ ] Digipeater disabled.
- [ ] Beaconing disabled.
- [ ] Weather beaconing disabled.
- [ ] Object transmit disabled until explicitly configured.
- [ ] Message transmit disabled until explicitly configured.
- [ ] REST API disabled.
- [ ] WebSocket disabled.
- [ ] File hooks disabled.
- [ ] Plugin loading disabled.
- [ ] Replay cannot transmit.
- [ ] Simulation cannot transmit.
- [ ] Training mode cannot transmit.
- [ ] Imported transmit requests are blocked by default.
- [ ] Plugin transmit requests are blocked by default.
- [ ] API transmit requests are blocked by default.
- [ ] Centralized transmit safety remains in force for APRS-IS, RF/TNC, beacon, object, message, weather, iGate, digipeater, API, file hook, plugin, replay, simulation, and training flows.

## API WebSocket File Hook Plugin Safety Validation

- [ ] Local REST API is disabled by default.
- [ ] WebSocket event streams are disabled by default.
- [ ] File import/export hooks are disabled by default.
- [ ] Plugin loading is disabled by default.
- [ ] Public data contracts remain separate from internal domain models.
- [ ] Source tagging is preserved for imported, generated, replayed, simulated, external, and plugin-provided data.
- [ ] API, WebSocket, file hook, and plugin transmit requests cannot bypass centralized safety checks.
- [ ] Example API, WebSocket, file hook, and plugin payloads use fake data only.

## Replay Simulation Training Safety Validation

- [ ] Replay sessions are disabled until explicitly started by the user.
- [ ] Simulation is disabled until explicitly started by the user.
- [ ] Training mode is disabled until explicitly started by the user.
- [ ] Replay, simulation, and training packets are source-tagged.
- [ ] Replay, simulation, and training cannot transmit.
- [ ] Replay, simulation, and training cannot enable APRS-IS transmit, RF transmit, beaconing, iGate, digipeater, object transmit, message transmit, or weather beaconing.

## Security And Credential Validation

- [ ] No real APRS-IS passcodes are present in the repository.
- [ ] No API tokens are present in the repository.
- [ ] No signing secrets are present in the repository.
- [ ] No private credentials are present in docs, examples, tests, scripts, templates, or generated release artifacts.
- [ ] Example callsigns are fake, such as `N0CALL`, `SIM001`, `TESTWX`, and `OBJTEST`.
- [ ] Scripts do not contain real credentials.
- [ ] Package artifacts do not include secrets.
- [ ] Release notes do not disclose private credentials, private paths, or private callsigns.

## Platform Notes

Windows:

- [ ] Launch app from the extracted Windows x64 ZIP.
- [ ] Complete first-run setup review.
- [ ] Open Help.
- [ ] Verify safe defaults.
- [ ] Exit app cleanly.

macOS:

- [ ] Launch app from the extracted macOS Apple Silicon package.
- [ ] Launch app from the extracted macOS Intel package if Intel support is being shipped.
- [ ] Note unsigned build or Gatekeeper behavior where applicable.
- [ ] Open Help.
- [ ] Verify safe defaults.
- [ ] Exit app cleanly.

Linux:

- [ ] Launch app from the extracted Linux x64 package.
- [ ] Verify serial permission notes mention groups such as `dialout`, `uucp`, or `tty`.
- [ ] Open Help.
- [ ] Verify safe defaults.
- [ ] Exit app cleanly.

Raspberry Pi / Linux ARM64:

- [ ] Launch the `linux-arm64` build.
- [ ] Verify UI is readable.
- [ ] Verify map and Help load.
- [ ] Verify storage paths for logs, maps, replay, training, and exports.
- [ ] Verify safe defaults.
- [ ] Confirm no root execution is required for normal use.

## Release Notes And Checksum Validation

- [ ] Release notes template exists.
- [ ] Release notes include a version placeholder.
- [ ] Release notes include package names and runtime identifiers.
- [ ] Package list is recorded.
- [ ] SHA256 checksums are generated for each package.
- [ ] Known issues section exists.
- [ ] Safe default statement exists.
- [ ] Documentation links are included.
- [ ] Checksums match the generated package files.
- [ ] Release notes do not claim final installers, signing, notarization, package-manager publication, or transmit support unless those items are actually complete.

## Known Issues

Use this section during release validation. Do not delete known issues unless they are verified fixed.

```text
Issue:
Affected platform/package:
Severity:
Workaround:
Approved for release: yes/no
Approver:
```

## Release Approval Checklist

- [ ] Source build validation passed.
- [ ] Unit tests passed.
- [ ] Desktop launch validation passed.
- [ ] First-run setup validation passed.
- [ ] UI layout validation passed.
- [ ] In-app Help validation passed.
- [ ] Documentation validation passed.
- [ ] Package script validation passed.
- [ ] Portable package contents validation passed.
- [ ] Safety defaults validation passed.
- [ ] API/WebSocket/file hook/plugin safety validation passed.
- [ ] Replay/simulation/training safety validation passed.
- [ ] Platform smoke tests passed or approved known issues are recorded.
- [ ] Release notes and checksums are complete.
- [ ] Security and credential validation passed.
- [ ] Final approver confirms APRS Command remains receive-first and transmit-disabled by default.

```text
Final release approval:
Approved by:
Date:
Signature/initials:
```
