using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Facility;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Logistics
{
    /// <summary>
    /// <see cref="WorkerHouse"/>별 일꾼 생성·관리를 담당하는 매니저.
    /// <see cref="ObjectPool{T}"/>를 사용하여 일꾼 인스턴스를 효율적으로 재활용한다.
    /// </summary>
    public class WorkerSpawner : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("일꾼 설정")]
        [Tooltip("일꾼 프리팹")]
        [SerializeField] private Worker _workerPrefab;
        [SerializeField] private WorkerDataSO _workerData;

        [Tooltip("오브젝트 풀 초기 크기")]
        [SerializeField] private int _poolInitialSize = 20;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private ObjectPool<Worker> _pool;
        private readonly Dictionary<WorkerHouse, List<Worker>> _houseWorkers
            = new Dictionary<WorkerHouse, List<Worker>>();

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void Awake()
        {
            if (_workerPrefab != null)
            {
                _pool = new ObjectPool<Worker>(_workerPrefab, _poolInitialSize, transform);
            }
            else
            {
                Debug.LogError("[WorkerSpawner] 일꾼 프리팹이 지정되지 않았습니다.");
            }
        }

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 지정된 숙소(생산소)에서 특정 좌표에 단발적으로 일꾼을 1명 생성하여 배출한다.
        /// </summary>
        public Worker SpawnWorkerAt(WorkerHouse house, Vector3 position)
        {
            if (house == null || _pool == null) return null;

            if (!_houseWorkers.ContainsKey(house))
            {
                _houseWorkers[house] = new List<Worker>();
            }

            List<Worker> workers = _houseWorkers[house];

            // 현재 이 숙소 소속으로 활동 중인 일꾼 수가 최대 한도를 넘었으면 스폰하지 않음
            Worker worker = _pool.Get();
            if (_workerData != null)
            {
                worker.ConfigureData(_workerData);
            }
            worker.HomeHouse = house;
            worker.transform.position = position;
            workers.Add(worker);

            return worker;
        }

        public bool HasWorkerNearPosition(Vector3 position, float worldDistance)
        {
            foreach (var workers in _houseWorkers.Values)
            {
                for (int i = workers.Count - 1; i >= 0; i--)
                {
                    Worker worker = workers[i];
                    if (worker == null || !worker.gameObject.activeInHierarchy)
                    {
                        workers.RemoveAt(i);
                        continue;
                    }

                    if (Vector2.Distance(worker.transform.position, position) <= worldDistance)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool HasWorkerNearHouse(WorkerHouse house, float gridDistance)
        {
            if (house == null || !_houseWorkers.ContainsKey(house))
                return false;

            float worldDistance = Mathf.Max(0f, gridDistance) * Constants.CellSize;
            Vector3 housePosition = house.transform.position;
            List<Worker> workers = _houseWorkers[house];

            for (int i = workers.Count - 1; i >= 0; i--)
            {
                Worker worker = workers[i];
                if (worker == null || !worker.gameObject.activeInHierarchy)
                {
                    workers.RemoveAt(i);
                    continue;
                }

                if (Vector2.Distance(worker.transform.position, housePosition) <= worldDistance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 지정된 숙소에 대해 최대 일꾼 수까지 일꾼을 생성한다. (컨베이어 시스템 전환으로 레거시 처리됨)
        /// </summary>
        /// <param name="house">일꾼을 생성할 숙소.</param>
        public void SpawnWorkersForHouse(WorkerHouse house)
        {
            // 컨베이어 벨트식 퐁퐁 배출로 전환되어 초기 대량 스폰은 하지 않습니다.
            return;
        }

        /// <summary>
        /// 지정된 숙소의 모든 일꾼을 풀로 반환한다.
        /// </summary>
        /// <param name="house">일꾼을 회수할 숙소.</param>
        public void DespawnWorkersForHouse(WorkerHouse house)
        {
            if (house == null || !_houseWorkers.ContainsKey(house))
                return;

            List<Worker> workers = _houseWorkers[house];
            for (int i = workers.Count - 1; i >= 0; i--)
            {
                Worker worker = workers[i];
                if (worker != null)
                {
                    worker.OnTaskCompleted -= OnWorkerTaskCompleted;
                    _pool.Return(worker);
                }
            }

            workers.Clear();
        }

        /// <summary>
        /// 지정된 숙소에서 대기 중인(Idle) 일꾼 한 명을 반환한다.
        /// </summary>
        /// <param name="house">숙소.</param>
        /// <returns>Idle 상태의 일꾼. 없으면 <c>null</c>.</returns>
        public Worker GetAvailableWorker(WorkerHouse house)
        {
            if (house == null || !_houseWorkers.ContainsKey(house))
                return null;

            foreach (Worker worker in _houseWorkers[house])
            {
                if (worker != null && worker.CurrentState == WorkerState.Idle)
                    return worker;
            }

            return null;
        }

        /// <summary>
        /// 지정된 일꾼을 풀로 반환한다.
        /// </summary>
        /// <param name="worker">반환할 일꾼.</param>
        public void ReturnWorker(Worker worker)
        {
            if (worker == null) return;

            if (worker.HomeHouse != null && _houseWorkers.ContainsKey(worker.HomeHouse))
            {
                _houseWorkers[worker.HomeHouse].Remove(worker);
            }

            worker.OnTaskCompleted -= OnWorkerTaskCompleted;
            _pool.Return(worker);
        }

        /// <summary>
        /// 지정된 숙소에 소속된 활성 일꾼 수를 반환한다.
        /// </summary>
        /// <param name="house">숙소.</param>
        /// <returns>활성 일꾼 수.</returns>
        public int GetActiveWorkerCount(WorkerHouse house)
        {
            if (house == null || !_houseWorkers.ContainsKey(house))
                return 0;
            return _houseWorkers[house].Count;
        }

        /// <summary>
        /// 지정된 숙소에서 대기 중인 일꾼 수를 반환한다.
        /// </summary>
        /// <param name="house">숙소.</param>
        /// <returns>Idle 상태 일꾼 수.</returns>
        public int GetIdleWorkerCount(WorkerHouse house)
        {
            if (house == null || !_houseWorkers.ContainsKey(house))
                return 0;

            int count = 0;
            foreach (Worker worker in _houseWorkers[house])
            {
                if (worker != null && worker.CurrentState == WorkerState.Idle)
                    count++;
            }
            return count;
        }

        // =========================================================================
        //  내부
        // =========================================================================

        /// <summary>
        /// 일꾼 작업 완료 콜백. 일꾼을 숙소 위치로 복귀시킨다.
        /// </summary>
        private void OnWorkerTaskCompleted(Worker worker)
        {
            if (worker.HomeHouse != null)
            {
                worker.transform.position = worker.HomeHouse.transform.position;
            }
        }
    }
}
