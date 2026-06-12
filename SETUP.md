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
   the Inspector: 8×8 board, 20 moves, target 2000, five colours — all editable. This
   asset IS the level; make a `Level2` later by duplicating and tweaking.
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
   prompted). Make three of them:

   | Name | Anchor (use the Anchor Presets box, hold Alt+Shift while clicking) | Text | Font size |
   |---|---|---|---|
   | `ScoreText` | top-left | `0` | 72 |
   | `MovesText` | top-right | `Moves: 20` | 48 |
   | `TargetText` | top-center | `Target: 2000` | 48 |

   Nudge them inwards so they don't touch the screen edge (e.g. ±40 px).
3. Add the HUD script: select the **Canvas** → **Add Component → Hud View** → wire
   GameManager → `Game`, and the three text fields to the three labels.
4. Game-over overlay, as a child of the Canvas:
   - Right-click Canvas → **UI → Panel**, name it `GameOverPanel`. Set its Image
     color to black with ~200 alpha (the dim background).
   - Inside it: a **Text - TextMeshPro** named `TitleText` (`You Win!`, size 96,
     centered, anchored center, pos Y ≈ +150), another named `SummaryText`
     (`Score: 0`, size 56, pos Y ≈ 0), and a **UI → Button - TextMeshPro** named
     `RestartButton` (label `Play Again`, pos Y ≈ -200, width ≈ 420, height ≈ 110).
   - Select the **Canvas** again → **Add Component → Game Over Panel** → wire:
     GameManager → `Game`, PanelRoot → the `GameOverPanel` object, TitleText,
     SummaryText, RestartButton → their objects.
   - The script hides the panel on start, so you can leave it visible while editing.

   > Why does the script sit on the Canvas instead of the panel? A disabled
   > GameObject never receives events — the listener must live on something that
   > stays active while the panel is hidden.

5. Save the scene (Cmd+S).

## 7. Run the tests

**Window → General → Test Runner** → **EditMode** tab → **Run All**.
All tests should be green — they exercise the board logic (match detection, gravity,
cascades, scoring) with zero scene dependencies, which is exactly why that code lives
in an engine-free assembly.

## 8. Play

Press **Play**. Swap adjacent tiles by pressing on a tile and dragging towards its
neighbour. Useless swaps bounce back without costing a move. Reach 2000 points within
20 moves.

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
