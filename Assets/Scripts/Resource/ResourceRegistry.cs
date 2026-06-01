using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FactoryDelivery.Data;

namespace FactoryDelivery.Resource
{
    /// <summary>
    /// 모든 <see cref="ResourceDataSO"/> 인스턴스를 관리하는 중앙 레지스트리.
    /// Awake 시점에 표시 이름(DisplayName)을 키로 하는 딕셔너리를 구축하여
    /// O(1) 시간 복잡도로 자원 데이터를 조회할 수 있다.
    /// </summary>
    public class ResourceRegistry : MonoBehaviour
    {
        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("자원 목록")]
        [Tooltip("게임에 존재하는 모든 자원 데이터 목록")]
        [SerializeField] private List<ResourceDataSO> _allResources = new List<ResourceDataSO>();

        // =========================================================================
        //  내부 상태
        // =========================================================================

        /// <summary>
        /// DisplayName → ResourceDataSO 매핑 딕셔너리.
        /// Awake에서 한 번 구축된다.
        /// </summary>
        private Dictionary<string, ResourceDataSO> _resourceLookup;

        // =========================================================================
        //  속성
        // =========================================================================

        /// <summary>
        /// 등록된 모든 자원 데이터의 읽기 전용 리스트를 반환한다.
        /// </summary>
        public IReadOnlyList<ResourceDataSO> AllResources => _allResources;

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void Awake()
        {
            BuildLookup();
        }

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 표시 이름으로 <see cref="ResourceDataSO"/>를 조회한다.
        /// </summary>
        /// <param name="displayName">찾으려는 자원의 표시 이름.</param>
        /// <returns>일치하는 자원 데이터. 없으면 <c>null</c>.</returns>
        public ResourceDataSO GetByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                Debug.LogWarning("[ResourceRegistry] GetByName: displayName이 null 또는 빈 문자열입니다.");
                return null;
            }

            _resourceLookup.TryGetValue(displayName, out ResourceDataSO result);
            return result;
        }

        /// <summary>
        /// 지정 카테고리에 속하는 모든 자원 데이터를 반환한다.
        /// </summary>
        /// <param name="category">필터링할 자원 카테고리.</param>
        /// <returns>해당 카테고리의 자원 목록.</returns>
        public List<ResourceDataSO> GetByCategory(ResourceCategory category)
        {
            return _allResources
                .Where(r => r != null && r.Category == category)
                .ToList();
        }

        // =========================================================================
        //  내부 도우미
        // =========================================================================

        /// <summary>
        /// _allResources 리스트로부터 DisplayName 기반 딕셔너리를 구축한다.
        /// 중복 키가 발견되면 경고를 로깅하고 첫 번째 항목을 유지한다.
        /// </summary>
        private void BuildLookup()
        {
            _resourceLookup = new Dictionary<string, ResourceDataSO>(_allResources.Count);

            foreach (ResourceDataSO resource in _allResources)
            {
                if (resource == null)
                {
                    Debug.LogWarning("[ResourceRegistry] null 자원 항목이 감지되었습니다. 건너뜁니다.");
                    continue;
                }

                if (string.IsNullOrEmpty(resource.DisplayName))
                {
                    Debug.LogWarning($"[ResourceRegistry] 자원 '{resource.name}'의 DisplayName이 비어 있습니다.");
                    continue;
                }

                if (!_resourceLookup.TryAdd(resource.DisplayName, resource))
                {
                    Debug.LogWarning(
                        $"[ResourceRegistry] 중복 DisplayName '{resource.DisplayName}' 감지. " +
                        $"'{resource.name}'을(를) 무시합니다.");
                }
            }

            Debug.Log($"[ResourceRegistry] {_resourceLookup.Count}개 자원 등록 완료.");
        }
    }
}
