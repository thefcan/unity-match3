using Match3.Game;
using Match3.View;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The settings / pause overlay, runtime-built like the result panel. Two modes:
    ///
    ///  - GAME (a GameManager is wired): the opener button toggles PausedState and the
    ///    panel follows PhaseChanged — Paused shows it, anything else hides it. Extra
    ///    buttons: Resume / Restart / Level Map.
    ///  - MENU (game is null): the opener simply shows/hides the overlay. Close only.
    ///
    /// All toggles write straight to <see cref="Prefs"/>; GameBoot listens to
    /// Prefs.Changed and pushes the values into the live systems, so this class never
    /// talks to AudioManager/Haptics directly (single write-path).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);

        private GameManager _game; // null => menu mode
        private GameObject _root;
        private Image _card;
        private TMP_Text _title;
        private TMP_Text _cloudStatus;
        private System.Action<bool> _setSfxVisual;
        private System.Action<bool> _setHapticsVisual;
        private System.Action<bool> _setColorblindVisual;
        private System.Action<bool> _setNotificationsVisual;

        /// <summary>
        /// Builds the (hidden) overlay under <paramref name="canvas"/> plus its opener
        /// button under <paramref name="buttonHost"/> (the safe-area container).
        /// Pass a null <paramref name="game"/> for menu mode.
        /// </summary>
        public static SettingsPanel Attach(Canvas canvas, Transform buttonHost, GameManager game)
        {
            var host = new GameObject(nameof(SettingsPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            // Deactivate-construct-activate: OnEnable must fire with _game wired.
            host.SetActive(false);
            var panel = host.AddComponent<SettingsPanel>();
            panel._game = game;
            panel.Build();
            panel.BuildOpenerButton(buttonHost);
            panel.Hide();
            host.SetActive(true);
            return panel;
        }

        private void OnEnable()
        {
            if (_game != null)
                _game.PhaseChanged += HandlePhaseChanged;
            Prefs.Changed += RefreshFromPrefs;
            CloudBridge.StatusChanged += RefreshCloudStatus;
            RefreshCloudStatus();
        }

        private void OnDisable()
        {
            if (_game != null)
                _game.PhaseChanged -= HandlePhaseChanged;
            Prefs.Changed -= RefreshFromPrefs;
            CloudBridge.StatusChanged -= RefreshCloudStatus;
        }

        private void RefreshCloudStatus() => SetCloudStatus(CloudBridge.StatusText);

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Paused)
                Show();
            else
                Hide();
        }

        private void Show()
        {
            _card.color = UiTheme.ThemeCard; // the ambience may have drifted since Build
            RefreshFromPrefs();
            _root.SetActive(true);
        }

        private void Hide() => _root.SetActive(false);

        private void RefreshFromPrefs()
        {
            _setSfxVisual?.Invoke(Prefs.SfxOn);
            _setHapticsVisual?.Invoke(Prefs.HapticsOn);
            _setColorblindVisual?.Invoke(Prefs.ColorblindOn);
            _setNotificationsVisual?.Invoke(Prefs.NotificationsOn);
        }

        /// <summary>Faz G fills this in with the real sign-in state.</summary>
        public void SetCloudStatus(string text)
        {
            if (_cloudStatus != null)
                _cloudStatus.text = text;
        }

        // ---- Actions ------------------------------------------------------------------

        private void OnOpenerClicked()
        {
            AudioManager.Play(Sfx.Button);
            if (_game != null)
                _game.TogglePause(); // panel follows the phase
            else if (_root.activeSelf)
                Hide();
            else
                Show();
        }

        private void OnResumeClicked()
        {
            AudioManager.Play(Sfx.Button);
            _game.TogglePause();
        }

        private void OnRestartClicked()
        {
            AudioManager.Play(Sfx.Button);
            _game.Restart(); // leaving PausedState restores timeScale in its Exit()
        }

        private static void OnLevelMapClicked()
        {
            AudioManager.Play(Sfx.Button);
            // Scene load skips PausedState.Exit — restore time/audio by hand.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
        }

        private void OnCloseClicked()
        {
            AudioManager.Play(Sfx.Button);
            Hide();
        }

        private static void OnNotificationsChanged(bool on)
        {
            Prefs.NotificationsOn = on;
            if (on)
                NotificationScheduler.EnsurePermissionThenSchedule(); // contextual permission ask
            else
                NotificationScheduler.Reschedule(); // cancels everything when disabled
        }

        private void OnColorblindChanged(bool on)
        {
            Prefs.ColorblindOn = on;
            // Rebind the live board immediately so the toggle gives instant feedback.
            var board = FindObjectOfType<BoardView>();
            if (board != null)
                board.RefreshTileVisuals();
        }

        // ---- Construction ---------------------------------------------------------------

        private void Build()
        {
            _root = CreateRect("Overlay", transform, Vector2.zero, Vector2.one, Vector2.zero);
            _root.AddComponent<Image>().color = OverlayColor; // also blocks board input

            GameObject cardGo = CreateRect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(860f, 1400f));
            _card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            _title = CreateText("Title", content, new Vector2(0f, 590f), 72f, FontStyles.Bold);
            UiTheme.ApplyFont(_title, UiTheme.TitleFont);
            _title.text = _game != null ? "PAUSED" : "SETTINGS";

            // Music volume — the slider writes Prefs; MusicManager listens.
            BuildRowLabel(content, "Music", 440f);
            BuildVolumeSlider(content, new Vector2(160f, 440f));

            (_setSfxVisual, _) = BuildToggleRow(content, "Sound FX", 310f, Prefs.SfxOn, on => Prefs.SfxOn = on);
            (_setHapticsVisual, _) = BuildToggleRow(content, "Haptics", 180f, Prefs.HapticsOn, on => Prefs.HapticsOn = on);
            (_setColorblindVisual, _) = BuildToggleRow(content, "Colorblind mode", 50f, Prefs.ColorblindOn, OnColorblindChanged);
            (_setNotificationsVisual, _) = BuildToggleRow(content, "Daily reminders", -80f, Prefs.NotificationsOn, OnNotificationsChanged);

            _cloudStatus = CreateText("CloudStatus", content, new Vector2(0f, -200f), 30f, FontStyles.Normal);
            UiTheme.ApplyFont(_cloudStatus, UiTheme.BodyFont);
            _cloudStatus.color = UiTheme.TextDim;
            _cloudStatus.text = "Cloud sync: offline";

            if (_game != null)
            {
                BuildActionButton(content, "Resume", new Vector2(0f, -350f), UiTheme.PillPink, Color.white, UiTheme.TextPrimary, OnResumeClicked);
                BuildActionButton(content, "Restart", new Vector2(0f, -490f), UiTheme.Pill, UiTheme.Slot, UiTheme.TextPrimary, OnRestartClicked);
                BuildActionButton(content, "Level Map", new Vector2(0f, -615f), UiTheme.Pill, UiTheme.Slot, UiTheme.TextDim, OnLevelMapClicked);
            }
            else
            {
                BuildActionButton(content, "Close", new Vector2(0f, -350f), UiTheme.PillPink, Color.white, UiTheme.TextPrimary, OnCloseClicked);
            }
        }

        /// <summary>The opener: a small round "II" (game) / "..." (menu) button, top-right in the safe area.</summary>
        private void BuildOpenerButton(Transform buttonHost)
        {
            var go = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(88f, 88f);
            rect.anchoredPosition = new Vector2(-24f, -24f);

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.CircleSprite, UiTheme.Slot);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnOpenerClicked);

            TMP_Text glyph = CreateText("Glyph", go.transform, Vector2.zero, 40f, FontStyles.Bold);
            UiTheme.ApplyFont(glyph, UiTheme.ButtonFont);
            glyph.text = _game != null ? "II" : "...";
            Stretch(glyph.rectTransform);
        }

        private void BuildRowLabel(Transform parent, string label, float y)
        {
            TMP_Text text = CreateText(label, parent, new Vector2(0f, y), 44f, FontStyles.Bold);
            UiTheme.ApplyFont(text, UiTheme.BodyFont);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(700f, 80f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private (System.Action<bool> setVisual, Button button) BuildToggleRow(
            Transform parent, string label, float y, bool initial, System.Action<bool> onChanged)
        {
            BuildRowLabel(parent, label, y);

            var go = new GameObject("Switch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(128f, 60f);
            rect.anchoredPosition = new Vector2(270f, y);

            var back = go.GetComponent<Image>();
            UiTheme.ApplySprite(back, UiTheme.Pill, UiTheme.Slot);

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(Image));
            knobGo.transform.SetParent(go.transform, false);
            var knobRect = (RectTransform)knobGo.transform;
            knobRect.sizeDelta = new Vector2(46f, 46f);
            var knob = knobGo.GetComponent<Image>();
            UiTheme.ApplySprite(knob, UiTheme.CircleSprite, Color.white);
            knob.raycastTarget = false;

            bool state = initial;
            void SetVisual(bool on)
            {
                state = on;
                back.color = on ? UiTheme.Cta : UiTheme.Slot;
                knobRect.anchoredPosition = new Vector2(on ? 31f : -31f, 0f);
            }
            SetVisual(initial);

            var button = go.GetComponent<Button>();
            button.targetGraphic = back;
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Button);
                SetVisual(!state);
                onChanged(state);
            });

            return (SetVisual, button);
        }

        private void BuildVolumeSlider(Transform parent, Vector2 position)
        {
            var go = new GameObject("MusicSlider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 44f);
            rect.anchoredPosition = position;

            var backGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backGo.transform.SetParent(go.transform, false);
            Stretch((RectTransform)backGo.transform);
            var back = backGo.GetComponent<Image>();
            UiTheme.ApplySprite(back, UiTheme.Pill, UiTheme.Slot);
            back.raycastTarget = false;

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(8f, 8f);
            fillAreaRect.offsetMax = new Vector2(-8f, -8f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            UiTheme.ApplySprite(fill, UiTheme.Pill, UiTheme.Cta);
            fill.raycastTarget = false;

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(24f, 0f);
            handleAreaRect.offsetMax = new Vector2(-24f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.sizeDelta = new Vector2(52f, 52f);
            var handle = handleGo.GetComponent<Image>();
            UiTheme.ApplySprite(handle, UiTheme.CircleSprite, Color.white);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Prefs.MusicVolume;
            slider.onValueChanged.AddListener(v => Prefs.MusicVolume = v);
        }

        private void BuildActionButton(Transform parent, string label, Vector2 position,
                                       Sprite sprite, Color spriteColor, Color labelColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreateRect(label + "Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 130f));
            go.GetComponent<RectTransform>().anchoredPosition = position;
            var image = go.AddComponent<Image>();
            UiTheme.ApplySprite(image, sprite, spriteColor);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = CreateText("Label", go.transform, Vector2.zero, 48f, FontStyles.Bold);
            UiTheme.ApplyFont(text, UiTheme.ButtonFont);
            text.color = labelColor;
            text.text = label;
            Stretch(text.rectTransform);
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
            GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 100f));
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
