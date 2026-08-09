using System.Collections;
using Match3.Core;
using Match3.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    /// <summary>
    /// The sticker album (menu only): a 6×6 grid of the game's own bestiary,
    /// filled by opening packs earned from stars, chests, missions and events.
    /// Owned stickers show in full colour, missing ones as dark silhouettes;
    /// completed pages wear a gold outline. The OPEN PACK ceremony pops the
    /// three rolled stickers over a dimmed grid — state is saved BEFORE any
    /// animation, so a mid-ceremony app kill loses nothing.
    /// </summary>
    public sealed class AlbumPanel : MonoBehaviour
    {
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color SilhouetteTint = new Color(0.16f, 0.16f, 0.2f);
        private static readonly Color DupeGrey = new Color(0.65f, 0.66f, 0.72f);

        private GameObject _root;
        private Image _card;
        private TMP_Text _completionLine;
        private RectTransform _barFill;
        private readonly Image[] _stickerIcons = new Image[AlbumCatalog.StickerCount];
        private readonly GameObject[] _rowOutlines = new GameObject[AlbumCatalog.PageCount];
        private Button _openButton;
        private TMP_Text _openLabel;
        private GameObject _ceremonyRoot;
        private readonly Image[] _ceremonyIcons = new Image[AlbumCatalog.PackSize];
        private readonly TMP_Text[] _ceremonyCaptions = new TMP_Text[AlbumCatalog.PackSize];
        private TMP_Text _ceremonyReward;
        private Coroutine _ceremony;
        private GameObject _badge;
        private CandySpriteLibrary _library;

        public static AlbumPanel Attach(Canvas canvas, Transform buttonHost)
        {
            var host = new GameObject(nameof(AlbumPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            host.SetActive(false);
            var panel = host.AddComponent<AlbumPanel>();
            panel._library = Resources.Load<CandySpriteLibrary>("CandySpriteLibrary");
            panel.Build();
            panel.BuildOpener(buttonHost);
            panel.Hide();
            host.SetActive(true);
            return panel;
        }

        private void Refresh()
        {
            MetaState meta = MetaService.Current;
            int owned = AlbumRules.OwnedCount(meta);
            _completionLine.text = $"{owned}/{AlbumCatalog.StickerCount}";
            float fraction = owned / (float)AlbumCatalog.StickerCount;
            if (fraction > 0f)
                fraction = Mathf.Max(fraction, 0.05f); // a sliced pill needs a sliver to read as round
            _barFill.anchorMax = new Vector2(fraction, 1f);

            for (int id = 0; id < AlbumCatalog.StickerCount; id++)
            {
                StickerDef def = AlbumCatalog.Get(id);
                UiTheme.ApplySprite(_stickerIcons[id], SpriteFor(def),
                    AlbumRules.IsOwned(meta, id) ? Color.white : SilhouetteTint);
            }
            for (int page = 0; page < AlbumCatalog.PageCount; page++)
                _rowOutlines[page].SetActive(AlbumRules.IsPageComplete(meta, page));

            int packs = AlbumService.Packs;
            _openLabel.text = $"OPEN PACK  (×{packs})";
            _openButton.interactable = packs > 0 && _ceremony == null;
        }

        private void Show()
        {
            _card.color = UiTheme.ThemeCard;
            Refresh();
            // Hide (not the default close) so a back press also folds the ceremony.
            UiTween.OpenPanel(this, _root, _card.transform, Hide);
        }

        private void Hide()
        {
            CloseCeremony();
            UiTween.ClosePanel(this, _root);
        }

        private void OnOpenerClicked()
        {
            AudioManager.Play(Sfx.Button);
            if (_badge != null)
                _badge.SetActive(AlbumService.Packs > 0); // soften the stale-badge idiom
            if (_root.activeSelf) Hide();
            else Show();
        }

        // ---- The pack-opening ceremony ----------------------------------------------

        private void OnOpenClicked()
        {
            AudioManager.Play(Sfx.Button);
            if (_ceremony != null)
                return; // spam guard — one ceremony at a time
            if (!AlbumService.TryOpenPack(out PackResult result))
            {
                Refresh();
                return;
            }

            // The pack is already on disk; everything below is presentation.
            _openButton.interactable = false;
            _ceremonyRoot.SetActive(true);
            _ceremonyReward.text = ComposeRewardLine(result);
            if (result.PagesCompletedMask != 0 || result.AlbumCompleted)
            {
                AudioManager.Play(Sfx.Win);
                Haptics.Medium();
            }

            if (Prefs.ReducedMotionOn)
            {
                for (int i = 0; i < AlbumCatalog.PackSize; i++)
                    PresentSlot(i, result.Slots[i], animateScale: false);
                Refresh();
                return; // overlay stays up until tapped; _ceremony stays null
            }

            _ceremony = StartCoroutine(PopStickers(result));
        }

        private IEnumerator PopStickers(PackResult result)
        {
            for (int i = 0; i < AlbumCatalog.PackSize; i++)
            {
                _ceremonyIcons[i].transform.localScale = Vector3.zero;
                _ceremonyCaptions[i].text = string.Empty;
            }

            for (int i = 0; i < AlbumCatalog.PackSize; i++)
            {
                PresentSlot(i, result.Slots[i], animateScale: true);
                AudioManager.Play(Sfx.Pop, 1f + 0.18f * i);
                Transform icon = _ceremonyIcons[i].transform;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
                {
                    // The PopStars curve: overshoot to 1.25 then settle.
                    float scale = t < 0.7f ? Mathf.Lerp(0f, 1.25f, t / 0.7f) : Mathf.Lerp(1.25f, 1f, (t - 0.7f) / 0.3f);
                    icon.localScale = Vector3.one * scale;
                    yield return null;
                }
                icon.localScale = Vector3.one;
            }

            Refresh();
            _ceremony = null;
        }

        private void PresentSlot(int index, PackSlotResult slot, bool animateScale)
        {
            StickerDef def = AlbumCatalog.Get(slot.StickerId);
            UiTheme.ApplySprite(_ceremonyIcons[index], SpriteFor(def), Color.white);
            if (!animateScale)
                _ceremonyIcons[index].transform.localScale = Vector3.one;

            TMP_Text caption = _ceremonyCaptions[index];
            if (slot.WasNew)
            {
                caption.text = "NEW!";
                caption.color = UiTheme.Gold;
            }
            else
            {
                caption.text = $"+1 {BoosterName(slot.BoosterPaid)}";
                caption.color = DupeGrey;
            }
        }

        private void CloseCeremony()
        {
            if (_ceremony != null)
            {
                StopCoroutine(_ceremony);
                _ceremony = null;
            }
            if (_ceremonyRoot != null && _ceremonyRoot.activeSelf)
            {
                _ceremonyRoot.SetActive(false);
                Refresh();
            }
        }

        private static string ComposeRewardLine(PackResult result)
        {
            if (result.AlbumCompleted)
                return $"ALBUM COMPLETE!  THE GOLDEN COVER IS YOURS  +{AlbumCatalog.AlbumRescues} SAVE";
            if (result.PagesCompletedMask == 0)
                return string.Empty;
            for (int page = 0; page < AlbumCatalog.PageCount; page++)
            {
                if ((result.PagesCompletedMask & (1 << page)) == 0)
                    continue;
                ChestReward reward = AlbumCatalog.PageReward(page);
                string line = $"{AlbumCatalog.PageName(page)} PAGE COMPLETE!  +{reward.Hammers} SMASH +{reward.FreeSwaps} SWAP +{reward.Shuffles} MIX";
                if (reward.StreakShields > 0)
                    line += $" +{reward.StreakShields} SHIELD";
                if (AlbumCatalog.PageRescues(page) > 0)
                    line += $" +{AlbumCatalog.PageRescues(page)} SAVE";
                return line; // one line; a multi-page pack shows the first (rare, and the grid glows anyway)
            }
            return string.Empty;
        }

        private static string BoosterName(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Hammer: return "SMASH";
                case BoosterKind.FreeSwap: return "SWAP";
                default: return "MIX";
            }
        }

        private Sprite SpriteFor(StickerDef def)
        {
            switch (def.Art)
            {
                case StickerArt.LockCage: return _library != null ? _library.LockCage : null;
                case StickerArt.Frosting: return _library != null ? _library.FrostingSprite(def.Param) : null;
                case StickerArt.Town: return Resources.Load<Sprite>($"UI/Town/town_stage{def.Param}");
                case StickerArt.Trophy: return UiTheme.TrophySprite != null ? UiTheme.TrophySprite : UiTheme.StarSprite;
                default: return _library != null ? _library.For(def.Param, def.Kind) : null;
            }
        }

        // ---- Construction -----------------------------------------------------------

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
            title.text = "STICKER ALBUM";

            _completionLine = CreateText("Completion", content, new Vector2(0f, 436f), 30f, FontStyles.Bold);
            UiTheme.ApplyFont(_completionLine, UiTheme.BodyFont);
            _completionLine.color = UiTheme.Gold;
            _completionLine.characterSpacing = 4f;

            GameObject trackGo = CreateRect("Track", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 28f));
            trackGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 396f);
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

            // The 6×6 grid: one row per page — caption left, six stickers across.
            for (int page = 0; page < AlbumCatalog.PageCount; page++)
            {
                float y = 330f - page * 118f;

                _rowOutlines[page] = CreateRect($"PageGlow{page}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 110f));
                _rowOutlines[page].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
                var glow = _rowOutlines[page].AddComponent<Image>();
                UiTheme.ApplySprite(glow, UiTheme.RoundOutline, UiTheme.Gold);
                glow.raycastTarget = false;
                _rowOutlines[page].SetActive(false);

                TMP_Text caption = CreateText($"PageName{page}", content, new Vector2(-348f, y), 20f, FontStyles.Bold);
                UiTheme.ApplyFont(caption, UiTheme.BodyFont);
                caption.rectTransform.sizeDelta = new Vector2(120f, 44f); // "BLOCKERS" must fit un-clipped
                caption.alignment = TextAlignmentOptions.MidlineLeft;
                caption.color = UiTheme.TextDim;
                caption.text = AlbumCatalog.PageName(page);

                for (int slot = 0; slot < AlbumCatalog.SlotsPerPage; slot++)
                {
                    GameObject icon = CreateRect($"Sticker{page}_{slot}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 90f));
                    icon.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250f + slot * 100f, y);
                    var image = icon.AddComponent<Image>();
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    _stickerIcons[page * AlbumCatalog.SlotsPerPage + slot] = image;
                }
            }

            GameObject openGo = CreateRect("OpenButton", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 130f));
            openGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -430f);
            var openImage = openGo.AddComponent<Image>();
            UiTheme.ApplySprite(openImage, UiTheme.PillPink, Color.white);
            _openButton = openGo.AddComponent<Button>();
            openGo.AddComponent<PressableButton>();
            _openButton.targetGraphic = openImage;
            _openButton.onClick.AddListener(OnOpenClicked);
            _openLabel = CreateText("Label", openGo.transform, Vector2.zero, 40f, FontStyles.Bold);
            UiTheme.ApplyFont(_openLabel, UiTheme.ButtonFont);
            Stretch(_openLabel.rectTransform);

            // -560, not -530: OPEN PACK's bottom edge reaches -495 and the two
            // pills were overlapping by 20px (mis-taps opened packs).
            UiWidgets.ClosePill(content, new Vector2(0f, -560f), OnOpenerClicked);

            BuildCeremony(content);
        }

        private void BuildCeremony(Transform content)
        {
            // Built LAST: the ceremony overlay must out-raycast the whole card.
            _ceremonyRoot = CreateRect("Ceremony", content, Vector2.zero, Vector2.one, Vector2.zero);
            var dim = _ceremonyRoot.AddComponent<Image>();
            UiTheme.ApplySprite(dim, UiTheme.Round, new Color(0.03f, 0.03f, 0.08f, 0.92f));
            var dismiss = _ceremonyRoot.AddComponent<Button>();
            dismiss.targetGraphic = dim;
            dismiss.onClick.AddListener(CloseCeremony);

            for (int i = 0; i < AlbumCatalog.PackSize; i++)
            {
                GameObject icon = CreateRect($"PackSticker{i}", _ceremonyRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(140f, 140f));
                icon.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180f + i * 180f, 40f);
                _ceremonyIcons[i] = icon.AddComponent<Image>();
                _ceremonyIcons[i].preserveAspect = true;
                _ceremonyIcons[i].raycastTarget = false;

                _ceremonyCaptions[i] = CreateText($"PackCaption{i}", _ceremonyRoot.transform, new Vector2(-180f + i * 180f, -70f), 24f, FontStyles.Bold);
                UiTheme.ApplyFont(_ceremonyCaptions[i], UiTheme.BodyFont);
                _ceremonyCaptions[i].rectTransform.sizeDelta = new Vector2(170f, 44f);
            }

            _ceremonyReward = CreateText("PackReward", _ceremonyRoot.transform, new Vector2(0f, -200f), 26f, FontStyles.Bold);
            UiTheme.ApplyFont(_ceremonyReward, UiTheme.BodyFont);
            _ceremonyReward.color = UiTheme.Gold;

            TMP_Text hint = CreateText("PackHint", _ceremonyRoot.transform, new Vector2(0f, -320f), 24f, FontStyles.Normal);
            UiTheme.ApplyFont(hint, UiTheme.BodyFont);
            hint.color = UiTheme.TextDim;
            hint.text = "tap to continue";

            _ceremonyRoot.SetActive(false);
        }

        private void BuildOpener(Transform buttonHost)
        {
            var go = new GameObject("AlbumButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(200f, 76f);
            rect.anchoredPosition = new Vector2(24f, -288f); // fourth in the left column

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Pill, UiTheme.Slot);
            var button = go.GetComponent<Button>();
            go.AddComponent<PressableButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnOpenerClicked);

            TMP_Text label = CreateText("Label", go.transform, Vector2.zero, 34f, FontStyles.Bold);
            UiTheme.ApplyFont(label, UiTheme.ButtonFont);
            label.text = "ALBUM";
            Stretch(label.rectTransform);

            _badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            _badge.transform.SetParent(go.transform, false);
            var badgeRect = (RectTransform)_badge.transform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.sizeDelta = new Vector2(30f, 30f);
            badgeRect.anchoredPosition = new Vector2(-4f, 4f);
            var badgeImage = _badge.GetComponent<Image>();
            UiTheme.ApplySprite(badgeImage, UiTheme.CircleSprite, UiTheme.Cta);
            badgeImage.raycastTarget = false;
            _badge.SetActive(AlbumService.Packs > 0);
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
