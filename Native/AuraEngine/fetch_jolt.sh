#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JOLT="$ROOT/third_party/JoltPhysics"

if [ -d "$JOLT/Jolt" ]; then
    echo "Jolt already present at $JOLT"
    exit 0
fi

mkdir -p "$ROOT/third_party"
git clone --depth 1 --filter=blob:none --sparse https://github.com/jrouwe/JoltPhysics.git "$JOLT"
git -C "$JOLT" sparse-checkout set Jolt Build
echo "fetched Jolt into $JOLT"
