using Match3.Core;
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
        private TMP_Text _eventLine;
        private GameObject _rescueButton;
        private TMP_Text _rescueLabel;
        private Image[] _starPips;
        private Image _actionImage;
        private TMP_Text _buttonLabel;
        private GameObject _menuButton;
        private System.Action _primaryAction;
        private Coroutine _starPop;
        private Coroutine _countUp;
        private UiConfetti _confetti;

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
            // number instead of the plain summary line. The number counts up once
            // the card has settled; confetti rides the same beat.
            _scoreCaption.gameObject.SetActive(true);
            _scoreValue.gameObject.SetActive(true);
            _scoreValue.text = "0";
            if (_countUp != null)
                StopCoroutine(_countUp);
            _countUp = StartCoroutine(WinCelebration(_game.Score));

            // Quiet event-progress toast — the win that just played already counted.
            if (EventService.IsWindowActive)
            {
                EventDef def = EventService.CurrentDef;
                _eventLine.text = def.Kind == EventKind.Race
                    ? $"RACE  {EventService.Progress}/{EventCalendar.RaceTarget}"
                    : $"EVENT  {EventService.Progress}/{def.Tier3}";
            }

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
                if (i < stars)
                    AudioManager.Play(Sfx.Pop, 1f + 0.18f * i);
                yield return UiTween.ScalePop(_starPips[i].transform, 0.22f);
            }
            _starPop = null;
        }

        /// <summary>Win beat two: the card settles (0.2s), confetti flies, the score counts up.</summary>
        private System.Collections.IEnumerator WinCelebration(int score)
        {
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
                yield return null;
            _confetti.Burst(_card.rectTransform);
            yield return UiTween.CountUp(_scoreValue, 0, score, 0.8f);
            _countUp = null;
        }

        private void HandleLevelFailed()
        {
            AudioManager.Play(Sfx.Lose);
            MusicManager.Duck();
            // A bomb loss happens with moves still on the counter — saying "out of
            // moves" there reads as a bug (seen in standalone play-testing).
            Show(_game.LastFailWasBomb ? "The Bomb Went Off!" : "Out of Moves!",
                 $"Score {_game.Score}", "Retry", _game.Restart);

            // The second chance, when the shelf has one and this attempt hasn't
            // used its single continue yet (CanRescue owns both gates).
            if (_game.CanRescue)
            {
                _summary.rectTransform.anchoredPosition = new Vector2(0f, 100f);
                _rescueLabel.text = _game.LastFailWasBomb
                    ? $"DEFUSE  +{GameManager.RescueMoves} MOVES  (×{MetaService.Rescues})"
                    : $"SAVE ME  +{GameManager.RescueMoves} MOVES  (×{MetaService.Rescues})";
                _rescueButton.SetActive(true);

                // Rescue is the star of this card — Retry steps down to the
                // secondary style so two identical pink CTAs never compete.
                // (Show resets the primary look on every outcome.)
                UiTheme.ApplySprite(_actionImage, UiTheme.Pill, UiTheme.Slot);
                _buttonLabel.color = UiTheme.TextDim;
            }
        }

        private void OnRescueClicked()
        {
            AudioManager.Play(Sfx.Button);
            if (_game.TryRescue())
            {
                AudioManager.Play(Sfx.Win);
                Haptics.Medium();
                // The panel hides itself on the PhaseChanged back to Playing/Shuffling.
            }
            else
            {
                _rescueButton.SetActive(false); // shelf emptied under us — withdraw the offer
            }
        }

        private void HandleGameEnded()
        {
            AudioManager.Play(Sfx.Lose);
            MusicManager.Duck();
            Show("Time's Up!", $"Reached Level {_game.Level}\nScore {_game.Score}", "Restart", _game.Restart);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            // Shuffling is in the list for the rescue path: a rescued DEAD board
            // goes through ShuffleState, and the fail card must not sit over it.
            if (phase == GamePhase.Playing || phase == GamePhase.Init || phase == GamePhase.Shuffling)
                Hide();
        }

        private void Show(string title, string summary, string buttonText, System.Action primaryAction)
        {
            _card.color = UiTheme.ThemeCard; // the ambience may have drifted since Build
            // The fail path may have demoted the primary button — restore it.
            UiTheme.ApplySprite(_actionImage, UiTheme.PillPink, Color.white);
            if (_actionImage.sprite == null)
                _actionImage.color = ButtonColor;
            _buttonLabel.color = Color.white;
            _title.text = title;
            _summary.text = summary;
            _buttonLabel.text = buttonText;
            _primaryAction = primaryAction;
            _scoreCaption.gameObject.SetActive(false);
            _scoreValue.gameObject.SetActive(false);
            _eventLine.text = string.Empty;
            // The rescue layout moves the summary up; every Show starts from the default.
            _summary.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            _rescueButton.SetActive(false);
            foreach (Image pip in _starPips)
                pip.gameObject.SetActive(false);
            // The menu button only makes sense when the MainMenu scene is loadable
            // (i.e. registered in the build scene list).
            _menuButton.SetActive(Application.CanStreamedLevelBeLoaded("MainMenu"));
            UiTween.OpenPanel(this, _root, _card.transform); // fail cards get the fade+pop too
        }

        private void Hide()
        {
            if (_countUp != null)
            {
                StopCoroutine(_countUp);
                _countUp = null;
            }
            UiTween.ClosePanel(this, _root);
        }

        private void OnButtonClicked()
        {
            AudioManager.Play(Sfx.Button);
            _primaryAction?.Invoke();
        }

        private static void OnMenuClicked()
        {
            AudioManager.Play(Sfx.Button);
            ScreenFader.LoadScene("MainMenu");
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

            // Win-only Candy Calendar beat, tucked between the score and the button.
            _eventLine = CreateText("EventLine", content, new Vector2(0f, -68f), 28f, FontStyles.Bold);
            _eventLine.rectTransform.sizeDelta = new Vector2(780f, 40f);
            UiTheme.ApplyFont(_eventLine, UiTheme.BodyFont);
            _eventLine.color = UiTheme.Gold;
            _eventLine.characterSpacing = 4f;

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
            _actionImage = buttonImage;
            var button = buttonGo.AddComponent<Button>();
            buttonGo.AddComponent<PressableButton>();
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
            _menuButton.AddComponent<PressableButton>();
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

            // The rescue offer — built LAST: its rect overlaps the (raycast-inert)
            // summary/event texts, and sibling order is the raycast defense.
            _summary.raycastTarget = false;
            _eventLine.raycastTarget = false;
            _rescueButton = CreateRect("RescueButton", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 130f));
            _rescueButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -10f);
            var rescueImage = _rescueButton.AddComponent<Image>();
            UiTheme.ApplySprite(rescueImage, UiTheme.PillPink, Color.white);
            if (rescueImage.sprite == null)
                rescueImage.color = ButtonColor;
            var rescueButton = _rescueButton.AddComponent<Button>();
            _rescueButton.AddComponent<PressableButton>();
            rescueButton.targetGraphic = rescueImage;
            rescueButton.onClick.AddListener(OnRescueClicked);
            Image rescueOutline = new GameObject("Outline", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            rescueOutline.transform.SetParent(_rescueButton.transform, false);
            UiTheme.ApplySprite(rescueOutline, UiTheme.PillOutline, UiTheme.Gold);
            Stretch(rescueOutline.rectTransform);
            rescueOutline.raycastTarget = false;
            _rescueLabel = CreateText("Label", _rescueButton.transform, Vector2.zero, 38f, FontStyles.Bold);
            UiTheme.ApplyFont(_rescueLabel, UiTheme.ButtonFont);
            Stretch(_rescueLabel.rectTransform);
            _rescueButton.SetActive(false);

            // Confetti last: it renders over everything on the card, and living
            // inside the overlay means Hide() kills a burst mid-flight.
            _confetti = UiConfetti.Attach(_root.transform);
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
            // The ROOT canvas, explicitly: HudView adds NESTED canvases to the score
            // and clock labels (mesh-rebuild isolation), and FindObjectOfType<Canvas>
            // can return one of those — every panel then builds inside a tiny label
            // rect (result card squeezed into the top bar, no dim over the board).
            // It must also belong to the ACTIVE scene: ScreenFader's persistent
            // (DontDestroyOnLoad) canvas is a root canvas too, and building the HUD
            // there hides it forever — the fader's CanvasGroup sits at alpha 0
            // between transitions.
            Canvas canvas = null;
            foreach (Canvas candidate in Object.FindObjectsOfType<Canvas>())
            {
                if (candidate.isRootCanvas &&
                    candidate.gameObject.scene == SceneManager.GetActiveScene())
                {
                    canvas = candidate;
                    break;
                }
            }
            if (game == null || canvas == null)
                return;

            // The scene-authored canvas honours the accessibility text scale too.
            UiTheme.ApplyUiScale(canvas.GetComponent<UnityEngine.UI.CanvasScaler>());

            Transform safe = canvas.transform.Find("SafeArea");
            if (safe == null)
                safe = BuildSafeAreaHost(canvas);

            Transform topBar = safe.Find("HudTopCard");
            if (topBar == null)
                topBar = BuildTopBar(safe, game);
            if (safe.Find(nameof(ObjectiveBarView)) == null)
                ObjectiveBarView.Attach(safe, game);
            // The result panel stays OUTSIDE the safe area on purpose: its dim overlay
            // should bleed under the notch, and its card is centred anyway.
            if (canvas.transform.Find(nameof(LevelResultPanel)) == null)
                LevelResultPanel.Attach(canvas, game);
            // buttonHost = the top bar: the pause opener docks inside its right edge.
            if (canvas.transform.Find(nameof(SettingsPanel)) == null)
                SettingsPanel.Attach(canvas, topBar, game);
            if (safe.Find(nameof(BoosterTray)) == null)
                BoosterTray.Attach(safe, game);
            // The act-opener teaching veil (chapter 3+ levels with tutorialText).
            if (canvas.transform.Find(nameof(TutorialOverlay)) == null)
                TutorialOverlay.Attach(canvas, game);
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

        /// <summary>
        /// The Stitch top bar: one rounded card holding the moves/time stat (left),
        /// level caption + score (centre), the pause opener (right — added by
        /// SettingsPanel) and a gold score progress bar along its bottom edge.
        /// The scene-authored HUD labels are adopted INTO the card and re-laid
        /// from code, so the scene file itself never changes.
        /// </summary>
        private static Transform BuildTopBar(Transform safe, GameManager game)
        {
            var go = new GameObject("HudTopCard", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(safe, false);
            go.transform.SetAsFirstSibling(); // behind the banner text and overlays
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-32f, 250f);
            rect.anchoredPosition = new Vector2(0f, -16f);
            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Round, new Color(UiTheme.Card.r, UiTheme.Card.g, UiTheme.Card.b, 0.92f));
            image.raycastTarget = false;

            // Tiny gold caps caption over the left stat block. The MODE isn't known
            // until BuildNewGame runs (after this bootstrap), so the caption re-reads
            // it on LevelChanged instead of freezing the boot-time default.
            TMP_Text modeCaption = CreateBarCaption(go.transform, "MOVES");
            var captionLabel = modeCaption.gameObject.AddComponent<ModeCaptionLabel>();
            captionLabel.Bind(game, modeCaption);

            // Left: the clock/moves value. Centre: level caption over the big score.
            AdoptLabel(safe, "TimeText", go.transform, new Vector2(0f, 1f), new Vector2(130f, -122f), new Vector2(240f, 96f), 60f);
            AdoptLabel(safe, "ScoreText", go.transform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(480f, 96f), 72f);
            TMP_Text level = AdoptLabel(safe, "LevelText", go.transform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(480f, 40f), 26f);
            if (level != null)
            {
                level.color = UiTheme.TextDim;
                level.characterSpacing = 6f;
            }

            // The old target line's info lives in the progress caption now.
            Transform target = safe.Find("TargetText");
            if (target != null)
                target.gameObject.SetActive(false);

            ScoreProgressBar.Attach(go.transform, game);

            // Win-streak badge (Butler's Gift): bottom-left, mirroring the caption.
            var streakGo = new GameObject("WinStreak", typeof(RectTransform));
            streakGo.transform.SetParent(go.transform, false);
            var streakRect = (RectTransform)streakGo.transform;
            streakRect.anchorMin = streakRect.anchorMax = new Vector2(0f, 0f);
            streakRect.pivot = new Vector2(0f, 0f);
            streakRect.sizeDelta = new Vector2(360f, 34f);
            streakRect.anchoredPosition = new Vector2(34f, 54f);
            var streakText = streakGo.AddComponent<TextMeshProUGUI>();
            streakText.fontSize = 26f;
            streakText.fontStyle = FontStyles.Bold;
            streakText.alignment = TextAlignmentOptions.MidlineLeft;
            streakText.color = UiTheme.Gold;
            streakText.characterSpacing = 3f;
            streakText.raycastTarget = false;
            UiTheme.ApplyFont(streakText, UiTheme.BodyFont);
            streakGo.AddComponent<WinStreakLabel>().Bind(game, streakText);

            return go.transform;
        }

        /// <summary>Keeps the stat caption honest once the game mode is actually known.</summary>
        internal sealed class ModeCaptionLabel : MonoBehaviour
        {
            private GameManager _game;
            private TMP_Text _text;

            public void Bind(GameManager game, TMP_Text text)
            {
                _game = game;
                _text = text;
                _game.LevelChanged += HandleLevelChanged;
            }

            private void OnDestroy()
            {
                if (_game != null)
                    _game.LevelChanged -= HandleLevelChanged;
            }

            private void HandleLevelChanged(int level)
            {
                if (_text != null)
                    _text.text = _game.Mode == GameMode.TimeAttack ? "TIME" : "MOVES";
            }
        }

        private static TMP_Text CreateBarCaption(Transform bar, string caption)
        {
            var go = new GameObject("ModeCaption", typeof(RectTransform));
            go.transform.SetParent(bar, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(130f, -46f);
            rect.sizeDelta = new Vector2(240f, 40f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = caption;
            text.fontSize = 26f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = UiTheme.Gold;
            text.characterSpacing = 6f;
            text.raycastTarget = false;
            UiTheme.ApplyFont(text, UiTheme.BodyFont);
            return text;
        }

        /// <summary>Tiny gold "WIN ×N" indicator driven by the win streak (empty at zero).</summary>
        internal sealed class WinStreakLabel : MonoBehaviour
        {
            private GameManager _game;
            private TMP_Text _text;

            public void Bind(GameManager game, TMP_Text text)
            {
                _game = game;
                _text = text;
                _game.LevelChanged += HandleLevelChanged;
                Refresh();
            }

            private void OnDestroy()
            {
                if (_game != null)
                    _game.LevelChanged -= HandleLevelChanged;
            }

            private void HandleLevelChanged(int level) => Refresh();

            private void Refresh()
            {
                if (_text == null)
                    return;
                int streak = MetaService.WinStreak;
                _text.text = streak > 0 ? $"WIN ×{streak}" : string.Empty;
            }
        }

        /// <summary>Reparents a scene-authored HUD label into the bar and re-styles it in place.</summary>
        private static TMP_Text AdoptLabel(Transform safe, string name, Transform bar,
                                           Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            Transform label = safe.Find(name);
            if (label == null)
                return null; // a scene without this label — tolerate

            label.SetParent(bar, false);
            var rect = (RectTransform)label;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            if (!label.TryGetComponent(out TMP_Text text))
                return null;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            return text;
        }
    }
}
