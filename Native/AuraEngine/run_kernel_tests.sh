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

"$ROOT/build_plugin.sh"

"$DOTNET" build -c Release "$TESTS/ManagedKernelTests.csproj"
OUT="$TESTS/bin/Release/net8.0"
case "$(uname -s)" in
    Darwin)
        cp "$ROOT/build/plugin/libaura.dylib" "$OUT/libaura.dylib"
        ;;
    *)
        cp "$ROOT/build/plugin/libaura.so" "$OUT/libaura.so"
        ;;
esac

# AURA_GMALLOC=1 runs the suite under macOS Guard Malloc so silent heap corruption aborts. The .NET host shipped with
# Unity (and Microsoft's apphost) has the hardened runtime, which makes dyld ignore DYLD_INSERT_LIBRARIES, so Guard
# Malloc would silently not be active. Run an ad-hoc re-signed copy of the host instead and fail if the Guard Malloc
# banner is missing, so a green result always means the suite really ran under it.
if [ "${AURA_GMALLOC:-0}" = "1" ] && [ "$(uname -s)" = "Darwin" ]; then
    REAL_DOTNET="$(command -v "$DOTNET" || echo "$DOTNET")"
    REAL_DIR="$(cd "$(dirname "$REAL_DOTNET")" && pwd)"
    GM_DIR="$(mktemp -d)"
    trap 'rm -rf "$GM_DIR"' EXIT
    cp "$REAL_DIR/dotnet" "$GM_DIR/dotnet"
    for entry in "$REAL_DIR"/*; do
        [ "$(basename "$entry")" = "dotnet" ] || ln -s "$entry" "$GM_DIR/$(basename "$entry")"
    done
    codesign --force --sign - "$GM_DIR/dotnet" >/dev/null 2>&1
    LOG="$GM_DIR/gmalloc.log"
    set +e
    DYLD_INSERT_LIBRARIES=/usr/lib/libgmalloc.dylib "$GM_DIR/dotnet" "$OUT/managed_kernel_tests.dll" "$@" 2>&1 | tee "$LOG"
    STATUS=${PIPESTATUS[0]}
    set -e
    if ! grep -q "GuardMalloc\[" "$LOG"; then
        echo "ERROR: Guard Malloc did not activate (hardened .NET host?); the result above is NOT a Guard Malloc run." >&2
        exit 3
    fi
    exit "$STATUS"
else
    "$DOTNET" "$OUT/managed_kernel_tests.dll" "$@"
fi
