using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FactoryDelivery.Data;

namespace FactoryDelivery.Resource
{
    /// <summary>
    /// 플레이어의 자원 보유량을 관리하는 순수 C# 클래스.
    /// MonoBehaviour가 아니므로 어디서든 인스턴스를 생성하여 사용할 수 있다.
    /// 자원의 추가, 소비, 조회 기능을 제공하며,
    /// 변경 시 <see cref="OnResourceChanged"/> 이벤트를 통해 외부에 알린다.
    /// </summary>
    public class ResourceInventory
    {
        // =========================================================================
        //  내부 상태
        // =========================================================================

        /// <summary>
        /// 자원별 보유량 딕셔너리.
        /// </summary>
        private readonly Dictionary<ResourceDataSO, int> _holdings = new Dictionary<ResourceDataSO, int>();

        // =========================================================================
        //  이벤트
        // =========================================================================

        /// <summary>
        /// 자원이 추가되거나 소비될 때 발행되는 이벤트.
        /// 매개변수: (변경된 자원 데이터, 변경 후 보유량).
        /// </summary>
        public event Action<ResourceDataSO, int> OnResourceChanged;

        // =========================================================================
        //  속성
        // =========================================================================

        /// <summary>
        /// 현재 보유량의 읽기 전용 뷰를 반환한다.
        /// </summary>
        public IReadOnlyDictionary<ResourceDataSO, int> Holdings =>
            new ReadOnlyDictionary<ResourceDataSO, int>(_holdings);

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 지정 자원을 보유량에 추가한다.
        /// </summary>
        /// <param name="resource">추가할 자원 데이터.</param>
        /// <param name="amount">추가할 수량. 기본값은 1.</param>
        /// <exception cref="ArgumentNullException"><paramref name="resource"/>가 null인 경우.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/>가 0 이하인 경우.</exception>
        public void Add(ResourceDataSO resource, int amount = 1)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "수량은 1 이상이어야 합니다.");

            if (_holdings.ContainsKey(resource))
            {
                _holdings[resource] += amount;
            }
            else
            {
                _holdings[resource] = amount;
            }

            OnResourceChanged?.Invoke(resource, _holdings[resource]);
        }

        /// <summary>
        /// 지정 자원을 보유량에서 소비한다.
        /// 보유량이 부족하면 소비하지 않고 <c>false</c>를 반환한다.
        /// </summary>
        /// <param name="resource">소비할 자원 데이터.</param>
        /// <param name="amount">소비할 수량. 기본값은 1.</param>
        /// <returns>소비 성공 시 <c>true</c>, 보유량 부족 시 <c>false</c>.</returns>
        public bool TryConsume(ResourceDataSO resource, int amount = 1)
        {
            if (resource == null || amount <= 0)
                return false;

            if (!_holdings.TryGetValue(resource, out int current) || current < amount)
                return false;

            _holdings[resource] = current - amount;

            // 보유량이 0이 되면 항목 제거
            if (_holdings[resource] <= 0)
            {
                _holdings.Remove(resource);
            }

            int remaining = _holdings.ContainsKey(resource) ? _holdings[resource] : 0;
            OnResourceChanged?.Invoke(resource, remaining);
            return true;
        }

        /// <summary>
        /// 지정 자원의 현재 보유량을 반환한다.
        /// </summary>
        /// <param name="resource">조회할 자원 데이터.</param>
        /// <returns>보유량. 해당 자원이 없으면 0.</returns>
        public int GetAmount(ResourceDataSO resource)
        {
            if (resource == null)
                return 0;

            return _holdings.TryGetValue(resource, out int amount) ? amount : 0;
        }

        /// <summary>
        /// 보유 중인 모든 자원의 총 가치를 계산한다.
        /// 각 자원의 <see cref="ResourceDataSO.BaseValue"/> × 보유량의 합산.
        /// </summary>
        /// <returns>총 가치.</returns>
        public int GetTotalValue()
        {
            return _holdings.Sum(kvp => kvp.Key.BaseValue * kvp.Value);
        }

        /// <summary>
        /// 모든 보유량을 초기화한다.
        /// </summary>
        public void Clear()
        {
            _holdings.Clear();
        }
    }
}
