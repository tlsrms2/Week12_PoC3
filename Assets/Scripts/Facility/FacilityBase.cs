using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;

namespace FactoryDelivery.Facility
{
    /// <summary>
    /// 모든 시설의 추상 기본 클래스.
    /// 입력/출력 자원 큐, 활성화 상태, 그리드 위치를 관리하며
    /// 하위 클래스가 <see cref="ProcessTick"/>을 구현하여 고유한 동작을 정의한다.
    /// </summary>
    public abstract class FacilityBase : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("시설 데이터")]
        [Tooltip("이 시설의 데이터 정의 (ScriptableObject)")]
        [SerializeField] protected FacilityDataSO _facilityData;

        [Header("큐 용량")]
        [Tooltip("입력 큐의 최대 용량")]
        [SerializeField] private int _inputCapacity = 3;

        [Tooltip("출력 큐의 최대 용량")]
        [SerializeField] private int _outputCapacity = 3;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private int _level = 1;
        private bool _isActive;
        private Vector2Int _gridPosition;

        /// <summary>가공을 대기하는 입력 자원 큐.</summary>
        protected readonly Queue<ResourceDataSO> _inputQueue = new Queue<ResourceDataSO>();

        /// <summary>가공이 완료되어 수거를 대기하는 출력 자원 큐.</summary>
        protected readonly Queue<ResourceDataSO> _outputQueue = new Queue<ResourceDataSO>();

        // =========================================================================
        //  프로퍼티
        // =========================================================================

        /// <summary>이 시설의 데이터 정의.</summary>
        public FacilityDataSO FacilityData => _facilityData;

        /// <summary>시설의 현재 레벨.</summary>
        public int Level
        {
            get => _level;
            set => _level = Mathf.Max(1, value);
        }

        /// <summary>시설이 현재 활성 상태인지 여부.</summary>
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        /// <summary>시설의 그리드 좌표.</summary>
        public Vector2Int GridPosition
        {
            get => _gridPosition;
            set => _gridPosition = value;
        }

        /// <summary>입력 큐의 최대 용량.</summary>
        public int InputCapacity
        {
            get => _inputCapacity;
            set => _inputCapacity = Mathf.Max(1, value);
        }

        /// <summary>출력 큐의 최대 용량.</summary>
        public int OutputCapacity
        {
            get => _outputCapacity;
            set => _outputCapacity = Mathf.Max(1, value);
        }

        /// <summary>입력 큐에 자원을 받을 수 있는지 여부.</summary>
        public bool CanAcceptInput => _inputQueue.Count < _inputCapacity;

        /// <summary>출력 큐에 수거 가능한 자원이 있는지 여부.</summary>
        public bool HasOutput => _outputQueue.Count > 0;

        /// <summary>현재 입력 큐에 쌓인 자원 수.</summary>
        public int InputCount => _inputQueue.Count;

        /// <summary>현재 출력 큐에 쌓인 자원 수.</summary>
        public int OutputCount => _outputQueue.Count;

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        protected virtual void Update()
        {
            if (_isActive)
            {
                ProcessTick(Time.deltaTime);
            }
        }

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 시설을 활성화한다. WorkerHouse AoE 범위 내에 들어왔을 때 호출된다.
        /// </summary>
        public virtual void Activate()
        {
            _isActive = true;
        }

        /// <summary>
        /// 시설을 비활성화한다. WorkerHouse AoE 범위를 벗어났을 때 호출된다.
        /// </summary>
        public virtual void Deactivate()
        {
            _isActive = false;
        }

        /// <summary>
        /// 입력 큐에 자원을 추가한다.
        /// 일꾼이 자원을 배달할 때 호출된다.
        /// </summary>
        /// <param name="resource">입력할 자원 데이터.</param>
        public void ReceiveResource(ResourceDataSO resource)
        {
            if (resource == null)
            {
                Debug.LogWarning($"[FacilityBase] '{gameObject.name}': null 자원을 수신하려 했습니다.");
                return;
            }

            if (!CanAcceptInput)
            {
                Debug.LogWarning(
                    $"[FacilityBase] '{gameObject.name}': 입력 큐가 가득 찼습니다. " +
                    $"({_inputQueue.Count}/{_inputCapacity})");
                return;
            }

            _inputQueue.Enqueue(resource);
        }

        /// <summary>
        /// 출력 큐에서 자원 하나를 꺼낸다.
        /// 일꾼이 완성품을 수거할 때 호출된다.
        /// </summary>
        /// <returns>꺼낸 자원 데이터. 큐가 비어 있으면 <c>null</c>.</returns>
        public ResourceDataSO PickupOutput()
        {
            if (_outputQueue.Count == 0)
            {
                Debug.LogWarning($"[FacilityBase] '{gameObject.name}': 출력 큐가 비어 있습니다.");
                return null;
            }

            return _outputQueue.Dequeue();
        }

        // =========================================================================
        //  추상 규약
        // =========================================================================

        /// <summary>
        /// 매 프레임 호출되는 처리 로직.
        /// 하위 클래스에서 구현하여 시설 고유의 동작을 정의한다.
        /// </summary>
        /// <param name="deltaTime">이전 프레임 이후 경과 시간 (초).</param>
        protected abstract void ProcessTick(float deltaTime);
    }
}
