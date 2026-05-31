using System.Collections.Generic;
using FactoryDelivery.Data;

namespace FactoryDelivery.Core
{
    public readonly struct SaleContext
    {
        public SaleContext(ResourceDataSO resource, int amount, DayManager dayManager, TributeManager tributeManager)
        {
            Resource = resource;
            Amount = amount;
            DayManager = dayManager;
            TributeManager = tributeManager;
        }

        public ResourceDataSO Resource { get; }
        public int Amount { get; }
        public DayManager DayManager { get; }
        public TributeManager TributeManager { get; }
        public bool IsNightSale => DayManager != null && DayManager.IsNight;
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
