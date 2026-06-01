using System;
using System.Collections.Generic;
using UnityEngine;

namespace FactoryDelivery.Core
{
    /// <summary>
    /// 게임 전체에 적용되는 영구 패시브 강화(어명)를 관리하는 클래스입니다.
    /// </summary>
    public class MandateManager : MonoBehaviour
    {
        private static MandateManager _instance;
        public static MandateManager Instance => _instance;

        private readonly HashSet<MandateType> _activeMandates = new HashSet<MandateType>();

        public event Action OnMandatesChanged;

        public IReadOnlyCollection<MandateType> ActiveMandates => _activeMandates;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 특정 어명을 획득합니다.
        /// </summary>
        public void AddMandate(MandateType mandate)
        {
            if (mandate == MandateType.None) return;

            if (_activeMandates.Add(mandate))
            {
                Debug.Log($"[MandateManager] 어명 획득: {mandate}");
                OnMandatesChanged?.Invoke();
            }
        }

        /// <summary>
        /// 특정 어명이 활성화되어 있는지 확인합니다.
        /// </summary>
        public bool HasMandate(MandateType mandate) => _activeMandates.Contains(mandate);

        /// <summary>
        /// 모든 어명 효과를 초기화합니다. (새 게임 시작 시)
        /// </summary>
        public void ClearMandates()
        {
            if (_activeMandates.Count == 0)
            {
                return;
            }

            _activeMandates.Clear();
            OnMandatesChanged?.Invoke();
        }

        public float GetWorkerSpeedMultiplier(DayPeriod period)
        {
            float multiplier = 1f;

            if (HasMandate(MandateType.WideRoadPaving))
                multiplier *= 2f;

            if (HasMandate(MandateType.NocturnalWorkers))
            {
                multiplier *= (period == DayPeriod.Day) ? 0.7f : 3.0f;
            }

            if (HasMandate(MandateType.WhiteNightLabor) && period == DayPeriod.Night)
            {
                multiplier *= 1.5f;
            }

            return multiplier;
        }

        public float GetProcessingSpeedMultiplier(DayPeriod period)
        {
            float multiplier = 1f;

            if (HasMandate(MandateType.BlessingOfCoupledTrees))
                multiplier *= 1.2f;

            if (HasMandate(MandateType.WhiteNightLabor) && period == DayPeriod.Night)
            {
                multiplier *= 2.0f;
            }

            return multiplier;
        }
    }
}
