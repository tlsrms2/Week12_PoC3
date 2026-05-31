using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;

namespace FactoryDelivery.Grid
{
    /// <summary>
    /// Describes the outcome of evaluating a block placement on the grid.
    /// </summary>
    public enum PlacementResult
    {
        /// <summary>All cells can be placed (either on empty cells or as valid level-ups).</summary>
        Valid,

        /// <summary>One or more cells cannot be placed (occupied by a different type or at max level).</summary>
        Invalid,

        /// <summary>
        /// Some cells would level up existing tiles while others are empty placements.
        /// Currently treated as valid; exposed for UI feedback if desired.
        /// </summary>
        PartialLevelUp,

        /// <summary>One or more cells fall outside the grid boundaries.</summary>
        OutOfBounds
    }

    /// <summary>
    /// Static utility class that validates block placement operations against the
    /// current grid state. All methods are pure queries with no side effects.
    /// </summary>
    public static class GridValidator
    {
        /// <summary>
        /// Checks whether every cell of a block can be placed on the grid,
        /// allowing both empty-cell placement and same-type level-ups.
        /// </summary>
        /// <param name="grid">The active grid manager.</param>
        /// <param name="cells">The block cells with local-position offsets.</param>
        /// <param name="pivotPosition">The world grid position of the block's pivot.</param>
        /// <returns><c>true</c> if all cells can be placed or leveled up.</returns>
        public static bool CanPlaceBlock(GridManager grid, List<BlockCell> cells, Vector2Int pivotPosition)
        {
            PlacementResult result = EvaluateBlockPlacement(grid, cells, pivotPosition);
            return result == PlacementResult.Valid || result == PlacementResult.PartialLevelUp;
        }

        /// <summary>
        /// Checks whether every cell of a block would result in a level-up.
        /// This requires that <b>all</b> target cells already contain a tile of the
        /// same type that has not yet reached its maximum level.
        /// </summary>
        /// <param name="grid">The active grid manager.</param>
        /// <param name="cells">The block cells with local-position offsets.</param>
        /// <param name="pivotPosition">The world grid position of the block's pivot.</param>
        /// <returns><c>true</c> if every cell maps to a valid level-up candidate.</returns>
        public static bool CanLevelUpBlock(GridManager grid, List<BlockCell> cells, Vector2Int pivotPosition)
        {
            if (grid == null || cells == null || cells.Count == 0) return false;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int worldPos = pivotPosition + cells[i].LocalPosition;

                if (!grid.IsInBounds(worldPos)) return false;

                bool isRoad = cells[i].TileData.Type == TileType.Road;
                TileEntity existing = isRoad ? grid.GetRoadTileAt(worldPos) : grid.GetTileAt(worldPos);
                
                if (existing == null) return false; // empty → not a level-up
                if (isRoad) return false; // 도로는 절대 레벨업이나 겹치기가 불가능합니다.
                if (existing.Data != cells[i].TileData) return false; // type mismatch
                if (!existing.CanLevelUp) return false; // already at max
            }

            return true;
        }

        /// <summary>
        /// Evaluates a block placement and returns a detailed <see cref="PlacementResult"/>.
        /// </summary>
        /// <param name="grid">The active grid manager.</param>
        /// <param name="cells">The block cells with local-position offsets.</param>
        /// <param name="pivotPosition">The world grid position of the block's pivot.</param>
        /// <returns>A <see cref="PlacementResult"/> describing the evaluation outcome.</returns>
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
                    // 해당 격자 레이어가 비어 있음 → 새로운 겹치기 혹은 신설 가능!
                    hasNewPlacement = true;
                }
                else
                {
                    // 도로는 중복 설치 및 겹치기가 불가능합니다.
                    if (isRoad)
                    {
                        return PlacementResult.Invalid;
                    }

                    // Occupied cell → must be same type and not at max level
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
