#!/bin/zsh
# AuraEngine demo smoke test (AI-agent/editor tool). Plays each demo scene through the Unity CLI,
# samples motion twice, captures the Game View and counts console errors.
# Usage: Tools/AuraSmoke/aura_smoke.sh [OUT_DIR] [SceneName ...]   (scene names without .unity)
# The Editor must be running, focused, and the open scene clean. Output: OUT_DIR/report.txt + PNGs.
set -u
ROOT=${0:A:h:h:h}; cd "$ROOT"
OUT=${1:-/tmp/aura_smoke}; shift 2>/dev/null
mkdir -p "$OUT" Temp; cp Tools/AuraSmoke/AuraSceneProbe.cs Temp/AuraSceneProbe.cs
SCENES=("$@"); [ ${#SCENES[@]} -eq 0 ] && SCENES=($(ls Assets/AuraEngine/Demo/Scenes/*.unity | xargs -n1 basename | sed 's/\.unity$//'))
alive() { unity status --format json 2>/dev/null | grep -q '"state": "ready"'; }
sample() { unity command run_script --file Temp/AuraSceneProbe.cs --entry AuraSceneProbe.Sample 2>&1 | grep -o '"result":"[^"]*"' | sed 's/"result":"//;s/"$//'; }
: > "$OUT/report.txt"
for n in $SCENES; do
  alive || { echo "$n | EDITOR DEAD" | tee -a "$OUT/report.txt"; break; }
  unity command open_scene --path "Assets/AuraEngine/Demo/Scenes/$n.unity" >/dev/null 2>&1
  unity command clear_console >/dev/null 2>&1
  unity command editor_play >/dev/null 2>&1; unity command editor_focus >/dev/null 2>&1; sleep 3
  alive || { echo "$n | CRASH on play entry" | tee -a "$OUT/report.txt"; break; }
  a=$(sample); sleep 4; b=$(sample)
  unity command capture_game_view 2>&1 | python3 -c "
import sys,base64,re
m=re.search(r'\"base64\":\"([A-Za-z0-9+/=]+)',sys.stdin.read())
open('$OUT/$n.png','wb').write(base64.b64decode(m.group(1)))" 2>/dev/null
  c=$(unity command console 2>&1 | grep -o '"counts":{[^}]*}')
  e=$(unity command console 2>&1 | grep -o '"message":"[^"]\{0,140\}' | head -2 | tr '\n' '|')
  unity command editor_stop >/dev/null 2>&1; sleep 1
  moved=$([ "$a" != "$b" ] && echo MOVING || echo STATIC)
  echo "$n | $moved | A: $a | B: $b | $c | $e" | tee -a "$OUT/report.txt"
done
