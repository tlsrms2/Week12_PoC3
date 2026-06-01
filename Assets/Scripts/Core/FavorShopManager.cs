using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using FactoryDelivery.Block;
using FactoryDelivery.UI;
using FactoryDelivery.Logistics;
using FactoryDelivery.Grid;
using FactoryDelivery.Data;
using FactoryDelivery.Resource;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Core
{
    public class FavorShopManager : MonoBehaviour
    {
        private enum GiftTargetMode
        {
            None,
            CarpentersMasterstroke
        }

        [SerializeField] private TributeManager _tributeManager;
        [SerializeField] private BlockFactory _blockFactory;
        [SerializeField] private BlockDeckManager _blockDeckManager;
        [SerializeField] private int _warehouseCardCost = 1;
        [SerializeField] private int _winterColdCost = 1;
        [SerializeField] private int _midnightTorchCost = 1;
        [SerializeField] private int _executionersSwordCost = 2;
        [SerializeField] private int _scholarsBookCost = 3;
        [SerializeField] private int _inspectorTokenCost = 3;
        [SerializeField] private int _magistrateShoutCost = 2;
        [SerializeField] private int _carpentersMasterstrokeCost = 3;
        [SerializeField] private int _mandateCost = 5;
        [SerializeField] private int _edictCost = 4;

        [SerializeField] private float _winterColdNightSeconds = 10f;
        [SerializeField] private float _midnightTorchNightSeconds = 5f;

        private GiftTargetMode _pendingTargetMode;
        private bool _executionersSwordArmed;

        public bool IsExecutionersSwordArmed => _executionersSwordArmed;
        public bool IsCarpentersMasterstrokeArmed => _pendingTargetMode == GiftTargetMode.CarpentersMasterstroke;

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

            HandleGiftTargetingInput();
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

        public bool TryBuyExecutionersSword()
        {
            ResolveReferences();
            if (_tributeManager != null && _tributeManager.TrySpendFavor(_executionersSwordCost))
            {
                GameManager.Instance.Gifts.Add(GiftType.ExecutionersSword);
                return true;
            }
            return false;
        }

        public bool TryUseExecutionersSword()
        {
            if (GameManager.Instance == null || GameManager.Instance.Gifts.GetCount(GiftType.ExecutionersSword) <= 0)
            {
                return false;
            }

            CancelAllTargetModes();
            _executionersSwordArmed = true;
            Debug.Log("[FavorShopManager] 망나니의 큰 칼 준비 완료. 블록 카드의 원하는 칸을 클릭하세요. ESC/우클릭으로 취소할 수 있습니다.");
            return true;
        }

        public bool TryApplyExecutionersSwordToCard(BlockCardUI card, Vector2 screenPosition)
        {
            if (!_executionersSwordArmed || GameManager.Instance == null || card == null)
            {
                return false;
            }

            if (!card.TrySliceCellAtScreenPosition(screenPosition))
            {
                Debug.Log("[FavorShopManager] 썰어낼 칸을 정확히 클릭했는지 확인하세요. 1칸짜리 카드는 절단할 수 없습니다.");
                return false;
            }

            if (!GameManager.Instance.Gifts.TryConsume(GiftType.ExecutionersSword))
            {
                _executionersSwordArmed = false;
                return false;
            }

            _executionersSwordArmed = false;
            Debug.Log("[FavorShopManager] 망나니의 큰 칼 발동! 선택한 블록 카드에서 1칸을 제거했습니다.");
            return true;
        }

        public bool TryBuyScholarsBook()
        {
            ResolveReferences();
            if (_tributeManager != null && _tributeManager.TrySpendFavor(_scholarsBookCost))
            {
                GameManager.Instance.Gifts.Add(GiftType.ScholarsBook);
                return true;
            }
            return false;
        }

        public bool TryUseScholarsBook()
        {
            ResolveReferences();

            if (GameManager.Instance == null || !GameManager.Instance.Gifts.TryConsume(GiftType.ScholarsBook))
            {
                return false;
            }

            ResourceDataSO target = FindLowestOwnedResource();
            SaleModifierManager saleModifiers = GameManager.Instance.SaleModifiers;
            if (target == null || saleModifiers == null)
            {
                GameManager.Instance.Gifts.Add(GiftType.ScholarsBook);
                return false;
            }

            saleModifiers.AddPermanentUnitBonus(target, 3);
            Debug.Log($"[FavorShopManager] 실학자의 서책 발동! '{target.DisplayName}'의 단가가 이번 런 동안 +3 상승합니다.");
            return true;
        }

        public bool TryBuyRoyalInspectorToken()
        {
            ResolveReferences();
            if (_tributeManager != null && _tributeManager.TrySpendFavor(_inspectorTokenCost))
            {
                GameManager.Instance.Gifts.Add(GiftType.RoyalInspectorToken);
                return true;
            }
            return false;
        }

        public bool TryUseRoyalInspectorToken()
        {
            if (GameManager.Instance == null || !GameManager.Instance.Gifts.TryConsume(GiftType.RoyalInspectorToken)) return false;

            WorkerSpawner spawner = FindFirstObjectByType<WorkerSpawner>();
            if (spawner != null)
            {
                spawner.SetTodaySpeedMultiplier(2.0f);
                Debug.Log("[FavorShopManager] 암행어사의 마패 발동! 오늘 하루 일꾼 속도가 2배가 됩니다.");
                return true;
            }
            return false;
        }

        public bool TryBuyMagistrateShout()
        {
            ResolveReferences();
            if (_tributeManager != null && _tributeManager.TrySpendFavor(_magistrateShoutCost))
            {
                GameManager.Instance.Gifts.Add(GiftType.MagistrateShout);
                return true;
            }
            return false;
        }

        public bool TryUseMagistrateShout()
        {
            if (GameManager.Instance == null || !GameManager.Instance.Gifts.TryConsume(GiftType.MagistrateShout)) return false;

            WorkerSpawner spawner = FindFirstObjectByType<WorkerSpawner>();
            if (spawner != null)
            {
                List<Worker> activeWorkers = spawner.GetAllActiveWorkers();
                int count = 0;
                foreach (Worker worker in activeWorkers)
                {
                    foreach (ResourceDataSO res in worker.CarriedResources)
                    {
                        GameManager.Instance.Inventory.Add(res, 1);
                    }
                    spawner.ReturnWorker(worker);
                    count++;
                }
                Debug.Log($"[FavorShopManager] 포도청의 호통! {count}명의 일꾼을 즉시 수납 처리했습니다.");
                return true;
            }
            return false;
        }

        public bool TryBuyCarpentersMasterstroke()
        {
            ResolveReferences();
            if (_tributeManager != null && _tributeManager.TrySpendFavor(_carpentersMasterstrokeCost))
            {
                GameManager.Instance.Gifts.Add(GiftType.CarpentersMasterstroke);
                return true;
            }
            return false;
        }

        public bool TryUseCarpentersMasterstroke()
        {
            if (GameManager.Instance == null || GameManager.Instance.Gifts.GetCount(GiftType.CarpentersMasterstroke) <= 0)
            {
                return false;
            }

            CancelAllTargetModes();
            _pendingTargetMode = GiftTargetMode.CarpentersMasterstroke;
            Debug.Log("[FavorShopManager] 도편수의 묘수 준비 완료. 맵 위의 레벨업시킬 타일을 클릭하세요. ESC/우클릭으로 취소할 수 있습니다.");
            return true;
        }

        public bool TryLevelUpTileWithGift(Vector2Int gridPos)
        {
            if (GameManager.Instance == null || !GameManager.Instance.Gifts.TryConsume(GiftType.CarpentersMasterstroke)) return false;

            GridManager grid = FindFirstObjectByType<GridManager>();
            if (grid != null)
            {
                TileEntity entity = grid.GetTileAt(gridPos);
                if (entity != null && entity.CanLevelUp)
                {
                    grid.TryPlaceTile(gridPos, entity.Data, out _);
                    Debug.Log($"[FavorShopManager] 도편수의 묘수! {gridPos} 타일 레벨업.");
                    return true;
                }
            }

            GameManager.Instance.Gifts.Add(GiftType.CarpentersMasterstroke);
            return false;
        }

        public bool TryBuyRandomMandate()
        {
            ResolveReferences();
            if (MandateManager.Instance == null || _tributeManager == null) return false;

            var available = new List<MandateType>();
            foreach (MandateType type in System.Enum.GetValues(typeof(MandateType)))
            {
                if (type == MandateType.None) continue;
                if (!MandateManager.Instance.HasMandate(type)) available.Add(type);
            }

            if (available.Count == 0)
            {
                Debug.Log("[FavorShopManager] 이미 모든 어명을 획득했습니다.");
                return false;
            }

            if (_tributeManager.TrySpendFavor(_mandateCost))
            {
                MandateType selected = available[Random.Range(0, available.Count)];
                MandateManager.Instance.AddMandate(selected);
                Debug.Log($"[FavorShopManager] 어명 구매 완료: {selected}. 총애 {_mandateCost} 소모.");
                return true;
            }
            return false;
        }

        public bool TryBuyRandomEdict()
        {
            ResolveReferences();
            if (EdictManager.Instance == null || _tributeManager == null) return false;

            var available = new List<EdictType>();
            foreach (EdictType type in System.Enum.GetValues(typeof(EdictType)))
            {
                if (type == EdictType.None) continue;
                if (!EdictManager.Instance.HasEdict(type)) available.Add(type);
            }

            if (available.Count == 0)
            {
                Debug.Log("[FavorShopManager] 이미 모든 교지를 장착했습니다.");
                return false;
            }

            if (_tributeManager.TrySpendFavor(_edictCost))
            {
                EdictType selected = available[Random.Range(0, available.Count)];
                EdictManager.Instance.EquipEdict(selected);
                Debug.Log($"[FavorShopManager] 교지 구매 완료: {selected}. 총애 {_edictCost} 소모.");
                return true;
            }
            return false;
        }

        private void HandleGiftTargetingInput()
        {
            if (!_executionersSwordArmed && _pendingTargetMode == GiftTargetMode.None)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelAllTargetModes();
                Debug.Log("[FavorShopManager] 하사품 대상 지정이 취소되었습니다.");
                return;
            }

            if (_pendingTargetMode != GiftTargetMode.CarpentersMasterstroke || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = mouseWorld.ToGridPosition();
            if (TryLevelUpTileWithGift(gridPos))
            {
                _pendingTargetMode = GiftTargetMode.None;
            }
            else
            {
                Debug.Log("[FavorShopManager] 레벨업 가능한 타일을 클릭해야 합니다.");
            }
        }

        private void CancelAllTargetModes()
        {
            _executionersSwordArmed = false;
            _pendingTargetMode = GiftTargetMode.None;
        }

        private ResourceDataSO FindLowestOwnedResource()
        {
            if (GameManager.Instance == null)
            {
                return null;
            }

            ResourceInventory inventory = GameManager.Instance.Inventory;
            ResourceRegistry registry = FindFirstObjectByType<ResourceRegistry>();
            if (registry == null || registry.AllResources == null || registry.AllResources.Count == 0)
            {
                return null;
            }

            ResourceDataSO best = null;
            int lowestAmount = int.MaxValue;

            foreach (ResourceDataSO resource in registry.AllResources)
            {
                if (resource == null)
                {
                    continue;
                }

                int amount = inventory != null ? inventory.GetAmount(resource) : 0;
                if (best == null || amount < lowestAmount || (amount == lowestAmount && string.CompareOrdinal(resource.DisplayName, best.DisplayName) < 0))
                {
                    best = resource;
                    lowestAmount = amount;
                }
            }

            return best;
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
