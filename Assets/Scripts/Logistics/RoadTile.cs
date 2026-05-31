using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Utils;
using TMPro;

namespace FactoryDelivery.Logistics
{
    /// <summary>
    /// 도로 타일의 유형을 정의한다.
    /// </summary>
    public enum RoadType
    {
        /// <summary>직선 도로.</summary>
        Straight,
        /// <summary>교차로 (정산 시 구매 가능한 특수 타일).</summary>
        Intersection,
        /// <summary>유턴 트랙 (정산 시 구매 가능한 특수 타일).</summary>
        UTurn
    }

    /// <summary>
    /// 그리드 위에 배치되는 단방향 도로 세그먼트.
    /// 일꾼은 <see cref="Direction"/>을 따라 <see cref="NextRoad"/>로 이동하며,
    /// 역방향으로는 <see cref="PreviousRoad"/>를 통해 추적할 수 있다.
    /// </summary>
    public class RoadTile : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("도로 설정")]
        [Tooltip("이 도로의 유형")]
        [SerializeField] private RoadType _type = RoadType.Straight;

        [Tooltip("이 도로 세그먼트의 방향 (단위 벡터: 상/하/좌/우)")]
        [SerializeField] private Vector2Int _direction = Vector2Int.up;

        [Header("도로 비주얼")]
        [Tooltip("직선 도로 스프라이트 (기본 방향: 세로)")]
        [SerializeField] private Sprite _straightSprite;

        [Tooltip("커브 도로 스프라이트 (기본 방향: 아래에서 진입 후 오른쪽으로 진행)")]
        [SerializeField] private Sprite _curveSprite;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private Vector2Int _gridPosition;
        private readonly List<Worker> _occupants = new List<Worker>();

        // =========================================================================
        //  속성
        // =========================================================================

        private TextMeshPro _directionVisualText;
        private TextMeshPro _endpointLabelText;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_straightSprite == null && _spriteRenderer != null)
            {
                _straightSprite = _spriteRenderer.sprite;
            }

            // 씬에 정적으로 미리 배치된 타일의 경우 GridPosition이 (0,0) 상태로 유실되어 있을 수 있으므로
            // 자신의 월드 X, Y 좌표를 바탕으로 .5f 오프셋을 뺀 뒤 반올림하여 부동소수점 오차와 Banker's Rounding(가장 가까운 짝수 정렬)을 완벽히 퇴치합니다.
            if (_gridPosition == Vector2Int.zero)
            {
                _gridPosition = transform.position.ToGridPosition();
            }
        }

        /// <summary>이 도로의 그리드 좌표.</summary>
        public Vector2Int GridPosition
        {
            get => _gridPosition;
            set => _gridPosition = value;
        }

        /// <summary>이 도로 세그먼트가 가리키는 방향 (단위 벡터).</summary>
        public Vector2Int Direction
        {
            get => _direction;
            set
            {
                _direction = value;
                UpdateDirectionVisual();
            }
        }

        /// <summary>이 도로의 유형.</summary>
        public RoadType Type
        {
            get => _type;
            set => _type = value;
        }

        /// <summary>이 도로가 특수 타일(교차로/유턴)인지 여부.</summary>
        public bool IsSpecial => _type != RoadType.Straight;

        /// <summary>방향에 따라 연결된 다음 도로 타일.</summary>
        public RoadTile NextRoad { get; set; }

        /// <summary>이 도로를 가리키는 이전 도로 타일.</summary>
        public RoadTile PreviousRoad { get; set; }

        /// <summary>현재 이 도로 세그먼트 위에 있는 일꾼 수.</summary>
        public int OccupantCount => _occupants.Count;

        public bool HasOutgoingDirection => NormalizeCardinal(_direction) != Vector2Int.zero;

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 이 도로 세그먼트의 월드 공간 중심 좌표를 반환한다.
        /// </summary>
        /// <returns>월드 좌표.</returns>
        public Vector3 GetWorldPosition()
        {
            return _gridPosition.ToWorldPosition();
        }

        /// <summary>
        /// 일꾼이 이 도로에 진입하는 지점을 반환한다.
        /// 도로 중심에서 방향의 반대쪽으로 반 셀 오프셋.
        /// </summary>
        /// <returns>진입 월드 좌표.</returns>
        public Vector3 GetEntryPoint()
        {
            Vector3 center = GetWorldPosition();
            Vector3 offset = new Vector3(-_direction.x, 0f, -_direction.y) * (Constants.CellSize * 0.5f);
            return center + offset;
        }

        /// <summary>
        /// 일꾼이 이 도로를 빠져나가는 지점을 반환한다.
        /// 도로 중심에서 방향쪽으로 반 셀 오프셋.
        /// </summary>
        /// <returns>출구 월드 좌표.</returns>
        public Vector3 GetExitPoint()
        {
            Vector3 center = GetWorldPosition();
            Vector3 offset = new Vector3(_direction.x, 0f, _direction.y) * (Constants.CellSize * 0.5f);
            return center + offset;
        }

        /// <summary>
        /// 일꾼이 이 도로 세그먼트에 진입할 때 호출한다.
        /// </summary>
        /// <param name="worker">등록할 일꾼.</param>
        public void RegisterWorker(Worker worker)
        {
            if (worker != null && !_occupants.Contains(worker))
            {
                _occupants.Add(worker);
            }
        }

        /// <summary>
        /// 일꾼이 이 도로 세그먼트를 벗어날 때 호출한다.
        /// </summary>
        /// <param name="worker">등록 해제할 일꾼.</param>
        public void UnregisterWorker(Worker worker)
        {
            _occupants.Remove(worker);
        }

        /// <summary>
        /// 현재 이 도로 위에 있는 일꾼 목록을 읽기 전용으로 반환한다.
        /// </summary>
        public IReadOnlyList<Worker> Occupants => _occupants;

        public void ConfigureSprites(Sprite straightSprite, Sprite curveSprite)
        {
            _straightSprite = straightSprite;
            _curveSprite = curveSprite;

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            UpdateRoadSprite();
        }

        private void Start()
        {
            UpdateDirectionVisual();
        }

        /// <summary>
        /// 도로의 진행 방향(단방향)을 나타내는 화살표 텍스트 비주얼을 실시간 업데이트/생성한다.
        /// </summary>
        public void UpdateDirectionVisual()
        {
            UpdateRoadSprite();

            if (_directionVisualText == null)
            {
                // 자식 오브젝트로 방향 지시용 TextMesh 동적 생성
                GameObject txtGo = new GameObject("DirectionVisual");
                txtGo.transform.parent = transform;
                txtGo.transform.localPosition = new Vector3(0f, 0f, -0.4f); // 도로 타일 위에 그려지도록 Z 좌표 조정

                _directionVisualText = txtGo.AddComponent<TextMeshPro>();
                _directionVisualText.alignment = TextAlignmentOptions.Center;
                _directionVisualText.fontSize = 5f; // TMP 고해상도 해상도 비례 스케일링

                var meshRenderer = txtGo.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingOrder = 50; // 도로 타일 스프라이트보다 명확하게 앞에 표시되도록 정렬
                }
            }

            if (_direction == Vector2Int.zero)
            {
                _directionVisualText.text = string.Empty;
                _directionVisualText.transform.rotation = Quaternion.identity;
                return;
            }

            // 방향에 매칭되는 화살표 텍스트 선택
            string arrow = "↑";
            if (_direction == Vector2Int.right) arrow = "→";
            else if (_direction == Vector2Int.down) arrow = "↓";
            else if (_direction == Vector2Int.left) arrow = "←";

            _directionVisualText.text = arrow;

            // 도로망 연결 성공 여부에 따른 색상 분기 (초록 = 연결 성공, 빨강 = 막다른 끊김 상태)
            if (Type == RoadType.Intersection || NextRoad != null)
            {
                _directionVisualText.color = new Color(0.2f, 1.0f, 0.2f, 0.9f); // 밝은 초록
            }
            else
            {
                _directionVisualText.color = new Color(1.0f, 0.3f, 0.3f, 0.9f); // 경고용 빨강
            }

            _directionVisualText.transform.rotation = Quaternion.identity;
        }

        public void SetEndpointLabel(string label, Color color)
        {
            if (_endpointLabelText == null)
            {
                GameObject txtGo = new GameObject("EndpointLabel");
                txtGo.transform.parent = transform;
                txtGo.transform.localPosition = new Vector3(0f, Constants.CellSize * 0.32f, -0.55f);

                _endpointLabelText = txtGo.AddComponent<TextMeshPro>();
                _endpointLabelText.alignment = TextAlignmentOptions.Center;
                _endpointLabelText.fontSize = 3.2f;
                _endpointLabelText.fontStyle = FontStyles.Bold;

                var meshRenderer = txtGo.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingOrder = 65;
                }
            }

            _endpointLabelText.text = label ?? string.Empty;
            _endpointLabelText.color = color;
            _endpointLabelText.transform.rotation = Quaternion.identity;
        }

        private void UpdateRoadSprite()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (_spriteRenderer == null) return;

            Vector2Int incomingDirection = GetIncomingDirection();
            Vector2Int outgoingDirection = NormalizeCardinal(_direction);
            Vector2Int visualDirection = outgoingDirection != Vector2Int.zero
                ? outgoingDirection
                : incomingDirection;

            bool isCurve = _curveSprite != null
                && incomingDirection != Vector2Int.zero
                && outgoingDirection != Vector2Int.zero
                && incomingDirection != outgoingDirection
                && incomingDirection != -outgoingDirection;

            if (isCurve)
            {
                _spriteRenderer.sprite = _curveSprite;
                int turn = Cross(incomingDirection, outgoingDirection);
                _spriteRenderer.flipX = turn > 0;
                transform.rotation = Quaternion.Euler(0f, 0f, AngleFromUp(incomingDirection));
            }
            else
            {
                if (_straightSprite != null)
                {
                    _spriteRenderer.sprite = _straightSprite;
                }

                _spriteRenderer.flipX = false;
                transform.rotation = Quaternion.Euler(0f, 0f, AngleFromUp(visualDirection));
            }

            if (_directionVisualText != null)
            {
                _directionVisualText.transform.rotation = Quaternion.identity;
            }

            if (_endpointLabelText != null)
            {
                _endpointLabelText.transform.rotation = Quaternion.identity;
            }
        }

        private Vector2Int GetIncomingDirection()
        {
            if (PreviousRoad == null) return Vector2Int.zero;
            return NormalizeCardinal(_gridPosition - PreviousRoad.GridPosition);
        }

        private static Vector2Int NormalizeCardinal(Vector2Int direction)
        {
            if (direction == Vector2Int.up || direction == Vector2Int.right ||
                direction == Vector2Int.down || direction == Vector2Int.left)
            {
                return direction;
            }

            return Vector2Int.zero;
        }

        private static int Cross(Vector2Int a, Vector2Int b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static float AngleFromUp(Vector2Int direction)
        {
            if (direction == Vector2Int.right) return -90f;
            if (direction == Vector2Int.down) return 180f;
            if (direction == Vector2Int.left) return 90f;
            return 0f;
        }
    }
}
