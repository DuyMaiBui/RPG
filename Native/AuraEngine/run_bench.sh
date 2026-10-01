#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="$ROOT/build/bench"

cmake -S "$ROOT" -B "$BUILD" -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DAURA_BUILD_BENCHMARKS=ON

cmake --build "$BUILD" --target aura_bench

echo "built $BUILD/aura_bench"
"$BUILD/aura_bench" "$@"
