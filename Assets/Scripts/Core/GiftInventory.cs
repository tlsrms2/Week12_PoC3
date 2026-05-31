using System;
using System.Collections.Generic;

namespace FactoryDelivery.Core
{
    public enum GiftType
    {
        WarehouseFoundation,
        WinterCold,
        MidnightTorch
    }

    public class GiftInventory
    {
        private readonly Dictionary<GiftType, int> _counts = new Dictionary<GiftType, int>();

        public event Action<GiftType, int> OnGiftCountChanged;

        public int GetCount(GiftType giftType)
        {
            return _counts.TryGetValue(giftType, out int count) ? count : 0;
        }

        public void Add(GiftType giftType, int amount = 1)
        {
            if (amount <= 0)
            {
                return;
            }

            int nextCount = GetCount(giftType) + amount;
            _counts[giftType] = nextCount;
            OnGiftCountChanged?.Invoke(giftType, nextCount);
        }

        public bool TryConsume(GiftType giftType, int amount = 1)
        {
            if (amount <= 0)
            {
                return false;
            }

            int current = GetCount(giftType);
            if (current < amount)
            {
                return false;
            }

            int nextCount = current - amount;
            if (nextCount <= 0)
            {
                _counts.Remove(giftType);
            }
            else
            {
                _counts[giftType] = nextCount;
            }

            OnGiftCountChanged?.Invoke(giftType, nextCount);
            return true;
        }

        public void Clear()
        {
            if (_counts.Count == 0)
            {
                return;
            }

            GiftType[] giftTypes = new GiftType[_counts.Count];
            _counts.Keys.CopyTo(giftTypes, 0);
            _counts.Clear();

            foreach (GiftType giftType in giftTypes)
            {
                OnGiftCountChanged?.Invoke(giftType, 0);
            }
        }
    }
}
