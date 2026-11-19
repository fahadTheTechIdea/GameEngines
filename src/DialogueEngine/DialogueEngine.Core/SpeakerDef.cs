namespace DialogueEngine;

/// <summary>A speaker used by the document, the portrait ids it may be shown with and the emotion ids a line may give it (none when the document declares none).</summary>
public sealed record SpeakerDef(string Id, IReadOnlyList<string> Portraits, IReadOnlyList<string>? Emotions = null)
{
    public IReadOnlyList<string> Emotions { get; init; } = Emotions ?? Array.Empty<string>();
}