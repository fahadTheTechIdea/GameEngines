using System.Globalization;

namespace GameRuntime.Headless;

public enum ScriptAction { Sell, AutoSell, Pause, Resume, Speed }

/// <summary>One scripted player input, due once the host has fed <see cref="AtMicroseconds"/> of time.</summary>
public sealed record ScriptLine(long AtMicroseconds, ScriptAction Action, int Units, int SpeedNumerator, int SpeedDenominator, int LineNumber);

/// <summary>
/// A human's inputs as text, one per line: <c>at 2.5 sell 3</c>, <c>at 3 autosell 2</c>, <c>at 5 autosell off</c>, <c>at 7 speed 2/1</c>, <c>at 8 pause</c>, <c>at 9 resume</c>.
/// Times are host seconds, not ticks; the runtime decides which tick an input lands on. Blank lines and <c>#</c> comments are skipped.
/// </summary>
public sealed class InputScript
{
    private readonly List<ScriptLine> _lines;
    private int _next;

    private InputScript(List<ScriptLine> lines) { _lines = lines; }

    public int Count => _lines.Count;

    public static InputScript Empty { get; } = new([]);

    public static InputScript Parse(IEnumerable<string> text)
    {
        var lines = new List<ScriptLine>();
        var number = 0;
        foreach (var raw in text)
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 3 || words[0] != "at") throw new UsageException($"script line {number}: expected 'at <seconds> <action>'");
            if (!decimal.TryParse(words[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
                throw new UsageException($"script line {number}: '{words[1]}' is not a time in seconds");
            var at = (long)(seconds * 1_000_000m);
            if (lines.Count > 0 && at < lines[^1].AtMicroseconds) throw new UsageException($"script line {number}: times must not go backwards");
            lines.Add(words[2] switch
            {
                "sell" => new ScriptLine(at, ScriptAction.Sell, Units(words, number), 1, 1, number),
                "autosell" => AutoSell(words, number, at),
                "pause" => NoArguments(words, number, new ScriptLine(at, ScriptAction.Pause, 0, 1, 1, number)),
                "resume" => NoArguments(words, number, new ScriptLine(at, ScriptAction.Resume, 0, 1, 1, number)),
                "speed" => Speed(words, number, at),
                _ => throw new UsageException($"script line {number}: unknown action '{words[2]}'"),
            });
        }
        return new InputScript(lines);
    }

    /// <summary>The lines whose time has come, in file order. Each line is returned once.</summary>
    public IEnumerable<ScriptLine> Due(long hostMicroseconds)
    {
        while (_next < _lines.Count && _lines[_next].AtMicroseconds <= hostMicroseconds) yield return _lines[_next++];
    }

    private static int Units(string[] words, int number) =>
        words.Length == 4 && int.TryParse(words[3], NumberStyles.None, CultureInfo.InvariantCulture, out var units) && units is >= 1 and <= 10
            ? units : throw new UsageException($"script line {number}: sell needs a whole number of units in 1..10");

    /// <summary><c>autosell &lt;units&gt;</c> holds the key down (a sale on every tick); <c>autosell off</c> lets it go.</summary>
    private static ScriptLine AutoSell(string[] words, int number, long at) =>
        words.Length == 4 && words[3] == "off"
            ? new ScriptLine(at, ScriptAction.AutoSell, 0, 1, 1, number)
            : new ScriptLine(at, ScriptAction.AutoSell, Units(words, number), 1, 1, number);

    private static ScriptLine NoArguments(string[] words, int number, ScriptLine line) =>
        words.Length == 3 ? line : throw new UsageException($"script line {number}: {words[2]} takes no arguments");

    private static ScriptLine Speed(string[] words, int number, long at)
    {
        var parts = words.Length == 4 ? words[3].Split('/') : [];
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerator)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator)
            && numerator is >= 1 and <= 64 && denominator is >= 1 and <= 64)
            return new ScriptLine(at, ScriptAction.Speed, 0, numerator, denominator, number);
        throw new UsageException($"script line {number}: speed needs <n>/<d> with both in 1..64");
    }
}
