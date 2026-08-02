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
    /// The Candy Calendar panel (menu only): midweek objective events with three
    /// claimable tiers, the weekend bot-race standings, the permanent trophy
    /// shelf and the one-shot "banked while you were away" toast. Every rule
    /// lives in Core behind <see cref="EventService"/>; this panel only renders
    /// state and forwards clicks. The opener hides itself on Mondays — the off
    /// day IS the anticipation beat.
    /// </summary>
    public sealed class EventsPanel : MonoBehaviour
    {
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color SilverTint = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color BronzeTint = new Color(0.80f, 0.55f, 0.30f);

        private sealed class TierRow
        {
            public GameObject Root;
            public TMP_Text Description;
            public TMP_Text Reward;
            public Button Claim;
            public TMP_Text ClaimLabel;
            public Image ClaimImage;
        }

        private sealed class RaceRow
        {
            public Image Pill;
            public TMP_Text Rank;
            public TMP_Text Name;
            public TMP_Text Progress;
        }

        private GameObject _root;
        private Image _card;
        private TMP_Text _title;
        private TMP_Text _countdown;
        private TMP_Text _brief;
        private TMP_Text _toast;
        private readonly TMP_Text[] _trophyCounts = new TMP_Text[3];

        private GameObject _objectiveGroup;
        private RectTransform _barFill;
        private TMP_Text _barLabel;
        private readonly TierRow[] _tiers = new TierRow[EventCalendar.TierCount];

        private GameObject _raceGroup;
        private readonly RaceRow[] _raceRows = new RaceRow[EventCalendar.BotCount + 1];
        private TMP_Text _racePreview;
        private GameObject _raceClaimGo;

        private GameObject _opener;
        private GameObject _badge;

        public static EventsPanel Attach(Canvas canvas, Transform buttonHost)
        {
            var host = new GameObject(nameof(EventsPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            host.SetActive(false);
            var panel = host.AddComponent<EventsPanel>();
            panel.Build();
            panel.BuildOpener(buttonHost);
            panel.Hide();
            host.SetActive(true);
            return panel;
        }

        private void Show()
        {
            _card.color = UiTheme.ThemeCard;
            // The banked toast is consumed HERE (not in Refresh) so a claim's
            // refresh two seconds later doesn't wipe the line mid-read.
            _toast.text = EventService.TryTakeBankedToast(out ChestReward banked, out int trophy, out int rescues, out int packs)
                ? ComposeToast(banked, trophy, rescues, packs)
                : string.Empty;
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

        private void Refresh()
        {
            EventService.EnsureFresh();
            MetaState meta = MetaService.Current;

            _trophyCounts[0].text = "×" + meta.TrophyGold;
            _trophyCounts[1].text = "×" + meta.TrophySilver;
            _trophyCounts[2].text = "×" + meta.TrophyBronze;

            bool active = EventService.IsWindowActive;
            EventDef def = EventService.CurrentDef;
            bool isRace = def.Kind == EventKind.Race;

            _title.text = active ? EventService.Title : "CANDY CALENDAR";
            int daysLeft = EventService.DaysLeftToday;
            _countdown.text = !active
                ? "NEXT EVENT TOMORROW"
                : (daysLeft <= 1 ? "LAST DAY!" : $"ENDS IN {daysLeft} DAYS");
            _brief.text = active ? EventService.Describe(def) : string.Empty;

            _objectiveGroup.SetActive(active && !isRace);
            _raceGroup.SetActive(active && isRace);
            if (active && !isRace)
                RefreshObjective(def, meta);
            else if (active)
                RefreshRace(meta);

            RefreshBadge();
        }

        private void RefreshObjective(EventDef def, MetaState meta)
        {
            int progress = meta.EventProgress;
            float fraction = Mathf.Clamp01(progress / (float)def.Tier3);
            if (fraction > 0f)
                fraction = Mathf.Max(fraction, 0.05f); // a sliced pill needs a sliver to read as round
            _barFill.anchorMax = new Vector2(fraction, 1f);
            _barLabel.text = $"{progress}/{def.Tier3}";

            for (int tier = 0; tier < _tiers.Length; tier++)
            {
                TierRow row = _tiers[tier];
                row.Description.text = $"TIER {tier + 1} — {def.TierTarget(tier)}";
                row.Reward.text = RewardLine(EventRules.TierReward(tier))
                    + (tier == EventCalendar.TierCount - 1 ? "  +1 PACK" : string.Empty);

                bool done = progress >= def.TierTarget(tier);
                bool claimed = meta.EventTierClaimed[tier];
                row.ClaimLabel.text = claimed ? "DONE" : "CLAIM";
                row.Claim.interactable = done && !claimed;
                row.ClaimImage.color = claimed ? UiTheme.Slot : (done ? UiTheme.Cta : UiTheme.Slot);
                row.Description.color = done ? UiTheme.Gold : Color.white;
            }
        }

        private void RefreshRace(MetaState meta)
        {
            int ticks = meta.EventProgress;

            var entries = new List<(int progress, bool isPlayer, string name)>
            {
                (ticks, true, "YOU"),
            };
            for (int bot = 0; bot < EventCalendar.BotCount; bot++)
                entries.Add((EventService.BotProgressNow(bot), false, EventService.BotName(bot)));
            // Progress descending; the player wins every tie (the Core rule, mirrored visually).
            entries.Sort((a, b) => a.progress != b.progress
                ? b.progress.CompareTo(a.progress)
                : b.isPlayer.CompareTo(a.isPlayer));

            for (int i = 0; i < _raceRows.Length; i++)
            {
                RaceRow row = _raceRows[i];
                (int progress, bool isPlayer, string name) = entries[i];
                row.Rank.text = (i + 1).ToString();
                row.Name.text = name;
                row.Progress.text = $"{Mathf.Min(progress, EventCalendar.RaceTarget)}/{EventCalendar.RaceTarget}";
                row.Name.color = isPlayer ? UiTheme.Gold : Color.white;
                row.Rank.color = isPlayer ? UiTheme.Gold : UiTheme.TextDim;
                row.Pill.color = isPlayer
                    ? new Color(UiTheme.Cta.r, UiTheme.Cta.g, UiTheme.Cta.b, 0.35f)
                    : UiTheme.Slot;
            }

            bool finished = ticks >= EventCalendar.RaceTarget;
            if (meta.EventRaceClaimed)
                _racePreview.text = $"FINISHED — {Ordinal(EventService.RacePlacementNow)} PLACE, REWARDS COLLECTED";
            else if (finished)
                _racePreview.text = $"{Ordinal(EventService.RacePlacementNow)} PLACE — COLLECT YOUR PRIZE!";
            else
                _racePreview.text = "1ST: +3 SMASH +3 SWAP +2 MIX +1 SHIELD +1 SAVE";
            _raceClaimGo.SetActive(finished && !meta.EventRaceClaimed);
        }

        private void RefreshBadge()
        {
            if (_badge != null)
                _badge.SetActive(EventService.HasClaimable);
        }

        private void OnClaimTier(int tier)
        {
            AudioManager.Play(Sfx.Button);
            if (EventService.ClaimTier(tier))
            {
                AudioManager.Play(Sfx.Win);
                AudioManager.Play(Sfx.Pop, 1f + 0.18f * tier);
                Haptics.Medium();
                if (!Prefs.ReducedMotionOn)
                    StartCoroutine(Pop(_tiers[tier].Root.transform));
            }
            Refresh();
        }

        private void OnClaimRace()
        {
            AudioManager.Play(Sfx.Button);
            if (EventService.ClaimRace())
            {
                AudioManager.Play(Sfx.Win);
                Haptics.Medium();
                if (!Prefs.ReducedMotionOn)
                    StartCoroutine(Pop(_raceRows[0].Pill.transform.parent));
            }
            Refresh();
        }

        private static IEnumerator Pop(Transform target)
        {
            // The PopStars beat: up fast, settle — no particles in menu space.
            float elapsed = 0f;
            const float duration = 0.22f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                float scale = k < 0.5f ? Mathf.Lerp(1f, 1.18f, k * 2f) : Mathf.Lerp(1.18f, 1f, (k - 0.5f) * 2f);
                target.localScale = Vector3.one * scale;
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private static string RewardLine(ChestReward reward)
        {
            string line = string.Empty;
            if (reward.Hammers > 0) line += $"+{reward.Hammers} SMASH  ";
            if (reward.FreeSwaps > 0) line += $"+{reward.FreeSwaps} SWAP  ";
            if (reward.Shuffles > 0) line += $"+{reward.Shuffles} MIX  ";
            if (reward.StreakShields > 0) line += $"+{reward.StreakShields} SHIELD";
            return line.TrimEnd();
        }

        private static string ComposeToast(ChestReward reward, int trophy, int rescues, int packs)
        {
            string line = "WHILE YOU WERE AWAY:  " + RewardLine(reward);
            if (rescues > 0) line += $"  +{rescues} SAVE";
            if (packs > 0) line += $"  +{packs} PACK";
            if (trophy == 3) line += "  +GOLD TROPHY";
            else if (trophy == 2) line += "  +SILVER TROPHY";
            else if (trophy == 1) line += "  +BRONZE TROPHY";
            return line;
        }

        private static string Ordinal(int rank)
        {
            switch (rank)
            {
                case 1: return "1ST";
                case 2: return "2ND";
                case 3: return "3RD";
                default: return rank + "TH";
            }
        }

        // ---- Construction ---------------------------------------------------------------

        private void Build()
        {
            _root = CreateRect("Overlay", transform, Vector2.zero, Vector2.one, Vector2.zero);
            _root.AddComponent<Image>().color = OverlayColor;

            GameObject cardGo = CreateRect("Card", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(880f, 1180f));
            _card = cardGo.AddComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            _title = CreateText("Title", content, new Vector2(0f, 500f), 64f, FontStyles.Bold);
            UiTheme.ApplyFont(_title, UiTheme.TitleFont);

            _countdown = CreateText("Countdown", content, new Vector2(0f, 428f), 30f, FontStyles.Bold);
            UiTheme.ApplyFont(_countdown, UiTheme.BodyFont);
            _countdown.color = UiTheme.Gold;
            _countdown.characterSpacing = 4f;

            _brief = CreateText("Brief", content, new Vector2(0f, 382f), 28f, FontStyles.Normal);
            UiTheme.ApplyFont(_brief, UiTheme.BodyFont);
            _brief.color = UiTheme.TextDim;

            _toast = CreateText("BankedToast", content, new Vector2(0f, 340f), 24f, FontStyles.Bold);
            UiTheme.ApplyFont(_toast, UiTheme.BodyFont);
            _toast.color = UiTheme.Gold;

            BuildTrophyShelf(content, 292f);
            BuildObjectiveGroup(content);
            BuildRaceGroup(content);

            GameObject closeGo = CreateRect("CloseButton", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 110f));
            closeGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -530f);
            var closeImage = closeGo.AddComponent<Image>();
            UiTheme.ApplySprite(closeImage, UiTheme.Pill, UiTheme.Slot);
            var closeButton = closeGo.AddComponent<Button>();
            closeGo.AddComponent<PressableButton>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(OnOpenerClicked);
            TMP_Text closeLabel = CreateText("Label", closeGo.transform, Vector2.zero, 42f, FontStyles.Normal);
            UiTheme.ApplyFont(closeLabel, UiTheme.ButtonFont);
            closeLabel.color = UiTheme.TextDim;
            closeLabel.text = "Close";
            Stretch(closeLabel.rectTransform);
        }

        private void BuildTrophyShelf(Transform content, float y)
        {
            Color[] tints = { UiTheme.Gold, SilverTint, BronzeTint };
            for (int i = 0; i < 3; i++)
            {
                float x = -170f + i * 170f;
                GameObject cup = CreateRect($"Trophy{i}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f, 44f));
                cup.GetComponent<RectTransform>().anchoredPosition = new Vector2(x - 28f, y);
                var cupImage = cup.AddComponent<Image>();
                // Until Match3 > Generate > UI Sprites bakes the cup, the star pip stands in.
                Sprite sprite = UiTheme.TrophySprite != null ? UiTheme.TrophySprite : UiTheme.StarSprite;
                UiTheme.ApplySprite(cupImage, sprite, tints[i]);
                cupImage.raycastTarget = false;

                TMP_Text count = CreateText($"TrophyCount{i}", content, new Vector2(x + 26f, y), 30f, FontStyles.Bold);
                count.rectTransform.sizeDelta = new Vector2(90f, 44f);
                count.alignment = TextAlignmentOptions.MidlineLeft;
                UiTheme.ApplyFont(count, UiTheme.ButtonFont);
                count.color = tints[i];
                _trophyCounts[i] = count;
            }
        }

        private void BuildObjectiveGroup(Transform content)
        {
            _objectiveGroup = CreateRect("Objective", content, Vector2.zero, Vector2.one, Vector2.zero);
            Transform group = _objectiveGroup.transform;

            GameObject trackGo = CreateRect("Track", group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 34f));
            trackGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 240f);
            var track = trackGo.AddComponent<Image>();
            UiTheme.ApplySprite(track, UiTheme.Pill, UiTheme.Slot);
            track.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            _barFill = (RectTransform)fillGo.transform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;
            var fillImage = fillGo.GetComponent<Image>();
            UiTheme.ApplySprite(fillImage, UiTheme.Pill, UiTheme.Gold);
            fillImage.raycastTarget = false;

            _barLabel = CreateText("BarLabel", group, new Vector2(0f, 196f), 30f, FontStyles.Bold);
            UiTheme.ApplyFont(_barLabel, UiTheme.BodyFont);
            _barLabel.color = UiTheme.TextDim;

            for (int tier = 0; tier < _tiers.Length; tier++)
                _tiers[tier] = BuildTierRow(group, 96f - tier * 180f, tier);
        }

        private TierRow BuildTierRow(Transform content, float y, int tier)
        {
            var row = new TierRow();

            row.Root = CreateRect($"Tier{tier}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 150f));
            row.Root.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
            var pill = row.Root.AddComponent<Image>();
            UiTheme.ApplySprite(pill, UiTheme.Pill, UiTheme.Slot);
            pill.raycastTarget = false;

            row.Description = CreateText("Desc", row.Root.transform, new Vector2(-90f, 30f), 34f, FontStyles.Bold);
            row.Description.alignment = TextAlignmentOptions.MidlineLeft;
            row.Description.rectTransform.sizeDelta = new Vector2(520f, 60f);
            UiTheme.ApplyFont(row.Description, UiTheme.BodyFont);

            row.Reward = CreateText("Reward", row.Root.transform, new Vector2(-90f, -32f), 26f, FontStyles.Normal);
            row.Reward.alignment = TextAlignmentOptions.MidlineLeft;
            row.Reward.rectTransform.sizeDelta = new Vector2(520f, 44f);
            UiTheme.ApplyFont(row.Reward, UiTheme.BodyFont);
            row.Reward.color = UiTheme.TextDim;

            GameObject claimGo = CreateRect("Claim", row.Root.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(170f, 84f));
            claimGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-24f, 0f);
            row.ClaimImage = claimGo.AddComponent<Image>();
            UiTheme.ApplySprite(row.ClaimImage, UiTheme.Pill, UiTheme.Cta);
            row.Claim = claimGo.AddComponent<Button>();
            claimGo.AddComponent<PressableButton>();
            row.Claim.targetGraphic = row.ClaimImage;
            int captured = tier;
            row.Claim.onClick.AddListener(() => OnClaimTier(captured));
            row.ClaimLabel = CreateText("Label", claimGo.transform, Vector2.zero, 30f, FontStyles.Bold);
            UiTheme.ApplyFont(row.ClaimLabel, UiTheme.ButtonFont);
            Stretch(row.ClaimLabel.rectTransform);

            return row;
        }

        private void BuildRaceGroup(Transform content)
        {
            _raceGroup = CreateRect("Race", content, Vector2.zero, Vector2.one, Vector2.zero);
            Transform group = _raceGroup.transform;

            for (int i = 0; i < _raceRows.Length; i++)
                _raceRows[i] = BuildRaceRow(group, 244f - i * 112f);

            _racePreview = CreateText("RewardPreview", group, new Vector2(0f, -448f), 26f, FontStyles.Bold);
            UiTheme.ApplyFont(_racePreview, UiTheme.BodyFont);
            _racePreview.color = UiTheme.TextDim;

            _raceClaimGo = CreateRect("RaceClaim", group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 96f));
            _raceClaimGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -382f);
            var claimImage = _raceClaimGo.AddComponent<Image>();
            UiTheme.ApplySprite(claimImage, UiTheme.PillPink, Color.white);
            var claimButton = _raceClaimGo.AddComponent<Button>();
            _raceClaimGo.AddComponent<PressableButton>();
            claimButton.targetGraphic = claimImage;
            claimButton.onClick.AddListener(OnClaimRace);
            TMP_Text claimLabel = CreateText("Label", _raceClaimGo.transform, Vector2.zero, 36f, FontStyles.Bold);
            UiTheme.ApplyFont(claimLabel, UiTheme.ButtonFont);
            claimLabel.text = "CLAIM";
            Stretch(claimLabel.rectTransform);
        }

        private RaceRow BuildRaceRow(Transform content, float y)
        {
            var row = new RaceRow();

            GameObject pillGo = CreateRect("RaceRow", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 100f));
            pillGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
            row.Pill = pillGo.AddComponent<Image>();
            UiTheme.ApplySprite(row.Pill, UiTheme.Pill, UiTheme.Slot);
            row.Pill.raycastTarget = false;

            row.Rank = CreateText("Rank", pillGo.transform, new Vector2(-340f, 0f), 34f, FontStyles.Bold);
            row.Rank.rectTransform.sizeDelta = new Vector2(70f, 60f);
            UiTheme.ApplyFont(row.Rank, UiTheme.ButtonFont);
            row.Rank.color = UiTheme.TextDim;

            row.Name = CreateText("Name", pillGo.transform, new Vector2(-40f, 0f), 34f, FontStyles.Bold);
            row.Name.alignment = TextAlignmentOptions.MidlineLeft;
            row.Name.rectTransform.sizeDelta = new Vector2(460f, 60f);
            UiTheme.ApplyFont(row.Name, UiTheme.BodyFont);

            row.Progress = CreateText("Progress", pillGo.transform, new Vector2(310f, 0f), 32f, FontStyles.Bold);
            row.Progress.alignment = TextAlignmentOptions.MidlineRight;
            row.Progress.rectTransform.sizeDelta = new Vector2(160f, 60f);
            UiTheme.ApplyFont(row.Progress, UiTheme.ButtonFont);
            row.Progress.color = UiTheme.Gold;

            return row;
        }

        private void BuildOpener(Transform buttonHost)
        {
            _opener = new GameObject("EventsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _opener.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)_opener.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(170f, 76f);
            rect.anchoredPosition = new Vector2(-24f, -212f); // under CHEST

            var image = _opener.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Pill, UiTheme.Slot);
            var button = _opener.GetComponent<Button>();
            _opener.AddComponent<PressableButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnOpenerClicked);

            TMP_Text label = CreateText("Label", _opener.transform, Vector2.zero, 34f, FontStyles.Bold);
            UiTheme.ApplyFont(label, UiTheme.ButtonFont);
            label.text = "EVENT";
            Stretch(label.rectTransform);

            _badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            _badge.transform.SetParent(_opener.transform, false);
            var badgeRect = (RectTransform)_badge.transform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.sizeDelta = new Vector2(30f, 30f);
            badgeRect.anchoredPosition = new Vector2(-4f, 4f);
            var badgeImage = _badge.GetComponent<Image>();
            UiTheme.ApplySprite(badgeImage, UiTheme.CircleSprite, UiTheme.Cta);
            badgeImage.raycastTarget = false;

            // Mondays have no event to open. Stale-across-midnight is accepted
            // panel-wide behaviour: the menu scene rebuilds on every entry.
            _opener.SetActive(EventService.PhaseToday != EventPhase.Off);
            RefreshBadge();
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
