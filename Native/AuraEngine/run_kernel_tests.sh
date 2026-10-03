#!/usr/bin/env bash
set -euo pipefail

# Builds libaura (Jolt + Box2D), then compiles and runs the standalone kernel
# test suite against it. This is the canonical place to validate the C++
# physics kernel: the equivalent in-Editor native suite crashes mono's JIT at
# the P/Invoke boundary, so kernel coverage lives here.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TESTS="$ROOT/tests/ManagedKernel"

DOTNET="${DOTNET:-/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet}"
if ! command -v "$DOTNET" >/dev/null 2>&1 && [ ! -x "$DOTNET" ]; then
    DOTNET=dotnet
fi

if [ ! -x "$ROOT/build/editor/libaura.dylib" ] && [ ! -x "$ROOT/build/editor/libaura.so" ]; then
    "$ROOT/build_plugin.sh"
fi

"$DOTNET" build -c Release "$TESTS/ManagedKernelTests.csproj"
OUT="$TESTS/bin/Release/net8.0"
case "$(uname -s)" in
    Darwin)
        cp "$ROOT/build/editor/libaura.dylib" "$OUT/libaura.dylib"
        ;;
    *)
        cp "$ROOT/build/editor/libaura.so" "$OUT/libaura.so"
        ;;
esac

"$DOTNET" "$OUT/managed_kernel_tests.dll" "$@"
