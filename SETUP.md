# Setup Guide — run it, regenerate it, and how it was built

The project in this repo is **complete and playable**: the scenes, the level
assets, the sprites, the sounds and the music are all committed. Nothing needs
wiring before you press Play.

So this guide reads in the order you actually need it:

1. **[Run it](#1-run-it)** — three steps, no Unity experience required.
2. **[Play it](#2-play-it)** — controls and the two modes.
3. **[Regenerate the assets](#3-regenerate-the-assets)** — the `Match3` editor menu,
   for when you change the palette, the level curve or the synths.
4. **[Run the tests](#4-run-the-tests)** — in the editor, or with no Unity at all.
5. **[Appendix](#appendix--how-the-scene-was-originally-built)** — how the scene was
   originally built. **Already done — do not follow it on a fresh clone.**

> **Mental model for Unity newcomers:** a *scene* is a tree of GameObjects; a
> GameObject is an empty shell you attach *components* (scripts) to; `[SerializeField]`
> fields in a script show up in the *Inspector* panel, and you "dependency-inject" by
> dragging another object onto that field. That wiring is saved in the scene file —
> which is why it ships committed here and you never have to redo it.

---

## 1. Run it

1. **Install Unity 2022.3 LTS.** Get **Unity Hub** from
   <https://unity.com/download>, then **Installs → Install Editor → Unity 2022.3
   LTS** (any 2022.3.x patch works; if Hub says the project wants a different patch,
   pick *Open with* your installed 2022.3). No extra modules are needed to play in
   the editor — *Android Build Support* is only for producing an APK.
2. **Add the project.** Unity Hub → **Projects → Add → Add project from disk** →
   select this repo folder (`unity-match3`), then open it. The first import takes a
   few minutes while Unity restores `Packages/manifest.json` (URP, TextMeshPro, Test
   Framework) and builds the git-ignored `Library/` folder.
3. **Open `Assets/Scenes/MainMenu.unity` and press Play.** That's it.

If a window titled **TMP Importer** appears, click **Import TMP Essentials**. (The
game ships its own pre-baked Baloo 2 / Nunito font assets in
`Assets/Resources/Fonts`, so this only affects Unity's own editor UI.)

**No Unity installed?** The rule layer still runs — see
[section 4](#4-run-the-tests).

## 2. Play it

Swap adjacent candies by pressing on one and dragging towards its neighbour;
useless swaps bounce back for free and cost nothing.

- **Moves campaign** (the main mode) — from the menu's level map, or press Play in
  `Assets/Scenes/Game.unity` to jump straight into `Resources/Levels/Level_01`.
  Complete the objectives shown at the top before the move counter runs out. Make
  4 / L / T / 5 shapes for striped, wrapped and colour-bomb candies, a 2×2 square
  for the jelly fish, and swap two specials together for combos.
- **Time attack** (the original endless mode) — the menu's TIME ATTACK button. A
  countdown against a rising score target, driven by the `Level1` config asset;
  big matches add seconds.

Sit idle and a hint pulses; if the board ever runs out of moves it auto-shuffles.
Progress saves itself to `persistentDataPath/progress.sav` (and to the cloud, if
UGS is configured — see [docs/UGS-SETUP.md](docs/UGS-SETUP.md)).

## 3. Regenerate the assets

Every sprite, sound, music loop and level asset in this repo was produced by code
in `Assets/Scripts/Editor`, so all of it is reproducible. You only need this after
changing a generator — the palette in `CandyArtist`, the campaign in `LevelCurve`,
the synths in `SfxSynth` / `MusicComposer`.

| Menu item | Produces |
|---|---|
| **Match3 → Generate → Candy Sprites** | `Assets/Sprites/Candies/*.png` (70 — the five colours × normal/striped/wrapped/fish/bomb, the same set again with colorblind badges, every blocker and the mystery egg), the five candy-town stages in `Assets/Resources/UI/Town`, and `Assets/Resources/CandySpriteLibrary.asset` |
| **Match3 → Generate → UI Sprites** | `Assets/Resources/UI/*.png` (10) — 9-slice cards and pills, outlines, star, padlock, circle, the baked gradients |
| **Match3 → Generate → Level Definitions** | `Assets/Resources/Levels/Level_01 … Level_120.asset` (120) + `Assets/Resources/LevelCatalog.asset` (objectives, blockers, star scores, tutorial lines) |
| **Match3 → Generate → Sound Effects** | `Assets/Resources/Audio/*.wav` — 10 synthesized clips |
| **Match3 → Generate → Music** | `Assets/Resources/Audio/Music/*.wav` — one loop-perfect track per chapter (deterministic: same chapter, same bytes) |
| **Match3 → Generate → Sprite Atlas** | `Assets/Sprites/CandyAtlas.spriteatlas` — one draw call for the candies |
| **Match3 → Generate → Font Assets** | the TMP SDF assets in `Assets/Resources/Fonts` — no runtime rasterization hitch |
| **Match3 → Setup → Apply Mobile Settings** | portrait lock, IL2CPP + ARM64, safe-area flag, vSync off, URP HDR/shadows off, Android ASTC overrides, mono SFX |
| **Match3 → Setup → Add Scenes To Build** | build list: `MainMenu` (0) + `Game` (1) — needed for scene switching |

Nothing in the scene references these assets directly: `BoardView` auto-loads the
sprite library from Resources, `AudioManager` builds itself on the first sound, and
every panel constructs its own UI at runtime.

## 4. Run the tests

**In the editor:** **Window → General → Test Runner** → **EditMode** tab → **Run
All**. Everything should be green. The **PlayMode** tab holds three scene-boot smoke
tests.

**Without Unity** — the rule layer is engine-free, so the same tests run on a plain
.NET 9 SDK:

```bash
dotnet test tests/Match3.Core.Tests
```

That project is tracked in git (the one `.csproj` the `.gitignore` lets through) and
globs `Assets/Scripts/Core` + `Assets/Tests/EditMode` straight out of the Unity
folders — no copying, no linking. It is also what CI runs on every push.

> Keep NUnit constraints **unchained** (`Assert.That(x, Is.EqualTo(y))`, not
> `Is.EqualTo(y).Within(z)`): the Unity Test Runner ships an older NUnit than
> `dotnet test`, so a chained form can pass headlessly and fail in the editor.

---

## Appendix — how the scene was originally built

> **You do not need any of this.** `Game.unity`, `MainMenu.unity`, the prefabs, the
> config assets and the URP settings are all committed. These steps are recorded so
> the scene can be rebuilt from nothing if it is ever lost — following them on a
> working clone means hand-editing exactly the scene and ProjectSettings files this
> project keeps stable.

<details>
<summary>Expand the original build-from-scratch steps</summary>

### A. URP (2D renderer)

1. `Assets` → right-click → **Create → Rendering → URP Asset (with 2D Renderer)**.
2. **Edit → Project Settings → Graphics** → set **Scriptable Render Pipeline
   Settings** to the created asset.
3. **Project Settings → Quality** → assign the same asset in the active level's
   **Rendering** dropdown.

*(The game renders identically on the built-in pipeline; URP is here because it is
the standard mobile production setup.)*

### B. Portrait aspect

1. **Project Settings → Player → Resolution and Presentation** → **Default
   Orientation: Portrait**.
2. Game view resolution dropdown → **+** → an **Aspect Ratio** entry `9:16`.

### C. The level config and tile sprite

1. Right-click `Assets/ScriptableObjects` → **Create → Match3 → Level Config**, name
   it `Level1`. Every time-attack number lives here: 8×8 board, 45s limit, target 120
   (+40 per level), +5s per 4-match, hint after 4s idle, five colours.
2. Right-click `Assets/Sprites` → **Create → 2D → Sprites → Circle**, name it
   `TileSprite`.

### D. The scene

**File → New Scene** (2D) → **Save As** `Assets/Scenes/Game.unity`, then **File →
Build Settings → Add Open Scenes**.

**The tile prefab:** an empty GameObject `Tile` with a **Sprite Renderer**
(`TileSprite`) and **Tile View** (drag the renderer onto its field), scale
`(0.9, 0.9, 1)` so the 0.1 gap draws the grid. Drag it into `Assets/Prefabs` and
delete it from the Hierarchy — the pool instantiates copies at runtime.

**The game objects** — three empties at `(0, 0, 0)`:

| GameObject | Components | Inspector wiring |
|---|---|---|
| `Board` | **Board View**, **Tile Pool** | BoardView.TilePool → the TilePool on this object. TilePool.TilePrefab → the `Tile` prefab. |
| `Input` | **Input Controller** | BoardView → the `Board` object. |
| `Game` | **Game Manager** | LevelConfig → `Level1`; BoardView → `Board`; InputController → `Input`. |

On **Main Camera**: add **Camera Fitter**, set its LevelConfig → `Level1`, and set
the background to a dark solid colour (e.g. `#1E2430`).

**The UI:** a **Canvas** with **Canvas Scaler** → Scale With Screen Size, reference
resolution 1080 × 1920, Match 0.5. Five TMP texts — `ScoreText` (top-center, 80),
`TimeText` (top-left, 64), `LevelText` (top-right, 48), `TargetText` (below score,
40) and an empty `MessageText` (middle-center, 72) for the "Level Complete!" /
"Shuffling…" banner. Add **Hud View** to the Canvas and wire GameManager → `Game`
plus each label. Everything else — the result panel, the menu, the settings overlay,
the boosters, every meta panel — is **built at runtime** and needs no wiring at all.

</details>

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Everything renders **magenta/pink** | URP pipeline asset not assigned — Project Settings → Graphics *and* Quality (appendix A). |
| `InvalidOperationException: ... is not assigned` on Play | GameManager's fail-fast check — an Inspector reference is missing (appendix D). |
| Text looks like squares / no text | TMP Essentials not imported — **Window → TextMeshPro → Import TMP Essential Resources**. |
| Clicks do nothing | The scene needs an **EventSystem** (created with the Canvas), and `Main Camera` must exist and be tagged `MainCamera`. |
| Tiles huge/tiny or off-screen | CameraFitter missing or its LevelConfig not assigned. |
| Tests don't appear in Test Runner | Let the compile finish (spinner, bottom-right), then reopen the Test Runner window. |
| `dotnet test` says the project is not found | Run it from the repo root: `dotnet test tests/Match3.Core.Tests`. It needs the .NET 9 SDK. |
