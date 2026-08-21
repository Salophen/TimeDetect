#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="$ROOT_DIR/.build"
STAGING_APP="$BUILD_DIR/TimeDetect.app"
APP_DIR="$ROOT_DIR/TimeDetect.app"
MACOS_DIR="$STAGING_APP/Contents/MacOS"

rm -rf "$STAGING_APP" "$APP_DIR"
mkdir -p "$MACOS_DIR"
swiftc -O \
  -target "$(uname -m)-apple-macosx12.0" \
  -framework AppKit \
  -framework SwiftUI \
  -framework Combine \
  -framework LocalAuthentication \
  -framework QuartzCore \
  -framework UserNotifications \
  -framework ServiceManagement \
  -framework Security \
  "$ROOT_DIR"/Sources/Shared/*.swift \
  "$ROOT_DIR"/Sources/App/*.swift \
  -o "$MACOS_DIR/TimeDetect"

cat > "$STAGING_APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>TimeDetect</string>
    <key>CFBundleExecutable</key>
    <string>TimeDetect</string>
    <key>CFBundleIdentifier</key>
    <string>local.timedetect.app</string>
    <key>CFBundleName</key>
    <string>TimeDetect</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleVersion</key>
    <string>2</string>
    <key>CFBundleShortVersionString</key>
    <string>1.1</string>
    <key>LSUIElement</key>
    <true/>
    <key>LSMultipleInstancesProhibited</key>
    <true/>
</dict>
</plist>
PLIST

ditto "$STAGING_APP" "$APP_DIR"
# 临时签名足以让本机 Finder 稳定识别并启动这个本地构建的应用包。
codesign --force --deep --sign - "$APP_DIR"

echo "Built $APP_DIR"
echo "Run with: open $APP_DIR"