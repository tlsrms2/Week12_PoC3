using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Events;
using FactoryDelivery.Utils;
using TMPro;

namespace FactoryDelivery.Grid
{
    /// <summary>
    /// Manages the runtime state of the game grid.
    /// The grid is a fixed <see cref="Constants.GridWidth"/> × <see cref="Constants.GridHeight"/>
    /// array of <see cref="TileEntity"/> slots.
    /// <para>
    /// This MonoBehaviour is designed to be wired via the Inspector (serialized-field singleton)
    /// rather than using a static <c>Instance</c> accessor.
    /// </para>
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Serialized Fields
        // ─────────────────────────────────────────────

        [Header("Visuals (PoC Setup)")]
        [SerializeField]
        [Tooltip("타일을 시각적으로 나타낼 SpriteRenderer 프리팹 (지정하지 않으면 런타임에 자동 생성)")]
        private SpriteRenderer _tileVisualPrefab;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _tileVisuals = new Dictionary<Vector2Int, SpriteRenderer>();

        [Header("Event Channels")]

        /// <summary>SO channel raised whenever a new tile is placed on the grid.</summary>
        [SerializeField]
        [Tooltip("Raised when a tile is placed.")]
        private VoidEventChannelSO _onTilePlacedChannel;

        /// <summary>SO channel raised whenever a tile is removed from the grid.</summary>
        [SerializeField]
        [Tooltip("Raised when a tile is removed.")]
        private VoidEventChannelSO _onTileRemovedChannel;

        /// <summary>SO channel raised whenever an existing tile is leveled up.</summary>
        [SerializeField]
        [Tooltip("Raised when a tile is leveled up.")]
        private VoidEventChannelSO _onTileLeveledUpChannel;

        // ─────────────────────────────────────────────
        //  C# Events (for direct subscribers)
        // ─────────────────────────────────────────────

        /// <summary>Fired after a tile has been successfully placed. Payload is the new entity.</summary>
        public event Action<TileEntity> OnTilePlaced;

        /// <summary>Fired after a tile has been removed. Payload is the position that was cleared.</summary>
        public event Action<Vector2Int> OnTileRemoved;

        /// <summary>Fired after a tile has been leveled up. Payload is the leveled entity.</summary>
        public event Action<TileEntity> OnTileLeveledUp;

        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        private TileEntity[,] _grid;
        private TileEntity[,] _roadGrid;

        /// <summary>Whether the grid has been initialized.</summary>
        public bool IsInitialized => _grid != null && _roadGrid != null;

        // ─────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (!IsInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Creates a fresh, empty grid of size
        /// <see cref="Constants.GridWidth"/> × <see cref="Constants.GridHeight"/>.
        /// This should be called once during scene setup.
        /// </summary>
        public void Initialize()
        {
            _grid = new TileEntity[Constants.GridWidth, Constants.GridHeight];
            _roadGrid = new TileEntity[Constants.GridWidth, Constants.GridHeight];
            
            // 바둑판 모양의 2D 그리드 격자 시각화판 자동 생성
            CreateGridBackground();

            // 씬의 메인 카메라를 찾아 그리드 정중앙으로 자동 정렬 및 제어기 부착
            Camera cam = Camera.main;
            if (cam != null)
            {
                // 셀 크기와 격자 수 기준 완벽한 월드 정중앙 좌표
                float centerX = Constants.GridWidth * Constants.CellSize * 0.5f;
                float centerY = Constants.GridHeight * Constants.CellSize * 0.5f;

                // 우측 인벤토리 대시보드 UI 공간을 배려하여, 그리드가 화면 좌측 중앙에 시각적으로 예쁘게 정렬되도록 카메라를 우측으로 약간 편향시킴
                float visualOffsetX = Constants.GridWidth * Constants.CellSize * 0.12f;
                cam.transform.position = new Vector3(centerX + visualOffsetX, centerY, -10f);

                // 그리드 전체가 시야에 한 눈에 들어오도록 줌 크기(Orthographic Size) 동적 맞춤
                cam.orthographicSize = Mathf.Max(Constants.GridWidth, Constants.GridHeight) * Constants.CellSize * 0.5f + 1f;

                // 마우스 우클릭 드래그 카메라 Panning 제어 컴포넌트 자동 증설
                if (!cam.gameObject.TryGetComponent<FactoryDelivery.Utils.CameraDragPan>(out _))
                {
                    cam.gameObject.AddComponent<FactoryDelivery.Utils.CameraDragPan>();
                }
            }
            
            Debug.Log($"[GridManager] Grid initialized ({Constants.GridWidth}x{Constants.GridHeight}) with dual-layer overlay slots.");
        }

        /// <summary>
        /// 30x30 바둑판 그리드 격자 라인을 화면에 미려하게 렌더링하기 위한 바닥 타일판을 자동 스폰한다.
        /// Scale 0.95f 기법을 사용하여 이웃 타일 사이의 틈새로 아름다운 검은 격자선이 노출되도록 한다.
        /// </summary>
        private void CreateGridBackground()
        {
            GameObject container = new GameObject("[GridBackgroundContainer]");
            container.transform.parent = transform;

            // 런타임에 확실한 1x1 흰색 텍스처를 픽셀 단위 1f(1픽셀 = 1유닛) 스프라이트로 동적 생성하여 버그 원천 차단
            Texture2D whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
            Sprite gridCellSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            for (int x = 0; x < Constants.GridWidth; x++)
            {
                for (int y = 0; y < Constants.GridHeight; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    GameObject cellGo = new GameObject($"Cell_{x}_{y}");
                    cellGo.transform.position = pos.ToWorldPosition();
                    cellGo.transform.parent = container.transform;
                    
                    // 정렬과 z축 깊이를 고정하여 타일 뒤쪽에 확실히 배치
                    Vector3 localPos = cellGo.transform.localPosition;
                    localPos.z = 0.5f; 
                    cellGo.transform.localPosition = localPos;

                    var sr = cellGo.AddComponent<SpriteRenderer>();
                    sr.sprite = gridCellSprite; // 캐싱된 스프라이트 공유
                    
                    // 0.95f 크기로 설정하여 0.05f 만큼의 틈새 격자선 유도
                    sr.transform.localScale = new Vector3(Constants.CellSize * 0.95f, Constants.CellSize * 0.95f, 1f);
                    
                    // 고급스러운 다크 계열 바둑판 타일 컬러
                    sr.color = new Color(0.2f, 0.2f, 0.2f, 1.0f);
                    sr.sortingOrder = -100; // 최하단 레이어 정렬
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Placement
        // ─────────────────────────────────────────────

        /// <summary>
        /// Attempts to place a tile at the given position.
        /// If the cell already contains a tile of the <b>same type</b> that has not reached
        /// its maximum level, the existing tile is leveled up instead.
        /// </summary>
        /// <param name="pos">Grid coordinates.</param>
        /// <param name="tileData">The tile data to place.</param>
        /// <param name="entity">
        /// On success, the newly created or leveled-up <see cref="TileEntity"/>.
        /// <c>null</c> on failure.
        /// </param>
        /// <returns><c>true</c> if the operation succeeded; otherwise <c>false</c>.</returns>
        public bool TryPlaceTile(Vector2Int pos, TileDataSO tileData, out TileEntity entity, Vector2Int? roadDirection = null)
        {
            entity = null;

            if (!IsInBounds(pos) || tileData == null) return false;

            bool isRoad = tileData.Type == TileType.Road;
            TileEntity existing = isRoad ? _roadGrid[pos.x, pos.y] : _grid[pos.x, pos.y];

            // 도로의 경우 중복 설치(겹치기/레벨업)가 절대 불가합니다.
            if (isRoad && existing != null)
            {
                return false;
            }

            // Level-up and direction-override path
            if (existing != null)
            {
                if (existing.Data == tileData)
                {
                    if (existing.CanLevelUp)
                    {
                        existing.LevelUp();
                        entity = existing;

                        // 레벨 텍스트 및 스프라이트 비주얼 갱신
                        if (_tileVisuals.TryGetValue(pos, out SpriteRenderer sr))
                        {
                            sr.color = Color.white;

                            // 레벨별 스프라이트 실시간 갱신
                            Sprite levelSprite = existing.GetCurrentSprite();
                            if (levelSprite != null)
                            {
                                sr.sprite = levelSprite;
                            }

                            FitSpriteRendererToCell(sr);

                            var lvLabelTrans = sr.transform.Find("LevelLabel");
                            if (lvLabelTrans != null)
                            {
                                var lvTxt = lvLabelTrans.GetComponent<TextMeshPro>();
                                if (lvTxt != null)
                                {
                                    lvTxt.text = $"Lv {existing.Level}";
                                }
                            }
                        }

                        _onTileLeveledUpChannel?.RaiseEvent();
                        OnTileLeveledUp?.Invoke(existing);
                    }
                    else
                    {
                        entity = existing;
                    }

                    if (isRoad)
                    {
                        RebuildRoadNetwork();
                    }

                    return true;
                }

                // Occupied by a different type → fail.
                return false;
            }

            // New placement path
            entity = new TileEntity(tileData, pos);
            if (isRoad)
            {
                _roadGrid[pos.x, pos.y] = entity;
            }
            else
            {
                _grid[pos.x, pos.y] = entity;
            }

            // 비주얼 인스턴스화
            SpawnTileVisual(entity, roadDirection);

            // 도로 타일 배치 시 도로 네트워크 실시간 재구성
            if (entity.IsRoad)
            {
                RebuildRoadNetwork();
            }

            _onTilePlacedChannel?.RaiseEvent();
            OnTilePlaced?.Invoke(entity);

            // 주변 숙소들에게 영토 갱신 알림
            var houses = FindObjectsByType<FactoryDelivery.Facility.WorkerHouse>(FindObjectsSortMode.None);
            foreach (var house in houses)
            {
                if (house != null) house.RefreshAoE();
            }

            return true;
        }

        /// <summary>
        /// Removes the tile at the given position, if one exists.
        /// </summary>
        /// <param name="pos">Grid coordinates.</param>
        /// <returns><c>true</c> if a tile was removed; <c>false</c> if the cell was already empty or out of bounds.</returns>
        public bool RemoveTile(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return false;

            bool hasGridTile = _grid[pos.x, pos.y] != null;
            bool hasRoadTile = _roadGrid[pos.x, pos.y] != null;

            if (!hasGridTile && !hasRoadTile) return false;

            bool wasRoad = hasRoadTile;

            _grid[pos.x, pos.y] = null;
            _roadGrid[pos.x, pos.y] = null;

            // 월드 공간상에서 해당 위치를 점유하던 모든 타일 비주얼 스프라이트들을 정밀 탐색하여 격리 제거
            var children = GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in children)
            {
                if (sr != null && sr.gameObject != null)
                {
                    if (sr.gameObject.name.Contains($"_{pos.x}_{pos.y}"))
                    {
                        Destroy(sr.gameObject);
                    }
                }
            }

            if (_tileVisuals.TryGetValue(pos, out SpriteRenderer cachedSr))
            {
                if (cachedSr != null)
                {
                    Destroy(cachedSr.gameObject);
                }
                _tileVisuals.Remove(pos);
            }

            // 도로 철거 시 도로 네트워크 실시간 재구성
            if (wasRoad)
            {
                RebuildRoadNetwork();
            }

            _onTileRemovedChannel?.RaiseEvent();
            OnTileRemoved?.Invoke(pos);

            // 주변 숙소들에게 영토 갱신 알림
            var houses = FindObjectsByType<FactoryDelivery.Facility.WorkerHouse>(FindObjectsSortMode.None);
            foreach (var house in houses)
            {
                if (house != null) house.RefreshAoE();
            }

            return true;
        }

        // ─────────────────────────────────────────────
        //  Queries
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="TileEntity"/> at the given grid position, or <c>null</c>
        /// if the cell is empty or out of bounds.
        /// </summary>
        /// <param name="pos">Grid coordinates.</param>
        /// <returns>The tile entity, or <c>null</c>.</returns>
        public TileEntity GetTileAt(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return null;
            return _grid[pos.x, pos.y];
        }

        /// <summary>
        /// 지정한 그리드 좌표에 존재하는 도로 타일 엔티티를 반환한다.
        /// </summary>
        public TileEntity GetRoadTileAt(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return null;
            return _roadGrid[pos.x, pos.y];
        }

        /// <summary>
        /// Checks whether the given position falls within the grid boundaries.
        /// </summary>
        /// <param name="pos">Grid coordinates to check.</param>
        /// <returns><c>true</c> if <paramref name="pos"/> is inside the grid.</returns>
        public bool IsInBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < Constants.GridWidth
                && pos.y >= 0 && pos.y < Constants.GridHeight;
        }

        /// <summary>
        /// Returns <c>true</c> if the cell at <paramref name="pos"/> is within bounds
        /// and currently unoccupied.
        /// </summary>
        /// <param name="pos">Grid coordinates.</param>
        /// <returns><c>true</c> if the cell is empty.</returns>
        public bool IsEmpty(Vector2Int pos)
        {
            return IsInBounds(pos) && _grid[pos.x, pos.y] == null;
        }

        /// <summary>
        /// Determines whether a tile can be placed at the given position,
        /// either as a new placement (cell is empty) or as a level-up
        /// (same type and not at max level).
        /// </summary>
        /// <param name="pos">Grid coordinates.</param>
        /// <param name="tileData">The tile data to evaluate.</param>
        /// <returns><c>true</c> if placement or level-up is valid.</returns>
        public bool CanPlaceOrLevelUp(Vector2Int pos, TileDataSO tileData)
        {
            if (!IsInBounds(pos) || tileData == null) return false;

            bool isRoad = tileData.Type == TileType.Road;
            TileEntity existing = isRoad ? _roadGrid[pos.x, pos.y] : _grid[pos.x, pos.y];

            // 도로는 겹칠 수 없는 예외 타일이므로, 이미 도로 격자에 도로가 존재하면 절대 설치 불가
            if (isRoad && existing != null) return false;

            // 겹쳐 지을 수 있으므로, 해당 격자 레이어가 비어 있다면 즉시 건설 허용!
            if (existing == null) return true;

            // 이미 동일 격자 레이어에 동일 타입이 설치되어 있다면 레벨업 가능 여부 판정
            return existing.Data == tileData && existing.CanLevelUp;
        }

        /// <summary>
        /// Collects all tiles whose grid position lies within <paramref name="radius"/>
        /// cells of <paramref name="center"/> (Euclidean distance).
        /// Useful for WorkerHouse area-of-effect calculations.
        /// </summary>
        /// <param name="center">Center grid position.</param>
        /// <param name="radius">Search radius in cell units.</param>
        /// <returns>A list of matching tile entities (never <c>null</c>).</returns>
        public List<TileEntity> GetTilesInRadius(Vector2Int center, float radius)
        {
            var results = new List<TileEntity>();
            float radiusSqr = radius * radius;

            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(Constants.GridWidth - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(Constants.GridHeight - 1, Mathf.CeilToInt(center.y + radius));

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (_grid[x, y] == null) continue;

                    float dx = x - center.x;
                    float dy = y - center.y;
                    if (dx * dx + dy * dy <= radiusSqr)
                    {
                        results.Add(_grid[x, y]);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all tiles on the grid that match the specified <see cref="TileType"/>.
        /// </summary>
        /// <param name="type">The tile type to filter by.</param>
        /// <returns>A list of matching tile entities (never <c>null</c>).</returns>
        public List<TileEntity> GetAllTilesOfType(TileType type)
        {
            var results = new List<TileEntity>();

            for (int x = 0; x < Constants.GridWidth; x++)
            {
                for (int y = 0; y < Constants.GridHeight; y++)
                {
                    TileEntity tile = _grid[x, y];
                    if (tile != null && tile.Data != null && tile.Data.Type == type)
                    {
                        results.Add(tile);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 타일 엔티티에 해당하는 비주얼 SpriteRenderer 게임 오브젝트를 월드 공간에 스폰한다.
        /// </summary>
        private void SpawnTileVisual(TileEntity entity, Vector2Int? roadDirection = null)
        {
            if (entity == null || entity.Data == null) return;

            SpriteRenderer sr = null;
            if (_tileVisualPrefab != null)
            {
                sr = Instantiate(_tileVisualPrefab, entity.GridPosition.ToWorldPosition(), Quaternion.identity, transform);
            }
            else
            {
                // 프리팹이 없을 경우를 대비해 런타임에 즉석에서 빈 GameObject 생성 (PoC 튼튼한 예외 처리)
                GameObject go = new GameObject($"Tile_{entity.GridPosition.x}_{entity.GridPosition.y}");
                go.transform.position = entity.GridPosition.ToWorldPosition();
                go.transform.parent = transform;
                sr = go.AddComponent<SpriteRenderer>();
                // 유니티 기본 스퀘어 에셋 로드 시도
                sr.sprite = Resources.Load<Sprite>("unity_builtin_extra/Background") ?? Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), new Vector2(0.5f,0.5f));
            }

            if (sr != null)
            {
                sr.gameObject.name = $"Tile_{entity.Data.DisplayName}_{entity.GridPosition.x}_{entity.GridPosition.y}";
                // 레벨별 스프라이트가 있다면 우선 적용
                Sprite initialSprite = entity.GetCurrentSprite();
                if (initialSprite != null)
                {
                    sr.sprite = initialSprite;
                }
                // 타일에 색을 입히지 않고 스프라이트 본래 비주얼을 유지합니다.
                sr.color = GetTileVisualColor(entity);
                sr.sortingOrder = entity.IsRoad ? 20 : 10;

                FitSpriteRendererToCell(sr);

                // 도로 타일일 경우 RoadTile 컴포넌트 동적 바인딩 및 초기화
                if (entity.IsRoad)
                {
                    var road = sr.gameObject.GetComponent<FactoryDelivery.Logistics.RoadTile>();
                    if (road == null)
                    {
                        road = sr.gameObject.AddComponent<FactoryDelivery.Logistics.RoadTile>();
                    }
                    road.ConfigureSprites(entity.Data.Sprite, entity.Data.RoadCurveSprite);
                    road.GridPosition = entity.GridPosition;
                    if (roadDirection.HasValue)
                    {
                        road.Direction = roadDirection.Value;
                    }
                }
                // 자원 타일일 경우 ResourceTile 컴포넌트 동적 바인딩 및 초기화
                else if (entity.IsResource)
                {
                    var resTile = sr.gameObject.GetComponent<FactoryDelivery.Resource.ResourceTile>();
                    if (resTile == null)
                    {
                        resTile = sr.gameObject.AddComponent<FactoryDelivery.Resource.ResourceTile>();
                    }
                    resTile.ResourceData = entity.Data.AssociatedResource;
                    resTile.GridPosition = entity.GridPosition;
                    resTile.Activate();

                    // 리플렉션으로 프라이빗 레벨 세팅
                    var levelField = resTile.GetType().GetField("_level", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (levelField != null)
                    {
                        levelField.SetValue(resTile, entity.Level);
                    }
                }
                // 시설 타일일 경우 데이터 정의 유형에 따른 컴포넌트 동적 바인딩 및 초기화
                else if (entity.IsFacility && entity.Data.AssociatedFacility != null)
                {
                    var facData = entity.Data.AssociatedFacility;
                    FactoryDelivery.Facility.FacilityBase facComponent = null;

                    switch (facData.Type)
                    {
                        case FacilityType.WorkerHouse:
                            facComponent = sr.gameObject.GetComponent<FactoryDelivery.Facility.WorkerHouse>() 
                                           ?? sr.gameObject.AddComponent<FactoryDelivery.Facility.WorkerHouse>();
                            break;
                        case FacilityType.ProcessingFacility:
                            facComponent = sr.gameObject.GetComponent<FactoryDelivery.Facility.ProcessingFacility>() 
                                           ?? sr.gameObject.AddComponent<FactoryDelivery.Facility.ProcessingFacility>();
                            break;
                        case FacilityType.Warehouse:
                            facComponent = sr.gameObject.GetComponent<FactoryDelivery.Facility.Warehouse>() 
                                           ?? sr.gameObject.AddComponent<FactoryDelivery.Facility.Warehouse>();
                            break;
                    }

                    if (facComponent != null)
                    {
                        // 리플렉션으로 _facilityData 필드 세팅
                        var dataField = typeof(FactoryDelivery.Facility.FacilityBase).GetField("_facilityData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (dataField != null)
                        {
                            dataField.SetValue(facComponent, facData);
                        }

                        facComponent.GridPosition = entity.GridPosition;
                        facComponent.Level = entity.Level;
                        facComponent.Activate();
                        
                        // 새로 배치한 숙소의 경우 즉각 주변 타일 스캔 및 강제 활성화 수행!
                        if (facComponent is FactoryDelivery.Facility.WorkerHouse house)
                        {
                            house.IsActive = true;
                            house.RefreshAoE();
                        }
                    }
                }

                // 각 시설 및 자원에 따른 시각적 라벨
                if (entity != null && entity.Data != null)
                {
                    string label = entity.Data.DisplayName;

                    if (!entity.IsRoad && entity.Data.Type != TileType.Empty)
                    {
                        GameObject labelGo = new GameObject("FacilityLabel");
                        labelGo.transform.parent = sr.transform;
                        labelGo.transform.localPosition = new Vector3(0f, 0f, -0.6f); // 정중앙 위에 둥둥 띄우기 보정

                        var txt = labelGo.AddComponent<TextMeshPro>();
                        txt.alignment = TextAlignmentOptions.Center;
                        txt.fontSize = 4.5f;
                        txt.color = Color.white;
                        
                        var meshRenderer = labelGo.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            meshRenderer.sortingOrder = 60; // 타일 스프라이트보다 앞에 렌더링
                        }
                        txt.text = label;

                        // [LevelLabel] 타일 아래에 "Lv X" 흰색 텍스트 추가!
                        GameObject lvGo = new GameObject("LevelLabel");
                        lvGo.transform.parent = sr.transform;
                        // 타일의 아래쪽에 배치 (CellSize 비례)
                        lvGo.transform.localPosition = new Vector3(0f, -Constants.CellSize * 0.42f, -0.6f);

                        var lvTxt = lvGo.AddComponent<TextMeshPro>();
                        lvTxt.alignment = TextAlignmentOptions.Center;
                        lvTxt.fontSize = 3f;
                        lvTxt.color = Color.white;
                        
                        var lvMesh = lvGo.GetComponent<MeshRenderer>();
                        if (lvMesh != null)
                        {
                            lvMesh.sortingOrder = 61; // 시설 라벨보다 위에 렌더링
                        }
                        lvTxt.text = $"Lv {entity.Level}";
                    }
                }

                _tileVisuals[entity.GridPosition] = sr;
                RefreshRoadOverlayAt(entity.GridPosition);
            }
        }

        private Color GetTileVisualColor(TileEntity entity)
        {
            if (entity != null && entity.IsRoad && _grid[entity.GridPosition.x, entity.GridPosition.y] != null)
            {
                return new Color(1f, 1f, 1f, 0.22f);
            }

            return Color.white;
        }

        private void FitSpriteRendererToCell(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return;

            Vector2 spriteSize = sr.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            float scaleX = Constants.CellSize / spriteSize.x;
            float scaleY = Constants.CellSize / spriteSize.y;
            float uniformScale = Mathf.Min(scaleX, scaleY);
            sr.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }

        private void RefreshRoadOverlayAt(Vector2Int pos)
        {
            TileEntity roadEntity = GetRoadTileAt(pos);
            if (roadEntity == null) return;

            var allRoads = FindObjectsByType<FactoryDelivery.Logistics.RoadTile>(FindObjectsSortMode.None);
            foreach (var road in allRoads)
            {
                if (road == null || road.GridPosition != pos) continue;

                var roadRenderer = road.GetComponent<SpriteRenderer>();
                if (roadRenderer != null)
                {
                    roadRenderer.color = GetTileVisualColor(roadEntity);
                    roadRenderer.sortingOrder = 20;
                }

                break;
            }
        }

        /// <summary>
        /// 씬에 존재하는 모든 RoadTile의 이웃 연결망(NextRoad, PreviousRoad)을 실시간으로 다시 구축하며,
        /// 끊겨 있거나 꺾인 코너 구간을 인접 도로에 맞춰 자동으로 꺾어 연결해 주는 스마트 오토-커브 기능을 제공합니다.
        /// </summary>
        public static void RebuildRoadNetwork()
        {
            var allRoads = FindObjectsByType<FactoryDelivery.Logistics.RoadTile>(FindObjectsSortMode.None);
            GridManager gridManager = FindFirstObjectByType<GridManager>();
            
            // 모든 연결 초기화 및 씬에 존재하는 모든 도로 타일 스프라이트 색상을 고급스러운 회색으로 일제 통일!
            foreach (var road in allRoads)
            {
                if (road != null)
                {
                    road.NextRoad = null;
                    road.PreviousRoad = null;

                    var sr = road.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        TileEntity roadEntity = gridManager != null ? gridManager.GetRoadTileAt(road.GridPosition) : null;
                        sr.color = gridManager != null && roadEntity != null
                            ? gridManager.GetTileVisualColor(roadEntity)
                            : Color.white;
                        sr.sortingOrder = 20;
                    }

                    road.SetEndpointLabel(string.Empty, Color.white);
                }
            }

            // 1차: 기본 정의된 Direction 기반 연결망 구축
            EstablishBasicConnections(allRoads);
            ApplyRoadEndpointLabels(allRoads);

            // 2차: 스마트 오토-커브 연쇄 보정 비활성화 (플레이어가 의도하여 수동 배치한 방향 설정을 100% 최우선 존중하기 위해 제외합니다.)

            // 모든 도로의 연결 유효 상태에 따른 실시간 화살표 색상 갱신 트리거
            foreach (var road in allRoads)
            {
                if (road != null)
                {
                    road.UpdateDirectionVisual();
                }
            }

            Debug.Log($"[GridManager] 도로 연결망 재설정 완료 및 스마트 오토-커브 보정 적용. (총 도로 개수: {allRoads.Length}개)");
        }

        private static void ApplyRoadEndpointLabels(FactoryDelivery.Logistics.RoadTile[] allRoads)
        {
            var visited = new HashSet<FactoryDelivery.Logistics.RoadTile>();

            foreach (var road in allRoads)
            {
                if (road == null || road.PreviousRoad != null || visited.Contains(road)) continue;

                FactoryDelivery.Logistics.RoadTile current = road;
                FactoryDelivery.Logistics.RoadTile end = road;
                int guard = 0;

                while (current != null && visited.Add(current) && guard++ < 4096)
                {
                    end = current;
                    current = current.NextRoad;
                }

                road.SetEndpointLabel("출발", new Color(0.2f, 1.0f, 1.0f, 0.95f));
                if (end != road)
                {
                    end.SetEndpointLabel("도착", new Color(0.2f, 1.0f, 0.2f, 0.95f));
                }
            }
        }

        private static void EstablishBasicConnections(FactoryDelivery.Logistics.RoadTile[] allRoads)
        {
            foreach (var road in allRoads)
            {
                if (road == null) continue;
                if (road.Direction == Vector2Int.zero) continue;

                Vector2Int nextPos = road.GridPosition + road.Direction;
                foreach (var other in allRoads)
                {
                    if (other != null && other != road && other.GridPosition == nextPos)
                    {
                        if (other.PreviousRoad != null || WouldCreateRoadCycle(road, other))
                        {
                            break;
                        }

                        road.NextRoad = other;
                        other.PreviousRoad = road;
                        break;
                    }
                }
            }
        }

        private static bool WouldCreateRoadCycle(
            FactoryDelivery.Logistics.RoadTile road,
            FactoryDelivery.Logistics.RoadTile candidateNext)
        {
            FactoryDelivery.Logistics.RoadTile current = candidateNext;
            int guard = 0;

            while (current != null && guard++ < 4096)
            {
                if (current == road) return true;
                current = current.NextRoad;
            }

            return false;
        }
    }
}
