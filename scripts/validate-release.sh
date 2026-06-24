#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOLUTION="$REPO_ROOT/CrossPlatformAprs.sln"
DESKTOP_PROJECT="$REPO_ROOT/src/Aprs.Desktop/Aprs.Desktop.csproj"

require_path() {
  local path="$1"
  local description="$2"

  if [[ ! -e "$path" ]]; then
    echo "Could not locate $description at: $path" >&2
    echo "Run this script from the APRS Command repository, or keep scripts/ beside the repository root." >&2
    exit 1
  fi
}

require_path "$SOLUTION" "solution file"
require_path "$DESKTOP_PROJECT" "desktop project file"
require_path "$REPO_ROOT/README.md" "README.md"
require_path "$REPO_ROOT/src" "src directory"
require_path "$REPO_ROOT/docs" "docs directory"
require_path "$REPO_ROOT/tests" "tests directory"

echo "APRS Command final release validation"
echo "Repository: $REPO_ROOT"
echo "Solution: $SOLUTION"
echo "Desktop project: $DESKTOP_PROJECT"
echo

DOTNET_VERSION="$(dotnet --version)"
echo ".NET SDK: $DOTNET_VERSION"
case "$DOTNET_VERSION" in
  10.*)
    ;;
  *)
    echo "Expected a .NET 10 SDK for APRS Command release validation." >&2
    exit 1
    ;;
esac

echo
echo "Restoring, building, and testing..."
dotnet restore "$SOLUTION"
dotnet build "$SOLUTION" --no-restore
dotnet test "$SOLUTION" --no-build

echo
echo "Checking required release documentation..."
required_docs=(
  "README.md"
  "docs/USER_MANUAL.md"
  "docs/QUICK_START.md"
  "docs/INSTALLATION_GUIDE.md"
  "docs/FIRST_RUN_SETUP.md"
  "docs/SAFETY_AND_TRANSMIT_GUIDE.md"
  "docs/TROUBLESHOOTING.md"
  "docs/GLOSSARY.md"
  "docs/BUILD_AND_PUBLISH.md"
  "docs/PACKAGING_PREPARATION.md"
  "docs/INSTALLER_AND_PACKAGE_PLAN.md"
  "docs/RELEASE_NOTES_TEMPLATE.md"
  "docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md"
)

for relative_path in "${required_docs[@]}"; do
  if [[ ! -f "$REPO_ROOT/$relative_path" ]]; then
    echo "Missing required documentation file: $relative_path" >&2
    exit 1
  fi
done

echo "Checking required release scripts..."
required_scripts=(
  "scripts/publish-runtime.sh"
  "scripts/publish-win-x64.sh"
  "scripts/publish-osx-arm64.sh"
  "scripts/publish-osx-x64.sh"
  "scripts/publish-linux-x64.sh"
  "scripts/publish-linux-arm64.sh"
  "scripts/package-runtime.sh"
  "scripts/package-win-x64.sh"
  "scripts/package-osx-arm64.sh"
  "scripts/package-osx-x64.sh"
  "scripts/package-linux-x64.sh"
  "scripts/package-linux-arm64.sh"
  "scripts/package-all.sh"
)

for relative_path in "${required_scripts[@]}"; do
  if [[ ! -f "$REPO_ROOT/$relative_path" ]]; then
    echo "Missing required release script: $relative_path" >&2
    exit 1
  fi
done

echo "Checking Help docs copy configuration..."
if ! grep -Fq 'docs\*.md' "$DESKTOP_PROJECT" || ! grep -q "CopyToPublishDirectory" "$DESKTOP_PROJECT"; then
  echo "Aprs.Desktop project does not appear to copy docs into publish output." >&2
  exit 1
fi

echo "Checking for obvious unsafe credentials or transmit-enabled placeholders..."
scan_targets=(
  "$REPO_ROOT/README.md"
  "$REPO_ROOT/docs"
  "$REPO_ROOT/examples"
  "$REPO_ROOT/scripts"
  "$REPO_ROOT/packaging/templates"
)

if command -v rg >/dev/null 2>&1; then
  if rg -n -i --glob '!validate-release.sh' "(api_key|password=|passcode=|secret=|BEGIN PRIVATE KEY|TransmitEnabled=true|transmitEnabled\"[[:space:]]*:[[:space:]]*true|RF transmit enabled|APRS-IS transmit enabled|[?&]token=[A-Za-z0-9._-]{20,}|token=[A-Za-z0-9._-]{20,})" "${scan_targets[@]}"; then
    echo "Found possible credentials or unsafe transmit-enabled placeholders. Review the matches above." >&2
    exit 1
  fi
else
  echo "ripgrep not found; skipping optional credential text scan."
fi

echo
echo "Validation completed. Manual platform/package smoke tests are still required before public release."
echo "No hardware, live APRS-IS connection, weather device, internet credentials, or transmit path was required."
