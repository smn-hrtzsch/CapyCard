#!/bin/bash -e
set -e

# Configuration
PROJECT_NAME="FlashcardMobile.Desktop"
PROJECT_DIR="FlashcardMobile.Desktop"
APP_EXECUTABLE="FlashcardMobile.Desktop"
APP_BUNDLE_NAME="CapyCard"
BUNDLE_DISPLAY_NAME="CapyCard"
# Icon is in the shared project relative to script execution root (FlashcardMobile/)
PROJECT_ASSETS_PATH="FlashcardMobile/Assets/icon.icns" 
OUTPUT_DIR="$PROJECT_DIR/bin/Release/net9.0/osx-arm64/publish"
TEMP_PUBLISH_DIR="$PROJECT_DIR/bin/Release/net9.0/osx-arm64/temp_publish"
APP_BUNDLE_PATH="$OUTPUT_DIR/$APP_BUNDLE_NAME.app"
BUNDLE_ID="com.alina.capycard.desktop"

echo "--- Building $APP_BUNDLE_NAME ---"

# 1. Clean
echo "Cleaning..."
rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj"

# 2. Publish to temp dir
echo "Publishing..."
dotnet publish "$PROJECT_DIR/$PROJECT_NAME.csproj" -c Release -r osx-arm64 -o "$TEMP_PUBLISH_DIR"

# 3. Create .app structure
echo "Bundling..."
mkdir -p "$APP_BUNDLE_PATH/Contents/MacOS"
mkdir -p "$APP_BUNDLE_PATH/Contents/Resources"

# 4. Copy files
cp -a "$TEMP_PUBLISH_DIR/." "$APP_BUNDLE_PATH/Contents/MacOS/"
cp "$PROJECT_ASSETS_PATH" "$APP_BUNDLE_PATH/Contents/Resources/icon.icns"

# 5. Create Info.plist
echo "Creating Info.plist..."
cat << EOF > "$APP_BUNDLE_PATH/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$APP_EXECUTABLE</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleName</key>
    <string>$APP_BUNDLE_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$BUNDLE_DISPLAY_NAME</string>
    <key>CFBundleIconFile</key>
    <string>icon.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <string>True</string>
</dict>
</plist>
EOF

# Cleanup temp
rm -rf "$TEMP_PUBLISH_DIR"

echo "--- Build Complete: $APP_BUNDLE_PATH ---"
