using System.Text.Json;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataServiceSupply(string Id,string Kind,string Unit,double Quantity,IReadOnlyList<string> Qualities,
    string Origin,string Purpose,string SourceRuleId,string? PortId=null,IReadOnlyList<MetadataServiceSourceReference>? UpstreamSources=null,WorldPolygon? Area=null);
public sealed record MetadataServiceSourceReference(string NodeId,string SupplyId);
public sealed record MetadataServiceOutput(string SupplyId,string? PortId=null,IReadOnlyList<MetadataServiceSourceReference>? UpstreamSources=null,WorldPolygon? Area=null);
public sealed record MetadataServiceRequirement(string Id,string Kind,string Unit,double Quantity,string ConnectionKind,
    string Purpose,string SourceRuleId,IReadOnlyList<string>? RequiredQualities=null,string? RequiredOrigin=null,
    bool RequirePhysicalRoute=false,double MinimumWidth=0,double? MaximumDistance=null,bool SourceContainsSite=false);
public sealed record MetadataServiceProfile(IReadOnlyList<MetadataServiceSupply> Supplies,IReadOnlyList<MetadataServiceRequirement> Requirements,
    bool UseStructureProfile=false,IReadOnlyList<string>? OutputQualities=null,IReadOnlyList<MetadataServiceOutput>? Outputs=null);
public sealed record MetadataServiceBinding(string ConsumerNodeId,string RequirementId,string ProviderNodeId,string SupplyId,
    IReadOnlyList<string> ConnectionIds,string Purpose,string SourceRuleId);
public sealed record MetadataServiceLink(string ConnectionId,double Capacity,string Unit,string Purpose,string SourceRuleId);
public sealed record MetadataServiceCorridorCapacity(string GroupId,double CapacityPerSegment,string Unit,string Purpose,string SourceRuleId);
public sealed record MetadataServiceSegmentCapacity(string GroupId,string SegmentId,double Capacity,string Unit,string Purpose,string SourceRuleId);
public sealed record MetadataServiceSegmentUse(string GroupId,string SegmentId,int Traversals,double Quantity);
public sealed record MetadataServiceDefinition(IReadOnlyList<MetadataServiceBinding> Bindings,IReadOnlyList<MetadataServiceLink> Links,long MaximumWork=2000000,
    IReadOnlyList<MetadataServiceCorridorCapacity>? CorridorCapacities=null);
public sealed record MetadataResolvedServices(string NodeId,string SourceId,IReadOnlyList<MetadataServiceSupply> Supplies,
    IReadOnlyList<MetadataServiceRequirement> Requirements,IReadOnlyList<string> RemainingRequirements);
public sealed record MetadataServiceAllocation(string ConsumerNodeId,string RequirementId,string ProviderNodeId,string SupplyId,
    string Kind,string Unit,double Quantity,IReadOnlyList<string> ConnectionIds,bool Physical,double? Distance,bool AreaContained=false,
    IReadOnlyList<MetadataServiceSegmentUse>? SegmentUses=null);
public sealed record MetadataServicePolicyEvaluation(string ConsumerNodeId,string RequirementId,string PolicyId,string SourceId);
public sealed record MetadataServicePlan(MetadataServiceDefinition Definition,IReadOnlyDictionary<string,MetadataServiceProfile> Profiles,
    IReadOnlyList<MetadataResolvedServices> Sources,IReadOnlyList<MetadataServiceAllocation> Allocations,IReadOnlyDictionary<string,double> Usage,long Work,
    IReadOnlyList<MetadataServicePolicyEvaluation>? PolicyEvaluations=null,IReadOnlyList<MetadataServiceSegmentCapacity>? SegmentCapacities=null);

public sealed partial class MetadataServicePlanner
{
    private static bool Id(string? value)=>WorldEnvironmentQuery.Id(value);
    private static bool Text(string? value)=>WorldEnvironmentQuery.Text(value);
    private static bool NodeId(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=2048;
    private static bool Quantity(double value)=>double.IsFinite(value)&&value>=1e-9&&value<=1e12;
    private static bool Tags(IReadOnlyList<string>? values)=>values is {Count:<=64}&&values.All(Id)&&values.Distinct(StringComparer.Ordinal).Count()==values.Count;
    private static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
    private static MetadataResolvedServices Resolve(WorldMetadataNode node,MetadataServiceProfile profile,IReadOnlyDictionary<string,MetadataStructureDefinition> structures,WorldSpatialWorkBudget budget)
    {
        if(profile?.Supplies is null||profile.Requirements is null||profile.Supplies.Count>32||profile.Requirements.Count>32||
            profile.OutputQualities is {} qualities&&!Tags(qualities)||profile.Outputs is {Count:>32}||
            profile.Outputs?.Any(o=>o is null||!Id(o.SupplyId))==true||profile.Outputs is {} outputs&&outputs.Select(o=>o.SupplyId).Distinct(StringComparer.Ordinal).Count()!=outputs.Count)
            throw new ArgumentException("METADATA_SERVICE_PROFILE_INVALID");
        var supplies=profile.Supplies;var requirements=profile.Requirements;var remaining=new List<string>();string source="archetype:"+node.ArchetypeId;
        if(profile.UseStructureProfile)
        {
            if(profile.Supplies.Count!=0||profile.Requirements.Count!=0)throw new ArgumentException("METADATA_SERVICE_SOURCE_CONFLICT");
            if(node.StructureKey is null||!structures.TryGetValue(node.StructureKey,out var structure)||structure.Technology is null||structure.Definition.SitingProfile is not {} siting)
                throw new ArgumentException("METADATA_SERVICE_STRUCTURE_TECHNOLOGY_REQUIRED");
            siting.Validate();source="structure:"+node.StructureKey;
            if(!siting.AllowedTechnologies.Contains(structure.Technology))throw new ArgumentException("METADATA_STRUCTURE_TECHNOLOGY_INVALID");
            if(profile.Outputs?.Any(o=>siting.Provides?.Contains(o.SupplyId)!=true)==true)throw new ArgumentException("METADATA_SERVICE_OUTPUT_UNKNOWN");
            supplies=(siting.Provides??new HashSet<string>()).Order(StringComparer.Ordinal).Select(kind=>{
                var output=profile.Outputs?.SingleOrDefault(o=>o.SupplyId==kind);
                return new MetadataServiceSupply(kind,kind,siting.OutputUnit,structure.Variant.ServiceCapacity*siting.OutputUnitsPerCapacity,
                    profile.OutputQualities??[],"facility","Selected source output",source,output?.PortId,output?.UpstreamSources,output?.Area);
            }).ToArray();
            var active=siting.Requirements.Where(r=>r.WhenTechnologies is null||r.WhenTechnologies.Contains(structure.Technology)).ToArray();
            requirements=active.Select(r=>new MetadataServiceRequirement(r.Id,r.Kind,r.Unit,r.BaseQuantity+structure.Variant.ServiceCapacity*r.QuantityPerCapacity,
                r.Mode.ToString().ToLowerInvariant(),"Selected source requirement",source,r.RequiredQualities?.Order(StringComparer.Ordinal).ToArray(),
                r.RequiredOrigin?.ToString().ToLowerInvariant(),!r.SourceContainsSite,r.MinimumRouteWidthMeters,r.MaximumRouteLengthMeters,r.SourceContainsSite)).ToArray();
            remaining.Add("catalog-context");
        }
        else if(node.StructureKey is not null||profile.OutputQualities is not null||profile.Outputs is not null)throw new ArgumentException("METADATA_SERVICE_SOURCE_CONFLICT");
        if(supplies.Count>32||requirements.Count>32||supplies.Select(s=>s?.Id).Distinct(StringComparer.Ordinal).Count()!=supplies.Count||
            requirements.Select(r=>r?.Id).Distinct(StringComparer.Ordinal).Count()!=requirements.Count)throw new ArgumentException("METADATA_SERVICE_PROFILE_INVALID");
        foreach(var s in supplies)if(s is null||!Id(s.Id)||!Id(s.Kind)||!Text(s.Unit)||!Quantity(s.Quantity)||!Tags(s.Qualities)||!Id(s.Origin)||!Text(s.Purpose)||!Text(s.SourceRuleId)||
            s.PortId is {} port&&!Id(port)||s.UpstreamSources is {Count:>64}||s.UpstreamSources?.Any(u=>u is null||!NodeId(u.NodeId)||!Id(u.SupplyId))==true)
            throw new ArgumentException("METADATA_SERVICE_SUPPLY_INVALID");
        foreach(var s in supplies)if(s.Area is {} area)
        {
            if(area.Vertices is not {Count:>=3 and <=256}||area.Vertices.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
                throw new ArgumentException("METADATA_SERVICE_AREA_INVALID");
            budget.Charge((long)area.Vertices.Count*area.Vertices.Count);area.Validate();
        }
        foreach(var r in requirements)if(r is null||!Id(r.Id)||!Id(r.Kind)||!Text(r.Unit)||!Quantity(r.Quantity)||!Id(r.ConnectionKind)||!Text(r.Purpose)||!Text(r.SourceRuleId)||
            r.RequiredQualities is {} tags&&!Tags(tags)||r.RequiredOrigin is {} origin&&!Id(origin)||!double.IsFinite(r.MinimumWidth)||r.MinimumWidth<0||r.MinimumWidth>1e6||
            r.MaximumDistance is {} distance&&(!double.IsFinite(distance)||distance<=0||distance>1e9)||!r.RequirePhysicalRoute&&(r.MinimumWidth>0||r.MaximumDistance is not null)||r.SourceContainsSite&&r.RequirePhysicalRoute)
            throw new ArgumentException("METADATA_SERVICE_REQUIREMENT_INVALID");
        return new(node.Id,source,Array.AsReadOnly(supplies.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(requirements.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()),remaining.AsReadOnly());
    }
}
