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

        private void Start()
        {
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
            Image background = NewImage("Background", canvas, Color.white);
            UiTheme.ApplySprite(background, UiTheme.BgGradient, Color.white);
            if (background.sprite == null)
                background.color = UiTheme.BgDeep;
            Stretch(background.rectTransform, Vector2.zero, Vector2.one);
            background.raycastTarget = false;

            TMP_Text title = NewText("Title", canvas, "Candy Match", 112f, FontStyles.Bold, UiTheme.TitleFont);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(980f, 150f));

            // the five candy dots under the logo
            var dots = NewRect("CandyDots", canvas);
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

            TMP_Text subtitle = NewText("Subtitle", canvas, "A sweet 20-level campaign", 38f, FontStyles.Normal, UiTheme.BodyFont);
            subtitle.color = UiTheme.TextDim;
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(900f, 60f));

            TMP_Text mapLabel = NewText("MapLabel", canvas, "L E V E L   M A P", 30f, FontStyles.Bold, UiTheme.BodyFont);
            mapLabel.color = UiTheme.TextDim;
            Anchor(mapLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(900f, 44f));

            BuildLevelList(canvas);
            BuildButtons(canvas);
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

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var catalog = Resources.Load<LevelCatalog>("LevelCatalog");
            if (catalog == null || catalog.Count == 0)
            {
                TMP_Text missing = NewText("NoLevels", content, "No levels found.\nRun Match3 > Generate > Level Definitions.", 36f, FontStyles.Normal, UiTheme.BodyFont);
                missing.rectTransform.sizeDelta = new Vector2(0f, 160f);
                return;
            }

            for (int number = 1; number <= catalog.Count; number++)
                BuildLevelRow(content, catalog, number);
        }

        private void BuildLevelRow(Transform content, LevelCatalog catalog, int number)
        {
            bool unlocked = ProgressService.Current.IsUnlocked(number);
            bool isCurrent = unlocked && !ProgressService.Current.IsCompleted(number);
            int stars = ProgressService.Current.StarsFor(number);

            var rowGo = new GameObject($"Level_{number:00}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(CanvasGroup));
            rowGo.transform.SetParent(content, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            rowGo.GetComponent<CanvasGroup>().alpha = unlocked ? 1f : 0.55f;

            var card = rowGo.GetComponent<Image>();
            UiTheme.ApplySprite(card, UiTheme.Round, UiTheme.Card);

            var button = rowGo.GetComponent<Button>();
            button.targetGraphic = card;
            button.interactable = unlocked;
            LevelDefinition definition = catalog.Get(number);
            int capturedNumber = number;
            button.onClick.AddListener(() => StartLevel(definition, capturedNumber));

            if (isCurrent)
            {
                Image outline = NewImage("Outline", rowGo.transform, UiTheme.Cta);
                UiTheme.ApplySprite(outline, UiTheme.RoundOutline, UiTheme.Cta);
                Stretch(outline.rectTransform, Vector2.zero, Vector2.one);
                outline.raycastTarget = false;
            }

            // candy chip with the level number — the chip colour cycles the candy palette
            Color chipColor = unlocked ? UiTheme.CandyColors[(number - 1) % UiTheme.CandyColors.Length] : UiTheme.Slot;
            Image chip = NewImage("Chip", rowGo.transform, chipColor);
            UiTheme.ApplySprite(chip, UiTheme.CircleSprite, chipColor);
            var chipRect = chip.rectTransform;
            chipRect.anchorMin = chipRect.anchorMax = new Vector2(0f, 0.5f);
            chipRect.sizeDelta = new Vector2(96f, 96f);
            chipRect.anchoredPosition = new Vector2(76f, 0f);
            chip.raycastTarget = false;

            TMP_Text numberLabel = NewText("Number", chip.transform, number.ToString(), 44f, FontStyles.Bold, UiTheme.ButtonFont);
            Stretch(numberLabel.rectTransform, Vector2.zero, Vector2.one);
            numberLabel.color = unlocked ? UiTheme.TextPrimary : UiTheme.TextDim;

            TMP_Text label = NewText("Label", rowGo.transform, $"Level {number}", 44f, FontStyles.Bold, UiTheme.BodyFont);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = unlocked ? UiTheme.TextPrimary : UiTheme.TextDim;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.6f, 1f);
            labelRect.offsetMin = new Vector2(150f, 0f);
            labelRect.offsetMax = Vector2.zero;

            if (unlocked)
            {
                for (int i = 0; i < 3; i++)
                {
                    Image pip = NewImage($"Star{i}", rowGo.transform, i < stars ? UiTheme.Gold : UiTheme.StarDim);
                    UiTheme.ApplySprite(pip, UiTheme.StarSprite, i < stars ? UiTheme.Gold : UiTheme.StarDim);
                    var pipRect = pip.rectTransform;
                    pipRect.anchorMin = pipRect.anchorMax = new Vector2(1f, 0.5f);
                    pipRect.sizeDelta = new Vector2(56f, 56f);
                    pipRect.anchoredPosition = new Vector2(-52f - (2 - i) * 64f, 0f);
                    if (pip.sprite == null) // fallback: rotated square reads as a diamond pip
                        pip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    pip.raycastTarget = false;
                }
            }
            else
            {
                Image lockIcon = NewImage("Lock", rowGo.transform, UiTheme.TextDim);
                UiTheme.ApplySprite(lockIcon, UiTheme.LockSprite, UiTheme.TextDim);
                var lockRect = lockIcon.rectTransform;
                lockRect.anchorMin = lockRect.anchorMax = new Vector2(1f, 0.5f);
                lockRect.sizeDelta = new Vector2(52f, 52f);
                lockRect.anchoredPosition = new Vector2(-70f, 0f);
                lockIcon.raycastTarget = false;
            }
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
