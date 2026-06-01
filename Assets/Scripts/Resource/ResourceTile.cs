using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Events;
using FactoryDelivery.Grid;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Resource
{
    /// <summary>
    /// 자원 타일 GameObject에 부착되는 MonoBehaviour.
    /// WorkerHouse의 AoE 범위 내에 들어오면 활성화되어
    /// <see cref="ResourceDataSO.BaseCooldown"/> 주기마다 자원을 생산한다.
    /// 생산된 자원은 출력 버퍼에 쌓여 일꾼이 수거해 갈 때까지 대기한다.
    /// </summary>
    public class ResourceTile : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("자원 데이터")]
        [Tooltip("이 타일이 생산하는 자원 데이터. TileDataSO.AssociatedResource에서 설정한다.")]
        [SerializeField] private ResourceDataSO _resourceData;

        [Header("이벤트")]
        [Tooltip("자원 생산 시 발행되는 이벤트 채널")]
        [SerializeField] private ResourceEventChannelSO _onResourceProduced;

        [Header("버퍼 설정")]
        [Tooltip("출력 버퍼의 최대 용량. 가득 차면 생산이 중단된다.")]
        [SerializeField] private int _bufferCapacity = 5;

        [Header("비주얼")]
        [Tooltip("스프라이트 렌더러 (비주얼 피드백용)")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private int _level = 1;
        private float _cooldownTimer;
        private bool _isActive;

        /// <summary>
        /// 생산된 자원이 일꾼 수거를 대기하는 출력 버퍼.
        /// </summary>
        private readonly Queue<ResourceDataSO> _outputBuffer = new Queue<ResourceDataSO>();

        // =========================================================================
        //  속성
        // =========================================================================

        /// <summary>이 타일이 생산하는 자원 데이터.</summary>
        public ResourceDataSO ResourceData
        {
            get => _resourceData;
            set => _resourceData = value;
        }

        /// <summary>타일의 현재 레벨. 높을수록 생산 속도가 빨라진다.</summary>
        public int Level
        {
            get => _level;
            set => _level = Mathf.Max(1, value);
        }

        /// <summary>출력 버퍼의 최대 용량.</summary>
        public int BufferCapacity
        {
            get => _bufferCapacity;
            set => _bufferCapacity = Mathf.Max(1, value);
        }

        /// <summary>출력 버퍼에 수거 가능한 자원이 있는지 여부.</summary>
        public bool HasResource => _outputBuffer.Count > 0;

        /// <summary>현재 버퍼에 쌓인 자원 수.</summary>
        public int BufferCount => _outputBuffer.Count;

        /// <summary>타일이 현재 활성 상태인지 여부.</summary>
        public bool IsActive => _isActive;

        private Vector2Int _gridPosition = Vector2Int.zero;
        /// <summary>이 자원 타일의 정밀한 그리드 주소</summary>
        public Vector2Int GridPosition
        {
            get
            {
                if (_gridPosition == Vector2Int.zero)
                {
                    _gridPosition = transform.position.ToGridPosition();
                }
                return _gridPosition;
            }
            set => _gridPosition = value;
        }

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private GridManager _gridManager;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            _gridManager = FindAnyObjectByType<GridManager>();
        }

        private void Update()
        {
            if (!_isActive || _resourceData == null)
                return;

            // 버퍼가 가득 차면 생산 중단
            if (_outputBuffer.Count >= _bufferCapacity)
            {
                UpdateVisual(isIdle: true);
                return;
            }

            UpdateVisual(isIdle: false);

            _cooldownTimer -= Time.deltaTime;

            if (_cooldownTimer <= 0f)
            {
                ProduceResource();
                _cooldownTimer = GetCooldown();
            }
        }

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 타일을 활성화한다. WorkerHouse AoE 범위 내에 들어왔을 때 호출된다.
        /// </summary>
        public void Activate()
        {
            if (_isActive)
                return;

            _isActive = true;
            _cooldownTimer = GetCooldown();
            UpdateVisual(isIdle: false);
        }

        /// <summary>
        /// 타일을 비활성화한다. WorkerHouse AoE 범위를 벗어났을 때 호출된다.
        /// </summary>
        public void Deactivate()
        {
            if (!_isActive)
                return;

            _isActive = false;
            UpdateVisual(isIdle: true);
        }

        /// <summary>
        /// 출력 버퍼에서 자원 하나를 꺼낸다.
        /// 일꾼이 수거할 때 호출된다.
        /// </summary>
        /// <returns>꺼낸 자원 데이터. 버퍼가 비어 있으면 <c>null</c>.</returns>
        public ResourceDataSO PickupResource()
        {
            if (_outputBuffer.Count == 0)
            {
                Debug.LogWarning($"[ResourceTile] '{gameObject.name}' 버퍼가 비어 있어 수거할 수 없습니다.");
                return null;
            }

            return _outputBuffer.Dequeue();
        }

        // =========================================================================
        //  내부 도우미
        // =========================================================================

        /// <summary>
        /// 현재 레벨에 따른 생산 쿨다운을 계산한다.
        /// 공식: BaseCooldown / (1 + 0.2 * (Level - 1))
        /// </summary>
        private float GetCooldown()
        {
            // 비주얼 및 물류 원활 감상을 위해 고속 자원 생산 셋업 (0.2초 고정)
            return 0.2f;
        }

        /// <summary>
        /// 자원 하나를 생산하여 출력 버퍼에 추가하고, 이벤트를 발행한다.
        /// </summary>
        private void ProduceResource()
        {
            _outputBuffer.Enqueue(_resourceData);

            // 이벤트 발행
            if (_onResourceProduced != null)
            {
                ResourcePayload payload = new ResourcePayload
                {
                    ResourceData = _resourceData,
                    Amount = 1,
                    GridPosition = GridPosition
                };
                _onResourceProduced.RaiseEvent(payload);
            }
        }

        /// <summary>
        /// 타일의 시각적 상태를 업데이트한다.
        /// 생산 중이면 완전 불투명, 유휴 상태이면 반투명으로 표시한다.
        /// </summary>
        /// <param name="isIdle"><c>true</c>이면 유휴 상태(반투명), <c>false</c>이면 생산 중(불투명).</param>
        private void UpdateVisual(bool isIdle)
        {
            if (_spriteRenderer == null)
                return;

            Color baseColor = Color.white;

            // 생산 중(isIdle == false)에는 알파 0.8f, 유휴 대기 상태에는 알파 0.4f로 조정하여 
            // 쨍한 불투명 생 흰색 사각형의 비주얼을 은은하고 고급스럽게 전환!
            baseColor.a = isIdle ? 0.4f : 0.8f;
            _spriteRenderer.color = baseColor;
        }
    }
}
