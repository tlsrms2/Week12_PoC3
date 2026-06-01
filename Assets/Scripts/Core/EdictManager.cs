using System;
using System.Collections.Generic;
using UnityEngine;

namespace FactoryDelivery.Core
{
    /// <summary>
    /// 장착형 시너지 강화(어명)를 관리하는 클래스입니다.
    /// 제한된 슬롯 내에서 교체 가능합니다.
    /// </summary>
    public class EdictManager : MonoBehaviour
    {
        private static EdictManager _instance;
        public static EdictManager Instance => _instance;

        [SerializeField] private int _maxSlots = 3;
        private readonly List<EdictType> _equippedEdicts = new List<EdictType>();

        public event Action OnEdictsChanged;

        public int MaxSlots => _maxSlots;

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
        /// 어명을 장착합니다. 슬롯이 꽉 찼다면 마지막 어명을 제거하고 추가합니다.
        /// </summary>
        public void EquipEdict(EdictType edict)
        {
            if (edict == EdictType.None) return;

            if (_equippedEdicts.Contains(edict))
            {
                Debug.Log($"[EdictManager] 이미 장착된 어명입니다: {edict}");
                return;
            }

            if (_equippedEdicts.Count >= _maxSlots)
            {
                EdictType removed = _equippedEdicts[0];
                _equippedEdicts.RemoveAt(0);
                Debug.Log($"[EdictManager] 슬롯 부족으로 어명 제거: {removed}");
            }

            _equippedEdicts.Add(edict);
            Debug.Log($"[EdictManager] 어명 장착: {edict}");
            OnEdictsChanged?.Invoke();
        }

        public bool HasEdict(EdictType edict)
        {
            if (GameManager.Instance != null && GameManager.Instance.DisableRoguelikeSystems)
            {
                return false;
            }
            return _equippedEdicts.Contains(edict);
        }

        public List<EdictType> GetEquippedEdicts()
        {
            if (GameManager.Instance != null && GameManager.Instance.DisableRoguelikeSystems)
            {
                return new List<EdictType>();
            }
            return new List<EdictType>(_equippedEdicts);
        }

        public void ClearEdicts()
        {
            if (_equippedEdicts.Count == 0)
            {
                return;
            }

            _equippedEdicts.Clear();
            OnEdictsChanged?.Invoke();
        }
    }
}
