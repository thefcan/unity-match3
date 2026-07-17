# Candy Match — a Candy-Crush-style match-3 built for architecture

<p align="center">
  <img src="docs/candy-set.png" alt="The procedurally generated candy set — five silhouettes plus striped, wrapped and colour-bomb specials" width="780">
</p>

A complete match-3 with **two modes**: a Candy-Crush-style **moves campaign** (60
levels in three slowly-shifting chapters, objectives, special candies, jelly, star
ratings, saved progress) and the original **endless time-attack**. Deliberately built so the focus stays on **code
architecture** — an engine-free, unit-tested C# core, a thin MonoBehaviour view
layer, and classic design patterns used where they pull their weight.

<p align="center">
  <img src="docs/design-main-menu.png" alt="Main menu with the scrollable level map" width="230">
  &nbsp;
  <img src="docs/design-hud.png" alt="In-game HUD with objective chips and the jelly rows" width="230">
  &nbsp;
  <img src="docs/design-level-complete.png" alt="Level-complete panel with the star trio and gold score" width="230">
</p>
<p align="center"><sub><i>UI design previews (Stitch + Figma) — the game builds this exact language at runtime, no scene wiring.</i></sub></p>

**The ambience drifts with the campaign** — every 20-level chapter slides towards a
new palette (purple night → ocean teal → dusk plum), one gentle step per level:

<p align="center">
  <img src="docs/design-hud-ocean.png" alt="Chapter 2 ambience — ocean teal" width="230">
  &nbsp;
  <img src="docs/design-hud-plum.png" alt="Chapter 3 ambience — dusk plum" width="230">
</p>

> 🎬 *gameplay GIF placeholder — record with Cmd+Shift+5 on macOS and drop it here as `docs/gameplay.gif`*

**Stack:** Unity 2022.3 LTS · 2D URP · TextMeshPro · Unity Test Framework (NUnit) ·
no third-party assets — candy sprites, UI chrome and sound effects are all
**procedurally generated**, the UI implements a Figma-authored design language
(Baloo 2 + Nunito), and all "juice" is hand-rolled coroutine tweens + one
runtime-built ParticleSystem.

## Gameplay

### Moves campaign (Candy Crush style — the main mode)

- **60 authored levels** on a scrollable level map, sequentially unlocked, each with
  a **move limit** and **objectives** shown as icon chips over the board: reach a
  score, collect N candies of a colour, or **clear all the jelly**.
- **Chapters that drift, never jump.** Every 20 levels is a chapter with its own
  ambience — purple night → ocean teal → dusk plum — and each level interpolates
  1/20th of the way towards the next palette (`ThemeCurve`, unit-tested to never
  shift a colour channel more than 0.02 per level). Difficulty repeats the chapter
  rhythm one notch harder; candy colours and controls never change.
- **Jelly blockers** (from level 8): translucent cells under the candies, in one or
  two layers. A match on a jelly cell peels one layer; jelly sticks to the CELL, so
  candies fall through it. Late levels widen the jelly and double its layers.
- **Special candies** from match shapes:

  | Shape | Candy | Detonation |
  |---|---|---|
  | 4 in a line | **Striped** (perpendicular) | clears a full row / column |
  | L or T | **Wrapped** | 3×3 blast — **twice** (survives, falls, re-detonates) |
  | 5 in a line | **Colour bomb** | clears every candy of one colour |

- **Special + special swaps:** striped+striped = cross; striped+wrapped = triple
  cross; wrapped+wrapped = two 5×5 blasts; bomb+normal = that colour wiped;
  bomb+striped = that colour *converted to striped and all detonated*;
  bomb+wrapped = colour wipe + double blast; bomb+bomb = **board wipe**.
  Activation swaps never bounce back — a bomb is always a legal move.
- **Chain reactions:** any special caught in a blast goes off too, within the wave.
- **Win** = all objectives complete (unused moves cash out as bonus points *before*
  the 1–3 **star rating**); **lose** = out of moves. Stars and unlocks are **saved**
  (`progress.sav` in `persistentDataPath` — plain `level=stars` lines).

### Time attack (the original endless mode)

Race a countdown to rising score targets; 4+ matches add seconds; endless levels on
the same board. Reachable from the main menu; all its original rules are intact.

Shared by both modes: cascades with rising multipliers, auto-shuffle on dead boards
(a board holding a colour bomb is never dead), idle move hints, drag-to-swap input.

## Architecture

The rule of the codebase: **logic decides, views obey.** All game rules live in
`Match3.Core`, a separate assembly compiled with `noEngineReferences: true` — the
compiler physically rejects `using UnityEngine` there. MonoBehaviours render, animate
and forward input; they never decide anything.

A player move flows one way: `InputController` raises an event → the current
`GameState` validates it → `CascadeResolver.ResolveSwap` mutates the `Board` and
returns a **recording** (`CascadeStep[]`: what cleared, what **morphed into a
special** (`SpecialCreation`), what **detonated** (`Detonation` — kind + area, in
chain order), which **jelly layers came off** (`JellyHit`), what fell, what spawned,
wave by wave) → `BoardView` animates the recording (staggered blast pops,
converge-and-morph beats, jelly pops) → C# events update the HUD. The view never
re-derives rules, so logic and presentation can't drift apart.

Core rule units, each small and independently tested:

- `Board` — match runs, gravity, refill, possible moves (incl. activation swaps), shuffle
- `JellyGrid` — the per-CELL blocker layer; matches damage it, gravity ignores it
- `SpecialMatchAnalyzer` — match *shape* → which special is born, and in which cell
- `DetonationRules` — pure blast geometry (rows, columns, blasts, colour/board wipes)
- `SwapRules` — classifies special+special / bomb swaps
- `CascadeResolver` — the wave loop: combos → matches → creations → detonation
  worklist (chains, wrapped double-blast) → jelly damage → score → clear/morph →
  gravity → refill
- `ObjectiveTracker` / `StarCalculator` / `PlayerProgress` — moves-mode win logic & save
- `LevelCurve` / `ThemeCurve` — the 60-level difficulty curve and the per-chapter
  ambience drift (single source for generated assets and runtime tinting)
- `CandyArtist` / `UiArtist` / `SfxSynth` — procedural candy sprites, UI chrome and
  sounds (pure pixel/sample math, no engine types)

### Game flow (State pattern)

```mermaid
stateDiagram-v2
    [*] --> Init
    Init --> Playing : board built (shuffles first if dead)
    Playing --> Resolving : swap gesture
    Resolving --> Playing : nothing happened (bounce back)
    Resolving --> LevelWon : objectives complete (moves mode)
    Resolving --> LevelFailed : out of moves (moves mode)
    Resolving --> LevelComplete : target reached (time attack)
    Resolving --> Shuffling : no moves left on board
    Resolving --> GameOver : clock ran out (time attack)
    LevelComplete --> Playing : after the celebration beat
    Shuffling --> Playing : board reshuffled
    LevelWon --> Init : Next / Replay
    LevelFailed --> Init : Retry
    GameOver --> Init : Restart
```

## Design patterns used (and why)

| Pattern | Where | Why it earns its place |
|---|---|---|
| **State** | `Scripts/Game/States/` | Each phase's behaviour and its input/clock rules live in one class; no `if (isBusy)` flags anywhere. |
| **Observer** (C# `event`) | `GameManager` → `HudView`, `LevelResultPanel` | UI subscribes to score / moves / objectives / win / fail / game-over. GameManager has zero references to UI types. |
| **Object Pool** | `TilePool`, `ScorePopup` | Constant clear/respawn churn without Instantiate/Destroy GC spikes. |
| **Factory** | `TileFactory` | Single creation point: unique tile IDs, injected randomness, and the only place special candies are minted. |
| **ScriptableObject config** | `LevelConfig`, `LevelDefinition`, `LevelCatalog`, `CandySpriteLibrary` | Levels, palette, and sprite lookups are data assets. |
| **Strategy-ish rule units** | `SpecialMatchAnalyzer` / `DetonationRules` / `SwapRules` | The resolver stays a loop; the candy rules stay unit-testable functions. |

Two supporting ideas: **dependency inversion** on randomness (`IRandom` is injected
into the factory, shuffle *and* resolver, so tests script every dice roll) and on
persistence (`IProgressRepository`), and **runtime-built UI** (result panel, main
menu, objective chips, HUD card, effects, audio) — no fragile scene wiring; each
builds itself from code and `Resources/`.

## Design language

The UI implements a Figma-authored design ("Candy Match — Game UI"): a deep
purple-navy gradient, rounded cards, pill CTAs with a pink gradient, gold star
pips, and a **Baloo 2 / Nunito** type pairing (TTFs ship in `Resources/Fonts`,
turned into TMP font assets at runtime). The whole language lives in one code
surface — [`UiTheme`](Assets/Scripts/UI/UiTheme.cs) mirrors the Figma colour
variables, fonts and generated sprites, so restyling the game is a one-file edit.

## Generated assets — no art or audio dependencies

Everything visual/audible ships generated, and can be regenerated inside Unity:

- **Match3 → Generate → Candy Sprites** — 21 PNGs (5 colours × normal/stripedH/
  stripedV/wrapped + colour bomb) drawn by `CandyArtist`: one silhouette per colour
  (circle/square/triangle/diamond/hexagon) so candies stay tellable-apart without
  colour vision.
- **Match3 → Generate → UI Sprites** — the design's chrome from `UiArtist`:
  9-slice rounded cards and pills (+outline rings), star, padlock, circle, and the
  baked background/CTA gradients.
- **Match3 → Generate → Level Definitions** — the 60 campaign levels + catalog
  from `LevelCurve` (jelly rows included).
- **Match3 → Generate → Sound Effects** — 10 WAVs synthesized by `SfxSynth`.
- **Match3 → Setup → Add Scenes To Build** — registers MainMenu + Game scenes.

## Testing

**203 EditMode tests, all green** — the core is tested without ever opening a scene:

```
Assets/Tests/EditMode/
├── MatchDetectionTests.cs        runs of 3/4, L-shapes counted once, no false positives
├── BoardTests.cs                 no-match initial fill (30 seeds), swap mechanics, factory rules
├── GravityTests.cs               falling, identity preservation, refill stacking
├── CascadeResolverTests.cs       chain reactions, multipliers, board stability after resolve
├── MatchRunTests.cs              per-run lengths → big-match (4+) detection
├── BoardRecoveryTests.cs         find-a-move, dead boards, colour-preserving shuffle
├── SpecialMatchAnalyzerTests.cs  4/L/T/5 shapes → striped/wrapped/bomb, placement rules
├── DetonationTests.cs            blast geometry + wrapped double-blast + chain order
├── SwapComboTests.cs             all special+special / bomb swaps, no-bounce activation
├── SpecialBoardTests.cs          bombs never colour-match, bomb keeps a board playable
├── ObjectiveTrackerTests.cs      collection/score objectives, star thresholds
├── JellyTests.cs                 jelly damage/recording, double layers, morph-cell hits, curve
├── ThemeCurveTests.cs            chapter anchors, drift-rate bound, 60-level campaign rhythm
└── ProgressTests.cs              save roundtrip, corrupt input, unlocks, level curve
```

Plus **3 PlayMode smoke tests** (`Assets/Tests/PlayMode/SceneSmokeTests.cs`) that
boot the real scenes: the Game scene builds a full match-free board in Moves mode
with the runtime UI attached, TimeAttack starts with a running clock, and the
MainMenu builds its 60-row level map. These catch what unit tests can't — broken
scene references, missing Resources assets, lifecycle ordering.

Run in Unity via **Window → General → Test Runner** (EditMode and PlayMode tabs),
or headless without Unity (the core is plain C#):

```bash
dotnet test   # a csproj that links Assets/Scripts/Core + Assets/Tests/EditMode
```

## Project structure

```
Assets/
├── Scripts/
│   ├── Core/        ← Match3.Core.asmdef (noEngineReferences) — board, jelly grid,
│   │                  resolver, special-candy rules, objectives, progress, level
│   │                  curve, CandyArtist + UiArtist + SfxSynth generators
│   ├── Game/        ← GameManager, GameSession, LevelConfig/LevelDefinition/
│   │                  LevelCatalog, AudioManager, ProgressService, States/
│   ├── View/        ← BoardView (incl. jelly overlay), TileView, TilePool,
│   │                  InputController, EffectsView, ScorePopup, CameraFitter
│   ├── UI/          ← UiTheme + HudView, ObjectiveBarView, LevelResultPanel,
│   │                  MainMenuView (all runtime-built)
│   └── Editor/      ← Match3.Editor.asmdef — sprite/UI/level/SFX generators, scene setup
├── Tests/EditMode/  ← NUnit tests for the core (dotnet-runnable)
├── Tests/PlayMode/  ← scene-boot smoke tests
├── Resources/       ← CandySpriteLibrary, LevelCatalog, Levels/, Audio/, Fonts/, UI/
├── Prefabs/ · Scenes/ (MainMenu + Game) · ScriptableObjects/ · Sprites/Candies/
```

## Run it

1. Clone, open with Unity 2022.3 LTS via Unity Hub.
2. Open `Assets/Scenes/MainMenu.unity` (or `Game.unity` to jump straight into
   level 1) and press Play. All required assets ship generated; the SETUP.md wiring
   guide is only needed if you rebuild the Game scene from scratch.
3. Drag a candy towards a neighbour to swap. Make 4/L/T/5 shapes for specials, swap
   specials together for combos, finish the objectives before the moves run out.

## Scope cuts (deliberate)

Kept out to leave obvious seams to grow from: **frosting-style blockers that occupy
the cell** (jelly shipped and shows the pattern — a state grid beside the board, a
per-step recording list, an `ObjectiveType`), **non-rectangular boards**,
**ingredients**, **boosters/economy**. The objective model and `TileFactory` are
the intended extension points.
