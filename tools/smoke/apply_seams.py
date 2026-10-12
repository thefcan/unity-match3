#!/usr/bin/env python3
"""Apply the three test seams to a COPY of the project (never to the project itself).

Each patch is anchored on an exact source snippet and fails loudly if the snippet has
moved, so a refactor shows up here as "anchor not found" rather than as a probe that
silently records nothing.
"""
import sys

copy = sys.argv[1]


def patch(relative, old, new, label):
    path = f"{copy}/Assets/Scripts/{relative}"
    source = open(path).read()
    if source.count(old) != 1:
        sys.exit(f"apply_seams: anchor for '{label}' found {source.count(old)}x in {relative} — update tools/smoke/apply_seams.py")
    open(path, "w").write(source.replace(old, new, 1))
    print(f"  seam: {label}")


# 1. No cloud. CloudSync.Boot does pull -> max-star merge -> PUSH, so an unlocked test
#    save would be merged into the standalone identity's cloud slot and come straight
#    back on every later launch.
patch("Cloud/CloudSync.cs",
      """            SceneManager.sceneLoaded += (_, _) => HookGameManager();
            HookGameManager();
            _ = InitializeAsync();""",
      """            return; // SMOKE COPY ONLY: no sign-in, no pull, no push""",
      "cloud sync cut off")

# 2. A swap without a pointer, through the REAL input event — so PlayingState handles
#    it exactly as it would a finger (boosters, hints and all).
patch("View/InputController.cs",
      """        public event Action<GridPosition, GridPosition> SwapRequested;""",
      """        public event Action<GridPosition, GridPosition> SwapRequested;

        /// <summary>SMOKE COPY ONLY: a swap through the real input path, without a pointer.</summary>
        public void DebugSwap(GridPosition from, GridPosition to) => SwapRequested?.Invoke(from, to);""",
      "InputController.DebugSwap")

# 3. A seedable board, so a recorded take can be shot again.
patch("Game/GameManager.cs",
      """            _random = new SystemRandom();""",
      """            _random = SmokeSeed >= 0 ? new SystemRandom(SmokeSeed) : new SystemRandom(); // SMOKE COPY ONLY""",
      "seeded board RNG")
patch("Game/GameManager.cs",
      """        public const int RelaxedMovesLimit = 999;""",
      """        public const int RelaxedMovesLimit = 999;

        /// <summary>SMOKE COPY ONLY: a fixed seed makes a recorded take repeatable.</summary>
        public static int SmokeSeed = -1;""",
      "GameManager.SmokeSeed")
