using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;


namespace FactoryDelivery.Data
{
    /// <summary>
    /// 블록 패턴 내 개별 셀을 정의하는 구조체.
    /// 피벗(0,0) 기준의 상대 위치와 해당 위치에 배치될 타일 데이터를 포함한다.
    /// </summary>
    [Serializable]
    public struct BlockCell
    {
        /// <summary>피벗(0,0) 기준의 상대 위치 오프셋.</summary>
        [Tooltip("피벗(0,0) 기준의 상대 위치")]
        public Vector2Int LocalPosition;

        /// <summary>이 셀에 배치될 타일 데이터.</summary>
        [Tooltip("이 셀에 배치될 타일")]
        public TileDataSO TileData;
    }
}

namespace FactoryDelivery.Block
{
    /// <summary>
    /// 런타임에 동적으로 형태 및 구성이 결정된 블록 인스턴스.
    /// 현재 회전을 추적하고 회전된 셀 접근을 제공합니다.
    /// </summary>
    [Serializable]
    public class BlockInstance
    {
        /// <summary>블록의 임의 표시 이름.</summary>
        public string PatternName { get; set; }

        /// <summary>블록을 구성하는 무작위 셀 목록.</summary>
        public List<BlockCell> Cells { get; set; } = new List<BlockCell>();

        /// <summary>
        /// 현재 회전 인덱스 (0 = 0°, 1 = 90°, 2 = 180°, 3 = 270°).
        /// </summary>
        public int CurrentRotation { get; private set; }

        /// <summary>
        /// 기본 생성자.
        /// </summary>
        public BlockInstance()
        {
            PatternName = "Dynamic Random Block";
            CurrentRotation = 0;
        }

        /// <summary>
        /// 현재 회전을 적용한 후의 <see cref="BlockCell"/> 위치 목록을 반환합니다.
        /// </summary>
        /// <returns>회전된 셀 목록 (호출 시마다 새로 할당됨).</returns>
        public List<BlockCell> GetCurrentCells()
        {
            int normalizedRotation = ((CurrentRotation % 4) + 4) % 4;
            var rotatedCells = new List<BlockCell>(Cells.Count);

            for (int i = 0; i < Cells.Count; i++)
            {
                var cell = Cells[i];
                rotatedCells.Add(new BlockCell
                {
                    LocalPosition = RotateCell(cell.LocalPosition, normalizedRotation),
                    TileData = cell.TileData
                });
            }

            return rotatedCells;
        }

        /// <summary>
        /// 블록 전체 형태를 한 바퀴 감싸는 bounding size를 계산합니다.
        /// </summary>
        public Vector2Int GetBounds()
        {
            if (Cells == null || Cells.Count == 0) return Vector2Int.zero;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < Cells.Count; i++)
            {
                Vector2Int pos = Cells[i].LocalPosition;
                if (pos.x < minX) minX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y > maxY) maxY = pos.y;
            }

            return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// 시계 방향으로 90° 회전시키며, 0 → 1 → 2 → 3 → 0 순으로 순환합니다.
        /// </summary>
        public void Rotate()
        {
            CurrentRotation = (CurrentRotation + 1) % 4;
        }

        private Vector2Int RotateCell(Vector2Int pos, int rotation)
        {
            return rotation switch
            {
                1 => new Vector2Int(pos.y, -pos.x),   // 90° 시계 방향
                2 => new Vector2Int(-pos.x, -pos.y),   // 180°
                3 => new Vector2Int(-pos.y, pos.x),    // 270° 시계 방향
                _ => pos                                // 0° (회전 없음)
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[BlockInstance] {PatternName} (cells={Cells.Count}, rot={CurrentRotation * 90}°)";
        }
    }

    /// <summary>
    /// 동적 런타임 프로시저럴 블록 생성을 담당하는 팩토리 클래스.
    /// 카테고리(자원/시설) 별 타일 풀에서 무작위로 타일을 조합하고, 
    /// 형태와 크기를 연결성을 보증하여 동적으로 빌드합니다.
    /// </summary>
    public class BlockFactory : MonoBehaviour
    {
        [Header("자원 블록 설정")]
        [Tooltip("자원 블록을 구성할 타일 데이터 에셋 목록")]
        [SerializeField] private List<TileDataSO> _resourceTilePool = new List<TileDataSO>();

        [Header("시설 블록 설정")]
        [Tooltip("시설 블록을 구성할 타일 데이터 에셋 목록")]
        [SerializeField] private List<TileDataSO> _facilityTilePool = new List<TileDataSO>();

        [Tooltip("물류 창고 타일 에셋 (일일 공급 forceWarehouse 강제 주입 보증용)")]
        [SerializeField] private TileDataSO _warehouseTileData;

        public TileDataSO WarehouseTileData => GetWarehouseTileData();

        /// <summary>
        /// 런타임에 형태, 크기, 타일을 전격 랜덤으로 조합하여 팩토리 블록들을 일일 지급합니다.
        /// </summary>
        public List<BlockInstance> GenerateBlocksForDay(int count, bool forceWarehouse = false)
        {
            var blocks = new List<BlockInstance>(count);

            for (int i = 0; i < count; i++)
            {
                // 1. 자원 블록인지, 시설 블록인지 카테고리 결정 (50% 확률)
                bool isResourceBlock = UnityEngine.Random.value > 0.5f;
                
                // 만약 forceWarehouse가 필요한데 아직 창고를 생성하지 못했고, 마지막 루프에 도달했다면 시설 블록으로 강제 전환
                if (forceWarehouse && i == count - 1)
                {
                    bool alreadyHasWarehouse = false;
                    foreach (var b in blocks)
                    {
                        if (ContainsWarehouse(b))
                        {
                            alreadyHasWarehouse = true;
                            break;
                        }
                    }
                    if (!alreadyHasWarehouse)
                    {
                        isResourceBlock = false;
                    }
                }

                // 2. 블록의 칸 개수(크기) 결정 (1 ~ 4칸 무작위 생성)
                int cellSize = UnityEngine.Random.Range(1, 5); 

                // 3. Procedural Growth 알고리즘으로 100% 하나로 연결된 단일 도형 격자들 생성
                List<Vector2Int> localPositions = GenerateConnectedCoordinates(cellSize);

                // 4. 블록 인스턴스 조립 및 타일 데이터 매핑
                BlockInstance blockInstance = new BlockInstance();
                blockInstance.PatternName = isResourceBlock ? "무작위 자원 블록" : "무작위 시설 블록";

                bool warehousePlaced = false;

                for (int j = 0; j < localPositions.Count; j++)
                {
                    TileDataSO selectedTile = null;

                    if (isResourceBlock)
                    {
                        selectedTile = GetRandomTileFromPool(_resourceTilePool);
                    }
                    else
                    {
                        // forceWarehouse 강제 주입 분기
                        if (forceWarehouse && !warehousePlaced && j == localPositions.Count - 1)
                        {
                            // 이미 다른 블록에서 창고가 배출되었는지 한 번 더 검사
                            bool alreadyHasWarehouse = false;
                            foreach (var b in blocks)
                            {
                                if (ContainsWarehouse(b)) alreadyHasWarehouse = true;
                            }

                            if (!alreadyHasWarehouse && _warehouseTileData != null)
                            {
                                selectedTile = _warehouseTileData;
                                warehousePlaced = true;
                            }
                            else
                            {
                                selectedTile = GetRandomTileFromPool(_facilityTilePool, false);
                            }
                        }
                        else
                        {
                            selectedTile = GetRandomTileFromPool(_facilityTilePool, false);
                        }
                    }

                    if (selectedTile != null)
                    {
                        blockInstance.Cells.Add(new BlockCell
                        {
                            LocalPosition = localPositions[j],
                            TileData = selectedTile
                        });
                    }
                }

                blocks.Add(blockInstance);
            }

            // 만약 forceWarehouse 플래그는 활성화되었으나, 창고 데이터 주입이 누락되었다면 최후의 보정 적용
            if (forceWarehouse && count > 0)
            {
                bool hasWarehouse = false;
                foreach (var b in blocks)
                {
                    if (ContainsWarehouse(b)) hasWarehouse = true;
                }

                if (!hasWarehouse && _warehouseTileData != null)
                {
                    // 생성된 시설 블록 중 하나를 골라 1칸을 물류 창고로 교체하거나, 아예 1칸짜리 물류창고 카드로 대체 지급
                    int replaceIndex = UnityEngine.Random.Range(0, count);
                    BlockInstance warehouseCard = new BlockInstance();
                    warehouseCard.PatternName = "지정 물류 창고 블록";
                    warehouseCard.Cells.Add(new BlockCell
                    {
                        LocalPosition = Vector2Int.zero,
                        TileData = _warehouseTileData
                    });
                    blocks[replaceIndex] = warehouseCard;
                    Debug.Log($"[BlockFactory] [Procedural-Force] 1칸짜리 물류 창고 블록을 강제 생성하여 대체 지급했습니다. (인덱스: {replaceIndex})");
                }
            }

            return blocks;
        }

        public BlockInstance CreateWarehouseBlock(int size = 3)
        {
            TileDataSO warehouseTile = GetWarehouseTileData();
            if (warehouseTile == null)
            {
                Debug.LogWarning("[BlockFactory] 창고 타일 데이터가 없어 창고 블록을 만들 수 없습니다.");
                return null;
            }

            BlockInstance block = new BlockInstance
            {
                PatternName = $"{size}x{size} 물류창고 블록"
            };

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    block.Cells.Add(new BlockCell
                    {
                        LocalPosition = new Vector2Int(x, y),
                        TileData = warehouseTile
                    });
                }
            }

            return block;
        }

        /// <summary>
        /// 격자가 부서지지 않고 반드시 한 덩어리로 끊김없이 연결되는 테트리스 모양 좌표를 생성합니다. (Procedural Growth)
        /// </summary>
        private List<Vector2Int> GenerateConnectedCoordinates(int size)
        {
            var positions = new List<Vector2Int>();
            var generatedSet = new HashSet<Vector2Int>();

            // 시작 피벗 (0, 0)
            Vector2Int start = Vector2Int.zero;
            positions.Add(start);
            generatedSet.Add(start);

            Vector2Int[] directions = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            for (int i = 1; i < size; i++)
            {
                bool added = false;
                // 무작위 순서로 기존 격자들 중 하나를 골라 성장 탐색 시도
                List<Vector2Int> candidates = new List<Vector2Int>(positions);
                // Fisher-Yates 셔플로 후보지 다양성 확보
                for (int c = candidates.Count - 1; c > 0; c--)
                {
                    int randIdx = UnityEngine.Random.Range(0, c + 1);
                    (candidates[c], candidates[randIdx]) = (candidates[randIdx], candidates[c]);
                }

                foreach (Vector2Int currentPos in candidates)
                {
                    // 4방향 셔플
                    var shuffledDirs = new List<Vector2Int>(directions);
                    for (int d = shuffledDirs.Count - 1; d > 0; d--)
                    {
                        int randIdx = UnityEngine.Random.Range(0, d + 1);
                        (shuffledDirs[d], shuffledDirs[randIdx]) = (shuffledDirs[randIdx], shuffledDirs[d]);
                    }

                    foreach (Vector2Int dir in shuffledDirs)
                    {
                        Vector2Int neighbor = currentPos + dir;
                        if (!generatedSet.Contains(neighbor))
                        {
                            positions.Add(neighbor);
                            generatedSet.Add(neighbor);
                            added = true;
                            break;
                        }
                    }
                    if (added) break;
                }

                // 만약 사방이 완전히 고립되는 특수 상황 시 강제로 1방향 빈 공간을 뚫고 연장
                if (!added)
                {
                    Vector2Int lastPos = positions[positions.Count - 1];
                    Vector2Int fallbackNeighbor = lastPos + directions[UnityEngine.Random.Range(0, 4)];
                    positions.Add(fallbackNeighbor);
                    generatedSet.Add(fallbackNeighbor);
                }
            }

            return positions;
        }

        /// <summary>
        /// 풀에서 확률 보정치를 주기 수월하도록 가중치 추적 연산을 거치는 무작위 타일 데이터 인스턴스 추출 헬퍼 메서드.
        /// </summary>
        private TileDataSO GetRandomTileFromPool(List<TileDataSO> pool, bool allowWarehouse = true)
        {
            if (pool == null || pool.Count == 0) return null;

            if (!allowWarehouse)
            {
                List<TileDataSO> filteredPool = new List<TileDataSO>();
                foreach (TileDataSO tile in pool)
                {
                    if (tile != null && !IsWarehouseTile(tile))
                    {
                        filteredPool.Add(tile);
                    }
                }

                pool = filteredPool;
                if (pool.Count == 0) return null;
            }
            
            // 추후 가중치 밸런스를 적용하기 쉽도록 구조를 마련함.
            int index = UnityEngine.Random.Range(0, pool.Count);
            return pool[index];
        }

        private TileDataSO GetWarehouseTileData()
        {
            if (_warehouseTileData != null) return _warehouseTileData;

            foreach (TileDataSO tile in _facilityTilePool)
            {
                if (IsWarehouseTile(tile))
                {
                    return tile;
                }
            }

            return null;
        }

        private bool IsWarehouseTile(TileDataSO tile)
        {
            return tile != null
                && tile.Type == TileType.Facility
                && tile.AssociatedFacility != null
                && tile.AssociatedFacility.Type == FacilityType.Warehouse;
        }

        private bool ContainsWarehouse(BlockInstance block)
        {
            if (block == null || block.Cells == null) return false;
            foreach (var cell in block.Cells)
            {
                if (IsWarehouseTile(cell.TileData))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
