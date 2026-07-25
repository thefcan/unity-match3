using Match3.Core;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The in-level booster shelf (Stitch design: right edge, vertically centred):
    /// three pills — SMASH (hammer, tap a cell), SWAP (free switch, swipe a pair)
    /// and MIX (instant shuffle) — each with a live count badge. Runtime-built like
    /// every other panel. Boosters never consume a move, and the whole tray hides
    /// in time attack so leaderboard runs stay pure.
    /// </summary>
    public sealed class BoosterTray : MonoBehaviour
    {
        private static readonly (BoosterKind kind, string label)[] Slots =
        {
            (BoosterKind.Hammer, "SMASH"),
            (BoosterKind.FreeSwap, "SWAP"),
            (BoosterKind.Shuffle, "MIX"),
        };

        private GameManager _game;
        private readonly Image[] _pills = new Image[3];
        private readonly TMP_Text[] _counts = new TMP_Text[3];

        public static BoosterTray Attach(Transform safe, GameManager game)
        {
            var host = new GameObject(nameof(BoosterTray), typeof(RectTransform));
            host.transform.SetParent(safe, false);
            var rect = (RectTransform)host.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 3 * 104f + 2 * 14f);
            rect.anchoredPosition = new Vector2(-16f, -60f); // below the board's midline

            host.SetActive(false);
            var tray = host.AddComponent<BoosterTray>();
            tray._game = game;
            tray.Build();
            host.SetActive(true);
            return tray;
        }

        private void OnEnable()
        {
            if (_game == null) return;
            _game.BoostersChanged += Refresh;
            _game.PhaseChanged += HandlePhaseChanged;
            _game.LevelChanged += HandleLevelChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_game == null) return;
            _game.BoostersChanged -= Refresh;
            _game.PhaseChanged -= HandlePhaseChanged;
            _game.LevelChanged -= HandleLevelChanged;
        }

        /// <summary>Mode is only known after BuildNewGame — LevelChanged is its "done" signal.</summary>
        private void HandleLevelChanged(int level)
        {
            bool visible = _game.Mode == GameMode.Moves;
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(visible);
            Refresh();
        }

        private void HandlePhaseChanged(GamePhase phase) => Refresh();

        private void Refresh()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (_pills[i] == null)
                    continue;
                bool armed = _game.ArmedBooster == Slots[i].kind;
                _pills[i].color = armed ? UiTheme.Cta : UiTheme.ThemeSlot;
                if (_counts[i] != null)
                    _counts[i].text = MetaService.BoosterCount(Slots[i].kind).ToString();
            }
        }

        private void Build()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                var pillGo = new GameObject(Slots[i].label, typeof(RectTransform), typeof(Image), typeof(Button));
                pillGo.transform.SetParent(transform, false);
                var pillRect = (RectTransform)pillGo.transform;
                pillRect.anchorMin = pillRect.anchorMax = new Vector2(0.5f, 1f);
                pillRect.pivot = new Vector2(0.5f, 1f);
                pillRect.sizeDelta = new Vector2(150f, 104f);
                pillRect.anchoredPosition = new Vector2(0f, -i * 118f);

                var pill = pillGo.GetComponent<Image>();
                UiTheme.ApplySprite(pill, UiTheme.Pill, UiTheme.ThemeSlot);
                _pills[i] = pill;

                var button = pillGo.GetComponent<Button>();
                button.targetGraphic = pill;
                button.onClick.AddListener(() =>
                {
                    AudioManager.Play(Sfx.Button);
                    _game.ArmBooster(Slots[index].kind);
                });

                var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(pillGo.transform, false);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 10f);
                labelRect.offsetMax = new Vector2(-8f, -34f);
                label.fontSize = 26f;
                label.fontStyle = FontStyles.Bold;
                label.characterSpacing = 2f;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.text = Slots[i].label;
                UiTheme.ApplyFont(label, UiTheme.ButtonFont);

                var badgeGo = new GameObject("Count", typeof(RectTransform), typeof(Image));
                badgeGo.transform.SetParent(pillGo.transform, false);
                var badgeRect = (RectTransform)badgeGo.transform;
                badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 1f);
                badgeRect.sizeDelta = new Vector2(44f, 44f);
                badgeRect.anchoredPosition = new Vector2(0f, -22f);
                var badge = badgeGo.GetComponent<Image>();
                UiTheme.ApplySprite(badge, UiTheme.CircleSprite, UiTheme.Gold);
                badge.raycastTarget = false;

                var count = new GameObject("Value", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                count.transform.SetParent(badgeGo.transform, false);
                var countRect = count.rectTransform;
                countRect.anchorMin = Vector2.zero;
                countRect.anchorMax = Vector2.one;
                countRect.offsetMin = Vector2.zero;
                countRect.offsetMax = Vector2.zero;
                count.fontSize = 26f;
                count.fontStyle = FontStyles.Bold;
                count.alignment = TextAlignmentOptions.Center;
                count.color = new Color(0.24f, 0.16f, 0.02f); // dark on gold
                count.raycastTarget = false;
                UiTheme.ApplyFont(count, UiTheme.ButtonFont);
                _counts[i] = count;
            }
        }
    }
}
