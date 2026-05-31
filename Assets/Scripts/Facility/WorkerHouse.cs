using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Resource;
using FactoryDelivery.Grid;
using FactoryDelivery.Logistics;
using FactoryDelivery.Utils;
using FactoryDelivery.Core;

namespace FactoryDelivery.Facility
{
    /// <summary>
    /// <see cref="FacilityBase"/>를 확장하는 일꾼 숙소.
    /// AoE(활동 반경) 내의 <see cref="ResourceTile"/>과 <see cref="FacilityBase"/>를 활성화하며,
    /// 자체적으로 항상 활성 상태를 유지한다.
    /// 실제 일꾼 생성은 Phase 5의 WorkerSpawner에서 담당한다.
    /// </summary>
    public class WorkerHouse : FacilityBase
    {
        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        /// <summary>현재 레벨 기준 AoE 반경 (캐시).</summary>
        private float _aoERadius;

        /// <summary>현재 레벨 기준 최대 일꾼 수 (캐시).</summary>
        /// <summary>AoE 내에서 활성화된 자원 타일 목록.</summary>
        private readonly List<ResourceTile> _activatedResourceTiles = new List<ResourceTile>();

        /// <summary>AoE 내에서 활성화된 시설 목록.</summary>
        private readonly List<FacilityBase> _activatedFacilities = new List<FacilityBase>();

        private float _spawnTimer;
        private GridManager _gridManager;
        private WorkerSpawner _workerSpawner;

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>AoE 내에서 활성화된 자원 타일의 읽기 전용 목록.</summary>
        public IReadOnlyList<ResourceTile> ActivatedResourceTiles => _activatedResourceTiles;

        /// <summary>AoE 내에서 활성화된 시설의 읽기 전용 목록.</summary>
        public IReadOnlyList<FacilityBase> ActivatedFacilities => _activatedFacilities;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // 일꾼 숙소는 항상 활성 상태
            IsActive = true;
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 일꾼 숙소를 활성화한다.
        /// 일꾼 숙소는 다른 시설과 달리 항상 활성 상태이므로,
        /// AoE 스캔을 수행하여 주변 타일과 시설을 활성화한다.
        /// </summary>
        public override void Activate()
        {
            base.Activate();
        }

        /// <summary>
        /// 일꾼 숙소를 비활성화한다.
        /// AoE 내의 모든 활성화된 타일과 시설을 비활성화한다.
        /// </summary>
        public override void Deactivate()
        {
            base.Deactivate();
        }

        /// <summary>
        /// AoE를 재스캔하여 범위 내의 자원 타일과 시설을 갱신한다.
        /// 레벨 변경 또는 주변에 새로운 타일/시설이 배치될 때 호출한다.
        /// </summary>
        public void RefreshAoE()
        {
            // 기존 활성화 해제
            DeactivateAll();

            RecalculateLevelStats();

            // AoE 반경 내의 ResourceTile 검색
            ResourceTile[] allResourceTiles = FindObjectsByType<ResourceTile>(FindObjectsSortMode.None);
            foreach (ResourceTile tile in allResourceTiles)
            {
                float distance = Vector2.Distance(transform.position, tile.transform.position);
                if (distance <= _aoERadius)
                {
                    tile.Activate();
                    _activatedResourceTiles.Add(tile);
                }
            }

            // AoE 반경 내의 FacilityBase 검색 (자기 자신 제외)
            FacilityBase[] allFacilities = FindObjectsByType<FacilityBase>(FindObjectsSortMode.None);
            foreach (FacilityBase facility in allFacilities)
            {
                if (facility == this)
                    continue;

                // 다른 WorkerHouse는 자체 활성화하므로 건너뛴다
                if (facility is WorkerHouse)
                    continue;

                float distance = Vector2.Distance(transform.position, facility.transform.position);
                if (distance <= _aoERadius)
                {
                    facility.Activate();
                    _activatedFacilities.Add(facility);
                }
            }

            Debug.Log(
                $"[WorkerHouse] '{gameObject.name}' AoE 갱신 완료 — " +
                $"반경: {_aoERadius:F1}, 자원 타일: {_activatedResourceTiles.Count}, " +
                $"시설: {_activatedFacilities.Count}");
        }

        /// <summary>
        /// 현재 레벨 기준 AoE 반경을 반환한다.
        /// </summary>
        /// <returns>AoE 반경 (월드 단위).</returns>
        public float GetAoERadius()
        {
            return 0f;
        }

        /// <summary>
        /// 현재 레벨 기준 최대 일꾼 수를 반환한다.
        /// </summary>
        /// <returns>배치 가능한 최대 일꾼 수.</returns>
        public int GetMaxWorkers()
        {
            return int.MaxValue;
        }

        // ─────────────────────────────────────────────
        //  Processing (No-op)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 일꾼 시설은 자체 생산력 주기마다 인접한 진출로 방향으로 일꾼을 배출합니다.
        /// </summary>
        /// <param name="deltaTime">이전 프레임 이후 경과 시간 (초).</param>
        protected override void ProcessTick(float deltaTime)
        {
            if (_gridManager == null)
                _gridManager = FindAnyObjectByType<GridManager>();
            if (_workerSpawner == null)
                _workerSpawner = FindAnyObjectByType<WorkerSpawner>();

            if (_gridManager == null || _workerSpawner == null) return;
            DayManager dayManager = FindAnyObjectByType<DayManager>();
            if (dayManager != null && dayManager.CurrentPhase != DayPhase.Operation) return;

            _spawnTimer += deltaTime;
            float cooldown = GetSpawnCooldown();

            if (_spawnTimer >= cooldown)
            {
                _spawnTimer = 0f;
                TrySpawnConveyorWorker();
            }
        }

        private float GetSpawnCooldown()
        {
            // 레벨이 높을수록 일꾼 배출 간격이 단축됩니다.
            return _facilityData != null ? _facilityData.GetWorkerSpawnInterval() : 4f;
            /*
            return Level switch
            {
                1 => 4.0f,
                2 => 2.5f,
                3 => 1.5f,
                _ => 1.0f
            };
            */
        }

        private void TrySpawnConveyorWorker()
        {
            Vector2Int myPos = transform.position.ToGridPosition();

            // 내 위치 바로 인접 1칸 내에서 외부로 나가는 진출로 감지
            FactoryDelivery.Logistics.RoadTile outgoingRoad = FactoryDelivery.Logistics.PathFinder.FindOutgoingRoad(_gridManager, myPos);
            if (outgoingRoad == null)
            {
                // 진출 도로망이 없으면 일꾼이 유실되므로 스폰을 유보합니다.
                return;
            }
            
            // 진출로에 이미 다른 일꾼이 겹쳐 있다면 (반경 0.25f 기준) 스폰을 유보하여 겹침 방지
            if (_workerSpawner.HasWorkerNearPosition(outgoingRoad.GetWorldPosition(), 0.25f * Constants.CellSize))
            {
                return;
            }

            // 진출로 월드 좌표 상에 일꾼 1명 생성 배출!
            Worker worker = _workerSpawner.SpawnWorkerAt(this, outgoingRoad.GetWorldPosition());
            if (worker != null)
            {
                // 일꾼에게 도로 순방향 컨베이어식 여행 지시
                worker.StartConveyorFlow(outgoingRoad);
            }
        }

        // ─────────────────────────────────────────────
        //  Internal Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// 현재 레벨에 맞게 AoE 반경과 최대 일꾼 수를 재계산한다.
        /// </summary>
        private void RecalculateLevelStats()
        {
            if (_facilityData == null)
                return;

            _aoERadius = 0f;
        }

        /// <summary>
        /// 현재 활성화된 모든 자원 타일과 시설을 비활성화하고 목록을 초기화한다.
        /// </summary>
        private void DeactivateAll()
        {
            foreach (ResourceTile tile in _activatedResourceTiles)
            {
                if (tile != null)
                {
                    tile.Deactivate();
                }
            }
            _activatedResourceTiles.Clear();

            foreach (FacilityBase facility in _activatedFacilities)
            {
                if (facility != null)
                {
                    facility.Deactivate();
                }
            }
            _activatedFacilities.Clear();
        }

        // ─────────────────────────────────────────────
        //  Gizmos (Editor Visualization)
        // ─────────────────────────────────────────────

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float radius = 0f;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
        #endif
    }
}
