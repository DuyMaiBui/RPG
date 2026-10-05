#!/bin/zsh
# AuraEngine demo smoke test (AI-agent/editor tool). Plays each demo scene through the Unity CLI, samples scene
# metrics 16 times, captures the Game View, counts console errors and evaluates Tools/AuraSmoke/expectations.json.
# Usage: Tools/AuraSmoke/aura_smoke.sh [OUT_DIR] [SceneName ...]   (scene names without .unity)
# The Editor must be running with the display awake (a sleeping display throttles the Editor and the CLI hangs)
# and the open scene clean. Environment: AURA_TARGET_FPS=8 forces a low frame rate and evaluates only the "<scene>:safety" bounds
# (nothing may explode at any frame rate). Output: OUT_DIR/report.txt, OUT_DIR/<scene>.jsonl, PNGs. Exit code 1 on any failure.
set -u
ROOT=${0:A:h:h:h}; cd "$ROOT"
OUT=${1:-/tmp/aura_smoke}; shift 2>/dev/null
mkdir -p "$OUT" Temp; cp Tools/AuraSmoke/AuraSceneProbe.cs Tools/AuraSmoke/AuraFpsControl.cs Temp/
FPS=${AURA_TARGET_FPS:-}; SAFETY=""; [ -n "$FPS" ] && SAFETY="--safety"
SCENES=("$@"); [ ${#SCENES[@]} -eq 0 ] && SCENES=($(ls Assets/AuraEngine/Demo/Scenes/*.unity | xargs -n1 basename | sed 's/\.unity$//'))
alive() { unity status --format json 2>/dev/null | grep -q '"state": "ready"'; }
sample() { unity command run_script --file Temp/AuraSceneProbe.cs --entry AuraSceneProbe.Sample 2>&1 | python3 Tools/AuraSmoke/extract_result.py; }
: > "$OUT/report.txt"; failed=0
for n in $SCENES; do
  alive || { echo "$n | EDITOR DEAD" | tee -a "$OUT/report.txt"; failed=1; break; }
  unity command open_scene --path "Assets/AuraEngine/Demo/Scenes/$n.unity" >/dev/null 2>&1
  unity command clear_console >/dev/null 2>&1
  unity command editor_play >/dev/null 2>&1; unity command editor_focus >/dev/null 2>&1; sleep 2
  alive || { echo "$n | CRASH on play entry" | tee -a "$OUT/report.txt"; failed=1; break; }
  if [ -n "$FPS" ]; then unity command run_script --file Temp/AuraFpsControl.cs --entry AuraFpsControl.Set8 >/dev/null 2>&1; fi
  : > "$OUT/$n.jsonl"
  for i in $(seq 1 16); do sample >> "$OUT/$n.jsonl"; sleep 0.3; done
  unity command capture_game_view 2>&1 | python3 -c "
import sys,base64,re
m=re.search(r'\"base64\":\"([A-Za-z0-9+/=]+)',sys.stdin.read())
open('$OUT/$n.png','wb').write(base64.b64decode(m.group(1)))" 2>/dev/null
  c=$(unity command console 2>&1 | grep -o '"counts":{[^}]*}')
  e=$(unity command console 2>&1 | grep -o '"message":"[^"]\{0,140\}' | head -2 | tr '\n' '|')
  [ -n "$FPS" ] && unity command run_script --file Temp/AuraFpsControl.cs --entry AuraFpsControl.Reset >/dev/null 2>&1
  unity command editor_stop >/dev/null 2>&1; sleep 1
  res=$(python3 Tools/AuraSmoke/evaluate.py "$OUT/$n.jsonl" Tools/AuraSmoke/expectations.json "$n" $SAFETY)
  echo "$res" | head -1 | tee -a "$OUT/report.txt"; echo "$res" | tail -n +2 | tee -a "$OUT/report.txt"
  case "$c" in *'"error":0,'*) ;; *) echo "   - console errors: $c $e" | tee -a "$OUT/report.txt"; failed=1;; esac
  case "$res" in *FAIL*) failed=1;; esac
done
echo "---"; grep -c PASS "$OUT/report.txt" | sed 's/^/passed scenes: /'; grep -c FAIL "$OUT/report.txt" | sed 's/^/failed scenes: /'
exit $failed
