#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_DIR="$ROOT_DIR/.build/tests"
mkdir -p "$TEST_DIR"

swiftc -O \
  -framework Combine \
  -framework Security \
  -framework LocalAuthentication \
  "$ROOT_DIR/Sources/Shared/PeakEngine.swift" \
  "$ROOT_DIR/Sources/Shared/NotificationPlan.swift" \
  "$ROOT_DIR/Sources/Shared/WidgetSupport.swift" \
  "$ROOT_DIR/Sources/App/FloatingWindowMode.swift" \
  "$ROOT_DIR/Sources/App/HTTPClient.swift" \
  "$ROOT_DIR/Sources/App/DeepSeekAPIModels.swift" \
  "$ROOT_DIR/Sources/App/KeychainManager.swift" \
  "$ROOT_DIR/Sources/App/DeepSeekStatusManager.swift" \
  "$ROOT_DIR/Sources/App/DeepSeekBalanceManager.swift" \
  "$ROOT_DIR/Tests/PeakEngineTests.swift" \
  -o "$TEST_DIR/PeakEngineTests"

"$TEST_DIR/PeakEngineTests"