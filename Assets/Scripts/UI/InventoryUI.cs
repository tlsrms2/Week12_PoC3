using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FactoryDelivery.Core;
using FactoryDelivery.Data;
using FactoryDelivery.Resource;
using FactoryDelivery.Block;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 에디터 씬에서 직접 생성된 창고 인벤토리 패널에 부착하여 사용하는 스크립트.
    /// 자원 목록을 탐색하여 카드 UI를 내부적으로 동적 생성하고, 글로벌 인벤토리와 연동합니다.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터 설정
        // =========================================================================

        [Header("UI 참조 연결")]
        [Tooltip("생성될 자원 카드들이 배치될 부모 컨테이너 (예: VerticalLayoutGroup이 있는 Transform)")]
        [SerializeField] private Transform _itemsContainer;

        [Tooltip("총 자산 가치를 표시할 텍스트 컴포넌트")]
        [SerializeField] private TextMeshProUGUI _totalValueText;

        [Tooltip("일괄 전체 판매를 담당할 버튼 (연결 시 이벤트 자동 할당됨)")]
        [SerializeField] private Button _globalSellAllButton;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private List<ResourceDataSO> _discoveredResources = new List<ResourceDataSO>();
        private readonly Dictionary<ResourceDataSO, int> _sellAmounts = new Dictionary<ResourceDataSO, int>();
        private readonly Dictionary<ResourceDataSO, TextMeshProUGUI> _amountTexts = new Dictionary<ResourceDataSO, TextMeshProUGUI>();
        private readonly Dictionary<ResourceDataSO, TextMeshProUGUI> _inputTexts = new Dictionary<ResourceDataSO, TextMeshProUGUI>();
        private readonly Dictionary<ResourceDataSO, TextMeshProUGUI> _valueTexts = new Dictionary<ResourceDataSO, TextMeshProUGUI>();

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void Start()
        {
            if (_globalSellAllButton != null)
            {
                _globalSellAllButton.onClick.AddListener(OnGlobalSellAllClicked);
            }

            HarvestResources();

            // 인벤토리 카드 동적 생성
            if (_itemsContainer != null)
            {
                // 초기화 시 기존 테스트용 플레이스홀더 등 제거
                foreach (Transform child in _itemsContainer)
                {
                    Destroy(child.gameObject);
                }

                foreach (var res in _discoveredResources)
                {
                    CreateResourceCard(res);
                }
            }

            if (GameManager.Instance != null && GameManager.Instance.Inventory != null)
            {
                GameManager.Instance.Inventory.OnResourceChanged += OnInventoryChanged;
            }

            RefreshAllUI();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null && GameManager.Instance.Inventory != null)
            {
                GameManager.Instance.Inventory.OnResourceChanged -= OnInventoryChanged;
            }
        }

        // =========================================================================
        //  자원 탐색
        // =========================================================================

        /// <summary>
        /// 게임 내에 존재하는 자원 종류를 탐색하여 목록화합니다.
        /// </summary>
        private void HarvestResources()
        {
            _discoveredResources.Clear();

            var factory = FindFirstObjectByType<BlockFactory>();
            if (factory != null)
            {
                var resPoolField = typeof(BlockFactory).GetField("_resourceTilePool", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var facPoolField = typeof(BlockFactory).GetField("_facilityTilePool", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                List<TileDataSO> resourcePool = resPoolField != null ? (List<TileDataSO>)resPoolField.GetValue(factory) : null;
                List<TileDataSO> facilityPool = facPoolField != null ? (List<TileDataSO>)facPoolField.GetValue(factory) : null;

                if (resourcePool != null)
                {
                    foreach (var tile in resourcePool)
                    {
                        if (tile == null) continue;
                        if (tile.AssociatedResource != null && !_discoveredResources.Contains(tile.AssociatedResource))
                            _discoveredResources.Add(tile.AssociatedResource);
                    }
                }

                if (facilityPool != null)
                {
                    foreach (var tile in facilityPool)
                    {
                        if (tile == null) continue;
                        if (tile.AssociatedFacility != null)
                        {
                            foreach (var recipe in tile.AssociatedFacility.SupportedRecipes)
                            {
                                if (recipe == null) continue;
                                if (recipe.InputResource != null && !_discoveredResources.Contains(recipe.InputResource))
                                    _discoveredResources.Add(recipe.InputResource);
                                if (recipe.OutputResource != null && !_discoveredResources.Contains(recipe.OutputResource))
                                    _discoveredResources.Add(recipe.OutputResource);
                            }
                        }
                    }
                }
            }

            if (_discoveredResources.Count == 0)
            {
                var allRes = Resources.FindObjectsOfTypeAll<ResourceDataSO>();
                foreach (var r in allRes)
                {
                    if (r != null && !_discoveredResources.Contains(r))
                        _discoveredResources.Add(r);
                }
            }

            foreach (var res in _discoveredResources)
            {
                _sellAmounts[res] = 1;
            }
        }

        // =========================================================================
        //  동적 카드 생성
        // =========================================================================

        /// <summary>
        /// 특정 자원을 위한 UI 카드를 생성합니다.
        /// </summary>
        private void CreateResourceCard(ResourceDataSO resource)
        {
            var cardGo = new GameObject($"Card_{resource.DisplayName}");
            cardGo.transform.SetParent(_itemsContainer, false);
            var cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(0f, 75f); // 아담하고 아름다운 세로 폭

            // 조선 기와/목판 느낌의 약간 밝은 브라운 박스
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.20f, 0.17f, 0.14f, 0.95f);

            var cardOutline = cardGo.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.35f, 0.30f, 0.25f, 0.5f);
            cardOutline.effectDistance = new Vector2(1f, 1f);

            // 0. 자원 아이콘 영역 추가
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = resource.Icon;
            iconImg.preserveAspect = true;

            // A. 자원 이름 및 개수 영역 (아이콘 공간 확보를 위해 x 좌표 조정)
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(cardGo.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(0.6f, 1f);
            nameRect.pivot = new Vector2(0f, 0.5f);
            nameRect.anchoredPosition = new Vector2(55f, -14f);
            nameRect.sizeDelta = Vector2.zero;

            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 13;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.text = resource.DisplayName;
            nameText.alignment = TextAlignmentOptions.Left;

            var amountGo = new GameObject("AmountText");
            amountGo.transform.SetParent(cardGo.transform, false);
            var amountRect = amountGo.AddComponent<RectTransform>();
            amountRect.anchorMin = new Vector2(0f, 0.5f);
            amountRect.anchorMax = new Vector2(0.6f, 1f);
            amountRect.pivot = new Vector2(0f, 0.5f);
            amountRect.anchoredPosition = new Vector2(125f, -14f);
            amountRect.sizeDelta = Vector2.zero;

            var amountText = amountGo.AddComponent<TextMeshProUGUI>();
            amountText.fontSize = 13;
            amountText.fontStyle = FontStyles.Bold;
            amountText.color = new Color(1.0f, 0.84f, 0f); // 골드
            amountText.text = "0 개";
            amountText.alignment = TextAlignmentOptions.Left;
            _amountTexts[resource] = amountText;

            var valueGo = new GameObject("SalePreviewText");
            valueGo.transform.SetParent(cardGo.transform, false);
            var valueRect = valueGo.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0f, 0.5f);
            valueRect.anchorMax = new Vector2(0.65f, 1f);
            valueRect.pivot = new Vector2(0f, 0.5f);
            valueRect.anchoredPosition = new Vector2(55f, -34f);
            valueRect.sizeDelta = Vector2.zero;

            var valueText = valueGo.AddComponent<TextMeshProUGUI>();
            valueText.fontSize = 10;
            valueText.color = new Color(0.78f, 0.72f, 0.62f, 1f);
            valueText.text = "예상 판매가: 0 엽전";
            valueText.alignment = TextAlignmentOptions.Left;
            _valueTexts[resource] = valueText;

            // B. 수량 조절 조작 컨트롤 영역 (아이콘 공간 확보를 위해 x 좌표 조정)
            var controlPanelGo = new GameObject("ControlPanel");
            controlPanelGo.transform.SetParent(cardGo.transform, false);
            var ctrlRect = controlPanelGo.AddComponent<RectTransform>();
            ctrlRect.anchorMin = new Vector2(0f, 0f);
            ctrlRect.anchorMax = new Vector2(0.65f, 0.5f);
            ctrlRect.pivot = new Vector2(0.5f, 0.5f);
            ctrlRect.anchoredPosition = new Vector2(52f, 6f);
            ctrlRect.sizeDelta = Vector2.zero;

            Action<int> changeAmountAction = (delta) =>
            {
                int currentHold = GameManager.Instance?.Inventory?.GetAmount(resource) ?? 0;
                int prevVal = _sellAmounts[resource];
                int newVal = Mathf.Clamp(prevVal + delta, 1, Mathf.Max(1, currentHold));
                _sellAmounts[resource] = newVal;
                RefreshResourceCardUI(resource);
            };

            float xOffset = 5f;
            CreateSmallButton(controlPanelGo.transform, "-10", new Vector2(xOffset, 0f), () => changeAmountAction(-10));
            xOffset += 30f;
            CreateSmallButton(controlPanelGo.transform, "-", new Vector2(xOffset, 0f), () => changeAmountAction(-1));
            xOffset += 26f;

            var qTextGo = new GameObject("Val");
            qTextGo.transform.SetParent(controlPanelGo.transform, false);
            var qRect = qTextGo.AddComponent<RectTransform>();
            qRect.anchorMin = new Vector2(0f, 0.5f);
            qRect.anchorMax = new Vector2(0f, 0.5f);
            qRect.pivot = new Vector2(0.5f, 0.5f);
            qRect.anchoredPosition = new Vector2(xOffset + 15f, 0f);
            qRect.sizeDelta = new Vector2(30f, 20f);
            var qText = qTextGo.AddComponent<TextMeshProUGUI>();
            qText.fontSize = 11;
            qText.fontStyle = FontStyles.Bold;
            qText.color = Color.white;
            qText.text = "1";
            qText.alignment = TextAlignmentOptions.Center;
            _inputTexts[resource] = qText;
            xOffset += 30f;

            CreateSmallButton(controlPanelGo.transform, "+", new Vector2(xOffset, 0f), () => changeAmountAction(1));
            xOffset += 26f;
            CreateSmallButton(controlPanelGo.transform, "+10", new Vector2(xOffset, 0f), () => changeAmountAction(10));

            // C. 실제 판매 액션 영역
            var actionPanelGo = new GameObject("ActionPanel");
            actionPanelGo.transform.SetParent(cardGo.transform, false);
            var actRect = actionPanelGo.AddComponent<RectTransform>();
            actRect.anchorMin = new Vector2(0.65f, 0f);
            actRect.anchorMax = new Vector2(1f, 1f);
            actRect.pivot = new Vector2(0.5f, 0.5f);
            actRect.anchoredPosition = Vector2.zero;
            actRect.sizeDelta = Vector2.zero;

            var sellBtnGo = new GameObject("SellBtn");
            sellBtnGo.transform.SetParent(actionPanelGo.transform, false);
            var sellBtnRect = sellBtnGo.AddComponent<RectTransform>();
            sellBtnRect.anchorMin = new Vector2(0.1f, 0.52f);
            sellBtnRect.anchorMax = new Vector2(0.9f, 0.9f);
            sellBtnRect.sizeDelta = Vector2.zero;
            var sellBtnImg = sellBtnGo.AddComponent<Image>();
            sellBtnImg.color = new Color(0.25f, 0.45f, 0.35f, 1f);
            var sellBtn = sellBtnGo.AddComponent<Button>();
            sellBtn.onClick.AddListener(() => OnSellClicked(resource));
            AddHoverEffect(sellBtnGo, new Color(0.25f, 0.45f, 0.35f, 1f), new Color(0.32f, 0.55f, 0.42f, 1f));

            var sellTextGo = new GameObject("Text");
            sellTextGo.transform.SetParent(sellBtnGo.transform, false);
            var sellTextRect = sellTextGo.AddComponent<RectTransform>();
            sellTextRect.anchorMin = Vector2.zero;
            sellTextRect.anchorMax = Vector2.one;
            sellTextRect.sizeDelta = Vector2.zero;
            var sellText = sellTextGo.AddComponent<TextMeshProUGUI>();
            sellText.fontSize = 10;
            sellText.fontStyle = FontStyles.Bold;
            sellText.color = Color.white;
            sellText.text = "선택 판매";
            sellText.alignment = TextAlignmentOptions.Center;

            var sellAllBtnGo = new GameObject("SellAllBtn");
            sellAllBtnGo.transform.SetParent(actionPanelGo.transform, false);
            var sellAllBtnRect = sellAllBtnGo.AddComponent<RectTransform>();
            sellAllBtnRect.anchorMin = new Vector2(0.1f, 0.1f);
            sellAllBtnRect.anchorMax = new Vector2(0.9f, 0.48f);
            sellAllBtnRect.sizeDelta = Vector2.zero;
            var sellAllBtnImg = sellAllBtnGo.AddComponent<Image>();
            sellAllBtnImg.color = new Color(0.6f, 0.4f, 0.2f, 1f);
            var sellAllBtnComponent = sellAllBtnGo.AddComponent<Button>();
            sellAllBtnComponent.onClick.AddListener(() => OnSellAllClicked(resource));
            AddHoverEffect(sellAllBtnGo, new Color(0.6f, 0.4f, 0.2f, 1f), new Color(0.7f, 0.48f, 0.24f, 1f));

            var sellAllTextGo = new GameObject("Text");
            sellAllTextGo.transform.SetParent(sellAllBtnGo.transform, false);
            var sellAllTextRect = sellAllTextGo.AddComponent<RectTransform>();
            sellAllTextRect.anchorMin = Vector2.zero;
            sellAllTextRect.anchorMax = Vector2.one;
            sellAllTextRect.sizeDelta = Vector2.zero;
            var sellAllText = sellAllTextGo.AddComponent<TextMeshProUGUI>();
            sellAllText.fontSize = 10;
            sellAllText.fontStyle = FontStyles.Bold;
            sellAllText.color = Color.white;
            sellAllText.text = "전체 판매";
            sellAllText.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// 카드 내의 작은 조작 버튼을 생성합니다.
        /// </summary>
        private GameObject CreateSmallButton(Transform parent, string label, Vector2 localPos, Action onClick)
        {
            var btnGo = new GameObject($"Btn_{label}");
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = localPos;
            rect.sizeDelta = new Vector2(24f, 20f);

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.3f, 0.27f, 0.24f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            AddHoverEffect(btnGo, new Color(0.3f, 0.27f, 0.24f, 1f), new Color(0.42f, 0.38f, 0.34f, 1f));

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 9;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.text = label;
            txt.alignment = TextAlignmentOptions.Center;

            return btnGo;
        }

        /// <summary>
        /// 버튼에 호버 효과를 추가합니다.
        /// </summary>
        private void AddHoverEffect(GameObject target, Color normal, Color hover)
        {
            var btn = target.GetComponent<Button>();
            if (btn == null) return;
            
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = normal;
            cb.highlightedColor = hover;
            cb.pressedColor = new Color(normal.r * 0.7f, normal.g * 0.7f, normal.b * 0.7f, 1f);
            cb.selectedColor = normal;
            btn.colors = cb;
        }

        // =========================================================================
        //  UI 갱신 로직
        // =========================================================================

        private void OnInventoryChanged(ResourceDataSO resource, int currentAmount)
        {
            RefreshUI();
        }

        public void RefreshUI()
        {
            RefreshAllUI();
        }

        private void RefreshAllUI()
        {
            if (GameManager.Instance == null || GameManager.Instance.Inventory == null) return;

            var inventory = GameManager.Instance.Inventory;

            foreach (var res in _discoveredResources)
            {
                int count = inventory.GetAmount(res);
                
                if (_amountTexts.TryGetValue(res, out TextMeshProUGUI txt))
                {
                    txt.text = $"<b>{count}</b> 개";
                }

                int prevSell = _sellAmounts[res];
                int safeSell = Mathf.Clamp(prevSell, 1, Mathf.Max(1, count));
                _sellAmounts[res] = safeSell;

                if (_inputTexts.TryGetValue(res, out TextMeshProUGUI inputTxt))
                {
                    inputTxt.text = safeSell.ToString();
                }

                RefreshSalePreview(res, safeSell);
            }

            if (_totalValueText != null)
            {
                int totalVal = inventory.GetTotalValue();
                _totalValueText.text = $"창고 적재 자산 총액: <color=#D4AF37><b>{totalVal}</b></color> 엽전 가치";
            }
        }

        private void RefreshResourceCardUI(ResourceDataSO res)
        {
            if (_inputTexts.TryGetValue(res, out TextMeshProUGUI inputTxt))
            {
                inputTxt.text = _sellAmounts[res].ToString();
            }

            RefreshSalePreview(res, _sellAmounts[res]);
        }

        private void RefreshSalePreview(ResourceDataSO resource, int amount)
        {
            if (!_valueTexts.TryGetValue(resource, out TextMeshProUGUI valueText))
            {
                return;
            }

            QuotaManager quotaManager = FindFirstObjectByType<QuotaManager>();
            if (quotaManager == null)
            {
                valueText.text = $"예상 판매가: {resource.BaseValue * amount} 엽전";
                return;
            }

            SaleResult preview = quotaManager.PreviewSale(resource, amount);
            valueText.text = $"예상 판매가: {preview.TotalValue} 엽전  (단가 {preview.FinalUnitValue})";

            // TODO(UI): SaleResult.ModifierNotes를 툴팁/상세 패널에 표시하면
            // 어떤 어명, 시세, 밤 판매 보너스가 적용됐는지 플레이어에게 설명할 수 있다.
        }

        // =========================================================================
        //  판매 액션 핸들러
        // =========================================================================

        private void OnSellClicked(ResourceDataSO resource)
        {
            var quotaMgr = FindFirstObjectByType<QuotaManager>();
            if (quotaMgr == null || GameManager.Instance?.Inventory == null) return;

            int hold = GameManager.Instance.Inventory.GetAmount(resource);
            int sellTarget = _sellAmounts[resource];

            if (hold >= sellTarget && sellTarget > 0)
            {
                if (GameManager.Instance.Inventory.TryConsume(resource, sellTarget))
                {
                    quotaMgr.SellResource(resource, sellTarget);
                }
            }
        }

        private void OnSellAllClicked(ResourceDataSO resource)
        {
            var quotaMgr = FindFirstObjectByType<QuotaManager>();
            if (quotaMgr == null || GameManager.Instance?.Inventory == null) return;

            int hold = GameManager.Instance.Inventory.GetAmount(resource);
            if (hold > 0)
            {
                if (GameManager.Instance.Inventory.TryConsume(resource, hold))
                {
                    quotaMgr.SellResource(resource, hold);
                }
            }
        }

        private void OnGlobalSellAllClicked()
        {
            var quotaMgr = FindFirstObjectByType<QuotaManager>();
            if (quotaMgr == null || GameManager.Instance?.Inventory == null) return;

            var inventory = GameManager.Instance.Inventory;
            var itemsToSell = new List<KeyValuePair<ResourceDataSO, int>>();
            
            foreach (var kvp in inventory.Holdings)
            {
                if (kvp.Value > 0) itemsToSell.Add(kvp);
            }

            if (itemsToSell.Count == 0) return;

            foreach (var item in itemsToSell)
            {
                if (inventory.TryConsume(item.Key, item.Value))
                {
                    quotaMgr.SellResource(item.Key, item.Value);
                }
            }

            RefreshAllUI();
        }
    }
}
