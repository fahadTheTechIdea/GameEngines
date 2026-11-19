namespace GameRuntime;

/// <summary>The one journal codec for hosts whose only input is an <see cref="InputState"/>: it names and decodes <see cref="InputEdgeRaised"/>.</summary>
public static class InputEdgeJournal
{
    public const string TypeName = "input-edge";

    public static JournalCodec CreateCodec() => new JournalCodec().Register<InputEdgeRaised>(TypeName, e =>
        new InputEdgeRaised(e.GetProperty("Action").GetString()!, e.GetProperty("Argument").GetString()!, e.GetProperty("Pressed").GetBoolean()));
}
