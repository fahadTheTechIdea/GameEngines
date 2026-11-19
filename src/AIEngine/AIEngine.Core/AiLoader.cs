namespace AIEngine;

/// <summary>Reads an AI document, strictly: every field present, none unknown, every reference resolved, every expression typed and every assignment proven to fit, or a refusal with a code.</summary>
public static class AiLoader
{
    public static AiSet Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return new AiReader(json).Read();
    }
}