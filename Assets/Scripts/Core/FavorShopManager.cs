using UnityEngine;
using FactoryDelivery.Block;
using FactoryDelivery.UI;

namespace FactoryDelivery.Core
{
    public class FavorShopManager : MonoBehaviour
    {
        [SerializeField] private TributeManager _tributeManager;
        [SerializeField] private BlockFactory _blockFactory;
        [SerializeField] private BlockDeckManager _blockDeckManager;
        [SerializeField] private int _warehouseCardCost = 1;
        [SerializeField] private int _winterColdCost = 1;
        [SerializeField] private int _midnightTorchCost = 1;
        [SerializeField] private float _winterColdNightSeconds = 10f;
        [SerializeField] private float _midnightTorchNightSeconds = 5f;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                TryBuyWarehouseFoundation();
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                TryUseWinterCold();
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                TryUseMidnightTorch();
            }
        }

        public bool TryBuyWarehouseFoundation()
        {
            ResolveReferences();

            if (_tributeManager == null || GameManager.Instance == null)
            {
                Debug.LogWarning("[FavorShopManager] 총애 상점 구매에 필요한 참조가 부족합니다.");
                return false;
            }

            if (!_tributeManager.TrySpendFavor(_warehouseCardCost))
            {
                Debug.Log("[FavorShopManager] 총애가 부족하여 물류창고 기반을 구매할 수 없습니다.");
                return false;
            }

            GameManager.Instance.Gifts.Add(GiftType.WarehouseFoundation);
            Debug.Log($"[FavorShopManager] 물류창고 기반 구매 완료. 총애 {_warehouseCardCost} 소모.");
            return true;
        }

        public bool TryUseWarehouseFoundation()
        {
            ResolveReferences();

            if (GameManager.Instance == null || _blockFactory == null || _blockDeckManager == null)
            {
                Debug.LogWarning("[FavorShopManager] 물류창고 기반 발동에 필요한 참조가 부족합니다.");
                return false;
            }

            if (!GameManager.Instance.Gifts.TryConsume(GiftType.WarehouseFoundation))
            {
                Debug.Log("[FavorShopManager] 보유한 물류창고 기반이 없습니다.");
                return false;
            }

            BlockInstance warehouseBlock = _blockFactory.CreateWarehouseBlock(2);
            if (warehouseBlock == null || !_blockDeckManager.AddCard(warehouseBlock))
            {
                GameManager.Instance.Gifts.Add(GiftType.WarehouseFoundation);
                Debug.LogWarning("[FavorShopManager] 물류창고 카드를 지급하지 못해 하사품을 되돌렸습니다.");
                return false;
            }

            Debug.Log("[FavorShopManager] 물류창고 기반 발동. 2x2 창고 카드를 지급했습니다.");
            return true;
        }

        public bool TryBuyWinterCold()
        {
            ResolveReferences();

            if (_tributeManager == null || GameManager.Instance == null)
            {
                Debug.LogWarning("[FavorShopManager] 겨울철의 추위를 구매할 수 있는 참조가 부족합니다.");
                return false;
            }

            if (!_tributeManager.TrySpendFavor(_winterColdCost))
            {
                Debug.Log("[FavorShopManager] 총애가 부족하여 겨울철의 추위를 구매할 수 없습니다.");
                return false;
            }

            GameManager.Instance.Gifts.Add(GiftType.WinterCold);
            Debug.Log($"[FavorShopManager] 겨울철의 추위 구매 완료. 총애 {_winterColdCost} 소모.");
            return true;
        }

        public bool TryUseWinterCold()
        {
            ResolveReferences();

            if (_tributeManager == null || GameManager.Instance == null || GameManager.Instance.Day == null)
            {
                Debug.LogWarning("[FavorShopManager] 겨울철의 추위를 발동할 수 있는 참조가 부족합니다.");
                return false;
            }

            if (!GameManager.Instance.Gifts.TryConsume(GiftType.WinterCold))
            {
                Debug.Log("[FavorShopManager] 보유한 겨울철의 추위가 없습니다.");
                return false;
            }

            GameManager.Instance.Day.AddNightTime(_winterColdNightSeconds, false);
            Debug.Log($"[FavorShopManager] 겨울철의 추위 발동. 당일 밤 시간 {_winterColdNightSeconds}초 증가.");
            return true;
        }

        public bool TryBuyMidnightTorch()
        {
            ResolveReferences();

            if (_tributeManager == null || GameManager.Instance == null)
            {
                Debug.LogWarning("[FavorShopManager] 심야의 횃불을 구매할 수 있는 참조가 부족합니다.");
                return false;
            }

            if (!_tributeManager.TrySpendFavor(_midnightTorchCost))
            {
                Debug.Log("[FavorShopManager] 총애가 부족하여 심야의 횃불을 구매할 수 없습니다.");
                return false;
            }

            GameManager.Instance.Gifts.Add(GiftType.MidnightTorch);
            Debug.Log($"[FavorShopManager] 심야의 횃불 구매 완료. 총애 {_midnightTorchCost} 소모.");
            return true;
        }

        public bool TryUseMidnightTorch()
        {
            ResolveReferences();

            if (GameManager.Instance == null || GameManager.Instance.Day == null)
            {
                Debug.LogWarning("[FavorShopManager] 심야의 횃불을 발동할 수 있는 참조가 부족합니다.");
                return false;
            }

            if (!GameManager.Instance.Gifts.TryConsume(GiftType.MidnightTorch))
            {
                Debug.Log("[FavorShopManager] 보유한 심야의 횃불이 없습니다.");
                return false;
            }

            GameManager.Instance.Day.AddNightTime(_midnightTorchNightSeconds, true);
            Debug.Log($"[FavorShopManager] 심야의 횃불 발동. 이후 밤 시간 {_midnightTorchNightSeconds}초 증가.");
            return true;
        }

        private void ResolveReferences()
        {
            if (_tributeManager == null)
            {
                _tributeManager = GameManager.Instance != null
                    ? GameManager.Instance.Tribute
                    : FindFirstObjectByType<TributeManager>();
            }

            if (_blockFactory == null)
            {
                _blockFactory = FindFirstObjectByType<BlockFactory>();
            }

            if (_blockDeckManager == null)
            {
                _blockDeckManager = FindFirstObjectByType<BlockDeckManager>();
            }
        }
    }
}
