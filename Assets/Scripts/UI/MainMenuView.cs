using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The main menu + scrollable level map, built entirely at runtime (the MainMenu
    /// scene holds only a camera and this component). Implements the Figma design
    /// ("Candy Match — Game UI"): gradient background, Baloo 2 display type, rounded
    /// level cards with candy chips + star pips, a pink gradient Continue CTA and a
    /// quiet Time Attack secondary button. Levels come from Resources/LevelCatalog,
    /// stars and locks from <see cref="ProgressService"/>.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        private const float RowHeight = 150f;
        private const float RowSpacing = 24f;
        private const float RowPitch = RowHeight + RowSpacing;
        private const float ListPadding = 8f;
        // Rows are VIRTUALIZED: a fixed pool rebinds while scrolling, so an 80-level
        // catalog costs ~12 rows of UI objects instead of building every row eagerly.
        private const int RowPoolSize = 12;

        private LevelCatalog _catalog;
        private RectTransform _listContent;
        private readonly System.Collections.Generic.List<LevelRow> _rows = new System.Collections.Generic.List<LevelRow>();
        private int _firstRowIndex = -1;

        private void Start()
        {
            // The menu wears the theme of wherever the player currently is in the campaign.
            UiTheme.SetThemeForLevel(ProgressService.Current.HighestUnlocked);
            if (Camera.main != null)
                Camera.main.backgroundColor = UiTheme.ThemeBgBottom;

            EnsureEventSystem();
            Canvas canvas = BuildCanvas();
            BuildMenu(canvas.transform);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Canvas BuildCanvas()
        {
            var go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private void BuildMenu(Transform canvas)
        {
            // The gradient sprite is NEUTRAL (white->gray) — the theme tint gives it
            // its hue, so the same sprite serves every chapter's ambience. It stays on
            // the canvas root so it bleeds under notches; everything else moves into
            // the safe-area host below.
            Image background = NewImage("Background", canvas, UiTheme.ThemeBgTop);
            UiTheme.ApplySprite(background, UiTheme.BgGradient, UiTheme.ThemeBgTop);
            if (background.sprite == null)
                background.color = UiTheme.ThemeBgBottom;
            Stretch(background.rectTransform, Vector2.zero, Vector2.one);
            background.raycastTarget = false;

            Transform content = BuildSafeAreaHost(canvas);

            TMP_Text title = NewText("Title", content, "Candy Match", 112f, FontStyles.Bold, UiTheme.TitleFont);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(980f, 150f));

            // the five candy dots under the logo
            var dots = NewRect("CandyDots", content);
            Anchor(dots, new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(300f, 36f));
            for (int i = 0; i < UiTheme.CandyColors.Length; i++)
            {
                Image dot = NewImage($"Dot{i}", dots.transform, UiTheme.CandyColors[i]);
                UiTheme.ApplySprite(dot, UiTheme.CircleSprite, UiTheme.CandyColors[i]);
                var dotRect = dot.rectTransform;
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = new Vector2(30f, 30f);
                dotRect.anchoredPosition = new Vector2((i - 2) * 56f, 0f);
                dot.raycastTarget = false;
            }

            var catalogForCount = Resources.Load<LevelCatalog>("LevelCatalog");
            int levelCount = catalogForCount != null && catalogForCount.Count > 0 ? catalogForCount.Count : 60;
            TMP_Text subtitle = NewText("Subtitle", content, $"A sweet {levelCount}-level campaign", 38f, FontStyles.Normal, UiTheme.BodyFont);
            subtitle.color = UiTheme.TextDim;
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(900f, 60f));

            TMP_Text mapLabel = NewText("MapLabel", content, "L E V E L   M A P", 30f, FontStyles.Bold, UiTheme.BodyFont);
            mapLabel.color = UiTheme.TextDim;
            Anchor(mapLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(900f, 44f));

            BuildLevelList(content);
            BuildButtons(content);
        }

        private static Transform BuildSafeAreaHost(Transform canvas)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<SafeAreaFitter>();
            return go.transform;
        }

        private void BuildLevelList(Transform canvas)
        {
            var scrollGo = new GameObject("LevelScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(canvas, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = new Vector2(0.05f, 0.2f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.78f);
            scrollRect.sizeDelta = Vector2.zero;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport, Vector2.zero, Vector2.one);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            // Plain RectTransform content — no layout group / size fitter: rows are
            // positioned by hand from their catalog index, which is what makes the
            // pooled virtualization possible (and kills per-frame layout rebuilds).
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            _catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            if (_catalog == null || _catalog.Count == 0)
            {
                TMP_Text missing = NewText("NoLevels", content, "No levels found.\nRun Match3 > Generate > Level Definitions.", 36f, FontStyles.Normal, UiTheme.BodyFont);
                missing.rectTransform.sizeDelta = new Vector2(0f, 160f);
                return;
            }

            _listContent = content;
            content.sizeDelta = new Vector2(0f, ListPadding * 2f + _catalog.Count * RowHeight + (_catalog.Count - 1) * RowSpacing);

            int poolSize = Mathf.Min(RowPoolSize, _catalog.Count);
            for (int i = 0; i < poolSize; i++)
                _rows.Add(new LevelRow(content, StartLevel));

            scroll.onValueChanged.AddListener(_ => RefreshVisibleRows());
            RefreshVisibleRows();
        }

        /// <summary>Rebinds the row pool to the catalog window under the viewport. No-op until the window moves.</summary>
        private void RefreshVisibleRows()
        {
            if (_listContent == null || _rows.Count == 0)
                return;

            float scrolled = Mathf.Max(0f, _listContent.anchoredPosition.y);
            int first = Mathf.Clamp(Mathf.FloorToInt((scrolled - ListPadding) / RowPitch),
                                    0, Mathf.Max(0, _catalog.Count - _rows.Count));
            if (first == _firstRowIndex)
                return;
            _firstRowIndex = first;

            for (int i = 0; i < _rows.Count; i++)
            {
                int number = first + i + 1;
                if (number <= _catalog.Count)
                    _rows[i].Bind(number, _catalog.Get(number), ListPadding + (number - 1) * RowPitch);
                else
                    _rows[i].Hide();
            }
        }

        /// <summary>
        /// One pooled level-map row. Every child element exists from construction and
        /// <see cref="Bind"/> only restyles/toggles them, so scrolling re-uses objects
        /// instead of instantiating — the virtualization's other half.
        /// </summary>
        private sealed class LevelRow
        {
            private static readonly Color BadgeDone = new Color(0.16f, 0.55f, 0.35f);

            private readonly GameObject _go;
            private readonly RectTransform _rect;
            private readonly CanvasGroup _group;
            private readonly Button _button;
            private readonly Image _outline;
            private readonly Image _chip;
            private readonly TMP_Text _number;
            private readonly TMP_Text _label;
            private readonly Image[] _pips = new Image[3];
            private readonly Image _badge;
            private readonly TMP_Text _badgeLabel;
            private readonly Image _lock;

            private LevelDefinition _definition;
            private int _levelNumber;

            public LevelRow(Transform content, System.Action<LevelDefinition, int> onClick)
            {
                _go = new GameObject("LevelRow", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
                _go.transform.SetParent(content, false);
                _rect = (RectTransform)_go.transform;
                _rect.anchorMin = new Vector2(0f, 1f);
                _rect.anchorMax = new Vector2(1f, 1f);
                _rect.pivot = new Vector2(0.5f, 1f);
                _rect.sizeDelta = new Vector2(-2f * ListPadding, RowHeight);
                _group = _go.GetComponent<CanvasGroup>();

                var card = _go.GetComponent<Image>();
                UiTheme.ApplySprite(card, UiTheme.Round, UiTheme.ThemeCard);

                _button = _go.GetComponent<Button>();
                _button.targetGraphic = card;
                _button.onClick.AddListener(() => onClick(_definition, _levelNumber));

                _outline = NewImage("Outline", _go.transform, UiTheme.Cta);
                UiTheme.ApplySprite(_outline, UiTheme.RoundOutline, UiTheme.Cta);
                Stretch(_outline.rectTransform, Vector2.zero, Vector2.one);
                _outline.raycastTarget = false;

                // candy chip with the level number — the chip colour cycles the candy palette
                _chip = NewImage("Chip", _go.transform, Color.white);
                UiTheme.ApplySprite(_chip, UiTheme.CircleSprite, Color.white);
                var chipRect = _chip.rectTransform;
                chipRect.anchorMin = chipRect.anchorMax = new Vector2(0f, 0.5f);
                chipRect.sizeDelta = new Vector2(96f, 96f);
                chipRect.anchoredPosition = new Vector2(76f, 0f);
                _chip.raycastTarget = false;

                _number = NewText("Number", _chip.transform, string.Empty, 44f, FontStyles.Bold, UiTheme.ButtonFont);
                Stretch(_number.rectTransform, Vector2.zero, Vector2.one);

                _label = NewText("Label", _go.transform, string.Empty, 44f, FontStyles.Bold, UiTheme.BodyFont);
                _label.alignment = TextAlignmentOptions.MidlineLeft;
                var labelRect = _label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0.6f, 1f);
                labelRect.offsetMin = new Vector2(150f, 0f);
                labelRect.offsetMax = Vector2.zero;

                for (int i = 0; i < 3; i++)
                {
                    Image pip = NewImage($"Star{i}", _go.transform, UiTheme.StarDim);
                    UiTheme.ApplySprite(pip, UiTheme.StarSprite, UiTheme.StarDim);
                    var pipRect = pip.rectTransform;
                    pipRect.anchorMin = pipRect.anchorMax = new Vector2(1f, 0.5f);
                    pipRect.sizeDelta = new Vector2(56f, 56f);
                    pipRect.anchoredPosition = new Vector2(-52f - (2 - i) * 64f, 0f);
                    if (pip.sprite == null) // fallback: rotated square reads as a diamond pip
                        pip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    pip.raycastTarget = false;
                    _pips[i] = pip;
                }

                // Status badge (Stitch design): green DONE on completed rows, pink PLAY
                // on the current one — a quick read of where you are in the campaign.
                _badge = NewImage("Badge", _go.transform, UiTheme.Cta);
                UiTheme.ApplySprite(_badge, UiTheme.Pill, UiTheme.Cta);
                var badgeRect = _badge.rectTransform;
                badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 0.5f);
                badgeRect.sizeDelta = new Vector2(140f, 54f);
                badgeRect.anchoredPosition = new Vector2(-320f, 0f);
                _badge.raycastTarget = false;

                _badgeLabel = NewText("Label", _badge.transform, string.Empty, 26f, FontStyles.Bold, UiTheme.BodyFont);
                _badgeLabel.characterSpacing = 3f;
                Stretch(_badgeLabel.rectTransform, Vector2.zero, Vector2.one);

                _lock = NewImage("Lock", _go.transform, UiTheme.TextDim);
                UiTheme.ApplySprite(_lock, UiTheme.LockSprite, UiTheme.TextDim);
                var lockRect = _lock.rectTransform;
                lockRect.anchorMin = lockRect.anchorMax = new Vector2(1f, 0.5f);
                lockRect.sizeDelta = new Vector2(52f, 52f);
                lockRect.anchoredPosition = new Vector2(-70f, 0f);
                _lock.raycastTarget = false;
            }

            public void Bind(int number, LevelDefinition definition, float top)
            {
                _levelNumber = number;
                _definition = definition;
                _go.SetActive(true);
                _rect.anchoredPosition = new Vector2(0f, -top);

                bool unlocked = ProgressService.Current.IsUnlocked(number);
                bool completed = ProgressService.Current.IsCompleted(number);
                bool isCurrent = unlocked && !completed;
                int stars = ProgressService.Current.StarsFor(number);

                _group.alpha = unlocked ? 1f : 0.55f;
                _button.interactable = unlocked;
                _outline.gameObject.SetActive(isCurrent);

                _chip.color = unlocked ? UiTheme.CandyColors[(number - 1) % UiTheme.CandyColors.Length] : UiTheme.ThemeSlot;
                _number.text = number.ToString();
                _number.color = unlocked ? UiTheme.TextPrimary : UiTheme.TextDim;

                _label.text = $"Level {number}";
                _label.color = unlocked ? UiTheme.TextPrimary : UiTheme.TextDim;

                for (int i = 0; i < _pips.Length; i++)
                {
                    _pips[i].gameObject.SetActive(unlocked);
                    _pips[i].color = i < stars ? UiTheme.Gold : UiTheme.StarDim;
                }

                bool showBadge = unlocked && (completed || isCurrent);
                _badge.gameObject.SetActive(showBadge);
                if (showBadge)
                {
                    _badge.color = isCurrent ? UiTheme.Cta : BadgeDone;
                    _badgeLabel.text = isCurrent ? "PLAY" : "DONE";
                }

                _lock.gameObject.SetActive(!unlocked);
            }

            public void Hide() => _go.SetActive(false);
        }

        private void BuildButtons(Transform canvas)
        {
            var catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            int next = Mathf.Clamp(ProgressService.Current.HighestUnlocked, 1, catalog != null && catalog.Count > 0 ? catalog.Count : 1);

            // primary CTA: jump straight into the furthest unlocked level
            Button cta = NewButton("ContinueButton", canvas, $"Continue  -  Level {next}", UiTheme.PillPink, Color.white,
                UiTheme.TextPrimary, () => StartLevel(catalog != null ? catalog.Get(next) : null, next));
            Anchor((RectTransform)cta.transform, new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(720f, 132f));

            Button timeAttack = NewButton("TimeAttackButton", canvas, "Time Attack", UiTheme.Pill, UiTheme.Card,
                UiTheme.TextDim, StartTimeAttack);
            Anchor((RectTransform)timeAttack.transform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(720f, 110f));
            Image taOutline = NewImage("Outline", timeAttack.transform, UiTheme.OutlineDim);
            UiTheme.ApplySprite(taOutline, UiTheme.PillOutline, UiTheme.OutlineDim);
            Stretch(taOutline.rectTransform, Vector2.zero, Vector2.one);
            taOutline.raycastTarget = false;
        }

        private void StartLevel(LevelDefinition definition, int number)
        {
            AudioManager.Play(Sfx.Button);
            GameSession.Mode = GameMode.Moves;
            GameSession.SelectedLevel = definition;
            GameSession.SelectedLevelIndex = number;
            SceneManager.LoadScene("Game");
        }

        private void StartTimeAttack()
        {
            AudioManager.Play(Sfx.Button);
            GameSession.Mode = GameMode.TimeAttack;
            GameSession.SelectedLevel = null;
            GameSession.SelectedLevelIndex = 1;
            SceneManager.LoadScene("Game");
        }

        // ---- Small UI builders --------------------------------------------------------

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            UiTheme.ApplyFont(tmp, font);
            return tmp;
        }

        private Button NewButton(string name, Transform parent, string label, Sprite sprite, Color spriteColor,
                                 Color labelColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, sprite, spriteColor);
            if (sprite == null)
                image.color = spriteColor == Color.white ? UiTheme.Cta : spriteColor;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = NewText("Label", go.transform, label, 48f, FontStyles.Bold, UiTheme.ButtonFont);
            text.color = labelColor;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
