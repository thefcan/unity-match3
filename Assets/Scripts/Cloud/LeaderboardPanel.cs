using Match3.Game;
using Match3.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.Cloud
{
    /// <summary>
    /// The time-attack leaderboard overlay (main menu only; lives in the cloud
    /// assembly, so it simply doesn't exist without the UGS packages). Attaches
    /// itself next to the menu's other runtime panels; the RANKS opener sits to the
    /// left of the settings button. Offline states degrade to a message — never an
    /// error.
    /// </summary>
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        private const int TopCount = 10;
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.78f);

        private GameObject _root;
        private Image _card;
        private TMP_Text _status;
        private TMP_Text _playerRow;
        private readonly TMP_Text[] _rows = new TMP_Text[TopCount];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (_, _) => TryAttach();
            TryAttach();
        }

        private static void TryAttach()
        {
            // Menu scene only: it has a MainMenuView and no GameManager.
            if (Object.FindObjectOfType<MainMenuView>() == null)
                return;
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null || canvas.transform.Find(nameof(LeaderboardPanel)) != null)
                return;

            Transform buttonHost = canvas.transform.Find("SafeArea");
            if (buttonHost == null)
                buttonHost = canvas.transform;

            var host = new GameObject(nameof(LeaderboardPanel), typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.sizeDelta = Vector2.zero;

            host.SetActive(false);
            var panel = host.AddComponent<LeaderboardPanel>();
            panel.Build();
            panel.BuildOpenerButton(buttonHost);
            panel.Hide();
            host.SetActive(true);
        }

        private void Show()
        {
            _card.color = UiTheme.ThemeCard;
            _root.SetActive(true);
            Refresh();
        }

        private void Hide() => _root.SetActive(false);

        private async void Refresh()
        {
            foreach (TMP_Text row in _rows)
                row.text = string.Empty;
            _playerRow.text = string.Empty;

            if (!CloudSync.SignedIn)
            {
                _status.text = "Offline — connect to see rankings";
                return;
            }

            _status.text = "Loading...";
            try
            {
                (var top, var me) = await CloudSync.FetchLeaderboardAsync(TopCount);
                if (this == null || !_root.activeSelf)
                    return; // panel closed / scene changed while loading

                _status.text = top.Count == 0 ? "No scores yet — set one in Time Attack!" : string.Empty;
                for (int i = 0; i < top.Count && i < _rows.Length; i++)
                {
                    (int rank, string name, double score) = top[i];
                    _rows[i].text = $"{rank}.  {name}  —  {score:N0}";
                    _rows[i].color = rank <= 3 ? UiTheme.Gold : UiTheme.TextPrimary;
                }
                _playerRow.text = me is { } mine ? $"You:  #{mine.rank}  —  {mine.score:N0}" : "You:  no score yet";
            }
            catch (System.Exception)
            {
                if (this != null)
                    _status.text = "Couldn't load the board — try again later";
            }
        }

        // ---- Construction ---------------------------------------------------------------

        private void Build()
        {
            _root = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            _root.GetComponent<Image>().color = OverlayColor;

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_root.transform, false);
            var cardRect = (RectTransform)cardGo.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(900f, 1240f);
            _card = cardGo.GetComponent<Image>();
            UiTheme.ApplySprite(_card, UiTheme.Round, UiTheme.ThemeCard);
            Transform content = cardGo.transform;

            TMP_Text title = NewText("Title", content, new Vector2(0f, 530f), 66f, FontStyles.Bold);
            UiTheme.ApplyFont(title, UiTheme.TitleFont);
            title.text = "TOP SCORES";

            TMP_Text subtitle = NewText("Subtitle", content, new Vector2(0f, 455f), 30f, FontStyles.Normal);
            UiTheme.ApplyFont(subtitle, UiTheme.BodyFont);
            subtitle.color = UiTheme.TextDim;
            subtitle.text = "Time Attack";

            _status = NewText("Status", content, new Vector2(0f, 40f), 34f, FontStyles.Normal);
            UiTheme.ApplyFont(_status, UiTheme.BodyFont);
            _status.color = UiTheme.TextDim;

            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i] = NewText($"Row{i}", content, new Vector2(0f, 380f - i * 72f), 38f, FontStyles.Bold);
                UiTheme.ApplyFont(_rows[i], UiTheme.ButtonFont);
                _rows[i].alignment = TextAlignmentOptions.MidlineLeft;
                var rect = _rows[i].rectTransform;
                rect.sizeDelta = new Vector2(720f, 64f);
            }

            _playerRow = NewText("PlayerRow", content, new Vector2(0f, -400f), 40f, FontStyles.Bold);
            UiTheme.ApplyFont(_playerRow, UiTheme.ButtonFont);
            _playerRow.color = UiTheme.Gold;

            BuildButton(content, "Close", new Vector2(0f, -520f), () =>
            {
                AudioManager.Play(Sfx.Button);
                Hide();
            });
        }

        private void BuildOpenerButton(Transform buttonHost)
        {
            var go = new GameObject("RanksButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonHost, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(170f, 76f);
            rect.anchoredPosition = new Vector2(-128f, -30f); // left of the settings opener

            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.Pill, UiTheme.Slot);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Button);
                if (_root.activeSelf) Hide();
                else Show();
            });

            TMP_Text label = NewText("Label", go.transform, Vector2.zero, 32f, FontStyles.Bold);
            UiTheme.ApplyFont(label, UiTheme.ButtonFont);
            label.text = "RANKS";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private void BuildButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 124f);
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            UiTheme.ApplySprite(image, UiTheme.PillPink, Color.white);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text text = NewText("Label", go.transform, Vector2.zero, 46f, FontStyles.Bold);
            UiTheme.ApplyFont(text, UiTheme.ButtonFont);
            text.text = label;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private static TMP_Text NewText(string name, Transform parent, Vector2 position, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(820f, 90f);
            rect.anchoredPosition = position;
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
