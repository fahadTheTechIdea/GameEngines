namespace DialogueEngine;

/// <summary>
/// One answer of a choice. It may be hidden or shown disabled when its condition is false; it may carry a weight (for a seeded choice) and effects. When its tags name content warnings, a blocked
/// <see cref="WarningAction.Skip"/> hides it and a blocked <see cref="WarningAction.Replace"/> shows <see cref="Alternative"/> instead of its own key.
/// </summary>
public sealed record OptionDef(string Id, string Key, Guard? When, bool ShowWhenUnavailable, int Weight, IReadOnlyList<Effect> Effects, string Next, IReadOnlyList<string> Tags,
    string? Alternative = null, IReadOnlyList<ContentWarningDef>? Warnings = null)
{
    public IReadOnlyList<ContentWarningDef> Warnings { get; init; } = Warnings ?? Array.Empty<ContentWarningDef>();
}
