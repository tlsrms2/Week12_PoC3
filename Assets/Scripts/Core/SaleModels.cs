using System.Collections.Generic;
using FactoryDelivery.Data;
using FactoryDelivery.Grid;
using FactoryDelivery.Resource;

namespace FactoryDelivery.Core
{
    public readonly struct SaleContext
    {
        public SaleContext(
            ResourceDataSO resource,
            int amount,
            DayManager dayManager,
            TributeManager tributeManager,
            ResourceInventory inventory,
            ResourceRegistry resourceRegistry,
            GridManager gridManager,
            bool isSettlementAutoSale = false)
        {
            Resource = resource;
            Amount = amount;
            DayManager = dayManager;
            TributeManager = tributeManager;
            Inventory = inventory;
            ResourceRegistry = resourceRegistry;
            GridManager = gridManager;
            IsSettlementAutoSale = isSettlementAutoSale;
        }

        public ResourceDataSO Resource { get; }
        public int Amount { get; }
        public DayManager DayManager { get; }
        public TributeManager TributeManager { get; }
        public ResourceInventory Inventory { get; }
        public ResourceRegistry ResourceRegistry { get; }
        public GridManager GridManager { get; }
        public bool IsNightSale => DayManager != null && DayManager.IsNight;
        public bool IsSettlementAutoSale { get; }
    }

    public readonly struct SaleResult
    {
        public SaleResult(int baseUnitValue, int finalUnitValue, int totalValue, IReadOnlyList<string> modifierNotes)
        {
            BaseUnitValue = baseUnitValue;
            FinalUnitValue = finalUnitValue;
            TotalValue = totalValue;
            ModifierNotes = modifierNotes;
        }

        public int BaseUnitValue { get; }
        public int FinalUnitValue { get; }
        public int TotalValue { get; }
        public IReadOnlyList<string> ModifierNotes { get; }
    }
}
