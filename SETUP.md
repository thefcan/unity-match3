# Setup Guide — from zero Unity experience to a playable game

Every script in this repo is complete; what's left is the part Unity stores in
binary/scene assets: creating the scene, two assets, and connecting Inspector
references. Follow this top to bottom — about 20 minutes the first time.

> **Mental model for Unity newcomers:** a *scene* is a tree of GameObjects; a
> GameObject is an empty shell you attach *components* (scripts) to; `[SerializeField]`
> fields in a script show up in the *Inspector* panel, and you "dependency-inject" by
> dragging another object onto that field. That wiring is saved in the scene file.

---

## 1. Install Unity

1. Install **Unity Hub** from <https://unity.com/download>.
2. In Unity Hub → **Installs → Install Editor**, pick **Unity 2022.3 LTS** (any
   2022.3.x patch works; if Hub complains the project wants 2022.3.45f1, just choose
   *Open with* your installed 2022.3 version).
3. No extra modules are needed to run in the editor. (Add *Android Build Support*
   later only if you want an APK.)

## 2. Open the project

1. Unity Hub → **Projects → Add → Add project from disk** → select this repo folder
   (`unity-match3`).
2. Open it. The first import takes a few minutes — Unity downloads the packages in
   `Packages/manifest.json` (URP, TextMeshPro, Test Framework) and generates the
   `Library/` folder (which is git-ignored).
3. If a window titled **TMP Importer** appears at any point: click
   **Import TMP Essentials**. (If it doesn't appear now, it will when you create the
   first text element in step 7 — import it then.)

## 3. Set up URP (2D renderer)

The URP *package* is already installed; Unity just needs a pipeline asset:

1. In the **Project** panel, open the `Assets` folder. Right-click →
   **Create → Rendering → URP Asset (with 2D Renderer)**. Keep the default names
   (it creates two files). You can drag them into a `Assets/Settings` folder you create, to keep things tidy.
2. **Edit → Project Settings → Graphics** → set **Scriptable Render Pipeline Settings**
   to the created `New Universal Render Pipeline Asset`.
3. Still in Project Settings → **Quality** → in the **Rendering** dropdown of the
   active quality level, assign the same asset.

*(Skip-able: the game renders identically on the built-in pipeline; URP is here
because it's the standard mobile production setup.)*

## 4. Portrait aspect

1. **Edit → Project Settings → Player → Resolution and Presentation** → set
   **Default Orientation** to **Portrait**.
2. In the **Game** view's resolution dropdown (top-left of the Game panel), click
   **+** and add a **Aspect Ratio** entry `9:16`. Select it.

## 5. Create the level config and tile sprite

1. Project panel → right-click `Assets/ScriptableObjects` →
   **Create → Match3 → Level Config**. Name it `Level1`. Click it once and look at
   the Inspector — every gameplay number lives here: 8×8 board, **45s** time limit,
   target **120** (+40 each level), **+5s** per 4-match, hint after 4s idle, five
   colours. Tune these any time, even while playing, to rebalance the game.
2. Right-click `Assets/Sprites` → **Create → 2D → Sprites → Circle** (or Square for a
   blockier look). Name it `TileSprite`.

## 6. Build the scene

**File → New Scene** (2D template) → **File → Save As** → `Assets/Scenes/Game.unity`.
Then add it to builds: **File → Build Settings → Add Open Scenes**.

### 6a. The tile prefab

1. Hierarchy panel → right-click → **Create Empty**, name it `Tile`.
2. With `Tile` selected: **Add Component → Sprite Renderer**; drag `TileSprite` into
   its **Sprite** field.
3. **Add Component → Tile View** (our script). Drag the **Sprite Renderer** component
   (grab its header) onto the script's **Sprite Renderer** field.
4. Set the Tile's **Transform → Scale** to `(0.9, 0.9, 1)` — the 0.1 gap draws the grid.
5. Drag the `Tile` object from the Hierarchy into `Assets/Prefabs` in the Project
   panel — this creates the prefab. Then **delete** the `Tile` from the Hierarchy
   (the pool will instantiate copies at runtime).

### 6b. The game objects

Create three empty GameObjects in the Hierarchy (right-click → Create Empty), all at
position `(0, 0, 0)`:

| GameObject | Components to add | Inspector wiring |
|---|---|---|
| `Board` | **Board View**, **Tile Pool** | BoardView.TilePool → the TilePool component on this same object. TilePool.TilePrefab → the `Tile` prefab from `Assets/Prefabs`. |
| `Input` | **Input Controller** | BoardView → the `Board` object. |
| `Game` | **Game Manager** | LevelConfig → `Level1` asset; BoardView → `Board`; InputController → `Input`. |

On the existing **Main Camera**: **Add Component → Camera Fitter**; set its
LevelConfig → `Level1`. Also set the camera's **Background** color to something dark
(e.g. `#1E2430`) — Environment → Background Type: Solid Color.

### 6c. The UI

1. Hierarchy → right-click → **UI → Canvas**. On the Canvas:
   - **Canvas Scaler** component → UI Scale Mode: **Scale With Screen Size**,
     Reference Resolution **1080 × 1920**, Match: **0.5**.
2. Right-click the Canvas → **UI → Text - TextMeshPro** (import TMP Essentials if
   prompted — assign **LiberationSans SDF** as the Font Asset). Make five of them.
   The first four are the readouts; `MessageText` is a big centre banner that stays
   empty until a "Level Complete!" / "Shuffling…" moment fills it:

   | Name | Anchor (Anchor Presets box, hold Alt+Shift while clicking) | Text | Font size |
   |---|---|---|---|
   | `ScoreText` | top-center | `0` | 80 |
   | `TimeText` | top-left | `45.0s` | 64 |
   | `LevelText` | top-right | `Level 1` | 48 |
   | `TargetText` | top-center (below score) | `Target 120` | 40 |
   | `MessageText` | middle-center | *(leave empty)* | 72 |

   Nudge the readouts inwards so they don't touch the screen edge.
3. Add the HUD script: select the **Canvas** → **Add Component → Hud View** → wire
   GameManager → `Game`, then Score/Time/Target/Level Text → their labels, and
   **Message Text → `MessageText`** (this last one is optional — without it the pause
   still happens, just no banner). Leave the colour / threshold fields at defaults.
4. That's the whole overlay story: the end-of-level panel (win / out-of-moves /
   time's-up, with star pips and Next/Retry/Level Map buttons) is **built at
   runtime** by `LevelResultPanel` — it attaches itself to any scene that has a
   Canvas and a GameManager, so there is nothing to wire.

5. Save the scene (Cmd+S).

## 7. Run the tests

**Window → General → Test Runner** → **EditMode** tab → **Run All**.
All tests should be green — they exercise the board logic (match detection, gravity,
cascades, scoring) with zero scene dependencies, which is exactly why that code lives
in an engine-free assembly.

## 8. Play

Press **Play**. Swap adjacent candies by pressing on one and dragging towards its
neighbour (useless swaps bounce back for free).

- **Moves campaign (default):** the Game scene plays `Resources/Levels/Level_01`
  unless you arrived via the level map. Complete the objectives shown at the top
  before the move counter runs out; make 4 / L / T / 5 shapes for striped, wrapped
  and colour-bomb candies, and swap specials into each other for combos.
- **Time attack:** start it from the MainMenu scene's button — the original
  countdown/target rules, driven by the `Level1` config asset.

Sit idle and a hint pulses; if the board ever has no moves it auto-shuffles. None of
these extras need wiring — they're pure code driven off data assets.

## 8b. The Candy-Crush layer — everything is generated

The campaign content and all art/audio ship as generated assets. To regenerate any
of them (or after changing the palette / `LevelCurve`), use the **Match3** menu:

| Menu item | Produces |
|---|---|
| **Match3 → Generate → Candy Sprites** | `Assets/Sprites/Candies/*.png` (21) + `Assets/Resources/CandySpriteLibrary.asset` |
| **Match3 → Generate → Level Definitions** | `Assets/Resources/Levels/Level_01..20.asset` + `Assets/Resources/LevelCatalog.asset` |
| **Match3 → Generate → Sound Effects** | `Assets/Resources/Audio/*.wav` (10 synthesized clips) |
| **Match3 → Setup → Add Scenes To Build** | Build list: `MainMenu` (0) + `Game` (1) — needed for scene switching |

Nothing in the scene references these directly: `BoardView` auto-loads the sprite
library from Resources, `AudioManager` builds itself on the first sound, the result
panel and main menu construct their own UI, and progress saves itself to
`persistentDataPath/progress.sav`.

## 9. First commit

Unity has now generated a `.meta` file next to every asset — these carry the stable
GUIDs that Inspector references point at, so they **must be committed** (the
`.gitignore` already handles everything else):

```bash
git init && git add . && git commit -m "Match-3 puzzle game: engine-free core, tests, Unity view layer"
```

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Everything renders **magenta/pink** | URP pipeline asset not assigned — redo step 3 (both Graphics *and* Quality). |
| `InvalidOperationException: ... is not assigned` on Play | That's GameManager's fail-fast check — an Inspector reference from the 6b table is missing. |
| Text looks like squares / no text | TMP Essentials not imported — **Window → TextMeshPro → Import TMP Essential Resources**. |
| Clicks do nothing | The scene needs an **EventSystem** for UI (created automatically with the Canvas) and the `Input` object must be wired to `Board`. Board clicks specifically: check `Main Camera` exists and is tagged `MainCamera`. |
| Tiles huge/tiny or off-screen | CameraFitter missing or its LevelConfig not assigned. |
| Tests don't appear in Test Runner | Let the compile finish (spinner bottom-right), then reopen the Test Runner window. |
