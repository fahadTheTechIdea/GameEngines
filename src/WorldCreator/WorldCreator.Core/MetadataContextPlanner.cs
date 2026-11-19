using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataContextBinding(string NodeId,string RegionId,IReadOnlyList<string>? Tags=null);
public sealed record MetadataContextDefinition(IReadOnlyList<CatalogContextRegion> Regions,
    IReadOnlyList<MetadataContextBinding> Sites,IReadOnlyList<SitingSeparationRule> Separations,long MaximumWork=2000000,IReadOnlyList<CatalogContextExclusion>? Exclusions=null,
    IReadOnlyList<MetadataContextProtection>? Protections=null);
public sealed record MetadataContextSource(string NodeId,string RegionId,string? PolicyId,IReadOnlyList<string> Tags);
public sealed record MetadataContextPlan(MetadataContextDefinition Definition,IReadOnlyList<MetadataContextSource> Sources,
    IReadOnlyList<CatalogContextEvaluation> Evaluations,long Work,IReadOnlyList<MetadataResolvedProtection>? Protections=null);
internal sealed record MetadataContextGeometry(string NodeId,WorldPolygon Footprint,IReadOnlyList<WorldPolygon> Surroundings);

/// <summary>Adapts exact generic allocations and authenticated catalog facts to shared context evaluation.</summary>
public sealed class MetadataContextPlanner(ISettlementCatalogRegistry? catalogs=null)
{
    public MetadataContextPlan Plan(MetadataContextDefinition definition,IReadOnlyList<WorldMetadataNode> nodes,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,MetadataPlacementPlan placement,MetadataSpace space,CancellationToken token=default)
    {
        return Plan(definition,nodes,structures,Geometry(placement),space,null,token);
    }
    internal static IReadOnlyList<MetadataContextGeometry> Geometry(MetadataPlacementPlan placement)
    {
        var stages=new[]{(placement.Definition.Bindings,placement.Placement)}.Concat(placement.NestedStages.Select(s=>
            (placement.Definition.NestedStages!.Single(d=>d.Id==s.Id).Bindings,s.Placement)));
        var geometry=stages.SelectMany(stage=>stage.Bindings.Select(binding=>{
            var site=stage.Placement.Sites.Single(s=>s.DemandId==binding.DemandId);
            return new MetadataContextGeometry(binding.NodeId,site.Footprint.Footprint,
                (site.Reservation is {} reservation?new[]{reservation.Footprint}:[])
                    .Concat((placement.SiteFeatures??[]).Where(f=>f.NodeId==binding.NodeId).Select(f=>f.Boundary)).ToArray());
        })).ToArray();
        return geometry;
    }

    internal MetadataContextPlan Plan(MetadataContextDefinition definition,IReadOnlyList<WorldMetadataNode> nodes,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,IReadOnlyList<MetadataContextGeometry> geometry,
        MetadataSpace space,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(space.Unit!="meters"||space.Axes.Count!=2)throw new ArgumentException("METADATA_CONTEXT_SPACE_INVALID");
        if(definition?.Regions is not {Count:>0 and <=512}||definition.Sites is not {Count:>0 and <=4096}||definition.Separations is not {Count:<=256}||definition.MaximumWork is <1 or >200000000||definition.Exclusions is {Count:>256})
            throw new ArgumentException("METADATA_CONTEXT_DEFINITION_INVALID");
        definition=JsonSerializer.Deserialize<MetadataContextDefinition>(JsonSerializer.Serialize(definition,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parent);
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var shapes=geometry.ToDictionary(g=>g.NodeId,StringComparer.Ordinal);
        var sites=new List<CatalogContextSite>();var sources=new List<MetadataContextSource>();var ids=new HashSet<string>(StringComparer.Ordinal);
        var policies=new Dictionary<string,ISettlementCatalogPolicy>(StringComparer.Ordinal);
        foreach(var binding in definition.Sites.OrderBy(b=>b?.NodeId,StringComparer.Ordinal))
        {
            budget.Charge();
            if(binding is null||binding.NodeId is null||!ids.Add(binding.NodeId)||!lookup.TryGetValue(binding.NodeId,out var node)||!shapes.TryGetValue(binding.NodeId,out var shape))
                throw new ArgumentException("METADATA_CONTEXT_BINDING_INVALID");
            IReadOnlySet<string>? allowed=null;IReadOnlySet<string> tags;
            if(node.StructureKey is {} key)
            {
                if(binding.Tags is not null)throw new ArgumentException("METADATA_CONTEXT_REGISTERED_TAG_OVERRIDE");
                if(!structures.TryGetValue(key,out var source))throw new ArgumentException("METADATA_CONTEXT_SOURCE_MISSING");
                policies.Add(node.Id,MetadataRegisteredStructure.Resolve(source,catalogs,"METADATA_CONTEXT_POLICY"));
                allowed=source.Definition.SitingProfile!.AllowedDistricts;tags=source.Definition.SitingProfile.Tags;
            }
            else
            {
                if(binding.Tags is not {Count:<=64}||binding.Tags.Any(t=>!SettlementCatalogRegistry.ValidId(t))||binding.Tags.Distinct(StringComparer.Ordinal).Count()!=binding.Tags.Count)
                    throw new ArgumentException("METADATA_CONTEXT_GENERIC_TAGS_REQUIRED");
                tags=binding.Tags.ToHashSet(StringComparer.Ordinal);
            }
            sites.Add(new(node.Id,binding.RegionId,shape.Footprint,shape.Surroundings,tags,allowed));
            sources.Add(new(node.Id,binding.RegionId,policies.GetValueOrDefault(node.Id)?.Id,tags.Order(StringComparer.Ordinal).ToArray()));
        }
        if(!ids.SetEquals(shapes.Keys)||nodes.Any(n=>n.StructureKey is not null&&!ids.Contains(n.Id)))throw new ArgumentException("METADATA_CONTEXT_SITE_COVERAGE");
        var protections=MetadataContextProtections.Resolve(definition,nodes,budget,token);
        var exclusions=protections is null?definition.Exclusions:(definition.Exclusions??[]).Concat(protections.Select(MetadataContextProtections.Exclusion)).ToArray();
        var evaluations=new CatalogContextPolicy().Evaluate(definition.Regions,sites,definition.Separations,policies,budget,token,exclusions);
        var failures=evaluations.Where(e=>!e.Passed).ToArray();
        if(failures.FirstOrDefault(e=>!e.Spatial) is {} invalid)throw new ArgumentException("METADATA_CONTEXT:"+invalid.RuleId+":"+invalid.SiteId+":"+invalid.RelatedId);
        if(failures.FirstOrDefault() is {} spatial)throw new MetadataSpatialConstraintException("METADATA_CONTEXT:"+spatial.RuleId+":"+spatial.SiteId+":"+spatial.RelatedId);
        return new(definition,sources.AsReadOnly(),evaluations,budget.Used,protections);
    }

    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        if(document.ContextPlan is not {} plan)return;
        var expected=Plan(plan.Definition,document.Nodes,document.StructureDefinitions,
            document.PlacementPlan??throw new ArgumentException("METADATA_CONTEXT_PLACEMENT_REQUIRED"),document.Space,token);
        if(!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected,WorldJson.CreateOptions()),JsonSerializer.SerializeToElement(plan,WorldJson.CreateOptions())))
            throw new ArgumentException("METADATA_CONTEXT_PLAN_MISMATCH");
    }
}
