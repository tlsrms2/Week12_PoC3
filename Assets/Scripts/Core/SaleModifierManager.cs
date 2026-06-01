using System.Collections.Generic;
using FactoryDelivery.Data;
using FactoryDelivery.Resource;
using UnityEngine;

namespace FactoryDelivery.Core
{
    public class SaleModifierManager : MonoBehaviour
    {
        private readonly Dictionary<ResourceDataSO, int> _permanentUnitBonuses = new Dictionary<ResourceDataSO, int>();

        public void AddPermanentUnitBonus(ResourceDataSO resource, int amount)
        {
            if (resource == null || amount == 0)
            {
                return;
            }

            int current = GetPermanentUnitBonus(resource);
            _permanentUnitBonuses[resource] = current + amount;
        }

        public int GetPermanentUnitBonus(ResourceDataSO resource)
        {
            if (resource == null)
            {
                return 0;
            }

            return _permanentUnitBonuses.TryGetValue(resource, out int amount) ? amount : 0;
        }

        public void ClearPermanentUnitBonuses()
        {
            _permanentUnitBonuses.Clear();
        }

        public SaleResult CalculateSale(SaleContext context)
        {
            ResourceDataSO resource = context.Resource;
            int amount = Mathf.Max(0, context.Amount);
            int baseUnitValue = resource != null ? Mathf.Max(0, resource.BaseValue) : 0;
            float unitMultiplier = 1f;
            int flatUnitBonus = GetPermanentUnitBonus(resource);
            var notes = new List<string>();

            if (flatUnitBonus != 0)
            {
                notes.Add($"[실학자의 서책] 영구 단가 +{flatUnitBonus}");
            }

            if (EdictManager.Instance != null && EdictManager.Instance.HasEdict(EdictType.MoonlightSmuggler) && context.IsNightSale)
            {
                unitMultiplier *= 2.5f;
                notes.Add("[달빛 상단] 야간 판매 2.5배");
            }

            if (EdictManager.Instance != null && EdictManager.Instance.HasEdict(EdictType.ExtremeDecision) && resource != null)
            {
                if (resource.IsRawResource)
                {
                    unitMultiplier *= 0.5f;
                    notes.Add("[어전회의] 원자재 가치 50% 하락");
                }
                else
                {
                    unitMultiplier *= 2f;
                    notes.Add("[어전회의] 가공품 가치 2배 상승");
                }
            }

            if (EdictManager.Instance != null && EdictManager.Instance.HasEdict(EdictType.RiceBagsOfHojo))
            {
                int riceAmount = GetAmountByDisplayName(context.Inventory, "쌀");
                if (riceAmount < 200)
                {
                    riceAmount = GetAmountByDisplayName(context.Inventory, "쌀 가마니");
                }

                if (riceAmount >= 200 && IsNamedResource(resource, "메주", "무명천"))
                {
                    unitMultiplier *= 2.5f;
                    notes.Add("[호조판서의 쌀가마니] 대상 품목 2.5배");
                }
            }

            if (EdictManager.Instance != null && EdictManager.Instance.HasEdict(EdictType.CleanOfficialsYard))
            {
                int emptyCellCount = context.GridManager != null ? context.GridManager.GetEmptyOwnedCellCount() : 0;
                if (emptyCellCount >= 15)
                {
                    unitMultiplier *= 1.2f;
                    notes.Add("[청백리의 텅 빈 마당] 빈칸 유지 20% 보너스");
                }
            }

            if (EdictManager.Instance != null && EdictManager.Instance.HasEdict(EdictType.SpringHungerRelief))
            {
                int emptyResourceTypes = CountEmptyResourceTypes(context.Inventory, context.ResourceRegistry);
                if (emptyResourceTypes > 0)
                {
                    flatUnitBonus += emptyResourceTypes;
                    notes.Add($"[보릿고개의 구휼] 비보유 자원 {emptyResourceTypes}종");
                }
            }

            if (EdictManager.Instance != null && EdictManager.Instance.HasEdict(EdictType.AestheticsOfOverload) && resource != null && resource.IsSpecialty)
            {
                unitMultiplier *= 2f;
                notes.Add("[과적의 미학] 특상품 가치 2배 상승");
            }

            if (EdictManager.Instance != null && EdictManager.Instance.HasEdict(EdictType.HojoInventoryClearing) && context.IsSettlementAutoSale && resource != null && resource.IsRawResource)
            {
                unitMultiplier *= 1.5f;
                notes.Add("[호조의 재고 정리] 정산 자동 판매 1.5배");
            }

            int finalUnitValue = Mathf.Max(0, Mathf.RoundToInt((baseUnitValue + flatUnitBonus) * unitMultiplier));
            int totalValue = finalUnitValue * amount;

            if (finalUnitValue != baseUnitValue)
            {
                notes.Add($"단가 {baseUnitValue} -> {finalUnitValue}");
            }

            return new SaleResult(baseUnitValue, finalUnitValue, totalValue, notes);
        }

        private static bool IsNamedResource(ResourceDataSO resource, params string[] displayNames)
        {
            if (resource == null || displayNames == null)
            {
                return false;
            }

            for (int i = 0; i < displayNames.Length; i++)
            {
                if (resource.DisplayName == displayNames[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetAmountByDisplayName(ResourceInventory inventory, string displayName)
        {
            if (inventory == null || string.IsNullOrEmpty(displayName))
            {
                return 0;
            }

            foreach (KeyValuePair<ResourceDataSO, int> holding in inventory.Holdings)
            {
                if (holding.Key != null && holding.Key.DisplayName == displayName)
                {
                    return holding.Value;
                }
            }

            return 0;
        }

        private static int CountEmptyResourceTypes(ResourceInventory inventory, ResourceRegistry registry)
        {
            if (registry == null || registry.AllResources == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ResourceDataSO resource in registry.AllResources)
            {
                if (resource == null)
                {
                    continue;
                }

                int amount = inventory != null ? inventory.GetAmount(resource) : 0;
                if (amount <= 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
