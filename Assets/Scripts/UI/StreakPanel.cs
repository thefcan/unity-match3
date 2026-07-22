using Match3.Core;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The daily-streak calendar (main menu only): a 7-day reward ladder, a CLAIM
    /// button, and a badge on its opener while a claim is waiting. Rules live in
    /// Core's DailyStreak via MetaService — this panel only renders states and
    /// forwards the claim. Rewards apply to the NEXT level automatically
    /// (GameManager consumes the pending reward on build).
    /// </summary>
    public sealed class StreakPanel : MonoBehaviour
    {
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color DoneTint = new Color(0.32f, 0.5f, 0.32f);

        private GameObject _root;
        private Image _card;
        private TMP_Text _streakLabel;
        private TMP_Text _todayLabel;
        private TMP_Text _resultLabel;
        private GameObject _claimButton;
        private GameObject _badge;
        private readonly Image[] _dayPills = new Image[DailyStreak.CycleLength];
        private readonly TMP_Text[] _dayTexts = new TMP_Text[DailyStreak.CycleLength];
        private readonly Image[] _dayIcons = new Image[DailyStreak.CycleLength];
        private CandySpriteLibrary _candies;

        public static StreakPanel Attach(Canvas canvas, Transform buttonHost)
        {
            var host = new GameObject(nameof(StreakPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            host.SetActive(false);
            var panel = host.AddComponent<StreakPanel>();
            panel._candies = Resources.Load<CandySpriteLibrary>("CandySpriteLibrary");
            panel.Build();
            panel.BuildOpenerButton(buttonHost);
            panel.Hide();
            host.SetActive(true);
            return panel;
        }

        private void Show()
        {
            _card.color = UiTheme.ThemeCard;
            _resultLabel.text = string.Empty;
            Refresh();
            _root.SetActive(true);
        }

        private void Hide() => _root.SetActive(false);

        private void OnOpenerClicked()
        {
            AudioManager.Play(Sfx.Button);
            if (_root.activeSelf) Hide();
            else Show();
        }

        private void OnClaimClicked()
        {
            StreakReward? reward = MetaService.Claim();
            if (reward == null)
                return;

            AudioManager.Play(Sfx.SpecialCreate);
            Haptics.Medium();
            _resultLabel.text = RewardSentence(reward.Value);

            if (Prefs.NotificationsOn)
                NotificationScheduler.EnsurePermissionThenSchedule();

            Refresh();
        }

        private static string RewardSentence(StreakReward reward)
        {
            switch (reward.Kind)
            {
                case StreakRewardKind.ExtraMoves: return $"+{reward.Amount} moves on your next level!";
                case StreakRewardKind.StartStriped: return "Next level starts with a striped candy!";
                case StreakRewardKind.StartWrapped: return "Next level starts with a wrapped candy!";
                default: return "Next level starts with a COLOR BOMB!";
            }
        }

        // ---- State rendering ------------------------------------------------------------

        private void Refresh()
        {
            StreakStatus status = MetaService.Status;
            int streak = MetaService.Current.Streak;
            bool claimable = status != StreakStatus.AlreadyClaimed;
            int slot = (MetaService.NextClaimStreakDay - 1) % DailyStreak.CycleLength;

            _streakLabel.text = status == StreakStatus.Broken
                ? "Streak melted — starting fresh!"
                : $"Streak: {streak} day{(streak == 1 ? "" : "s")}";

            for (int i = 0; i < DailyStreak.CycleLength; i++)
            {
                bool done = i < slot || (!claimable && i == slot);
                bool isTarget = claimable && i == slot;
                _dayPills[i].color = isTarget ? UiTheme.Cta : done ? DoneTint : UiTheme.ThemeSlot;
                _dayTexts[i].color = done || isTarget ? UiTheme.TextPrimary : UiTheme.TextDim;
            }

            StreakReward next = DailyStreak.RewardFor(MetaService.NextClaimStreakDay);
            _todayLabel.text = claimable
                ? $"Today: {RewardShort(next)}"
                : "Come back tomorrow!";
            _claimButton.SetActive(claimable);
            RefreshBadge();
        }

        private void RefreshBadge()
        {
            if (_badge != null)
                _badge.SetActive(MetaService.Status != StreakStatus.AlreadyClaimed);
        }

        private static string RewardShort(StreakReward reward)
        {
            switch (reward.Kind)
            {
                case StreakRewardKind.ExtraMoves: return $"+{reward.Amount} moves";
                case StreakRewardKind.StartStriped: return "striped candy start";
                case StreakRewardKind.StartWrapped: return "wrapped candy start";
                default: return "color bomb start";
            }
        }

        // ---- Construction ---------------------------------------------------------------

        private void Build()
        {
            _root = CreateRect("Overlay", transform, Vector2.zero, Vector2.one, Vector2.zero);
            _root.AddComponent<Image>().color = OverlayColor;

            GameObject cardGo = CreateRect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900f, 1000f));
            _card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            TMP_Text title = CreateText("Title", content, new Vector2(0f, 400f), 68f, FontStyles.Bold);
            UiTheme.ApplyFont(title, UiTheme.TitleFont);
            title.text = "DAILY TREATS";

            _streakLabel = CreateText("Streak", content, new Vector2(0f, 300f), 42f, FontStyles.Normal);
            UiTheme.ApplyFont(_streakLabel, UiTheme.BodyFont);
            _streakLabel.color = UiTheme.TextDim;

            // The 7-day ladder. 110px pills with 8px gaps, centred.
            const float pillWidth = 112f;
            const float gap = 8f;
            float startX = -(DailyStreak.CycleLength - 1) * (pillWidth + gap) * 0.5f;
            for (int i = 0; i < DailyStreak.CycleLength; i++)
            {
                GameObject pillGo = CreateRect($"Day{i + 1}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(pillWidth, 170f));
                pillGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(startX + i * (pillWidth + gap), 120f);
                _dayPills[i] = pillGo.AddComponent<Image>();
                UiTheme.ApplySprite(_dayPills[i], UiTheme.Round, UiTheme.ThemeSlot);

                TMP_Text dayNo = CreateText("No", pillGo.transform, new Vector2(0f, 55f), 26f, FontStyles.Bold);
                UiTheme.ApplyFont(dayNo, UiTheme.BodyFont);
                dayNo.text = $"DAY {i + 1}";
                dayNo.rectTransform.sizeDelta = new Vector2(pillWidth, 40f);

                StreakReward reward = DailyStreak.RewardFor(i + 1);
                Sprite icon = IconFor(reward);
                if (icon != null)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(pillGo.transform, false);
                    var iconRect = (RectTransform)iconGo.transform;
                    iconRect.sizeDelta = new Vector2(64f, 64f);
                    iconRect.anchoredPosition = new Vector2(0f, -20f);
                    _dayIcons[i] = iconGo.GetComponent<Image>();
                    _dayIcons[i].sprite = icon;
                    _dayIcons[i].raycastTarget = false;
                }

                _dayTexts[i] = CreateText("Reward", pillGo.transform, new Vector2(0f, icon != null ? -62f : -20f),
                                          icon != null ? 24f : 40f, FontStyles.Bold);
                UiTheme.ApplyFont(_dayTexts[i], UiTheme.ButtonFont);
                _dayTexts[i].text = reward.Kind == StreakRewardKind.ExtraMoves ? $"+{reward.Amount}" : string.Empty;
                _dayTexts[i].rectTransform.sizeDelta = new Vector2(pillWidth, 44f);
            }

            _todayLabel = CreateText("Today", content, new Vector2(0f, -30f), 40f, FontStyles.Bold);
            UiTheme.ApplyFont(_todayLabel, UiTheme.BodyFont);

            _resultLabel = CreateText("Result", content, new Vector2(0f, -110f), 34f, FontStyles.Normal);
            UiTheme.ApplyFont(_resultLabel, UiTheme.BodyFont);
            _resultLabel.color = UiTheme.Gold;

            _claimButton = BuildButton(content, "CLAIM", new Vector2(0f, -230f), UiTheme.PillPink, Color.white, UiTheme.TextPrimary, () =>
            {
                AudioManager.Play(Sfx.Button);
                OnClaimClicked();
            });

            BuildButton(content, "Close", new Vector2(0f, -380f), UiTheme.Pill, UiTheme.Slot, UiTheme.TextDim, () =>
            {
                AudioManager.Play(Sfx.Button);
                Hide();
            });
        }

        private Sprite IconFor(StreakReward reward)
        {
            if (_candies == null)
                return null;
            switch (reward.Kind)
            {
                case StreakRewardKind.StartStriped: return _candies.For(2, TileKind.StripedV);
                case StreakRewardKind.StartWrapped: return _candies.For(3, TileKind.Wrapped);
                case StreakRewardKind.StartColorBomb: return _candies.For(0, TileKind.ColorBomb);
                default: return null;
            }
        }

        /// <summary>Opener pill (top-left): "DAILY" + a gold dot while a claim waits.</summary>
        private void BuildOpenerButton(Transform buttonHost)
        {
            var go = new GameObject("DailyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(200f, 76f);
            rect.anchoredPosition = new Vector2(24f, -24f);

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Pill, UiTheme.Slot);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnOpenerClicked);

            TMP_Text label = CreateText("Label", go.transform, Vector2.zero, 34f, FontStyles.Bold);
            UiTheme.ApplyFont(label, UiTheme.ButtonFont);
            label.text = "DAILY";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            _badge.transform.SetParent(go.transform, false);
            var badgeRect = (RectTransform)_badge.transform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.sizeDelta = new Vector2(30f, 30f);
            badgeRect.anchoredPosition = new Vector2(-4f, 4f);
            var badgeImage = _badge.GetComponent<Image>();
            UiTheme.ApplySprite(badgeImage, UiTheme.CircleSprite, UiTheme.Gold);
            badgeImage.raycastTarget = false;
            RefreshBadge();
        }

        private GameObject BuildButton(Transform parent, string label, Vector2 position,
                                       Sprite sprite, Color spriteColor, Color labelColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreateRect(label + "Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560f, 124f));
            go.GetComponent<RectTransform>().anchoredPosition = position;
            var image = go.AddComponent<Image>();
            UiTheme.ApplySprite(image, sprite, spriteColor);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = CreateText("Label", go.transform, Vector2.zero, 46f, FontStyles.Bold);
            UiTheme.ApplyFont(text, UiTheme.ButtonFont);
            text.color = labelColor;
            text.text = label;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return go;
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
            GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(820f, 90f));
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
