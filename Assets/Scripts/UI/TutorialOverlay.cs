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
    /// The one-line teaching moment (Stitch design): on an act-opener's first look
    /// the board dims behind a veil, the mechanic's cells glow inside pulsing gold
    /// rings, and a floating banner says one short line ("MATCH BESIDE THE
    /// FROSTING"). The first tap anywhere dismisses it — the veil is a fullscreen
    /// button, so the tap can never leak onto the board. Shown once per level per
    /// app run; levels without tutorialText never build anything.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private static readonly HashSet<int> ShownThisRun = new HashSet<int>();

        private GameManager _game;
        private GameObject _root;
        private Image _card;
        private Image _cardOutline;
        private TMP_Text _title;
        private TMP_Text _caption;
        private readonly List<Image> _rings = new List<Image>();
        private Transform _ringHost;
        private Coroutine _pulse;

        public static TutorialOverlay Attach(Canvas canvas, GameManager game)
        {
            var host = new GameObject(nameof(TutorialOverlay), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)host.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            host.SetActive(false);
            var overlay = host.AddComponent<TutorialOverlay>();
            overlay._game = game;
            overlay.Build();
            host.SetActive(true);
            return overlay;
        }

        private void OnEnable()
        {
            if (_game == null) return;
            _game.LevelChanged += HandleLevelChanged;
            _game.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            VeilOpen = false; // a scene change must never leave the flag stuck on
            if (_game == null) return;
            _game.LevelChanged -= HandleLevelChanged;
            _game.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandleLevelChanged(int level)
        {
            LevelDefinition definition = _game.LevelDefinition;
            if (_game.Mode != GameMode.Moves || definition == null ||
                string.IsNullOrEmpty(definition.tutorialText) || ShownThisRun.Contains(level))
            {
                Hide();
                return;
            }

            ShownThisRun.Add(level);
            StartCoroutine(ShowAfterLayout(definition));
        }

        /// <summary>
        /// True while the teaching veil is on screen. The veil covers a PLAYING
        /// board and eats every touch, so the idle-hint timer must not treat that
        /// as the player thinking (GameManager.TickHint).
        /// </summary>
        public static bool VeilOpen { get; private set; }

        /// <summary>Anything that takes the board out of Playing sweeps the overlay away.</summary>
        private void HandlePhaseChanged(GamePhase phase)
        {
            if (_root != null && _root.activeSelf &&
                phase != GamePhase.Playing && phase != GamePhase.Init)
                Hide();
        }

        /// <summary>Camera fitting and board spawning settle first — then we can map cells to screen.</summary>
        private IEnumerator ShowAfterLayout(LevelDefinition definition)
        {
            yield return null;
            yield return null;

            _card.color = UiTheme.ThemeCard;
            _title.text = definition.tutorialText;
            PositionRings(definition.tutorialCells);
            UiTween.OpenPanel(this, _root, _card.transform);
            VeilOpen = true;
            if (_pulse != null)
                StopCoroutine(_pulse);
            _pulse = StartCoroutine(Pulse());
        }

        private void Hide()
        {
            VeilOpen = false;
            if (_pulse != null)
            {
                StopCoroutine(_pulse);
                _pulse = null;
            }
            if (_root != null)
                UiTween.ClosePanel(this, _root);
        }

        // ---- Construction ---------------------------------------------------------------

        private void Build()
        {
            _root = new GameObject("Veil", typeof(RectTransform), typeof(Image), typeof(Button));
            _root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var veil = _root.GetComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0.6f);

            // The whole veil is the dismiss button — "tap to start".
            var button = _root.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Button);
                Hide();
            });

            _ringHost = new GameObject("Rings", typeof(RectTransform)).transform;
            _ringHost.SetParent(_root.transform, false);
            var ringHostRect = (RectTransform)_ringHost;
            ringHostRect.anchorMin = Vector2.zero;
            ringHostRect.anchorMax = Vector2.one;
            ringHostRect.offsetMin = Vector2.zero;
            ringHostRect.offsetMax = Vector2.zero;

            var cardGo = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_root.transform, false);
            var cardRect = (RectTransform)cardGo.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(780f, 190f);
            cardRect.anchoredPosition = new Vector2(0f, 560f);
            _card = cardGo.GetComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);

            var outlineGo = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            outlineGo.transform.SetParent(cardGo.transform, false);
            var outlineRect = (RectTransform)outlineGo.transform;
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;
            _cardOutline = outlineGo.GetComponent<Image>();
            UiTheme.ApplySprite(_cardOutline, UiTheme.RoundOutline, UiTheme.Gold);
            _cardOutline.raycastTarget = false;

            _title = NewText("Title", cardGo.transform, new Vector2(0f, 22f), 44f);
            UiTheme.ApplyFont(_title, UiTheme.TitleFont);
            _title.fontStyle = FontStyles.Bold;

            _caption = NewText("Caption", cardGo.transform, new Vector2(0f, -46f), 28f);
            UiTheme.ApplyFont(_caption, UiTheme.BodyFont);
            _caption.color = UiTheme.TextDim;
            _caption.text = "tap to start";
        }

        /// <summary>
        /// Maps each highlighted BOARD cell into canvas space and drops a gold ring
        /// on it. No cells = banner only (mechanics that arrive via refills).
        /// </summary>
        private void PositionRings(Vector2Int[] cells)
        {
            foreach (Image ring in _rings)
                ring.gameObject.SetActive(false);
            if (cells == null || cells.Length == 0 || Camera.main == null || _game.BoardView == null)
                return;

            var canvasRect = (RectTransform)transform;
            float cellCanvasSize = CellCanvasSize(canvasRect);

            for (int i = 0; i < cells.Length; i++)
            {
                Image ring = i < _rings.Count ? _rings[i] : CreateRing();
                ring.gameObject.SetActive(true);

                Vector3 world = _game.BoardView.GridToWorld(new GridPosition(cells[i].x, cells[i].y));
                Vector2 screen = Camera.main.WorldToScreenPoint(world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
                var rect = ring.rectTransform;
                rect.anchoredPosition = local;
                rect.sizeDelta = new Vector2(cellCanvasSize, cellCanvasSize) * 1.12f;
            }
        }

        /// <summary>One board cell's size in canvas units (projected through the camera).</summary>
        private float CellCanvasSize(RectTransform canvasRect)
        {
            Vector2 a = Camera.main.WorldToScreenPoint(_game.BoardView.GridToWorld(new GridPosition(0, 0)));
            Vector2 b = Camera.main.WorldToScreenPoint(_game.BoardView.GridToWorld(new GridPosition(1, 0)));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, a, null, out Vector2 localA);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, b, null, out Vector2 localB);
            return Mathf.Abs(localB.x - localA.x);
        }

        private Image CreateRing()
        {
            var go = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_ringHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.RoundOutline, UiTheme.Gold);
            image.raycastTarget = false;
            _rings.Add(image);
            return image;
        }

        /// <summary>Gentle breathing on the rings and the banner while the veil is up.</summary>
        private IEnumerator Pulse()
        {
            Transform banner = _card.transform;
            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                float scale = 1f + 0.06f * Mathf.Sin(t * 4.2f);
                foreach (Image ring in _rings)
                    if (ring.gameObject.activeSelf)
                        ring.transform.localScale = Vector3.one * scale;
                banner.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(t * 2.4f));
                yield return null;
            }
        }

        private static TMP_Text NewText(string name, Transform parent, Vector2 position, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 80f);
            rect.anchoredPosition = position;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
