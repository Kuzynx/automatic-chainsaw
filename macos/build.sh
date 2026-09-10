#!/usr/bin/env bash
# Builds Sakura Music as a proper .app bundle (menu bar app, no Dock icon).
# Requires macOS 13+ and the Xcode Command Line Tools (xcode-select --install).
set -euo pipefail
cd "$(dirname "$0")"

swift build -c release

APP="build/Sakura Music.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp .build/release/SakuraMusic "$APP/Contents/MacOS/SakuraMusic"
cp Resources/Info.plist "$APP/Contents/Info.plist"
echo -n "APPL????" > "$APP/Contents/PkgInfo"

# Ad-hoc signature: enough for a local build to launch cleanly and to register
# "Launch at Login" with SMAppService.
codesign --force --sign - "$APP"

echo
echo "Built: $APP"
echo "Run:   open \"$APP\""
echo "Or install it: cp -R \"$APP\" /Applications/"
