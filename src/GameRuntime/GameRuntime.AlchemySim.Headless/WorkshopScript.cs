using System.Globalization;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>One scripted player input, due once the host has fed <see cref="AtMicroseconds"/> of time. The action is a <see cref="WorkshopActions"/> name.</summary>
public sealed record WorkshopScriptLine(long AtMicroseconds, string Action, string Argument, int LineNumber);

/// <summary>
/// A human's inputs as text, one per line: <c>at 2.5 combine ore+crucible</c> or <c>at 6 order space.smelt.planetx|1</c>. Times are host
/// seconds, not ticks; the runtime decides which tick an input lands on. Blank lines and <c>#</c> comments are skipped.
/// </summary>
public sealed class WorkshopScript
{
    private readonly List<WorkshopScriptLine> _lines;
    private int _next;

    private WorkshopScript(List<WorkshopScriptLine> lines) { _lines = lines; }

    public int Count => _lines.Count;

    public static WorkshopScript Empty { get; } = new([]);

    public static WorkshopScript Parse(IEnumerable<string> text)
    {
        var lines = new List<WorkshopScriptLine>();
        var number = 0;
        foreach (var raw in text)
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 4 || words[0] != "at" || words[2] is not (WorkshopActions.Combine or WorkshopActions.Order))
                throw new WorkshopUsageException($"script line {number}: expected 'at <seconds> combine|order <argument>'");
            if (!decimal.TryParse(words[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
                throw new WorkshopUsageException($"script line {number}: '{words[1]}' is not a time in seconds");
            var at = (long)(seconds * 1_000_000m);
            if (lines.Count > 0 && at < lines[^1].AtMicroseconds) throw new WorkshopUsageException($"script line {number}: times must not go backwards");
            if (words[3].Split('|').Length > 2) throw new WorkshopUsageException($"script line {number}: use one '|' at most");
            lines.Add(new WorkshopScriptLine(at, words[2], words[3], number));
        }
        return new WorkshopScript(lines);
    }

    /// <summary>The lines whose time has come, in file order. Each line is returned once.</summary>
    public IEnumerable<WorkshopScriptLine> Due(long hostMicroseconds)
    {
        while (_next < _lines.Count && _lines[_next].AtMicroseconds <= hostMicroseconds) yield return _lines[_next++];
    }
}
