using Beep.SimEngine.Hosting;

namespace GameRuntime;

/// <summary>
/// A change probe owned by the Sim adapter: reads one value from the sim after each tick and yields an observation only
/// when the value changed. It is the tier-3 event source for values the sim does not announce itself.
/// </summary>
public sealed class SimProbe
{
    public SimProbe(string kind, Func<SimGame, long> read, Func<long, long, string> payload)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Read = read ?? throw new ArgumentNullException(nameof(read));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }
    public string Kind { get; }
    internal Func<SimGame, long> Read { get; }
    /// <summary>Builds the flat JSON payload from (previous, current).</summary>
    internal Func<long, long, string> Payload { get; }
    internal long Last { get; set; }
}
