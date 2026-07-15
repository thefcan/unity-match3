using System;
using System.IO;

namespace Match3.Core
{
    /// <summary>
    /// Progress on disk. The path is injected (Unity passes persistentDataPath; tests
    /// pass a temp file) — System.IO is fine in the engine-free core, Unity APIs are
    /// not. IO failures never crash the game: loads fall back to a fresh profile,
    /// saves fail silently (losing one save beats losing the session).
    /// </summary>
    public sealed class FileProgressRepository : IProgressRepository
    {
        private readonly string _path;

        public FileProgressRepository(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Need a file path.", nameof(path));
            _path = path;
        }

        public PlayerProgress Load()
        {
            try
            {
                return File.Exists(_path)
                    ? ProgressSerializer.Deserialize(File.ReadAllText(_path))
                    : new PlayerProgress();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return new PlayerProgress();
            }
        }

        public void Save(PlayerProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(_path, ProgressSerializer.Serialize(progress));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Swallow: a failed save must never take the game down.
            }
        }
    }
}
