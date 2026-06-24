#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/package-runtime.sh" win-x64
"$SCRIPT_DIR/package-runtime.sh" osx-arm64
"$SCRIPT_DIR/package-runtime.sh" osx-x64
"$SCRIPT_DIR/package-runtime.sh" linux-x64
"$SCRIPT_DIR/package-runtime.sh" linux-arm64
