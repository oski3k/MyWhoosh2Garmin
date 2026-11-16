#!/bin/bash
# Builds "Whoosh2Garmin.app" (universal: Apple Silicon + Intel) into build/.
# Requires only the Xcode command line tools (swiftc).
#
#   ./build.sh            — just builds build/Whoosh2Garmin.app
#   ./build.sh --install  — also copies the app to /Applications
#   ./build.sh --zip      — also packs it into build/Whoosh2Garmin.zip (for releases)
set -euo pipefail
cd "$(dirname "$0")"

INSTALL=0; ZIP=0
for arg in "$@"; do
    case "$arg" in
        --install) INSTALL=1 ;;
        --zip) ZIP=1 ;;
        *) echo "Unknown option: $arg" >&2; exit 2 ;;
    esac
done
VERSION="${VERSION:-1.0}"

APP="build/Whoosh2Garmin.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

for arch in arm64 x86_64; do
    swiftc -O -parse-as-library -target "$arch-apple-macos14" \
        Sources/App.swift Sources/FitPatcher.swift -o "build/Whoosh2Garmin-$arch"
done
lipo -create build/Whoosh2Garmin-arm64 build/Whoosh2Garmin-x86_64 -output "$APP/Contents/MacOS/Whoosh2Garmin"
rm -f build/Whoosh2Garmin-arm64 build/Whoosh2Garmin-x86_64

swift Tools/icon.swift build/AppIcon.iconset
iconutil -c icns build/AppIcon.iconset -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf build/AppIcon.iconset

cat > "$APP/Contents/Info.plist" <<PLIST
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
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key><string>FIT File</string>
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
echo "Done: $APP"

if [ "$ZIP" = 1 ]; then
    rm -f build/Whoosh2Garmin.zip
    ditto -c -k --keepParent "$APP" build/Whoosh2Garmin.zip
    echo "Packed: build/Whoosh2Garmin.zip"
fi

if [ "$INSTALL" = 1 ]; then
    # Replace the installed copy so the latest version always runs.
    pkill -x Whoosh2Garmin 2>/dev/null || true
    rm -rf /Applications/Whoosh2Garmin.app
    cp -R "$APP" /Applications/
    echo "Installed to /Applications"
fi
