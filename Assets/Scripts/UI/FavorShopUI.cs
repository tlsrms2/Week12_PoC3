using System;
using FactoryDelivery.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FactoryDelivery.UI
{
    public class FavorShopUI : MonoBehaviour
    {
        [SerializeField] private FavorShopManager _favorShopManager;
        [SerializeField] private TributeManager _tributeManager;

        private TextMeshProUGUI _favorText;
        private TextMeshProUGUI _giftText;
        private Button _buyWarehouseButton;
        private Button _buyWinterColdButton;
        private Button _buyMidnightTorchButton;
        private Button _useWarehouseButton;
        private Button _useWinterColdButton;
        private Button _useMidnightTorchButton;

        private void Awake()
        {
            ResolveReferences();
            BuildPanel();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_tributeManager != null)
            {
                _tributeManager.OnFavorChanged += OnFavorChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Gifts.OnGiftCountChanged += OnGiftCountChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_tributeManager != null)
            {
                _tributeManager.OnFavorChanged -= OnFavorChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Gifts.OnGiftCountChanged -= OnGiftCountChanged;
            }
        }

        private void BuildPanel()
        {
            if (_favorText != null)
            {
                return;
            }

            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-24f, 180f);
            root.sizeDelta = new Vector2(300f, 360f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.09f, 0.08f, 0.06f, 0.92f);

            VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _favorText = CreateLabel("총애 상점", 18, FontStyles.Bold);
            _buyWarehouseButton = CreateButton("구매: 물류창고 기반 (총애 1)", () => RunAndRefresh(_favorShopManager.TryBuyWarehouseFoundation));
            _buyWinterColdButton = CreateButton("구매: 겨울철의 추위 (총애 1)", () => RunAndRefresh(_favorShopManager.TryBuyWinterCold));
            _buyMidnightTorchButton = CreateButton("구매: 심야의 횃불 (총애 1)", () => RunAndRefresh(_favorShopManager.TryBuyMidnightTorch));
            _giftText = CreateLabel("하사품 주머니", 15, FontStyles.Bold);
            _useWarehouseButton = CreateButton("발동: 물류창고 카드 받기", () => RunAndRefresh(_favorShopManager.TryUseWarehouseFoundation));
            _useWinterColdButton = CreateButton("발동: 오늘 밤 +10초", () => RunAndRefresh(_favorShopManager.TryUseWinterCold));
            _useMidnightTorchButton = CreateButton("발동: 매일 밤 +5초", () => RunAndRefresh(_favorShopManager.TryUseMidnightTorch));
        }

        private TextMeshProUGUI CreateLabel(string text, float fontSize, FontStyles style)
        {
            GameObject labelGo = new GameObject(text);
            labelGo.transform.SetParent(transform, false);

            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = new Color(0.95f, 0.82f, 0.25f, 1f);
            label.alignment = TextAlignmentOptions.Center;

            LayoutElement layout = labelGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 28f;
            return label;
        }

        private Button CreateButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonGo = new GameObject(label);
            buttonGo.transform.SetParent(transform, false);

            Image image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.22f, 0.14f, 0.08f, 1f);

            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            LayoutElement layout = buttonGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 42f;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform, false);

            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 13f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;

            return button;
        }

        private void RunAndRefresh(Func<bool> action)
        {
            if (action != null)
            {
                action.Invoke();
            }

            Refresh();
        }

        private void OnFavorChanged(int favor)
        {
            Refresh();
        }

        private void OnGiftCountChanged(GiftType giftType, int count)
        {
            Refresh();
        }

        private void Refresh()
        {
            ResolveReferences();

            int favor = _tributeManager != null ? _tributeManager.FavorBalance : 0;
            if (_favorText != null)
            {
                _favorText.text = $"총애 상점  |  보유 총애: {favor}";
            }

            bool canBuy = favor > 0;
            if (_buyWarehouseButton != null) _buyWarehouseButton.interactable = canBuy;
            if (_buyWinterColdButton != null) _buyWinterColdButton.interactable = canBuy;
            if (_buyMidnightTorchButton != null) _buyMidnightTorchButton.interactable = canBuy;

            int warehouseCount = GetGiftCount(GiftType.WarehouseFoundation);
            int winterColdCount = GetGiftCount(GiftType.WinterCold);
            int midnightTorchCount = GetGiftCount(GiftType.MidnightTorch);

            if (_giftText != null)
            {
                _giftText.text = $"하사품 주머니  |  창고 {warehouseCount} / 추위 {winterColdCount} / 횃불 {midnightTorchCount}";
            }

            if (_useWarehouseButton != null) _useWarehouseButton.interactable = warehouseCount > 0;
            if (_useWinterColdButton != null) _useWinterColdButton.interactable = winterColdCount > 0;
            if (_useMidnightTorchButton != null) _useMidnightTorchButton.interactable = midnightTorchCount > 0;
        }

        private int GetGiftCount(GiftType giftType)
        {
            return GameManager.Instance != null ? GameManager.Instance.Gifts.GetCount(giftType) : 0;
        }

        private void ResolveReferences()
        {
            if (_favorShopManager == null)
            {
                _favorShopManager = GameManager.Instance != null
                    ? GameManager.Instance.FavorShop
                    : FindFirstObjectByType<FavorShopManager>();
            }

            if (_tributeManager == null)
            {
                _tributeManager = GameManager.Instance != null
                    ? GameManager.Instance.Tribute
                    : FindFirstObjectByType<TributeManager>();
            }
        }
    }
}
