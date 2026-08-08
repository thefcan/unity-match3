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
        private readonly Button[] _buttons = new Button[3];
        private readonly Coroutine[] _pulses = new Coroutine[3];

        public static BoosterTray Attach(Transform safe, GameManager game)
        {
            var host = new GameObject(nameof(BoosterTray), typeof(RectTransform));
            host.transform.SetParent(safe, false);
            var rect = (RectTransform)host.transform;
            // Bottom-centre ROW: a side-docked tray overlapped the board's right
            // column on narrow screens (seen in the standalone test build).
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(3 * 200f + 2 * 20f, 110f);
            rect.anchoredPosition = new Vector2(0f, 28f);

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
            // Coroutines die with the object — keep the pulse bookkeeping honest
            // and leave every pill at rest scale for the next enable.
            for (int i = 0; i < _pulses.Length; i++)
            {
                _pulses[i] = null;
                if (_pills[i] != null)
                    _pills[i].transform.localScale = Vector3.one;
            }

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
                int count = MetaService.BoosterCount(Slots[i].kind);
                // A spent booster reads as spent: dimmed pill, inert button —
                // no full-colour pill that only plays a click sound.
                Color pill = armed ? UiTheme.Cta : UiTheme.ThemeSlot;
                pill.a = count > 0 ? 1f : 0.4f;
                _pills[i].color = pill;
                if (_buttons[i] != null)
                    _buttons[i].interactable = count > 0;
                if (_counts[i] != null)
                    _counts[i].text = count.ToString();

                // The armed booster breathes, so "which one is loaded?" is
                // readable from across the board and not just from its tint.
                if (armed && _pulses[i] == null && !Prefs.ReducedMotionOn && isActiveAndEnabled)
                    _pulses[i] = StartCoroutine(PulsePill(_pills[i].transform, i));
                else if (!armed && _pulses[i] != null)
                {
                    StopCoroutine(_pulses[i]);
                    _pulses[i] = null;
                    _pills[i].transform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>Armed-pill breathing: 1.0 → 1.05 sine, unscaled (a booster can be armed while paused).</summary>
        private System.Collections.IEnumerator PulsePill(Transform pill, int index)
        {
            float phase = 0f;
            while (true)
            {
                phase += Time.unscaledDeltaTime * 3.2f;
                pill.localScale = Vector3.one * (1f + 0.05f * (0.5f + 0.5f * Mathf.Sin(phase)));
                yield return null;
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
                pillRect.anchorMin = pillRect.anchorMax = new Vector2(0f, 0.5f);
                pillRect.pivot = new Vector2(0f, 0.5f);
                pillRect.sizeDelta = new Vector2(200f, 104f);
                pillRect.anchoredPosition = new Vector2(i * 220f, 0f);

                var pill = pillGo.GetComponent<Image>();
                UiTheme.ApplySprite(pill, UiTheme.Pill, UiTheme.ThemeSlot);
                _pills[i] = pill;

                var button = pillGo.GetComponent<Button>();
                pillGo.AddComponent<PressableButton>();
                _buttons[i] = button;
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
