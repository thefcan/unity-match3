using System.Collections;
using System.Globalization;
using System.IO;
using Match3.Core;
using Match3.Game;
using Match3.View;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// TEST-COPY ONLY. Drives the game itself and writes what it sees to Player.log and
    /// to PNGs, so a smoke run needs no synthetic input and no screen takeover.
    /// Mode comes from persistentDataPath/probe-mode.txt:
    ///   menu                              - label + missions + menu/tasks screenshots
    ///   record LEVEL SEED SECONDS         - jump into a level, auto-play, dump frames
    /// </summary>
    public sealed class SmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Application.runInBackground = true; // keep ticking even if focus is elsewhere
            string modePath = Path.Combine(Application.persistentDataPath, "probe-mode.txt");
            string[] parts = File.Exists(modePath)
                ? File.ReadAllText(modePath).Trim().Split(' ')
                : new[] { "menu" };

            var go = new GameObject("SmokeProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<SmokeProbe>();
            if (parts[0] == "record")
                probe.StartCoroutine(probe.Record(int.Parse(parts[1]), int.Parse(parts[2]),
                                                  float.Parse(parts[3], CultureInfo.InvariantCulture)));
            else
                probe.StartCoroutine(probe.Menu());
        }

        private static string Dir
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "probe");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private IEnumerator Menu()
        {
            yield return new WaitForSecondsRealtime(3f);

            GameObject cta = GameObject.Find("ContinueButton");
            string label = cta != null ? cta.GetComponentInChildren<TMP_Text>().text : "<no ContinueButton>";
            PlayerProgress progress = ProgressService.Current;
            Debug.Log($"PROBE cta=\"{label}\" highestUnlocked={progress.HighestUnlocked} totalStars={progress.TotalStars}");

            MetaState meta = MetaService.Current;
            Debug.Log($"PROBE before-open missionDay={meta.MissionDay} frontier={meta.MissionFrontier}");
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "menu.png"));
            yield return new WaitForSecondsRealtime(0.8f);

            GameObject tasks = GameObject.Find("TasksButton");
            if (tasks != null)
            {
                tasks.GetComponent<Button>().onClick.Invoke(); // the panel's own Refresh rolls the day
                yield return new WaitForSecondsRealtime(1.5f);
                Debug.Log($"PROBE after-open missionDay={meta.MissionDay} frontier={meta.MissionFrontier}");
                for (int slot = 0; slot < MissionCatalog.DailyCount; slot++)
                {
                    MissionDef def = MissionService.DefForSlot(slot);
                    Debug.Log($"PROBE mission[{slot}]={def.Type}({def.Param},{def.Target})");
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "tasks.png"), 2);
                yield return new WaitForSecondsRealtime(0.8f);
            }
            Debug.Log("PROBE done");
        }

        private IEnumerator Record(int level, int seed, float seconds)
        {
            string frames = Path.Combine(Dir, "frames");
            Directory.CreateDirectory(frames);
            yield return new WaitForSecondsRealtime(1.5f);

            GameManager.SmokeSeed = seed;
            var catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            GameSession.Mode = GameMode.Moves;
            GameSession.SelectedLevel = catalog.Get(level);
            GameSession.SelectedLevelIndex = level;
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            GameManager game = null;
            while (game == null)
            {
                game = FindObjectOfType<GameManager>();
                yield return null;
            }
            while (game.CurrentPhase != GamePhase.Playing)
                yield return null;
            yield return new WaitForSecondsRealtime(1.2f); // the intro curtain settles

            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "still.png"), 2);
            yield return new WaitForSecondsRealtime(0.8f);

            var input = FindObjectOfType<InputController>();
            // Real time at a capped 30 fps, NOT Time.captureFramerate: that only fixes
            // deltaTime, while unscaledDeltaTime keeps following the wall clock — so
            // every UiTween (unscaled by house rule) would play at the wrong speed
            // against the board in the recording.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            yield return null;
            yield return null;
            int total = Mathf.RoundToInt(seconds * 30f);
            int idle = 0, moves = 0;
            for (int frame = 0; frame < total; frame++)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(frames, $"f_{frame:0000}.png"));
                if (frame == 5)
                    Debug.Log($"PROBE dt={Time.deltaTime:F4} unscaledDt={Time.unscaledDeltaTime:F4} " +
                              $"screen={Screen.width}x{Screen.height}");

                if (game.CurrentPhase == GamePhase.Playing)
                {
                    idle++;
                    if (idle >= 14 && TryPickMove(game.Board, out GridPosition a, out GridPosition b, out int eggs))
                    {
                        moves++;
                        Debug.Log($"PROBE frame {frame} move {moves}: ({a.X},{a.Y})->({b.X},{b.Y}) eggsNear={eggs}");
                        input.DebugSwap(a, b);
                        idle = 0;
                    }
                }
                else
                {
                    idle = 0;
                }
                yield return null;
            }
            Application.targetFrameRate = 60;
            Debug.Log($"PROBE recorded {total} frames, {moves} moves, score={game.Score}");
            Debug.Log("PROBE done");
        }

        /// <summary>The legal swap with the most mystery eggs within two cells of it.</summary>
        private static bool TryPickMove(Board board, out GridPosition from, out GridPosition to, out int bestEggs)
        {
            from = default;
            to = default;
            bestEggs = -1;
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var a = new GridPosition(x, y);
                    foreach (GridPosition b in new[] { new GridPosition(x + 1, y), new GridPosition(x, y + 1) })
                    {
                        if (!board.IsInside(b) || board.IsImmobile(a) || board.IsImmobile(b))
                            continue;
                        if (!board.WouldSwapMatch(a, b))
                            continue;
                        int eggs = EggsNear(board, a) + EggsNear(board, b);
                        if (eggs > bestEggs)
                        {
                            bestEggs = eggs;
                            from = a;
                            to = b;
                        }
                    }
                }
            }
            return bestEggs >= 0;
        }

        private static int EggsNear(Board board, GridPosition centre)
        {
            int count = 0;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > 2)
                        continue;
                    var pos = new GridPosition(centre.X + dx, centre.Y + dy);
                    if (board.IsInside(pos) && board[pos] is { } tile && tile.Kind == TileKind.MysteryEgg)
                        count++;
                }
            }
            return count;
        }
    }
}
