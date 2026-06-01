using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FactoryDelivery.Utils
{
    /// <summary>
    /// 프로젝트 전반에서 사용되는 공통 유니티 및 .NET 타입들에 대한 확장 메서드입니다.
    /// </summary>
    public static class Extensions
    {
        // =========================================================================
        //  Vector2Int 확장
        // =========================================================================

        /// <summary>
        /// <see cref="Constants.CellSize"/>를 사용하여 그리드 위치를 월드 스페이스 위치로 변환합니다.
        /// 결과 위치는 그리드 셀의 중앙에 위치합니다.
        /// </summary>
        /// <param name="gridPos">변환할 그리드 좌표.</param>
        /// <returns>그리드 셀의 월드 스페이스 중앙을 나타내는 <see cref="Vector3"/>.</returns>
        public static Vector3 ToWorldPosition(this Vector2Int gridPos)
        {
            return new Vector3(
                gridPos.x * Constants.CellSize + Constants.CellSize * 0.5f,
                gridPos.y * Constants.CellSize + Constants.CellSize * 0.5f,
                0f
            );
        }

        /// <summary>
        /// <see cref="Constants.CellSize"/>와 0.5f 오프셋을 고려하여 월드 스페이스 위치를 그리드 위치로 다시 변환합니다.
        /// 이는 홀수 좌표에서 유니티의 Banker's Rounding 오류를 완전히 제거합니다.
        /// </summary>
        /// <param name="worldPos">변환할 월드 스페이스 위치.</param>
        /// <returns>정확한 그리드 좌표를 나타내는 <see cref="Vector2Int"/>.</returns>
        public static Vector2Int ToGridPosition(this Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.RoundToInt((worldPos.x - Constants.CellSize * 0.5f) / Constants.CellSize),
                Mathf.RoundToInt((worldPos.y - Constants.CellSize * 0.5f) / Constants.CellSize)
            );
        }

        /// <summary>
        /// 지정된 그리드 위치의 4개 기본(직교) 이웃 위치를 반환합니다.
        /// 범위 검사는 수행하지 않으며, 호출자가 그리드에 대해 위치를 검증해야 합니다.
        /// </summary>
        /// <param name="gridPos">이웃을 구할 그리드 위치.</param>
        /// <returns>4개의 <see cref="Vector2Int"/> 위치(상, 하, 좌, 우) 배열.</returns>
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

        // =========================================================================
        //  Transform 확장
        // =========================================================================

        /// <summary>
        /// 그리드 좌표를 기반으로 트랜스폼의 월드 위치를 설정합니다.
        /// <see cref="ToWorldPosition(Vector2Int)"/>를 통해 변환됩니다.
        /// </summary>
        /// <param name="transform">위치를 재설정할 트랜스폼.</param>
        /// <param name="gridPos">변환하여 적용할 그리드 좌표.</param>
        public static void SetPositionFromGrid(this Transform transform, Vector2Int gridPos)
        {
            transform.position = gridPos.ToWorldPosition();
        }

        // =========================================================================
        //  IEnumerable<T> 확장
        // =========================================================================

        /// <summary>
        /// 균등 분포를 위해 Fisher-Yates 셔플 알고리즘을 사용하여 소스 시퀀스의 모든 요소를 무작위 순서로 포함하는 새 리스트를 반환합니다.
        /// </summary>
        /// <typeparam name="T">시퀀스의 요소 타입.</typeparam>
        /// <param name="source">무작위로 섞을 소스 시퀀스.</param>
        /// <returns>무작위 순서로 요소가 포함된 새 <see cref="List{T}"/>.</returns>
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
