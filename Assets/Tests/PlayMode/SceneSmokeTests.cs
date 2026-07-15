using System.Collections;
using Match3.Core;
using Match3.Game;
using Match3.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Match3.Tests
{
    /// <summary>
    /// End-to-end boot checks: the real scenes load, the game builds a playable
    /// board in the right mode, and every piece of runtime-built UI attaches itself.
    /// These catch the class of bug unit tests can't — broken scene references,
    /// missing Resources assets, and lifecycle ordering (e.g. a panel subscribing
    /// before its GameManager is wired).
    ///
    /// Run via Test Runner -> PlayMode. Both scenes must be in the build list
    /// (Match3 > Setup > Add Scenes To Build).
    /// </summary>
    public sealed class SceneSmokeTests
    {
        private const int BootFrameBudget = 120;

        [UnityTest]
        public IEnumerator GameScene_BootsIntoMovesMode_WithAFullBoard()
        {
            GameSession.Mode = GameMode.Moves;
            GameSession.SelectedLevel = null; // force the Resources/Levels/Level_01 fallback
            GameSession.SelectedLevelIndex = 1;

            SceneManager.LoadScene("Game");
            yield return null;

            GameManager game = Object.FindObjectOfType<GameManager>();
            Assert.That(game, Is.Not.Null, "the Game scene must contain a GameManager");

            for (int frame = 0; frame < BootFrameBudget && game.Board == null; frame++)
                yield return null;

            Assert.That(game.Board, Is.Not.Null, "InitState should have built a board");
            Assert.That(game.Mode, Is.EqualTo(GameMode.Moves), "with Level_01 present the default is moves mode");
            Assert.That(game.LevelDefinition, Is.Not.Null);
            Assert.That(game.MovesLeft, Is.EqualTo(game.LevelDefinition.movesLimit));
            Assert.That(game.Objectives, Is.Not.Null);
            Assert.That(game.Objectives.AllComplete, Is.False, "a fresh level must have open objectives");

            for (int x = 0; x < game.Board.Width; x++)
                for (int y = 0; y < game.Board.Height; y++)
                    Assert.That(game.Board[new GridPosition(x, y)].HasValue, $"cell ({x},{y}) should be filled");

            Assert.That(game.Board.FindMatches(), Is.Empty, "the starting board must be match-free");

            // Runtime-built pieces attached themselves.
            Assert.That(Object.FindObjectOfType<LevelResultPanel>(), Is.Not.Null,
                "the result panel bootstraps into any scene with a Canvas + GameManager");
            Assert.That(Resources.Load<CandySpriteLibrary>("CandySpriteLibrary"), Is.Not.Null,
                "candy sprites must ship in Resources");
        }

        [UnityTest]
        public IEnumerator GameScene_BootsTimeAttack_WhenSessionSaysSo()
        {
            GameSession.Mode = GameMode.TimeAttack;
            GameSession.SelectedLevel = null;

            SceneManager.LoadScene("Game");
            yield return null;

            GameManager game = Object.FindObjectOfType<GameManager>();
            for (int frame = 0; frame < BootFrameBudget && game.Board == null; frame++)
                yield return null;

            Assert.That(game.Mode, Is.EqualTo(GameMode.TimeAttack));
            Assert.That(game.TimeLeft, Is.GreaterThan(0f), "the clock starts full");
            Assert.That(game.CurrentTarget, Is.GreaterThan(0), "time attack races a score target");

            // Leave the session in its default state for whoever runs next.
            GameSession.Mode = GameMode.Moves;
        }

        [UnityTest]
        public IEnumerator MainMenu_BuildsTheLevelMap()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null; // MainMenuView builds its UI in Start

            Assert.That(Object.FindObjectOfType<MainMenuView>(), Is.Not.Null);
            Assert.That(GameObject.Find("MenuCanvas"), Is.Not.Null, "the menu builds its own canvas");
            Assert.That(Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>(), Is.Not.Null,
                "buttons need an EventSystem");

            var catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Count, Is.EqualTo(LevelCurve.LevelCount));

            Transform content = GameObject.Find("MenuCanvas").transform
                .Find("LevelScroll/Viewport/Content");
            Assert.That(content, Is.Not.Null, "the scrollable map exists");
            Assert.That(content.childCount, Is.EqualTo(catalog.Count), "one row per catalog level");
        }
    }
}
