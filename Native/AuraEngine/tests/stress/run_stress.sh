#!/usr/bin/env bash
set -euo pipefail

# Builds libaura's native sources with ThreadSanitizer and, separately, UndefinedBehaviorSanitizer, links the
# aura_stress driver (C ABI only) against each and runs it. Everything goes into the build directory passed as the
# first argument; nothing is copied into Assets.
#
#   usage: run_stress.sh <build-dir> [steps=400] [seed=1] [mode=all|jolt|box2d|multiworld] [tsan|ubsan|both]
#
# Output of each run is kept in <build-dir>/{tsan,ubsan}.log.

if [ $# -lt 1 ]; then
    echo "usage: $0 <build-dir> [steps] [seed] [mode] [tsan|ubsan|both]" >&2
    exit 2
fi

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
OUT="$1"
STEPS="${2:-400}"
SEED="${3:-1}"
MODE="${4:-all}"
WHICH="${5:-both}"

case "$OUT" in
    "$ROOT"/../../Assets*|*/Assets/*) echo "refusing to build into Assets" >&2; exit 2 ;;
esac

if [ ! -d "$ROOT/third_party/JoltPhysics/Build" ] || [ ! -d "$ROOT/third_party/box2d/src" ]; then
    echo "run fetch_jolt.sh and fetch_box2d.sh first" >&2
    exit 2
fi

build_one() {
    local name="$1" flags="$2"
    local dir="$OUT/$name"
    cmake -S "$ROOT" -B "$dir" -G Ninja \
        -DCMAKE_BUILD_TYPE=RelWithDebInfo \
        -DAURA_USE_JOLT=ON -DAURA_USE_BOX2D=ON \
        -DAURA_BUILD_PLUGIN=OFF -DAURA_BUILD_TESTS=OFF \
        -DCMAKE_C_FLAGS="$flags -fno-omit-frame-pointer" \
        -DCMAKE_CXX_FLAGS="$flags -fno-omit-frame-pointer" \
        -DCMAKE_EXE_LINKER_FLAGS="$flags" > "$dir.configure.log" 2>&1 || { cat "$dir.configure.log"; exit 1; }
    cmake --build "$dir" --target aura_native > "$dir.build.log" 2>&1 || { tail -40 "$dir.build.log"; exit 1; }
    # Link the driver by hand: the repository CMake has no stress target and this folder owns its own flags.
    local libs
    libs="$dir/libaura_native.a $(find "$dir" -name 'libJolt.a' | head -1) $(find "$dir" -name 'libbox2d*.a' | head -1)"
    # shellcheck disable=SC2086
    c++ -std=c++17 -O1 -g $flags -fno-omit-frame-pointer -I"$ROOT/include" "$HERE/aura_stress.cpp" $libs -lpthread -o "$dir/aura_stress"
    echo "built $dir/aura_stress"
}

run_one() {
    local name="$1" env_text="$2"
    echo "== running $name (steps=$STEPS seed=$SEED mode=$MODE)"
    set +e
    env $env_text "$OUT/$name/aura_stress" "$STEPS" "$SEED" "$MODE" > "$OUT/$name.log" 2>&1
    local rc=$?
    set -e
    echo "exit code $rc, log $OUT/$name.log"
    echo "-- $(grep -c 'WARNING: ThreadSanitizer' "$OUT/$name.log" || true) TSan warnings, $(grep -c 'runtime error:' "$OUT/$name.log" || true) UBSan reports"
}

mkdir -p "$OUT"

if [ "$WHICH" = "tsan" ] || [ "$WHICH" = "both" ]; then
    build_one tsan "-fsanitize=thread"
    run_one tsan "TSAN_OPTIONS=halt_on_error=0:second_deadlock_stack=1:history_size=4"
fi

if [ "$WHICH" = "ubsan" ] || [ "$WHICH" = "both" ]; then
    # vptr needs RTTI, which Jolt is built without.
    build_one ubsan "-fsanitize=undefined -fno-sanitize=vptr"
    run_one ubsan "UBSAN_OPTIONS=print_stacktrace=1:halt_on_error=0"
fi
