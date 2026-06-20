using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// Win/lose overlay. Lives on an always-active parent (the Canvas) and toggles
    /// a child panel — a disabled GameObject can't receive events, so the listener
    /// must sit on an object that stays enabled.
    /// Another observer: it learns the game ended from the GameEnded event, and its
    /// only call back into the game is the explicit Restart button.
    /// </summary>
    public sealed class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private Button restartButton;

        private void OnEnable()
        {
            gameManager.GameEnded += HandleGameEnded;
            restartButton.onClick.AddListener(HandleRestartClicked);
        }

        private void OnDisable()
        {
            gameManager.GameEnded -= HandleGameEnded;
            restartButton.onClick.RemoveListener(HandleRestartClicked);
        }

        private void Start()
        {
            panelRoot.SetActive(false);
        }

        private void HandleGameEnded()
        {
            // Endless time-attack: the run always ends because the clock ran out.
            // How far you got IS the score, so we lead with the level reached.
            titleText.text = "Time's Up!";
            summaryText.text = $"Reached Level {gameManager.Level}\nScore {gameManager.Score}";
            panelRoot.SetActive(true);
        }

        private void HandleRestartClicked()
        {
            panelRoot.SetActive(false);
            gameManager.Restart();
        }
    }
}
