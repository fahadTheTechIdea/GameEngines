namespace DialogueEngine;

/// <summary>An answer that is taken when the player has not answered after <paramref name="AfterMs"/> (the host keeps the timer and calls <see cref="DialogueRuntime.Timeout"/>).</summary>
public sealed record ChoiceTimeout(int AfterMs, string Option);