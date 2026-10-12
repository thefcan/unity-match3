#!/bin/zsh
# Smoke-test and record the game from a real player build, with no synthetic input.
#
#   tools/smoke/smoke.sh prepare            copy the project, add the probe + three test seams
#   tools/smoke/smoke.sh build              headless macOS player build of the copy
#   tools/smoke/smoke.sh run NAME SAVE [MODE] [TIMEOUT]
#                                           SAVE = a progress file ("level=stars" lines) or "none"
#                                           MODE = "menu" (default) or "record LEVEL SEED SECONDS"
#   tools/smoke/smoke.sh gif NAME           frames of a recorded run -> NAME/gameplay.gif
#   tools/smoke/smoke.sh restore            put the real standalone save back
#
# Everything lands in $SMOKE_DIR (default: $TMPDIR/match3-smoke). The project itself is
# never modified: the probe and the seams exist only in the copy. See README.md here.
set -e
set -u
setopt null_glob   # an empty *.png is "nothing to copy", not an error

REPO="${0:A:h:h:h}"
SMOKE_DIR="${SMOKE_DIR:-${TMPDIR:-/tmp}/match3-smoke}"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity}"
COPY="$SMOKE_DIR/project"
SAVE="$HOME/Library/Application Support/com.DefaultCompany.unity-match3"
LOG="$HOME/Library/Logs/DefaultCompany/unity-match3/Player.log"
SAVE_FILES=(progress.sav meta.sav progress.sav.bak meta.sav.bak progress.sav.tmp meta.sav.tmp probe probe-mode.txt)

# Move, never delete: a stray variable in an rm is how a save directory disappears.
set_aside() {
  local bin="$SMOKE_DIR/trash/$1-$(date +%s)"; mkdir -p "$bin"
  for f in $SAVE_FILES; do
    if [ -e "$SAVE/$f" ]; then mv "$SAVE/$f" "$bin/"; fi
  done
}

case "${1:-}" in
  prepare)
    mkdir -p "$COPY"
    rsync -a --delete --exclude '.git' --exclude 'Logs' --exclude 'obj' --exclude 'Temp' \
          --exclude 'SmokeBuild' --exclude 'tests/Match3.Core.Tests/bin' \
          --exclude 'tests/Match3.Core.Tests/obj' "$REPO/" "$COPY/"
    cp "$REPO/tools/smoke/SmokeBuild.cs" "$COPY/Assets/Scripts/Editor/SmokeBuild.cs"
    cp "$REPO/tools/smoke/SmokeProbe.cs" "$COPY/Assets/Scripts/UI/SmokeProbe.cs"
    python3 "$REPO/tools/smoke/apply_seams.py" "$COPY"
    echo "prepared: $COPY"
    ;;

  build)
    "$UNITY" -batchmode -quit -nographics -projectPath "$COPY" \
             -executeMethod Match3.EditorTools.SmokeBuild.Build \
             -logFile "$SMOKE_DIR/build.log" >/dev/null 2>&1 || true
    if grep -q "SMOKE BUILD RESULT: Succeeded" "$SMOKE_DIR/build.log"; then
      echo "built: $COPY/SmokeBuild/match3.app"
    else
      grep -E "error CS|No valid Unity Editor license|another Unity instance" "$SMOKE_DIR/build.log" | sort -u | head -10
      echo "BUILD FAILED - see $SMOKE_DIR/build.log"; exit 1
    fi
    ;;

  run)
    NAME="$2"; PROGRESS="$3"; MODE="${4:-menu}"; TIMEOUT="${5:-30}"
    pkill -9 -f "match3.app" 2>/dev/null || true; sleep 1
    mkdir -p "$SAVE" "$SMOKE_DIR/shots/$NAME"
    # The very first run keeps the player's real save somewhere `restore` can find it.
    if [ ! -d "$SMOKE_DIR/save-backup" ]; then
      mkdir -p "$SMOKE_DIR/save-backup"
      for f in progress.sav meta.sav; do
        if [ -f "$SAVE/$f" ]; then cp "$SAVE/$f" "$SMOKE_DIR/save-backup/$f"; fi
      done
    fi
    set_aside "$NAME"
    if [ "$PROGRESS" != "none" ]; then cp "$PROGRESS" "$SAVE/progress.sav"; fi
    echo "$MODE" > "$SAVE/probe-mode.txt"
    # Empty the previous run's log BEFORE launching. The player only truncates it once
    # it is up, and a freshly built bundle can take seconds to start — long enough for
    # the loop below to read the last run's "PROBE done" and report on nothing.
    if [ -f "$LOG" ]; then : > "$LOG"; fi
    open "$COPY/SmokeBuild/match3.app"
    for i in $(seq 1 "$TIMEOUT"); do
      sleep 1
      if grep -q "PROBE done" "$LOG" 2>/dev/null; then break; fi
    done
    sleep 1
    echo "=== $NAME ==="; grep "^PROBE" "$LOG" || echo "(no PROBE lines - did the app start?)"
    for shot in "$SAVE/probe/"*.png; do cp "$shot" "$SMOKE_DIR/shots/$NAME/"; done
    if [ -d "$SAVE/probe/frames" ]; then cp -R "$SAVE/probe/frames" "$SMOKE_DIR/shots/$NAME/"; fi
    pkill -9 -f "match3.app" 2>/dev/null || true
    echo "output: $SMOKE_DIR/shots/$NAME"
    ;;

  gif)
    DIR="$SMOKE_DIR/shots/$2"
    ffmpeg -y -loglevel error -framerate 30 -i "$DIR/frames/f_%04d.png" \
           -vf "fps=15,scale=360:-1:flags=lanczos,palettegen=max_colors=160:stats_mode=diff" "$DIR/palette.png"
    ffmpeg -y -loglevel error -framerate 30 -i "$DIR/frames/f_%04d.png" -i "$DIR/palette.png" \
           -lavfi "fps=15,scale=360:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle" \
           "$DIR/gameplay.gif"
    # A contact sheet is the fast way to judge a take before reading 300 frames.
    ffmpeg -y -loglevel error -framerate 30 -i "$DIR/frames/f_%04d.png" \
           -vf "select='not(mod(n\,30))',scale=286:-1,tile=6x2" -frames:v 1 "$DIR/sheet.png"
    ls -la "$DIR/gameplay.gif" | awk '{printf "%s  %.2f MB\n", $9, $5/1048576}'
    ;;

  restore)
    pkill -9 -f "match3.app" 2>/dev/null || true; sleep 1
    set_aside restore
    for f in progress.sav meta.sav; do
      if [ -f "$SMOKE_DIR/save-backup/$f" ]; then cp "$SMOKE_DIR/save-backup/$f" "$SAVE/$f"; fi
    done
    echo "restored:"; ls "$SAVE"
    ;;

  *)
    sed -n '2,12p' "$0"; exit 1
    ;;
esac
