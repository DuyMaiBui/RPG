#!/usr/bin/env bash
set -euo pipefail

# Builds libaura for iOS and copies it into the Unity plugin folder.
# Requires an active Xcode (xcode-select -p must point at Xcode) and cmake.
#
# Usage:
#   ./Native/AuraEngine/build_ios.sh                 # device arm64, static lib
#   IOS_ARCH=x86_64 SIMULATOR=1 ./Native/AuraEngine/build_ios.sh

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCH="${IOS_ARCH:-arm64}"
SIMULATOR="${SIMULATOR:-0}"
BUILD="$ROOT/build/ios-$ARCH"
OUT_NAME="libaura.a"

if ! xcrun --sdk iphoneos --show-sdk-path >/dev/null 2>&1; then
    echo "iOS SDK not found; install Xcode and run: sudo xcode-select -s /Applications/Xcode.app" >&2
    exit 1
fi

if [ ! -d "$ROOT/third_party/JoltPhysics/Build" ]; then
    "$ROOT/fetch_jolt.sh"
fi
if [ ! -d "$ROOT/third_party/box2d/src" ]; then
    "$ROOT/fetch_box2d.sh"
fi

TOOLCHAIN_ARGS=(-DIOS_ARCH="$ARCH")
if [ "$SIMULATOR" = "1" ]; then
    TOOLCHAIN_ARGS+=(-DCMAKE_OSX_SYSROOT=iphonesimulator -DCMAKE_SYSTEM_NAME=iOS)
fi

cmake -S "$ROOT" -B "$BUILD" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE="$ROOT/cmake/ios.toolchain.cmake" \
    "${TOOLCHAIN_ARGS[@]}" \
    -DAURA_USE_JOLT=ON \
    -DAURA_USE_BOX2D=ON \
    -DAURA_BUILD_PLUGIN=ON

cmake --build "$BUILD"

PLUGIN_DIR="$ROOT/../../Assets/Plugins/AuraEngine/iOS"
mkdir -p "$PLUGIN_DIR"
cp "$BUILD/$OUT_NAME" "$PLUGIN_DIR/$OUT_NAME" 2>/dev/null || cp "$BUILD/libaura.dylib" "$PLUGIN_DIR/libaura.dylib"
echo "copied iOS plugin to $PLUGIN_DIR"
