using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Facility;
using FactoryDelivery.Resource;
using FactoryDelivery.Utils;
using FactoryDelivery.Grid;
using FactoryDelivery.Core;


namespace FactoryDelivery.Logistics
{
    /// <summary>
    /// 일꾼의 행동 상태를 정의하는 열거형.
    /// </summary>
    public enum WorkerState
    {
        /// <summary>숙소에서 대기 중. 작업 배정을 기다린다.</summary>
        Idle,
        /// <summary>자원 타일 또는 시설 출력으로 이동 중.</summary>
        MovingToPickup,
        /// <summary>자원을 수거하는 짧은 대기.</summary>
        PickingUp,
        /// <summary>자원을 들고 목적지로 이동 중.</summary>
        Carrying,
        /// <summary>자원을 배달하는 짧은 대기.</summary>
        Delivering,
        /// <summary>빈손으로 숙소로 복귀 중.</summary>
        Returning
    }

    /// <summary>
    /// 개별 일꾼 에이전트. <see cref="IPoolable"/>을 구현하여 오브젝트 풀에서 관리된다.
    /// 도로 위를 연속적으로 이동하며, 다른 일꾼과 최소 거리를 유지한다.
    /// </summary>
    public class Worker : MonoBehaviour, IPoolable
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("비주얼")]
        [Tooltip("일꾼의 스프라이트 렌더러")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private WorkerDataSO _workerData;

        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        private float _baseSpeed;
        private float _currentSpeed;

        private GridManager _gridManager;
        private readonly HashSet<Vector2Int> _harvestedResourcePositions = new HashSet<Vector2Int>();

        /// <summary>현재 일꾼이 위치한 도로 타일</summary>
        public RoadTile CurrentRoad;
        private Vector3 _moveTarget;
        private readonly List<SpriteRenderer> _carriedVisuals = new List<SpriteRenderer>();

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>일꾼의 현재 상태.</summary>
        public WorkerState CurrentState { get; private set; } = WorkerState.Idle;

        /// <summary>현재 운반 중인 자원 목록.</summary>
        public List<ResourceDataSO> CarriedResources { get; } = new List<ResourceDataSO>();

        /// <summary>호환성 속성: 적재된 자원 중 첫 번째 자원을 반환.</summary>
        public ResourceDataSO CarriedResource => CarriedResources.Count > 0 ? CarriedResources[0] : null;

        /// <summary>이 일꾼이 소속된 숙소.</summary>
        public WorkerHouse HomeHouse { get; set; }

        private float BaseSpeed => _workerData != null ? _workerData.EffectiveBaseSpeed : Constants.WorkerBaseSpeed;
        private float WorkerMinDistance => _workerData != null ? _workerData.EffectiveMinDistance : Constants.WorkerMinDistance;
        private int CarryCapacity => _workerData != null ? _workerData.EffectiveCarryCapacity : 1;
        private float WorkerScale => _workerData != null ? _workerData.EffectiveWorkerScale : 1.2f;
        private float CarriedItemScale => _workerData != null ? _workerData.CarriedItemScale : 0.32f;
        private Vector2 CarriedItemBaseOffset => _workerData != null ? _workerData.CarriedItemBaseOffset : new Vector2(0f, 0.34f);
        private float CarriedItemStackOffset => _workerData != null ? _workerData.CarriedItemStackOffset : 0.18f;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>작업 완료 후 숙소에 복귀했을 때 발생.</summary>
        public event Action<Worker> OnTaskCompleted;

        // /// <summary>경로가 유효하지 않아 작업이 실패했을 때 발생.</summary>
        // public event Action<Worker> OnTaskFailed;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            _baseSpeed = BaseSpeed;
            _currentSpeed = _baseSpeed;

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            // 일꾼이 시설을 가리지 않도록 아담하게 스케일 다운 및 Z축 격리 (크기를 1.2f로 대폭 거대화하여 가독성 보증)
            transform.localScale = new Vector3(WorkerScale, WorkerScale, 1f);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 20; // 타일(-100 ~ 0)보다 위, 텍스트 라벨(60)보다 아래
            }

            _gridManager = FindAnyObjectByType<GridManager>();
        }

        private void Update()
        {
            if (CurrentState == WorkerState.Idle)
                return;

            MoveAlongConveyorRoad();
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 일꾼에게 컨베이어식 단방향 이동을 개시하도록 지시한다.
        /// </summary>
        /// <param name="startRoad">출발 도로 타일</param>
        public void StartConveyorFlow(RoadTile startRoad)
        {
            _baseSpeed = BaseSpeed;
            CurrentRoad = startRoad;
            if (startRoad != null)
            {
                _moveTarget = startRoad.GetWorldPosition();
                _moveTarget.z = -1.0f; // Z축 띄우기 보정으로 가림 현상 완벽 방지
 
                Vector3 startPos = startRoad.GetWorldPosition();
                startPos.z = -1.0f;
                transform.position = startPos;
            }
            CarriedResources.Clear();
            _harvestedResourcePositions.Clear();
            _currentSpeed = _baseSpeed;
 
            // 이동 시작 상태(파란색)로 전환
            TransitionTo(WorkerState.MovingToPickup);
        }
 
        /// <summary>
        /// (레거시 경로 배정 API - 컨베이어식 개편으로 더 이상 쓰이지 않음)
        /// </summary>
        public void AssignTask(
            List<Vector3> pathToPickup,
            Component pickupTarget,
            List<Vector3> pathToDelivery,
            Component deliveryTarget,
            List<Vector3> pathHome)
        {
            // 사용하지 않음
        }
 
        // ─────────────────────────────────────────────
        //  IPoolable
        // ─────────────────────────────────────────────
 
        /// <summary>
        /// 풀에서 꺼내어 활성화될 때 호출. 상태를 초기화한다.
        /// </summary>
        public void OnSpawnFromPool()
        {
            _baseSpeed = BaseSpeed;
            CurrentState = WorkerState.Idle;
            CarriedResources.Clear();
            _harvestedResourcePositions.Clear();
            CurrentRoad = null;
            _moveTarget = Vector3.zero;
            _currentSpeed = _baseSpeed;
 
            // 풀링에서 복귀 시에도 스케일과 정렬 확실하게 보증 (1.2f로 대폭 거대화)
            transform.localScale = new Vector3(WorkerScale, WorkerScale, 1f);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = 20;
            }
 
            UpdateVisual();
        }
 
        /// <summary>
        /// 풀로 반환될 때 호출. 참조를 정리한다.
        /// </summary>
        public void OnReturnToPool()
        {
            CurrentState = WorkerState.Idle;
            CurrentRoad = null;
            _moveTarget = Vector3.zero;
            HomeHouse = null;
 
            // 들고 있던 비주얼 리셋 처리
            foreach (var renderer in _carriedVisuals)
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

        public void ConfigureData(WorkerDataSO workerData)
        {
            _workerData = workerData;
            _baseSpeed = BaseSpeed;
            _currentSpeed = _baseSpeed;
            transform.localScale = new Vector3(WorkerScale, WorkerScale, 1f);
        }

        // ─────────────────────────────────────────────
        //  Movement (Conveyor-style)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 도로 순방향(NextRoad)을 감지하여 컨베이어 벨트처럼 연속 이동한다.
        /// </summary>
        private void MoveAlongConveyorRoad()
        {
            if (CurrentRoad == null)
            {
                TransitionTo(WorkerState.Idle);
                return;
            }

            Vector3 target = _moveTarget;
            Vector3 direction = (target - transform.position).normalized;

            // 앞에 다른 일꾼이 가깝게 있을 때 감속 또는 정지
            float effectiveSpeed = CalculateEffectiveSpeed(direction);

            // 현재 위치가 도로 위인지 감지하여, 도로 이탈 시 25%의 감속 디버프 패널티 적용
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

            float speedMultiplier = isOnRoad ? 1.0f : 0.25f;
            float finalSpeed = effectiveSpeed * speedMultiplier;

            // 컨베이어 벨트를 타듯 전진 (Z축 격리 보정을 더해 렌더링 노출 보장)
            Vector3 nextPos = Vector3.MoveTowards(
                transform.position,
                target,
                finalSpeed * Time.deltaTime
            );
            nextPos.z = -1.0f; // 타일 스프라이트들보다 무조건 카메라 쪽에 띄우기
            transform.position = nextPos;

            // 현재 도로 웨이포인트(중심점) 도달 검사
            if (Vector3.Distance(transform.position, target) < 0.05f)
            {
                // 자신이 위치한 바로 그 타일(겹쳐진 타일)의 기능 상호작용 수행
                ProcessAdjacentInteractions();

                // 상호작용 중 판매소 납품 완료 등으로 소멸(Despawn)했다면 루틴 탈출
                if (!gameObject.activeInHierarchy)
                    return;

                // 다음 도로로 목표 갱신
                if (CurrentRoad.NextRoad != null)
                {
                    CurrentRoad = CurrentRoad.NextRoad;
                    _moveTarget = CurrentRoad.GetWorldPosition();
                    _moveTarget.z = -1.0f; // 다음 타겟도 Z 보정 확실하게 고정
                    
                    if (CurrentState == WorkerState.Idle)
                    {
                        TransitionTo(CarriedResource != null ? WorkerState.Carrying : WorkerState.MovingToPickup);
                    }
                }
                else
                {
                    // 도로가 끝났다면 일단 그 자리에서 유휴(대기) 상태로 전환
                    TransitionTo(WorkerState.Idle);
                }
            }
        }

        /// <summary>
        /// 일꾼이 딛고 있는 바로 그 타일(현재 위치)과 물류 상호작용을 처리한다.
        /// </summary>
        private void ProcessAdjacentInteractions()
        {
            if (_gridManager == null) return;

            // 1. 오직 일꾼의 현재 그리드 위치만 가져옵니다. (4방향 탐색 삭제)
            Vector2Int myGridPos = transform.position.ToGridPosition();

            if (!_gridManager.IsInBounds(myGridPos)) return;

            TileEntity entity = _gridManager.GetTileAt(myGridPos);
            if (entity == null) return;

            // 2. 자원밭(ResourceTile) 감지
            // (이제 다른 칸을 검사하지 않으므로 pos != myGridPos 예외 처리 삭제)
            if (entity.IsResource && CarriedResources.Count < CarryCapacity && !_harvestedResourcePositions.Contains(myGridPos))
            {
                ResourceTile resourceTile = FindComponentAtGrid<ResourceTile>(myGridPos);
                if (resourceTile != null && resourceTile.HasResource)
                {
                    ResourceDataSO harvested = resourceTile.PickupResource();
                    if (harvested != null)
                    {
                        CarriedResources.Add(harvested);
                        _harvestedResourcePositions.Add(myGridPos);
                        
                        // 속도 배율 재계산
                        float totalWeight = 0f;
                        foreach (var res in CarriedResources) totalWeight += res.WeightMultiplier;
                        _currentSpeed = _baseSpeed / Mathf.Max(1f, totalWeight - (CarriedResources.Count - 1) * 0.5f);
                        
                        TransitionTo(WorkerState.Carrying); 
                        return; // 성공 시 함수 즉시 종료 (기존 break 역할)
                    }
                }
            }
            // 3. 시설(FacilityBase) 감지
            else if (entity.IsFacility)
            {
                FacilityBase facility = FindComponentAtGrid<FacilityBase>(myGridPos);
                if (facility != null)
                {
                    // 3-가. 창고(Warehouse)
                    if (facility is Warehouse warehouse)
                    {
                        if (CarriedResources.Count > 0)
                        {
                            if (GameManager.Instance != null)
                            {
                                foreach (var res in CarriedResources)
                                {
                                    GameManager.Instance.Inventory.Add(res, 1);
                                }
                            }
                            CarriedResources.Clear();
                            DespawnSelf();
                            return;
                        }
                        
                        if (CarriedResources.Count == 0) return;
                        DespawnSelf();
                        return;
                    }
                    // 3-나. 일반 가공소
                    else
                    {
                        // 투입
                        if (CarriedResources.Count > 0 && facility.CanAcceptInput)
                        {
                            ResourceDataSO inputTarget = null;
                            foreach (var res in CarriedResources)
                            {
                                if (IsRecipeSupported(facility, res))
                                {
                                    inputTarget = res;
                                    break;
                                }
                            }

                            if (inputTarget != null)
                            {
                                facility.ReceiveResource(inputTarget);
                                CarriedResources.Remove(inputTarget);

                                if (CarriedResources.Count == 0)
                                {
                                    TransitionTo(WorkerState.MovingToPickup);
                                    _currentSpeed = _baseSpeed;
                                }
                                else
                                {
                                    float totalWeight = 0f;
                                    foreach (var res in CarriedResources) totalWeight += res.WeightMultiplier;
                                    _currentSpeed = _baseSpeed / Mathf.Max(1f, totalWeight - (CarriedResources.Count - 1) * 0.5f);
                                    TransitionTo(WorkerState.Carrying);
                                }
                                return;
                            }
                        }
                        // 수거
                        else if (CarriedResources.Count < CarryCapacity && facility.HasOutput)
                        {
                            ResourceDataSO output = facility.PickupOutput();
                            if (output != null)
                            {
                                CarriedResources.Add(output);
                                
                                float totalWeight = 0f;
                                foreach (var res in CarriedResources) totalWeight += res.WeightMultiplier;
                                _currentSpeed = _baseSpeed / Mathf.Max(1f, totalWeight - (CarriedResources.Count - 1) * 0.5f);
                                
                                TransitionTo(WorkerState.Carrying);
                                return;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 해당 가공소의 레시피에서 해당 자원이 입력 자원으로 등록되어 있는지 체크
        /// </summary>
        private bool IsRecipeSupported(FacilityBase facility, ResourceDataSO resource)
        {
            if (facility.FacilityData == null || facility.FacilityData.SupportedRecipes == null)
                return false;

            foreach (var recipe in facility.FacilityData.SupportedRecipes)
            {
                if (recipe != null && recipe.InputResource == resource)
                    return true;
            }
            return false;
        }

        private T FindComponentAtGrid<T>(Vector2Int gridPos) where T : Component
        {
            T[] allComponents = FindObjectsByType<T>(FindObjectsSortMode.None);
            foreach (T comp in allComponents)
            {
                if (comp == null) continue;

                if (comp is ResourceTile resTile)
                {
                    if (resTile.GridPosition == gridPos)
                        return comp;
                }
                else if (comp is FacilityBase facTile)
                {
                    if (facTile.GridPosition == gridPos)
                        return comp;
                }
                else if (comp.transform.position.ToGridPosition() == gridPos)
                {
                    return comp;
                }
            }
            return null;
        }

        /// <summary>
        /// 일꾼의 수명을 다하여 Spawner 풀로 조용히 돌려보냅니다 (소멸).
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
                gameObject.SetActive(false); // 풀 실패 대비 비활성화 안전 처리
            }
        }

        // ─────────────────────────────────────────────
        //  State & Visual
        // ─────────────────────────────────────────────

        /// <summary>
        /// 상태를 전환하고 비주얼을 업데이트한다.
        /// </summary>
        private void TransitionTo(WorkerState newState)
        {
            CurrentState = newState;
            UpdateVisual();
        }

        /// <summary>
        /// 현재 상태에 따라 스프라이트 색상을 변경한다.
        /// </summary>
        private void UpdateVisual()
        {
            if (_spriteRenderer == null) return;

            _spriteRenderer.color = CurrentState switch
            {
                WorkerState.Idle => new Color(1.0f, 0.85f, 0.0f),            // 멋진 황금 엽전 노란색 (가만히 서 있어도 귀여움)
                WorkerState.MovingToPickup => new Color(0.2f, 0.6f, 1.0f),   // 활기찬 스마트 파란색
                WorkerState.PickingUp => new Color(1.0f, 0.95f, 0.4f),       // 연한 수확 노란색
                WorkerState.Carrying => new Color(0.95f, 0.45f, 0.1f),       // 강렬한 배달 주황색
                WorkerState.Delivering => new Color(0.25f, 0.85f, 0.25f),    // 싱그러운 가공 초록색
                WorkerState.Returning => new Color(0.6f, 0.6f, 0.6f),        // 차분한 은회색 복귀 컬러
                _ => Color.white
            };
            _spriteRenderer.color = Color.white;

            // 다중 운반 자원 비주얼 실시간 수직 Stacking 렌더링 (WOW Point!)
            int count = CarriedResources.Count;

            // 1. 적재된 자원 개수만큼의 비주얼 스프라이트 렌더러 스폰 및 관리
            while (_carriedVisuals.Count < count)
            {
                int index = _carriedVisuals.Count;
                GameObject go = new GameObject($"CarriedResourceVisual_{index}");
                go.transform.parent = transform;
                
                // 수직으로 이쁘게 차곡차곡 올라가는 로컬 간격 세팅 (Y: 0.35f, Z: 층별 전면 돌출)
                go.transform.localPosition = new Vector3(
                    CarriedItemBaseOffset.x,
                    CarriedItemBaseOffset.y + index * CarriedItemStackOffset,
                    -0.1f * (index + 1));
                go.transform.localScale = new Vector3(CarriedItemScale, CarriedItemScale, 1f);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _spriteRenderer.sortingOrder + 5 + index; // 차곡차곡 깊이 배정
                
                _carriedVisuals.Add(sr);
            }

            // 2. 적재 해제 등으로 남는 스프라이트 렌더러 파괴
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

            // 3. 자원 종류별 실시간 스프라이트 아이콘 동적 갱신
            for (int i = 0; i < count; i++)
            {
                if (_carriedVisuals[i] != null && CarriedResources[i] != null)
                {
                    _carriedVisuals[i].sprite = CarriedResources[i].Icon;
                    _carriedVisuals[i].transform.localPosition = new Vector3(
                        CarriedItemBaseOffset.x,
                        CarriedItemBaseOffset.y + i * CarriedItemStackOffset,
                        -0.1f * (i + 1));
                    _carriedVisuals[i].transform.localScale = new Vector3(CarriedItemScale, CarriedItemScale, 1f);
                }
            }
        }

        /// <summary>
        /// 다른 일꾼과의 거리를 고려한 실효 이동 속도를 계산한다.
        /// </summary>
        private float CalculateEffectiveSpeed(Vector3 moveDirection)
        {
            // 전방 일꾼 탐색 (간단한 거리 체크)
            Worker[] allWorkers = FindObjectsByType<Worker>(FindObjectsSortMode.None);
            foreach (Worker other in allWorkers)
            {
                if (other == this || !other.gameObject.activeInHierarchy)
                    continue;

                Vector3 toOther = other.transform.position - transform.position;
                float dist = toOther.magnitude;

                // 최소 거리 이내이고 같은 방향에 있는 경우
                if (dist < WorkerMinDistance &&
                    Vector3.Dot(toOther.normalized, moveDirection) > 0.5f)
                {
                    return 0f; // 정지
                }

                // 브레이킹 거리 이내이면 감속
                if (dist < WorkerMinDistance * 2f &&
                    Vector3.Dot(toOther.normalized, moveDirection) > 0.5f)
                {
                    return _currentSpeed * 0.3f;
                }
            }

            return _currentSpeed;
        }
    }
}
