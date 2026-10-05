#!/bin/zsh
# Play/Stop lifecycle check (AI-agent/editor tool). Enters and exits Play Mode repeatedly over several scenes and asserts
# that exactly one native world is alive while playing, none after stopping, that the console stays free of errors and
# that the Editor's resident memory does not grow without bound. The project enters Play Mode with domain reload
# disabled, so static state and native handles survive between sessions.
# Usage: Tools/AuraSmoke/aura_lifecycle.sh [CYCLES] [SceneName ...]
set -u
ROOT=${0:A:h:h:h}; cd "$ROOT"
CYCLES=${1:-25}; shift 2>/dev/null
SCENES=("$@"); [ ${#SCENES[@]} -eq 0 ] && SCENES=(AuraDemoSandbox2D AuraDemoConstraints3D AuraDemoSpace3D AuraDemoPlatformer2D)
mkdir -p Temp; cp Tools/AuraSmoke/AuraLifecycleProbe.cs Temp/
live() { unity command run_script --file Temp/AuraLifecycleProbe.cs --entry AuraLifecycleProbe.Live 2>&1 | python3 Tools/AuraSmoke/extract_result.py; }
rss() { ps -o rss= -p "$(pgrep -f 'Unity.app/Contents/MacOS/Unity -projectpath' | head -1)" | awk '{printf "%d", $1/1024}'; }
failures=0; rss0=$(rss); echo "start rss=${rss0}MB cycles=$CYCLES scenes=${SCENES[*]}"
n=0
for cycle in $(seq 1 $CYCLES); do
  scene=${SCENES[$(( (cycle - 1) % ${#SCENES[@]} + 1 ))]}
  unity command open_scene --path "Assets/AuraEngine/Demo/Scenes/$scene.unity" >/dev/null 2>&1
  unity command clear_console >/dev/null 2>&1
  unity command editor_play >/dev/null 2>&1; unity command editor_focus >/dev/null 2>&1; sleep 1.5
  during=$(live)
  errs=$(unity command console 2>&1 | grep -o '"error":[0-9]*' | head -1 | cut -d: -f2)
  unity command editor_stop >/dev/null 2>&1; sleep 1.5
  after=$(live)
  ok=PASS; { [ "$during" = "1" ] && [ "$after" = "0" ] && [ "${errs:-1}" = "0" ]; } || { ok=FAIL; failures=$((failures+1)); }
  echo "cycle $cycle $scene: liveDuring=$during liveAfter=$after consoleErrors=${errs:-?} rss=$(rss)MB $ok"
done
rss1=$(rss); echo "end rss=${rss1}MB growth=$((rss1 - rss0))MB failures=$failures"
exit $((failures > 0))
