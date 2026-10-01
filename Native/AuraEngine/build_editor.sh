#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="$ROOT/build/editor"

case "$(uname -s)" in
    Darwin)
        LIBNAME="libaura.dylib"
        PLUGIN_SUBDIR="macOS"
        ;;
    *)
        LIBNAME="libaura.so"
        PLUGIN_SUBDIR="Linux/x86_64"
        ;;
esac

cmake -S "$ROOT" -B "$BUILD" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DAURA_USE_JOLT=ON \
    -DAURA_USE_BOX2D=ON \
    -DAURA_BUILD_PLUGIN=ON \
    -DAURA_BUILD_TESTS=ON

cmake --build "$BUILD"

echo "built $BUILD/$LIBNAME and $BUILD/aura_headless"
"$BUILD/aura_headless"

PLUGIN_DIR="$ROOT/../../Assets/Plugins/AuraEngine/$PLUGIN_SUBDIR"
mkdir -p "$PLUGIN_DIR"
cp "$BUILD/$LIBNAME" "$PLUGIN_DIR/$LIBNAME"
echo "copied $LIBNAME to $PLUGIN_DIR"
