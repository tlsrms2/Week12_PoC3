using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FactoryDelivery.UI
{
    public class RunSystemsPanelUI : MonoBehaviour
    {
        private TextMeshProUGUI _augmentText;
        private TextMeshProUGUI _edictText;
        private TextMeshProUGUI _giftHintText;

        private void Awake()
        {
            BuildPanel();
            RefreshPlaceholders();
        }

        private void BuildPanel()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -72f);
            root.sizeDelta = new Vector2(620f, 74f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.08f, 0.075f, 0.06f, 0.72f);

            HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _augmentText = CreateSlot("증강");
            _edictText = CreateSlot("어명");
            _giftHintText = CreateSlot("하사품");
        }

        private TextMeshProUGUI CreateSlot(string title)
        {
            GameObject slotGo = new GameObject(title);
            slotGo.transform.SetParent(transform, false);

            Image image = slotGo.AddComponent<Image>();
            image.color = new Color(0.16f, 0.13f, 0.09f, 0.92f);

            RectTransform rect = slotGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 58f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(slotGo.transform, false);

            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 5f);
            textRect.offsetMax = new Vector2(-8f, -5f);

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 12f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        public void RefreshPlaceholders()
        {
            if (_augmentText != null)
            {
                _augmentText.text = "<b>증강</b>\n정산 성공 후 선택 UI 예정";
            }

            if (_edictText != null)
            {
                _edictText.text = "<b>어명 슬롯</b>\n총애 구매/장착 UI 예정";
            }

            if (_giftHintText != null)
            {
                _giftHintText.text = "<b>하사품</b>\n우측 총애 상점/주머니 사용";
            }

            // TODO(Augment UI): AugmentManager가 생기면 여기에서 보유 증강 목록을 읽어
            // 첫 슬롯에 아이콘/이름/효과 요약을 배치한다. 정산 성공 시 선택 팝업도 이 패널 아래에 붙인다.

            // TODO(Edict UI): EdictManager가 생기면 제한 슬롯 수, 장착된 어명, 교체 버튼을 이 영역에 연결한다.
            // 판매 계산은 SaleModifierManager에서 처리하고, 이 UI는 장착 상태만 보여준다.

            // TODO(Gift UI): 하사품 주머니가 커지면 FavorShopUI의 발동 버튼들을 이 패널의 하사품 영역으로 이전한다.
        }
    }
}
