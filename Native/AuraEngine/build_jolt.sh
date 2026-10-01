#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="$ROOT/build/jolt"

cmake -S "$ROOT" -B "$BUILD" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DAURA_USE_JOLT=ON \
    -DAURA_BUILD_PLUGIN=ON \
    -DAURA_BUILD_TESTS=ON

cmake --build "$BUILD"

echo "built $BUILD/libaura.dylib and $BUILD/aura_headless"
"$BUILD/aura_headless"

PLUGIN_DIR="$ROOT/../../Assets/Plugins/AuraEngine/macOS"
mkdir -p "$PLUGIN_DIR"
cp "$BUILD/libaura.dylib" "$PLUGIN_DIR/libaura.dylib"
echo "copied libaura.dylib to $PLUGIN_DIR"
