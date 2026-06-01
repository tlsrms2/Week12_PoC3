using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FactoryDelivery.Core;

namespace FactoryDelivery.UI
{
    public class FavorShopUI : MonoBehaviour
    {
        private enum ShopTab
        {
            GiftShop,
            EdictShop,
            GiftBag
        }

        [SerializeField] private FavorShopManager _favorShopManager;
        [SerializeField] private TributeManager _tributeManager;

        [Header("Scene UI")]
        [SerializeField] private TextMeshProUGUI _favorText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _giftShopTabButton;
        [SerializeField] private Button _edictShopTabButton;
        [SerializeField] private Button _giftBagTabButton;
        [SerializeField] private RectTransform _gridContainer;

        private ShopTab _activeTab = ShopTab.GiftShop;

        private void Awake()
        {
            ResolveReferences();
            EnsureScenePanelFallback();
        }

        private void OnEnable()
        {
            ResolveReferences();
            HookSceneButtons();

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
            UnhookSceneButtons();

            if (_tributeManager != null)
            {
                _tributeManager.OnFavorChanged -= OnFavorChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Gifts.OnGiftCountChanged -= OnGiftCountChanged;
            }
        }

        public void ShowShop()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void HideShop()
        {
            gameObject.SetActive(false);
        }

        private void EnsureScenePanelFallback()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(760f, 560f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.055f, 0.035f, 0.98f);

            Outline outline = gameObject.GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.78f, 0.18f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            if (_favorText != null && _closeButton != null && _giftShopTabButton != null &&
                _edictShopTabButton != null && _giftBagTabButton != null && _gridContainer != null)
            {
                return;
            }

            BuildFallbackChildren();
        }

        private void BuildFallbackChildren()
        {
            ClearAllChildren(transform);

            GameObject headerGo = new GameObject("Header");
            headerGo.transform.SetParent(transform, false);
            headerGo.AddComponent<LayoutElement>().preferredHeight = 44f;
            HorizontalLayoutGroup headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = true;

            _favorText = CreateText(headerGo.transform, "총애 상점", 22f, FontStyles.Bold, TextAlignmentOptions.Left);
            _favorText.color = new Color(1f, 0.84f, 0.22f, 1f);

            _closeButton = CreateSmallButton(headerGo.transform, "닫기");
            _closeButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 84f;

            GameObject tabsGo = new GameObject("Tabs");
            tabsGo.transform.SetParent(transform, false);
            tabsGo.AddComponent<LayoutElement>().preferredHeight = 42f;
            HorizontalLayoutGroup tabsLayout = tabsGo.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 8f;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            _giftShopTabButton = CreateSmallButton(tabsGo.transform, "하사품 구매");
            _edictShopTabButton = CreateSmallButton(tabsGo.transform, "교지 구매");
            _giftBagTabButton = CreateSmallButton(tabsGo.transform, "주머니");

            GameObject scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(transform, false);
            scrollGo.AddComponent<LayoutElement>().preferredHeight = 430f;

            ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 34f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewport = viewportGo.AddComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportGo.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = viewport;

            GameObject contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            _gridContainer = contentGo.AddComponent<RectTransform>();
            _gridContainer.anchorMin = new Vector2(0f, 1f);
            _gridContainer.anchorMax = new Vector2(1f, 1f);
            _gridContainer.pivot = new Vector2(0.5f, 1f);
            _gridContainer.anchoredPosition = Vector2.zero;
            _gridContainer.sizeDelta = Vector2.zero;
            scrollRect.content = _gridContainer;

            GridLayoutGroup grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(224f, 94f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(2, 14, 2, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void HookSceneButtons()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(HideShop);
            if (_giftShopTabButton != null) _giftShopTabButton.onClick.AddListener(ShowGiftShopTab);
            if (_edictShopTabButton != null) _edictShopTabButton.onClick.AddListener(ShowEdictShopTab);
            if (_giftBagTabButton != null) _giftBagTabButton.onClick.AddListener(ShowGiftBagTab);
        }

        private void UnhookSceneButtons()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(HideShop);
            if (_giftShopTabButton != null) _giftShopTabButton.onClick.RemoveListener(ShowGiftShopTab);
            if (_edictShopTabButton != null) _edictShopTabButton.onClick.RemoveListener(ShowEdictShopTab);
            if (_giftBagTabButton != null) _giftBagTabButton.onClick.RemoveListener(ShowGiftBagTab);
        }

        private void ShowGiftShopTab() => SetActiveTab(ShopTab.GiftShop);
        private void ShowEdictShopTab() => SetActiveTab(ShopTab.EdictShop);
        private void ShowGiftBagTab() => SetActiveTab(ShopTab.GiftBag);

        private void SetActiveTab(ShopTab tab)
        {
            _activeTab = tab;
            Refresh();
        }

        private void Refresh()
        {
            ResolveReferences();

            int favor = _tributeManager != null ? _tributeManager.FavorBalance : 0;
            if (_favorText != null)
            {
                _favorText.text = $"총애 상점  |  보유: {favor}";
            }

            ClearGrid();

            switch (_activeTab)
            {
                case ShopTab.GiftShop:
                    AddGiftPurchaseItems(favor);
                    break;
                case ShopTab.EdictShop:
                    AddEdictItems(favor);
                    break;
                case ShopTab.GiftBag:
                    AddGiftBagItems();
                    break;
            }
        }

        private void AddGiftPurchaseItems(int favor)
        {
            AddShopItem("물류창고 기반", "창고 블록 카드를 즉시 지급합니다.", 1, favor, _favorShopManager.TryBuyWarehouseFoundation);
            AddShopItem("겨울철의 추위", "오늘의 밤 시간을 10초 늘립니다.", 1, favor, _favorShopManager.TryBuyWinterCold);
            AddShopItem("심야의 횃불", "이후 매일 밤 시간을 5초 늘립니다.", 1, favor, _favorShopManager.TryBuyMidnightTorch);
            AddShopItem("망나니의 큰 칼", "블록 카드에서 원하는 1칸을 제거합니다.", 2, favor, _favorShopManager.TryBuyExecutionersSword);
            AddShopItem("실학자의 서책", "가장 적게 보유한 자원의 판매 단가를 영구히 올립니다.", 3, favor, _favorShopManager.TryBuyScholarsBook);
            AddShopItem("포도청의 호통", "막힌 일꾼을 회수하고 들고 있던 자원을 수납합니다.", 2, favor, _favorShopManager.TryBuyMagistrateShout);
            AddShopItem("암행어사의 마패", "오늘 일꾼 이동 속도를 2배로 올립니다.", 3, favor, _favorShopManager.TryBuyRoyalInspectorToken);
            AddShopItem("도편수의 묘수", "선택한 타일을 즉시 1레벨 올립니다.", 3, favor, _favorShopManager.TryBuyCarpentersMasterstroke);
        }

        private void AddEdictItems(int favor)
        {
            AddShopItem("무작위 교지", "판매 규칙을 바꾸는 교지를 하나 받습니다.", 4, favor, _favorShopManager.TryBuyRandomEdict);
        }

        private void AddGiftBagItems()
        {
            AddUseGiftItem(GiftType.WarehouseFoundation, _favorShopManager.TryUseWarehouseFoundation);
            AddUseGiftItem(GiftType.WinterCold, _favorShopManager.TryUseWinterCold);
            AddUseGiftItem(GiftType.MidnightTorch, _favorShopManager.TryUseMidnightTorch);
            AddUseGiftItem(GiftType.ExecutionersSword, _favorShopManager.TryUseExecutionersSword);
            AddUseGiftItem(GiftType.ScholarsBook, _favorShopManager.TryUseScholarsBook);
            AddUseGiftItem(GiftType.MagistrateShout, _favorShopManager.TryUseMagistrateShout);
            AddUseGiftItem(GiftType.RoyalInspectorToken, _favorShopManager.TryUseRoyalInspectorToken);
            AddUseGiftItem(GiftType.CarpentersMasterstroke, _favorShopManager.TryUseCarpentersMasterstroke);
        }

        private void AddUseGiftItem(GiftType giftType, Func<bool> action)
        {
            int count = GameManager.Instance != null ? GameManager.Instance.Gifts.GetCount(giftType) : 0;
            string label = RunSystemsPanelUI.GetGiftLabel(giftType);
            string description = RunSystemsPanelUI.GetGiftDescription(giftType);
            AddShopItem(label, $"{description}\n보유: {count}", 0, count, action, count > 0 ? "발동" : "없음");
        }

        private void AddShopItem(string title, string description, int cost, int currency, Func<bool> action, string actionLabel = null)
        {
            if (_gridContainer == null)
            {
                return;
            }

            GameObject itemGo = new GameObject("Item_" + title);
            itemGo.transform.SetParent(_gridContainer, false);

            Image image = itemGo.AddComponent<Image>();
            image.color = new Color(0.16f, 0.10f, 0.055f, 1f);

            Button button = itemGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = currency >= cost;
            button.onClick.AddListener(() =>
            {
                if (action != null && action.Invoke())
                {
                    Refresh();
                }
            });

            VerticalLayoutGroup layout = itemGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI titleText = CreateText(itemGo.transform, title, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            titleText.color = new Color(1f, 0.84f, 0.22f, 1f);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 10f;
            titleText.fontSizeMax = 14f;
            titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

            string footer = actionLabel ?? $"{cost} 총애";
            TextMeshProUGUI descText = CreateText(itemGo.transform, footer, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
            descText.color = button.interactable ? Color.white : new Color(0.65f, 0.60f, 0.50f, 1f);
            descText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            itemGo.AddComponent<HoverTooltip>().SetText($"{title}\n{description}");
        }

        private Button CreateSmallButton(Transform parent, string label)
        {
            GameObject buttonGo = new GameObject("Button_" + label);
            buttonGo.transform.SetParent(parent, false);

            Image image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.20f, 0.13f, 0.07f, 1f);

            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI text = CreateText(buttonGo.transform, label, 13f, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 2f);
            textRect.offsetMax = new Vector2(-4f, -2f);
            return button;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(parent, false);
            TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private void ClearGrid()
        {
            if (_gridContainer == null)
            {
                return;
            }

            for (int i = _gridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_gridContainer.GetChild(i).gameObject);
            }
        }

        private void ClearAllChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private void OnFavorChanged(int favor) => Refresh();
        private void OnGiftCountChanged(GiftType giftType, int count) => Refresh();

        private void ResolveReferences()
        {
            if (_favorShopManager == null) _favorShopManager = GameManager.Instance != null ? GameManager.Instance.FavorShop : FindFirstObjectByType<FavorShopManager>();
            if (_tributeManager == null) _tributeManager = GameManager.Instance != null ? GameManager.Instance.Tribute : FindFirstObjectByType<TributeManager>();
        }
    }
}
