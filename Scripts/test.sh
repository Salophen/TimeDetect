#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_DIR="$ROOT_DIR/.build/tests"
mkdir -p "$TEST_DIR"

swiftc -O \
  "$ROOT_DIR/Sources/Shared/PeakEngine.swift" \
  "$ROOT_DIR/Sources/Shared/WidgetSupport.swift" \
  "$ROOT_DIR/Tests/PeakEngineTests.swift" \
  -o "$TEST_DIR/PeakEngineTests"

"$TEST_DIR/PeakEngineTests"