using System.Globalization;

namespace GameRuntime.Alchemy.Headless;

/// <summary>One scripted player input, due once the host has fed <see cref="AtMicroseconds"/> of time. The argument is items or items|environment.</summary>
public sealed record AlchemyScriptLine(long AtMicroseconds, string Argument, int LineNumber);

/// <summary>
/// A human's inputs as text, one per line: <c>at 2.5 combine fire+water</c> or <c>at 6 combine stone+fire|heat</c>. Times are host seconds,
/// not ticks; the runtime decides which tick an input lands on. Blank lines and <c>#</c> comments are skipped.
/// </summary>
public sealed class AlchemyScript
{
    private readonly List<AlchemyScriptLine> _lines;
    private int _next;

    private AlchemyScript(List<AlchemyScriptLine> lines) { _lines = lines; }

    public int Count => _lines.Count;

    public static AlchemyScript Empty { get; } = new([]);

    public static AlchemyScript Parse(IEnumerable<string> text)
    {
        var lines = new List<AlchemyScriptLine>();
        var number = 0;
        foreach (var raw in text)
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 4 || words[0] != "at" || words[2] != AlchemyActions.Combine)
                throw new AlchemyUsageException($"script line {number}: expected 'at <seconds> combine <items>[|<environment>]'");
            if (!decimal.TryParse(words[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
                throw new AlchemyUsageException($"script line {number}: '{words[1]}' is not a time in seconds");
            var at = (long)(seconds * 1_000_000m);
            if (lines.Count > 0 && at < lines[^1].AtMicroseconds) throw new AlchemyUsageException($"script line {number}: times must not go backwards");
            if (words[3].Split('|').Length > 2) throw new AlchemyUsageException($"script line {number}: use items or items|environment");
            lines.Add(new AlchemyScriptLine(at, words[3], number));
        }
        return new AlchemyScript(lines);
    }

    /// <summary>The lines whose time has come, in file order. Each line is returned once.</summary>
    public IEnumerable<AlchemyScriptLine> Due(long hostMicroseconds)
    {
        while (_next < _lines.Count && _lines[_next].AtMicroseconds <= hostMicroseconds) yield return _lines[_next++];
    }
}
