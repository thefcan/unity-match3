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
        private float _contentShift;  // menu's shorter card moves every row with its top edge
        private GameObject _root;
        private Image _card;
        private TMP_Text _title;
        private TMP_Text _cloudStatus;
        private System.Action<bool> _setSfxVisual;
        private System.Action<bool> _setHapticsVisual;
        private System.Action<bool> _setColorblindVisual;
        private System.Action<bool> _setNotificationsVisual;
        private System.Action<bool> _setRelaxedVisual;
        private System.Action<bool> _setReducedMotionVisual;
        private System.Action<bool> _setBigTextVisual;

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
            UiTween.OpenPanel(this, _root, _card.transform);
        }

        private void Hide() => UiTween.ClosePanel(this, _root);

        private void RefreshFromPrefs()
        {
            _setSfxVisual?.Invoke(Prefs.SfxOn);
            _setHapticsVisual?.Invoke(Prefs.HapticsOn);
            _setColorblindVisual?.Invoke(Prefs.ColorblindOn);
            _setNotificationsVisual?.Invoke(Prefs.NotificationsOn);
            _setRelaxedVisual?.Invoke(Prefs.RelaxedOn);
            _setReducedMotionVisual?.Invoke(Prefs.ReducedMotionOn);
            _setBigTextVisual?.Invoke(Prefs.BigTextOn);
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
            // Scene load skips PausedState.Exit — restore time/audio by hand,
            // BEFORE the fade so the curtain never runs on frozen time.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ScreenFader.LoadScene("MainMenu");
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

            // Tapping the dim outside the card closes the panel (the card's own
            // raycast blocks pass-through - the album ceremony's idiom).
            var dismiss = _root.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(OnOpenerClicked);

            // The menu card carries one button instead of three, so it is shorter —
            // and everything inside shifts with the top edge, which is why every
            // y below goes through Y(). (A flat list of eight unlabelled rows was
            // the readability problem; the sections are the fix.)
            bool inGame = _game != null;
            float cardHeight = inGame ? 1620f : 1400f;
            _contentShift = (cardHeight - 1620f) * 0.5f;

            GameObject cardGo = CreateRect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(860f, cardHeight));
            _card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            _title = CreateText("Title", content, new Vector2(0f, Y(700f)), 72f, FontStyles.Bold);
            UiTheme.ApplyFont(_title, UiTheme.TitleFont);
            _title.text = inGame ? "PAUSED" : "SETTINGS";

            // ---- Sound ----------------------------------------------------------
            BuildSectionHeader(content, "SOUND", 610f);
            // Music volume — the slider writes Prefs; MusicManager listens.
            BuildRowLabel(content, "Music", Y(545f));
            BuildVolumeSlider(content, new Vector2(160f, Y(545f)));
            (_setSfxVisual, _) = BuildToggleRow(content, "Sound FX", Y(445f), Prefs.SfxOn, on => Prefs.SfxOn = on);
            (_setHapticsVisual, _) = BuildToggleRow(content, "Haptics", Y(345f), Prefs.HapticsOn, on => Prefs.HapticsOn = on);

            // ---- Gameplay -------------------------------------------------------
            BuildSectionHeader(content, "GAMEPLAY", 255f);
            (_setRelaxedVisual, _) = BuildToggleRow(content, "Relaxed mode", Y(190f), Prefs.RelaxedOn, on => Prefs.RelaxedOn = on);
            (_setNotificationsVisual, _) = BuildToggleRow(content, "Daily reminders", Y(90f), Prefs.NotificationsOn, OnNotificationsChanged);

            // ---- Accessibility --------------------------------------------------
            BuildSectionHeader(content, "ACCESSIBILITY", 0f);
            (_setColorblindVisual, _) = BuildToggleRow(content, "Colorblind mode", Y(-65f), Prefs.ColorblindOn, OnColorblindChanged);
            (_setReducedMotionVisual, _) = BuildToggleRow(content, "Reduced motion", Y(-165f), Prefs.ReducedMotionOn, on => Prefs.ReducedMotionOn = on);
            (_setBigTextVisual, _) = BuildToggleRow(content, "Big text", Y(-265f), Prefs.BigTextOn, on => Prefs.BigTextOn = on);

            _cloudStatus = CreateText("CloudStatus", content, new Vector2(0f, Y(-350f)), 30f, FontStyles.Normal);
            UiTheme.ApplyFont(_cloudStatus, UiTheme.BodyFont);
            _cloudStatus.color = UiTheme.TextDim;
            _cloudStatus.text = "Cloud sync: offline";

            if (inGame)
            {
                BuildActionButton(content, "Resume", new Vector2(0f, Y(-450f)), UiTheme.PillPink, Color.white, UiTheme.TextPrimary, OnResumeClicked);
                BuildActionButton(content, "Restart", new Vector2(0f, Y(-580f)), UiTheme.Pill, UiTheme.Slot, UiTheme.TextPrimary, OnRestartClicked);
                BuildActionButton(content, "Level Map", new Vector2(0f, Y(-710f)), UiTheme.Pill, UiTheme.Slot, UiTheme.TextDim, OnLevelMapClicked);
            }
            else
            {
                BuildActionButton(content, "Close", new Vector2(0f, Y(-450f)), UiTheme.PillPink, Color.white, UiTheme.TextPrimary, OnCloseClicked);
            }
        }

        /// <summary>Shifts a card-centred y for the current card height (see Build).</summary>
        private float Y(float y) => y + _contentShift;

        /// <summary>
        /// A small gold caps label with a hairline rule beside it — the same
        /// caption voice as the HUD's "MOVES". Takes the UNSHIFTED y (it applies
        /// the shift itself) so the layout above reads as one column of numbers.
        /// </summary>
        private void BuildSectionHeader(Transform parent, string caption, float y)
        {
            TMP_Text text = CreateText(caption + "Header", parent, new Vector2(-240f, Y(y)), 26f, FontStyles.Bold);
            UiTheme.ApplyFont(text, UiTheme.BodyFont);
            text.text = caption; // CreateText only names the object (the BuildRowLabel rule)
            text.color = UiTheme.Gold;
            text.characterSpacing = 8f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.rectTransform.sizeDelta = new Vector2(400f, 40f);

            GameObject ruleGo = CreateRect(caption + "Rule", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 2f));
            ruleGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, Y(y) - 26f);
            var rule = ruleGo.AddComponent<Image>();
            rule.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.25f);
            rule.raycastTarget = false;
        }

        /// <summary>
        /// The opener: a small round "II" (game) / "..." (menu) button. In game mode
        /// <paramref name="buttonHost"/> is the HUD top bar and the button docks inside
        /// its right edge, centred on the stat row; in menu mode it sits top-right of
        /// the safe area.
        /// </summary>
        private void BuildOpenerButton(Transform buttonHost)
        {
            var go = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            if (_game != null)
            {
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(96f, 96f);
                rect.anchoredPosition = new Vector2(-26f, -104f);
            }
            else
            {
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(88f, 88f);
                rect.anchoredPosition = new Vector2(-24f, -24f);
            }

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.CircleSprite, UiTheme.Slot);

            var button = go.GetComponent<Button>();
            go.AddComponent<PressableButton>();
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
            text.text = label; // CreateText only names the object — the visible string is set here
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
            Coroutine glide = null;
            void SetVisual(bool on)
            {
                state = on;
                back.color = on ? UiTheme.Cta : UiTheme.Slot;
                float targetX = on ? 31f : -31f;
                if (glide != null)
                    StopCoroutine(glide);
                // Glide only when the panel is actually on screen (build-time calls
                // and reduced motion snap). Unscaled: this panel lives at timeScale 0.
                if (isActiveAndEnabled && _root != null && _root.activeSelf && !Prefs.ReducedMotionOn)
                    glide = StartCoroutine(GlideKnob(knobRect, targetX));
                else
                    knobRect.anchoredPosition = new Vector2(targetX, 0f);
            }
            SetVisual(initial);

            void Toggle()
            {
                AudioManager.Play(Sfx.Button);
                SetVisual(!state);
                onChanged(state);
            }

            var button = go.GetComponent<Button>();
            go.AddComponent<PressableButton>();
            button.targetGraphic = back;
            button.onClick.AddListener(Toggle);

            // The whole row toggles, not just the 128px pill — tapping the label
            // of an accessibility setting must count. Invisible, slotted UNDER
            // the switch so the pill keeps its own press feedback.
            var rowGo = new GameObject("RowTap", typeof(RectTransform), typeof(Image), typeof(Button));
            rowGo.transform.SetParent(parent, false);
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(760f, 100f);
            rowRect.anchoredPosition = new Vector2(0f, y);
            rowGo.GetComponent<Image>().color = Color.clear;
            var rowButton = rowGo.GetComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            rowButton.onClick.AddListener(Toggle);
            rowGo.transform.SetSiblingIndex(go.transform.GetSiblingIndex());

            return (SetVisual, button);
        }

        private static System.Collections.IEnumerator GlideKnob(RectTransform knob, float toX)
        {
            float fromX = knob.anchoredPosition.x;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f)
            {
                knob.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, Mathf.SmoothStep(0f, 1f, t)), 0f);
                yield return null;
            }
            knob.anchoredPosition = new Vector2(toX, 0f);
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
            // The Slider stretches the handle's cross axis to the 44px track, and
            // sizeDelta ADDS to that — 8 yields a true 52x52 circle, not an ellipse.
            handleRect.sizeDelta = new Vector2(52f, 8f);
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
            go.AddComponent<PressableButton>();
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
