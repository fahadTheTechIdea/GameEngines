using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>A direct catalog-part binding, resolved separately inside each actual parent.</summary>
public sealed record MetadataProgramBinding(string DemandId,string PartId,bool UseCatalogFootprint=true,double RotationDegrees=0,
    MetadataSiteLayout? SiteLayout=null,MetadataPopulationGrammar? Population=null);
public sealed record MetadataProgramGrowth(string RootPartId,int RootIndex,string AnchorId,IReadOnlyList<string> ConnectionKinds,
    IReadOnlyList<double> DirectionAnglesDegrees,double Gap,double MaximumConnectionDistance);
/// <summary>Reusable local physical program. It selects actual expanded parents;
/// it does not create metadata nodes, replace identifiers or resize source content.</summary>
public sealed record MetadataNestedProgram(string Id,string Purpose,string SourceRuleId,string ScopeNodeId,string ParentArchetypeId,
    int MinimumParents,int MaximumParents,WorldPlacementRequest Request,IReadOnlyList<MetadataProgramBinding> Bindings,
    MetadataProgramGrowth? Growth=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataParentBoundary? ParentBoundary=null);
public sealed record MetadataProgramInstance(string ProgramId,string ParentNodeId,string StageId,IReadOnlyList<string> ChildNodeIds);

public sealed partial class MetadataPlacementPlanner
{
    internal static bool RequiresConnectedPrograms(MetadataPlacementDefinition? definition)=>definition?.ConnectedLayouts is not null||
        definition?.Programs?.Any(p=>p?.Growth is not null)==true;
    private sealed record ProgramExpansion(MetadataPlacementDefinition Definition,IReadOnlyList<MetadataProgramInstance>? Instances);
    private static ProgramExpansion InstantiatePrograms(MetadataPlacementDefinition definition,IReadOnlyList<WorldMetadataNode> nodes,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition.Programs is not {} programs)return new(definition,null);
        if(programs.Count is <1 or >64)throw new ArgumentException("METADATA_PROGRAM_DECLARATIONS_INVALID");
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var children=nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        var nested=(definition.NestedStages??[]).ToList();var layouts=(definition.ConnectedLayouts??[]).ToList();
        var stageIds=new HashSet<string>(StringComparer.Ordinal){definition.Request.Id};var owners=new HashSet<string>(StringComparer.Ordinal);
        foreach(var stage in nested)
            if(stage is null||!stageIds.Add(stage.Id)||!owners.Add(stage.ParentNodeId))throw new ArgumentException("METADATA_PLACEMENT_STAGE_INVALID");
        var programIds=new HashSet<string>(StringComparer.Ordinal);var instances=new List<MetadataProgramInstance>();
        foreach(var program in programs.OrderBy(p=>p?.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var limits=program?.Request is {} request?WorldSitePlacementPlanner.Limits(request.Execution,program.Growth is not null):default;
            if(program is null||!WorldEnvironmentQuery.Id(program.Id)||!programIds.Add(program.Id)||!WorldEnvironmentQuery.Text(program.Purpose)||
                !WorldEnvironmentQuery.Text(program.SourceRuleId)||program.ScopeNodeId is null||!lookup.ContainsKey(program.ScopeNodeId)||
                !WorldEnvironmentQuery.Id(program.ParentArchetypeId)||program.MinimumParents<0||program.MaximumParents<program.MinimumParents||
                program.MaximumParents>MaximumNestedStages||program.Request?.Demands is not {Count:>=1}||program.Request.Demands.Count>limits.Demands||
                program.Bindings is not {Count:>=1}||program.Bindings.Count>limits.Demands||program.Bindings.Count!=program.Request.Demands.Count||program.Request.Growth is not null)
                throw new ArgumentException("METADATA_PROGRAM_INVALID");
            var demands=new HashSet<string>(StringComparer.Ordinal);var parts=new HashSet<string>(StringComparer.Ordinal);
            foreach(var binding in program.Bindings)
                if(binding is null||!WorldEnvironmentQuery.Id(binding.DemandId)||!demands.Add(binding.DemandId)||
                    !WorldEnvironmentQuery.Id(binding.PartId)||!parts.Add(binding.PartId))throw new ArgumentException("METADATA_PROGRAM_BINDING_INVALID");
            if(program.Request.Demands.Any(d=>d is null)||!demands.SetEquals(program.Request.Demands.Select(d=>d.Id)))
                throw new ArgumentException("METADATA_PROGRAM_DEMAND_MISMATCH");
            if(program.ParentBoundary is {} boundaryKind&&!Enum.IsDefined(boundaryKind))throw new ArgumentException("METADATA_PLACEMENT_PARENT_BOUNDARY_INVALID");
            if(program.Growth is {} growth)
            {
                if(program.Request.Execution!=WorldPlacementExecution.Search)throw new ArgumentException("METADATA_PROGRAM_GROWTH_INVALID");
                if(growth.RootPartId is null||!parts.Contains(growth.RootPartId)||growth.RootIndex<0||growth.RootIndex>=limits.Demands)
                    throw new ArgumentException("METADATA_PROGRAM_GROWTH_ROOT_INVALID");
                if(growth.AnchorId is null||program.Request.Anchors is null||!program.Request.Anchors.Any(a=>a?.Id==growth.AnchorId)||
                    growth.ConnectionKinds is not {Count:>=1 and <=32}||growth.ConnectionKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||
                    growth.ConnectionKinds.Distinct(StringComparer.Ordinal).Count()!=growth.ConnectionKinds.Count||
                    growth.DirectionAnglesDegrees is not {Count:>=1 and <=32}||growth.DirectionAnglesDegrees.Any(a=>!double.IsFinite(a)||a<0||a>=360)||
                    growth.DirectionAnglesDegrees.Distinct().Count()!=growth.DirectionAnglesDegrees.Count||!double.IsFinite(growth.Gap)||growth.Gap<0||growth.Gap>1e7||
                    !double.IsFinite(growth.MaximumConnectionDistance)||growth.MaximumConnectionDistance<=growth.Gap||growth.MaximumConnectionDistance>1e7)
                    throw new ArgumentException("METADATA_PROGRAM_GROWTH_INVALID");
            }
            bool InScope(WorldMetadataNode node)
            {
                var visited=new HashSet<string>(StringComparer.Ordinal);string? id=node.ParentId;
                while(id is not null)
                {
                    budget.Charge();
                    if(!visited.Add(id)||visited.Count>32||!lookup.TryGetValue(id,out var parent))throw new ArgumentException("METADATA_PROGRAM_HIERARCHY_INVALID");
                    if(id==program.ScopeNodeId)return true;id=parent.ParentId;
                }
                return false;
            }
            budget.Charge(nodes.Count);
            var selected=nodes.Where(n=>n.ArchetypeId==program.ParentArchetypeId&&InScope(n)).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            if(selected.Length<program.MinimumParents||selected.Length>program.MaximumParents)
                throw new ArgumentException("METADATA_PROGRAM_PARENT_COUNT:"+program.Id);
            foreach(var parent in selected)
            {
                budget.Charge();
                if(nested.Count>=MaximumNestedStages)throw new ArgumentException("METADATA_PROGRAM_STAGE_BUDGET");
                if(!owners.Add(parent.Id))throw new ArgumentException("METADATA_PROGRAM_PARENT_OWNERSHIP_CONFLICT:"+parent.Id);
                string identity=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Program=program.Id,Parent=parent.Id})));
                string stageId="program-"+identity[..24];
                if(!stageIds.Add(stageId))throw new ArgumentException("METADATA_PROGRAM_STAGE_ID_CONFLICT");
                var members=new List<string>();var concrete=new List<MetadataPlacementBinding>();
                WorldMetadataNode[] Part(string part)
                {
                    var available=children.GetValueOrDefault(parent.Id,[]);budget.Charge(available.Length);
                    return available.Where(n=>MetadataCapacityPlanner.IsPartChild(n,parent.Id,part)).OrderBy(n=>
                        int.Parse(n.Id[(n.Id.LastIndexOf('-')+1)..],System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                }
                foreach(var binding in program.Bindings.OrderBy(b=>b.DemandId,StringComparer.Ordinal))
                {
                    var actual=Part(binding.PartId);
                    if(actual.Length==0)throw new ArgumentException("METADATA_PROGRAM_PART_REQUIRED:"+program.Id+":"+parent.Id+":"+binding.PartId);
                    members.AddRange(actual.Select(n=>n.Id));
                    if(members.Count>limits.Demands)throw new ArgumentException("METADATA_PROGRAM_MEMBER_BUDGET");
                    concrete.Add(new(binding.DemandId,parent.Id,binding.UseCatalogFootprint,binding.RotationDegrees,binding.PartId,binding.SiteLayout,binding.Population));
                }
                nested.Add(new(stageId,parent.Id,program.Request,concrete,program.ParentBoundary));
                if(program.Growth is {} pattern)
                {
                    var roots=Part(pattern.RootPartId);
                    if(pattern.RootIndex>=roots.Length)throw new ArgumentException("METADATA_PROGRAM_GROWTH_ROOT_MISSING");
                    layouts.Add(new(stageId+"-links",program.Purpose,program.SourceRuleId,stageId,roots[pattern.RootIndex].Id,pattern.AnchorId,
                        pattern.ConnectionKinds,pattern.DirectionAnglesDegrees,pattern.Gap,pattern.MaximumConnectionDistance));
                }
                instances.Add(new(program.Id,parent.Id,stageId,Array.AsReadOnly(members.Order(StringComparer.Ordinal).ToArray())));
            }
        }
        return new(definition with{NestedStages=nested.AsReadOnly(),ConnectedLayouts=layouts.Count==0?null:layouts.AsReadOnly(),Programs=null},instances.AsReadOnly());
    }
}
