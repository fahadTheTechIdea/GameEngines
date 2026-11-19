using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core.Geometry;
using static WorldCreator.Core.Spatial.WorldRegionGeometry;

namespace WorldCreator.Core.Spatial;

public enum WorldLocalLayoutStrategy { OrientedGrid, PowerDiagram, BinaryPartition, AnchorBisection, AreaBalanced }
public sealed record WorldLocalProgramIntent(string Id,string Purpose,string SourceRuleId,double MinimumArea,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? AllocationWeight=null);
public sealed record WorldLocalGeneratedArea(string LayoutId,WorldAreaPopulationPlan AreaPlan);
public sealed record WorldLocalPartitionCut(string Id,string Purpose,string SourceRuleId,WorldPoint Origin,double NormalDegrees,string NegativeId,string PositiveId);
public sealed record WorldLocalPartition(string RootId,IReadOnlyList<WorldLocalPartitionCut> Cuts);
public sealed record WorldLocalGeneratedPartition(string LayoutId,WorldLocalPartition Partition);
internal sealed class WorldLayoutRegionConstraintException(string diagnostic):ArgumentException(diagnostic);
public sealed record WorldLocalGrid(WorldPoint Origin,double RotationDegrees,double CellWidth,double CellHeight);
public sealed record WorldLocalProgram(string Id,string Purpose,string SourceRuleId,WorldPoint Anchor,double MinimumArea,
    double Influence=0,int? Column=null,int? Row=null,bool AllowDisconnected=false);
public sealed record WorldLocalRegionFrame(string Purpose,string SourceRuleId);
public sealed record WorldLocalLayout(string Id,string RegionId,string Purpose,string SourceRuleId,WorldLocalLayoutStrategy Strategy,
    IReadOnlyList<WorldLocalProgram> Programs,WorldLocalGrid? Grid=null,string? ResidualPurpose=null,string? ParentAllocationId=null,WorldLocalRegionFrame? RegionFrame=null,WorldLocalPartition? Partition=null,
    IReadOnlyList<WorldLocalProgramIntent>? ProgramIntents=null,WorldAreaPopulationPattern? AreaPattern=null);
public sealed record WorldLocalInterfaceRequirement(string Id,string FromId,string ToId,string Purpose,string SourceRuleId,double MinimumSpan);
public sealed record WorldLocalLayoutDefinition(IReadOnlyList<WorldLocalLayout> Layouts,
    IReadOnlyList<WorldLocalInterfaceRequirement> Interfaces,int MaximumCells=4096,long MaximumWork=100000000);
public sealed record WorldLocalAllocation(string Id,string LayoutId,string RegionId,WorldLocalProgram Program,WorldRegion Shape);
public sealed record WorldLocalLayoutArea(string LayoutId,string RegionId,double RegionArea,double AllocatedArea,double ResidualArea,string? ResidualPurpose,string? ParentAllocationId=null);
public sealed record WorldLocalInterface(string Id,WorldLocalInterfaceRequirement Requirement,IReadOnlyList<WorldRegionEdge> Segments,
    WorldRegionEdge SelectedSpan,WorldPoint Position);
public sealed record WorldLocalLayoutPlan(WorldLocalLayoutDefinition Definition,string RegionPlanSha256,
    IReadOnlyList<WorldLocalAllocation> Allocations,IReadOnlyList<WorldLocalLayoutArea> Areas,IReadOnlyList<WorldLocalInterface> Interfaces,long Work,
    IReadOnlyList<WorldLocalGeneratedPartition>? GeneratedPartitions=null,IReadOnlyList<WorldLocalGeneratedArea>? GeneratedAreas=null);

/// <summary>Purposeful local programs clipped to real region geometry. Partition boundaries do not imply roads.</summary>
public sealed partial class WorldLocalLayoutPlanner
{
    public WorldLocalLayoutPlan Plan(WorldEnvironmentQuery environment,WorldRegionPlan regions,
        WorldLocalLayoutDefinition definition,CancellationToken token=default)=>Plan(environment,regions,definition,null,token);

    internal WorldLocalLayoutPlan Plan(WorldEnvironmentQuery environment,WorldRegionPlan regions,
        WorldLocalLayoutDefinition definition,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(environment);ArgumentNullException.ThrowIfNull(regions);token.ThrowIfCancellationRequested();
        ValidateSourceLimits(definition);
        definition=JsonSerializer.Deserialize<WorldLocalLayoutDefinition>(JsonSerializer.Serialize(definition,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parent);
        var replay=new WorldRegionPlanner().Plan(environment,regions.Request,budget,token);
        if(Json(regions)!=Json(replay))throw new ArgumentException("WORLD_LAYOUT_REGION_PLAN_MISMATCH");
        definition=ValidateSource(environment,regions,definition,budget,token);
        var layouts=definition.Layouts.ToList();
        var generated=new List<WorldLocalGeneratedPartition>();
        layouts=layouts.Select(layout=>{
            if(layout.Strategy!=WorldLocalLayoutStrategy.AnchorBisection)return layout;
            var partition=GenerateAnchorPartition(layout,budget);generated.Add(new(layout.Id,partition));
            return layout with{Strategy=WorldLocalLayoutStrategy.BinaryPartition,Partition=partition};
        }).ToList();
        // Slot fit can change under an authorized adaptation. Do not let that
        // outcome mask malformed later programs, ownership or interfaces.
        foreach(var layout in layouts.Where(l=>l.Grid is not null))foreach(var program in layout.Programs)
        {
            var grid=layout.Grid!;var p=Local(grid,program.Anchor);int col=program.Column!.Value,row=program.Row!.Value;
            if(p.X<col*grid.CellWidth||p.X>(col+1)*grid.CellWidth||p.Y<row*grid.CellHeight||p.Y>(row+1)*grid.CellHeight)
                throw new ArgumentException("WORLD_LAYOUT_ANCHOR_OUTSIDE_SLOT:"+program.Id);
        }
        // Retain the canonical source declaration. Geometry consumes resolved world
        // positions only; replay must resolve the same relative frame afresh.
        layouts=layouts.Select(layout=>{
            var region=regions.Regions.Single(r=>r.Id==layout.RegionId);
            return layout.RegionFrame is null?layout:layout with{
                Programs=layout.Programs.Select(p=>p with{Anchor=Resolve(p.Anchor,layout,region,budget)}).ToArray(),
                Grid=layout.Grid is {} grid?grid with{Origin=Resolve(grid.Origin,layout,region,budget)}:null,
                Partition=layout.Partition is {} partition?partition with{Cuts=partition.Cuts.Select(c=>c with{Origin=Resolve(c.Origin,layout,region,budget)}).ToArray()}:null};
        }).ToList();
        // Complete static validation before reporting a candidate-dependent regional conflict.
        foreach(var layout in layouts)
        {
            var rootRegion=regions.Regions.Single(r=>r.Id==layout.RegionId);
            foreach(var program in layout.Programs)
            {
                budget.Charge(rootRegion.Cells.Sum(c=>(long)c.Vertices.Count));
                if(!rootRegion.Cells.Any(c=>c.ContainsPoint(program.Anchor)))throw new WorldLayoutRegionConstraintException("WORLD_LAYOUT_ANCHOR_OUTSIDE_REGION:"+program.Id);
            }
        }
        var composition=ComposeLayouts(layouts,environment,regions,definition,budget,token);
        var interfaces=definition.Interfaces.Select(requirement=>Interface(composition.Allocations,requirement,budget)).ToArray();
        return new(definition,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(regions)))).ToLowerInvariant(),
            composition.Allocations.AsReadOnly(),composition.Areas.AsReadOnly(),Array.AsReadOnly(interfaces),budget.Used,
            generated.Count==0?null:generated.AsReadOnly(),composition.GeneratedAreas.Count==0?null:composition.GeneratedAreas.AsReadOnly());
    }
    // One source validator for normal planning and every declared strategy alternative.
    // Candidate-dependent slot/region/partition fit remains in Plan.
    internal static WorldLocalLayoutDefinition ValidateSource(WorldEnvironmentQuery environment,WorldRegionPlan regions,
        WorldLocalLayoutDefinition definition,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ValidateSourceLimits(definition);
        var layoutIds=new HashSet<string>(StringComparer.Ordinal);var owners=new HashSet<(string Region,string? Parent)>();
        var programs=new HashSet<string>(StringComparer.Ordinal);var layouts=new List<WorldLocalLayout>();
        foreach(var layout in definition.Layouts.OrderBy(l=>l?.Id,StringComparer.Ordinal))
        {
            if(layout is null||!WorldEnvironmentQuery.Id(layout.Id)||!layoutIds.Add(layout.Id)||layout.RegionId is null||!owners.Add((layout.RegionId,layout.ParentAllocationId))||
                (layout.ParentAllocationId is not null&&!WorldEnvironmentQuery.Id(layout.ParentAllocationId))||
                !WorldEnvironmentQuery.Text(layout.Purpose)||!WorldEnvironmentQuery.Text(layout.SourceRuleId)||!Enum.IsDefined(layout.Strategy)||
                layout.Programs is null||layout.Programs.Count>32||(layout.Strategy==WorldLocalLayoutStrategy.AreaBalanced?layout.Programs.Count!=0:layout.Programs.Count<1)||
                (layout.ResidualPurpose is not null&&!WorldEnvironmentQuery.Text(layout.ResidualPurpose)))
                throw new ArgumentException("WORLD_LAYOUT_REQUEST_INVALID");
            var region=regions.Regions.SingleOrDefault(r=>r.Id==layout.RegionId)??throw new ArgumentException("WORLD_LAYOUT_REGION_REQUIRED:"+layout.Id);
            if(layout.RegionFrame is {} frame&&(!WorldEnvironmentQuery.Text(frame.Purpose)||!WorldEnvironmentQuery.Text(frame.SourceRuleId)))
                throw new ArgumentException("WORLD_LAYOUT_REGION_FRAME_INVALID");
            var pointQuery=new WorldPointSuitability(environment,WorldRegionPlanner.ProfileFor(regions,layout.RegionId));
            if(layout.Strategy==WorldLocalLayoutStrategy.AreaBalanced)
            {
                if(layout.ProgramIntents is not {Count:>=1 and <=32}||layout.AreaPattern is null||layout.RegionFrame is not null)
                    throw new ArgumentException("WORLD_LAYOUT_PROGRAM_INTENTS_INVALID");
                WorldPopulationGeometry.ValidateAreaPattern(environment,layout.AreaPattern,layout.ProgramIntents.Count);
                foreach(var intent in layout.ProgramIntents)
                    if(intent is null||!WorldEnvironmentQuery.Id(intent.Id)||!programs.Add(intent.Id)||programs.Count>128||
                        !WorldEnvironmentQuery.Text(intent.Purpose)||!WorldEnvironmentQuery.Text(intent.SourceRuleId)||
                        !double.IsFinite(intent.MinimumArea)||intent.MinimumArea is <=0 or >4e14||
                        !ValidAllocationWeight(intent.AllocationWeight))
                        throw new ArgumentException("WORLD_LAYOUT_PROGRAM_INTENT_INVALID");
            }
            else if(layout.ProgramIntents is not null||layout.AreaPattern is not null)throw new ArgumentException("WORLD_LAYOUT_PROGRAM_INTENTS_NOT_SELECTED");
            if(layout.Strategy==WorldLocalLayoutStrategy.OrientedGrid)
            {
                var grid=layout.Grid;
                if(grid is null||!Point(grid.Origin)||!double.IsFinite(grid.RotationDegrees)||Math.Abs(grid.RotationDegrees)>360||
                    !double.IsFinite(grid.CellWidth)||grid.CellWidth<=1e-6||grid.CellWidth>1e7||
                    !double.IsFinite(grid.CellHeight)||grid.CellHeight<=1e-6||grid.CellHeight>1e7)
                    throw new ArgumentException("WORLD_LAYOUT_GRID_INVALID");
            }
            else if(layout.Grid is not null)throw new ArgumentException("WORLD_LAYOUT_GRID_NOT_SELECTED");
            if(layout.Strategy!=WorldLocalLayoutStrategy.BinaryPartition&&layout.Partition is not null)
                throw new ArgumentException("WORLD_LAYOUT_PARTITION_NOT_SELECTED");
            var slots=new HashSet<(int,int)>();var anchors=new HashSet<WorldPoint>();
            foreach(var program in layout.Programs)
            {
                if(program is null||!WorldEnvironmentQuery.Id(program.Id)||!programs.Add(program.Id)||programs.Count>128||
                    !WorldEnvironmentQuery.Text(program.Purpose)||!WorldEnvironmentQuery.Text(program.SourceRuleId)||!Point(program.Anchor)||
                    !double.IsFinite(program.MinimumArea)||program.MinimumArea<=0||program.MinimumArea>4e14||
                    !double.IsFinite(program.Influence)||Math.Abs(program.Influence)>1e14||!anchors.Add(program.Anchor)||
                    (layout.RegionFrame is null&&!pointQuery.Evaluate(program.Anchor,token).Eligible))
                    throw new ArgumentException("WORLD_LAYOUT_PROGRAM_INVALID");
                if(layout.Strategy==WorldLocalLayoutStrategy.OrientedGrid)
                {
                    if(program.Column is not {} col||program.Row is not {} row||Math.Abs((long)col)>1024||Math.Abs((long)row)>1024||
                        !slots.Add((col,row))||program.Influence!=0)throw new ArgumentException("WORLD_LAYOUT_GRID_SLOT_INVALID");
                }
                else if(program.Column is not null||program.Row is not null)throw new ArgumentException("WORLD_LAYOUT_SLOT_NOT_SELECTED");
                if(layout.Strategy is WorldLocalLayoutStrategy.BinaryPartition or WorldLocalLayoutStrategy.AnchorBisection&&program.Influence!=0)
                    throw new ArgumentException("WORLD_LAYOUT_PARTITION_INFLUENCE_NOT_SELECTED");
            }
            if(layout.Strategy==WorldLocalLayoutStrategy.BinaryPartition)PartitionPaths(layout,budget);
            layouts.Add(layout with{Programs=Array.AsReadOnly(layout.Programs.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray()),
                ProgramIntents=layout.ProgramIntents?.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray(),
                Partition=layout.Partition is {} partition?partition with{Cuts=partition.Cuts.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray()}:null});
        }
        // Resolve the declared allocation hierarchy before generating any shapes.
        // Child strategies consume their parent's complete clipped geometry;
        // terrain components and artificial barriers are not layout scopes.
        var programOwners=layouts.SelectMany(l=>l.Programs.Select(p=>p.Id).Concat(l.ProgramIntents?.Select(p=>p.Id)??[]).Select(id=>(Id:id,Layout:l))).ToDictionary(x=>x.Id,x=>x.Layout,StringComparer.Ordinal);
        foreach(var layout in layouts.Where(l=>l.ParentAllocationId is not null))
        {
            if(!programOwners.TryGetValue(layout.ParentAllocationId!,out var owner))throw new ArgumentException("WORLD_LAYOUT_PARENT_REQUIRED:"+layout.Id);
            if(owner.RegionId!=layout.RegionId)throw new ArgumentException("WORLD_LAYOUT_PARENT_REGION_MISMATCH:"+layout.Id);
        }
        var pending=layouts.ToList();layouts=[];var resolved=new HashSet<string>(StringComparer.Ordinal);
        while(pending.Count>0)
        {
            budget.Charge(pending.Count);
            var next=pending.FirstOrDefault(l=>l.ParentAllocationId is null||resolved.Contains(programOwners[l.ParentAllocationId].Id))
                ??throw new ArgumentException("WORLD_LAYOUT_DEPENDENCY_CYCLE");
            pending.Remove(next);layouts.Add(next);resolved.Add(next.Id);
        }
        var interfaceIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var requirement in definition.Interfaces)
            if(requirement is null||!WorldEnvironmentQuery.Id(requirement.Id)||!interfaceIds.Add(requirement.Id)||requirement.FromId==requirement.ToId||
                !programs.Contains(requirement.FromId)||!programs.Contains(requirement.ToId)||!WorldEnvironmentQuery.Text(requirement.Purpose)||
                !WorldEnvironmentQuery.Text(requirement.SourceRuleId)||!double.IsFinite(requirement.MinimumSpan)||requirement.MinimumSpan<=1e-8||requirement.MinimumSpan>3e7)
                throw new ArgumentException("WORLD_LAYOUT_INTERFACE_INVALID");
        definition=definition with{Layouts=layouts.AsReadOnly(),Interfaces=Array.AsReadOnly(definition.Interfaces.OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray())};
        return definition;
    }
    internal static void ValidateSourceLimits(WorldLocalLayoutDefinition definition)
    {
        if(definition?.Layouts is null||definition.Layouts.Count is <1 or >32||definition.Interfaces is null||definition.Interfaces.Count>128||
            definition.MaximumCells is <1 or >16384||definition.MaximumWork is <1 or >200000000)
            throw new ArgumentException("WORLD_LAYOUT_DEFINITION_INVALID");
        // Check typed floating-point weights before JSON snapshotting so invalid
        // values receive the same semantic diagnostic as finite bad weights.
        if(definition.Layouts.Where(l=>l?.ProgramIntents is not null).SelectMany(l=>l.ProgramIntents!).Any(i=>i is not null&&!ValidAllocationWeight(i.AllocationWeight)))
            throw new ArgumentException("WORLD_LAYOUT_PROGRAM_INTENT_INVALID");
    }
    private static bool ValidAllocationWeight(double? weight)=>weight is null||double.IsFinite(weight.Value)&&weight.Value>0&&weight.Value<=4e14;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPoint Resolve(WorldPoint point,WorldLocalLayout layout,WorldRegion region,WorldSpatialWorkBudget budget)
    {
        if(layout.RegionFrame is null)return point;
        budget.Charge();var resolved=point.Offset(region.Seed.Position.X,region.Seed.Position.Y);
        if(!Point(resolved))throw new ArgumentException("WORLD_LAYOUT_REGION_FRAME_RANGE_INVALID");
        return resolved;
    }
    private static bool Point(WorldPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X)<=1e7&&Math.Abs(p.Y)<=1e7;
    private static WorldPoint Local(WorldLocalGrid g,WorldPoint p)
    {double angle=g.RotationDegrees*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle),x=p.X-g.Origin.X,y=p.Y-g.Origin.Y;return new(c*x+s*y,-s*x+c*y);}
    private static WorldRegionEdge Canonical(WorldRegionEdge e)=>e.From.X<e.To.X||e.From.X==e.To.X&&e.From.Y<=e.To.Y?e:new(e.To,e.From);
    private static IReadOnlyList<WorldRegionEdge> Merge(List<WorldRegionEdge> source,WorldSpatialWorkBudget budget)
    {
        var result=source.Distinct().OrderBy(e=>e.From.X).ThenBy(e=>e.From.Y).ThenBy(e=>e.To.X).ThenBy(e=>e.To.Y).ToList();
        bool changed=true;
        while(changed)
        {
            changed=false;
            for(int i=0;i<result.Count&&!changed;i++)for(int j=i+1;j<result.Count;j++)
            {
                budget.Charge();var a=result[i];var b=result[j];double length=a.From.DistanceTo(a.To);
                double Cross(WorldPoint p)=>Math.Abs((a.To.X-a.From.X)*(p.Y-a.From.Y)-(a.To.Y-a.From.Y)*(p.X-a.From.X))/length;
                if(Cross(b.From)>1e-8||Cross(b.To)>1e-8)continue;
                if(Shared(a,b) is null&&Math.Min(a.To.DistanceTo(b.From),b.To.DistanceTo(a.From))>1e-8)continue;
                var points=new[]{a.From,a.To,b.From,b.To}.OrderBy(p=>(p.X-a.From.X)*(a.To.X-a.From.X)+(p.Y-a.From.Y)*(a.To.Y-a.From.Y)).ToArray();result[i]=Canonical(new(points[0],points[^1]));result.RemoveAt(j);changed=true;break;
            }
        }
        return result.OrderBy(e=>e.From.X).ThenBy(e=>e.From.Y).ThenBy(e=>e.To.X).ThenBy(e=>e.To.Y).ToArray();
    }
}
