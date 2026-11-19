using System.Collections.Frozen;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

public sealed record CatalogSupplySource(string Id,string Kind,string Unit,string Origin,IReadOnlySet<string> Qualities,
    WorldPoint? ServicePoint,string? ProviderId,IReadOnlyList<string> UpstreamSourceIds);
public sealed record CatalogSupplyContext(string Technology,IReadOnlyDictionary<string,CatalogSupplySource> Sources,
    IReadOnlyDictionary<string,string> RequirementSources);
public sealed record CatalogSupplyIssue(string Code,string Message,bool Spatial=false);

/// <summary>Shared validated facts for catalog-owned supply policies, independent of art and renderers.</summary>
public sealed class CatalogSupplyPolicy
{
    private readonly IReadOnlyDictionary<string,CatalogSupplySource> sources;
    public CatalogSupplyPolicy(IEnumerable<CatalogSupplySource> input,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var saved=new Dictionary<string,CatalogSupplySource>(StringComparer.Ordinal);
        foreach(var source in input)
        {
            token.ThrowIfCancellationRequested();
            if(saved.Count>=50000||source is null||string.IsNullOrWhiteSpace(source.Id)||source.Id.Length>4096||
                string.IsNullOrWhiteSpace(source.Kind)||string.IsNullOrWhiteSpace(source.Unit)||string.IsNullOrWhiteSpace(source.Origin)||
                source.Qualities is null||source.Qualities.Count>64||source.Qualities.Any(string.IsNullOrWhiteSpace)||
                source.UpstreamSourceIds is null||source.UpstreamSourceIds.Count>64||source.UpstreamSourceIds.Any(string.IsNullOrWhiteSpace)||
                source.UpstreamSourceIds.Distinct(StringComparer.Ordinal).Count()!=source.UpstreamSourceIds.Count||
                source.ProviderId is {} provider&&(string.IsNullOrWhiteSpace(provider)||provider.Length>2048)||
                source.ServicePoint is {} p&&(!double.IsFinite(p.X)||!double.IsFinite(p.Y)))throw new ArgumentException("CATALOG_SUPPLY_SOURCE_INVALID");
            if(!saved.TryAdd(source.Id,source with{Qualities=source.Qualities.ToFrozenSet(StringComparer.Ordinal),
                UpstreamSourceIds=Array.AsReadOnly(source.UpstreamSourceIds.Order(StringComparer.Ordinal).ToArray())}))throw new ArgumentException("CATALOG_SUPPLY_SOURCE_DUPLICATE");
        }
        var active=new HashSet<string>(StringComparer.Ordinal);var done=new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            token.ThrowIfCancellationRequested();
            if(!saved.TryGetValue(id,out var source))throw new ArgumentException("CATALOG_SUPPLY_UPSTREAM_MISSING");
            if(done.Contains(id))return;
            if(!active.Add(id))throw new ArgumentException("CATALOG_SUPPLY_UPSTREAM_CYCLE");
            if(active.Count>256)throw new ArgumentException("CATALOG_SUPPLY_UPSTREAM_DEPTH");
            foreach(string upstream in source.UpstreamSourceIds)Visit(upstream);
            active.Remove(id);done.Add(id);
        }
        foreach(string id in saved.Keys.Order(StringComparer.Ordinal))Visit(id);
        sources=saved.ToFrozenDictionary(StringComparer.Ordinal);
    }
    public IReadOnlyList<CatalogSupplyIssue> Evaluate(ISettlementCatalogPolicy policy,BuildingDefinition definition,
        StructureResourceRequirement requirement,string sourceId,string technology,IReadOnlyDictionary<string,string> bindings,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(!sources.TryGetValue(sourceId,out var source)||bindings.Count>256||bindings.Any(b=>string.IsNullOrWhiteSpace(b.Key)||!sources.ContainsKey(b.Value)))
            throw new ArgumentException("CATALOG_SUPPLY_BINDING_INVALID");
        var issues=policy.ValidateSupply(definition,requirement,source,new(technology,sources,bindings.ToFrozenDictionary(StringComparer.Ordinal)),token);
        token.ThrowIfCancellationRequested();
        if(issues is null||issues.Count>256||issues.Any(i=>i is null||string.IsNullOrWhiteSpace(i.Code)||string.IsNullOrWhiteSpace(i.Message)))
            throw new ArgumentException("CATALOG_SUPPLY_POLICY_RESULT_INVALID");
        return Array.AsReadOnly(issues.ToArray());
    }
}
