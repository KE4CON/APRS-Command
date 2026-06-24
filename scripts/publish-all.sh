#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/publish-runtime.sh" win-x64
"$SCRIPT_DIR/publish-runtime.sh" osx-arm64
"$SCRIPT_DIR/publish-runtime.sh" osx-x64
"$SCRIPT_DIR/publish-runtime.sh" linux-x64
"$SCRIPT_DIR/publish-runtime.sh" linux-arm64
