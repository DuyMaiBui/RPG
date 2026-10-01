#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$ROOT/build"
mkdir -p "$OUT"

CXX="${CXX:-clang++}"
FLAGS=(-std=c++17 -O2 -fPIC -I"$ROOT/include" -I"$ROOT/src")

case "$(uname -s)" in
    Darwin)
        SHARED=(-dynamiclib)
        LIBNAME="libaura.dylib"
        RPATH=(-Wl,-rpath,"$OUT")
        ;;
    *)
        SHARED=(-shared)
        LIBNAME="libaura.so"
        RPATH=(-Wl,-rpath,"$OUT")
        ;;
esac

"$CXX" "${FLAGS[@]}" "${SHARED[@]}" \
    "$ROOT/src/aura_reference_world.cpp" \
    "$ROOT/src/aura_backend_select.cpp" \
    "$ROOT/src/capi/aura_capi.cpp" \
    "$ROOT/src/capi/aura_capi_events.cpp" \
    "$ROOT/src/capi/aura_capi_queries.cpp" \
    "$ROOT/src/capi/aura_capi_contacts.cpp" \
    "$ROOT/src/capi/aura_capi_joints.cpp" \
    "$ROOT/src/capi/aura_capi_character.cpp" \
    "$ROOT/src/capi/aura_capi_snapshot.cpp" \
    -o "$OUT/$LIBNAME"

"$CXX" "${FLAGS[@]}" \
    "$ROOT/src/headless/main.cpp" \
    -L"$OUT" -laura "${RPATH[@]}" \
    -o "$OUT/aura_headless"

echo "built $OUT/$LIBNAME and $OUT/aura_headless"
"$OUT/aura_headless"
