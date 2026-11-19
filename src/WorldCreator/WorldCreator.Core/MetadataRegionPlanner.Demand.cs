using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionDemandRule(string Id,string Purpose,string SourceRuleId,string RegionId,double MaximumCoverage);
public sealed record MetadataRegionDemandDefinition(int Version,string Id,string Purpose,string SourceRuleId,
    IReadOnlyList<MetadataRegionDemandRule> Rules,long MaximumWork=1000000);
public sealed record MetadataRegionSourceDemand(MetadataResolvedFootprint Source,double EnvelopeArea,double AnchorRadius);
public sealed record MetadataRegionDemandResult(MetadataRegionDemandRule Rule,IReadOnlyList<MetadataRegionSourceDemand> Sources,
    double EnvelopeArea,double RequiredArea,double RequiredClearance);
public sealed record MetadataRegionDemandPlan(int Version,MetadataRegionDemandDefinition Definition,WorldRegionRequest SourceRequest,
    WorldRegionRequest DerivedRequest,IReadOnlyList<MetadataRegionDemandResult> Results,long Work);

public sealed partial class MetadataRegionPlanner
{
    internal MetadataRegionDemandPlan Measure(MetadataRegionDemandDefinition definition,WorldRegionRequest request,MetadataPlacementDefinition placement,
        IReadOnlyList<WorldMetadataNode> nodes,MetadataSpace space,IReadOnlyDictionary<string,MetadataCatalogFootprint> footprints,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,IReadOnlyDictionary<string,MetadataCatalogAccess>? access,CancellationToken token,bool unplaced=false)
    {
        token.ThrowIfCancellationRequested();
        if(definition is null||definition.Version!=1||!WorldEnvironmentQuery.Id(definition.Id)||!WorldEnvironmentQuery.Text(definition.Purpose)||
            !WorldEnvironmentQuery.Text(definition.SourceRuleId)||definition.Rules is not {Count:>=1 and <=32}||definition.MaximumWork is <1 or >20000000||
            request?.Intents is not {Count:>=1 and <=32}||request.Intents.Any(i=>i is null)||space?.Axes.Count!=2)
            throw new ArgumentException("METADATA_REGION_DEMAND_DEFINITION_INVALID");
        if(request.ExcludedAssignments is {Count:>0})throw new ArgumentException("METADATA_REGION_DEMAND_EXCLUSIONS_INTERNAL");
        var ids=new HashSet<string>(StringComparer.Ordinal);var regions=new HashSet<string>(StringComparer.Ordinal);
        foreach(var rule in definition.Rules)
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!ids.Add(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
                !WorldEnvironmentQuery.Id(rule.RegionId)||!regions.Add(rule.RegionId)||request.Intents.Count(i=>i?.Id==rule.RegionId)!=1||
                !double.IsFinite(rule.MaximumCoverage)||rule.MaximumCoverage is <=0 or >1)
                throw new ArgumentException("METADATA_REGION_DEMAND_RULE_INVALID");
        definition=definition with{Rules=definition.Rules.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()};
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,failureCode:"METADATA_REGION_DEMAND_WORK_BUDGET");
        var sources=new MetadataPlacementPlanner().RegionSources(placement,nodes,space.Unit,footprints,structures,access,budget,token,unplaced);
        var results=new List<MetadataRegionDemandResult>();var minima=new Dictionary<string,(double Area,double Clearance)>(StringComparer.Ordinal);
        foreach(var rule in definition.Rules)
        {
            budget.Charge(sources.Count);var selected=sources.Where(s=>s.RegionId==rule.RegionId).Select(s=>s.Source).OrderBy(s=>s.NodeId,StringComparer.Ordinal).ToArray();
            if(selected.Length==0)throw new ArgumentException("METADATA_REGION_DEMAND_SOURCE_REQUIRED:"+rule.RegionId);
            var measured=MeasureEnvelopes(selected,budget);
            double total=measured.Sum(s=>s.EnvelopeArea),required=total/rule.MaximumCoverage,clearance=measured.Max(s=>s.AnchorRadius);
            if(!double.IsFinite(required)||required<=0||required>4e14)throw new ArgumentException("METADATA_REGION_DEMAND_AREA_INVALID:"+rule.Id);
            minima.Add(rule.RegionId,(required,clearance));results.Add(new(rule,measured,total,required,clearance));
        }
        var derived=request with{Intents=request.Intents.Select(i=>minima.TryGetValue(i.Id,out var bound)?i with{
            MinimumArea=Math.Max(i.MinimumArea,bound.Area),MinimumClearance=Math.Max(i.MinimumClearance,bound.Clearance)}:i).ToArray()};
        return new(1,definition,request,derived,results.ToArray(),budget.Used);
    }

    internal static IReadOnlyList<MetadataRegionSourceDemand> MeasureEnvelopes(IEnumerable<MetadataResolvedFootprint> sources,WorldSpatialWorkBudget budget)
    {
        var measured=new List<MetadataRegionSourceDemand>();
        foreach(var source in sources.OrderBy(s=>s.NodeId,StringComparer.Ordinal))
        {
            var envelope=source.Reservation??source.Polygon;budget.Charge(envelope.Vertices.Count*4L);
            // Preserve source-local offsets: the actual anchor is the source origin.
            var origin=envelope.Vertices[0];double twice=0;
            for(int i=0;i<envelope.Vertices.Count;i++)
            {var a=envelope.Vertices[i];var b=envelope.Vertices[(i+1)%envelope.Vertices.Count];twice+=(a.X-origin.X)*(b.Y-origin.Y)-(a.Y-origin.Y)*(b.X-origin.X);}
            measured.Add(new(source,Math.Abs(twice)/2,envelope.Vertices.Max(v=>Math.Sqrt(v.X*v.X+v.Y*v.Y))));
        }
        return measured.ToArray();
    }

    private MetadataRegionDemandPlan? ValidateDemand(WorldMetadataDocument document,CancellationToken token)
    {
        if(document.RegionDemandPlan is not {} stored)
        {if(document.RegionDemandId is not null)throw new ArgumentException("METADATA_REGION_DEMAND_PLAN_REQUIRED");return null;}
        if(stored.Version!=1||stored.Definition is null||document.RegionDemandId!=stored.Definition.Id||document.RegionPlan is null||document.PlacementPlan is null)
            throw new ArgumentException("METADATA_REGION_DEMAND_PLAN_INVALID");
        var expected=Measure(stored.Definition,stored.SourceRequest,document.PlacementPlan.TemplateDefinition??document.PlacementPlan.Definition,
            document.Nodes,document.Space,document.FootprintDefinitions??new Dictionary<string,MetadataCatalogFootprint>(),document.StructureDefinitions,
            document.AccessPlan?.Definitions,token);
        if(!MetadataPopulationSources.Equal(expected,stored))throw new ArgumentException("METADATA_REGION_DEMAND_PLAN_MISMATCH");
        return expected;
    }
}
