using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;

namespace FactoryDelivery.Grid
{
    /// <summary>
    /// 그리드 상의 블록 배치 평가 결과를 나타냅니다.
    /// </summary>
    public enum PlacementResult
    {
        /// <summary>모든 셀을 배치할 수 있습니다 (빈 셀이거나 유효한 레벨업).</summary>
        Valid,

        /// <summary>하나 이상의 셀을 배치할 수 없습니다 (다른 타입이 점유 중이거나 최대 레벨).</summary>
        Invalid,

        /// <summary>
        /// 일부 셀은 기존 타일을 레벨업시키고 다른 셀은 빈 공간에 배치됩니다.
        /// 현재는 유효한 것으로 처리되나, 필요한 경우 UI 피드백을 위해 노출됩니다.
        /// </summary>
        PartialLevelUp,

        /// <summary>하나 이상의 셀이 그리드 범위를 벗어났습니다.</summary>
        OutOfBounds
    }

    /// <summary>
    /// 현재 그리드 상태를 기준으로 블록 배치 작업을 검증하는 정적 유틸리티 클래스입니다.
    /// 모든 메서드는 부수 효과가 없는 순수 쿼리입니다.
    /// </summary>
    public static class GridValidator
    {
        /// <summary>
        /// 블록의 모든 셀을 그리드에 배치할 수 있는지 확인합니다.
        /// 빈 셀 배치와 동일 타입 레벨업 배치를 모두 허용합니다.
        /// </summary>
        /// <param name="grid">활성 그리드 매니저.</param>
        /// <param name="cells">로컬 위치 오프셋을 가진 블록 셀 목록.</param>
        /// <param name="pivotPosition">블록 피벗의 월드 그리드 위치.</param>
        /// <returns>모든 셀을 배치하거나 레벨업할 수 있으면 <c>true</c>.</returns>
        public static bool CanPlaceBlock(GridManager grid, List<BlockCell> cells, Vector2Int pivotPosition)
        {
            PlacementResult result = EvaluateBlockPlacement(grid, cells, pivotPosition);
            return result == PlacementResult.Valid || result == PlacementResult.PartialLevelUp;
        }

        /// <summary>
        /// 블록의 모든 셀이 레벨업으로 이어지는지 확인합니다.
        /// 이를 위해서는 <b>모든</b> 대상 셀에 동일한 타입의 타일이 이미 존재해야 하며,
        /// 해당 타일들이 아직 최대 레벨에 도달하지 않았어야 합니다.
        /// </summary>
        /// <param name="grid">활성 그리드 매니저.</param>
        /// <param name="cells">로컬 위치 오프셋을 가진 블록 셀 목록.</param>
        /// <param name="pivotPosition">블록 피벗의 월드 그리드 위치.</param>
        /// <returns>모든 셀이 유효한 레벨업 후보와 매칭되면 <c>true</c>.</returns>
        public static bool CanLevelUpBlock(GridManager grid, List<BlockCell> cells, Vector2Int pivotPosition)
        {
            if (grid == null || cells == null || cells.Count == 0) return false;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int worldPos = pivotPosition + cells[i].LocalPosition;

                if (!grid.IsInBounds(worldPos)) return false;

                bool isRoad = cells[i].TileData.Type == TileType.Road;
                TileEntity existing = isRoad ? grid.GetRoadTileAt(worldPos) : grid.GetTileAt(worldPos);
                
                if (existing == null) return false; // 비어 있음 → 레벨업 아님
                if (isRoad) return false; // 도로는 절대 레벨업이나 겹치기가 불가능합니다.
                if (existing.Data != cells[i].TileData) return false; // 타입 불일치
                if (!existing.CanLevelUp) return false; // 이미 최대 레벨
            }

            return true;
        }

        /// <summary>
        /// 블록 배치를 평가하고 상세한 <see cref="PlacementResult"/>를 반환합니다.
        /// </summary>
        /// <param name="grid">활성 그리드 매니저.</param>
        /// <param name="cells">로컬 위치 오프셋을 가진 블록 셀 목록.</param>
        /// <param name="pivotPosition">블록 피벗의 월드 그리드 위치.</param>
        /// <returns>평가 결과를 설명하는 <see cref="PlacementResult"/>.</returns>
        public static PlacementResult EvaluateBlockPlacement(
            GridManager grid,
            List<BlockCell> cells,
            Vector2Int pivotPosition)
        {
            if (grid == null || cells == null || cells.Count == 0)
            {
                return PlacementResult.Invalid;
            }

            bool hasNewPlacement = false;
            bool hasLevelUp = false;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int worldPos = pivotPosition + cells[i].LocalPosition;

                if (!grid.IsInBounds(worldPos))
                {
                    return PlacementResult.OutOfBounds;
                }

                bool isRoad = cells[i].TileData.Type == TileType.Road;
                TileEntity existing = isRoad ? grid.GetRoadTileAt(worldPos) : grid.GetTileAt(worldPos);

                if (existing == null)
                {
                    // 해당 격자 레이어가 비어 있음 → 새로운 배치 가능!
                    hasNewPlacement = true;
                }
                else
                {
                    // 도로는 중복 설치 및 겹치기가 불가능합니다.
                    if (isRoad)
                    {
                        return PlacementResult.Invalid;
                    }

                    // 점유된 셀 → 동일 타입이어야 하며 최대 레벨이 아니어야 함
                    if (existing.Data != cells[i].TileData || !existing.CanLevelUp)
                    {
                        return PlacementResult.Invalid;
                    }

                    hasLevelUp = true;
                }
            }

            if (hasNewPlacement && hasLevelUp)
            {
                // 혼재된 상태
                return PlacementResult.PartialLevelUp;
            }

            return PlacementResult.Valid;
        }
    }
}
