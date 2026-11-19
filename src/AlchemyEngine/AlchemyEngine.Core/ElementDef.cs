namespace AlchemyEngine;

/// <summary>A substance. <see cref="Starting"/> elements are known before anything is combined; all others are discovered.</summary>
public sealed record ElementDef(string Id, string Name, IReadOnlyList<string> Properties, bool Starting);
