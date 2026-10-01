#!/usr/bin/env bash
set -euo pipefail

# Builds libaura for Android and copies it into the Unity plugin folder.
# Requires the Android NDK (ANDROID_NDK_HOME or ANDROID_NDK_ROOT) and cmake/ninja.
#
# Usage:
#   ./Native/AuraEngine/build_android.sh                 # arm64-v8a, android-24
#   ANDROID_ABI=armeabi-v7a ./Native/AuraEngine/build_android.sh

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ABI="${ANDROID_ABI:-arm64-v8a}"
PLATFORM="${ANDROID_PLATFORM:-android-24}"
BUILD="$ROOT/build/android-$ABI"

if [ -z "${ANDROID_NDK_HOME:-}" ] && [ -z "${ANDROID_NDK_ROOT:-}" ]; then
    echo "ANDROID_NDK_HOME or ANDROID_NDK_ROOT must be set." >&2
    exit 1
fi

if [ ! -d "$ROOT/third_party/JoltPhysics/Build" ]; then
    "$ROOT/fetch_jolt.sh"
fi
if [ ! -d "$ROOT/third_party/box2d/src" ]; then
    "$ROOT/fetch_box2d.sh"
fi

cmake -S "$ROOT" -B "$BUILD" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE="$ROOT/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="$ABI" \
    -DANDROID_PLATFORM="$PLATFORM" \
    -DAURA_USE_JOLT=ON \
    -DAURA_USE_BOX2D=ON \
    -DAURA_BUILD_PLUGIN=ON

cmake --build "$BUILD"

PLUGIN_DIR="$ROOT/../../Assets/Plugins/AuraEngine/Android/$ABI"
mkdir -p "$PLUGIN_DIR"
cp "$BUILD/libaura.so" "$PLUGIN_DIR/libaura.so"
echo "copied libaura.so to $PLUGIN_DIR"
