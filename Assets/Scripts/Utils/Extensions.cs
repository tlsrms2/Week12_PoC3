using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FactoryDelivery.Utils
{
    /// <summary>
    /// Extension methods for common Unity and .NET types used throughout the project.
    /// </summary>
    public static class Extensions
    {
        // ─────────────────────────────────────────────
        //  Vector2Int Extensions
        // ─────────────────────────────────────────────

        /// <summary>
        /// Converts a grid position to a world-space position using <see cref="Constants.CellSize"/>.
        /// The resulting position is centered within the grid cell.
        /// </summary>
        /// <param name="gridPos">The grid coordinates to convert.</param>
        /// <returns>A <see cref="Vector3"/> representing the world-space center of the grid cell.</returns>
        public static Vector3 ToWorldPosition(this Vector2Int gridPos)
        {
            return new Vector3(
                gridPos.x * Constants.CellSize + Constants.CellSize * 0.5f,
                gridPos.y * Constants.CellSize + Constants.CellSize * 0.5f,
                0f
            );
        }

        /// <summary>
        /// Converts a world-space position back to a grid position, accounting for the <see cref="Constants.CellSize"/> and 0.5f offset.
        /// This completely eliminates Unity's Banker's Rounding errors on odd coordinates.
        /// </summary>
        /// <param name="worldPos">The world-space position to convert.</param>
        /// <returns>A <see cref="Vector2Int"/> representing the precise grid coordinate.</returns>
        public static Vector2Int ToGridPosition(this Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.RoundToInt((worldPos.x - Constants.CellSize * 0.5f) / Constants.CellSize),
                Mathf.RoundToInt((worldPos.y - Constants.CellSize * 0.5f) / Constants.CellSize)
            );
        }

        /// <summary>
        /// Returns the four cardinal (orthogonal) neighbors of the given grid position.
        /// Does not perform bounds checking — callers should validate positions against the grid.
        /// </summary>
        /// <param name="gridPos">The grid position to get neighbors for.</param>
        /// <returns>An array of four <see cref="Vector2Int"/> positions (up, down, left, right).</returns>
        public static Vector2Int[] GetNeighbors(this Vector2Int gridPos)
        {
            return new Vector2Int[]
            {
                gridPos + Vector2Int.up,
                gridPos + Vector2Int.down,
                gridPos + Vector2Int.left,
                gridPos + Vector2Int.right
            };
        }

        // ─────────────────────────────────────────────
        //  Transform Extensions
        // ─────────────────────────────────────────────

        /// <summary>
        /// Sets the transform's world position based on a grid coordinate,
        /// converting it via <see cref="ToWorldPosition(Vector2Int)"/>.
        /// </summary>
        /// <param name="transform">The transform to reposition.</param>
        /// <param name="gridPos">The grid coordinates to convert and apply.</param>
        public static void SetPositionFromGrid(this Transform transform, Vector2Int gridPos)
        {
            transform.position = gridPos.ToWorldPosition();
        }

        // ─────────────────────────────────────────────
        //  IEnumerable<T> Extensions
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns a new list containing all elements of the source sequence in a random order,
        /// using the Fisher-Yates shuffle algorithm for uniform distribution.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequence.</typeparam>
        /// <param name="source">The source sequence to shuffle.</param>
        /// <returns>A new <see cref="List{T}"/> with elements in randomized order.</returns>
        public static List<T> Shuffle<T>(this IEnumerable<T> source)
        {
            List<T> list = source.ToList();

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }
    }
}
