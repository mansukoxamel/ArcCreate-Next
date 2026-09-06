using ArcCreate.Compose.Components;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Compose.Popups
{
    [RequireComponent(typeof(RectTransform))]
    public class TextDialog : Dialog
    {
        private RectTransform rect;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text contentText;
        [SerializeField] private RectTransform contentRect;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Transform buttonParent;
        [SerializeField] private float extraHeight;
        [SerializeField] private float maxHeight;
        [SerializeField] private Color[] buttonColors;

        private const float ListDialogWidth = 700;
        private const float ListRowHeight = 30;
        private const float ListRowSpacing = 5;
        private const float ListContentHeight = 45;
        private const float ListBottomPadding = 10;

        public void Setup(string title, string content, ButtonSetting[] buttonSettings)
        {
            Open();
            titleText.text = title;
            contentText.text = content;

            foreach (ButtonSetting setting in buttonSettings)
            {
                GameObject go = Instantiate(buttonPrefab, buttonParent);
                TextDialogButton button = go.GetComponent<TextDialogButton>();
                Color color = buttonColors[(int)setting.ButtonColor];
                button.Setup(setting.Text, setting.Callback, color, this);
            }

            SetBoxHeightCoroutine().Forget();
        }

        public void SetupVerticalList(string title, string content, ButtonSetting[] buttonSettings)
        {
            Open();
            titleText.text = title;
            contentText.text = content;

            HorizontalLayoutGroup horizontalLayout = buttonParent.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout != null)
            {
                horizontalLayout.enabled = false;
            }

            for (int index = 0; index < buttonSettings.Length; index++)
            {
                ButtonSetting setting = buttonSettings[index];
                GameObject go = Instantiate(buttonPrefab, buttonParent);
                TextDialogButton button = go.GetComponent<TextDialogButton>();
                Color color = buttonColors[(int)setting.ButtonColor];
                button.Setup(setting.Text, setting.Callback, color, this);

                RectTransform buttonRect = go.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0, 1);
                buttonRect.anchorMax = new Vector2(1, 1);
                buttonRect.pivot = new Vector2(0.5f, 1);
                buttonRect.anchoredPosition = new Vector2(0, -index * (ListRowHeight + ListRowSpacing));
                buttonRect.sizeDelta = new Vector2(0, ListRowHeight);

                TMP_Text buttonText = go.GetComponentInChildren<TMP_Text>(true);
                buttonText.parseCtrlCharacters = false;
                buttonText.textWrappingMode = TextWrappingModes.NoWrap;
                buttonText.overflowMode = TextOverflowModes.Ellipsis;
                buttonText.enableAutoSizing = true;
                buttonText.fontSizeMin = 8;
                buttonText.fontSizeMax = buttonText.fontSize;
                buttonText.alignment = setting.Callback != null
                    ? TextAlignmentOptions.MidlineLeft
                    : TextAlignmentOptions.Center;
                buttonText.margin = new Vector4(10, 0, 10, 0);
            }

            float buttonListHeight = buttonSettings.Length * ListRowHeight
                + Mathf.Max(0, buttonSettings.Length - 1) * ListRowSpacing;
            RectTransform buttonParentRect = buttonParent as RectTransform;
            buttonParentRect.anchorMin = new Vector2(0, 0);
            buttonParentRect.anchorMax = new Vector2(1, 0);
            buttonParentRect.pivot = new Vector2(0.5f, 0);
            buttonParentRect.anchoredPosition = new Vector2(0, ListBottomPadding);
            buttonParentRect.sizeDelta = new Vector2(-20, buttonListHeight);

            RectTransform scrollRect = contentRect.parent.parent as RectTransform;
            scrollRect.anchorMin = new Vector2(0, 1);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.pivot = new Vector2(0.5f, 1);
            scrollRect.anchoredPosition = new Vector2(0, -10);
            scrollRect.sizeDelta = new Vector2(-20, ListContentHeight);

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ListDialogWidth);
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                25 + ListContentHeight + buttonListHeight + (ListBottomPadding * 2));
        }

        public override void Close()
        {
            base.Close();
            Destroy(gameObject);
        }

        // I hate unity so much
        private async UniTask SetBoxHeightCoroutine()
        {
            await UniTask.NextFrame();

            float preferredHeight = contentRect.rect.height;
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Min(maxHeight, preferredHeight + extraHeight));
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }
    }
}
