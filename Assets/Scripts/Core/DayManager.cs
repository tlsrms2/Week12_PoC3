using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Block;
using FactoryDelivery.Data;
using FactoryDelivery.Events;
using FactoryDelivery.Grid;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Core
{
    /// <summary>
    /// Day 단위 4-Phase 코어 루프를 정의하는 열거형.
    /// </summary>
    public enum DayPhase
    {
        /// <summary>가동 페이즈: 할당량과 블록이 지급되고, 제한 시간 내에 목표를 달성해야 함. 일시 정지 가능.</summary>
        Operation,
        /// <summary>정산 페이즈: 할당량 달성 판정, 다음 날로 넘어가거나 게임 오버.</summary>
        Settlement
    }

    public enum DayPeriod
    {
        Day,
        Night
    }

    /// <summary>
    /// Day 단위 코어 루프를 제어하는 매니저.
    /// 가동 → 정산 순서로 진행되며,
    /// 각 페이즈 전환 시 이벤트를 발행하여 다른 시스템에 알린다.
    /// </summary>
    public class DayManager : MonoBehaviour
    {
        // =========================================================================
        //  Inspector
        // =========================================================================

        [Header("매니저 참조")]
        [SerializeField] private BlockFactory _blockFactory;
        [SerializeField] private BlockPlacer _blockPlacer;
        [SerializeField] private QuotaManager _quotaManager;
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private TributeManager _tributeManager;
        [SerializeField] private FactoryDelivery.Logistics.WorkerSpawner _workerSpawner;

        [Header("가동 페이즈 설정")]
        [Tooltip("가동 페이즈의 제한 시간 (초)")]
        [SerializeField] private float _operationTimeLimit = 60f;

        [Header("낮/밤 배율")]
        [SerializeField] private float _dayWorkerSpeedMultiplier = 1f;
        [SerializeField] private float _nightWorkerSpeedMultiplier = 1f;
        [SerializeField] private float _dayProcessingSpeedMultiplier = 1f;
        [SerializeField] private float _nightProcessingSpeedMultiplier = 1f;

        [Header("이벤트 채널")]
        [Tooltip("페이즈 전환 시 발행")]
        [SerializeField] private VoidEventChannelSO _onPhaseChanged;

        [Tooltip("새 Day 시작 시 발행 (Day 번호 페이로드)")]
        [SerializeField] private IntEventChannelSO _onDayChanged;

        [Tooltip("가동 페이즈 시작 시 발행")]
        [SerializeField] private VoidEventChannelSO _onOperationStarted;

        [Tooltip("가동 페이즈 종료 시 발행")]
        [SerializeField] private VoidEventChannelSO _onOperationEnded;

        // =========================================================================
        //  Runtime State
        // =========================================================================

        private int _currentDay = 1;
        private DayPhase _currentPhase;
        private float _timeScale = 1f;
        private bool _isPaused;
        private float _operationTimer;
        private float _operationElapsed;
        private float _permanentNightTimeBonus;
        private float _todayNightTimeBonus;
        private DayPeriod _currentPeriod = DayPeriod.Day;
        private bool _initialWarehousePlaced;

        /// <summary>배급된 블록 인스턴스들.</summary>
        private List<BlockInstance> _currentBlocks;

        /// <summary>현재 순차 배치 중인 블록의 인덱스.</summary>
        private int _placementIndex;

        // =========================================================================
        //  Properties
        // =========================================================================

        /// <summary>현재 일차.</summary>
        public int CurrentDay => _currentDay;

        /// <summary>현재 페이즈.</summary>
        public DayPhase CurrentPhase => _currentPhase;

        /// <summary>가동 페이즈 일시 정지 여부.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>가동 페이즈 배속.</summary>
        public float TimeScale => _timeScale;

        /// <summary>가동 페이즈 남은 시간.</summary>
        public float OperationTimer => _operationTimer;

        /// <summary>가동 페이즈 전체 시간.</summary>
        public float OperationTimeLimit => _operationTimeLimit + _permanentNightTimeBonus + _todayNightTimeBonus;

        public float DayTimeLimit => _operationTimeLimit;

        public float OperationElapsed => _operationElapsed;

        public float NightTimeLimit => _permanentNightTimeBonus + _todayNightTimeBonus;

        public DayPeriod CurrentPeriod => _currentPeriod;

        public bool IsNight => _currentPhase == DayPhase.Operation && _currentPeriod == DayPeriod.Night;

        public float RemainingDayTime => Mathf.Max(0f, _operationTimeLimit - _operationElapsed);

        public float RemainingNightTime => IsNight ? _operationTimer : Mathf.Max(0f, OperationTimeLimit - _operationTimeLimit);

        public float WorkerSpeedMultiplier
        {
            get
            {
                float baseMult = IsNight ? _nightWorkerSpeedMultiplier : _dayWorkerSpeedMultiplier;
                float augmentMult = (MandateManager.Instance != null)
                    ? MandateManager.Instance.GetWorkerSpeedMultiplier(_currentPeriod)
                    : 1f;
                return baseMult * augmentMult;
            }
        }

        public float ProcessingSpeedMultiplier
        {
            get
            {
                float baseMult = IsNight ? _nightProcessingSpeedMultiplier : _dayProcessingSpeedMultiplier;
                float augmentMult = (MandateManager.Instance != null)
                    ? MandateManager.Instance.GetProcessingSpeedMultiplier(_currentPeriod)
                    : 1f;
                return baseMult * augmentMult;
            }
        }

        /// <summary>현재 배급된 블록 목록.</summary>
        public IReadOnlyList<BlockInstance> DistributedBlocks => _currentBlocks;

        // =========================================================================
        //  Events
        // =========================================================================

        /// <summary>페이즈가 전환될 때 발생. 페이로드는 새 페이즈.</summary>
        public event Action<DayPhase> OnPhaseChanged;

        /// <summary>Day가 시작될 때 발생. 페이로드는 Day 번호.</summary>
        public event Action<int> OnDayStarted;

        /// <summary>블록이 배급되었을 때 발생.</summary>
        public event Action<List<BlockInstance>> OnBlocksDistributed;

        public event Action<DayPeriod> OnDayPeriodChanged;

        // =========================================================================
        //  Unity Lifecycle & PoC Debug Controls
        // =========================================================================

        private void Start()
        {
            if (_blockPlacer != null)
            {
                _blockPlacer.OnBlockPlaced += OnBlockPlacedHandler;
            }
        }

        private void OnDestroy()
        {
            if (_blockPlacer != null)
            {
                _blockPlacer.OnBlockPlaced -= OnBlockPlacedHandler;
            }
        }

        private void Update()
        {
            // 가동 페이즈 타이머 처리
            if (_currentPhase == DayPhase.Operation && !_isPaused)
            {
                _operationTimer -= Time.deltaTime;
                _operationElapsed += Time.deltaTime;
                RefreshDayPeriod();

                if (_operationTimer <= 0f)
                {
                    _operationTimer = 0f;
                    EndOperation();
                }
            }

            // 배속 단축키 지원 (1, 2, 3)
            if (_currentPhase == DayPhase.Operation)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SetTimeScale(1f);
                if (Input.GetKeyDown(KeyCode.Alpha2)) SetTimeScale(2f);
                if (Input.GetKeyDown(KeyCode.Alpha3)) SetTimeScale(3f);
            }
        }

        private void OnBlockPlacedHandler(BlockInstance placedBlock)
        {
            if (_currentPhase != DayPhase.Operation) return;

            // 배치 후 인덱스 증가
            _placementIndex++;
        }

        // =========================================================================
        //  Phase Flow
        // =========================================================================

        /// <summary>
        /// 새로운 Day를 시작한다. 가동 페이즈로 바로 진입한다.
        /// </summary>
        public void StartDay()
        {
            Debug.Log($"[DayManager] ===== Day {_currentDay} 시작 =====");

            if (_workerSpawner != null) _workerSpawner.ResetDailyBonuses();

            _onDayChanged?.RaiseEvent(_currentDay);
            OnDayStarted?.Invoke(_currentDay);

            EnterOperationPhase();
        }

        /// <summary>
        /// 가동 페이즈를 종료하고 정산 페이즈로 진입한다.
        /// 시간 종료 또는 UI 버튼에서 호출한다.
        /// </summary>
        public void EndOperation()
        {
            if (_currentPhase != DayPhase.Operation)
            {
                Debug.LogWarning("[DayManager] 가동 페이즈가 아닌데 EndOperation이 호출되었습니다.");
                return;
            }

            EnterSettlementPhase();
        }

        /// <summary>
        /// 정산 완료 후 다음 Day로 진행한다.
        /// UI 버튼에서 호출한다.
        /// </summary>
        public void ProceedToNextDay()
        {
            if (_currentPhase != DayPhase.Settlement)
            {
                Debug.LogWarning("[DayManager] 정산 페이즈가 아닌데 ProceedToNextDay가 호출되었습니다.");
                return;
            }

            _currentDay++;
            StartDay();
        }

        // =========================================================================
        //  Time Control (가동 페이즈)
        // =========================================================================

        /// <summary>
        /// 가동 페이즈의 배속을 설정한다.
        /// </summary>
        /// <param name="scale">1, 2, 또는 3.</param>
        public void SetTimeScale(float scale)
        {
            _timeScale = Mathf.Clamp(scale, 1f, 3f);

            if (_currentPhase == DayPhase.Operation && !_isPaused)
            {
                Time.timeScale = _timeScale;
            }
        }

        /// <summary>
        /// 가동 페이즈의 일시 정지를 토글한다.
        /// </summary>
        public void TogglePause()
        {
            if (_currentPhase != DayPhase.Operation) return;

            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : _timeScale;

            Debug.Log($"[DayManager] {(_isPaused ? "일시 정지" : $"재개 (x{_timeScale})")}");
        }

        public void AddNightTime(float seconds, bool permanent)
        {
            if (seconds <= 0f)
            {
                return;
            }

            if (permanent)
            {
                _permanentNightTimeBonus += seconds;
            }
            else
            {
                _todayNightTimeBonus += seconds;
            }

            if (_currentPhase == DayPhase.Operation)
            {
                _operationTimer += seconds;
                RefreshDayPeriod();
            }
        }

        public void SetDayNightMultipliers(
            float dayWorker,
            float nightWorker,
            float dayProcessing,
            float nightProcessing)
        {
            _dayWorkerSpeedMultiplier = Mathf.Max(0.01f, dayWorker);
            _nightWorkerSpeedMultiplier = Mathf.Max(0.01f, nightWorker);
            _dayProcessingSpeedMultiplier = Mathf.Max(0.01f, dayProcessing);
            _nightProcessingSpeedMultiplier = Mathf.Max(0.01f, nightProcessing);
        }

        // =========================================================================
        //  Phase Implementations
        // =========================================================================

        /// <summary>
        /// 가동 페이즈: 시간이 흐르고 일꾼이 이동한다. 제한 시간이 존재하며 일시정지 상태로 시작.
        /// </summary>
        private void EnterOperationPhase()
        {
            TransitionToPhase(DayPhase.Operation);

            // 초기 상태는 일시정지
            _isPaused = true;
            Time.timeScale = 0f;
            _todayNightTimeBonus = 0f;
            _operationElapsed = 0f;
            _operationTimer = OperationTimeLimit;
            _currentPeriod = DayPeriod.Day;

            _placementIndex = 0; // 배치 인덱스 초기화

            // 할당량 계산
            if (_quotaManager != null)
            {
                _quotaManager.StartNewDay(_currentDay);
            }

            TributeManager tributeManager = _tributeManager != null
                ? _tributeManager
                : GameManager.Instance != null ? GameManager.Instance.Tribute : FindFirstObjectByType<TributeManager>();
            if (tributeManager != null)
            {
                tributeManager.StartNewDay(_currentDay);
            }

            EnsureInitialWarehouse();

            // 블록 배급
            if (_blockFactory != null)
            {
                _currentBlocks = _blockFactory.GenerateBlocksForDay(Constants.BlocksPerDay, false);
                OnBlocksDistributed?.Invoke(_currentBlocks);
                Debug.Log($"[DayManager] 블록 {_currentBlocks.Count}개 배급 완료.");
            }

            // 가동 페이즈 시작 시 모든 숙소에서 일꾼 생성 스폰
            if (_workerSpawner != null)
            {
                var houses = FindObjectsByType<FactoryDelivery.Facility.WorkerHouse>(FindObjectsSortMode.None);
                foreach (var house in houses)
                {
                    if (house != null && house.IsActive)
                    {
                        _workerSpawner.SpawnWorkersForHouse(house);
                    }
                }
                Debug.Log($"[DayManager] 가동 페이즈 시작: 활성화된 숙소 {houses.Length}개에서 일꾼을 정식 스폰했습니다.");
            }

            _onOperationStarted?.RaiseEvent();

            Debug.Log($"[DayManager] 가동 페이즈 진입. 제한 시간 {_operationTimeLimit}초. 일시정지 상태. '재개' 버튼으로 시작하세요.");
        }

        /// <summary>
        /// 정산 페이즈: 할당량 달성 여부를 판정한다.
        /// </summary>
        private void EnterSettlementPhase()
        {
            TransitionToPhase(DayPhase.Settlement);

            Time.timeScale = 0f; // 정산 중 시간 정지

            // 정산 페이즈 진입 시 가동 완료된 모든 일꾼을 회수하여 소멸 처리
            if (_workerSpawner != null)
            {
                var houses = FindObjectsByType<FactoryDelivery.Facility.WorkerHouse>(FindObjectsSortMode.None);
                foreach (var house in houses)
                {
                    if (house != null)
                    {
                        _workerSpawner.DespawnWorkersForHouse(house);
                    }
                }
                Debug.Log("[DayManager] 정산 페이즈 진입: 모든 일꾼을 안전하게 회수하고 비활성화했습니다.");
            }

            _onOperationEnded?.RaiseEvent();

            if (_quotaManager != null &&
                EdictManager.Instance != null &&
                EdictManager.Instance.HasEdict(EdictType.HojoInventoryClearing))
            {
                int autoSoldValue = _quotaManager.ExecuteSettlementAutoSales();
                if (autoSoldValue > 0)
                {
                    Debug.Log($"[DayManager] [호조의 재고 정리] 정산 직전 원자재를 자동 판매하여 {autoSoldValue} 엽전을 확보했습니다.");
                }
            }

            // 할당량 판정
            if (_quotaManager != null)
            {
                if (_quotaManager.IsQuotaMet)
                {
                    int surplus = _quotaManager.GetSurplus();
                    Debug.Log($"[DayManager] 할당량 달성! 잉여: {surplus}. '다음 날' 버튼으로 진행하세요.");
                }
                else
                {
                    Debug.Log($"[DayManager] 할당량 미달! " +
                              $"({_quotaManager.CurrentProgress}/{_quotaManager.CurrentQuota}). 게임 오버.");

                    // 게임 오버 처리
                    if (GameManager.Instance != null)
                    {
                        int metaCurrency = _currentDay; // Day 수에 비례한 메타 재화 (간이 공식)
                        GameManager.Instance.EndGame(_currentDay, metaCurrency);
                    }
                }
            }
        }

        private void EnsureInitialWarehouse()
        {
            if (_initialWarehousePlaced || _currentDay != 1 || _blockFactory == null)
            {
                return;
            }

            GridManager gridManager = _gridManager != null ? _gridManager : FindFirstObjectByType<GridManager>();
            if (gridManager == null)
            {
                Debug.LogWarning("[DayManager] 중앙 창고를 배치할 GridManager를 찾지 못했습니다.");
                return;
            }

            TileDataSO warehouseTile = _blockFactory.WarehouseTileData;
            if (warehouseTile == null)
            {
                Debug.LogWarning("[DayManager] 중앙 창고 배치에 사용할 창고 타일 데이터가 없습니다.");
                return;
            }

            int warehouseSize = 2;
            int start = Mathf.FloorToInt((Constants.LandPlotSize - warehouseSize) * 0.5f);
            Vector2Int origin = new Vector2Int(start, start);
            bool placedAny = false;

            for (int x = 0; x < warehouseSize; x++)
            {
                for (int y = 0; y < warehouseSize; y++)
                {
                    Vector2Int pos = origin + new Vector2Int(x, y);
                    if (gridManager.GetTileAt(pos) != null)
                    {
                        continue;
                    }

                    if (gridManager.TryPlaceTile(pos, warehouseTile, out _))
                    {
                        placedAny = true;
                    }
                }
            }

            _initialWarehousePlaced = true;
            Debug.Log(placedAny
                ? "[DayManager] 시작 중앙 2x2 창고를 자동 배치했습니다."
                : "[DayManager] 중앙 창고 위치가 이미 점유되어 있어 추가 배치하지 않았습니다.");
        }

        // ─────────────────────────────────────────────
        //  Internal
        // ─────────────────────────────────────────────

        /// <summary>
        /// 페이즈를 전환하고 이벤트를 발행한다.
        /// </summary>
        private void TransitionToPhase(DayPhase newPhase)
        {
            DayPhase previous = _currentPhase;
            _currentPhase = newPhase;

            Debug.Log($"[DayManager] 페이즈 전환: {previous} → {newPhase}");

            _onPhaseChanged?.RaiseEvent();
            OnPhaseChanged?.Invoke(newPhase);
        }

        private void RefreshDayPeriod()
        {
            DayPeriod nextPeriod = _operationElapsed >= _operationTimeLimit
                ? DayPeriod.Night
                : DayPeriod.Day;

            if (_currentPeriod == nextPeriod)
            {
                return;
            }

            _currentPeriod = nextPeriod;
            OnDayPeriodChanged?.Invoke(_currentPeriod);
            Debug.Log($"[DayManager] 시간대 전환: {_currentPeriod}");
        }
    }
}
