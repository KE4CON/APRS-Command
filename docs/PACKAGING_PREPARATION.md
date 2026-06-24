# APRS Command Packaging Preparation

Phase 15.1 prepares APRS Command for packaging. Phase 15.2 adds repeatable publish scripts and MSBuild publish profiles. Phase 15.6 plans final installer/package strategy. Phase 15.7 adds portable ZIP and `tar.gz` package scripts. These phases do not create full installers.

## Packaging Goals

- Preserve receive-only and transmit-disabled defaults.
- Include first-run folder setup.
- Keep logs, maps, exports, file hooks, plugins, training data, and replay data in user-writable application data folders.
- Keep real credentials out of repository files and generated placeholders.
- Keep internal project names stable unless a later explicit rename task is scheduled.

## Publish Modes

Framework-dependent publish:
- Smaller output.
- Requires the target machine to have a compatible .NET runtime installed.

Self-contained publish:
- Larger output.
- Carries the .NET runtime with APRS Command.
- Preferred for first-user testing when runtime installation should be minimized.

## Repeatable Publish Commands

Phase 15.2 publish scripts restore, build in Release, run tests, and publish the desktop app into `artifacts/publish/<runtime-identifier>/`:

```bash
./scripts/publish-win-x64.sh
./scripts/publish-osx-arm64.sh
./scripts/publish-osx-x64.sh
./scripts/publish-linux-x64.sh
./scripts/publish-linux-arm64.sh
./scripts/publish-all.sh
```

Raspberry Pi 5 ARM64 uses the `linux-arm64` runtime identifier:

```bash
./scripts/publish-linux-arm64.sh
```

The matching MSBuild publish profiles are stored in `src/Aprs.Desktop/Properties/PublishProfiles/`.

Direct publish profile examples:

```bash
dotnet publish src/Aprs.Desktop/Aprs.Desktop.csproj -c Release /p:PublishProfile=win-x64
dotnet publish src/Aprs.Desktop/Aprs.Desktop.csproj -c Release /p:PublishProfile=osx-arm64
dotnet publish src/Aprs.Desktop/Aprs.Desktop.csproj -c Release /p:PublishProfile=linux-arm64
```

See `docs/BUILD_AND_PUBLISH.md` for the full build and publish workflow.

See `docs/INSTALLER_AND_PACKAGE_PLAN.md` for the planned Windows, macOS, Linux, and Raspberry Pi package strategy before final installers are created. Use `docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md` as the final go/no-go checklist before public package publication.

Portable package commands:

```bash
./scripts/package-win-x64.sh
./scripts/package-osx-arm64.sh
./scripts/package-osx-x64.sh
./scripts/package-linux-x64.sh
./scripts/package-linux-arm64.sh
./scripts/package-all.sh
```

Package archives are written to `artifacts/packages/`, and SHA256 checksum files are written to `artifacts/checksums/`.

## Platform Packaging Plan

Windows:
- Verify `win-x64` publish output.
- Later installer work should add Start Menu shortcuts, uninstall metadata, and app display name `APRS Command`.

macOS Apple Silicon:
- Verify `osx-arm64` publish output.
- Later package work should handle `.app` layout, signing, and notarization.

macOS Intel:
- Verify `osx-x64` publish output while Intel support remains planned.

Linux x64:
- Verify `linux-x64` publish output.
- Later package work should add `.desktop` metadata and icon/shortcut display name `APRS Command`.

Linux ARM64 / Raspberry Pi:
- Verify `linux-arm64` publish output.
- Document user-writable storage for maps, logs, and replay files.

## Stored Data

The first-run setup service prepares this layout under the application data root:

- `config/`
- `logs/`
- `packet-logs/`
- `event-logs/`
- `maps/`
- `map-cache/`
- `exports/`
- `file-hooks/`
- `plugins/`
- `backups/`
- `training/`
- `replay/`

## Safety

Packaging must preserve these defaults:

- transmit disabled
- APRS-IS transmit disabled
- RF transmit disabled
- iGate disabled
- digipeater disabled
- beaconing disabled
- weather beaconing disabled
- REST API disabled
- WebSocket disabled
- file hooks disabled
- plugin loading disabled

No installer, publish profile, or first-run placeholder should include real callsigns, passcodes, API tokens, weather credentials, or plugin credentials.
