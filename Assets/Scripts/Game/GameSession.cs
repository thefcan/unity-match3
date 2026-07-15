namespace Match3.Game
{
    public enum GameMode
    {
        /// <summary>The original endless mode: race the clock to rising score targets.</summary>
        TimeAttack,
        /// <summary>Candy-Crush-style: complete the level's objectives within a move limit.</summary>
        Moves,
    }

    /// <summary>
    /// Carries the player's pick (mode + level) across scene loads — plain statics,
    /// the simplest thing that survives a SceneManager.LoadScene. When the Game scene
    /// is played directly in the editor, the defaults kick in: Moves mode with
    /// Resources/Levels/Level_01 (falling back to TimeAttack if no level asset exists).
    /// </summary>
    public static class GameSession
    {
        public static GameMode Mode = GameMode.Moves;

        /// <summary>The level to play in Moves mode; null means "load the default".</summary>
        public static LevelDefinition SelectedLevel;

        /// <summary>1-based index of <see cref="SelectedLevel"/> in the catalog (drives HUD + progress).</summary>
        public static int SelectedLevelIndex = 1;
    }
}
