#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <runtime-identifier>" >&2
  exit 2
fi

RID="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOLUTION="$REPO_ROOT/CrossPlatformAprs.sln"
PROJECT="$REPO_ROOT/src/Aprs.Desktop/Aprs.Desktop.csproj"
OUTPUT="$REPO_ROOT/artifacts/publish/$RID"

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
require_path "$PROJECT" "desktop project file"
require_path "$REPO_ROOT/README.md" "README.md"
require_path "$REPO_ROOT/src" "src directory"
require_path "$REPO_ROOT/docs" "docs directory"
require_path "$REPO_ROOT/tests" "tests directory"

case "$RID" in
  win-x64|osx-arm64|osx-x64|linux-x64|linux-arm64)
    ;;
  *)
    echo "Unsupported runtime identifier: $RID" >&2
    exit 2
    ;;
esac

echo "Publishing APRS Command for $RID"
echo "Repository root: $REPO_ROOT"
echo "Solution: $SOLUTION"
echo "Desktop project: $PROJECT"
dotnet restore "$SOLUTION"
dotnet build "$SOLUTION" -c Release --no-restore
dotnet test "$SOLUTION" -c Release --no-build
rm -rf "$OUTPUT"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true -o "$OUTPUT" /p:PublishSingleFile=false /p:DebugType=none /p:DebugSymbols=false

echo "APRS Command publish output: $OUTPUT"
