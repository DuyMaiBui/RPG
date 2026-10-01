#!/usr/bin/env bash
set -euo pipefail

# Cross-platform plugin build for AuraEngine. Produces libaura for the current
# host and copies it into the Unity plugin folder for that platform. Requires
# the fetched third-party sources (fetch_jolt.sh, fetch_box2d.sh).

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="$ROOT/build/plugin"

if [ ! -d "$ROOT/third_party/JoltPhysics/Build" ]; then
    "$ROOT/fetch_jolt.sh"
fi
if [ ! -d "$ROOT/third_party/box2d/src" ]; then
    "$ROOT/fetch_box2d.sh"
fi

cmake -S "$ROOT" -B "$BUILD" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DAURA_USE_JOLT=ON \
    -DAURA_USE_BOX2D=ON \
    -DAURA_BUILD_PLUGIN=ON \
    -DAURA_BUILD_TESTS=ON

cmake --build "$BUILD"

case "$(uname -s)" in
    Darwin)
        LIBNAME="libaura.dylib"
        PLUGIN_SUBDIR="macOS"
        ;;
    MINGW*|MSYS*|CYGWIN*)
        LIBNAME="aura.dll"
        PLUGIN_SUBDIR="Windows/x86_64"
        ;;
    *)
        LIBNAME="libaura.so"
        PLUGIN_SUBDIR="Linux/x86_64"
        ;;
esac

echo "built $BUILD/$LIBNAME"
"$BUILD/aura_headless"

# DLLs load from the folder next to the executable on Windows, so the plain
# name copy is what Unity resolves there.
cp "$BUILD/$LIBNAME" "$BUILD/aura.dll" 2>/dev/null || true

PLUGIN_DIR="$ROOT/../../Assets/Plugins/AuraEngine/$PLUGIN_SUBDIR"
mkdir -p "$PLUGIN_DIR"
cp "$BUILD/$LIBNAME" "$PLUGIN_DIR/$LIBNAME"
if [ "$LIBNAME" = "aura.dll" ] && [ -f "$BUILD/aura.dll" ]; then
    cp "$BUILD/aura.dll" "$PLUGIN_DIR/aura.dll"
fi
echo "copied $LIBNAME to $PLUGIN_DIR"
