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
        private static readonly Color StarEarned = UiTheme.Gold;
        private static readonly Color StarMissing = UiTheme.StarDim;
        private static readonly Color ButtonColor = UiTheme.Cta;

        private GameManager _game;
        private GameObject _root;
        private TMP_Text _title;
        private TMP_Text _summary;
        private Image[] _starPips;
        private TMP_Text _buttonLabel;
        private GameObject _menuButton;
        private System.Action _primaryAction;

        /// <summary>Builds the (hidden) panel under <paramref name="canvas"/> and hooks the game's outcome events.</summary>
        public static LevelResultPanel Attach(Canvas canvas, GameManager game)
        {
            var host = new GameObject(nameof(LevelResultPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = host.GetComponent<RectTransform>();
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            // AddComponent on an ACTIVE object runs OnEnable immediately — before _game
            // is wired, which would silently skip every event subscription. Deactivate
            // around construction so OnEnable fires exactly once, fully wired.
            host.SetActive(false);
            var panel = host.AddComponent<LevelResultPanel>();
            panel._game = game;
            panel.Build();
            panel.Hide();
            host.SetActive(true);
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
            AudioManager.Play(Sfx.Win);
            // "Next" jumps straight into the following level when the catalog has one;
            // otherwise the campaign is finished and the button replays this level.
            var catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            LevelDefinition next = catalog != null ? catalog.Get(_game.Level + 1) : null;
            if (next != null)
            {
                int nextIndex = _game.Level + 1;
                Show($"Level {_game.Level} Complete!", $"Score {_game.Score}", "Next", () =>
                {
                    GameSession.Mode = GameMode.Moves;
                    GameSession.SelectedLevel = next;
                    GameSession.SelectedLevelIndex = nextIndex;
                    _game.Restart();
                });
            }
            else
            {
                Show($"Level {_game.Level} Complete!", $"Score {_game.Score}\nAll levels cleared!", "Replay", _game.Restart);
            }

            for (int i = 0; i < _starPips.Length; i++)
            {
                _starPips[i].gameObject.SetActive(true);
                _starPips[i].color = i < stars ? StarEarned : StarMissing;
            }
        }

        private void HandleLevelFailed()
        {
            AudioManager.Play(Sfx.Lose);
            Show("Out of Moves!", $"Score {_game.Score}", "Retry", _game.Restart);
        }

        private void HandleGameEnded()
        {
            AudioManager.Play(Sfx.Lose);
            Show("Time's Up!", $"Reached Level {_game.Level}\nScore {_game.Score}", "Restart", _game.Restart);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Playing || phase == GamePhase.Init)
                Hide();
        }

        private void Show(string title, string summary, string buttonText, System.Action primaryAction)
        {
            _title.text = title;
            _summary.text = summary;
            _buttonLabel.text = buttonText;
            _primaryAction = primaryAction;
            foreach (Image pip in _starPips)
                pip.gameObject.SetActive(false);
            // The menu button only makes sense when the MainMenu scene is loadable
            // (i.e. registered in the build scene list).
            _menuButton.SetActive(Application.CanStreamedLevelBeLoaded("MainMenu"));
            _root.SetActive(true);
        }

        private void Hide() => _root.SetActive(false);

        private void OnButtonClicked()
        {
            AudioManager.Play(Sfx.Button);
            _primaryAction?.Invoke();
        }

        private static void OnMenuClicked()
        {
            AudioManager.Play(Sfx.Button);
            SceneManager.LoadScene("MainMenu");
        }

        // ---- Runtime UI construction --------------------------------------------------
        // Implements the Figma design language (UiTheme): rounded card on a dim
        // overlay, Baloo 2 headings, star sprites, pink gradient CTA pill.

        private void Build()
        {
            _root = CreateRect("Overlay", transform, Vector2.zero, Vector2.one, Vector2.zero);
            _root.AddComponent<Image>().color = PanelColor;

            GameObject cardGo = CreateRect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(860f, 980f));
            var card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(card, UiTheme.Round, UiTheme.Card);
            Transform content = cardGo.transform;

            _title = CreateText("Title", content, new Vector2(0f, 330f), 76f, FontStyles.Bold);
            UiTheme.ApplyFont(_title, UiTheme.TitleFont);

            _summary = CreateText("Summary", content, new Vector2(0f, 10f), 50f, FontStyles.Normal);
            UiTheme.ApplyFont(_summary, UiTheme.BodyFont);
            _summary.color = UiTheme.TextDim;

            _starPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject pip = CreateRect($"Star{i}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, 150f));
                // the middle star sits slightly higher, the classic result-screen arc
                pip.GetComponent<RectTransform>().anchoredPosition = new Vector2((i - 1) * 180f, i == 1 ? 185f : 160f);
                _starPips[i] = pip.AddComponent<Image>();
                UiTheme.ApplySprite(_starPips[i], UiTheme.StarSprite, StarMissing);
                if (_starPips[i].sprite == null)
                    pip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            GameObject buttonGo = CreateRect("ActionButton", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 140f));
            buttonGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -160f);
            var buttonImage = buttonGo.AddComponent<Image>();
            UiTheme.ApplySprite(buttonImage, UiTheme.PillPink, Color.white);
            if (buttonImage.sprite == null)
                buttonImage.color = ButtonColor;
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(OnButtonClicked);

            _buttonLabel = CreateText("Label", buttonGo.transform, Vector2.zero, 52f, FontStyles.Bold);
            UiTheme.ApplyFont(_buttonLabel, UiTheme.ButtonFont);
            Stretch(_buttonLabel.rectTransform);

            _menuButton = CreateRect("MenuButton", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 120f));
            _menuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -330f);
            var menuImage = _menuButton.AddComponent<Image>();
            UiTheme.ApplySprite(menuImage, UiTheme.Pill, UiTheme.Slot);
            var menuButton = _menuButton.AddComponent<Button>();
            menuButton.targetGraphic = menuImage;
            menuButton.onClick.AddListener(OnMenuClicked);
            Image menuOutline = new GameObject("Outline", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            menuOutline.transform.SetParent(_menuButton.transform, false);
            UiTheme.ApplySprite(menuOutline, UiTheme.PillOutline, UiTheme.OutlineDim);
            Stretch(menuOutline.rectTransform);
            menuOutline.raycastTarget = false;
            TMP_Text menuLabel = CreateText("Label", _menuButton.transform, Vector2.zero, 44f, FontStyles.Normal);
            UiTheme.ApplyFont(menuLabel, UiTheme.ButtonFont);
            menuLabel.color = UiTheme.TextDim;
            menuLabel.text = "Level Map";
            Stretch(menuLabel.rectTransform);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
