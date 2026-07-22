using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Match3.Core;
using Match3.Game;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Match3.Cloud
{
    /// <summary>
    /// Unity Gaming Services glue: anonymous sign-in, cloud save sync, and
    /// leaderboard submission. This whole ASSEMBLY only compiles when the UGS
    /// packages are installed (asmdef defineConstraints), and the game is strictly
    /// LOCAL-FIRST: nothing here ever blocks the menu or gameplay — every call is
    /// fire-and-forget with a silent fallback to local-only on any failure.
    ///
    /// Sync model: pull → ProgressMerger.Merge (per-level max stars, conflict-free)
    /// → replace local if it grew → push if the cloud is behind. Wins push the
    /// fresh profile in the background. Time-attack scores go through the
    /// "submit_score" Cloud Code endpoint (server-side plausibility checks —
    /// ScoreBounds) with a direct leaderboard write as fallback while the script
    /// isn't deployed yet.
    /// </summary>
    public static class CloudSync
    {
        private const string ProgressKey = "progress";
        public const string LeaderboardId = "time-attack-score";

        public static bool SignedIn { get; private set; }

        private static GameManager _hookedGame;
        private static float _runStartedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => HookGameManager();
            HookGameManager();
            _ = InitializeAsync();
        }

        private static void HookGameManager()
        {
            var game = UnityEngine.Object.FindObjectOfType<GameManager>();
            if (game == null || game == _hookedGame)
                return;

            _hookedGame = game; // the old instance died with its scene — its events died too
            game.PhaseChanged += phase =>
            {
                if (phase == GamePhase.Init)
                    _runStartedAt = Time.realtimeSinceStartup;
            };
            game.LevelWon += _ => PushProgress();
            game.GameEnded += () =>
            {
                if (game.Mode == GameMode.TimeAttack)
                    _ = SubmitScoreAsync(game.Score, Time.realtimeSinceStartup - _runStartedAt);
            };
        }

        private static async Task InitializeAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                SignedIn = true;
                CloudBridge.SetStatus($"Cloud: online (Player {ShortId(AuthenticationService.Instance.PlayerId)})");
                await PullMergePushAsync();
            }
            catch (Exception)
            {
                // Project not linked to UGS yet / no network / quota — local-first by design.
                SignedIn = false;
                CloudBridge.SetStatus("Cloud sync: offline");
            }
        }

        private static string ShortId(string playerId) =>
            string.IsNullOrEmpty(playerId) ? "?" : playerId.Substring(0, Math.Min(6, playerId.Length));

        // ---- Progress sync -----------------------------------------------------------

        private static async Task PullMergePushAsync()
        {
            PlayerProgress local = ProgressService.Current;
            PlayerProgress cloud = await LoadCloudProgressAsync();
            PlayerProgress merged = ProgressMerger.Merge(local, cloud);

            if (!ProgressMerger.AreEquivalent(merged, local))
            {
                ProgressService.ReplaceCurrent(merged);
                CloudBridge.RaiseProgressRefreshed(); // the menu rebinds its rows
            }
            if (!ProgressMerger.AreEquivalent(merged, cloud))
                await SaveCloudProgressAsync(merged);
        }

        private static void PushProgress()
        {
            if (!SignedIn)
                return;
            _ = SafePushAsync();

            static async Task SafePushAsync()
            {
                try { await SaveCloudProgressAsync(ProgressService.Current); }
                catch (Exception) { /* next win or next launch retries */ }
            }
        }

        private static async Task<PlayerProgress> LoadCloudProgressAsync()
        {
            try
            {
                var keys = new HashSet<string> { ProgressKey };
                Dictionary<string, Unity.Services.CloudSave.Models.Item> data =
                    await CloudSaveService.Instance.Data.Player.LoadAsync(keys);
                if (data.TryGetValue(ProgressKey, out Unity.Services.CloudSave.Models.Item item))
                    return ProgressSerializer.Deserialize(item.Value.GetAs<string>());
            }
            catch (Exception)
            {
                // fresh player / transient failure — treat as empty cloud
            }
            return new PlayerProgress();
        }

        private static async Task SaveCloudProgressAsync(PlayerProgress progress)
        {
            var data = new Dictionary<string, object> { { ProgressKey, ProgressSerializer.Serialize(progress) } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        }

        // ---- Leaderboard -------------------------------------------------------------

        private static async Task SubmitScoreAsync(int score, float duration)
        {
            if (!SignedIn || score <= 0)
                return;

            try
            {
                // The "middleware" path: Cloud Code validates plausibility server-side
                // (mirrors Match3.Core.ScoreBounds) before writing to the board.
                var args = new Dictionary<string, object> { { "score", score }, { "duration", duration } };
                await CloudCodeService.Instance.CallEndpointAsync<string>("submit_score", args);
            }
            catch (Exception)
            {
                try
                {
                    // Script not deployed yet — direct write keeps the feature alive.
                    await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, score);
                }
                catch (Exception)
                {
                    // offline — the run's score just stays local
                }
            }
        }

        /// <summary>Top N + the player's own entry, for the leaderboard panel.</summary>
        public static async Task<(List<(int rank, string name, double score)> top, (int rank, double score)? me)>
            FetchLeaderboardAsync(int limit)
        {
            var top = new List<(int, string, double)>();
            (int, double)? me = null;

            var page = await LeaderboardsService.Instance.GetScoresAsync(
                LeaderboardId, new Unity.Services.Leaderboards.GetScoresOptions { Limit = limit });
            foreach (Unity.Services.Leaderboards.Models.LeaderboardEntry entry in page.Results)
                top.Add((entry.Rank + 1, CleanName(entry.PlayerName), entry.Score));

            try
            {
                Unity.Services.Leaderboards.Models.LeaderboardEntry own =
                    await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId);
                if (own != null)
                    me = (own.Rank + 1, own.Score);
            }
            catch (Exception)
            {
                // no submitted score yet
            }

            return (top, me);
        }

        private static string CleanName(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return "Player";
            // UGS default names look like "Player#1234" — keep them short and tidy.
            int hash = playerName.IndexOf('#');
            return hash > 0 ? playerName.Substring(0, Math.Min(playerName.Length, hash + 5)) : playerName;
        }
    }
}
