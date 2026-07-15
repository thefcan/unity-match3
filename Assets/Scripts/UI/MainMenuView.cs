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
    /// scene holds only a camera and this component — same no-scene-wiring approach
    /// as <see cref="LevelResultPanel"/>). Levels come from Resources/LevelCatalog,
    /// stars and locks from <see cref="ProgressService"/>; a separate button starts
    /// the original time-attack mode.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.09f, 0.1f, 0.14f);
        private static readonly Color RowColor = new Color(0.16f, 0.2f, 0.3f);
        private static readonly Color RowLocked = new Color(0.12f, 0.13f, 0.16f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.52f, 0.9f);
        private static readonly Color StarEarned = new Color(1f, 0.8f, 0.15f);
        private static readonly Color StarMissing = new Color(0.3f, 0.3f, 0.35f);

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
            Image background = NewImage("Background", canvas, Background);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            TMP_Text title = NewText("Title", canvas, "Candy Match", 96f, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 130f));

            TMP_Text subtitle = NewText("Subtitle", canvas, "Pick a level", 40f, FontStyles.Normal);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 60f));

            BuildLevelList(canvas);

            Button timeAttack = NewButton("TimeAttackButton", canvas, "Time Attack", ButtonColor, StartTimeAttack);
            Anchor(((RectTransform)timeAttack.transform), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 110f));
        }

        private void BuildLevelList(Transform canvas)
        {
            // ScrollRect -> viewport (masked) -> content (vertical layout, auto-height).
            var scrollGo = new GameObject("LevelScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(canvas, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = new Vector2(0.06f, 0.15f);
            scrollRect.anchorMax = new Vector2(0.94f, 0.83f);
            scrollRect.sizeDelta = Vector2.zero;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport, Vector2.zero, Vector2.one);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
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
                TMP_Text missing = NewText("NoLevels", content, "No levels found.\nRun Match3 > Generate > Level Definitions.", 36f, FontStyles.Normal);
                missing.rectTransform.sizeDelta = new Vector2(0f, 160f);
                return;
            }

            for (int number = 1; number <= catalog.Count; number++)
                BuildLevelRow(content, catalog, number);
        }

        private void BuildLevelRow(Transform content, LevelCatalog catalog, int number)
        {
            bool unlocked = ProgressService.Current.IsUnlocked(number);
            int stars = ProgressService.Current.StarsFor(number);

            var rowGo = new GameObject($"Level_{number:00}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            rowGo.transform.SetParent(content, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 110f;

            var image = rowGo.GetComponent<Image>();
            image.color = unlocked ? RowColor : RowLocked;

            var button = rowGo.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = unlocked;
            LevelDefinition definition = catalog.Get(number);
            int capturedNumber = number;
            button.onClick.AddListener(() => StartLevel(definition, capturedNumber));

            TMP_Text label = NewText("Label", rowGo.transform, unlocked ? $"Level {number}" : $"Level {number}  (locked)", 44f, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.6f, 1f);
            labelRect.offsetMin = new Vector2(30f, 0f);
            labelRect.offsetMax = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                Image pip = NewImage($"Star{i}", rowGo.transform, i < stars ? StarEarned : StarMissing);
                var pipRect = pip.rectTransform;
                pipRect.anchorMin = pipRect.anchorMax = new Vector2(1f, 0.5f);
                pipRect.sizeDelta = new Vector2(34f, 34f);
                pipRect.anchoredPosition = new Vector2(-40f - (2 - i) * 55f, 0f);
                pip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                pip.raycastTarget = false;
            }
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

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style)
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
            return tmp;
        }

        private static Button NewButton(string name, Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = NewText("Label", go.transform, label, 44f, FontStyles.Bold);
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
