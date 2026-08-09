using Match3.Core;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// Daily missions (menu only): three deterministic tasks per day plus one
    /// weekly, one reroll per day, rewards paid into the booster inventory.
    /// Definitions regenerate from the day number (Core MissionCatalog); this panel
    /// only renders state and forwards clicks to <see cref="MissionService"/>.
    /// </summary>
    public sealed class TasksPanel : MonoBehaviour
    {
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);

        private sealed class Row
        {
            public TMP_Text Description;
            public TMP_Text Progress;
            public Button Claim;
            public TMP_Text ClaimLabel;
            public Image ClaimImage;
            public Button Reroll;
        }

        private GameObject _root;
        private Image _card;
        private readonly Row[] _rows = new Row[MissionCatalog.DailyCount];
        private Row _weeklyRow;

        public static TasksPanel Attach(Canvas canvas, Transform buttonHost)
        {
            var host = new GameObject(nameof(TasksPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            host.SetActive(false);
            var panel = host.AddComponent<TasksPanel>();
            panel.Build();
            panel.BuildOpener(buttonHost);
            panel.Hide();
            host.SetActive(true);
            return panel;
        }

        private void Refresh()
        {
            MissionService.EnsureFresh();
            MetaState meta = MetaService.Current;

            for (int slot = 0; slot < _rows.Length; slot++)
            {
                MissionDef def = MissionService.DefForSlot(slot);
                RefreshRow(_rows[slot], def, meta.MissionProgress[slot], meta.MissionClaimed[slot]);
                bool rerollable = meta.RerolledSlot == -1 && !meta.MissionClaimed[slot];
                _rows[slot].Reroll.gameObject.SetActive(rerollable);
            }

            RefreshRow(_weeklyRow, MissionService.WeeklyDef, meta.WeeklyProgress, meta.WeeklyClaimed);
            _weeklyRow.Reroll.gameObject.SetActive(false);
        }

        private static void RefreshRow(Row row, MissionDef def, int progress, bool claimed)
        {
            row.Description.text = MissionService.Describe(def);
            row.Progress.text = $"{Mathf.Min(progress, def.Target)}/{def.Target}";

            bool done = progress >= def.Target;
            row.ClaimLabel.text = claimed ? "DONE" : "CLAIM";
            row.Claim.interactable = done && !claimed;
            row.ClaimImage.color = claimed ? UiTheme.Slot : (done ? UiTheme.Cta : UiTheme.Slot);
            row.Progress.color = done ? UiTheme.Gold : UiTheme.TextDim;
        }

        private void OnClaim(int slot)
        {
            AudioManager.Play(Sfx.Button);
            bool paid = slot < 0 ? MissionService.ClaimWeekly() : MissionService.Claim(slot);
            if (paid)
            {
                AudioManager.Play(Sfx.Win);
                Haptics.Medium();
            }
            Refresh();
        }

        private void OnReroll(int slot)
        {
            AudioManager.Play(Sfx.Button);
            MissionService.Reroll(slot);
            Refresh();
        }

        private void Show()
        {
            _card.color = UiTheme.ThemeCard;
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

            GameObject cardGo = CreateRect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(880f, 1180f));
            _card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            TMP_Text title = CreateText("Title", content, new Vector2(0f, 500f), 64f, FontStyles.Bold);
            UiTheme.ApplyFont(title, UiTheme.TitleFont);
            title.text = "DAILY TASKS";

            TMP_Text hint = CreateText("Hint", content, new Vector2(0f, 425f), 28f, FontStyles.Normal);
            UiTheme.ApplyFont(hint, UiTheme.BodyFont);
            hint.color = UiTheme.TextDim;
            hint.text = "New tasks at midnight — one swap per day";

            for (int slot = 0; slot < _rows.Length; slot++)
                _rows[slot] = BuildRow(content, 300f - slot * 180f, slot);

            TMP_Text weeklyTitle = CreateText("WeeklyTitle", content, new Vector2(0f, -280f), 34f, FontStyles.Bold);
            UiTheme.ApplyFont(weeklyTitle, UiTheme.BodyFont);
            weeklyTitle.color = UiTheme.Gold;
            weeklyTitle.characterSpacing = 4f;
            weeklyTitle.text = "WEEKLY";

            _weeklyRow = BuildRow(content, -380f, -1);

            UiWidgets.ClosePill(content, new Vector2(0f, -510f), OnOpenerClicked);
        }

        private Row BuildRow(Transform content, float y, int slot)
        {
            var row = new Row();

            GameObject pillGo = CreateRect($"Row{slot}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 150f));
            pillGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
            var pill = pillGo.AddComponent<Image>();
            UiTheme.ApplySprite(pill, UiTheme.Pill, UiTheme.Slot);
            pill.raycastTarget = false;

            row.Description = CreateText("Desc", pillGo.transform, new Vector2(-60f, 30f), 34f, FontStyles.Bold);
            row.Description.alignment = TextAlignmentOptions.MidlineLeft;
            row.Description.rectTransform.sizeDelta = new Vector2(520f, 60f);
            row.Description.rectTransform.anchoredPosition = new Vector2(-90f, 30f);
            UiTheme.ApplyFont(row.Description, UiTheme.BodyFont);

            row.Progress = CreateText("Progress", pillGo.transform, new Vector2(-90f, -32f), 30f, FontStyles.Normal);
            row.Progress.alignment = TextAlignmentOptions.MidlineLeft;
            row.Progress.rectTransform.sizeDelta = new Vector2(520f, 44f);
            UiTheme.ApplyFont(row.Progress, UiTheme.BodyFont);
            row.Progress.color = UiTheme.TextDim;

            GameObject claimGo = CreateRect("Claim", pillGo.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(170f, 84f));
            claimGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-104f, 0f);
            row.ClaimImage = claimGo.AddComponent<Image>();
            UiTheme.ApplySprite(row.ClaimImage, UiTheme.Pill, UiTheme.Cta);
            row.Claim = claimGo.AddComponent<Button>();
            claimGo.AddComponent<PressableButton>();
            row.Claim.targetGraphic = row.ClaimImage;
            int captured = slot;
            row.Claim.onClick.AddListener(() => OnClaim(captured));
            row.ClaimLabel = CreateText("Label", claimGo.transform, Vector2.zero, 30f, FontStyles.Bold);
            UiTheme.ApplyFont(row.ClaimLabel, UiTheme.ButtonFont);
            Stretch(row.ClaimLabel.rectTransform);

            GameObject rerollGo = CreateRect("Reroll", pillGo.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(64f, 64f));
            rerollGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-24f, 0f);
            var rerollImage = rerollGo.AddComponent<Image>();
            UiTheme.ApplySprite(rerollImage, UiTheme.CircleSprite, UiTheme.OutlineDim);
            row.Reroll = rerollGo.AddComponent<Button>();
            rerollGo.AddComponent<PressableButton>();
            row.Reroll.targetGraphic = rerollImage;
            row.Reroll.onClick.AddListener(() => OnReroll(captured));
            TMP_Text rerollLabel = CreateText("Label", rerollGo.transform, Vector2.zero, 32f, FontStyles.Bold);
            UiTheme.ApplyFont(rerollLabel, UiTheme.ButtonFont);
            rerollLabel.text = "↺";
            if (rerollLabel.font == null || !rerollLabel.font.HasCharacter('↺'))
                rerollLabel.text = "~"; // glyph-safe fallback
            Stretch(rerollLabel.rectTransform);

            return row;
        }

        private void BuildOpener(Transform buttonHost)
        {
            var go = new GameObject("TasksButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(200f, 76f);
            rect.anchoredPosition = new Vector2(24f, -112f); // under DAILY

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Pill, UiTheme.Slot);
            var button = go.GetComponent<Button>();
            go.AddComponent<PressableButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnOpenerClicked);

            TMP_Text label = CreateText("Label", go.transform, Vector2.zero, 34f, FontStyles.Bold);
            UiTheme.ApplyFont(label, UiTheme.ButtonFont);
            label.text = "TASKS";
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
            GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(780f, 100f));
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
