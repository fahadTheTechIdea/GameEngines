using System.Globalization;
using System.Reflection;
using EngineCommon;

namespace GameRuntime;

/// <summary>
/// Every event of the kinds it was told to listen for, as text, in the order the host received them, and the SHA-256 of that text. The same session gives the same hash on every run, so a run, a resumed run
/// and a replay can be compared by one value. A line is <c>EventName{Property=value,...}</c> with the properties in ordinal order. An engine's transcript is this class listening to that engine's events.
/// </summary>
public class EventTranscript
{
    private readonly List<string> _lines = new();

    protected EventTranscript(RuntimeSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    protected RuntimeSession Session { get; }

    public IReadOnlyList<string> Lines => _lines;

    public string Hash => Digest.Of(string.Join('\n', _lines));

    /// <summary>Starts recording events of type <typeparamref name="T"/>.</summary>
    protected void Listen<T>() where T : struct => Session.Subscribe<T>(Record);

    private void Record<T>(T evt) where T : struct
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal);
        _lines.Add(typeof(T).Name + "{" + string.Join(',', properties.Select(p => p.Name + "=" + Convert.ToString(p.GetValue(evt), CultureInfo.InvariantCulture))) + "}");
    }
}
