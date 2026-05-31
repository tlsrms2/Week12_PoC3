using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Core;
using FactoryDelivery.Data;
using FactoryDelivery.Facility;
using FactoryDelivery.Grid;
using FactoryDelivery.Resource;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Logistics
{
    /// <summary>
    /// 일꾼의 현재 행동 상태를 나타내는 열거형입니다.
    /// </summary>
    public enum WorkerState
    {
        Idle,               // 대기 중
        MovingToPickup,     // 자원을 가지러 이동 중
        PickingUp,          // 자원을 줍는 중
        Carrying,           // 자원을 운반 중
        Delivering,         // 자원을 전달 중
        Returning,          // 복귀 중
        Processing          // 시설 내에서 가공 중
    }

    /// <summary>
    /// 그리드 상에서 자원을 수집하고 운반하는 일꾼 유닛입니다.
    /// 도로를 따라 이동하며 자원 및 시설과 상호작용합니다.
    /// </summary>
    public class Worker : MonoBehaviour, IPoolable
    {
        [Header("비주얼")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private WorkerDataSO _workerData;

        [Header("가공 진행 바")]
        private GameObject _progressBarGo;
        private SpriteRenderer _progressBg;
        private SpriteRenderer _progressFill;

        private float _baseSpeed;
        private float _currentSpeed;
        private GridManager _gridManager;
        private readonly HashSet<Vector2Int> _harvestedResourcePositions = new HashSet<Vector2Int>();
        private readonly List<SpriteRenderer> _carriedVisuals = new List<SpriteRenderer>();

        private ProcessingFacility _activeProcessingFacility;
        private RecipeDataSO _activeProcessingRecipe;
        private float _processingTimer;
        private float _processingDuration;

        /// <summary>일꾼이 현재 위치한 도로 타일입니다.</summary>
        public RoadTile CurrentRoad { get; private set; }
        private Vector3 _moveTarget;
        public Vector3 MoveTarget => _moveTarget;
        private Vector2Int _lastMoveDir = Vector2Int.down;
        private bool _hasReachedCurrentRoadCenter;

        /// <summary>일꾼의 현재 상태입니다.</summary>
        public WorkerState CurrentState { get; private set; } = WorkerState.Idle;
        
        /// <summary>현재 운반 중인 자원들의 목록입니다.</summary>
        public List<ResourceDataSO> CarriedResources { get; } = new List<ResourceDataSO>();
        
        /// <summary>현재 운반 중인 대표 자원(첫 번째 자원)입니다.</summary>
        public ResourceDataSO CarriedResource => CarriedResources.Count > 0 ? CarriedResources[0] : null;
        
        /// <summary>일꾼이 소속된 숙소입니다.</summary>
        public WorkerHouse HomeHouse { get; set; }

        private float BaseSpeed => _workerData != null ? _workerData.EffectiveBaseSpeed : Constants.WorkerBaseSpeed;
        private float WorkerMinDistance => _workerData != null ? _workerData.EffectiveMinDistance : Constants.WorkerMinDistance;
        private int CarryCapacity => _workerData != null ? _workerData.EffectiveCarryCapacity : 1;
        private float WorkerScale => _workerData != null ? _workerData.EffectiveWorkerScale : 1.2f;
        private float CarriedItemScale => _workerData != null ? _workerData.CarriedItemScale : 0.32f;
        private Vector2 CarriedItemBaseOffset => _workerData != null ? _workerData.CarriedItemBaseOffset : new Vector2(0f, 0.34f);
        private float CarriedItemStackOffset => _workerData != null ? _workerData.CarriedItemStackOffset : 0.18f;

        /// <summary>일꾼이 작업을 마쳤을 때 발생하는 이벤트입니다.</summary>
        public event Action<Worker> OnTaskCompleted;

        private void Awake()
        {
            _baseSpeed = BaseSpeed;
            _currentSpeed = _baseSpeed;

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            transform.localScale = new Vector3(WorkerScale, WorkerScale, 1f);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 20;
            }

            _gridManager = FindAnyObjectByType<GridManager>();
            CreateProgressBar();
        }

        private void CreateProgressBar()
        {
            if (_progressBarGo != null) return;

            _progressBarGo = new GameObject("WorkerProgressBar");
            _progressBarGo.transform.SetParent(transform);
            _progressBarGo.transform.localPosition = new Vector3(0f, 0.7f, -0.6f); // 약간 더 높게, 더 앞으로
            _progressBarGo.SetActive(false);

            // 흰색 텍스처 동적 생성 (Resources.Load 실패 대비)
            Texture2D whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
            Sprite barSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(_progressBarGo.transform, false);
            bg.transform.localScale = new Vector3(0.6f, 0.1f, 1f);
            _progressBg = bg.AddComponent<SpriteRenderer>();
            _progressBg.sprite = barSprite;
            _progressBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            _progressBg.sortingOrder = 100; // 충분히 높은 순서

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(_progressBarGo.transform, false);
            fill.transform.localScale = new Vector3(0.56f, 0.06f, 1f);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _progressFill = fill.AddComponent<SpriteRenderer>();
            _progressFill.sprite = barSprite;
            _progressFill.color = new Color(0.2f, 1f, 0.2f, 1f);
            _progressFill.sortingOrder = 101;
        }

        private void Update()
        {
            if (CurrentState == WorkerState.Idle)
            {
                return;
            }

            if (CurrentState == WorkerState.Processing)
            {
                UpdateProcessing();
                return;
            }

            MoveAlongConveyorRoad();
        }

        /// <summary>
        /// 도로 네트워크를 따른 컨베이어 흐름 이동을 시작합니다.
        /// </summary>
        /// <param name="startRoad">시작할 도로 타일입니다.</param>
        public void StartConveyorFlow(RoadTile startRoad)
        {
            _baseSpeed = BaseSpeed;
            _currentSpeed = _baseSpeed;
            CarriedResources.Clear();
            _harvestedResourcePositions.Clear();
            ClearProcessingState();

            SetCurrentRoad(startRoad);

            if (startRoad != null)
            {
                Vector3 startPos = GetRoadTargetPosition(startRoad);
                startPos.z = -1f;
                transform.position = startPos;
            }

            TransitionTo(WorkerState.MovingToPickup);
        }

        /// <summary>
        /// 일꾼에게 특정 작업을 할당합니다. (PoC 버전에서는 현재 미사용)
        /// </summary>
        public void AssignTask(
            List<Vector3> pathToPickup,
            Component pickupTarget,
            List<Vector3> pathToDelivery,
            Component deliveryTarget,
            List<Vector3> pathHome)
        {
        }

        /// <summary>
        /// 오브젝트 풀에서 꺼내질 때 호출되어 상태를 초기화합니다.
        /// </summary>
        public void OnSpawnFromPool()
        {
            _baseSpeed = BaseSpeed;
            _currentSpeed = _baseSpeed;
            CurrentState = WorkerState.Idle;
            CarriedResources.Clear();
            _harvestedResourcePositions.Clear();
            SetCurrentRoad(null);
            HomeHouse = null;
            ClearProcessingState();

            transform.localScale = new Vector3(WorkerScale, WorkerScale, 1f);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 20;
            }

            UpdateVisual();
        }

        /// <summary>
        /// 오브젝트 풀로 돌아갈 때 호출되어 리소스를 정리합니다.
        /// </summary>
        public void OnReturnToPool()
        {
            CurrentState = WorkerState.Idle;
            SetCurrentRoad(null);
            HomeHouse = null;
            ClearProcessingState();

            foreach (SpriteRenderer renderer in _carriedVisuals)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }

            _carriedVisuals.Clear();
            CarriedResources.Clear();
            _harvestedResourcePositions.Clear();
        }

        /// <summary>
        /// 일꾼 데이터를 설정하고 능력치를 갱신합니다.
        /// </summary>
        public void ConfigureData(WorkerDataSO workerData)
        {
            _workerData = workerData;
            _baseSpeed = BaseSpeed;
            _currentSpeed = _baseSpeed;
            transform.localScale = new Vector3(WorkerScale, WorkerScale, 1f);
        }

        /// <summary>
        /// 도로를 따라 이동을 처리합니다.
        /// </summary>
        private void MoveAlongConveyorRoad()
        {
            if (CurrentRoad == null)
            {
                TransitionTo(WorkerState.Idle);
                return;
            }

            Vector3 target = _moveTarget;
            Vector3 diff = target - transform.position;
            Vector3 direction = diff.normalized;

            // 마지막 이동 방향 업데이트 (스프라이트 결정용)
            if (diff.magnitude > 0.01f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                if (angle > 45 && angle <= 135) _lastMoveDir = Vector2Int.up;
                else if (angle > -45 && angle <= 45) _lastMoveDir = Vector2Int.right;
                else if (angle > -135 && angle <= -45) _lastMoveDir = Vector2Int.down;
                else _lastMoveDir = Vector2Int.left;

                UpdateVisual();
            }

            float effectiveSpeed = CalculateEffectiveSpeed(direction);

            Vector2Int currentGridPos = transform.position.ToGridPosition();
            bool isOnRoad = false;
            if (_gridManager != null)
            {
                TileEntity roadTile = _gridManager.GetRoadTileAt(currentGridPos);
                if (roadTile != null && roadTile.IsRoad)
                {
                    isOnRoad = true;
                }
            }

            // 도로 위에서는 정상 속도, 아닐 경우 25% 속도로 이동
            float speedMultiplier = isOnRoad ? 1f : 0.25f;
            float finalSpeed = effectiveSpeed * speedMultiplier;

            Vector3 nextPos = Vector3.MoveTowards(
                transform.position,
                target,
                finalSpeed * Time.deltaTime);
            nextPos.z = -1f;
            transform.position = nextPos;

            if (Vector3.Distance(transform.position, target) >= 0.05f)
            {
                return;
            }

            if (!_hasReachedCurrentRoadCenter && IsCurveRoad(CurrentRoad))
            {
                _hasReachedCurrentRoadCenter = true;
                _moveTarget = GetRoadTargetPosition(CurrentRoad);
                _moveTarget.z = -1f;
                return;
            }

            // 주변 상호작용 지점 처리
            ProcessAdjacentInteractions();

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            // 다음 도로 타일로 전진 시도
            RoadTile nextRoad = CurrentRoad.NextRoad;
            if (nextRoad == null)
            {
                TransitionTo(WorkerState.Idle);
                return;
            }

            if (!CanAdvanceTo(nextRoad))
            {
                SetCurrentRoad(CurrentRoad);
                return;
            }

            _hasReachedCurrentRoadCenter = false;
            SetCurrentRoad(nextRoad);

            if (CurrentState == WorkerState.Idle)
            {
                TransitionTo(CarriedResource != null ? WorkerState.Carrying : WorkerState.MovingToPickup);
            }
        }

        /// <summary>
        /// 시설 내 가공 진행 상태를 업데이트합니다.
        /// </summary>
        private void UpdateProcessing()
        {
            if (_activeProcessingFacility == null || _activeProcessingRecipe == null)
            {
                ClearProcessingState();
                TransitionTo(CarriedResources.Count > 0 ? WorkerState.Carrying : WorkerState.MovingToPickup);
                return;
            }

            _processingTimer -= Time.deltaTime;
            _activeProcessingFacility.UpdateProcessingProgress(this, _processingTimer, _processingDuration);

            // 진행 바 업데이트
            if (_progressBarGo != null)
            {
                _progressBarGo.SetActive(true);
                float progress = 1f - Mathf.Clamp01(_processingTimer / _processingDuration);
                _progressFill.transform.localScale = new Vector3(0.56f * progress, 0.06f, 1f);
                _progressFill.transform.localPosition = new Vector3(-0.28f * (1f - progress), 0f, -0.01f);
            }

            if (_processingTimer > 0f)
            {
                return;
            }

            // 가공 완료 처리
            ProcessingFacility facility = _activeProcessingFacility;
            ResourceDataSO output = facility.CompleteProcessing(this);
            if (output != null)
            {
                CarriedResources.Add(output);
            }

            ClearProcessingState();
            RecalculateCarrySpeed();
            UpdateVisual();

            // 같은 시설에서 연속 가공 가능한지 확인
            ResourceDataSO nextInput = FindSupportedInput(facility);
            if (nextInput != null && StartProcessingAtFacility(facility, nextInput))
            {
                return;
            }

            TransitionTo(CarriedResources.Count > 0 ? WorkerState.Carrying : WorkerState.MovingToPickup);
            TryAdvanceAfterProcessing();
        }

        /// <summary>
        /// 현재 위치 주변의 자원이나 시설과 상호작용합니다.
        /// </summary>
        private void ProcessAdjacentInteractions()
        {
            if (_gridManager == null)
            {
                return;
            }

            Vector2Int myGridPos = transform.position.ToGridPosition();
            if (!_gridManager.IsInBounds(myGridPos))
            {
                return;
            }

            TileEntity entity = _gridManager.GetTileAt(myGridPos);
            if (entity == null)
            {
                return;
            }

            // 자원 타일인 경우 수집 시도
            if (entity.IsResource && TryPickupResourceAt(myGridPos))
            {
                return;
            }

            // 시설 타일인 경우 상호작용 시도
            if (!entity.IsFacility)
            {
                return;
            }

            FacilityBase facility = FindComponentAtGrid<FacilityBase>(myGridPos);
            if (facility != null)
            {
                TryInteractWithFacility(facility);
            }
        }

        /// <summary>
        /// 특정 좌표에서 자원 수집을 시도합니다.
        /// </summary>
        private bool TryPickupResourceAt(Vector2Int gridPos)
        {
            if (CarriedResources.Count >= CarryCapacity || _harvestedResourcePositions.Contains(gridPos))
            {
                return false;
            }

            ResourceTile resourceTile = FindComponentAtGrid<ResourceTile>(gridPos);
            if (resourceTile == null || !resourceTile.HasResource)
            {
                return false;
            }

            ResourceDataSO harvested = resourceTile.PickupResource();
            if (harvested == null)
            {
                return false;
            }

            CarriedResources.Add(harvested);
            _harvestedResourcePositions.Add(gridPos);
            RecalculateCarrySpeed();
            TransitionTo(WorkerState.Carrying);
            return true;
        }

        /// <summary>
        /// 특정 시설과 상호작용(납품, 가공, 배출)을 시도합니다.
        /// </summary>
        private bool TryInteractWithFacility(FacilityBase facility)
        {
            // 창고인 경우 납품 처리 (빈손이라도 창고에 도달하면 소멸)
            if (facility is Warehouse)
            {
                if (CarriedResources.Count > 0 && GameManager.Instance != null)
                {
                    foreach (ResourceDataSO res in CarriedResources)
                    {
                        GameManager.Instance.Inventory.Add(res, 1);
                    }
                }

                CarriedResources.Clear();
                DespawnSelf();
                return true;
            }

            // 가공 시설인 경우 가공 시작 시도
            if (facility is ProcessingFacility processingFacility)
            {
                ResourceDataSO input = FindSupportedInput(processingFacility);
                if (input == null)
                {
                    return false;
                }

                return StartProcessingAtFacility(processingFacility, input);
            }

            // 일반 시설에 자원 투입 시도
            if (CarriedResources.Count > 0 && facility.CanAcceptInput)
            {
                ResourceDataSO inputTarget = FindSupportedInput(facility);
                if (inputTarget != null)
                {
                    facility.ReceiveResource(inputTarget);
                    CarriedResources.Remove(inputTarget);

                    if (CarriedResources.Count == 0)
                    {
                        _currentSpeed = _baseSpeed;
                        TransitionTo(WorkerState.MovingToPickup);
                    }
                    else
                    {
                        RecalculateCarrySpeed();
                        TransitionTo(WorkerState.Carrying);
                    }

                    return true;
                }
            }

            // 시설에서 생산된 자원이 있으면 수령 시도
            if (CarriedResources.Count < CarryCapacity && facility.HasOutput)
            {
                ResourceDataSO output = facility.PickupOutput();
                if (output != null)
                {
                    CarriedResources.Add(output);
                    RecalculateCarrySpeed();
                    TransitionTo(WorkerState.Carrying);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 가공 시설에서 가공 작업을 시작합니다.
        /// </summary>
        private bool StartProcessingAtFacility(ProcessingFacility facility, ResourceDataSO input)
        {
            if (facility == null || input == null)
            {
                return false;
            }

            if (!facility.TryBeginProcessing(this, input, out RecipeDataSO recipe, out float duration))
            {
                return false;
            }

            CarriedResources.Remove(input);
            RecalculateCarrySpeed();

            _activeProcessingFacility = facility;
            _activeProcessingRecipe = recipe;
            _processingDuration = duration;
            _processingTimer = duration;
            facility.UpdateProcessingProgress(this, _processingTimer, _processingDuration);
            TransitionTo(WorkerState.Processing);
            return true;
        }

        /// <summary>
        /// 해당 시설에서 지원하는 레시피의 입력 자원을 현재 소지품에서 찾습니다.
        /// </summary>
        private ResourceDataSO FindSupportedInput(FacilityBase facility)
        {
            if (facility == null)
            {
                return null;
            }

            foreach (ResourceDataSO resource in CarriedResources)
            {
                if (IsRecipeSupported(facility, resource))
                {
                    return resource;
                }
            }

            return null;
        }

        /// <summary>
        /// 시설에서 특정 자원을 소모하는 레시피를 지원하는지 확인합니다.
        /// </summary>
        private bool IsRecipeSupported(FacilityBase facility, ResourceDataSO resource)
        {
            if (facility is ProcessingFacility processingFacility)
            {
                return processingFacility.FindRecipeFor(resource) != null;
            }

            if (facility.FacilityData == null || facility.FacilityData.SupportedRecipes == null)
            {
                return false;
            }

            foreach (RecipeDataSO recipe in facility.FacilityData.SupportedRecipes)
            {
                if (recipe != null && recipe.InputResource == resource)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 다음 도로 타일로 진행 가능한지 확인합니다. (가공 시설 진입 가능 여부 등 체크)
        /// </summary>
        private bool CanAdvanceTo(RoadTile nextRoad)
        {
            if (nextRoad == null)
            {
                return false;
            }

            ProcessingFacility nextFacility = FindComponentAtGrid<ProcessingFacility>(nextRoad.GridPosition);
            if (nextFacility == null)
            {
                return true;
            }

            if (FindSupportedInput(nextFacility) == null)
            {
                return true;
            }

            if (!nextFacility.CanWorkerEnter(this))
            {
                return false;
            }

            // 진입 가능하면 예약하여 다른 일꾼이 겹쳐 들어오지 못하게 함
            nextFacility.Reserve(this);
            return true;
        }

        private void TryAdvanceAfterProcessing()
        {
            if (CurrentRoad == null)
            {
                SetCurrentRoad(CurrentRoad);
                return;
            }

            _hasReachedCurrentRoadCenter = false;
            _moveTarget = CurrentRoad.GetWorldPosition();
            _moveTarget.z = -1f;
        }

        /// <summary>
        /// 특정 그리드 좌표에 위치한 컴포넌트를 탐색합니다.
        /// </summary>
        private T FindComponentAtGrid<T>(Vector2Int gridPos) where T : Component
        {
            T[] allComponents = FindObjectsByType<T>(FindObjectsSortMode.None);
            foreach (T comp in allComponents)
            {
                if (comp == null)
                {
                    continue;
                }

                if (comp is ResourceTile resTile)
                {
                    if (resTile.GridPosition == gridPos)
                    {
                        return comp;
                    }
                }
                else if (comp is FacilityBase facTile)
                {
                    if (facTile.GridPosition == gridPos)
                    {
                        return comp;
                    }
                }
                else if (comp.transform.position.ToGridPosition() == gridPos)
                {
                    return comp;
                }
            }

            return null;
        }

        /// <summary>
        /// 운반 중인 자원의 무게에 따라 이동 속도를 재계산합니다.
        /// </summary>
        private void RecalculateCarrySpeed()
        {
            float totalWeight = 0f;
            foreach (ResourceDataSO res in CarriedResources)
            {
                totalWeight += res.WeightMultiplier;
            }

            _currentSpeed = CarriedResources.Count == 0
                ? _baseSpeed
                : _baseSpeed / Mathf.Max(1f, totalWeight - (CarriedResources.Count - 1) * 0.5f);
        }

        /// <summary>
        /// 가공 진행 상태를 초기화합니다.
        /// </summary>
        private void ClearProcessingState()
        {
            if (_activeProcessingFacility != null)
            {
                _activeProcessingFacility.ReleaseWorker(this);
            }

            _activeProcessingFacility = null;
            _activeProcessingRecipe = null;
            _processingTimer = 0f;
            _processingDuration = 0f;

            if (_progressBarGo != null)
            {
                _progressBarGo.SetActive(false);
            }
        }

        /// <summary>
        /// 일꾼이 이동할 새로운 도로 타일을 설정하고 목표 위치를 갱신합니다.
        /// </summary>
        private void SetCurrentRoad(RoadTile newRoad)
        {
            if (CurrentRoad == newRoad)
            {
                if (newRoad != null)
                {
                    _moveTarget = GetRoadTargetPosition(newRoad);
                    _moveTarget.z = -1f;
                }
                else
                {
                    _moveTarget = Vector3.zero;
                }

                return;
            }

            if (CurrentRoad != null)
            {
                CurrentRoad.UnregisterWorker(this);
            }

            CurrentRoad = newRoad;

            if (CurrentRoad != null)
            {
                CurrentRoad.RegisterWorker(this);
                _hasReachedCurrentRoadCenter = !IsCurveRoad(CurrentRoad);
                _moveTarget = _hasReachedCurrentRoadCenter
                    ? GetRoadTargetPosition(CurrentRoad)
                    : CurrentRoad.GetWorldPosition();
                _moveTarget.z = -1f;
            }
            else
            {
                _moveTarget = Vector3.zero;
            }
        }

        /// <summary>
        /// 동일 도로 타일 내의 여러 일꾼이 겹치지 않도록 측면 오프셋이 적용된 목표 위치를 계산합니다.
        /// </summary>
        private Vector3 GetRoadTargetPosition(RoadTile road)
        {
            Vector3 basePosition = road.GetWorldPosition();
            IReadOnlyList<Worker> occupants = road.Occupants;

            int occupantIndex = 0;
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] == this)
                {
                    occupantIndex = i;
                    break;
                }
            }

            Vector2Int direction = road.Direction;
            Vector2Int incomingDirection = GetIncomingDirection(road);

            Vector3 lateral = new Vector3(-direction.y, direction.x, 0f);
            if (lateral == Vector3.zero)
            {
                lateral = Vector3.right;
            }
            float centeredIndex = occupantIndex - (occupants.Count - 1) * 0.5f;
            Vector3 lateralOffset = lateral.normalized * (0.10f * centeredIndex);

            // 다음 도로가 시설이고 진입 불가한 경우 줄 서기 모드 발동
            bool isWaitingForFacility = false;
            if (road.NextRoad != null)
            {
                ProcessingFacility nextFacility = FindComponentAtGrid<ProcessingFacility>(road.NextRoad.GridPosition);
                if (nextFacility != null && FindSupportedInput(nextFacility) != null && !nextFacility.CanWorkerEnter(this))
                {
                    isWaitingForFacility = true;
                }
            }

            if (isWaitingForFacility)
            {
                return GetQueuedRoadPosition(basePosition, direction, incomingDirection, occupantIndex, centeredIndex);
            }

            return basePosition + lateralOffset;
        }

        private bool IsCurveRoad(RoadTile road)
        {
            if (road == null)
            {
                return false;
            }

            Vector2Int incomingDirection = GetIncomingDirection(road);
            Vector2Int outgoingDirection = NormalizeCardinal(road.Direction);
            return incomingDirection != Vector2Int.zero
                && outgoingDirection != Vector2Int.zero
                && incomingDirection != outgoingDirection
                && incomingDirection != -outgoingDirection;
        }

        private Vector3 GetQueuedRoadPosition(
            Vector3 basePosition,
            Vector2Int outgoingDirection,
            Vector2Int incomingDirection,
            int occupantIndex,
            float centeredIndex)
        {
            Vector2Int normalizedOutgoing = NormalizeCardinal(outgoingDirection);
            Vector2Int normalizedIncoming = NormalizeCardinal(incomingDirection);
            bool isCurve = normalizedIncoming != Vector2Int.zero
                && normalizedOutgoing != Vector2Int.zero
                && normalizedIncoming != normalizedOutgoing
                && normalizedIncoming != -normalizedOutgoing;

            float distanceFromExit = 0.12f + occupantIndex * 0.32f;
            Vector2Int laneDirection = normalizedOutgoing != Vector2Int.zero ? normalizedOutgoing : Vector2Int.up;
            Vector3 queuePosition = basePosition;

            if (isCurve && distanceFromExit > 0.5f)
            {
                float incomingDistance = Mathf.Min(0.42f, distanceFromExit - 0.5f);
                queuePosition += new Vector3(normalizedIncoming.x, normalizedIncoming.y, 0f) * -incomingDistance;
                laneDirection = normalizedIncoming;
            }
            else
            {
                float clampedDistance = Mathf.Min(0.42f, distanceFromExit);
                queuePosition += new Vector3(laneDirection.x, laneDirection.y, 0f) * (0.5f - clampedDistance);
            }

            Vector3 lateral = new Vector3(-laneDirection.y, laneDirection.x, 0f);
            if (lateral == Vector3.zero)
            {
                lateral = Vector3.right;
            }

            return queuePosition + lateral.normalized * (0.08f * centeredIndex);
        }

        private Vector2Int GetIncomingDirection(RoadTile road)
        {
            if (road == null || road.PreviousRoad == null)
            {
                return Vector2Int.zero;
            }

            return NormalizeCardinal(road.GridPosition - road.PreviousRoad.GridPosition);
        }

        private Vector2Int NormalizeCardinal(Vector2Int direction)
        {
            if (direction == Vector2Int.up || direction == Vector2Int.right ||
                direction == Vector2Int.down || direction == Vector2Int.left)
            {
                return direction;
            }

            return Vector2Int.zero;
        }

        /// <summary>
        /// 일꾼을 비활성화하거나 오브젝트 풀로 반환합니다.
        /// </summary>
        private void DespawnSelf()
        {
            WorkerSpawner spawner = FindAnyObjectByType<WorkerSpawner>();
            if (spawner != null)
            {
                spawner.ReturnWorker(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 일꾼의 행동 상태를 변경하고 비주얼을 업데이트합니다.
        /// </summary>
        private void TransitionTo(WorkerState newState)
        {
            CurrentState = newState;
            UpdateVisual();
        }

        /// <summary>
        /// 일꾼 본체 및 운반 중인 자원의 비주얼 인스턴스를 업데이트합니다.
        /// </summary>
        private void UpdateVisual()
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            _spriteRenderer.color = Color.white;

            // 일꾼 스프라이트 방향 결정
            if (_workerData != null)
            {
                if (_lastMoveDir == Vector2Int.up && _workerData.SpriteBack != null)
                {
                    _spriteRenderer.sprite = _workerData.SpriteBack;
                    _spriteRenderer.flipX = false;
                }
                else if (_lastMoveDir == Vector2Int.down && _workerData.SpriteFront != null)
                {
                    _spriteRenderer.sprite = _workerData.SpriteFront;
                    _spriteRenderer.flipX = false;
                }
                else if ((_lastMoveDir == Vector2Int.left || _lastMoveDir == Vector2Int.right) && _workerData.SpriteSide != null)
                {
                    _spriteRenderer.sprite = _workerData.SpriteSide;
                    _spriteRenderer.flipX = (_lastMoveDir == Vector2Int.left);
                }
            }

            int count = CarriedResources.Count;

            // 운반 중인 자원 스프라이트Renderer 관리
            while (_carriedVisuals.Count < count)
            {
                int index = _carriedVisuals.Count;
                GameObject go = new GameObject($"CarriedResourceVisual_{index}");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(
                    CarriedItemBaseOffset.x,
                    CarriedItemBaseOffset.y + index * CarriedItemStackOffset,
                    -0.1f * (index + 1));
                go.transform.localScale = new Vector3(CarriedItemScale, CarriedItemScale, 1f);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _spriteRenderer.sortingOrder + 5 + index;
                _carriedVisuals.Add(sr);
            }

            while (_carriedVisuals.Count > count)
            {
                int lastIdx = _carriedVisuals.Count - 1;
                SpriteRenderer lastSr = _carriedVisuals[lastIdx];
                if (lastSr != null)
                {
                    Destroy(lastSr.gameObject);
                }

                _carriedVisuals.RemoveAt(lastIdx);
            }

            // 각 운반물 스프라이트 갱신
            for (int i = 0; i < count; i++)
            {
                if (_carriedVisuals[i] == null || CarriedResources[i] == null)
                {
                    continue;
                }

                _carriedVisuals[i].sprite = CarriedResources[i].Icon;
                _carriedVisuals[i].transform.localPosition = new Vector3(
                    CarriedItemBaseOffset.x,
                    CarriedItemBaseOffset.y + i * CarriedItemStackOffset,
                    -0.1f * (i + 1));
                _carriedVisuals[i].transform.localScale = new Vector3(CarriedItemScale, CarriedItemScale, 1f);
            }
        }

        /// <summary>
        /// 전방에 다른 일꾼이 있을 경우 충돌을 피하기 위해 유효 속도를 조절합니다.
        /// </summary>
        private float CalculateEffectiveSpeed(Vector3 moveDirection)
        {
            float speedMultiplier = 1f;
            DayManager dayManager = GameManager.Instance != null
                ? GameManager.Instance.Day
                : FindFirstObjectByType<DayManager>();
            if (dayManager != null)
            {
                speedMultiplier = dayManager.WorkerSpeedMultiplier;
            }

            float adjustedSpeed = _currentSpeed * speedMultiplier;
            Worker[] allWorkers = FindObjectsByType<Worker>(FindObjectsSortMode.None);
            foreach (Worker other in allWorkers)
            {
                if (other == this || !other.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 toOther = other.transform.position - transform.position;
                float dist = toOther.magnitude;
                if (dist <= 0.001f)
                {
                    continue;
                }

                float alignment = Vector3.Dot(toOther.normalized, moveDirection);

                // 상대방의 진행 방향 확인
                Vector3 otherDir = (other.MoveTarget - other.transform.position).normalized;
                float directionDot = Vector3.Dot(moveDirection, otherDir);

                // 서로 반대 방향으로 가고 있다면 (스쳐 지나가는 상황) 충돌 체크 완화
                if (directionDot < -0.5f)
                {
                    continue;
                }

                // 너무 가까우면 정지
                if (dist < WorkerMinDistance && alignment > 0.5f)
                {
                    return 0f;
                }

                // 일정 거리 이내면 서행
                if (dist < WorkerMinDistance * 2f && alignment > 0.5f)
                {
                    return adjustedSpeed * 0.3f;
                }
            }

            return adjustedSpeed;
        }
    }
}
