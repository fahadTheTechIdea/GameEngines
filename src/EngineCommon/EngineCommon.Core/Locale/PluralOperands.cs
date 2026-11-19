using System.Globalization;
using EngineCommon;

namespace EngineCommon;

/// <summary>
/// The operands the CLDR plural rule language reads from a number (UTS 35 part 3): <c>n</c> its absolute value, <c>i</c> the integer digits, <c>v</c> the count of visible fraction digits
/// with trailing zeros, <c>w</c> the same without trailing zeros, <c>f</c> the visible fraction digits as a whole number, <c>t</c> the same without trailing zeros. <c>c</c> and <c>e</c>
/// (compact exponent) are zero: a number is given in full. "1.0" and "1" are different numbers to the rules, which is why a decimal is given as text.
/// </summary>
public readonly record struct PluralOperands(decimal N, long I, int V, int W, long F, long T)
{
    private const int MaxDigits = 18;

    /// <summary>Reads <c>-?digits</c> with an optional <c>.digits</c>; anything else (an exponent, a separator, a space) is refused.</summary>
    public static PluralOperands Parse(string number)
    {
        ArgumentNullException.ThrowIfNull(number);
        var text = number.StartsWith('-') ? number[1..] : number;
        var dot = text.IndexOf('.');
        var whole = dot < 0 ? text : text[..dot];
        var fraction = dot < 0 ? "" : text[(dot + 1)..];
        if (whole.Length == 0 || !whole.All(char.IsAsciiDigit) || (dot >= 0 && (fraction.Length == 0 || !fraction.All(char.IsAsciiDigit))) || whole.Length + fraction.Length > MaxDigits)
            throw new EngineRefusedException(LocaleCodes.Argument, "'" + number + "' is not a plain number of at most " + MaxDigits + " digits");
        var trimmed = fraction.TrimEnd('0');
        var f = fraction.Length == 0 ? 0 : long.Parse(fraction, CultureInfo.InvariantCulture);
        var t = trimmed.Length == 0 ? 0 : long.Parse(trimmed, CultureInfo.InvariantCulture);
        return new PluralOperands(decimal.Parse(text, CultureInfo.InvariantCulture), long.Parse(whole, CultureInfo.InvariantCulture), fraction.Length, trimmed.Length, f, t);
    }

    public static PluralOperands Of(long integer) => new(Math.Abs((decimal)integer), Math.Abs(integer), 0, 0, 0, 0);

    internal decimal Operand(char name)
    {
        switch (name)
        {
            case 'n': return N;
            case 'i': return I;
            case 'v': return V;
            case 'w': return W;
            case 'f': return F;
            case 't': return T;
            case 'c': case 'e': return 0;
        }
        throw new InvalidOperationException("unknown plural operand '" + name + "'");
    }
}
