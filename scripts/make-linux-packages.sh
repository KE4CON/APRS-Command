#!/usr/bin/env bash
# ============================================================
# APRS Command — Linux .deb and .rpm packages
#
# No signing required for personal/open-source distribution.
#
# Usage:
#   bash scripts/make-linux-packages.sh [linux-x64|linux-arm64] [version]
#
# Requirements:
#   .deb: dpkg-dev   (sudo apt install dpkg-dev)
#   .rpm: rpm-build  (sudo dnf install rpm-build)
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
RID="${1:-linux-x64}"
VERSION="${2:-$(git -C "$REPO_ROOT" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo "0.0.0")}"

if [[ "$RID" != "linux-x64" && "$RID" != "linux-arm64" ]]; then
  echo "Usage: $0 [linux-x64|linux-arm64] [version]" >&2; exit 2
fi

DEB_ARCH="$( [[ "$RID" == "linux-x64" ]] && echo "amd64" || echo "arm64" )"
RPM_ARCH="$( [[ "$RID" == "linux-x64" ]] && echo "x86_64" || echo "aarch64" )"
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
INSTALLER_DIR="$REPO_ROOT/artifacts/installers"
mkdir -p "$INSTALLER_DIR"

# ── 1. Publish ────────────────────────────────────────────────────────────────
if [[ ! -f "$PUBLISH_DIR/Aprs.Desktop" ]]; then
  echo "Publishing $RID..."
  dotnet publish "$REPO_ROOT/src/Aprs.Desktop/Aprs.Desktop.csproj" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=false -p:PublishReadyToRun=true \
    -o "$PUBLISH_DIR"
fi

# ── 2. .deb ───────────────────────────────────────────────────────────────────
build_deb() {
  local DEB_NAME="aprs-command_${VERSION}_${DEB_ARCH}.deb"
  local STAGING
  STAGING="$(mktemp -d)"
  trap "rm -rf '$STAGING'" RETURN

  local APP_DIR="$STAGING/opt/aprs-command"
  mkdir -p "$APP_DIR" \
    "$STAGING/usr/local/bin" \
    "$STAGING/usr/share/applications" \
    "$STAGING/DEBIAN"

  cp -R "$PUBLISH_DIR"/. "$APP_DIR/"
  chmod +x "$APP_DIR/Aprs.Desktop"

  # Launcher
  cat > "$STAGING/usr/local/bin/aprs-command" << 'LAUNCHER'
#!/usr/bin/env bash
exec /opt/aprs-command/Aprs.Desktop "$@"
LAUNCHER
  chmod +x "$STAGING/usr/local/bin/aprs-command"

  # .desktop entry
  cat > "$STAGING/usr/share/applications/aprs-command.desktop" << DESKTOP
[Desktop Entry]
Name=APRS Command
Comment=Cross-platform APRS client for amateur radio operators
Exec=/opt/aprs-command/Aprs.Desktop
Icon=aprs-command
Terminal=false
Type=Application
Categories=HamRadio;Network;
Keywords=APRS;ham radio;packet radio;
DESKTOP

  # control file
  cat > "$STAGING/DEBIAN/control" << CONTROL
Package: aprs-command
Version: $VERSION
Architecture: $DEB_ARCH
Maintainer: KE4CON <ke4con@users.noreply.github.com>
Installed-Size: $(du -sk "$APP_DIR" | awk '{print $1}')
Description: Cross-platform APRS client for amateur radio operators
 APRS Command is an open-source cross-platform APRS desktop client and the
 spiritual successor to UI-View32. Built with .NET 10 and Avalonia UI.
 Supports APRS-IS, KISS-TCP, Serial KISS, AGWPE/BPQ32, and RF connections.
Homepage: https://github.com/KE4CON/APRS-Command
Section: hamradio
Priority: optional
CONTROL

  # postinst
  cat > "$STAGING/DEBIAN/postinst" << 'POSTINST'
#!/bin/sh
set -e
chmod +x /opt/aprs-command/Aprs.Desktop 2>/dev/null || true
exit 0
POSTINST
  chmod 755 "$STAGING/DEBIAN/postinst"

  dpkg-deb --build --root-owner-group "$STAGING" "$INSTALLER_DIR/$DEB_NAME"
  echo "  → $INSTALLER_DIR/$DEB_NAME"
}

# ── 3. .rpm ───────────────────────────────────────────────────────────────────
build_rpm() {
  local BUILD
  BUILD="$(mktemp -d)"
  trap "rm -rf '$BUILD'" RETURN
  mkdir -p "$BUILD"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

  # Source tarball
  local SRC="aprs-command-$VERSION"
  mkdir -p "$BUILD/BUILD/$SRC/opt/aprs-command"
  cp -R "$PUBLISH_DIR"/. "$BUILD/BUILD/$SRC/opt/aprs-command/"
  chmod +x "$BUILD/BUILD/$SRC/opt/aprs-command/Aprs.Desktop"
  (cd "$BUILD/BUILD" && tar -czf "$BUILD/SOURCES/${SRC}.tar.gz" "$SRC")

  cat > "$BUILD/SPECS/aprs-command.spec" << SPEC
Name:      aprs-command
Version:   $VERSION
Release:   1%{?dist}
Summary:   Cross-platform APRS client for amateur radio operators
License:   GPLv3
URL:       https://github.com/KE4CON/APRS-Command
Source0:   %{name}-%{version}.tar.gz
BuildArch: $RPM_ARCH
AutoReqProv: no

%description
APRS Command is an open-source cross-platform APRS desktop client.
Successor to UI-View32, built with .NET 10 and Avalonia UI.

%prep
%setup -q

%install
mkdir -p %{buildroot}/opt/aprs-command
cp -R opt/aprs-command/. %{buildroot}/opt/aprs-command/
chmod +x %{buildroot}/opt/aprs-command/Aprs.Desktop
mkdir -p %{buildroot}/usr/local/bin
printf '#!/usr/bin/env bash\nexec /opt/aprs-command/Aprs.Desktop "\$@"\n' \
  > %{buildroot}/usr/local/bin/aprs-command
chmod +x %{buildroot}/usr/local/bin/aprs-command
mkdir -p %{buildroot}/usr/share/applications
cat > %{buildroot}/usr/share/applications/aprs-command.desktop << 'DESKTOP'
[Desktop Entry]
Name=APRS Command
Comment=Cross-platform APRS client for amateur radio operators
Exec=/opt/aprs-command/Aprs.Desktop
Terminal=false
Type=Application
Categories=HamRadio;Network;
DESKTOP

%files
/opt/aprs-command/
/usr/local/bin/aprs-command
/usr/share/applications/aprs-command.desktop

%changelog
* $(date '+%a %b %d %Y') KE4CON <ke4con@users.noreply.github.com> - $VERSION-1
- Release $VERSION
SPEC

  rpmbuild --define "_topdir $BUILD" -bb "$BUILD/SPECS/aprs-command.spec" 2>&1
  local RPM
  RPM="$(find "$BUILD/RPMS" -name "*.rpm" | head -1)"
  if [[ -n "$RPM" ]]; then
    cp "$RPM" "$INSTALLER_DIR/"
    echo "  → $INSTALLER_DIR/$(basename "$RPM")"
  fi
}

# ── Run ───────────────────────────────────────────────────────────────────────
echo "Building Linux packages for $RID (version $VERSION)..."

if command -v dpkg-deb >/dev/null 2>&1; then
  echo "Building .deb..."
  build_deb
else
  echo "dpkg-deb not found — skipping .deb (sudo apt install dpkg-dev)"
fi

if command -v rpmbuild >/dev/null 2>&1; then
  echo "Building .rpm..."
  build_rpm
else
  echo "rpmbuild not found — skipping .rpm (sudo dnf install rpm-build)"
fi

echo ""
echo "Packages written to: $INSTALLER_DIR"
echo "  Install .deb:  sudo dpkg -i aprs-command_*.deb"
echo "  Install .rpm:  sudo rpm -i aprs-command-*.rpm"
