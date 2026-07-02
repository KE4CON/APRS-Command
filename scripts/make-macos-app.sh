#!/usr/bin/env bash
# ============================================================
# APRS Command — macOS .app bundle + .dmg
#
# No Apple Developer account or code signing required.
# Users bypass Gatekeeper with right-click → Open on first launch.
# This is standard for open-source macOS software.
#
# Usage:
#   bash scripts/make-macos-app.sh [osx-arm64|osx-x64]
#
# Requirements (macOS only):
#   - .NET 10 SDK
#   - create-dmg (optional, brew install create-dmg — falls back to hdiutil)
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
RID="${1:-osx-arm64}"

if [[ "$RID" != "osx-arm64" && "$RID" != "osx-x64" ]]; then
  echo "Usage: $0 [osx-arm64|osx-x64]" >&2
  exit 2
fi

# Derive version from git tag, fall back to dev
VERSION="$(git -C "$REPO_ROOT" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo "0.0.0-dev")"
ARCH="${RID#osx-}"
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
INSTALLER_DIR="$REPO_ROOT/artifacts/installers"
APP_NAME="APRS Command"
APP_BUNDLE="$INSTALLER_DIR/$APP_NAME.app"
DMG_OUT="$INSTALLER_DIR/APRSCommand-$RID.dmg"

mkdir -p "$INSTALLER_DIR"

# ── 1. Publish ────────────────────────────────────────────────────────────────
if [[ ! -f "$PUBLISH_DIR/Aprs.Desktop" ]]; then
  echo "Publishing $RID..."
  dotnet publish "$REPO_ROOT/src/Aprs.Desktop/Aprs.Desktop.csproj" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=false -p:PublishReadyToRun=true \
    -o "$PUBLISH_DIR"
fi

# ── 2. .app bundle ────────────────────────────────────────────────────────────
echo "Building $APP_NAME.app..."
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS" "$APP_BUNDLE/Contents/Resources"
cp -R "$PUBLISH_DIR"/. "$APP_BUNDLE/Contents/MacOS/"
chmod +x "$APP_BUNDLE/Contents/MacOS/Aprs.Desktop"

cat > "$APP_BUNDLE/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>              <string>APRS Command</string>
  <key>CFBundleDisplayName</key>       <string>APRS Command</string>
  <key>CFBundleIdentifier</key>        <string>com.ke4con.aprs-command</string>
  <key>CFBundleVersion</key>           <string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key>        <string>Aprs.Desktop</string>
  <key>CFBundlePackageType</key>       <string>APPL</string>
  <key>LSMinimumSystemVersion</key>    <string>12.0</string>
  <key>NSHighResolutionCapable</key>   <true/>
  <key>NSHumanReadableCopyright</key>  <string>Copyright 2026 KE4CON. GPL v3.</string>
  <key>LSArchitecturePriority</key>
  <array><string>$ARCH</string></array>
  <key>NSAppTransportSecurity</key>
  <dict><key>NSAllowsLocalNetworking</key><true/></dict>
</dict>
</plist>
PLIST

echo "  → $APP_BUNDLE"

# ── 3. .dmg ───────────────────────────────────────────────────────────────────
rm -f "$DMG_OUT"
DMG_STAGING="$(mktemp -d)"
trap "rm -rf '$DMG_STAGING'" EXIT
cp -R "$APP_BUNDLE" "$DMG_STAGING/"

if command -v create-dmg >/dev/null 2>&1; then
  echo "Building DMG with create-dmg..."
  create-dmg \
    --volname "APRS Command" \
    --window-pos 200 120 --window-size 560 400 \
    --icon-size 100 \
    --icon "APRS Command.app" 140 190 \
    --hide-extension "APRS Command.app" \
    --app-drop-link 420 190 \
    "$DMG_OUT" "$DMG_STAGING/" 2>/dev/null || {
      echo "  create-dmg failed, falling back to hdiutil..."
      hdiutil create -volname "APRS Command" -srcfolder "$DMG_STAGING" \
        -ov -format UDZO "$DMG_OUT"
    }
elif command -v hdiutil >/dev/null 2>&1; then
  echo "Building DMG with hdiutil..."
  hdiutil create -volname "APRS Command" -srcfolder "$DMG_STAGING" \
    -ov -format UDZO "$DMG_OUT"
else
  echo "Neither create-dmg nor hdiutil found."
  echo "App bundle is ready at: $APP_BUNDLE"
  exit 0
fi

echo ""
echo "Done."
echo "  App bundle : $APP_BUNDLE"
echo "  DMG        : $DMG_OUT"
echo ""
echo "NOTE: This build is NOT code-signed. On first launch, users must"
echo "  right-click the .app and choose Open, then click Open again."
