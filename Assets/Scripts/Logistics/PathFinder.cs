using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Grid;
using FactoryDelivery.Data;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Logistics
{
    /// <summary>
    /// 도로 네트워크 위에서 경로를 탐색하는 정적 유틸리티 클래스.
    /// BFS 기반으로 <see cref="RoadTile.NextRoad"/> 연결을 따라 최단 경로를 찾고,
    /// 결과를 월드 공간 웨이포인트 리스트로 변환한다.
    /// </summary>
    public static class PathFinder
    {
        /// <summary>
        /// BFS를 사용하여 <paramref name="start"/>에서 <paramref name="end"/>까지의
        /// 최단 도로 경로를 탐색한다.
        /// <see cref="RoadTile.NextRoad"/> 연결만 따르므로 단방향 흐름을 존중한다.
        /// </summary>
        /// <param name="start">출발 도로 타일.</param>
        /// <param name="end">도착 도로 타일.</param>
        /// <returns>경로에 해당하는 <see cref="RoadTile"/> 리스트. 경로가 없으면 빈 리스트.</returns>
        public static List<RoadTile> FindPath(RoadTile start, RoadTile end)
        {
            if (start == null || end == null)
                return new List<RoadTile>();

            if (start == end)
                return new List<RoadTile> { start };

            var visited = new HashSet<RoadTile>();
            var parentMap = new Dictionary<RoadTile, RoadTile>();
            var queue = new Queue<RoadTile>();

            visited.Add(start);
            queue.Enqueue(start);
            parentMap[start] = null;

            while (queue.Count > 0)
            {
                RoadTile current = queue.Dequeue();

                if (current == end)
                {
                    return ReconstructPath(parentMap, end);
                }

                // 다음 도로 연결 탐색
                if (current.NextRoad != null && !visited.Contains(current.NextRoad))
                {
                    visited.Add(current.NextRoad);
                    parentMap[current.NextRoad] = current;
                    queue.Enqueue(current.NextRoad);
                }

                // 교차로인 경우 추가 연결 탐색 (교차로 주변의 모든 도로)
                if (current.Type == RoadType.Intersection)
                {
                    RoadTile[] allRoads = Object.FindObjectsByType<RoadTile>(FindObjectsSortMode.None);
                    foreach (RoadTile road in allRoads)
                    {
                        if (road != current && !visited.Contains(road))
                        {
                            // 인접한 도로만 (1칸 거리)
                            if (Vector2Int.Distance(current.GridPosition, road.GridPosition) <= 1.1f)
                            {
                                visited.Add(road);
                                parentMap[road] = current;
                                queue.Enqueue(road);
                            }
                        }
                    }
                }
            }

            // 경로 없음
            return new List<RoadTile>();
        }

        /// <summary>
        /// 지정된 그리드 좌표에서 가장 가까운 도로 타일을 찾는다.
        /// </summary>
        /// <param name="grid">그리드 매니저 (현재 미사용, 확장을 위한 예약).</param>
        /// <param name="position">기준 그리드 좌표.</param>
        /// <returns>가장 가까운 <see cref="RoadTile"/>. 없으면 <c>null</c>.</returns>
        public static RoadTile FindNearestRoad(GridManager grid, Vector2Int position)
        {
            RoadTile[] allRoads = Object.FindObjectsByType<RoadTile>(FindObjectsSortMode.None);
            RoadTile nearest = null;
            float nearestDist = float.MaxValue;

            foreach (RoadTile road in allRoads)
            {
                float dist = Vector2Int.Distance(position, road.GridPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = road;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 도로 타일 경로를 월드 공간 웨이포인트 리스트로 변환한다.
        /// 각 도로의 중심점을 웨이포인트로 사용하여 일꾼이 부드럽게 이동할 수 있도록 한다.
        /// </summary>
        /// <param name="path">도로 타일 경로.</param>
        /// <returns>월드 좌표 웨이포인트 리스트.</returns>
        public static List<Vector3> ConvertPathToWaypoints(List<RoadTile> path)
        {
            var waypoints = new List<Vector3>(path.Count);

            for (int i = 0; i < path.Count; i++)
            {
                waypoints.Add(path[i].GetWorldPosition());
            }

            return waypoints;
        }

        /// <summary>
        /// 경로의 연속성을 검증한다.
        /// 각 도로 타일의 <see cref="RoadTile.NextRoad"/>가 다음 타일과 연결되어 있는지 확인한다.
        /// </summary>
        /// <param name="path">검증할 도로 경로.</param>
        /// <returns>유효하면 <c>true</c>.</returns>
        public static bool IsPathValid(List<RoadTile> path)
        {
            if (path == null || path.Count == 0)
                return false;

            for (int i = 0; i < path.Count - 1; i++)
            {
                if (path[i] == null || path[i].NextRoad != path[i + 1])
                    return false;
            }

            return true;
        }

        // ─────────────────────────────────────────────
        //  Internal Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// BFS 부모 맵으로부터 경로를 역추적하여 재구성한다.
        /// </summary>
        private static List<RoadTile> ReconstructPath(Dictionary<RoadTile, RoadTile> parentMap, RoadTile end)
        {
            var path = new List<RoadTile>();
            RoadTile current = end;

            while (current != null)
            {
                path.Add(current);
                current = parentMap[current];
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// 지정된 시설 위치로 들어가는(Incoming) 인접 도로 타일을 찾습니다.
        /// (인접해 있고, 방향이 시설을 가리키는 도로)
        /// </summary>
        public static RoadTile FindIncomingRoad(GridManager grid, Vector2Int facilityPos)
        {
            RoadTile[] allRoads = Object.FindObjectsByType<RoadTile>(FindObjectsSortMode.None);
            foreach (RoadTile road in allRoads)
            {
                if (road == null) continue;
                
                // 인접성 검사 (상하좌우 1칸 거리)
                if (Vector2Int.Distance(road.GridPosition, facilityPos) <= 1.1f)
                {
                    // 방향이 시설을 가리키는지 검사 (GridPosition + Direction == facilityPos)
                    if (road.GridPosition + road.Direction == facilityPos)
                    {
                        return road;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 지정된 시설 위치에서 나가는(Outgoing) 인접 도로 타일을 찾습니다.
        /// (동일한 위치에 겹쳐서 설치된 도로가 있으면 1순위 반환, 그렇지 않으면 인접해 있고 방향이 시설 반대쪽을 가리키는 도로 반환)
        /// </summary>
        public static RoadTile FindOutgoingRoad(GridManager grid, Vector2Int facilityPos)
        {
            RoadTile[] allRoads = Object.FindObjectsByType<RoadTile>(FindObjectsSortMode.None);
            
            // 1순위: 시설의 위치와 정확히 일치하는(겹쳐서 설치된) 도로가 있으면 즉시 반환
            foreach (RoadTile road in allRoads)
            {
                if (road != null && road.GridPosition == facilityPos)
                {
                    return road;
                }
            }

            // 2순위: 인접해 있고, 방향이 시설 반대쪽(즉, 시설에서 도로 방향으로 뻗음)을 가리키는 도로
            foreach (RoadTile road in allRoads)
            {
                if (road == null) continue;
                
                // 인접성 검사 (상하좌우 1칸 거리)
                if (Vector2Int.Distance(road.GridPosition, facilityPos) <= 1.1f)
                {
                    // 방향이 시설 반대쪽(즉, 시설에서 도로 방향으로 뻗음)을 가리키는지 검사 (GridPosition - Direction == facilityPos)
                    if (road.GridPosition - road.Direction == facilityPos)
                    {
                        return road;
                    }
                }
            }
            return null;
        }
    }
}
