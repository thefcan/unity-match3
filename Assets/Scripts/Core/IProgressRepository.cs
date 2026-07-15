namespace Match3.Core
{
    /// <summary>
    /// Where progress lives. An interface so the game code (and tests) never care
    /// whether it's a file, memory, or a future cloud save — and so the pure-C# test
    /// assembly can exercise persistence without Unity.
    /// </summary>
    public interface IProgressRepository
    {
        /// <summary>Loads saved progress, or a fresh profile when nothing (valid) is stored.</summary>
        PlayerProgress Load();

        void Save(PlayerProgress progress);
    }
}
