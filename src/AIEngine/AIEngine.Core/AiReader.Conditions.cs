using System.Text.Json;
using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed partial class AiReader
{
    /// <summary>The facts a condition may name here: every key as <c>key.id</c>, the tick as <c>tick</c> and, inside a loop or query, the item as <c>item.id</c>, <c>item.owner</c>, <c>item.index</c> (in a loop body) and <c>item.attribute</c>.</summary>
    private Dictionary<string, FactDefinition> FactsIn(ReadScope scope)
    {
        var facts = new Dictionary<string, FactDefinition>(StringComparer.Ordinal) { ["tick"] = new FactDefinition(FactKind.Integer, 0, AiRuntime.TickLimit) };
        foreach (var key in _keys) facts["key." + key.Id] = key.Value.Fact;
        if (scope.Empty) return facts;
        var frame = scope.Innermost;
        facts["item.id"] = new FactDefinition(FactKind.String);
        facts["item.owner"] = new FactDefinition(FactKind.String);
        if (frame.HasIndex) facts["item.index"] = new FactDefinition(FactKind.Integer, 0, frame.Limit - 1);
        foreach (var attr in frame.Kind.Attrs) facts["item." + attr.Id] = attr.Fact;
        return facts;
    }

    private Cond ReadCond(JsonElement element, string where, ReadScope scope)
    {
        var facts = FactsIn(scope);
        var condition = _conditions.Read(element, where, facts);
        var refs = new List<FactRef>();
        foreach (var id in EngineCommon.Conditions.FactsOf(condition))
            refs.Add(id switch
            {
                "tick" => new FactRef(id, FactSource.Tick, 0, FactKind.Integer),
                "item.id" => new FactRef(id, FactSource.ItemId, 0, FactKind.String),
                "item.owner" => new FactRef(id, FactSource.ItemOwner, 0, FactKind.String),
                "item.index" => new FactRef(id, FactSource.ItemIndex, 0, FactKind.Integer),
                _ when id.StartsWith("key.", StringComparison.Ordinal) => new FactRef(id, FactSource.Key, _keyById[id[4..]].Index, _keyById[id[4..]].Value.Kind),
                _ => AttributeRef(id, scope),
            });
        return new Cond(condition, refs);
    }

    private static FactRef AttributeRef(string id, ReadScope scope)
    {
        scope.Innermost.Kind.TryAttr(id["item.".Length..], out var attr);
        return new FactRef(id, FactSource.ItemAttr, attr.Index, attr.Kind);
    }
}