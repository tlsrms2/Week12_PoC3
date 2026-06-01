using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FactoryDelivery.Core;
using FactoryDelivery.Block;
using FactoryDelivery.Resource;
using FactoryDelivery.Utils;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 하단 블록 카드 덱을 관리하고, 엽전(골드)을 지불하여 새로운 블록들을 다시 무작위 배급받는
    /// 리롤(Reroll) 시스템과 카드 레이아웃 배치를 조율하는 매니저.
    /// </summary>
    public class BlockDeckManager : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터: 참조
        // =========================================================================

        [Header("코어 시스템 참조")]
        [SerializeField] private DayManager _dayManager;
        [SerializeField] private BlockFactory _blockFactory;
        [SerializeField] private BlockPlacer _blockPlacer;
        [SerializeField] private ResourceInventory _resourceInventory; // 엽전(골드) 보관소 겸용

        [Header("UI 프리팹 및 컨테이너")]
        [SerializeField] private BlockCardUI _cardPrefab;               // 카드 UI 원본 프리팹
        [SerializeField] private Transform _deckContainer;              // 카드가 배치될 하단 가로 레이아웃 그룹 컨테이너
        [SerializeField] private Canvas _parentCanvas;                  // 카드가 드래그될 때 최상단 정렬을 위한 메인 캔버스

        [Header("리롤 (Reroll) GUI")]
        [SerializeField] private Button _rerollButton;
        [SerializeField] private TextMeshProUGUI _rerollButtonText;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private readonly List<BlockCardUI> _activeCards = new List<BlockCardUI>();
        private int _currentRerollCost;

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void OnEnable()
        {
            if (_dayManager != null)
            {
                _dayManager.OnBlocksDistributed += PopulateDeck;
                _dayManager.OnDayStarted += ResetRerollCost;
            }

            if (_rerollButton != null)
            {
                _rerollButton.onClick.AddListener(OnRerollClicked);
            }

            ResetRerollCost(1);
        }

        private void OnDisable()
        {
            if (_dayManager != null)
            {
                _dayManager.OnBlocksDistributed -= PopulateDeck;
                _dayManager.OnDayStarted -= ResetRerollCost;
            }

            if (_rerollButton != null)
            {
                _rerollButton.onClick.RemoveListener(OnRerollClicked);
            }
        }

        private void Start()
        {
            // 초기화 레이스 컨디션 방지 가드:
            // DayManager가 OnEnable() 구독 시점보다 먼저 OnBlocksDistributed 이벤트를 날렸을 수 있음.
            // Start() 시점에 활성화된 카드가 없고, DayManager에 배급된 블록이 존재한다면 즉시 로드.
            if (_activeCards.Count == 0 && _dayManager != null && _dayManager.DistributedBlocks != null && _dayManager.DistributedBlocks.Count > 0)
            {
                PopulateDeck(new List<BlockInstance>(_dayManager.DistributedBlocks));
            }
        }

        private void Update()
        {
            // 리롤 버튼 상태 실시간 제어 (골드가 부족하면 비활성화)
            UpdateRerollButtonState();
        }

        // =========================================================================
        //  덱 관리
        // =========================================================================

        private void ResetRerollCost(int day)
        {
            _currentRerollCost = Constants.RerollBaseCost;
            UpdateRerollText();
        }

        /// <summary>
        /// 배급받은 블록 목록으로 덱을 채웁니다.
        /// </summary>
        private void PopulateDeck(List<BlockInstance> blocks)
        {
            ClearDeck();

            if (blocks == null || _cardPrefab == null || _deckContainer == null) return;

            foreach (var block in blocks)
            {
                AddCard(block);
            }

            Debug.Log($"[BlockDeckManager] 덱 카드 {_activeCards.Count}개 로드 완료.");
        }

        public bool AddCard(BlockInstance block)
        {
            if (block == null || _cardPrefab == null || _deckContainer == null)
            {
                return false;
            }

            BlockCardUI card = Instantiate(_cardPrefab, _deckContainer);
            card.Initialize(block, _blockPlacer, this, _parentCanvas);
            _activeCards.Add(card);
            return true;
        }

        /// <summary>
        /// 현재 덱의 모든 카드를 제거합니다.
        /// </summary>
        private void ClearDeck()
        {
            foreach (var card in _activeCards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }
            _activeCards.Clear();
        }

        /// <summary>
        /// 카드가 정상적으로 설치되어 필드에 올라갔을 때 덱 리스트에서 제거한다.
        /// </summary>
        public void RemoveCard(BlockCardUI card)
        {
            if (_activeCards.Contains(card))
            {
                _activeCards.Remove(card);
            }
        }

        // =========================================================================
        //  리롤 (Reroll) 로직
        // =========================================================================

        /// <summary>
        /// 리롤 버튼 클릭 시 실행됩니다.
        /// </summary>
        private void OnRerollClicked()
        {
            if (_dayManager == null || _blockFactory == null) return;

            // 엽전 부족 예외 처리
            // (PoC 자원 인벤토리에서 금이나 가치가 있는 재화 또는 실시간 수입량에서 차감 연동)
            // QuotaManager 또는 ResourceInventory의 총 자산에서 엽전 차감
            // PoC 편의성을 위해, 인벤토리의 특정 ResourceDataSO(예: 금/엽전 에셋)를 소모하거나 
            // 또는 골드 수치 변수 자체를 차감할 수 있습니다. 
            // 여기서는 QuotaManager의 실시간 실적(골드)에서 리롤 비용을 차감하거나
            // QuotaManager를 찾아서 차감 연동하는 것이 가장 직관적입니다.
            var quotaMgr = FindFirstObjectByType<QuotaManager>();
            if (quotaMgr == null) return;

            if (quotaMgr.TrySpendProgress(_currentRerollCost))
            {
                Debug.Log($"[BlockDeckManager] 엽전 {_currentRerollCost} 소모하여 리롤 실행!");

                // 리롤 비용 증가 (기하급수: 10 -> 20 -> 40 -> 80 등)
                _currentRerollCost = Mathf.RoundToInt(_currentRerollCost * Constants.RerollCostMultiplier);
                UpdateRerollText();

                // 덱에 남은 카드 수만큼 새 블록 패턴 생성하여 덮어쓰기
                int remainingCount = _activeCards.Count;
                if (remainingCount > 0)
                {
                    var newBlocks = _blockFactory.GenerateBlocksForDay(remainingCount);
                    
                    // DayManager의 _currentBlocks 리스트 갱신 (리플렉션)
                    var dayBlocksField = _dayManager.GetType().GetField("_currentBlocks", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (dayBlocksField != null)
                    {
                        dayBlocksField.SetValue(_dayManager, newBlocks);
                    }

                    // 순차 배치 인덱스 0으로 복구
                    var placementIndexField = _dayManager.GetType().GetField("_placementIndex", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (placementIndexField != null)
                    {
                        placementIndexField.SetValue(_dayManager, 0);
                    }

                    // 덱 카드 비주얼 전면 교체
                    PopulateDeck(newBlocks);
                }
            }
        }

        /// <summary>
        /// 리롤 버튼의 활성화 상태를 업데이트합니다.
        /// </summary>
        private void UpdateRerollButtonState()
        {
            if (_rerollButton == null) return;

            var quotaMgr = FindFirstObjectByType<QuotaManager>();
            if (quotaMgr == null || _dayManager == null) return;

            // 가동 페이즈이고, 카드가 남아있고, 리롤 비용을 지불할 수 있을 때만 활성화
            bool canReroll = _dayManager.CurrentPhase == DayPhase.Operation 
                && _activeCards.Count > 0 
                && quotaMgr.WalletBalance >= _currentRerollCost;

            _rerollButton.interactable = canReroll;
        }

        /// <summary>
        /// 리롤 텍스트를 현재 비용에 맞게 업데이트합니다.
        /// </summary>
        private void UpdateRerollText()
        {
            if (_rerollButtonText != null)
            {
                _rerollButtonText.text = $"개조 리롤\n({_currentRerollCost} 엽전 소모)";
            }
        }

        // =========================================================================
        //  카드 드래그 상태 콜백 (레이아웃 제어용)
        // =========================================================================

        public void OnCardDragStarted(BlockCardUI card)
        {
            // 드래그 중에는 컨테이너의 레이아웃 계산에서 빠지므로 패스 (자유로운 위치 드래그 유도)
        }

        public void OnCardDragEnded(BlockCardUI card)
        {
            // 드래그 복귀 시 레이아웃 그룹에 의해 하단 덱으로 자동 재배열됨
        }
    }
}
