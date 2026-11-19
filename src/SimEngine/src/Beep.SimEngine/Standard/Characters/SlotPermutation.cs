using System;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// A seeded bijection on the slots 0 to count - 1 (a four-round Feistel network on the smallest even bit width that holds the count,
    /// walked until the result falls inside the count). The nth casualty of a squad is slot <c>Permute(n)</c>: casualties are spread
    /// without clustering, no two casualties are the same slot, and nothing about the order has to be stored.
    /// </summary>
    public static class SlotPermutation
    {
        public static int Permute(int index, int count, ulong key)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            if (count == 1) return 0;
            int bits = 2;
            while ((1L << bits) < count) bits += 2;
            int half = bits / 2;
            uint mask = (1u << half) - 1u;
            uint value = (uint)index;
            do
            {
                uint left = value >> half, right = value & mask;
                for (int round = 0; round < 4; round++)
                {
                    uint next = left ^ (Round(key, round, right) & mask);
                    left = right;
                    right = next;
                }
                value = (left << half) | right;
            }
            while (value >= (uint)count);
            return (int)value;
        }

        private static uint Round(ulong key, int round, uint right)
        {
            unchecked
            {
                ulong z = key + (ulong)(round + 1) * 0x9E3779B97F4A7C15UL + right * 0xD6E8FEB86659FD93UL;
                z = (z ^ (z >> 32)) * 0xD6E8FEB86659FD93UL;
                z = (z ^ (z >> 32)) * 0xD6E8FEB86659FD93UL;
                return (uint)(z ^ (z >> 32));
            }
        }
    }
}
