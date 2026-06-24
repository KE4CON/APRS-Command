#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
"$SCRIPT_DIR/package-runtime.sh" osx-arm64

REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PACKAGE_DIR="$REPO_ROOT/artifacts/packages"
CHECKSUM_DIR="$REPO_ROOT/artifacts/checksums"
SOURCE_PACKAGE="$PACKAGE_DIR/APRS-Command-osx-arm64.tar.gz"
TEST_PACKAGE="$PACKAGE_DIR/APRS-Command-osx-arm64-test.tar.gz"
TEST_CHECKSUM="$CHECKSUM_DIR/APRS-Command-osx-arm64-test.tar.gz.sha256"

cp "$SOURCE_PACKAGE" "$TEST_PACKAGE"
if command -v shasum >/dev/null 2>&1; then
  (cd "$PACKAGE_DIR" && shasum -a 256 "$(basename "$TEST_PACKAGE")" > "$TEST_CHECKSUM")
elif command -v sha256sum >/dev/null 2>&1; then
  (cd "$PACKAGE_DIR" && sha256sum "$(basename "$TEST_PACKAGE")" > "$TEST_CHECKSUM")
else
  echo "No SHA256 tool found. Install shasum or sha256sum." >&2
  exit 1
fi

echo "APRS Command test package output: $TEST_PACKAGE"
echo "APRS Command test checksum output: $TEST_CHECKSUM"
