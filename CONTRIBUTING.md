# Contributing to APRS Command

Thank you for your interest in contributing to APRS Command — the open-source,
cross-platform successor to UI-View32. All contributions are welcome.

## Quick Start

```bash
git clone https://github.com/KE4CON/APRS-Command.git
cd APRS-Command
dotnet build CrossPlatformAprs.sln -c Debug
dotnet test tests/Aprs.Tests/Aprs.Tests.csproj
dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj --no-build
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The solution
pins the exact SDK version in `global.json`.

## Project Layout

```
src/
  Aprs.Core/           APRS packet models, parser, symbol logic
  Aprs.Transport/      APRS-IS, KISS, Direwolf, serial transport
  Aprs.Services/       Station database, beacon scheduler, messaging
  Aprs.Mapping/        Map tile cache, APRS marker rendering
  Aprs.Desktop/        Avalonia desktop application
  AprsCommand.Contracts/ Versioned public DTOs for developer integrations
  AprsCommand.Api/     Local REST API, WebSocket streams, file hooks
tests/
  Aprs.Tests/          Unit and integration tests (914+ tests)
```

## Making a Contribution

1. **Check existing issues** — your idea may already be tracked or in progress.
2. **Open an issue** before starting significant work so we can discuss scope.
3. **Fork and branch** — create a feature branch from `main`.
4. **Write tests** — all new logic should have corresponding tests in `Aprs.Tests`.
5. **Zero warnings** — the build must produce 0 errors and 0 warnings.
6. **Open a pull request** against `main` with a clear description of what changed and why.

## Coding Standards

- **Language**: C# 12 / .NET 10
- **UI**: Avalonia UI — no platform-specific UI code
- **Nullability**: All files use `#nullable enable` (set in `Directory.Build.props`)
- **Naming**: Follow existing conventions — PascalCase for types and members, camelCase for locals
- **No UI-View32 code**: Do not copy code, icons, or text from UI-View32. Use it only as a feature reference.
- **APRS spec compliance**: Packet formats must conform to the APRS Protocol Reference

## What We're Looking For

- Bug fixes with a test that reproduces the bug
- New APRS packet type support (telemetry display, area objects, etc.)
- RF/TNC hardware compatibility improvements
- Cross-platform fixes (macOS, Linux, Raspberry Pi)
- Accessibility improvements
- Documentation corrections

## What to Avoid

- Windows-only code
- Dependencies that require paid licenses
- Features that require code signing certificates (we do not pay for these for open source)
- Large refactors without prior discussion

## Developer API

APRS Command exposes a REST API, WebSocket event stream, and file hook system for
external integrations. See the **APRS Command Developer API Manual** in the
repository's docs/ folder for the full reference.

## License

By contributing you agree that your contributions will be licensed under the
[GNU General Public License v3](LICENSE).

## Contact

Open an issue on GitHub or find KE4CON on the amateur radio digital modes community.

73 de KE4CON
