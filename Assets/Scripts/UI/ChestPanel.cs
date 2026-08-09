using Match3.Core;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The star chest (menu only): stars from level wins fill a repeating 20-star
    /// chest, milestones pay bigger ones. The panel shows the live total, a progress
    /// bar into the current window, and a CLAIM button that banks every newly earned
    /// chest into the booster inventory. Runtime-built like every other panel.
    /// </summary>
    public sealed class ChestPanel : MonoBehaviour
    {
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);

        private GameObject _root;
        private Image _card;
        private TMP_Text _starLine;
        private TMP_Text _nextLine;
        private RectTransform _barFill;
        private TMP_Text _resultLine;
        private TMP_Text _claimLabel;
        private Button _claimButton;
        private Image _claimImage;

        public static ChestPanel Attach(Canvas canvas, Transform buttonHost)
        {
            var host = new GameObject(nameof(ChestPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            host.SetActive(false);
            var panel = host.AddComponent<ChestPanel>();
            panel.Build();
            panel.BuildOpener(buttonHost);
            panel.Hide();
            host.SetActive(true);
            return panel;
        }

        private static int TotalStars() => ProgressService.Current.TotalStars;

        private void Refresh()
        {
            int total = TotalStars();
            MetaState meta = MetaService.Current;

            // No '★' glyph in TMP text — neither bundled TTF carries it (renders as
            // tofu); the star is drawn as a SPRITE next to this number instead.
            _starLine.text = total.ToString();
            int into = StarChest.StarsTowardNextChest(total);
            _barFill.anchorMax = new Vector2(Mathf.Clamp01(into / (float)StarChest.StarsPerChest), 1f);

            int pending = StarChest.RepeatingChestsEarned(total, meta.LastChestStars) +
                          StarChest.MilestonesCrossed(total, meta.LastChestStars);
            // Nothing to open reads as nothing to open: dim pill, inert button.
            _claimLabel.text = pending > 0 ? $"OPEN {pending}" : "CLAIM";
            if (_claimButton != null)
                _claimButton.interactable = pending > 0;
            if (_claimImage != null)
                _claimImage.color = new Color(1f, 1f, 1f, pending > 0 ? 1f : 0.4f);
            _nextLine.text = pending > 0
                ? $"{pending} chest{(pending > 1 ? "s" : "")} ready!"
                : $"{StarChest.StarsPerChest - into} stars to the next chest";
        }

        private void OnClaimClicked()
        {
            AudioManager.Play(Sfx.Button);
            int total = TotalStars();
            ChestReward reward = MetaService.CollectChests(total, out int opened, out int rescues);
            if (opened == 0)
            {
                _resultLine.text = "Win levels to fill the chest!";
            }
            else
            {
                AudioManager.Play(Sfx.Win);
                Haptics.Medium();
                string line = $"+{reward.Hammers} SMASH   +{reward.FreeSwaps} SWAP   +{reward.Shuffles} MIX";
                if (reward.StreakShields > 0)
                    line += $"   +{reward.StreakShields} SHIELD";
                if (rescues > 0)
                    line += $"   +{rescues} SAVE";
                _resultLine.text = line;
            }
            Refresh();
        }

        private void Show()
        {
            _card.color = UiTheme.ThemeCard;
            _resultLine.text = string.Empty;
            Refresh();
            UiTween.OpenPanel(this, _root, _card.transform);
        }

        private void Hide() => UiTween.ClosePanel(this, _root);

        private void OnOpenerClicked()
        {
            AudioManager.Play(Sfx.Button);
            if (_root.activeSelf) Hide();
            else Show();
        }

        // ---- Construction ---------------------------------------------------------------

        private void Build()
        {
            _root = CreateRect("Overlay", transform, Vector2.zero, Vector2.one, Vector2.zero);
            _root.AddComponent<Image>().color = OverlayColor;

            // Tapping the dim outside the card closes the panel (the card's own
            // raycast blocks pass-through - the album ceremony's idiom).
            var dismiss = _root.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(OnOpenerClicked);

            GameObject cardGo = CreateRect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(860f, 1000f));
            _card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            TMP_Text title = CreateText("Title", content, new Vector2(0f, 400f), 64f, FontStyles.Bold);
            UiTheme.ApplyFont(title, UiTheme.TitleFont);
            title.text = "STAR CHEST";

            // Big gold star headline: sprite star + number (no '★' glyph in the fonts).
            GameObject starGo = CreateRect("StarIcon", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(84f, 84f));
            starGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90f, 250f);
            var starIcon = starGo.AddComponent<Image>();
            UiTheme.ApplySprite(starIcon, UiTheme.StarSprite, UiTheme.Gold);
            starIcon.raycastTarget = false;

            _starLine = CreateText("Stars", content, new Vector2(60f, 250f), 96f, FontStyles.Bold);
            UiTheme.ApplyFont(_starLine, UiTheme.TitleFont);
            _starLine.color = UiTheme.Gold;
            _starLine.alignment = TextAlignmentOptions.MidlineLeft;
            _starLine.rectTransform.sizeDelta = new Vector2(300f, 110f);

            // Progress bar into the current 20-star window.
            GameObject trackGo = CreateRect("Track", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 34f));
            trackGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 120f);
            var track = trackGo.AddComponent<Image>();
            UiTheme.ApplySprite(track, UiTheme.Pill, UiTheme.Slot);
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            _barFill = (RectTransform)fillGo.transform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            UiTheme.ApplySprite(fill, UiTheme.Pill, UiTheme.Gold);
            fill.raycastTarget = false;

            _nextLine = CreateText("Next", content, new Vector2(0f, 50f), 34f, FontStyles.Normal);
            UiTheme.ApplyFont(_nextLine, UiTheme.BodyFont);
            _nextLine.color = UiTheme.TextDim;

            // Milestone dots (60..300) — filled once the total passes them.
            for (int i = 0; i < StarChest.Milestones.Length; i++)
            {
                GameObject dotGo = CreateRect($"Milestone{i}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56f, 56f));
                dotGo.GetComponent<RectTransform>().anchoredPosition = new Vector2((i - 2) * 120f, -60f);
                var dot = dotGo.AddComponent<Image>();
                UiTheme.ApplySprite(dot, UiTheme.StarSprite, UiTheme.StarDim);
                dot.raycastTarget = false;
                int milestone = StarChest.Milestones[i];
                dotGo.AddComponent<MilestoneDot>().Bind(dot, milestone);
                TMP_Text label = CreateText("Value", dotGo.transform, new Vector2(0f, -52f), 24f, FontStyles.Normal);
                label.rectTransform.sizeDelta = new Vector2(120f, 30f);
                label.color = UiTheme.TextDim;
                label.text = milestone.ToString();
                UiTheme.ApplyFont(label, UiTheme.BodyFont);
            }

            _resultLine = CreateText("Result", content, new Vector2(0f, -170f), 34f, FontStyles.Bold);
            UiTheme.ApplyFont(_resultLine, UiTheme.ButtonFont);
            _resultLine.color = UiTheme.Gold;

            // CLAIM + Close.
            GameObject claimGo = CreateRect("ClaimButton", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 130f));
            claimGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -290f);
            var claimImage = claimGo.AddComponent<Image>();
            UiTheme.ApplySprite(claimImage, UiTheme.PillPink, Color.white);
            var claimButton = claimGo.AddComponent<Button>();
            claimGo.AddComponent<PressableButton>();
            claimButton.targetGraphic = claimImage;
            claimButton.onClick.AddListener(OnClaimClicked);
            _claimButton = claimButton;
            _claimImage = claimImage;
            _claimLabel = CreateText("Label", claimGo.transform, Vector2.zero, 48f, FontStyles.Bold);
            UiTheme.ApplyFont(_claimLabel, UiTheme.ButtonFont);
            Stretch(_claimLabel.rectTransform);
            _claimLabel.text = "CLAIM";

            UiWidgets.ClosePill(content, new Vector2(0f, -420f), OnOpenerClicked);
        }

        /// <summary>Star pip that lights up once the running total passes its milestone.</summary>
        private sealed class MilestoneDot : MonoBehaviour
        {
            private Image _image;
            private int _milestone;

            public void Bind(Image image, int milestone)
            {
                _image = image;
                _milestone = milestone;
            }

            private void OnEnable()
            {
                if (_image != null)
                    _image.color = TotalStars() >= _milestone ? UiTheme.Gold : UiTheme.StarDim;
            }
        }

        private void BuildOpener(Transform buttonHost)
        {
            var go = new GameObject("ChestButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(170f, 76f);
            rect.anchoredPosition = new Vector2(-24f, -124f); // under the settings/RANKS row

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Pill, UiTheme.Slot);
            var button = go.GetComponent<Button>();
            go.AddComponent<PressableButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnOpenerClicked);

            TMP_Text label = CreateText("Label", go.transform, Vector2.zero, 34f, FontStyles.Bold);
            UiTheme.ApplyFont(label, UiTheme.ButtonFont);
            label.text = "CHEST";
            Stretch(label.rectTransform);
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
            GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(780f, 110f));
            go.GetComponent<RectTransform>().anchoredPosition = position;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
