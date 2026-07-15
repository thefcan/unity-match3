using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The end-of-level overlay for every outcome: moves-mode win (with a 3-diamond
    /// star display), moves-mode fail, and the time-attack game over. It BUILDS its
    /// own UI under the scene's Canvas at runtime — no scene wiring, no prefab —
    /// so it works in any scene that has a Canvas + GameManager.
    ///
    /// Star pips are rotated squares (diamonds) rather than "★" text: the bundled
    /// LiberationSans font has no star glyph, and sprites-as-UI stay font-proof.
    /// </summary>
    public sealed class LevelResultPanel : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color StarEarned = new Color(1f, 0.8f, 0.15f);
        private static readonly Color StarMissing = new Color(0.28f, 0.28f, 0.32f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.52f, 0.9f);

        private GameManager _game;
        private GameObject _root;
        private TMP_Text _title;
        private TMP_Text _summary;
        private Image[] _starPips;
        private TMP_Text _buttonLabel;

        /// <summary>Builds the (hidden) panel under <paramref name="canvas"/> and hooks the game's outcome events.</summary>
        public static LevelResultPanel Attach(Canvas canvas, GameManager game)
        {
            var host = new GameObject(nameof(LevelResultPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = host.GetComponent<RectTransform>();
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            var panel = host.AddComponent<LevelResultPanel>();
            panel._game = game;
            panel.Build();
            panel.Hide();
            return panel;
        }

        private void OnEnable()
        {
            if (_game == null) return;
            _game.LevelWon += HandleLevelWon;
            _game.LevelFailed += HandleLevelFailed;
            _game.GameEnded += HandleGameEnded;
            _game.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (_game == null) return;
            _game.LevelWon -= HandleLevelWon;
            _game.LevelFailed -= HandleLevelFailed;
            _game.GameEnded -= HandleGameEnded;
            _game.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandleLevelWon(int stars)
        {
            Show($"Level {_game.Level} Complete!", $"Score {_game.Score}", "Continue");
            for (int i = 0; i < _starPips.Length; i++)
            {
                _starPips[i].gameObject.SetActive(true);
                _starPips[i].color = i < stars ? StarEarned : StarMissing;
            }
        }

        private void HandleLevelFailed()
        {
            Show("Out of Moves!", $"Score {_game.Score}", "Retry");
        }

        private void HandleGameEnded()
        {
            Show("Time's Up!", $"Reached Level {_game.Level}\nScore {_game.Score}", "Restart");
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Playing || phase == GamePhase.Init)
                Hide();
        }

        private void Show(string title, string summary, string buttonText)
        {
            _title.text = title;
            _summary.text = summary;
            _buttonLabel.text = buttonText;
            foreach (Image pip in _starPips)
                pip.gameObject.SetActive(false);
            _root.SetActive(true);
        }

        private void Hide() => _root.SetActive(false);

        private void OnButtonClicked()
        {
            // "Continue" after a win re-enters the flow (the level map takes over once
            // scenes exist); Retry/Restart rebuild the same setup. Restart covers all.
            _game.Restart();
        }

        // ---- Runtime UI construction --------------------------------------------------

        private void Build()
        {
            _root = CreateRect("Overlay", transform, Vector2.zero, Vector2.one, Vector2.zero);
            _root.AddComponent<Image>().color = PanelColor;

            _title = CreateText("Title", _root.transform, new Vector2(0f, 240f), 64f, FontStyles.Bold);
            _summary = CreateText("Summary", _root.transform, new Vector2(0f, 20f), 42f, FontStyles.Normal);

            _starPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject pip = CreateRect($"Star{i}", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 90f));
                pip.GetComponent<RectTransform>().anchoredPosition = new Vector2((i - 1) * 130f, 140f);
                pip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                _starPips[i] = pip.AddComponent<Image>();
            }

            GameObject buttonGo = CreateRect("ActionButton", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 110f));
            buttonGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -220f);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = ButtonColor;
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(OnButtonClicked);

            _buttonLabel = CreateText("Label", buttonGo.transform, Vector2.zero, 44f, FontStyles.Bold);
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = anchorMin == anchorMax ? size : Vector2.zero;
            return go;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 position, float fontSize, FontStyles style)
        {
            GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900f, 130f));
            go.GetComponent<RectTransform>().anchoredPosition = position;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }
    }

    /// <summary>
    /// Hooks the result panel into every scene that has a GameManager + Canvas —
    /// runs on play and after each scene load, so no scene needs manual wiring.
    /// </summary>
    internal static class RuntimeUiBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnGameStart()
        {
            SceneManager.sceneLoaded += (_, _) => TryAttach();
            TryAttach();
        }

        private static void TryAttach()
        {
            var game = Object.FindObjectOfType<GameManager>();
            var canvas = Object.FindObjectOfType<Canvas>();
            if (game == null || canvas == null)
                return;
            if (canvas.transform.Find(nameof(LevelResultPanel)) != null)
                return;

            LevelResultPanel.Attach(canvas, game);
        }
    }
}
