using System.Collections.Generic;
using Match3.Core;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The Candy-Crush-style objective chips over the board: one pill per goal with
    /// the ACTUAL candy sprite (or a star for score, a jelly quad for jelly) and a
    /// live "12/30" counter that turns gold when done. Runtime-built like the result
    /// panel — no scene wiring — and replaces the HUD's plain-text objective summary
    /// (see <see cref="Active"/>, which HudView checks).
    /// Only meaningful in Moves mode; it hides itself in time attack.
    /// </summary>
    public sealed class ObjectiveBarView : MonoBehaviour
    {
        private const float ChipHeight = 96f;

        /// <summary>True while a bar is showing objectives — HudView then leaves its text label empty.</summary>
        public static bool Active { get; private set; }

        private GameManager _game;
        private GameObject _root;
        private CandySpriteLibrary _candies;
        private readonly List<(GameObject chip, Image icon, TMP_Text count)> _chips = new List<(GameObject, Image, TMP_Text)>();

        public static ObjectiveBarView Attach(Canvas canvas, GameManager game)
        {
            var host = new GameObject(nameof(ObjectiveBarView), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)host.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -300f);
            rect.sizeDelta = new Vector2(1000f, ChipHeight);

            // Same lifecycle trick as LevelResultPanel: construct deactivated so
            // OnEnable subscribes with _game already wired.
            host.SetActive(false);
            var bar = host.AddComponent<ObjectiveBarView>();
            bar._game = game;
            bar._candies = Resources.Load<CandySpriteLibrary>("CandySpriteLibrary");
            bar.Build();
            host.SetActive(true);
            return bar;
        }

        private void OnEnable()
        {
            if (_game == null) return;
            _game.ObjectivesChanged += Refresh;
            _game.LevelChanged += HandleLevelChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_game == null) return;
            _game.ObjectivesChanged -= Refresh;
            _game.LevelChanged -= HandleLevelChanged;
            Active = false;
        }

        private void Build()
        {
            _root = new GameObject("Chips", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            _root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;

            var layout = _root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _root.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void HandleLevelChanged(int level) => Refresh();

        private void Refresh()
        {
            ObjectiveTracker tracker = _game.Mode == GameMode.Moves ? _game.Objectives : null;
            if (tracker == null)
            {
                Active = false;
                _root.SetActive(false);
                return;
            }

            Active = true;
            _root.SetActive(true);
            EnsureChipCount(tracker.Count);

            for (int i = 0; i < tracker.Count; i++)
            {
                Objective objective = tracker.At(i);
                (GameObject _, Image icon, TMP_Text count) = _chips[i];

                (Sprite sprite, Color tint) = IconFor(objective);
                icon.sprite = sprite;
                icon.color = tint;

                bool done = tracker.IsComplete(i);
                count.text = $"{tracker.Progress(i)}/{objective.TargetAmount}";
                count.color = done ? UiTheme.Gold : UiTheme.TextPrimary;
            }
        }

        private (Sprite sprite, Color tint) IconFor(Objective objective)
        {
            switch (objective.Type)
            {
                case ObjectiveType.Score:
                    return (UiTheme.StarSprite, UiTheme.Gold);
                case ObjectiveType.ClearJelly:
                    return (UiTheme.Round, new Color(0.98f, 0.55f, 0.75f));
                default:
                    Sprite candy = _candies != null ? _candies.For(objective.ColorIndex, TileKind.Normal) : null;
                    return candy != null
                        ? (candy, Color.white)
                        : (UiTheme.CircleSprite, UiTheme.CandyColors[Mathf.Clamp(objective.ColorIndex, 0, UiTheme.CandyColors.Length - 1)]);
            }
        }

        private void EnsureChipCount(int needed)
        {
            while (_chips.Count < needed)
                _chips.Add(BuildChip());
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].chip.SetActive(i < needed);
        }

        private (GameObject chip, Image icon, TMP_Text count) BuildChip()
        {
            var chipGo = new GameObject($"Chip{_chips.Count}", typeof(RectTransform), typeof(Image));
            chipGo.transform.SetParent(_root.transform, false);
            ((RectTransform)chipGo.transform).sizeDelta = new Vector2(250f, ChipHeight);
            var pill = chipGo.GetComponent<Image>();
            UiTheme.ApplySprite(pill, UiTheme.Pill, UiTheme.Slot);
            pill.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(chipGo.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            iconRect.anchoredPosition = new Vector2(56f, 0f);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;

            var countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(chipGo.transform, false);
            var countRect = (RectTransform)countGo.transform;
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.offsetMin = new Vector2(100f, 0f);
            countRect.offsetMax = new Vector2(-16f, 0f);
            var count = countGo.AddComponent<TextMeshProUGUI>();
            count.fontSize = 40f;
            count.fontStyle = FontStyles.Bold;
            count.alignment = TextAlignmentOptions.Center;
            count.raycastTarget = false;
            UiTheme.ApplyFont(count, UiTheme.ButtonFont);

            return (chipGo, icon, count);
        }
    }
}
