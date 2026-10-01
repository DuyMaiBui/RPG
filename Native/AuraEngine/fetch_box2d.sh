#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BOX2D="$ROOT/third_party/box2d"

if [ -d "$BOX2D/src" ]; then
    echo "Box2D already present at $BOX2D"
    exit 0
fi

mkdir -p "$ROOT/third_party"
git clone --depth 1 --branch v3.1.0 --filter=blob:none --sparse https://github.com/erincatto/box2d.git "$BOX2D"
git -C "$BOX2D" sparse-checkout set include src
echo "fetched Box2D into $BOX2D"
