using System;
using System.Collections.Generic;
using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Resource;

namespace FactoryDelivery.Core
{
    public class TributeManager : MonoBehaviour
    {
        [SerializeField] private QuotaManager _quotaManager;
        [SerializeField] private ResourceRegistry _resourceRegistry;

        private readonly List<ResourceDataSO> _fallbackResources = new List<ResourceDataSO>();
        private ResourceDataSO _currentTributeResource;
        private int _requiredAmount;
        private int _deliveredAmount;
        private int _favorBalance;
        private int _todayFavorEarned;
        private readonly List<string> _todayFavorReasons = new List<string>();

        public ResourceDataSO CurrentTributeResource => _currentTributeResource;
        public int RequiredAmount => _requiredAmount;
        public int DeliveredAmount => _deliveredAmount;
        public int FavorBalance => _favorBalance;
        public int TodayFavorEarned => _todayFavorEarned;
        public IReadOnlyList<string> TodayFavorReasons => _todayFavorReasons;

        public event Action OnTributeChanged;
        public event Action<int> OnFavorChanged;

        private void OnEnable()
        {
            ResolveReferences();

            if (_quotaManager != null)
            {
                _quotaManager.OnResourceSoldDetailed += OnResourceSold;
            }
        }

        private void OnDisable()
        {
            if (_quotaManager != null)
            {
                _quotaManager.OnResourceSoldDetailed -= OnResourceSold;
            }
        }

        public void StartNewDay(int dayNumber)
        {
            ResolveReferences();

            if (GameManager.Instance != null && GameManager.Instance.DisableRoguelikeSystems)
            {
                _currentTributeResource = null;
                _requiredAmount = 0;
                _deliveredAmount = 0;
                _todayFavorEarned = 0;
                _todayFavorReasons.Clear();
                _favorBalance = 0;
                OnTributeChanged?.Invoke();
                Debug.Log("[TributeManager] 로그라이크 비활성화 모드이므로 오늘의 진상품을 설정하지 않습니다.");
                return;
            }

            List<ResourceDataSO> candidates = GetTributeCandidates();
            if (candidates.Count == 0)
            {
                _currentTributeResource = null;
                _requiredAmount = 0;
                _deliveredAmount = 0;
                OnTributeChanged?.Invoke();
                Debug.LogWarning("[TributeManager] 진상품 후보 자원이 없어 오늘의 진상품을 설정하지 못했습니다.");
                return;
            }

            int index = UnityEngine.Random.Range(0, candidates.Count);
            _currentTributeResource = candidates[index];
            _requiredAmount = Mathf.Clamp(8 + dayNumber * 2, 10, 40);
            _deliveredAmount = 0;
            _todayFavorEarned = 0;
            _todayFavorReasons.Clear();

            OnTributeChanged?.Invoke();
            Debug.Log($"[TributeManager] 오늘의 진상품: {_currentTributeResource.DisplayName} {_requiredAmount}개");
        }

        public void AddFavor(int amount)
        {
            if (amount <= 0) return;
            if (GameManager.Instance != null && GameManager.Instance.DisableRoguelikeSystems) return;

            _favorBalance += amount;
            OnFavorChanged?.Invoke(_favorBalance);
        }

        public bool TrySpendFavor(int amount)
        {
            if (amount <= 0 || _favorBalance < amount)
            {
                return false;
            }

            _favorBalance -= amount;
            OnFavorChanged?.Invoke(_favorBalance);
            return true;
        }

        private void OnResourceSold(ResourceDataSO resource, int amount, int value)
        {
            if (_currentTributeResource == null || resource != _currentTributeResource || amount <= 0)
            {
                return;
            }

            _deliveredAmount += amount;

            int completedThresholds = 0;
            while (_requiredAmount > 0 && _deliveredAmount >= _requiredAmount)
            {
                int completedRequirement = _requiredAmount;
                _deliveredAmount -= _requiredAmount;
                AddFavor(1);
                _todayFavorEarned++;
                _todayFavorReasons.Add($"{_currentTributeResource.DisplayName} {completedRequirement}개 납품");
                completedThresholds++;
                _requiredAmount += 2;
            }

            if (completedThresholds > 0)
            {
                Debug.Log(
                    $"[TributeManager] 진상품 납품 {completedThresholds}회 완료! 총애 +{completedThresholds} " +
                    $"(보유: {_favorBalance}, 다음 요구치: {_requiredAmount})");
            }

            OnTributeChanged?.Invoke();
        }

        private void ResolveReferences()
        {
            if (_quotaManager == null)
            {
                _quotaManager = FindFirstObjectByType<QuotaManager>();
            }

            if (_resourceRegistry == null)
            {
                _resourceRegistry = FindFirstObjectByType<ResourceRegistry>();
            }
        }

        private List<ResourceDataSO> GetTributeCandidates()
        {
            if (_resourceRegistry != null && _resourceRegistry.AllResources != null && _resourceRegistry.AllResources.Count > 0)
            {
                var result = new List<ResourceDataSO>();
                foreach (ResourceDataSO resource in _resourceRegistry.AllResources)
                {
                    if (resource != null)
                    {
                        result.Add(resource);
                    }
                }
                return result;
            }

            _fallbackResources.Clear();
            ResourceDataSO[] loadedResources = Resources.FindObjectsOfTypeAll<ResourceDataSO>();
            foreach (ResourceDataSO resource in loadedResources)
            {
                if (resource != null && !_fallbackResources.Contains(resource))
                {
                    _fallbackResources.Add(resource);
                }
            }

            return _fallbackResources;
        }
    }
}
