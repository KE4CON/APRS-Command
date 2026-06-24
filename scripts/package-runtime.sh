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
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
PACKAGES_DIR="$REPO_ROOT/artifacts/packages"
CHECKSUMS_DIR="$REPO_ROOT/artifacts/checksums"
RELEASE_NOTES_DIR="$REPO_ROOT/artifacts/release-notes"
STAGING_ROOT="$REPO_ROOT/artifacts/package-staging"
STAGING_DIR="$STAGING_ROOT/APRS-Command-$RID"
PACKAGE_BASENAME="APRS-Command-$RID"

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
  win-x64)
    PACKAGE_PATH="$PACKAGES_DIR/$PACKAGE_BASENAME.zip"
    TEST_PACKAGE_PATH="$PACKAGES_DIR/$PACKAGE_BASENAME-test.zip"
    ;;
  osx-arm64|osx-x64|linux-x64|linux-arm64)
    PACKAGE_PATH="$PACKAGES_DIR/$PACKAGE_BASENAME.tar.gz"
    TEST_PACKAGE_PATH="$PACKAGES_DIR/$PACKAGE_BASENAME-test.tar.gz"
    ;;
  *)
    echo "Unsupported runtime identifier: $RID" >&2
    exit 2
    ;;
esac

write_checksum() {
  local package_path="$1"
  local checksum_path="$CHECKSUMS_DIR/$(basename "$package_path").sha256"

  if command -v shasum >/dev/null 2>&1; then
    (cd "$PACKAGES_DIR" && shasum -a 256 "$(basename "$package_path")" > "$checksum_path")
  elif command -v sha256sum >/dev/null 2>&1; then
    (cd "$PACKAGES_DIR" && sha256sum "$(basename "$package_path")" > "$checksum_path")
  else
    echo "No SHA256 tool found. Install shasum or sha256sum." >&2
    exit 1
  fi
}

echo "Packaging APRS Command portable build for $RID"
echo "Repository root: $REPO_ROOT"
echo "Solution: $SOLUTION"
echo "Desktop project: $PROJECT"
"$SCRIPT_DIR/publish-runtime.sh" "$RID"

mkdir -p "$PACKAGES_DIR" "$CHECKSUMS_DIR" "$RELEASE_NOTES_DIR" "$STAGING_ROOT"
rm -rf "$STAGING_DIR" \
  "$PACKAGE_PATH" \
  "$TEST_PACKAGE_PATH" \
  "$CHECKSUMS_DIR/$(basename "$PACKAGE_PATH").sha256" \
  "$CHECKSUMS_DIR/$(basename "$TEST_PACKAGE_PATH").sha256"
mkdir -p "$STAGING_DIR"

cp -R "$PUBLISH_DIR"/. "$STAGING_DIR"/
mkdir -p "$STAGING_DIR/docs" "$STAGING_DIR/packaging"

cp "$REPO_ROOT/README.md" "$STAGING_DIR/README.md"
cp "$REPO_ROOT/docs/QUICK_START.md" "$STAGING_DIR/QUICK_START.md"
cp "$REPO_ROOT/docs/INSTALLATION_GUIDE.md" "$STAGING_DIR/INSTALLATION_GUIDE.md"
cp "$REPO_ROOT/docs/SAFETY_AND_TRANSMIT_GUIDE.md" "$STAGING_DIR/SAFETY_AND_TRANSMIT_GUIDE.md"
cp "$REPO_ROOT/docs/TROUBLESHOOTING.md" "$STAGING_DIR/TROUBLESHOOTING.md"
cp "$REPO_ROOT/docs/RELEASE_NOTES_TEMPLATE.md" "$RELEASE_NOTES_DIR/RELEASE_NOTES_TEMPLATE.md"

if [[ -f "$REPO_ROOT/LICENSE" ]]; then
  cp "$REPO_ROOT/LICENSE" "$STAGING_DIR/LICENSE"
elif [[ -f "$REPO_ROOT/LICENSE.md" ]]; then
  cp "$REPO_ROOT/LICENSE.md" "$STAGING_DIR/LICENSE.md"
else
  printf 'APRS Command license: TBD\n' > "$STAGING_DIR/LICENSE_PLACEHOLDER.txt"
fi

if [[ -d "$REPO_ROOT/packaging/templates" ]]; then
  cp -R "$REPO_ROOT/packaging/templates" "$STAGING_DIR/packaging/templates"
fi

cat > "$STAGING_DIR/VERSION.txt" <<EOF
APRS Command
Version: 0.0.0-dev
Runtime: $RID
Package: $(basename "$PACKAGE_PATH")
Build date: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
Safe defaults: APRS-IS transmit disabled; RF transmit disabled; iGate disabled; digipeater disabled; beaconing disabled; weather beaconing disabled; REST API disabled; WebSocket disabled; file hooks disabled; plugin loading disabled; replay/simulation/training cannot transmit.
EOF

if [[ "$RID" == linux-* ]]; then
  cp "$REPO_ROOT/packaging/templates/aprs-command.desktop.template" "$STAGING_DIR/aprs-command.desktop.template"
fi

if [[ "$RID" == osx-* ]]; then
  cat > "$STAGING_DIR/APRS Command.command" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$APP_DIR"

if [[ ! -x ./Aprs.Desktop ]]; then
  chmod +x ./Aprs.Desktop
fi

exec ./Aprs.Desktop "$@"
EOF
  chmod +x "$STAGING_DIR/APRS Command.command"
fi

chmod +x "$STAGING_DIR/Aprs.Desktop" 2>/dev/null || true

case "$RID" in
  win-x64)
    (cd "$STAGING_ROOT" && zip -qr "$PACKAGE_PATH" "APRS-Command-$RID")
    ;;
  *)
    (cd "$STAGING_ROOT" && tar -czf "$PACKAGE_PATH" "APRS-Command-$RID")
    ;;
esac

cp "$PACKAGE_PATH" "$TEST_PACKAGE_PATH"
write_checksum "$PACKAGE_PATH"
write_checksum "$TEST_PACKAGE_PATH"

echo "APRS Command package output: $PACKAGE_PATH"
echo "APRS Command checksum output: $CHECKSUMS_DIR/$(basename "$PACKAGE_PATH").sha256"
echo "APRS Command test package output: $TEST_PACKAGE_PATH"
echo "APRS Command test checksum output: $CHECKSUMS_DIR/$(basename "$TEST_PACKAGE_PATH").sha256"
