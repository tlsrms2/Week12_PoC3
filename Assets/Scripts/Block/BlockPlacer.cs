using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FactoryDelivery.Data;
using FactoryDelivery.Grid;
using FactoryDelivery.Core;
using FactoryDelivery.Utils;
using FactoryDelivery.Logistics;
using FactoryDelivery.UI;
using TMPro;

namespace FactoryDelivery.Block
{
    /// <summary>
    /// <see cref="BlockInstance"/>를 그리드에 배치하는 플레이어 상호작용을 처리합니다.
    /// 마우스 커서를 따라다니는 실시간 프리뷰(초록 = 유효, 빨강 = 무효)를 제공하며,
    /// 회전 및 취소를 지원합니다.
    /// </summary>
    public class BlockPlacer : MonoBehaviour
    {
        // =========================================================================
        //  직렬화 필드
        // =========================================================================

        /// <summary>씬의 그리드 매니저 참조입니다.</summary>
        [Header("참조")]
        [SerializeField]
        [Tooltip("배치 유효성 검사에 사용되는 그리드 매니저입니다.")]
        private GridManager _gridManager;

        /// <summary>스크린-투-월드 레이캐스팅에 사용되는 카메라입니다.</summary>
        [SerializeField]
        [Tooltip("메인 카메라입니다. 할당되지 않은 경우 Camera.main으로 대체됩니다.")]
        private Camera _camera;

        /// <summary>단일 프리뷰 셀을 표시하는 데 사용되는 프리팹입니다.</summary>
        [Header("프리뷰")]
        [SerializeField]
        [Tooltip("각 프리뷰 셀에 대해 생성되는 SpriteRenderer 프리팹입니다.")]
        private SpriteRenderer _previewTilePrefab;

        /// <summary>배치가 유효할 때 프리뷰 타일에 적용되는 색상입니다.</summary>
        [SerializeField]
        [Tooltip("배치가 유효할 때의 프리뷰 색조입니다.")]
        private Color _validColor = new Color(0f, 1f, 0f, 0.5f);

        /// <summary>배치가 유효하지 않을 때 프리뷰 타일에 적용되는 색상입니다.</summary>
        [SerializeField]
        [Tooltip("배치가 유효하지 않을 때의 프리뷰 색조입니다.")]
        private Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);

        [Header("도로 설정")]
        [Tooltip("자유 도로 건설 시 사용되는 도로 타일 데이터 에셋")]
        [SerializeField] private TileDataSO _roadTileData;

        // =========================================================================
        //  이벤트
        // =========================================================================

        /// <summary>블록이 그리드에 성공적으로 배치된 후 발생합니다.</summary>
        public event Action<BlockInstance> OnBlockPlaced;

        /// <summary>플레이어가 현재 배치를 취소할 때 발생합니다.</summary>
        public event Action OnPlacementCancelled;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private BlockInstance _currentBlock;
        private Vector2Int _previewPosition;
        private bool _isPlacing;
        private readonly List<SpriteRenderer> _previewInstances = new List<SpriteRenderer>();
        private bool _currentPreviewValid;

        private bool _isBuildingRoad;
        private int _roadCost = 5;
        private Vector2Int _currentRoadDirection = Vector2Int.up;

        // Satisfactory 스타일의 도로 배치 상태
        private bool _hasStartPoint;
        private Vector2Int _startGridPosition;
        private List<Vector2Int> _currentRoadPath = new List<Vector2Int>();

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 지정된 블록에 대한 배치 모드를 시작합니다.
        /// 프리뷰 스프라이트가 생성되어 커서를 따라다닙니다.
        /// </summary>
        /// <param name="block">배치할 블록 인스턴스입니다.</param>
        public void StartPlacing(BlockInstance block)
        {
            if (block == null)
            {
                Debug.LogWarning("[BlockPlacer] StartPlacing이 null 블록으로 호출되었습니다.");
                return;
            }

            CancelPlacing(); // 이전 세션 정리

            _currentBlock = block;
            _isPlacing = true;

            CreatePreviewSprites();
        }

        /// <summary>
        /// 도로 배치 모드를 시작합니다.
        /// </summary>
        public void StartRoadBuilding(TileDataSO roadTile, int cost = 5)
        {
            CancelPlacing(); // 이전 세션 정리

            _roadTileData = roadTile;
            _roadCost = cost;
            _isBuildingRoad = true;
            _isPlacing = true;
            _currentRoadDirection = Vector2Int.up;

            _hasStartPoint = false;
            _currentRoadPath.Clear();

            _currentBlock = new BlockInstance();
            _currentBlock.PatternName = "Road Build Preview";
            _currentBlock.Cells = new List<BlockCell>
            {
                new BlockCell { LocalPosition = Vector2Int.zero, TileData = roadTile }
            };

            CreatePreviewSprites();
        }

        /// <summary>
        /// 현재 배치 모드를 취소하고 프리뷰 비주얼을 정리합니다.
        /// </summary>
        public void CancelPlacing()
        {
            if (!_isPlacing) return;

            _isPlacing = false;
            _isBuildingRoad = false;
            _currentBlock = null;
            _hasStartPoint = false;
            _currentRoadPath.Clear();
            DestroyPreviewSprites();

            OnPlacementCancelled?.Invoke();
        }

        /// <summary>배치기가 현재 배치 모드인지 여부입니다.</summary>
        public bool IsPlacing => _isPlacing;

        // =========================================================================
        //  MonoBehaviour
        // =========================================================================

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void Update()
        {
            // B 키로 도로 건설 모드 토글
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                if (_isBuildingRoad)
                {
                    CancelPlacing();
                    Debug.Log("[BlockPlacer] 도로 자유 확장 모드가 해제되었습니다.");
                }
                else
                {
                    // 블록 카드가 손에 들어있거나 드래그 중인 상태더라도 즉시 작업을 취소하고 
                    // 도로 자유 확장 모드로 묻지도 따지지도 않고 바로 바꿉니다.
                    TileDataSO roadTile = FindRoadTileData();
                    if (roadTile != null)
                    {
                        StartRoadBuilding(roadTile, 5);
                        Debug.Log("[BlockPlacer] 도로 자유 확장 모드 진입 (비용: 5 엽전 / 취소: ESC 또는 마우스 우클릭 / 단축키: B)");
                    }
                    else
                    {
                        Debug.LogError("[BlockPlacer] 도로 타일 데이터를 찾을 수 없습니다.");
                    }
                }
            }

            if (!_isPlacing || _currentBlock == null)
            {
                return;
            }

            UpdatePreviewPosition();
            UpdatePreviewVisuals();

            // 클릭하여 배치 (도로 자유 확장 모드에서만 마우스 좌클릭 즉각 건설 허용)
            // 일반 카드는 드래그를 마친 후 PointerUp 대기 시간에만 CardUI를 통해 건설을 수행하므로 대폭 충돌 사전 방지!
            if (_isBuildingRoad && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                OnClick();
            }

            if (!_isBuildingRoad && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacing();
            }

            if (_isBuildingRoad && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                TryRemoveRoadUnderMouse();
            }

            // 회전 (R 키) - 블록 회전 또는 도로 방향 순환
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (_isBuildingRoad)
                {
                    // 도로 배치 방향 시계방향 전환 회전
                    if (_currentRoadDirection == Vector2Int.up) _currentRoadDirection = Vector2Int.right;
                    else if (_currentRoadDirection == Vector2Int.right) _currentRoadDirection = Vector2Int.down;
                    else if (_currentRoadDirection == Vector2Int.down) _currentRoadDirection = Vector2Int.left;
                    else if (_currentRoadDirection == Vector2Int.left) _currentRoadDirection = Vector2Int.up;

                    UpdatePreviewVisuals();
                    Debug.Log($"[BlockPlacer] 도로 확장 방향 변경: {_currentRoadDirection}");
                }
                else
                {
                    OnRotate();
                }
            }
        }

        // =========================================================================
        //  입력 핸들러
        // =========================================================================

        /// <summary>
        /// 현재 블록을 프리뷰 위치에 배치하려고 시도합니다.
        /// </summary>
        private void OnClick()
        {
            if (_isBuildingRoad)
            {
                if (!_hasStartPoint)
                {
                    if (!IsRoadExtensionStartAllowed(_previewPosition))
                    {
                        var warningTextMgr = FindFirstObjectByType<FloatingTextManager>();
                        if (warningTextMgr != null)
                        {
                            warningTextMgr.ShowText(_previewPosition.ToWorldPosition() + new Vector3(0f, 0.5f, -0.5f), "도로를 겹칠 수 없습니다", Color.red, 2.5f);
                        }
                        return;
                    }

                    // 1단계: 시작 지점 설정
                    _startGridPosition = _previewPosition;
                    _hasStartPoint = true;
                    
                    _currentRoadPath = CalculateRoadPath(_startGridPosition, _previewPosition);
                    CreatePreviewSprites(_currentRoadPath.Count);
                    UpdatePreviewVisuals();

                    Debug.Log($"[BlockPlacer] 도로 시작 지점이 설정되었습니다: {_startGridPosition}");
                }
                else
                {
                    // 2단계: 끝 지점 설정 및 경로 전체 일괄 배치
                    if (!_currentPreviewValid) return;

                    var quotaMgr = FindFirstObjectByType<QuotaManager>();
                    if (quotaMgr != null)
                    {
                        // 기존 도로가 아닌 곳만 건설 비용 계산 및 가이드라인 형성 대상에서 제외됩니다.
                        List<Vector2Int> tilesToPlace = new List<Vector2Int>();
                        foreach (var pos in _currentRoadPath)
                        {
                            if (_gridManager.GetRoadTileAt(pos) == null)
                            {
                                tilesToPlace.Add(pos);
                            }
                        }

                        int currentRoadCost = _roadCost;
                        if (MandateManager.Instance != null && MandateManager.Instance.HasMandate(MandateType.MasterStrokeRoad))
                        {
                            currentRoadCost = Mathf.CeilToInt(_roadCost * 0.5f);
                        }

                        int totalCost = currentRoadCost * tilesToPlace.Count;
                        if (quotaMgr.WalletBalance >= totalCost)
                        {
                            quotaMgr.TrySpendProgress(totalCost);

                            for (int i = 0; i < _currentRoadPath.Count; i++)
                            {
                                Vector2Int pos = _currentRoadPath[i];
                                Vector2Int dir = Vector2Int.up;
                                if (i < _currentRoadPath.Count - 1)
                                {
                                    dir = _currentRoadPath[i + 1] - _currentRoadPath[i];
                                }
                                else
                                {
                                    dir = Vector2Int.zero;
                                }

                                // 기존 도로가 있는 곳은 도로 배치를 하지 않고, 필요에 따라 방향만 업데이트해 줍니다.
                                if (_gridManager.GetRoadTileAt(pos) != null)
                                {
                                    if (i != 0 && i != _currentRoadPath.Count - 1)
                                    {
                                        Debug.LogWarning("[BlockPlacer] 연장 시작 지점 외에서 도로 중복이 감지되었습니다. 배치가 취소되었습니다.");
                                        return;
                                    }

                                    var existingRoad = GetRoadTileComponentAt(pos);
                                    if (existingRoad != null && i == 0)
                                    {
                                        existingRoad.Direction = dir;
                                    }
                                    continue;
                                }

                                _gridManager.TryPlaceTile(pos, _roadTileData, out _, dir);
                            }

                            Debug.Log($"[BlockPlacer] 도로 {tilesToPlace.Count}개 일괄 배치 완료 (기존 도로 제외). {totalCost} 엽전 차감.");

                            // 상태 리셋
                            _hasStartPoint = false;
                            _currentRoadPath.Clear();
                            CreatePreviewSprites(1);
                            UpdatePreviewVisuals();
                        }
                    }
                }
                return;
            }

            List<BlockCell> cells = _currentBlock.GetCurrentCells();

            // 그리드 매니저를 통해 각 셀을 배치합니다.
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int worldPos = _previewPosition + cells[i].LocalPosition;
                
                Vector2Int? roadDir = null;
                if (cells[i].TileData != null && cells[i].TileData.Type == TileType.Road)
                {
                    roadDir = _currentBlock.CurrentRotation switch
                    {
                        1 => Vector2Int.right,
                        2 => Vector2Int.down,
                        3 => Vector2Int.left,
                        _ => Vector2Int.up
                    };
                }

                _gridManager.TryPlaceTile(worldPos, cells[i].TileData, out _, roadDir);
            }

            BlockInstance placedBlock = _currentBlock;

            _isPlacing = false;
            _currentBlock = null;
            DestroyPreviewSprites();

            OnBlockPlaced?.Invoke(placedBlock);
        }

        /// <summary>
        /// 현재 블록을 시계 방향으로 90도 회전하고 프리뷰를 새로 고칩니다.
        /// </summary>
        private void OnRotate()
        {
            _currentBlock.Rotate();
            RefreshPreviewSprites();
        }

        private void TryRemoveRoadUnderMouse()
        {
            Vector2Int targetPos = GetGridPositionUnderMouse();
            if (_gridManager == null || !_gridManager.IsInBounds(targetPos)) return;

            TileEntity roadEntity = _gridManager.GetRoadTileAt(targetPos);
            if (roadEntity == null) return;

            if (!_gridManager.RemoveRoadTile(targetPos)) return;

            int refund = Mathf.RoundToInt(_roadCost * 0.5f);
            QuotaManager quotaMgr = FindFirstObjectByType<QuotaManager>();
            if (quotaMgr != null)
            {
                quotaMgr.AddWalletBalance(refund);
            }

            var textMgr = FindFirstObjectByType<FloatingTextManager>();
            if (textMgr != null)
            {
                textMgr.ShowText(targetPos.ToWorldPosition() + new Vector3(0f, 0.4f, -0.5f), $"+{refund}", new Color(0.95f, 0.85f, 0.3f), 1.6f);
            }

            if (_hasStartPoint)
            {
                _currentRoadPath = CalculateRoadPath(_startGridPosition, _previewPosition);
                CreatePreviewSprites(_currentRoadPath.Count);
                UpdatePreviewVisuals();
            }
        }

        // =========================================================================
        //  프리뷰 헬퍼
        // =========================================================================

        /// <summary>
        /// 현재 마우스/커서 위치를 기반으로 <see cref="_previewPosition"/>을 업데이트합니다.
        /// </summary>
        private void UpdatePreviewPosition()
        {
            if (_camera == null) return;

            Vector2 mouseScreenPos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;

            Ray ray = _camera.ScreenPointToRay(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));

            // XY 평면(z = 0)에 투영합니다.
            Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPoint = ray.GetPoint(distance);
                _previewPosition = new Vector2Int(
                    Mathf.FloorToInt(worldPoint.x / Constants.CellSize),
                    Mathf.FloorToInt(worldPoint.y / Constants.CellSize)
                );
            }
        }

        /// <summary>
        /// 프리뷰 스프라이트를 현재 프리뷰 위치로 이동시키고 배치 유효성 및 타일 특성에 따라 색상을 입힙니다.
        /// </summary>
        private void UpdatePreviewVisuals()
        {
            if (_isBuildingRoad)
            {
                // 임시 경로 계산
                if (_hasStartPoint)
                {
                    _currentRoadPath = CalculateRoadPath(_startGridPosition, _previewPosition);
                }
                else
                {
                    _currentRoadPath = new List<Vector2Int> { _previewPosition };
                }

                // 프리뷰 인스턴스 개수 맞추기
                if (_previewInstances.Count != _currentRoadPath.Count)
                {
                    CreatePreviewSprites(_currentRoadPath.Count);
                }

                // 전체 경로에 대한 건설 가능 상태 검사 (기존 도로가 있는 곳은 제외하고 비용 산정)
                var quotaMgr = FindFirstObjectByType<QuotaManager>();
                
                int newRoadsCount = 0;
                foreach (var pos in _currentRoadPath)
                {
                    if (_gridManager.GetRoadTileAt(pos) == null)
                    {
                        newRoadsCount++;
                    }
                }

                int totalCost = _roadCost * newRoadsCount;
                bool goldSufficient = (quotaMgr == null || quotaMgr.WalletBalance >= totalCost);

                _currentPreviewValid = goldSufficient;
                if (_currentPreviewValid)
                {
                    _currentPreviewValid = _hasStartPoint
                        ? IsRoadPathValidForOneWayExtension(_currentRoadPath)
                        : IsRoadExtensionStartAllowed(_previewPosition);
                }

                if (_currentPreviewValid)
                {
                    for (int pathIndex = 0; pathIndex < _currentRoadPath.Count; pathIndex++)
                    {
                        Vector2Int pos = _currentRoadPath[pathIndex];

                        // 기존 도로의 전방 종점인 칸에서만 새 도로 연결로 허용합니다.
                        if (_gridManager.GetRoadTileAt(pos) != null)
                        {
                            continue;
                        }

                        if (!_gridManager.CanPlaceOrLevelUp(pos, _roadTileData))
                        {
                            _currentPreviewValid = false;
                            break;
                        }
                    }
                }

                for (int i = 0; i < _previewInstances.Count && i < _currentRoadPath.Count; i++)
                {
                    Vector2Int worldPos = _currentRoadPath[i];
                    _previewInstances[i].transform.position = worldPos.ToWorldPosition();

                    // 색상 설정 (유효하면 연두색, 부족하거나 막히면 빨간색)
                    if (_currentPreviewValid)
                    {
                        _previewInstances[i].color = new Color(0.2f, 0.9f, 0.2f, 0.7f); // 연두색 투명
                    }
                    else
                    {
                        _previewInstances[i].color = new Color(0.9f, 0.2f, 0.2f, 0.7f); // 빨간색 투명
                    }

                    // [개선] 도로 방향에 따른 실제 직선/커브 스프라이트 및 회전 표시를 반영
                    if (_roadTileData != null)
                    {
                        Vector2Int incomingDirection = Vector2Int.zero;
                        Vector2Int outgoingDirection = Vector2Int.zero;

                        if (i > 0) incomingDirection = worldPos - _currentRoadPath[i - 1];
                        if (i < _currentRoadPath.Count - 1) outgoingDirection = _currentRoadPath[i + 1] - worldPos;

                        Vector2Int visualDirection = outgoingDirection != Vector2Int.zero ? outgoingDirection : incomingDirection;

                        bool isCurve = _roadTileData.RoadCurveSprite != null &&
                                       incomingDirection != Vector2Int.zero &&
                                       outgoingDirection != Vector2Int.zero &&
                                       incomingDirection != outgoingDirection &&
                                       incomingDirection != -outgoingDirection;

                        if (isCurve)
                        {
                            _previewInstances[i].sprite = _roadTileData.RoadCurveSprite;
                            int turn = (incomingDirection.x * outgoingDirection.y) - (incomingDirection.y * outgoingDirection.x);
                            _previewInstances[i].flipX = turn > 0;

                            float angle = 0f;
                            if (incomingDirection == Vector2Int.right) angle = -90f;
                            else if (incomingDirection == Vector2Int.down) angle = 180f;
                            else if (incomingDirection == Vector2Int.left) angle = 90f;
                            
                            _previewInstances[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        }
                        else
                        {
                            _previewInstances[i].sprite = _roadTileData.Sprite;
                            _previewInstances[i].flipX = false;
                            
                            float angle = 0f;
                            if (visualDirection == Vector2Int.right) angle = -90f;
                            else if (visualDirection == Vector2Int.down) angle = 180f;
                            else if (visualDirection == Vector2Int.left) angle = 90f;
                            
                            _previewInstances[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        }
                    }

                    _previewInstances[i].transform.localScale = Vector3.one;

                    var previewText = _previewInstances[i].GetComponentInChildren<TextMeshPro>();
                    if (previewText == null)
                    {
                        GameObject txtGo = new GameObject("PreviewDirectionVisual");
                        txtGo.transform.parent = _previewInstances[i].transform;
                        txtGo.transform.localPosition = new Vector3(0f, 0f, -0.4f);

                        previewText = txtGo.AddComponent<TextMeshPro>();
                        previewText.alignment = TextAlignmentOptions.Center;
                        previewText.fontSize = 4f;

                        var meshRenderer = txtGo.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            meshRenderer.sortingOrder = 100;
                        }
                    }

                    // 프리뷰 스프라이트가 회전하더라도 텍스트는 정방향 0도로 유지하도록 보정
                    if (previewText != null)
                    {
                        previewText.transform.rotation = Quaternion.identity;
                        UpdateRoadPreviewLabel(previewText, i);
                    }
                }
                return;
            }

            // 일반 블록 카드 프리뷰 렌더링
            List<BlockCell> cells = _currentBlock.GetCurrentCells();
            _currentPreviewValid = GridValidator.CanPlaceBlock(_gridManager, cells, _previewPosition);

            for (int i = 0; i < _previewInstances.Count && i < cells.Count; i++)
            {
                Vector2Int worldPos = _previewPosition + cells[i].LocalPosition;
                _previewInstances[i].transform.position = worldPos.ToWorldPosition();

                // 일반 카드의 회전이나 반전을 기본적으로 같게 초기화
                _previewInstances[i].transform.rotation = Quaternion.identity;
                _previewInstances[i].flipX = false;

                if (_currentPreviewValid)
                {
                    _previewInstances[i].color = new Color(0.2f, 0.9f, 0.2f, 0.7f); // 연두색 투명
                }
                else
                {
                    _previewInstances[i].color = new Color(0.9f, 0.2f, 0.2f, 0.7f); // 빨간색 투명
                }

                if (cells[i].TileData != null)
                {
                    Sprite targetSprite = cells[i].TileData.Sprite;

                    // [개선] 이미 타일이 있고 레벨업 가능한 상태라면, 실제 설치될 레벨의 (현재+1) 스프라이트를 반영!
                    TileEntity existingTile = _gridManager.GetTileAt(worldPos);
                    if (existingTile != null && existingTile.Data == cells[i].TileData && existingTile.CanLevelUp)
                    {
                        int nextLevel = existingTile.Level + 1;
                        if (cells[i].TileData.LevelSprites != null && cells[i].TileData.LevelSprites.Length > 0)
                        {
                            int index = Mathf.Clamp(nextLevel - 1, 0, cells[i].TileData.LevelSprites.Length - 1);
                            targetSprite = cells[i].TileData.LevelSprites[index] ?? cells[i].TileData.Sprite;
                        }
                    }

                    _previewInstances[i].sprite = targetSprite;
                }

                _previewInstances[i].transform.localScale = Vector3.one;

                // (일반 카드에 대한 도로 관련 체크 제거 코드는 이전 버전 유지)
            }
        }

        private TileDataSO FindRoadTileData()
        {
            // 1. 인스펙터에 직접 등록된 도로 타일 데이터를 가져와서 최우선 적용
            if (_roadTileData != null) return _roadTileData;

            // 2. Resources 폴더에서 직접 타일 에셋을 검색하여 최고 속도 반환
            TileDataSO road = Resources.Load<TileDataSO>("Tiles/Tile_Road");
            if (road != null) return road;

            // 3. 차선책으로 프로젝트 내의 메모리 상의 도로 타일 캐싱
            var allTiles = UnityEngine.Resources.FindObjectsOfTypeAll<TileDataSO>();
            if (allTiles != null && allTiles.Length > 0)
            {
                return System.Array.Find(allTiles, t => t.Type == TileType.Road);
            }
            return null;
        }

        private void CreatePreviewSprites(int count = -1)
        {
            DestroyPreviewSprites();

            if (_previewTilePrefab == null)
            {
                Debug.LogWarning("[BlockPlacer] 할당된 프리뷰 타일 프리팹이 없습니다.");
                return;
            }

            int targetCount = count;
            if (targetCount < 0 && _currentBlock != null)
            {
                targetCount = _currentBlock.GetCurrentCells().Count;
            }

            for (int i = 0; i < targetCount; i++)
            {
                SpriteRenderer sr = Instantiate(_previewTilePrefab, transform);
                sr.gameObject.name = $"Preview_{i}";
                _previewInstances.Add(sr);
            }
        }

        /// <summary>
        /// 프리뷰 스프라이트를 제거하고 다시 생성합니다.
        /// 회전으로 인해 셀 수나 배치가 변경된 후 호출됩니다.
        /// </summary>
        private void RefreshPreviewSprites()
        {
            CreatePreviewSprites();
        }

        /// <summary>
        /// 현재 활성화된 모든 프리뷰 스프라이트를 제거합니다.
        /// </summary>
        private void DestroyPreviewSprites()
        {
            for (int i = 0; i < _previewInstances.Count; i++)
            {
                if (_previewInstances[i] != null)
                {
                    Destroy(_previewInstances[i].gameObject);
                }
            }

            _previewInstances.Clear();
        }

        private Vector2Int GetGridPositionUnderMouse()
        {
            if (_camera == null) return Vector2Int.zero;

            Vector2 mouseScreenPos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;

            Ray ray = _camera.ScreenPointToRay(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));

            Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPoint = ray.GetPoint(distance);
                return new Vector2Int(
                    Mathf.FloorToInt(worldPoint.x / Constants.CellSize),
                    Mathf.FloorToInt(worldPoint.y / Constants.CellSize)
                );
            }
            return Vector2Int.zero;
        }

        private RoadTile GetRoadTileComponentAt(Vector2Int pos)
        {
            var roadEntity = _gridManager.GetRoadTileAt(pos);
            if (roadEntity == null) return null;

            var allRoads = FindObjectsByType<RoadTile>(FindObjectsSortMode.None);
            foreach (var r in allRoads)
            {
                if (r != null && r.GridPosition == pos)
                {
                    return r;
                }
            }
            return null;
        }

        private void PropagateRoadDirections(RoadTile startTile)
        {
            if (startTile == null) return;

            var allRoads = FindObjectsByType<RoadTile>(FindObjectsSortMode.None);
            
            var roadMap = new Dictionary<Vector2Int, RoadTile>();
            foreach (var r in allRoads)
            {
                if (r != null)
                {
                    roadMap[r.GridPosition] = r;
                }
            }

            var visited = new HashSet<RoadTile>();
            var parentMap = new Dictionary<RoadTile, RoadTile>();
            
            DFS(startTile, null, visited, parentMap, roadMap, startTile);

            GridManager.RebuildRoadNetwork();

        }

        private void DFS(
            RoadTile current, 
            RoadTile parent, 
            HashSet<RoadTile> visited,
            Dictionary<RoadTile, RoadTile> parentMap,
            Dictionary<Vector2Int, RoadTile> roadMap,
            RoadTile startTile)
        {
            visited.Add(current);
            parentMap[current] = parent;

            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            var unvisitedNeighbors = new List<RoadTile>();
            RoadTile startNeighbor = null;

            foreach (var d in dirs)
            {
                Vector2Int neighborPos = current.GridPosition + d;
                if (roadMap.TryGetValue(neighborPos, out var neighbor))
                {
                    if (neighbor == startTile && neighbor != parent)
                    {
                        startNeighbor = neighbor; // 시작 지점으로의 루프 감지
                    }
                    if (!visited.Contains(neighbor))
                    {
                        unvisitedNeighbors.Add(neighbor);
                    }
                }
            }

            if (unvisitedNeighbors.Count > 0)
            {
                var next = unvisitedNeighbors[0];
                current.Direction = next.GridPosition - current.GridPosition;

                DFS(next, current, visited, parentMap, roadMap, startTile);

                for (int i = 1; i < unvisitedNeighbors.Count; i++)
                {
                    var other = unvisitedNeighbors[i];
                    other.Direction = other.GridPosition - current.GridPosition;
                    DFS(other, current, visited, parentMap, roadMap, startTile);
                }
            }
            else
            {
                if (startNeighbor != null)
                {
                    current.Direction = Vector2Int.zero;
                }
                else if (parent != null)
                {
                    current.Direction = Vector2Int.zero;
                }
            }
        }

        private void UpdateRoadPreviewLabel(TextMeshPro previewText, int pathIndex)
        {
            if (previewText == null) return;

            previewText.text = string.Empty;
            previewText.fontSize = 3.2f;
            previewText.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);

            if (!_hasStartPoint || pathIndex == 0)
            {
                previewText.text = "출발";
                previewText.color = new Color(0.2f, 1.0f, 1.0f, 0.95f);
                return;
            }

            if (pathIndex == _currentRoadPath.Count - 1 && _currentRoadPath.Count > 1)
            {
                previewText.text = "도착";
                previewText.color = new Color(0.2f, 1.0f, 0.2f, 0.95f);
            }
        }

        private bool IsRoadExtensionStartAllowed(Vector2Int position)
        {
            if (_gridManager == null || !_gridManager.IsInBounds(position)) return false;

            if (_gridManager.GetRoadTileAt(position) == null)
            {
                return _gridManager.CanPlaceOrLevelUp(position, _roadTileData);
            }

            RoadTile road = GetRoadTileComponentAt(position);
            return road != null && road.NextRoad == null;
        }

        private bool IsRoadPathValidForOneWayExtension(List<Vector2Int> path)
        {
            if (path == null || path.Count == 0) return false;

            for (int i = 0; i < path.Count; i++)
            {
                bool hasRoad = _gridManager.GetRoadTileAt(path[i]) != null;
                if (!hasRoad) continue;

                bool isStart = i == 0;
                bool isEnd = i == path.Count - 1;
                if (!isStart && !isEnd)
                {
                    return false;
                }

                if (isStart && !IsRoadExtensionStartAllowed(path[i]))
                {
                    return false;
                }

                if (path.Count == 1)
                {
                    return false;
                }
            }

            return true;
        }

        private List<Vector2Int> CalculateRoadPath(Vector2Int start, Vector2Int end)
        {
            var path = new List<Vector2Int>();
            if (start == end)
            {
                path.Add(start);
                return path;
            }

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var parentMap = new Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(start);
            visited.Add(start);
            parentMap[start] = new Vector2Int(-999, -999);

            bool found = false;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                if (current == end)
                {
                    found = true;
                    break;
                }

                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
                foreach (var d in dirs)
                {
                    Vector2Int neighbor = current + d;
                    if (_gridManager.IsInBounds(neighbor) && !visited.Contains(neighbor))
                    {
                        // 도로 프리뷰는 이미 선택된 시작 셀을 제외하고 기존 도로와 겹칠 수 없습니다.
                        TileEntity existingEntity = _gridManager.GetTileAt(neighbor);
                        bool isResource = existingEntity != null && existingEntity.IsResource;

                        bool isFacility = existingEntity != null && existingEntity.IsFacility;
                        
                        bool isWalkable = _gridManager.IsEmpty(neighbor) || isResource || isFacility;
                        
                        if (isWalkable)
                        {
                            visited.Add(neighbor);
                            parentMap[neighbor] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (found)
            {
                Vector2Int curr = end;
                while (curr != new Vector2Int(-999, -999))
                {
                    path.Add(curr);
                    curr = parentMap[curr];
                }
                path.Reverse();
            }
            else
            {
                // 맨해튼 경로로 대체
                path = GetManhattanPath(start, end);
            }

            return path;
        }

        private List<Vector2Int> GetManhattanPath(Vector2Int start, Vector2Int end)
        {
            var path = new List<Vector2Int>();
            Vector2Int curr = start;
            path.Add(curr);

            while (curr.x != end.x)
            {
                curr.x += (end.x > curr.x) ? 1 : -1;
                path.Add(curr);
            }

            while (curr.y != end.y)
            {
                curr.y += (end.y > curr.y) ? 1 : -1;
                path.Add(curr);
            }

            return path;
        }
    }
}
