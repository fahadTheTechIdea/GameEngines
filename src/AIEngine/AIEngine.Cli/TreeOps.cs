/// <summary>What is known about the six comparisons: their names (the same in the interchange and in an AI condition), their opposites and what a true or false answer says about the value.</summary>
internal static class TreeOps
{
    public static bool TryParse(string name, out TreeOp op)
    {
        switch (name)
        {
            case "lt": op = TreeOp.Lt; return true;
            case "lte": op = TreeOp.Lte; return true;
            case "gt": op = TreeOp.Gt; return true;
            case "gte": op = TreeOp.Gte; return true;
            case "eq": op = TreeOp.Eq; return true;
            case "ne": op = TreeOp.Ne; return true;
        }
        op = TreeOp.Lt;
        return false;
    }

    // Indexed by the enum's value, in declaration order (Lt, Lte, Gt, Gte, Eq, Ne); a value outside the enum fails the index, never a quiet default.
    private static readonly string[] Names = ["lt", "lte", "gt", "gte", "eq", "ne"];
    private static readonly TreeOp[] Opposites = [TreeOp.Gte, TreeOp.Gt, TreeOp.Lte, TreeOp.Lt, TreeOp.Ne, TreeOp.Eq];

    public static string Name(TreeOp op) => Names[(int)op];

    public static TreeOp Negate(TreeOp op) => Opposites[(int)op];

    /// <summary>
    /// The values inside <paramref name="range"/> for which "value op threshold" holds, as a range again; null when there are none. For a not-equal the excluded value is trimmed only when
    /// it is an end of the range; inside the range the hole is not tracked, which keeps the range a superset, so a branch is dropped only when it is truly empty.
    /// </summary>
    public static TreeSpan? Holds(TreeSpan range, TreeOp op, long threshold)
    {
        long low = range.Min, high = range.Max;
        switch (op)
        {
            case TreeOp.Lt: high = Math.Min(high, threshold - 1); break;
            case TreeOp.Lte: high = Math.Min(high, threshold); break;
            case TreeOp.Gt: low = Math.Max(low, threshold + 1); break;
            case TreeOp.Gte: low = Math.Max(low, threshold); break;
            case TreeOp.Eq:
                if (threshold < low || threshold > high) return null;
                low = high = threshold;
                break;
            case TreeOp.Ne:
                if (low == high && low == threshold) return null;
                if (threshold == low) low++;
                else if (threshold == high) high--;
                break;
        }
        return low <= high ? new TreeSpan(low, high) : null;
    }
}
