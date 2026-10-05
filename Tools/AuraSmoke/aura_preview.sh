#!/bin/zsh
# Captures the Game View of each demo scene at several moments (AI-agent/editor tool) for human or model review.
# Usage: Tools/AuraSmoke/aura_preview.sh OUT_DIR [SceneName ...]   Output: OUT_DIR/<scene>_<n>.png (n = 1..3)
# Needs the Editor running, focused, display awake and the open scene clean. Times: 1.5 s, 5 s and 10 s after Play.
set -u
ROOT=${0:A:h:h:h}; cd "$ROOT"
OUT=${1:?output dir}; shift
mkdir -p "$OUT"
SCENES=("$@"); [ ${#SCENES[@]} -eq 0 ] && SCENES=($(ls Assets/AuraEngine/Demo/Scenes/*.unity | xargs -n1 basename | sed 's/\.unity$//'))
shot() { unity command capture_game_view 2>&1 | python3 -c "
import sys,base64,re
m=re.search(r'\"base64\":\"([A-Za-z0-9+/=]+)',sys.stdin.read())
open('$1','wb').write(base64.b64decode(m.group(1)))" 2>/dev/null; }
for n in $SCENES; do
  unity command open_scene --path "Assets/AuraEngine/Demo/Scenes/$n.unity" >/dev/null 2>&1
  unity command clear_console >/dev/null 2>&1
  unity command editor_play >/dev/null 2>&1; unity command editor_focus >/dev/null 2>&1
  sleep 1.5; shot "$OUT/${n}_1.png"
  sleep 3.5; shot "$OUT/${n}_2.png"
  sleep 5; shot "$OUT/${n}_3.png"
  unity command editor_stop >/dev/null 2>&1; sleep 1
  echo "captured $n"
done
