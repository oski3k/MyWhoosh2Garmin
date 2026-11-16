#!/bin/bash
# Buduje "Whoosh2Garmin.app" w katalogu build/. Wymaga tylko narzędzi Xcode (swiftc).
set -euo pipefail
cd "$(dirname "$0")"

APP="build/Whoosh2Garmin.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

swiftc -O -parse-as-library -target arm64-apple-macos14 \
    Sources/App.swift Sources/FitPatcher.swift -o "$APP/Contents/MacOS/Whoosh2Garmin"

swift Tools/icon.swift build/AppIcon.iconset
iconutil -c icns build/AppIcon.iconset -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf build/AppIcon.iconset

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>Whoosh2Garmin</string>
    <key>CFBundleDisplayName</key><string>Whoosh2Garmin</string>
    <key>CFBundleIdentifier</key><string>local.whoosh2garmin</string>
    <key>CFBundleExecutable</key><string>Whoosh2Garmin</string>
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>1.0</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key><string>Plik FIT</string>
            <key>CFBundleTypeRole</key><string>Viewer</string>
            <key>LSHandlerRank</key><string>Alternate</string>
            <key>CFBundleTypeExtensions</key><array><string>fit</string></array>
            <key>LSItemContentTypes</key><array><string>public.data</string></array>
        </dict>
    </array>
</dict>
</plist>
PLIST

codesign --force --sign - "$APP" >/dev/null
# Podmień zainstalowaną kopię, żeby zawsze uruchamiać najnowszą wersję.
pkill -x Whoosh2Garmin 2>/dev/null || true
rm -rf /Applications/Whoosh2Garmin.app
cp -R "$APP" /Applications/
echo "Gotowe: $APP (zainstalowano w /Applications)"
