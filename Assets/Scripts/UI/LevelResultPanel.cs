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
        private Image _card;
        private TMP_Text _title;
        private TMP_Text _summary;
        private TMP_Text _scoreCaption;
        private TMP_Text _scoreValue;
        private Image[] _starPips;
        private TMP_Text _buttonLabel;
        private GameObject _menuButton;
        private System.Action _primaryAction;
        private Coroutine _starPop;

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
            Haptics.Heavy();
            MusicManager.Duck();
            // "Next" jumps straight into the following level when the catalog has one;
            // otherwise the campaign is finished and the button replays this level.
            var catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            LevelDefinition next = catalog != null ? catalog.Get(_game.Level + 1) : null;
            if (next != null)
            {
                int nextIndex = _game.Level + 1;
                Show($"Level {_game.Level}\nComplete!", string.Empty, "Next", () =>
                {
                    GameSession.Mode = GameMode.Moves;
                    GameSession.SelectedLevel = next;
                    GameSession.SelectedLevelIndex = nextIndex;
                    _game.Restart();
                });
            }
            else
            {
                Show($"Level {_game.Level}\nComplete!", "All levels cleared!", "Replay", _game.Restart);
            }

            // The win layout (from the Stitch design): FINAL SCORE caption + big gold
            // number instead of the plain summary line.
            _scoreCaption.gameObject.SetActive(true);
            _scoreValue.gameObject.SetActive(true);
            _scoreValue.text = _game.Score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            for (int i = 0; i < _starPips.Length; i++)
            {
                _starPips[i].gameObject.SetActive(true);
                _starPips[i].color = i < stars ? StarEarned : StarMissing;
            }
            if (_starPop != null)
                StopCoroutine(_starPop);
            _starPop = StartCoroutine(PopStars(stars));
        }

        /// <summary>Stitch-design beat: the stars pop in one by one over the card's top edge.</summary>
        private System.Collections.IEnumerator PopStars(int stars)
        {
            foreach (Image pip in _starPips)
                pip.transform.localScale = Vector3.zero;

            for (int i = 0; i < _starPips.Length; i++)
            {
                Transform pip = _starPips[i].transform;
                if (i < stars)
                    AudioManager.Play(Sfx.Pop, 1f + 0.18f * i);

                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
                {
                    // overshoot to 1.25 then settle — a squash-free pop
                    float scale = t < 0.7f ? Mathf.Lerp(0f, 1.25f, t / 0.7f) : Mathf.Lerp(1.25f, 1f, (t - 0.7f) / 0.3f);
                    pip.localScale = Vector3.one * scale;
                    yield return null;
                }
                pip.localScale = Vector3.one;
            }
            _starPop = null;
        }

        private void HandleLevelFailed()
        {
            AudioManager.Play(Sfx.Lose);
            MusicManager.Duck();
            Show("Out of Moves!", $"Score {_game.Score}", "Retry", _game.Restart);
        }

        private void HandleGameEnded()
        {
            AudioManager.Play(Sfx.Lose);
            MusicManager.Duck();
            Show("Time's Up!", $"Reached Level {_game.Level}\nScore {_game.Score}", "Restart", _game.Restart);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Playing || phase == GamePhase.Init)
                Hide();
        }

        private void Show(string title, string summary, string buttonText, System.Action primaryAction)
        {
            _card.color = UiTheme.ThemeCard; // the ambience may have drifted since Build
            _title.text = title;
            _summary.text = summary;
            _buttonLabel.text = buttonText;
            _primaryAction = primaryAction;
            _scoreCaption.gameObject.SetActive(false);
            _scoreValue.gameObject.SetActive(false);
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
            _card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            _title = CreateText("Title", content, new Vector2(0f, 280f), 72f, FontStyles.Bold);
            UiTheme.ApplyFont(_title, UiTheme.TitleFont);

            _summary = CreateText("Summary", content, new Vector2(0f, 10f), 50f, FontStyles.Normal);
            UiTheme.ApplyFont(_summary, UiTheme.BodyFont);
            _summary.color = UiTheme.TextDim;

            // Win-only score block (Stitch design): a spaced caption over a big gold number.
            _scoreCaption = CreateText("ScoreCaption", content, new Vector2(0f, 110f), 32f, FontStyles.Normal);
            UiTheme.ApplyFont(_scoreCaption, UiTheme.BodyFont);
            _scoreCaption.color = UiTheme.TextDim;
            _scoreCaption.characterSpacing = 8f;
            _scoreCaption.text = "FINAL SCORE";

            _scoreValue = CreateText("ScoreValue", content, new Vector2(0f, 15f), 96f, FontStyles.Bold);
            UiTheme.ApplyFont(_scoreValue, UiTheme.TitleFont);
            _scoreValue.color = UiTheme.Gold;

            // The star trio STRADDLES the card's top edge (card is 980 tall, so the
            // edge sits at +490 in card space) — straight from the Stitch mock.
            _starPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject pip = CreateRect($"Star{i}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    i == 1 ? new Vector2(170f, 170f) : new Vector2(140f, 140f));
                pip.GetComponent<RectTransform>().anchoredPosition = new Vector2((i - 1) * 190f, i == 1 ? 525f : 480f);
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

            Transform safe = canvas.transform.Find("SafeArea");
            if (safe == null)
                safe = BuildSafeAreaHost(canvas);

            if (safe.Find("HudTopCard") == null)
                BuildHudCard(safe);
            if (safe.Find(nameof(ObjectiveBarView)) == null)
                ObjectiveBarView.Attach(safe, game);
            // The result panel stays OUTSIDE the safe area on purpose: its dim overlay
            // should bleed under the notch, and its card is centred anyway.
            if (canvas.transform.Find(nameof(LevelResultPanel)) == null)
                LevelResultPanel.Attach(canvas, game);
            if (canvas.transform.Find(nameof(SettingsPanel)) == null)
                SettingsPanel.Attach(canvas, safe, game);
        }

        /// <summary>
        /// A full-stretch container tracking <see cref="Screen.safeArea"/>. The
        /// scene-authored HUD labels are adopted into it once, so notches and gesture
        /// bars never cover them — no scene edit required.
        /// </summary>
        private static Transform BuildSafeAreaHost(Canvas canvas)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<SafeAreaFitter>();

            var adopt = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in canvas.transform)
                if (child != go.transform)
                    adopt.Add(child);
            foreach (Transform child in adopt)
                child.SetParent(go.transform, false); // parent rect is identical, layout is preserved

            return go.transform;
        }

        /// <summary>The design's top-bar card, slid BEHIND the scene-authored HUD labels.</summary>
        private static void BuildHudCard(Transform parent)
        {
            var go = new GameObject("HudTopCard", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling(); // behind every HUD label
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-40f, 430f);
            rect.anchoredPosition = new Vector2(0f, -14f);
            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Round, new Color(UiTheme.Card.r, UiTheme.Card.g, UiTheme.Card.b, 0.88f));
            image.raycastTarget = false;
        }
    }
}
