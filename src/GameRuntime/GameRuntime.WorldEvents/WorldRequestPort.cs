namespace GameRuntime;

/// <summary>What a request asks for. One list, so a component that serves requests, the journal that records them and a script that makes them cannot disagree about which requests exist.</summary>
public enum WorldRequestKind { Intervene, Storyteller, Difficulty, Hazard, Outbreak, Card, Weather, Disease }

/// <summary>The names of the request kinds on the wire and in the journal: one place to turn a kind into its name and a name into its kind.</summary>
public static class WorldRequestNames
{
    public static string NameOf(WorldRequestKind kind)
    {
        switch (kind)
        {
            case WorldRequestKind.Intervene: return "intervene";
            case WorldRequestKind.Storyteller: return "storyteller";
            case WorldRequestKind.Difficulty: return "difficulty";
            case WorldRequestKind.Hazard: return "hazard";
            case WorldRequestKind.Outbreak: return "outbreak";
            case WorldRequestKind.Card: return "card";
            case WorldRequestKind.Weather: return "weather";
            case WorldRequestKind.Disease: return "disease";
        }
        throw new InvalidOperationException("unknown request kind " + kind);
    }

    /// <summary>The kind a name stands for, or null when the name is none of them (the component answers that with a refusal, it does not drop it).</summary>
    public static WorldRequestKind? KindOf(string name) => Enum.GetValues<WorldRequestKind>().Cast<WorldRequestKind?>().FirstOrDefault(k => NameOf(k!.Value) == name);
}

/// <summary>
/// One queued request: the kind as the sender wrote it (it may be none the component knows), the id, three names and a number. Intervene: region, intervention, number 1 for on and 0 for off. Storyteller and Difficulty: the
/// id in A. Hazard: region, hazard, severity. Outbreak: region, disease, cases in the number. Card: card, region. Weather: region. Disease: region, disease.
/// </summary>
internal readonly record struct WorldPending(string Kind, string RequestId, string A, string B, string C, long Number);

/// <summary>The one place the world request event is connected: the handler gets the request channel and the request as a <see cref="WorldPending"/>.</summary>
internal static class WorldRequestPort
{
    public static void Connect(IEventPort events, Action<string, WorldPending> handler) =>
        events.Connect<WorldRequested>(r => handler(r.Channel, new WorldPending(r.Kind, r.RequestId, r.A, r.B, r.C, r.Number)));
}
