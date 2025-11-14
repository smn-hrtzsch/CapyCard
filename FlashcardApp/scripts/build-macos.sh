#!/bin/bash -e

# Stoppt das Skript, wenn ein Befehl fehlschlägt
set -e

# --- KONFIGURATION ---
BUNDLE_DISPLAY_NAME="Alinas Karteikarten"
APP_NAME="Alinas Karteikarten"
PROJECT_ASSETS_PATH="Assets/icon.icns"
PUBLISH_DIR="bin/Release/net9.0/osx-arm64/publish"
APP_BUNDLE_PATH="$PUBLISH_DIR/$APP_NAME.app"
BUNDLE_ID="com.simon.flashcardapp" # Du kannst 'simon' durch deinen Namen/Firma ersetzen

echo "--- Starte sauberen macOS Build für $APP_NAME ---"

# 1. Alte Builds löschen
echo "1/6: Lösche alte Builds..."
rm -rf bin obj

# 2. App mit losen Dateien veröffentlichen
echo "2/6: Veröffentliche .NET-Dateien..."
dotnet publish -c Release -r osx-arm64

# 3. .app-Struktur erstellen
echo "3/6: Erstelle .app-Bundle-Struktur..."
mkdir -p "$APP_BUNDLE_PATH/Contents/MacOS"
mkdir -p "$APP_BUNDLE_PATH/Contents/Resources"

# 4. Alle losen Dateien in den MacOS-Ordner verschieben
echo "4/6: Verschiebe App-Dateien..."
# Wir verschieben alles aus dem publish-Ordner in den MacOS-Ordner.
# Die Fehlermeldung "Invalid argument", falls es versucht, sich selbst zu verschieben,
# unterdrücken wir mit '|| true', da sie erwartet wird und harmlos ist.
mv $PUBLISH_DIR/* "$APP_BUNDLE_PATH/Contents/MacOS/" || true

# 5. Icon kopieren
echo "5/6: Kopiere App-Icon..."
cp "$PROJECT_ASSETS_PATH" "$APP_BUNDLE_PATH/Contents/Resources/icon.icns"

# 6. Info.plist erstellen (Das "Gehirn" der App)
echo "6/6: Erstelle Info.plist..."
cat << EOF > "$APP_BUNDLE_PATH/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
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

echo ""
echo "--- ERFOLGREICH ---"
echo "App-Bundle erstellt: $APP_BUNDLE_PATH"
echo "Du kannst die App jetzt im Finder öffnen und nach /Applications kopieren."