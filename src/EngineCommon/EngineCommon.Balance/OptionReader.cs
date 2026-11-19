using System.Globalization;

namespace EngineCommon.Balance;

/// <summary>The reading steps the sweep and the rare-event command lines share: an option given once, its value, and a whole number inside a range.</summary>
internal static class OptionReader
{
    public static EngineRefusedException Twice(string option) => new(BalanceCodes.OptionTwice, option + " is given once");

    public static string Value(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count) throw new EngineRefusedException(BalanceCodes.OptionValue, args[i] + " needs a value");
        i++;
        return args[i];
    }

    public static long Whole(string what, string text, long min, long max, string rangeCode)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) throw new EngineRefusedException(BalanceCodes.OptionValue, what + " is a whole number, not '" + text + "'");
        if (number < min || number > max) throw new EngineRefusedException(rangeCode, what + " " + number.ToString(CultureInfo.InvariantCulture) + " is outside " + min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture));
        return number;
    }
}
