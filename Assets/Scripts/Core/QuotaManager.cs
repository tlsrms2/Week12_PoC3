using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Events;
using FactoryDelivery.Grid;
using FactoryDelivery.Resource;

namespace FactoryDelivery.Core
{
    /// <summary>
    /// 일일 판매 할당량 진행 상황을 추적하고 달성/미달을 판정하는 매니저.
    /// <see cref="IntEventChannelSO"/>를 통해 글로벌 자원 판매 이벤트를 구독한다.
    /// </summary>
    public class QuotaManager : MonoBehaviour
    {
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

        [Header("판매 계산")]
        [SerializeField] private SaleModifierManager _saleModifierManager;
        [SerializeField] private ResourceRegistry _resourceRegistry;
        [SerializeField] private GridManager _gridManager;

        private int _currentQuota;
        private int _currentProgress;
        private bool _quotaMetNotified;
        private int _walletBalance;

        public int CurrentQuota => _currentQuota;
        public int CurrentProgress => _currentProgress;
        public bool IsQuotaMet => _currentProgress >= _currentQuota;
        public int WalletBalance => _walletBalance;

        public event Action<int, int> OnQuotaProgressChanged;
        public event Action<int> OnWalletBalanceChanged;
        public event Action<ResourceDataSO, int, int> OnResourceSoldDetailed;

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

        public void StartNewDay(int dayNumber)
        {
            _currentQuota = _quotaTable != null
                ? _quotaTable.GetQuotaForDay(dayNumber)
                : 100;

            _currentProgress = 0;
            _quotaMetNotified = false;

            if (dayNumber == 1)
            {
                _walletBalance = 200;
            }

            Debug.Log($"[QuotaManager] {dayNumber}일차 할당량: {_currentQuota}");

            OnQuotaProgressChanged?.Invoke(_currentProgress, _currentQuota);
            OnWalletBalanceChanged?.Invoke(_walletBalance);
        }

        public int GetSurplus()
        {
            return Mathf.Max(0, _currentProgress - _currentQuota);
        }

        public float GetProgressRatio()
        {
            if (_currentQuota <= 0) return 1f;
            return (float)_currentProgress / _currentQuota;
        }

        private void OnResourceSold(int value)
        {
            _currentProgress += value;
            _walletBalance += value;

            OnQuotaProgressChanged?.Invoke(_currentProgress, _currentQuota);
            OnWalletBalanceChanged?.Invoke(_walletBalance);

            if (IsQuotaMet && !_quotaMetNotified)
            {
                _quotaMetNotified = true;
                _onQuotaMet?.RaiseEvent();
                Debug.Log($"[QuotaManager] 할당량 달성! ({_currentProgress}/{_currentQuota})");
            }
        }

        public void SellResource(ResourceDataSO resource, int amount)
        {
            if (resource == null || amount <= 0)
            {
                return;
            }

            SaleResult saleResult = CalculateSale(resource, amount);
            ApplySale(resource, amount, saleResult);
        }

        public SaleResult PreviewSale(ResourceDataSO resource, int amount)
        {
            return CalculateSale(resource, amount);
        }

        public int ExecuteSettlementAutoSales()
        {
            ResolveSaleReferences();

            if (GameManager.Instance == null || GameManager.Instance.Inventory == null)
            {
                return 0;
            }

            ResourceInventory inventory = GameManager.Instance.Inventory;
            var pendingSales = new List<(ResourceDataSO resource, int amount)>();

            foreach (KeyValuePair<ResourceDataSO, int> holding in inventory.Holdings)
            {
                if (holding.Key != null && holding.Key.IsRawResource && holding.Value > 0)
                {
                    pendingSales.Add((holding.Key, holding.Value));
                }
            }

            int totalValue = 0;
            foreach ((ResourceDataSO resource, int amount) sale in pendingSales)
            {
                if (!inventory.TryConsume(sale.resource, sale.amount))
                {
                    continue;
                }

                SaleResult saleResult = CalculateSale(sale.resource, sale.amount, true);
                ApplySale(sale.resource, sale.amount, saleResult);
                totalValue += saleResult.TotalValue;
            }

            return totalValue;
        }

        private SaleResult CalculateSale(ResourceDataSO resource, int amount, bool isSettlementAutoSale = false)
        {
            ResolveSaleReferences();

            var context = new SaleContext(
                resource,
                amount,
                GameManager.Instance != null ? GameManager.Instance.Day : FindFirstObjectByType<DayManager>(),
                GameManager.Instance != null ? GameManager.Instance.Tribute : FindFirstObjectByType<TributeManager>(),
                GameManager.Instance != null ? GameManager.Instance.Inventory : null,
                _resourceRegistry,
                _gridManager,
                isSettlementAutoSale);

            if (_saleModifierManager != null)
            {
                return _saleModifierManager.CalculateSale(context);
            }

            int baseUnitValue = resource != null ? resource.BaseValue : 0;
            int safeAmount = Mathf.Max(0, amount);
            return new SaleResult(baseUnitValue, baseUnitValue, baseUnitValue * safeAmount, Array.Empty<string>());
        }

        public bool TrySpendProgress(int amount)
        {
            if (_walletBalance < amount) return false;

            _walletBalance -= amount;
            OnWalletBalanceChanged?.Invoke(_walletBalance);
            Debug.Log($"[QuotaManager] 엽전 {amount} 소모 완료. (잔액: {_walletBalance})");
            return true;
        }

        public void AddWalletBalance(int amount)
        {
            if (amount <= 0) return;

            _walletBalance += amount;
            OnWalletBalanceChanged?.Invoke(_walletBalance);
            Debug.Log($"[QuotaManager] 엽전 {amount} 환불 완료. (잔액: {_walletBalance})");
        }

        private void ApplySale(ResourceDataSO resource, int amount, SaleResult saleResult)
        {
            int totalValue = saleResult.TotalValue;

            if (_onResourceSoldChannel != null)
            {
                _onResourceSoldChannel.RaiseEvent(totalValue);
            }
            else
            {
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

            OnResourceSoldDetailed?.Invoke(resource, amount, totalValue);
            Debug.Log($"[QuotaManager] {resource.DisplayName} {amount}개 판매 완료. 가치: {totalValue} 엽전");
        }

        private void ResolveSaleReferences()
        {
            if (_saleModifierManager == null)
            {
                _saleModifierManager = GameManager.Instance != null
                    ? GameManager.Instance.SaleModifiers
                    : FindFirstObjectByType<SaleModifierManager>();
            }

            if (_resourceRegistry == null)
            {
                _resourceRegistry = FindFirstObjectByType<ResourceRegistry>();
            }

            if (_gridManager == null)
            {
                _gridManager = FindFirstObjectByType<GridManager>();
            }
        }
    }
}
