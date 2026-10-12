# Smoke tooling — drive a real player build without touching it

Compile checks and unit tests cannot see a button drawn on top of another
button, a 0px gap, or a chapter whose palette blends to grey. All three shipped;
all three were obvious in one screenshot. This folder is how those screenshots —
and `docs/gameplay.gif` — are made.

It lives **outside `Assets/`** on purpose: Unity never compiles it, so the shipped
game carries no test seams at all.

## What it does

`smoke.sh prepare` rsyncs the project into a scratch copy and adds, **to the copy only**:

| Added to the copy | Why |
|---|---|
| `SmokeBuild.cs` | a headless macOS player build (`-executeMethod`) |
| `SmokeProbe.cs` | drives the UI itself, logs `PROBE …` lines, writes PNGs |
| `CloudSync.Boot` returns early | boot does pull → max-star merge → **push**; a test save would land in the cloud slot and come back on every later launch |
| `InputController.DebugSwap` | a swap through the real `SwapRequested` event, so `PlayingState` handles it like a finger |
| `GameManager.SmokeSeed` | a fixed board seed, so a recorded take can be shot again |

The seams are applied by `apply_seams.py`, anchored on exact source snippets — if a
refactor moves one, it stops with "anchor not found" instead of recording nothing.

Why a probe instead of clicking: synthetic clicks do not reach the Unity player
(they report success and nothing happens), and screen recording needs a human at
the keyboard. The probe needs neither.

## Use

```bash
tools/smoke/smoke.sh prepare
tools/smoke/smoke.sh build                       # needs Unity Hub signed in; the editor may stay open (this builds the copy)

# look at the menu + daily tasks for a brand-new player
tools/smoke/smoke.sh run fresh none menu

# ...or for any save: a progress file is just "level=stars" lines
printf '%s\n' {1..60}=1 > /tmp/mid.sav
tools/smoke/smoke.sh run mid /tmp/mid.sav menu

# record 11 seconds of level 108 on seed 7, then encode it
tools/smoke/smoke.sh run take none "record 108 7 11" 90
tools/smoke/smoke.sh gif take                    # -> gameplay.gif + sheet.png (contact sheet)

tools/smoke/smoke.sh restore                     # put your real standalone save back
```

Output lands in `$SMOKE_DIR/shots/<name>/` (default `$TMPDIR/match3-smoke`).
`docs/gameplay.gif` is exactly `run take none "record 108 7 11" 90` + `gif take`.

## Things that will bite you

- **It swaps the standalone player's save** (`~/Library/Application
  Support/com.DefaultCompany.unity-match3/`). The first `run` backs the real one up;
  `restore` puts it back. Files are moved aside, never deleted. The *editor's* save
  lives elsewhere and is never touched.
- **Do not use `Time.captureFramerate` to record.** It fixes `deltaTime` only;
  `unscaledDeltaTime` keeps following the wall clock (measured 0.0333 vs 0.0167),
  and every `UiTween` runs on unscaled time — the HUD plays at half speed against
  the board. The probe caps `targetFrameRate` at 30 and captures in real time.
- **Level openers show a tutorial veil** (101, 106, 111, 116 …). Record a level
  that is not one.
- **A freshly built bundle starts slowly.** `run` empties the previous `Player.log`
  before launching for that reason — otherwise it reads the last run's "PROBE done".
- `gif` needs `ffmpeg`.
