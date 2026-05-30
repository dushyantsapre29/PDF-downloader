#!/bin/bash

# Exit immediately if any command fails
set -e

echo "🚀 Starting macOS native App bundling process..."

# Define paths relative to the script location
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"
BUILD_DIR="$PROJECT_DIR/publish-mac"
APP_NAME="PDF Downloader"
APP_BUNDLE_DIR="$PROJECT_DIR/$APP_NAME.app"
ZIP_NAME="PDF-Downloader-macOS-AppleSilicon.zip"

# 1. Clean up old builds
echo "🧹 Cleaning up previous build artifacts..."
rm -rf "$BUILD_DIR"
rm -rf "$APP_BUNDLE_DIR"
rm -f "$PROJECT_DIR/$ZIP_NAME"

# 2. Compile self-contained single file binary for Apple Silicon
echo "📦 Compiling self-contained release build for macOS (osx-arm64)..."
dotnet publish "$PROJECT_DIR/PdfDownloader.csproj" \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$BUILD_DIR"

# 3. Create the native .app structure
echo "📁 Structuring the $APP_NAME.app bundle..."
mkdir -p "$APP_BUNDLE_DIR/Contents/MacOS"
mkdir -p "$APP_BUNDLE_DIR/Contents/Resources"

# 4. Copy the executable and make sure it is runnable
echo "💾 Packaging the executable..."
cp "$BUILD_DIR/PdfDownloader" "$APP_BUNDLE_DIR/Contents/MacOS/$APP_NAME"
chmod +x "$APP_BUNDLE_DIR/Contents/MacOS/$APP_NAME"

# 5. Create Info.plist configuration file
echo "📄 Generating Info.plist metadata..."
cat <<EOF > "$APP_BUNDLE_DIR/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>English</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>com.pdfdownloader.app</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>2.0.0</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleVersion</key>
    <string>2.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.12</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

# 6. Compress the .app bundle to a standard distribution ZIP
echo "🗜️ Archiving the .app bundle into $ZIP_NAME..."
cd "$PROJECT_DIR"
zip -r -y "$ZIP_NAME" "$APP_NAME.app"

# 7. Clean up temporary publish directory
rm -rf "$BUILD_DIR"
rm -rf "$APP_BUNDLE_DIR"

echo "✅ Success! Your native macOS app bundle is ready for release:"
echo "👉 Location: $PROJECT_DIR/$ZIP_NAME"
