using System.Globalization;

namespace GameRuntime.Combat.Headless;

/// <summary>One scripted order, due once the host has fed <see cref="AtMicroseconds"/> of time.</summary>
public sealed record SkirmishScriptLine(long AtMicroseconds, SkirmishOrder Order, int LineNumber);

/// <summary>
/// A human's orders as text, one per line: <c>at 2.5 engage a&gt;b|200|10|open|open|mutual</c>, <c>at 1 march a|300|10|road</c>,
/// <c>at 4 round 5@a&gt;c|100|open|open;b&gt;c|100|open|open</c> or <c>at 6 resupply a|main|30</c>. Times are host seconds, not ticks; the
/// runtime decides which tick an order lands on. Blank lines and <c>#</c> comments are skipped.
/// </summary>
public sealed class SkirmishScript
{
    private readonly List<SkirmishScriptLine> _lines;
    private int _next;

    private SkirmishScript(List<SkirmishScriptLine> lines) { _lines = lines; }

    public int Count => _lines.Count;

    public static SkirmishScript Empty { get; } = new([]);

    public static SkirmishScript Parse(IEnumerable<string> text)
    {
        var lines = new List<SkirmishScriptLine>();
        var number = 0;
        foreach (var raw in text)
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 4 || words[0] != "at") throw new SkirmishUsageException($"script line {number}: expected 'at <seconds> <action> <argument>' with an action of " + string.Join(", ", SkirmishActions.All));
            if (!decimal.TryParse(words[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
                throw new SkirmishUsageException($"script line {number}: '{words[1]}' is not a time in seconds");
            var at = (long)(seconds * 1_000_000m);
            if (lines.Count > 0 && at < lines[^1].AtMicroseconds) throw new SkirmishUsageException($"script line {number}: times must not go backwards");
            lines.Add(new SkirmishScriptLine(at, ParseOrder(words[2], words[3], number), number));
        }
        return new SkirmishScript(lines);
    }

    private static SkirmishOrder ParseOrder(string action, string argument, int number)
    {
        try { return SkirmishOrder.Parse(action, argument); }
        catch (SkirmishUsageException exception) { throw new SkirmishUsageException($"script line {number}: {exception.Message}"); }
    }

    /// <summary>The lines whose time has come, in file order. Each line is returned once.</summary>
    public IEnumerable<SkirmishScriptLine> Due(long hostMicroseconds)
    {
        while (_next < _lines.Count && _lines[_next].AtMicroseconds <= hostMicroseconds) yield return _lines[_next++];
    }
}
