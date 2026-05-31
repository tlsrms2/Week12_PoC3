using System;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Events;

namespace FactoryDelivery.Core
{
    /// <summary>
    /// 일일 판매 할당량 진행 상황을 추적하고 달성/미달을 판정하는 매니저.
    /// <see cref="IntEventChannelSO"/>를 통해 글로벌 자원 판매 이벤트를 구독한다.
    /// </summary>
    public class QuotaManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("일별 할당량 테이블")]
        [SerializeField] private QuotaTableSO _quotaTable;

        [Header("이벤트 채널")]
        [Tooltip("자원 판매 시 발행되는 채널 (구독용)")]
        [SerializeField] private IntEventChannelSO _onResourceSoldChannel;

        [Tooltip("할당량 최초 달성 시 발행")]
        [SerializeField] private VoidEventChannelSO _onQuotaMet;

        [Tooltip("정산 시 할당량 미달일 경우 발행")]
        [SerializeField] private VoidEventChannelSO _onQuotaFailed;

        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        private int _currentQuota;
        private int _currentProgress;
        private bool _quotaMetNotified;
        private int _walletBalance;

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>오늘의 목표 할당량.</summary>
        public int CurrentQuota => _currentQuota;

        /// <summary>오늘 판매한 총 가치.</summary>
        public int CurrentProgress => _currentProgress;

        /// <summary>할당량을 달성했는지 여부.</summary>
        public bool IsQuotaMet => _currentProgress >= _currentQuota;

        /// <summary>플레이어가 현재 보유한 소비 가능한 엽전 잔액.</summary>
        public int WalletBalance => _walletBalance;

        // ─────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────

        /// <summary>
        /// 할당량 진행 상황이 변경될 때 발생.
        /// 페이로드: (현재 진행량, 목표 할당량).
        /// </summary>
        public event Action<int, int> OnQuotaProgressChanged;

        /// <summary>
        /// 보유 엽전 잔액이 변경될 때 발생.
        /// 페이로드: (현재 보유 엽전).
        /// </summary>
        public event Action<int> OnWalletBalanceChanged;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            if (_onResourceSoldChannel != null)
            {
                _onResourceSoldChannel.OnEventRaised += OnResourceSold;
            }
        }

        private void OnDisable()
        {
            if (_onResourceSoldChannel != null)
            {
                _onResourceSoldChannel.OnEventRaised -= OnResourceSold;
            }
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 새로운 Day를 시작한다. 할당량을 계산하고 진행 상황을 초기화한다.
        /// </summary>
        /// <param name="dayNumber">시작하는 일차 (1부터).</param>
        public void StartNewDay(int dayNumber)
        {
            _currentQuota = _quotaTable != null
                ? _quotaTable.GetQuotaForDay(dayNumber)
                : 100;

            _currentProgress = 0;
            _quotaMetNotified = false;

            if (dayNumber == 1)
            {
                _walletBalance = 200; // 200 starting gold on Day 1!
            }

            Debug.Log($"[QuotaManager] Day {dayNumber} 할당량: {_currentQuota}");

            OnQuotaProgressChanged?.Invoke(_currentProgress, _currentQuota);
            OnWalletBalanceChanged?.Invoke(_walletBalance);
        }

        /// <summary>
        /// 할당량 초과분을 반환한다.
        /// </summary>
        /// <returns>할당량을 초과한 판매 가치. 미달이면 0.</returns>
        public int GetSurplus()
        {
            return Mathf.Max(0, _currentProgress - _currentQuota);
        }

        /// <summary>
        /// 할당량 달성 비율을 반환한다.
        /// </summary>
        /// <returns>0 ~ 1+ 사이의 비율.</returns>
        public float GetProgressRatio()
        {
            if (_currentQuota <= 0) return 1f;
            return (float)_currentProgress / _currentQuota;
        }

        // ─────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────

        /// <summary>
        /// 자원 판매 시 호출되는 이벤트 핸들러.
        /// 판매 가치를 진행 상황에 추가한다.
        /// </summary>
        /// <param name="value">판매된 자원의 가치.</param>
        private void OnResourceSold(int value)
        {
            _currentProgress += value;
            _walletBalance += value; // Earn gold on sale

            OnQuotaProgressChanged?.Invoke(_currentProgress, _currentQuota);
            OnWalletBalanceChanged?.Invoke(_walletBalance);

            // 할당량 최초 달성 알림
            if (IsQuotaMet && !_quotaMetNotified)
            {
                _quotaMetNotified = true;
                _onQuotaMet?.RaiseEvent();
                Debug.Log($"[QuotaManager] 할당량 달성! ({_currentProgress}/{_currentQuota})");
            }
        }

        /// <summary>
        /// 인벤토리 자원을 직접 판매하여 금액을 획득한다.
        /// </summary>
        /// <param name="resource">판매할 자원 데이터.</param>
        /// <param name="amount">판매할 수량.</param>
        public void SellResource(ResourceDataSO resource, int amount)
        {
            if (resource == null || amount <= 0) return;

            int totalValue = resource.BaseValue * amount;
            if (_onResourceSoldChannel != null)
            {
                _onResourceSoldChannel.RaiseEvent(totalValue);
            }
            else
            {
                // 채널이 혹시 없을 경우 폴백 처리
                _currentProgress += totalValue;
                _walletBalance += totalValue;
                OnQuotaProgressChanged?.Invoke(_currentProgress, _currentQuota);
                OnWalletBalanceChanged?.Invoke(_walletBalance);

                if (IsQuotaMet && !_quotaMetNotified)
                {
                    _quotaMetNotified = true;
                    _onQuotaMet?.RaiseEvent();
                    Debug.Log($"[QuotaManager] 할당량 달성! ({_currentProgress}/{_currentQuota})");
                }
            }
            Debug.Log($"[QuotaManager] {resource.DisplayName} {amount}개 판매 완료. 가치: {totalValue} 엽전");
        }

        /// <summary>
        /// 비용(엽전)을 지불한다. 성공 시 true를 반환하고 보유 엽전을 삭감한다.
        /// </summary>
        /// <param name="amount">소모할 엽전의 양.</param>
        /// <returns>지불 가능 및 성공 여부.</returns>
        public bool TrySpendProgress(int amount)
        {
            if (_walletBalance < amount) return false;
            
            _walletBalance -= amount;
            OnWalletBalanceChanged?.Invoke(_walletBalance);
            Debug.Log($"[QuotaManager] 엽전 {amount} 소모 완료. (잔액: {_walletBalance})");
            return true;
        }
    }
}
