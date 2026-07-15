using System.IO;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// The game's single access point to saved progress: a cached PlayerProgress
    /// backed by a file in persistentDataPath. Static because progress is inherently
    /// app-global and must survive scene loads; the repository stays swappable for
    /// tooling via <see cref="OverrideRepository"/>.
    /// </summary>
    public static class ProgressService
    {
        private static IProgressRepository _repository;
        private static PlayerProgress _current;

        public static PlayerProgress Current => _current ??= Repository.Load();

        private static IProgressRepository Repository =>
            _repository ??= new FileProgressRepository(Path.Combine(Application.persistentDataPath, "progress.sav"));

        /// <summary>Records a won level (best stars kept) and persists immediately.</summary>
        public static void RecordWin(int levelIndex, int stars)
        {
            Current.RecordResult(levelIndex, stars);
            Repository.Save(Current);
        }

        /// <summary>Swap the backing store (tests / tooling). Resets the cache.</summary>
        public static void OverrideRepository(IProgressRepository repository)
        {
            _repository = repository;
            _current = null;
        }
    }
}
