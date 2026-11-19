using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>One explicit source-world habitat funds one catalog part at one parent. Amount is density-weighted source area per member.</summary>
public sealed record MetadataTerrainDemandRule(string Id,string Purpose,string SourceRuleId,string ParentNodeId,string PartId,
    string RegionId,double AmountPerMember,string? DensityFieldId=null,string? AllocationId=null,int MaximumCount=1024,int MaximumCells=4096);
public sealed record MetadataTerrainDemandDefinition(IReadOnlyList<MetadataTerrainDemandRule> Rules,long MaximumWork=5000000);
public sealed record MetadataTerrainDemandResult(string RuleId,WorldAreaPopulationMeasure Measure,int Count);
public sealed record MetadataTerrainDemandPlan(MetadataTerrainDemandDefinition Definition,string SourceRecipeSha256,string SourceCatalogSha256,
    IReadOnlyList<MetadataTerrainDemandResult> Results,long Work);
public sealed record MetadataTerrainDemandSource(string RecipeSha256,string CatalogSha256,WorldSurfaceDefinition Surface,
    WorldEnvironmentDefinition Environment,WorldRegionPlan Regions,WorldLocalLayoutPlan? LocalLayouts);

/// <summary>Owns terrain-derived catalog quantities. Physical placement still qualifies every source-sized required member.</summary>
public sealed class MetadataTerrainDemandPlanner
{
    public MetadataTerrainDemandPlan Plan(MetadataTerrainDemandDefinition definition,WorldMetadataDocument source,CancellationToken token=default)
        =>Plan(definition,source,null,token);
    internal MetadataTerrainDemandPlan Plan(MetadataTerrainDemandDefinition definition,WorldMetadataDocument source,WorldSpatialWorkBudget? parent,CancellationToken token)
    {token.ThrowIfCancellationRequested();return Plan(definition,Source(source),parent,token);}
    internal static MetadataTerrainDemandSource Source(WorldMetadataDocument source)
    {
        if(source?.Surface is null||source.EnvironmentPlan is null||source.RegionPlan is null||source.PlacementPlan?.Definition.SourceWorld is not null)
            throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        return new(source.RecipeSha256,source.CatalogSha256,source.Surface,source.EnvironmentPlan.Definition,source.RegionPlan,source.LocalLayoutPlan);
    }
    // Physical placement nogoods may change the source recipe hash without
    // changing the measured habitat. Retain both recipe identities separately.
    internal static string HabitatSha256(MetadataTerrainDemandSource source)=>MetadataExecutionCache.Hash(new{
        source.CatalogSha256,source.Surface,source.Environment,source.Regions,source.LocalLayouts});
    internal static bool MatchesAnalysis(MetadataProgramTerrainAnalysis? analysis,WorldMetadataDocument world)
    {
        if(analysis is null)return world.TerrainDemandPlan is null;
        if(world.TerrainDemandPlan is not {} actual||world.PlacementPlan?.Definition.SourceWorld is not {} source)return false;
        return analysis.HabitatSha256==HabitatSha256(analysis.Source)&&analysis.HabitatSha256==HabitatSha256(Source(source))&&
            analysis.Source.RecipeSha256==analysis.Plan.SourceRecipeSha256&&analysis.Source.CatalogSha256==analysis.Plan.SourceCatalogSha256&&
            actual.SourceRecipeSha256==source.RecipeSha256&&MetadataPopulationSources.Equal(analysis.Plan with{SourceRecipeSha256=actual.SourceRecipeSha256},actual);
    }
    internal MetadataTerrainDemandPlan Plan(MetadataTerrainDemandDefinition definition,MetadataTerrainDemandSource source,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(definition?.Rules is null||definition.Rules.Count is <1 or >32||definition.MaximumWork is <1 or >200000000)
            throw new ArgumentException("METADATA_TERRAIN_DEMAND_INVALID");
        if(source?.Surface is null||source.Environment is null||source.Regions is null)
            throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        var seen=new HashSet<string>(StringComparer.Ordinal);var bindings=new HashSet<(string,string)>();
        foreach(var rule in definition.Rules)
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!seen.Add(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
                !WorldEnvironmentQuery.Text(rule.ParentNodeId)||!WorldEnvironmentQuery.Id(rule.PartId)||!bindings.Add((rule.ParentNodeId,rule.PartId))||!WorldEnvironmentQuery.Id(rule.RegionId)||
                !double.IsFinite(rule.AmountPerMember)||rule.AmountPerMember<=0||rule.MaximumCount is <1 or >1024||rule.MaximumCells is <1 or >16384||
                rule.DensityFieldId is {} field&&!WorldEnvironmentQuery.Id(field)||rule.AllocationId is {} allocation&&!WorldEnvironmentQuery.Id(allocation))
                throw new ArgumentException("METADATA_TERRAIN_DEMAND_RULE_INVALID");
        definition=JsonSerializer.Deserialize<MetadataTerrainDemandDefinition>(JsonSerializer.Serialize(definition,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        definition=definition with{Rules=definition.Rules.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()};
        var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(source.Surface,token),source.Environment,token);
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parent);var results=new List<MetadataTerrainDemandResult>();
        foreach(var rule in definition.Rules)
        {
            budget.Charge();
            var measure=WorldPopulationGeometry.MeasureArea(new(source.Regions,rule.RegionId,rule.AllocationId is null?null:source.LocalLayouts,rule.AllocationId),
                environment,rule.DensityFieldId,rule.MaximumCells,budget,token);
            double quantity=measure.Mass/rule.AmountPerMember;
            if(!double.IsFinite(quantity))throw new ArgumentException("METADATA_TERRAIN_DEMAND_COUNT_LIMIT:"+rule.Id);
            double nearest=Math.Round(quantity);
            if(quantity!=nearest&&Math.Abs(quantity-nearest)<=1e-9*Math.Max(1,Math.Abs(quantity)))
                throw new ArgumentException("METADATA_TERRAIN_DEMAND_COUNT_AMBIGUOUS:"+rule.Id);
            double count=Math.Floor(quantity);
            if(count<1)throw new ArgumentException("METADATA_TERRAIN_DEMAND_EMPTY:"+rule.Id);
            if(count>rule.MaximumCount)throw new ArgumentException("METADATA_TERRAIN_DEMAND_COUNT_LIMIT:"+rule.Id);
            results.Add(new(rule.Id,measure,(int)count));
        }
        return new(definition,source.RecipeSha256,source.CatalogSha256,results.AsReadOnly(),budget.Used);
    }
    internal static int? Count(MetadataTerrainDemandPlan? plan,string parent,MetadataPart part,HashSet<string> used)
    {
        var rule=plan?.Definition.Rules.SingleOrDefault(r=>r.ParentNodeId==parent&&r.PartId==part.Id);
        if(rule is null)return null;
        if(part.CountParameter is not null)throw new ArgumentException("METADATA_TERRAIN_DEMAND_COUNT_CONFLICT:"+rule.Id);
        if(!used.Add(rule.Id))throw new ArgumentException("METADATA_TERRAIN_DEMAND_BINDING_REUSED:"+rule.Id);
        int count=plan!.Results.Single(r=>r.RuleId==rule.Id).Count;
        if(count<part.Count)throw new ArgumentException("METADATA_TERRAIN_DEMAND_BELOW_CATALOG_MINIMUM:"+rule.Id);
        return count;
    }
    internal static void ValidateBindings(MetadataTerrainDemandPlan plan,IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token)
    {
        var ids=nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        var members=new HashSet<string>(StringComparer.Ordinal);
        foreach(var rule in plan.Definition.Rules)
        {
            token.ThrowIfCancellationRequested();
            if(!ids.Contains(rule.ParentNodeId))throw new ArgumentException("METADATA_TERRAIN_DEMAND_PARENT_REQUIRED:"+rule.Id);
            string prefix=rule.ParentNodeId+"/"+rule.PartId+"-";
            var expected=Enumerable.Range(0,plan.Results.Single(r=>r.RuleId==rule.Id).Count).Select(i=>prefix+i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);
            var actual=nodes.Where(n=>MetadataCapacityPlanner.IsPartChild(n,rule.ParentNodeId,rule.PartId)).Select(n=>n.Id).ToArray();
            if(actual.Length!=expected.Count||!expected.SetEquals(actual)||nodes.Any(n=>expected.Contains(n.Id)&&n.TerrainDemandRuleId!=rule.Id))throw new ArgumentException("METADATA_TERRAIN_DEMAND_MEMBERS_MISMATCH:"+rule.Id);
            members.UnionWith(expected);
        }
        if(nodes.Any(n=>n.TerrainDemandRuleId is not null&&!members.Contains(n.Id)))throw new ArgumentException("METADATA_TERRAIN_DEMAND_MEMBER_UNUSED");
    }
    internal static void MarkBindings(MetadataTerrainDemandPlan plan,List<WorldMetadataNode> nodes)
    {
        foreach(var rule in plan.Definition.Rules)
        {
            for(int i=0;i<nodes.Count;i++)if(MetadataCapacityPlanner.IsPartChild(nodes[i],rule.ParentNodeId,rule.PartId))
                nodes[i]=nodes[i] with{TerrainDemandRuleId=rule.Id};
        }
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();if(document.TerrainDemandPlan is not {} plan)
        {
            if(document.Nodes.Any(n=>n.TerrainDemandRuleId is not null))throw new ArgumentException("METADATA_TERRAIN_DEMAND_PLAN_REQUIRED");
            return;
        }
        var expected=Plan(plan.Definition,document.PlacementPlan?.Definition.SourceWorld??throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED"),token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(plan,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_TERRAIN_DEMAND_PLAN_MISMATCH");
        ValidateBindings(expected,document.Nodes,token);
        foreach(var rule in expected.Definition.Rules)
        {
            var mix=document.ProgramMixPlan?.Allocations.SingleOrDefault(a=>a.Request.ParentNodeId==rule.ParentNodeId&&a.Request.PartId==rule.PartId);
            if(mix is not null&&mix.Request.ExactCount!=expected.Results.Single(r=>r.RuleId==rule.Id).Count)
                throw new ArgumentException("METADATA_TERRAIN_DEMAND_MIX_COUNT_MISMATCH:"+rule.Id);
            var capacity=document.CapacityPlan?.Allocations.SingleOrDefault(a=>a.Request.ParentNodeId==rule.ParentNodeId&&a.Request.PartId==rule.PartId);
            if(capacity is not null&&capacity.Request.ExactCount!=expected.Results.Single(r=>r.RuleId==rule.Id).Count)
                throw new ArgumentException("METADATA_TERRAIN_DEMAND_CAPACITY_COUNT_MISMATCH:"+rule.Id);
        }
    }
}
