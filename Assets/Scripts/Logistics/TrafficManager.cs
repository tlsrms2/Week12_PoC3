using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Events;

namespace FactoryDelivery.Logistics
{
    /// <summary>
    /// 도로 혼잡도를 모니터링하고 병목 지점을 감지하는 매니저.
    /// 도로 타일별 점유 일꾼 수를 추적하여 시각적 피드백 및 경고를 제공한다.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("혼잡도 설정")]
        [Tooltip("심각한 혼잡으로 판단하는 일꾼 수 임계값")]
        [SerializeField] private int _severeThreshold = 5;

        [Tooltip("혼잡도 업데이트 주기 (초)")]
        [SerializeField] private float _updateInterval = 1f;

        [Header("이벤트")]
        [Tooltip("심각한 혼잡 발생 시 발행되는 이벤트 채널")]
        [SerializeField] private VoidEventChannelSO _onTrafficAlert;

        [Header("비주얼 피드백")]
        [Tooltip("혼잡도에 따른 도로 색상 변경 활성화")]
        [SerializeField] private bool _enableVisualFeedback = false;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private readonly Dictionary<RoadTile, int> _congestionMap
            = new Dictionary<RoadTile, int>();

        private float _timer;
        private bool ShouldUpdateRoadVisuals => false;

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = _updateInterval;
                UpdateCongestion();
            }
        }

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 모든 도로 타일의 혼잡도를 갱신한다.
        /// </summary>
        public void UpdateCongestion()
        {
            _congestionMap.Clear();

            RoadTile[] allRoads = FindObjectsByType<RoadTile>(FindObjectsSortMode.None);
            bool hasSevereCongestion = false;

            foreach (RoadTile road in allRoads)
            {
                int occupantCount = road.OccupantCount;
                _congestionMap[road] = occupantCount;

                if (occupantCount >= _severeThreshold)
                {
                    hasSevereCongestion = true;
                }

                // 비주얼 피드백
                if (_enableVisualFeedback && ShouldUpdateRoadVisuals)
                {
                    UpdateRoadVisual(road, occupantCount);
                }
            }

            if (hasSevereCongestion)
            {
                _onTrafficAlert?.RaiseEvent();
            }
        }

        /// <summary>
        /// 지정된 도로의 혼잡 수준을 0~1 비율로 반환한다.
        /// </summary>
        /// <param name="road">조회할 도로 타일.</param>
        /// <returns>0 (한산) ~ 1 (심각한 혼잡) 사이의 값.</returns>
        public float GetCongestionLevel(RoadTile road)
        {
            if (road == null || !_congestionMap.ContainsKey(road))
                return 0f;

            return Mathf.Clamp01((float)_congestionMap[road] / _severeThreshold);
        }

        /// <summary>
        /// 혼잡도가 <paramref name="threshold"/>를 초과하는 도로 목록을 반환한다.
        /// </summary>
        /// <param name="threshold">혼잡 임계 비율 (0~1).</param>
        /// <returns>병목 도로 리스트.</returns>
        public List<RoadTile> GetBottlenecks(float threshold = 0.7f)
        {
            var bottlenecks = new List<RoadTile>();

            foreach (var kvp in _congestionMap)
            {
                float level = Mathf.Clamp01((float)kvp.Value / _severeThreshold);
                if (level >= threshold)
                {
                    bottlenecks.Add(kvp.Key);
                }
            }

            return bottlenecks;
        }

        // =========================================================================
        //  내부 도우미
        // =========================================================================

        /// <summary>
        /// 혼잡도에 따라 도로 타일의 색상을 변경한다.
        /// 초록(한산) → 노랑(보통) → 빨강(혼잡)
        /// </summary>
        private void UpdateRoadVisual(RoadTile road, int occupantCount)
        {
            SpriteRenderer sr = road.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            float ratio = Mathf.Clamp01((float)occupantCount / _severeThreshold);

            if (ratio < 0.3f)
            {
                sr.color = Color.Lerp(new Color(0.45f, 0.45f, 0.45f, 1f), Color.yellow, ratio / 0.3f);
            }
            else if (ratio < 0.7f)
            {
                sr.color = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (ratio - 0.3f) / 0.4f);
            }
            else
            {
                sr.color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (ratio - 0.7f) / 0.3f);
            }
        }
    }
}
