using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CombatEngine;

/// <summary>
/// The only source of randomness: a value from the caller's seed, the caller's attempt counter and the shot's own identity. There is no
/// generator state, so the same shot always rolls the same way and any shot can be re-rolled to check it. Numbers are written in the
/// invariant culture, so no machine setting changes a roll.
/// </summary>
public static class CombatRoll
{
    public static long Permille(ulong seed, long attempt, string group, string battery, long index)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"combat-roll|{seed}|{attempt}|{group}|{battery}|{index}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return (long)(BinaryPrimitives.ReadUInt64BigEndian(hash) % 1000UL);
    }
}
