using System.Text.Json.Serialization;
using System.Text.Json;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

/// <summary>Semantic dependencies, not physical floors, render order or evidence of stage execution.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataLayerDefinition(string Id, string Kind, string Purpose, string SourceRuleId,
    IReadOnlyList<string> DependsOn, MetadataCoverage Coverage, IReadOnlyList<string> RemainingScopes);
public sealed record MetadataLayerEntry(MetadataLayerDefinition Definition,
    IReadOnlyList<string> NodeIds, IReadOnlyList<string> ConnectionIds);
public sealed record MetadataLayerPlan(IReadOnlyList<MetadataLayerEntry> Layers,
    IReadOnlyList<string> UnassignedNodeIds, IReadOnlyList<string> UnassignedConnectionIds);

/// <summary>Single owner for validating and ordering semantic layers and resolving content membership.
/// It does not generate terrain, infer layer kinds, schedule algorithms or claim geometric coverage.</summary>
public sealed class MetadataLayerPlanner
{
    public void ValidateDocument(WorldMetadataDocument document, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (document is null || document.SchemaVersion != 2 || document.LayerPlan is null ||
            document.LayerPlan.Layers is null || document.LayerPlan.Layers.Count > 256 ||
            document.LayerPlan.Layers.Any(l => l is null))
            throw new ArgumentException("METADATA_LAYER_DOCUMENT_INVALID");
        var rebuilt = Plan(document.LayerPlan.Layers.Select(l => l.Definition).ToArray(), document.Nodes, document.Connections, token);
        if (JsonSerializer.Serialize(rebuilt, WorldJson.CreateOptions()) != JsonSerializer.Serialize(document.LayerPlan, WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_LAYER_PLAN_MISMATCH");
    }

    /// <summary>Validate all authored memberships, including zero-count or unused catalog parts.</summary>
    public void ValidateMemberships(IReadOnlyList<MetadataLayerDefinition> definitions,
        IEnumerable<string?> memberships, CancellationToken token = default)
    {
        var plan = Plan(definitions, [], [], token);
        var ids = plan.Layers.Select(l => l.Definition.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in memberships)
        {
            token.ThrowIfCancellationRequested();
            if (id is not null && (!Id(id) || !ids.Contains(id)))
                throw new ArgumentException("METADATA_LAYER_MEMBERSHIP_UNKNOWN:" + id);
        }
    }

    public MetadataLayerPlan Plan(IReadOnlyList<MetadataLayerDefinition> definitions,
        IReadOnlyList<WorldMetadataNode> nodes, IReadOnlyList<WorldMetadataConnection> connections,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (definitions is null || definitions.Count > 256 || nodes is null || nodes.Count > 100000 ||
            connections is null || connections.Count > 500000)
            throw new ArgumentException("METADATA_LAYER_BUDGET_OR_INPUT_INVALID");

        var byId = new Dictionary<string, MetadataLayerDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            token.ThrowIfCancellationRequested();
            if (definition is null || !Id(definition.Id) || !Id(definition.Kind) ||
                !Text(definition.Purpose) || !Text(definition.SourceRuleId) ||
                definition.DependsOn is null || definition.DependsOn.Count > 256 ||
                definition.DependsOn.Any(d => !Id(d)) ||
                definition.DependsOn.Distinct(StringComparer.Ordinal).Count() != definition.DependsOn.Count ||
                !Enum.IsDefined(definition.Coverage) || definition.RemainingScopes is null ||
                definition.RemainingScopes.Count > 512 || definition.RemainingScopes.Any(s => !Text(s)) ||
                (definition.Coverage == MetadataCoverage.PartialScope ? definition.RemainingScopes.Count == 0 : definition.RemainingScopes.Count != 0))
                throw new ArgumentException("METADATA_LAYER_DEFINITION_INVALID");
            var snapshot = definition with {
                DependsOn = Array.AsReadOnly(definition.DependsOn.Order(StringComparer.Ordinal).ToArray()),
                RemainingScopes = Array.AsReadOnly(definition.RemainingScopes.ToArray())
            };
            if (!byId.TryAdd(definition.Id, snapshot)) throw new ArgumentException("METADATA_LAYER_DUPLICATE:" + definition.Id);
        }

        var dependents = byId.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        var pending = byId.ToDictionary(p => p.Key, p => p.Value.DependsOn.Count, StringComparer.Ordinal);
        foreach (var definition in byId.Values)
            foreach (var dependency in definition.DependsOn)
            {
                token.ThrowIfCancellationRequested();
                if (!byId.ContainsKey(dependency)) throw new ArgumentException("METADATA_LAYER_DEPENDENCY_UNKNOWN:" + dependency);
                dependents[dependency].Add(definition.Id);
            }
        var ready = new SortedSet<string>(pending.Where(p => p.Value == 0).Select(p => p.Key), StringComparer.Ordinal);
        var ordered = new List<string>();
        while (ready.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            string id = ready.Min!;
            ready.Remove(id); ordered.Add(id);
            foreach (var dependent in dependents[id]) if (--pending[dependent] == 0) ready.Add(dependent);
        }
        if (ordered.Count != definitions.Count) throw new ArgumentException("METADATA_LAYER_DEPENDENCY_CYCLE");

        var nodeMembers = Members(nodes.Select(n => n is null ? (null, null) : (n.Id, n.LayerId)));
        var connectionMembers = Members(connections.Select(c => c is null ? (null, null) : (c.Id, c.LayerId)));
        return new(Array.AsReadOnly(ordered.Select(id => new MetadataLayerEntry(byId[id],
            Freeze(nodeMembers[id]), Freeze(connectionMembers[id]))).ToArray()),
            Freeze(nodeMembers[string.Empty]), Freeze(connectionMembers[string.Empty]));

        Dictionary<string, List<string>> Members(IEnumerable<(string? Id, string? Layer)> members)
        {
            var result = byId.Keys.Append(string.Empty).ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, layer) in members)
            {
                token.ThrowIfCancellationRequested();
                if (!Text(id) || !seen.Add(id!)) throw new ArgumentException("METADATA_LAYER_MEMBER_INVALID");
                if (layer is not null && (!Id(layer) || !byId.ContainsKey(layer)))
                    throw new ArgumentException("METADATA_LAYER_MEMBERSHIP_UNKNOWN:" + layer);
                result[layer ?? string.Empty].Add(id!);
            }
            return result;
        }
    }

    private static IReadOnlyList<string> Freeze(List<string> values) => Array.AsReadOnly(values.Order(StringComparer.Ordinal).ToArray());
    private static bool Id(string? id) => SettlementCatalogRegistry.ValidId(id) && !id!.Contains('/');
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048;
}
