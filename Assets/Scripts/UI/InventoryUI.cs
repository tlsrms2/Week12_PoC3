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
    /// 이 카드가 매핑할 단일 자원 카드의 UI 컴포넌트 참조들을 갖는 구조입니다.
    /// </summary>
    [Serializable]
    public class ResourceCardUI
    {
        [Tooltip("이 카드가 매핑할 자원 데이터")]
        public ResourceDataSO resourceData;

        [Header("UI 텍스트 & 이미지 컴포넌트")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI amountText;
        public TextMeshProUGUI inputValText;

        [Header("수량 변경 버튼")]
        public Button btnMinus10;
        public Button btnMinus1;
        public Button btnPlus1;
        public Button btnPlus10;

        [Header("판매 실행 버튼")]
        public Button btnSell;
        public Button btnSellAll;
        
        [HideInInspector]
        public GameObject cardGo; // 런타임 자동 바인딩을 위한 캐싱 필드
    }

    /// <summary>
    /// 에디터 씬에서 직접 생성된 창고 인벤토리 패널에 부착하여 사용하는 스크립트.
    /// 씬에 미리 배치되어 컴포넌트와 연결된 자원 카드들과 연동하여 글로벌 인벤토리와 연계합니다.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터 설정
        // =========================================================================

        [Header("UI 참조 연결")]
        [Tooltip("생성될 자원 카드들이 배치될 부모 컨테이너")]
        [SerializeField] private Transform _itemsContainer;

        [Tooltip("총 자산 가치를 표시할 텍스트 컴포넌트")]
        [SerializeField] private TextMeshProUGUI _totalValueText;

        [Tooltip("일괄 전체 판매를 담당할 버튼 (연결 시 이벤트 자동 할당됨)")]
        [SerializeField] private Button _globalSellAllButton;

        [Header("미리 배치된 자원 카드 목록")]
        [Tooltip("씬에 미리 생성되어 각 자원과 매핑된 카드 UI 컴포넌트들. 비워두면 ItemsContainer의 자식들을 자동으로 탐색하여 바인딩합니다.")]
        [SerializeField] private List<ResourceCardUI> _resourceCards = new List<ResourceCardUI>();

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private readonly Dictionary<ResourceDataSO, int> _sellAmounts = new Dictionary<ResourceDataSO, int>();
        private readonly Dictionary<ResourceDataSO, ResourceCardUI> _cardLookup = new Dictionary<ResourceDataSO, ResourceCardUI>();
        private List<ResourceDataSO> _discoveredResources = new List<ResourceDataSO>();

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void Start()
        {
            if (_globalSellAllButton != null)
            {
                _globalSellAllButton.onClick.AddListener(OnGlobalSellAllClicked);
            }

            InitializeCards();

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
        //  초기화 및 바인딩 로직
        // =========================================================================

        private void InitializeCards()
        {
            _cardLookup.Clear();
            _sellAmounts.Clear();

            // 1. 자동 탐색 및 바인딩 (인스펙터 목록이 비어있을 경우 fallback)
            if (_resourceCards == null || _resourceCards.Count == 0)
            {
                _resourceCards = new List<ResourceCardUI>();
                if (_itemsContainer != null)
                {
                    HarvestResources(); // 모든 등록된 자원 탐색

                    foreach (Transform child in _itemsContainer)
                    {
                        var cardGo = child.gameObject;
                        var nameTrans = child.Find("NameText");
                        if (nameTrans == null) continue;
                        
                        var nameTxt = nameTrans.GetComponent<TextMeshProUGUI>();
                        if (nameTxt == null) continue;
                        
                        string cardResourceName = nameTxt.text.Trim();
                        
                        ResourceDataSO matchingRes = null;
                        foreach (var res in _discoveredResources)
                        {
                            if (res.DisplayName == cardResourceName)
                            {
                                matchingRes = res;
                                break;
                            }
                        }
                        
                        if (matchingRes != null)
                        {
                            var autoCard = new ResourceCardUI
                            {
                                cardGo = cardGo,
                                resourceData = matchingRes
                            };
                            BindCardComponents(autoCard);
                            _resourceCards.Add(autoCard);
                        }
                        else
                        {
                            Debug.LogWarning($"[InventoryUI] 자식 UI '{cardGo.name}' (이름: '{cardResourceName}') 에 매칭되는 자원 데이터를 찾을 수 없습니다.");
                        }
                    }
                }
            }
            else
            {
                // 인스펙터에 수동 지정된 카드가 있는 경우
                foreach (var card in _resourceCards)
                {
                    if (card == null || card.resourceData == null) continue;
                    if (card.cardGo == null && card.nameText != null)
                    {
                        card.cardGo = card.nameText.transform.parent.gameObject;
                    }
                    BindCardComponents(card);
                }
            }

            // 2. 이벤트 등록 및 매핑 캐싱
            foreach (var card in _resourceCards)
            {
                if (card == null || card.resourceData == null) continue;

                var res = card.resourceData;
                _cardLookup[res] = card;
                _sellAmounts[res] = 1;

                if (card.iconImage != null && res.Icon != null)
                {
                    card.iconImage.sprite = res.Icon;
                }

                if (card.btnMinus10 != null) card.btnMinus10.onClick.AddListener(() => ChangeAmount(res, -10));
                if (card.btnMinus1 != null) card.btnMinus1.onClick.AddListener(() => ChangeAmount(res, -1));
                if (card.btnPlus1 != null) card.btnPlus1.onClick.AddListener(() => ChangeAmount(res, 1));
                if (card.btnPlus10 != null) card.btnPlus10.onClick.AddListener(() => ChangeAmount(res, 10));

                if (card.btnSell != null) card.btnSell.onClick.AddListener(() => OnSellClicked(res));
                if (card.btnSellAll != null) card.btnSellAll.onClick.AddListener(() => OnSellAllClicked(res));
            }
        }

        private void BindCardComponents(ResourceCardUI card)
        {
            if (card.cardGo == null) return;
            Transform t = card.cardGo.transform;

            if (card.nameText == null)
            {
                var nameTrans = t.Find("NameText");
                if (nameTrans != null) card.nameText = nameTrans.GetComponent<TextMeshProUGUI>();
            }
            if (card.amountText == null)
            {
                var amountTrans = t.Find("AmountText");
                if (amountTrans != null) card.amountText = amountTrans.GetComponent<TextMeshProUGUI>();
            }
            if (card.iconImage == null)
            {
                var iconTrans = t.Find("Icon");
                if (iconTrans != null) card.iconImage = iconTrans.GetComponent<Image>();
            }

            var ctrlPanel = t.Find("ControlPanel");
            if (ctrlPanel != null)
            {
                if (card.btnMinus10 == null)
                {
                    var btn = ctrlPanel.Find("Btn_-10");
                    if (btn != null) card.btnMinus10 = btn.GetComponent<Button>();
                }
                if (card.btnMinus1 == null)
                {
                    var btn = ctrlPanel.Find("Btn_-");
                    if (btn != null) card.btnMinus1 = btn.GetComponent<Button>();
                }
                if (card.btnPlus1 == null)
                {
                    var btn = ctrlPanel.Find("Btn_+");
                    if (btn != null) card.btnPlus1 = btn.GetComponent<Button>();
                }
                if (card.btnPlus10 == null)
                {
                    var btn = ctrlPanel.Find("Btn_+10");
                    if (btn != null) card.btnPlus10 = btn.GetComponent<Button>();
                }
                if (card.inputValText == null)
                {
                    var txt = ctrlPanel.Find("Val");
                    if (txt != null) card.inputValText = txt.GetComponent<TextMeshProUGUI>();
                }
            }

            var actPanel = t.Find("ActionPanel");
            if (actPanel != null)
            {
                if (card.btnSell == null)
                {
                    var btn = actPanel.Find("SellBtn");
                    if (btn != null) card.btnSell = btn.GetComponent<Button>();
                }
                if (card.btnSellAll == null)
                {
                    var btn = actPanel.Find("SellAllBtn");
                    if (btn != null) card.btnSellAll = btn.GetComponent<Button>();
                }
            }
        }

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
        }

        // =========================================================================
        //  수량 조절
        // =========================================================================

        private void ChangeAmount(ResourceDataSO resource, int delta)
        {
            int currentHold = GameManager.Instance?.Inventory?.GetAmount(resource) ?? 0;
            int prevVal = _sellAmounts.ContainsKey(resource) ? _sellAmounts[resource] : 1;
            int newVal = Mathf.Clamp(prevVal + delta, 1, Mathf.Max(1, currentHold));
            _sellAmounts[resource] = newVal;
            RefreshResourceCardUI(resource);
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

            foreach (var kvp in _cardLookup)
            {
                var res = kvp.Key;
                var card = kvp.Value;

                int count = inventory.GetAmount(res);
                if (card.amountText != null)
                {
                    card.amountText.text = $"<b>{count}</b>개";
                }

                int prevSell = _sellAmounts.ContainsKey(res) ? _sellAmounts[res] : 1;
                int safeSell = Mathf.Clamp(prevSell, 1, Mathf.Max(1, count));
                _sellAmounts[res] = safeSell;

                if (card.inputValText != null)
                {
                    card.inputValText.text = safeSell.ToString();
                }

                UpdateNameTextWithPrice(res, safeSell);
            }

            if (_totalValueText != null)
            {
                int totalVal = inventory.GetTotalValue();
                _totalValueText.text = $"창고 적재 자산 총액: <color=#D4AF37><b>{totalVal}</b></color> 엽전 가치";
            }
        }

        private void RefreshResourceCardUI(ResourceDataSO res)
        {
            if (_cardLookup.TryGetValue(res, out var card))
            {
                if (card.inputValText != null)
                {
                    card.inputValText.text = _sellAmounts[res].ToString();
                }
                UpdateNameTextWithPrice(res, _sellAmounts[res]);
            }
        }

        private void UpdateNameTextWithPrice(ResourceDataSO resource, int amount)
        {
            if (!_cardLookup.TryGetValue(resource, out var card) || card.nameText == null)
            {
                return;
            }

            int price = resource.BaseValue * amount;
            
            QuotaManager quotaManager = FindFirstObjectByType<QuotaManager>();
            if (quotaManager != null)
            {
                SaleResult preview = quotaManager.PreviewSale(resource, amount);
                price = preview.TotalValue;
            }

            // UI상의 이름에 가격 표시 (예: "벼 (10원)" 또는 "쌀 (10원)")
            // 원래 씬에 작성되어 있던 이름(벼, 콩, 쌀 가마니 등)을 최대한 존중하여 표시하기 위해,
            // resource.DisplayName 대신 원래 NameText에 들어있던 리터럴에서 가격을 파싱하기 전 이름 부분을 보존합니다.
            string baseName = resource.DisplayName;
            
            card.nameText.text = $"{baseName}({price}원)";
        }

        // =========================================================================
        //  판매 액션 핸들러
        // =========================================================================

        private void OnSellClicked(ResourceDataSO resource)
        {
            var quotaMgr = FindFirstObjectByType<QuotaManager>();
            if (quotaMgr == null || GameManager.Instance?.Inventory == null) return;

            int hold = GameManager.Instance.Inventory.GetAmount(resource);
            int sellTarget = _sellAmounts.ContainsKey(resource) ? _sellAmounts[resource] : 0;

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
