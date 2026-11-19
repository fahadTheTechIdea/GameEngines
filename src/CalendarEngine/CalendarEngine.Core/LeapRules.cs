using EngineCommon;

namespace CalendarEngine;

/// <summary>One leap rule: years where (year - offset) is a multiple of <paramref name="Every"/> are leap or not. The last rule that matches decides (Gregorian: every 4 leap, every 100 not, every 400 leap).</summary>
public sealed record LeapRule(int Every, int Offset, bool Leap);

/// <summary>
/// The leap years as a periodic pattern: the pattern repeats every <see cref="Period"/> (the least common multiple of the rules), so the number of leap years before any year is one
/// division and one table lookup, with no loop over years.
/// </summary>
public sealed class LeapRules
{
    public const int MaxPeriod = 100_000;
    public const int MaxEvery = 10_000;

    private readonly IReadOnlyList<LeapRule> _rules;
    private readonly int[] _prefix;   // _prefix[r] = leap years among residues 0 .. r-1

    public LeapRules(IReadOnlyList<LeapRule> rules)
    {
        _rules = rules;
        long period = 1;
        foreach (var rule in rules)
        {
            period = period / Gcd(period, rule.Every) * rule.Every;
            if (period > MaxPeriod) throw new EngineRefusedException(CalendarCodes.Leap, "the leap rules repeat only every " + period + " years; at most " + MaxPeriod + " is allowed");
        }
        Period = (int)period;
        _prefix = new int[Period + 1];
        for (var r = 0; r < Period; r++) _prefix[r + 1] = _prefix[r] + (IsLeapResidue(r) ? 1 : 0);
        LeapPerPeriod = _prefix[Period];
    }

    public int Period { get; }
    public int LeapPerPeriod { get; }
    public bool Any => _rules.Count > 0;

    private static long Gcd(long a, long b) => b == 0 ? a : Gcd(b, a % b);

    private bool IsLeapResidue(long year)
    {
        var leap = false;
        foreach (var rule in _rules)
            if (FloorMod(year - rule.Offset, rule.Every) == 0) leap = rule.Leap;
        return leap;
    }

    public bool IsLeap(long year) => IsLeapResidue(FloorMod(year, Period));

    /// <summary>Leap years in [0, year) for a year at or above 0, and minus the leap years in [year, 0) below it, so the difference of two results counts the leap years between two years either way.</summary>
    public long LeapsBefore(long year) => FloorDiv(year, Period) * LeapPerPeriod + _prefix[(int)FloorMod(year, Period)];

    public static long FloorMod(long a, long b) => ((a % b) + b) % b;

    public static long FloorDiv(long a, long b) => a / b - (a % b != 0 && (a < 0) != (b < 0) ? 1 : 0);
}
