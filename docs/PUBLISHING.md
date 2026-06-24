# APRS Command Publishing Guidance

Phase 14.13 upgrades APRS Command to target .NET 10 LTS. Phase 15 verifies first-run setup and publish output before final packaging.

This document summarizes publish guidance. It does not create installers.

See also:

- `docs/FIRST_RUN_SETUP.md`
- `docs/PACKAGING_PREPARATION.md`
- `docs/BUILD_AND_PUBLISH.md`

## SDK Requirement

Install the .NET 10 SDK.

```bash
dotnet --version
dotnet restore
dotnet build
dotnet test
```

The repository includes `global.json` pinned to the current .NET 10 SDK feature band.

## Desktop Run

```bash
dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj
```

## Runtime Identifiers

Phase 15.2 includes repeatable publish profiles and scripts for:

- Windows x64: `win-x64`
- macOS Apple Silicon: `osx-arm64`
- macOS Intel, if supported: `osx-x64`
- Linux x64: `linux-x64`
- Linux ARM64: `linux-arm64`
- Raspberry Pi 5 ARM64: `linux-arm64`

## Repeatable Publish Scripts

These scripts restore, build in Release, test, and publish the desktop app into `artifacts/publish/<runtime-identifier>/`:

```bash
./scripts/publish-win-x64.sh
./scripts/publish-osx-arm64.sh
./scripts/publish-osx-x64.sh
./scripts/publish-linux-x64.sh
./scripts/publish-linux-arm64.sh
```

Use `./scripts/publish-all.sh` to run every supported target. Windows users can also run `.\scripts\publish-win-x64.ps1`.

Direct publish profiles are available under `src/Aprs.Desktop/Properties/PublishProfiles/`.

Phase 15.3 and later should confirm Avalonia app packaging, signing/notarization needs, settings storage, first-run setup, and platform-specific dependencies.

## Safety

Packaging must preserve safe defaults:

- APRS-IS transmit disabled unless explicitly configured
- RF transmit disabled unless explicitly configured
- API/WebSocket/file/plugin transmit paths blocked by default
- replay, simulation, training, iGate, digipeater, beacon, object, message, and weather transmit flows gated by central safety checks
