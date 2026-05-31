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
    /// Handles player interaction for placing <see cref="BlockInstance"/>s onto the grid.
    /// Provides a real-time preview (green = valid, red = invalid) that follows
    /// the mouse cursor, with rotation and cancellation support.
    /// </summary>
    public class BlockPlacer : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Serialized Fields
        // ─────────────────────────────────────────────

        /// <summary>Reference to the scene's grid manager.</summary>
        [Header("References")]
        [SerializeField]
        [Tooltip("The grid manager used for placement validation.")]
        private GridManager _gridManager;

        /// <summary>Camera used for screen-to-world raycasting.</summary>
        [SerializeField]
        [Tooltip("Main camera. Falls back to Camera.main if not assigned.")]
        private Camera _camera;

        /// <summary>Prefab used to display a single preview cell.</summary>
        [Header("Preview")]
        [SerializeField]
        [Tooltip("SpriteRenderer prefab instantiated for each preview cell.")]
        private SpriteRenderer _previewTilePrefab;

        /// <summary>Color applied to preview tiles when placement is valid.</summary>
        [SerializeField]
        [Tooltip("Preview tint when placement is valid.")]
        private Color _validColor = new Color(0f, 1f, 0f, 0.5f);

        /// <summary>Color applied to preview tiles when placement is invalid.</summary>
        [SerializeField]
        [Tooltip("Preview tint when placement is invalid.")]
        private Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);

        [Header("도로 설정")]
        [Tooltip("자유 도로 건설 시 사용될 도로 타일 데이터 에셋")]
        [SerializeField] private TileDataSO _roadTileData;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>Fired after a block has been successfully placed on the grid.</summary>
        public event Action<BlockInstance> OnBlockPlaced;

        /// <summary>Fired when the player cancels the current placement.</summary>
        public event Action OnPlacementCancelled;

        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        private BlockInstance _currentBlock;
        private Vector2Int _previewPosition;
        private bool _isPlacing;
        private readonly List<SpriteRenderer> _previewInstances = new List<SpriteRenderer>();
        private bool _currentPreviewValid;

        private bool _isBuildingRoad;
        private int _roadCost = 5;
        private Vector2Int _currentRoadDirection = Vector2Int.up;

        // Satisfactory-style Road Placement State
        private bool _hasStartPoint;
        private Vector2Int _startGridPosition;
        private List<Vector2Int> _currentRoadPath = new List<Vector2Int>();

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Begins placement mode for the given block.
        /// Preview sprites are spawned and will follow the cursor.
        /// </summary>
        /// <param name="block">The block instance to place.</param>
        public void StartPlacing(BlockInstance block)
        {
            if (block == null)
            {
                Debug.LogWarning("[BlockPlacer] StartPlacing called with a null block.");
                return;
            }

            CancelPlacing(); // clean up any previous session

            _currentBlock = block;
            _isPlacing = true;

            CreatePreviewSprites();
        }

        /// <summary>
        /// Begins road placement mode.
        /// </summary>
        public void StartRoadBuilding(TileDataSO roadTile, int cost = 5)
        {
            CancelPlacing(); // clean up any previous session

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
        /// Cancels the current placement mode and cleans up preview visuals.
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

        /// <summary>Whether the placer is currently in placement mode.</summary>
        public bool IsPlacing => _isPlacing;

        // ─────────────────────────────────────────────
        //  MonoBehaviour
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void Update()
        {
            // B key to toggle Road Building Mode
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                if (_isBuildingRoad)
                {
                    CancelPlacing();
                    Debug.Log("[BlockPlacer] 도로 자유 포장 모드가 해제되었습니다.");
                }
                else
                {
                    // 블록 카드가 덱에 남아있거나 심지어 손에 쥐어져 있더라도 즉시 작업을 취소하고 
                    // 도로 자유 포장 모드를 묻지도 따지지도 않고 바로 켜 줍니다!
                    TileDataSO roadTile = FindRoadTileData();
                    if (roadTile != null)
                    {
                        StartRoadBuilding(roadTile, 5);
                        Debug.Log("[BlockPlacer] 도로 자유 포장 모드 진입 (비용: 5 엽전 / 취소: ESC 또는 마우스 우클릭 / 단축키: B)");
                    }
                    else
                    {
                        Debug.LogError("[BlockPlacer] 도로 타일 데이터를 찾을 수 없습니다.");
                    }
                }
            }

            if (!_isPlacing || _currentBlock == null)
            {
                // If not placing anything, clicking on a road tile sets the start position!
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    Vector2Int clickedGridPos = GetGridPositionUnderMouse();
                    if (_gridManager.IsInBounds(clickedGridPos))
                    {
                        var roadTile = GetRoadTileComponentAt(clickedGridPos);
                        if (roadTile != null)
                        {
                            PropagateRoadDirections(roadTile);
                            Debug.Log($"[BlockPlacer] 클릭된 도로 타일 {clickedGridPos}를 시작점으로 설정하고 방향을 전파합니다.");
                        }
                    }

                }
                return;
            }

            UpdatePreviewPosition();
            UpdatePreviewVisuals();

            // Click to place (자유 도로 포장 모드일 때만 마우스 좌클릭 즉각 건설 허용)
            // 일반 카드는 드래그를 마친 후 PointerUp 떼기 순간에만 CardUI에 의해 건설이 수행되므로 오폭 충돌 완전 방지!
            if (_isBuildingRoad && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                OnClick();
            }

            // Cancel (Escape or Right click)
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            {
                if (_isBuildingRoad && _hasStartPoint)
                {
                    _hasStartPoint = false;
                    _currentRoadPath.Clear();
                    CreatePreviewSprites(1); // Reset preview to a single cell
                    UpdatePreviewVisuals();
                    Debug.Log("[BlockPlacer] 도로 시작 지점 선택이 취소되었습니다.");
                }
                else
                {
                    CancelPlacing();
                }
            }

            // Rotate (R key) - rotate block or cycle road direction
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (_isBuildingRoad)
                {
                    // 도로 배치 방향 시계방향 순환 회전
                    if (_currentRoadDirection == Vector2Int.up) _currentRoadDirection = Vector2Int.right;
                    else if (_currentRoadDirection == Vector2Int.right) _currentRoadDirection = Vector2Int.down;
                    else if (_currentRoadDirection == Vector2Int.down) _currentRoadDirection = Vector2Int.left;
                    else if (_currentRoadDirection == Vector2Int.left) _currentRoadDirection = Vector2Int.up;

                    UpdatePreviewVisuals();
                    Debug.Log($"[BlockPlacer] 도로 포장 방향 변경: {_currentRoadDirection}");
                }
                else
                {
                    OnRotate();
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Input Handlers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Attempts to place the current block at the preview position.
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
                            warningTextMgr.ShowText(_previewPosition.ToWorldPosition() + new Vector3(0f, 0.5f, -0.5f), "끝점에서만 연결", Color.red, 2.5f);
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
                        // 이미 도로가 놓인 타일은 건설 비용 계산 및 새 타일 생성 대상에서 제외합니다!
                        List<Vector2Int> tilesToPlace = new List<Vector2Int>();
                        foreach (var pos in _currentRoadPath)
                        {
                            if (_gridManager.GetRoadTileAt(pos) == null)
                            {
                                tilesToPlace.Add(pos);
                            }
                        }

                        int totalCost = _roadCost * tilesToPlace.Count;
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

                                // 이미 도로가 있는 곳은 새로 배치하지 않고, 필요에 따라 방향만 업데이트해 줍니다.
                                if (_gridManager.GetRoadTileAt(pos) != null)
                                {
                                    if (i != 0)
                                    {
                                        Debug.LogWarning("[BlockPlacer] Road overlap detected outside the extension start. Placement cancelled.");
                                        return;
                                    }

                                    var existingRoad = GetRoadTileComponentAt(pos);
                                    if (existingRoad != null)
                                    {
                                        existingRoad.Direction = dir;
                                    }
                                    continue;
                                }

                                _gridManager.TryPlaceTile(pos, _roadTileData, out _, dir);
                            }

                            Debug.Log($"[BlockPlacer] 도로망 {tilesToPlace.Count}개 일괄 배치 완료 (기존 도로 제외). {totalCost} 엽전 차감.");

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

            // Place each cell via the grid manager.
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
        /// Rotates the current block 90° clockwise and refreshes the preview.
        /// </summary>
        private void OnRotate()
        {
            _currentBlock.Rotate();
            RefreshPreviewSprites();
        }

        // ─────────────────────────────────────────────
        //  Preview Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Updates <see cref="_previewPosition"/> based on the current mouse/cursor position.
        /// </summary>
        private void UpdatePreviewPosition()
        {
            if (_camera == null) return;

            Vector2 mouseScreenPos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;

            Ray ray = _camera.ScreenPointToRay(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));

            // Project onto the XY plane (z = 0).
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
        /// Moves preview sprites to match the current preview position and
        /// tints them according to placement validity and tile characteristics.
        /// </summary>
        private void UpdatePreviewVisuals()
        {
            if (_isBuildingRoad)
            {
                // 실시간 경로 계산
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

                // 전체 경로에 대한 건설 가능 상태 검증 (이미 도로가 있는 곳은 제외하고 비용 산정)
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

                        // 기존 도로는 단방향 끝점인 첫 칸에서만 새 도로 연결용으로 허용합니다.
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

                    // 색상 설정 (유효하면 연두색, 부족하거나 막히면 붉은색)
                    if (_currentPreviewValid)
                    {
                        _previewInstances[i].color = new Color(0.2f, 0.9f, 0.2f, 0.7f); // 연두색 투명
                    }
                    else
                    {
                        _previewInstances[i].color = new Color(0.9f, 0.2f, 0.2f, 0.7f); // 빨간색 투명
                    }

                    // [개선] 도로 방향에 따른 실제 직선/커브 스프라이트 및 회전 실시간 반영
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

                    // 부모 스프라이트가 회전하더라도 텍스트는 정방향(0도)을 유지하도록 보정
                    if (previewText != null)
                    {
                        previewText.transform.rotation = Quaternion.identity;
                        UpdateRoadPreviewLabel(previewText, i);
                    }
                }
                return;
            }

            // 일반 블록 카드 건설 모드 프리뷰 렌더링
            List<BlockCell> cells = _currentBlock.GetCurrentCells();
            _currentPreviewValid = GridValidator.CanPlaceBlock(_gridManager, cells, _previewPosition);

            for (int i = 0; i < _previewInstances.Count && i < cells.Count; i++)
            {
                Vector2Int worldPos = _previewPosition + cells[i].LocalPosition;
                _previewInstances[i].transform.position = worldPos.ToWorldPosition();

                // 일반 카드는 회전이나 반전이 기본적으로 없게 초기화
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

                    // [개선] 이미 타일이 있고 레벨업 가능한 상태라면, 실제 설치될(레벨업 후의) 스프라이트를 반영!
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

                // (일반 카드에 대한 도로 관련 체크 레거시 코드 완전히 삭제됨)
            }
        }

        private TileDataSO FindRoadTileData()
        {
            // 1. 인스펙터에 직접 등록된 도로 타일 데이터가 있다면 최우선 적용
            if (_roadTileData != null) return _roadTileData;

            // 2. Resources 폴더에서 직접 타일 에셋을 탐색하여 초고속 반환
            TileDataSO road = Resources.Load<TileDataSO>("Tiles/Tile_Road");
            if (road != null) return road;

            // 3. 차선책으로 씬 내부 및 프로젝트 메모리 상의 도로 타일 캐싱
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
                Debug.LogWarning("[BlockPlacer] No preview tile prefab assigned.");
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
        /// Destroys and re-creates preview sprites.
        /// Called after rotation changes the cell count/arrangement.
        /// </summary>
        private void RefreshPreviewSprites()
        {
            CreatePreviewSprites();
        }

        /// <summary>
        /// Destroys all currently active preview sprites.
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
                        startNeighbor = neighbor; // Loop back to start detected
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

                if (i != 0 || !IsRoadExtensionStartAllowed(path[i]))
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
                        // Road preview may not overlap existing roads, except for the already-selected start cell.
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
                // Fallback to Manhattan path
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
