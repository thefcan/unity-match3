using System.Collections;
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

        private GameManager _game;
        private GameObject _root;
        private CandySpriteLibrary _candies;
        private readonly List<(GameObject chip, Image icon, TMP_Text count, Image outline)> _chips = new List<(GameObject, Image, TMP_Text, Image)>();
        private readonly List<int> _visible = new List<int>(); // tracker indices minus the Score goal

        // Fly-to-chip sparks: last shown progress per tracker index (the delta
        // detector), a small flyer pool, and the board/canvas handles for the
        // world → canvas conversion. Counters always show tracker truth — the
        // flyers are decoration launched AFTER the number already updated.
        private readonly Dictionary<int, int> _seenProgress = new Dictionary<int, int>();
        private readonly Stack<Image> _flyerPool = new Stack<Image>();
        private Match3.View.BoardView _boardView;
        private Canvas _rootCanvas;

        public static ObjectiveBarView Attach(Transform parent, GameManager game)
        {
            var host = new GameObject(nameof(ObjectiveBarView), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            var rect = (RectTransform)host.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -292f); // just under the 250-tall top bar
            rect.sizeDelta = new Vector2(1000f, ChipHeight);

            // Chip pops, completion rims and fly-to-chip sparks all move UI
            // transforms per frame; a nested Canvas keeps those rebuilds off the
            // shared HUD canvas.
            host.AddComponent<Canvas>();

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

        private void HandleLevelChanged(int level)
        {
            _seenProgress.Clear(); // new level — next refresh sets the baseline, no flyers
            Refresh();
        }

        private void Refresh()
        {
            ObjectiveTracker tracker = _game.Mode == GameMode.Moves ? _game.Objectives : null;
            if (tracker == null)
            {
                _root.SetActive(false);
                return;
            }

            // The Score goal is OWNED by the HUD's gold progress bar (ScoreProgressBar);
            // its five-digit counter is exactly what used to wrap inside a pill here.
            _visible.Clear();
            for (int i = 0; i < tracker.Count; i++)
            {
                if (tracker.At(i).Type != ObjectiveType.Score)
                    _visible.Add(i);
            }

            if (_visible.Count == 0)
            {
                _root.SetActive(false);
                return;
            }

            _root.SetActive(true);
            EnsureChipCount(_visible.Count);

            for (int c = 0; c < _visible.Count; c++)
            {
                int i = _visible[c];
                Objective objective = tracker.At(i);
                (GameObject chipGo, Image icon, TMP_Text count, Image outline) = _chips[c];

                // Re-tint the pill each refresh — the chapter theme drifts per level.
                if (chipGo.TryGetComponent(out Image pill))
                    pill.color = UiTheme.ThemeSlot;

                (Sprite sprite, Color tint) = IconFor(objective);
                icon.sprite = sprite;
                icon.color = tint;

                bool done = tracker.IsComplete(i);
                int progress = tracker.Progress(i);
                count.text = $"{progress}/{objective.TargetAmount}";
                count.color = done ? UiTheme.Gold : UiTheme.TextPrimary;
                // The gold rim pops in the moment a goal completes (Stitch) — the
                // rim used to blink on with no beat of its own.
                bool wasDone = outline.gameObject.activeSelf;
                outline.gameObject.SetActive(done);
                if (done && !wasDone)
                {
                    // A goal completing is the level's core progress beat and it was
                    // entirely silent. Sound and haptic sit OUTSIDE the reduced-motion
                    // gate — that pref suppresses movement, not feedback.
                    AudioManager.Play(Sfx.SpecialCreate, 1.25f);
                    Haptics.Light();
                    if (!Prefs.ReducedMotionOn)
                        StartCoroutine(UiTween.ScalePop(outline.transform, 0.25f));
                }

                // Sparks only on an INCREASE seen while the chip was already on
                // screen — the first refresh of a level just sets the baseline.
                if (_seenProgress.TryGetValue(i, out int seen) && progress > seen)
                    LaunchFlyers(Mathf.Min(progress - seen, 3), c);
                _seenProgress[i] = progress;

                // Pills hug their text: long counters widen the chip instead of wrapping.
                float textWidth = count.GetPreferredValues(count.text).x;
                ((RectTransform)chipGo.transform).sizeDelta =
                    new Vector2(Mathf.Max(196f, 124f + textWidth), ChipHeight);
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
                case ObjectiveType.ClearChocolate:
                {
                    Sprite chocolate = _candies != null ? _candies.For(0, TileKind.Chocolate) : null;
                    return chocolate != null
                        ? (chocolate, Color.white)
                        : (UiTheme.Round, new Color(0.36f, 0.22f, 0.12f));
                }
                case ObjectiveType.CollectIngredients:
                {
                    Sprite ingredient = _candies != null ? _candies.For(0, TileKind.Ingredient) : null;
                    return ingredient != null
                        ? (ingredient, Color.white)
                        : (UiTheme.CircleSprite, new Color(0.92f, 0.30f, 0.28f));
                }
                case ObjectiveType.ClearFrosting:
                {
                    Sprite frosting = _candies != null ? _candies.For(0, TileKind.Frosting) : null;
                    return frosting != null
                        ? (frosting, Color.white)
                        : (UiTheme.Round, new Color(0.85f, 0.9f, 1f));
                }
                case ObjectiveType.HatchEggs:
                {
                    Sprite egg = _candies != null ? _candies.For(0, TileKind.MysteryEgg) : null;
                    return egg != null
                        ? (egg, Color.white)
                        : (UiTheme.CircleSprite, new Color(0.94f, 0.9f, 0.8f));
                }
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

        private (GameObject chip, Image icon, TMP_Text count, Image outline) BuildChip()
        {
            var chipGo = new GameObject($"Chip{_chips.Count}", typeof(RectTransform), typeof(Image));
            chipGo.transform.SetParent(_root.transform, false);
            ((RectTransform)chipGo.transform).sizeDelta = new Vector2(196f, ChipHeight);
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
            countRect.offsetMin = new Vector2(96f, 0f);
            countRect.offsetMax = new Vector2(-20f, 0f);
            var count = countGo.AddComponent<TextMeshProUGUI>();
            count.fontSize = 36f;
            count.fontStyle = FontStyles.Bold;
            count.alignment = TextAlignmentOptions.Center;
            count.enableWordWrapping = false; // the chip resizes; the counter never breaks
            count.raycastTarget = false;
            UiTheme.ApplyFont(count, UiTheme.ButtonFont);

            // Gold rim, shown only when the goal is complete.
            var outlineGo = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            outlineGo.transform.SetParent(chipGo.transform, false);
            var outlineRect = (RectTransform)outlineGo.transform;
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;
            var outline = outlineGo.GetComponent<Image>();
            UiTheme.ApplySprite(outline, UiTheme.PillOutline, UiTheme.Gold);
            outline.raycastTarget = false;
            outlineGo.SetActive(false);

            return (chipGo, icon, count, outline);
        }

        // ---- Fly-to-chip sparks (pure decoration; scaled time on purpose so they
        // freeze WITH the board under pause) ------------------------------------

        private void LaunchFlyers(int count, int chipIndex)
        {
            if (Prefs.ReducedMotionOn)
                return;
            if (_boardView == null)
                _boardView = FindObjectOfType<Match3.View.BoardView>();
            if (_boardView == null)
                return; // no board in this scene — nothing to launch from

            Vector3 start = BoardToCanvas(_boardView.LastClearCentroid);
            for (int n = 0; n < count; n++)
                StartCoroutine(Fly(start, chipIndex, n * 0.05f));
        }

        private Vector3 BoardToCanvas(Vector3 world)
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = Camera.main;
            Vector3 screen = cam != null
                ? cam.WorldToScreenPoint(world)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f);
            if (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return new Vector3(screen.x, screen.y, 0f); // overlay canvas: world == screen pixels
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)_rootCanvas.transform, screen, _rootCanvas.worldCamera, out Vector3 point);
            return point;
        }

        private IEnumerator Fly(Vector3 from, int chipIndex, float delay)
        {
            for (float w = 0f; w < delay; w += Time.deltaTime)
                yield return null;
            if (chipIndex >= _chips.Count)
                yield break;
            (GameObject chipGo, Image icon, _, _) = _chips[chipIndex];

            Image flyer = GetFlyer();
            flyer.sprite = icon.sprite;
            flyer.color = icon.color;
            flyer.transform.position = from;
            flyer.gameObject.SetActive(true);

            float arc = 40f * (_rootCanvas != null ? _rootCanvas.scaleFactor : 1f);
            Transform target = icon.transform; // chased live — chips can resize mid-flight
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                flyer.transform.position = Vector3.Lerp(from, target.position, s)
                                           + Vector3.up * (arc * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            flyer.gameObject.SetActive(false);
            _flyerPool.Push(flyer);

            AudioManager.Play(Sfx.Pop, 1.3f, 0.5f);
            StartCoroutine(ChipPop(chipGo.transform));
        }

        private Image GetFlyer()
        {
            Image flyer = null;
            while (_flyerPool.Count > 0 && flyer == null)
                flyer = _flyerPool.Pop(); // drain destroyed instances
            if (flyer == null)
            {
                var go = new GameObject("Flyer", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false); // after Chips — renders over them
                ((RectTransform)go.transform).sizeDelta = new Vector2(56f, 56f);
                flyer = go.GetComponent<Image>();
                flyer.raycastTarget = false;
            }
            return flyer;
        }

        private IEnumerator ChipPop(Transform chip)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.16f)
            {
                chip.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            chip.localScale = Vector3.one;
        }
    }
}
