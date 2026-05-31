using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Events;
using FactoryDelivery.Utils;
using FactoryDelivery.Core;
using TMPro;

namespace FactoryDelivery.Grid
{
    /// <summary>
    /// 게임 그리드의 런타임 상태를 관리합니다.
    /// 그리드는 고정된 <see cref="Constants.GridWidth"/> × <see cref="Constants.GridHeight"/>
    /// 크기의 <see cref="TileEntity"/> 슬롯 배열입니다.
    /// <para>
    /// 이 MonoBehaviour는 정적 <c>Instance</c> 접근자 대신 인스펙터를 통해 연결(직렬화 필드 싱글톤)되도록 설계되었습니다.
    /// </para>
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  직렬화 필드
        // ─────────────────────────────────────────────

        [Header("비주얼 (PoC 설정)")]
        [SerializeField]
        [Tooltip("타일을 시각적으로 나타낼 SpriteRenderer 프리팹 (지정하지 않으면 런타임에 자동 생성)")]
        private SpriteRenderer _tileVisualPrefab;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _tileVisuals = new Dictionary<Vector2Int, SpriteRenderer>();

        [Header("이벤트 채널")]

        /// <summary>그리드에 새 타일이 배치될 때마다 발생하는 SO 채널입니다.</summary>
        [SerializeField]
        [Tooltip("타일이 배치될 때 발생합니다.")]
        private VoidEventChannelSO _onTilePlacedChannel;

        /// <summary>그리드에서 타일이 제거될 때마다 발생하는 SO 채널입니다.</summary>
        [SerializeField]
        [Tooltip("타일이 제거될 때 발생합니다.")]
        private VoidEventChannelSO _onTileRemovedChannel;

        /// <summary>기존 타일의 레벨이 올라갈 때마다 발생하는 SO 채널입니다.</summary>
        [SerializeField]
        [Tooltip("타일이 레벨업될 때 발생합니다.")]
        private VoidEventChannelSO _onTileLeveledUpChannel;

        // ─────────────────────────────────────────────
        //  C# 이벤트 (직접 구독자용)
        // ─────────────────────────────────────────────

        /// <summary>타일이 성공적으로 배치된 후 발생합니다. 페이로드는 새 엔티티입니다.</summary>
        public event Action<TileEntity> OnTilePlaced;

        /// <summary>타일이 제거된 후 발생합니다. 페이로드는 제거된 위치입니다.</summary>
        public event Action<Vector2Int> OnTileRemoved;

        /// <summary>타일의 레벨이 올라간 후 발생합니다. 페이로드는 레벨업된 엔티티입니다.</summary>
        public event Action<TileEntity> OnTileLeveledUp;

        // ─────────────────────────────────────────────
        //  런타임 상태
        // ─────────────────────────────────────────────

        private readonly Dictionary<Vector2Int, TileEntity> _grid = new Dictionary<Vector2Int, TileEntity>();
        private readonly Dictionary<Vector2Int, TileEntity> _roadGrid = new Dictionary<Vector2Int, TileEntity>();
        private readonly HashSet<Vector2Int> _ownedPlots = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, GameObject> _plotButtons = new Dictionary<Vector2Int, GameObject>();
        private Transform _gridBackgroundContainer;
        private Sprite _gridCellSprite;

        /// <summary>그리드가 초기화되었는지 여부입니다.</summary>
        public bool IsInitialized => _ownedPlots.Count > 0;

        // ─────────────────────────────────────────────
        //  초기화
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (!IsInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// <see cref="Constants.GridWidth"/> × <see cref="Constants.GridHeight"/> 크기의
        /// 새로운 빈 그리드를 생성합니다. 씬 설정 중에 한 번 호출해야 합니다.
        /// </summary>
        public void Initialize()
        {
            _grid.Clear();
            _roadGrid.Clear();
            _ownedPlots.Clear();
            _plotButtons.Clear();
            
            // 바둑판 모양의 2D 그리드 격자 시각화판 자동 생성
            CreateGridBackground(Vector2Int.zero);
            RefreshPurchaseButtons();

            // 씬의 메인 카메라를 찾아 그리드 정중앙으로 자동 정렬 및 제어기 부착
            Camera cam = Camera.main;
            if (cam != null)
            {
                // 셀 크기와 격자 수 기준 완벽한 월드 정중앙 좌표
                float centerX = Constants.LandPlotSize * Constants.CellSize * 0.5f;
                float centerY = Constants.LandPlotSize * Constants.CellSize * 0.5f;

                // 우측 인벤토리 대시보드 UI 공간을 배려하여, 그리드가 화면 좌측 중앙에 시각적으로 예쁘게 정렬되도록 카메라를 우측으로 약간 편향시킴
                float visualOffsetX = Constants.LandPlotSize * Constants.CellSize * 0.12f;
                cam.transform.position = new Vector3(centerX + visualOffsetX, centerY, -10f);

                // 그리드 전체가 시야에 한 눈에 들어오도록 줌 크기(Orthographic Size) 동적 맞춤
                cam.orthographicSize = Constants.LandPlotSize * Constants.CellSize * 0.5f + 1f;

                // 마우스 우클릭 드래그 카메라 Panning 제어 컴포넌트 자동 증설
                if (!cam.gameObject.TryGetComponent<FactoryDelivery.Utils.CameraDragPan>(out _))
                {
                    cam.gameObject.AddComponent<FactoryDelivery.Utils.CameraDragPan>();
                }
            }
            
            Debug.Log($"[GridManager] 그리드 초기화 완료 ({Constants.GridWidth}x{Constants.GridHeight}). 듀얼 레이어 슬롯이 준비되었습니다.");
        }

        /// <summary>
        /// 30x30 바둑판 그리드 격자 라인을 화면에 미려하게 렌더링하기 위한 바닥 타일판을 자동 스폰한다.
        /// Scale 0.95f 기법을 사용하여 이웃 타일 사이의 틈새로 아름다운 검은 격자선이 노출되도록 한다.
        /// </summary>
        private void CreateGridBackground(Vector2Int plotOrigin)
        {
            if (_gridBackgroundContainer == null)
            {
                GameObject container = new GameObject("[GridBackgroundContainer]");
                container.transform.parent = transform;
                _gridBackgroundContainer = container.transform;
            }

            // 런타임에 확실한 1x1 흰색 텍스처를 픽셀 단위 1f(1픽셀 = 1유닛) 스프라이트로 동적 생성하여 버그 원천 차단
            if (_gridCellSprite == null)
            {
                Texture2D whiteTex = new Texture2D(1, 1);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
                _gridCellSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            Sprite gridCellSprite = _gridCellSprite;

            _ownedPlots.Add(plotOrigin);

            for (int x = 0; x < Constants.LandPlotSize; x++)
            {
                for (int y = 0; y < Constants.LandPlotSize; y++)
                {
                    Vector2Int pos = plotOrigin + new Vector2Int(x, y);
                    GameObject cellGo = new GameObject($"Cell_{pos.x}_{pos.y}");
                    cellGo.transform.position = pos.ToWorldPosition();
                    cellGo.transform.parent = _gridBackgroundContainer;
                    
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
        //  배치
        // ─────────────────────────────────────────────

        /// <summary>
        /// 지정된 위치에 타일 배치를 시도합니다.
        /// 해당 셀에 이미 최대 레벨에 도달하지 않은 <b>동일한 타입</b>의 타일이 있는 경우, 
        /// 대신 기존 타일의 레벨을 올립니다.
        /// </summary>
        /// <param name="pos">그리드 좌표입니다.</param>
        /// <param name="tileData">배치할 타일 데이터입니다.</param>
        /// <param name="entity">
        /// 성공 시 새로 생성되거나 레벨업된 <see cref="TileEntity"/>입니다. 실패 시 <c>null</c>입니다.
        /// </param>
        /// <returns>작업에 성공하면 <c>true</c>, 그렇지 않으면 <c>false</c>를 반환합니다.</returns>
        public bool TryPlaceTile(Vector2Int pos, TileDataSO tileData, out TileEntity entity, Vector2Int? roadDirection = null)
        {
            entity = null;

            if (!IsInBounds(pos) || tileData == null) return false;

            bool isRoad = tileData.Type == TileType.Road;
            TileEntity existing = isRoad ? GetRoadEntity(pos) : GetGridEntity(pos);

            // 도로의 경우 중복 설치(겹치기/레벨업)가 절대 불가합니다.
            if (isRoad && existing != null)
            {
                return false;
            }

            // 레벨업 및 방향 재설정 경로
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

                // 다른 타입에 의해 점유됨 → 실패
                return false;
            }

            // 새 배치 경로
            entity = new TileEntity(tileData, pos);
            if (isRoad)
            {
                _roadGrid[pos] = entity;
            }
            else
            {
                _grid[pos] = entity;
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
        /// 지정된 위치에 타일이 있으면 제거합니다.
        /// </summary>
        /// <param name="pos">그리드 좌표입니다.</param>
        /// <returns>타일이 제거되면 <c>true</c>, 셀이 이미 비어 있거나 범위를 벗어난 경우 <c>false</c>를 반환합니다.</returns>
        public bool RemoveTile(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return false;

            bool hasGridTile = GetGridEntity(pos) != null;
            bool hasRoadTile = GetRoadEntity(pos) != null;

            if (!hasGridTile && !hasRoadTile) return false;

            bool wasRoad = hasRoadTile;

            _grid.Remove(pos);
            _roadGrid.Remove(pos);

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
        //  쿼리
        // ─────────────────────────────────────────────

        /// <summary>
        /// 지정한 그리드 좌표에 존재하는 도로 타일을 제거한다.
        /// </summary>
        public bool RemoveRoadTile(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return false;
            if (GetRoadEntity(pos) == null) return false;

            _roadGrid.Remove(pos);

            var children = GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in children)
            {
                if (sr == null || sr.gameObject == null) continue;
                if (!sr.gameObject.name.Contains($"_{pos.x}_{pos.y}")) continue;

                var road = sr.GetComponent<FactoryDelivery.Logistics.RoadTile>();
                if (road != null && road.GridPosition == pos)
                {
                    sr.gameObject.SetActive(false);
                    Destroy(sr.gameObject);
                }
            }

            if (_tileVisuals.TryGetValue(pos, out SpriteRenderer cachedSr))
            {
                if (cachedSr != null && cachedSr.GetComponent<FactoryDelivery.Logistics.RoadTile>() != null)
                {
                    cachedSr.gameObject.SetActive(false);
                    Destroy(cachedSr.gameObject);
                    _tileVisuals.Remove(pos);
                }
            }

            RebuildRoadNetwork();
            _onTileRemovedChannel?.RaiseEvent();
            OnTileRemoved?.Invoke(pos);

            var houses = FindObjectsByType<FactoryDelivery.Facility.WorkerHouse>(FindObjectsSortMode.None);
            foreach (var house in houses)
            {
                if (house != null) house.RefreshAoE();
            }

            return true;
        }

        /// <summary>
        /// 지정한 그리드 위치에 있는 <see cref="TileEntity"/>를 반환하거나, 
        /// 셀이 비어 있거나 범위를 벗어난 경우 <c>null</c>을 반환합니다.
        /// </summary>
        /// <param name="pos">그리드 좌표입니다.</param>
        /// <returns>타일 엔티티 또는 <c>null</c>입니다.</returns>
        public TileEntity GetTileAt(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return null;
            return GetGridEntity(pos);
        }

        /// <summary>
        /// 지정한 그리드 좌표에 존재하는 도로 타일 엔티티를 반환한다.
        /// </summary>
        public TileEntity GetRoadTileAt(Vector2Int pos)
        {
            if (!IsInBounds(pos)) return null;
            return GetRoadEntity(pos);
        }

        /// <summary>
        /// 지정한 위치가 그리드 경계 내에 있는지 확인합니다.
        /// </summary>
        /// <param name="pos">확인할 그리드 좌표입니다.</param>
        /// <returns><paramref name="pos"/>가 그리드 내부에 있으면 <c>true</c>입니다.</returns>
        public bool IsInBounds(Vector2Int pos)
        {
            return IsPlotOwned(GetPlotOrigin(pos));
        }

        public bool TryPurchasePlot(Vector2Int plotOrigin)
        {
            if (IsPlotOwned(plotOrigin)) return false;

            QuotaManager quotaManager = FindFirstObjectByType<QuotaManager>();
            if (quotaManager != null && !quotaManager.TrySpendProgress(Constants.LandPurchaseCost))
            {
                return false;
            }

            CreateGridBackground(plotOrigin);

            if (_plotButtons.TryGetValue(plotOrigin, out GameObject buttonGo) && buttonGo != null)
            {
                Destroy(buttonGo);
            }
            _plotButtons.Remove(plotOrigin);

            RefreshPurchaseButtons();
            return true;
        }

        private TileEntity GetGridEntity(Vector2Int pos)
        {
            _grid.TryGetValue(pos, out TileEntity entity);
            return entity;
        }

        private TileEntity GetRoadEntity(Vector2Int pos)
        {
            _roadGrid.TryGetValue(pos, out TileEntity entity);
            return entity;
        }

        private Vector2Int GetPlotOrigin(Vector2Int pos)
        {
            int size = Constants.LandPlotSize;
            return new Vector2Int(Mathf.FloorToInt((float)pos.x / size) * size, Mathf.FloorToInt((float)pos.y / size) * size);
        }

        private bool IsPlotOwned(Vector2Int plotOrigin)
        {
            return _ownedPlots.Contains(plotOrigin);
        }

        private void RefreshPurchaseButtons()
        {
            Vector2Int[] directions =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 1),
                new Vector2Int(1, -1),
                new Vector2Int(-1, 1),
                new Vector2Int(-1, -1)
            };

            var candidates = new HashSet<Vector2Int>();
            int size = Constants.LandPlotSize;
            foreach (Vector2Int owned in _ownedPlots)
            {
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int candidate = owned + direction * size;
                    if (!IsPlotOwned(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            foreach (Vector2Int origin in candidates)
            {
                if (!_plotButtons.ContainsKey(origin))
                {
                    _plotButtons[origin] = CreatePurchaseButton(origin);
                }
            }
        }

        private GameObject CreatePurchaseButton(Vector2Int plotOrigin)
        {
            GameObject buttonGo = new GameObject($"LandPurchaseButton_{plotOrigin.x}_{plotOrigin.y}");
            buttonGo.transform.parent = transform;
            buttonGo.transform.position = (plotOrigin + new Vector2Int(Constants.LandPlotSize / 2, Constants.LandPlotSize / 2)).ToWorldPosition();

            var background = buttonGo.AddComponent<SpriteRenderer>();
            background.sprite = _gridCellSprite;
            background.color = new Color(0.15f, 0.12f, 0.08f, 0.92f);
            background.sortingOrder = 80;
            background.transform.localScale = new Vector3(3.8f, 1.6f, 1f);

            var collider = buttonGo.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3.8f, 1.6f);

            var purchaseButton = buttonGo.AddComponent<LandPurchaseButton>();
            purchaseButton.Initialize(this, plotOrigin);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.parent = buttonGo.transform;
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.2f);

            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 2.6f;
            label.color = new Color(1f, 0.86f, 0.25f, 1f);
            label.text = $"부지 구매\n{Constants.LandPurchaseCost} 엽전";

            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 81;
            }

            return buttonGo;
        }

        /// <summary>
        /// <paramref name="pos"/> 위치의 셀이 그리드 범위 내에 있고 현재 비어 있는 경우 <c>true</c>를 반환합니다.
        /// </summary>
        /// <param name="pos">그리드 좌표입니다.</param>
        /// <returns>셀이 비어 있으면 <c>true</c>입니다.</returns>
        public bool IsEmpty(Vector2Int pos)
        {
            return IsInBounds(pos) && GetGridEntity(pos) == null;
        }

        /// <summary>
        /// 해당 위치에 새 타일을 배치(셀이 비어 있음)하거나 
        /// 레벨업(동일한 타입이고 최대 레벨이 아님)할 수 있는지 확인합니다.
        /// </summary>
        /// <param name="pos">그리드 좌표입니다.</param>
        /// <param name="tileData">평가할 타일 데이터입니다.</param>
        /// <returns>배치 또는 레벨업이 유효하면 <c>true</c>입니다.</returns>
        public bool CanPlaceOrLevelUp(Vector2Int pos, TileDataSO tileData)
        {
            if (!IsInBounds(pos) || tileData == null) return false;

            bool isRoad = tileData.Type == TileType.Road;
            TileEntity existing = isRoad ? GetRoadEntity(pos) : GetGridEntity(pos);

            // 도로는 겹칠 수 없는 예외 타일이므로, 이미 도로 격자에 도로가 존재하면 절대 설치 불가
            if (isRoad && existing != null) return false;

            // 겹쳐 지을 수 있으므로, 해당 격자 레이어가 비어 있다면 즉시 건설 허용!
            if (existing == null) return true;

            // 이미 동일 격자 레이어에 동일 타입이 설치되어 있다면 레벨업 가능 여부 판정
            return existing.Data == tileData && existing.CanLevelUp;
        }

        /// <summary>
        /// <paramref name="center"/>에서 <paramref name="radius"/> 셀 이내(유클리드 거리)에 있는 모든 타일을 수집합니다. 
        /// WorkerHouse의 효과 범위 계산에 유용합니다.
        /// </summary>
        /// <param name="center">그리드 중심 위치입니다.</param>
        /// <param name="radius">셀 단위의 검색 반경입니다.</param>
        /// <returns>일치하는 타일 엔티티 목록입니다(절대 <c>null</c>이 아님).</returns>
        public List<TileEntity> GetTilesInRadius(Vector2Int center, float radius)
        {
            var results = new List<TileEntity>();
            float radiusSqr = radius * radius;

            int minX = Mathf.FloorToInt(center.x - radius);
            int maxX = Mathf.CeilToInt(center.x + radius);
            int minY = Mathf.FloorToInt(center.y - radius);
            int maxY = Mathf.CeilToInt(center.y + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    TileEntity tile = GetGridEntity(pos);
                    if (tile == null) continue;

                    float dx = x - center.x;
                    float dy = y - center.y;
                    if (dx * dx + dy * dy <= radiusSqr)
                    {
                        results.Add(tile);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 그리드에서 지정된 <see cref="TileType"/>과 일치하는 모든 타일을 반환합니다.
        /// </summary>
        /// <param name="type">필터링할 타일 타입입니다.</param>
        /// <returns>일치하는 타일 엔티티 목록입니다(절대 <c>null</c>이 아님).</returns>
        public List<TileEntity> GetAllTilesOfType(TileType type)
        {
            var results = new List<TileEntity>();

            foreach (TileEntity tile in _grid.Values)
            {
                if (tile != null && tile.Data != null && tile.Data.Type == type)
                {
                    results.Add(tile);
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
            if (entity != null && entity.IsRoad && GetGridEntity(entity.GridPosition) != null)
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
            ApplyRoadEndpointLabelsForSegments(allRoads);

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
                if (road == null || !road.gameObject.activeInHierarchy || road.PreviousRoad != null || visited.Contains(road)) continue;

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

        private static void ApplyRoadEndpointLabelsForSegments(FactoryDelivery.Logistics.RoadTile[] allRoads)
        {
            var visited = new HashSet<FactoryDelivery.Logistics.RoadTile>();

            foreach (var road in allRoads)
            {
                if (road == null || !road.gameObject.activeInHierarchy || road.PreviousRoad != null || visited.Contains(road))
                {
                    continue;
                }

                FactoryDelivery.Logistics.RoadTile current = road;
                FactoryDelivery.Logistics.RoadTile end = road;
                int guard = 0;

                while (current != null && current.gameObject.activeInHierarchy && visited.Add(current) && guard++ < 4096)
                {
                    end = current;
                    current = current.NextRoad;
                }

                if (end == road)
                {
                    road.SetEndpointLabel("출발/도착", new Color(0.2f, 1.0f, 0.7f, 0.95f));
                }
                else
                {
                    road.SetEndpointLabel("출발", new Color(0.2f, 1.0f, 1.0f, 0.95f));
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

    public class LandPurchaseButton : MonoBehaviour
    {
        private GridManager _gridManager;
        private Vector2Int _plotOrigin;

        public void Initialize(GridManager gridManager, Vector2Int plotOrigin)
        {
            _gridManager = gridManager;
            _plotOrigin = plotOrigin;
        }

        private void OnMouseDown()
        {
            if (_gridManager == null) return;
            _gridManager.TryPurchasePlot(_plotOrigin);
        }
    }
}
