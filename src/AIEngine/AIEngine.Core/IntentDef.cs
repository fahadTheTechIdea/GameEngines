namespace AIEngine;

/// <summary>A request an agent can make of the host: a name and typed arguments. The engine only announces it; the host decides what it means.</summary>
public sealed class IntentDef(string id, IReadOnlyList<ValueDef> args)
{
    public string Id { get; } = id;
    public IReadOnlyList<ValueDef> Args { get; } = args;

    private readonly Dictionary<string, ValueDef> _byId = args.ToDictionary(a => a.Id, StringComparer.Ordinal);

    public bool TryArg(string id, out ValueDef arg) => _byId.TryGetValue(id, out arg!);
}