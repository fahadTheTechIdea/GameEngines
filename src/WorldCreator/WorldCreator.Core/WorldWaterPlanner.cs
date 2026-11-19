using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldWaterDefinition(int SchemaVersion,string Id,string Kind,string Purpose,string SourceRuleId,
    double MinimumFlow,double BaseWidth,double WidthPerRate=0,double MaximumWidth=10000,int Priority=1000,
    int MaximumSegments=512,int MaximumPatches=1024,long MaximumWork=20000000,IReadOnlyList<WorldRetainedWaterDefinition>? RetainedBasins=null);
public sealed record WorldWaterSegment(int FromSampleIndex,int ToSampleIndex,double Flow,double Width,WorldPolygon Envelope,
    IReadOnlyList<string> PatchIds,double RetainedArea,double ClippedArea);
public sealed record WorldWaterPlan(int SchemaVersion,WorldWaterDefinition Definition,string DrainageSha256,
    IReadOnlyList<WorldWaterSegment> Segments,IReadOnlyList<WorldTerrainPatch> Patches,int BelowThresholdEdges,long Work,
    IReadOnlyList<WorldRetainedWaterBody>? RetainedBodies=null);

/// <summary>Optional occupied wet terrain. Existing terrain faces retain their
/// elevation planes; threshold and width are explicit game rules, not hydraulics.</summary>
public sealed partial class WorldWaterPlanner
{
    public WorldWaterPlan Plan(WorldDrainagePlan drainage,WorldWaterDefinition definition,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(drainage);
        static bool Text(string? s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=2048;
        if(definition is null||definition.SchemaVersion!=1||!SettlementCatalogRegistry.ValidId(definition.Id)||definition.Id.Length>96||
            !SettlementCatalogRegistry.ValidId(definition.Kind)||!Text(definition.Purpose)||!Text(definition.SourceRuleId)||
            !double.IsFinite(definition.MinimumFlow)||definition.MinimumFlow is <=0 or >1e15||!double.IsFinite(definition.BaseWidth)||definition.BaseWidth is <.001 or >10000||
            !double.IsFinite(definition.WidthPerRate)||definition.WidthPerRate is <0 or >10000||!double.IsFinite(definition.MaximumWidth)||definition.MaximumWidth<definition.BaseWidth||definition.MaximumWidth>10000||
            definition.Priority>int.MaxValue-1024||definition.MaximumSegments is <1 or >4096||definition.MaximumPatches is <1 or >1024||definition.MaximumWork is <1 or >200000000)
            throw new ArgumentException("WORLD_WATER_DEFINITION_INVALID");
        definition=SnapshotRetained(definition,token);
        new WorldDrainagePlanner().Validate(drainage,token);
        var surface=new WorldSurfaceQuery(drainage.Surface,token);var field=surface.HeightField?.CompleteField??throw new ArgumentException("WORLD_WATER_HEIGHTFIELD_REQUIRED");
        if(surface.Definition.Patches.Any(p=>p.Priority>=definition.Priority))throw new ArgumentException("WORLD_WATER_PRIORITY_CONFLICT");
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,failureCode:"WORLD_WATER_WORK_BUDGET");
        var selected=drainage.Nodes.Where(n=>n.DownstreamSampleIndex is not null&&n.Outgoing>=definition.MinimumFlow).OrderBy(n=>n.SampleIndex).ToArray();
        budget.Charge(drainage.Nodes.Count);
        if(selected.Length>definition.MaximumSegments)throw new ArgumentException("WORLD_WATER_SEGMENT_BUDGET");
        var lookup=drainage.Nodes.ToDictionary(n=>n.SampleIndex);var patches=new List<WorldTerrainPatch>();var segments=new List<WorldWaterSegment>();
        var sourcePriorities=surface.Definition.Patches.ToDictionary(p=>p.Id,p=>(long)p.Priority,StringComparer.Ordinal);
        var patchRanks=new Dictionary<string,long>(StringComparer.Ordinal);
        var ids=surface.Definition.Patches.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
        var areas=new WorldSurfaceAreaQuery(surface,false);
        var fieldBoundary=new WorldPolygon([field.Origin,new(field.Origin.X+(field.Columns-1)*field.StepX,field.Origin.Y),
            new(field.Origin.X+(field.Columns-1)*field.StepX,field.Origin.Y+(field.Rows-1)*field.StepY),new(field.Origin.X,field.Origin.Y+(field.Rows-1)*field.StepY)]);
        // All selected envelopes share original coordinate anchors before any
        // face intersections are calculated. Independent edge sweeps must not
        // promote slightly different calculations of one seam into new anchors.
        var envelopes=selected.ToDictionary(edge=>edge.SampleIndex,edge=>{
            double width=definition.BaseWidth+definition.WidthPerRate*edge.Outgoing;
            if(!double.IsFinite(width)||width>definition.MaximumWidth)throw new ArgumentException("WORLD_WATER_WIDTH_LIMIT:"+edge.SampleIndex);
            budget.Charge(2048);return WorldSurfaceTraversal.Envelope(edge.Position,lookup[edge.DownstreamSampleIndex!.Value].Position,width/2);
        });
        var anchors=envelopes.Values.Concat(surface.Definition.Patches.Select(p=>p.Boundary))
            .Concat(surface.Definition.Exclusions.Select(e=>e.Boundary)).Append(fieldBoundary)
            .Concat(surface.Definition.Boundary is {} domain?new[]{domain}:[]).SelectMany(p=>p.Vertices).ToArray();
        budget.Charge(anchors.Length+field.Columns+field.Rows);
        var anchorX=new SortedSet<double>(anchors.Select(p=>p.X));var anchorY=new SortedSet<double>(anchors.Select(p=>p.Y));
        for(int col=0;col<field.Columns;col++)anchorX.Add(field.Origin.X+col*field.StepX);
        for(int row=0;row<field.Rows;row++)anchorY.Add(field.Origin.Y+row*field.StepY);
        double scale=Math.Max(1,anchors.Max(p=>Math.Max(Math.Abs(p.X),Math.Abs(p.Y))));
        double roundoff=4*(Math.BitIncrement(scale)-scale);
        WorldPoint Canonical(WorldPoint p)=>new(WorldGeometryEvents.Canonical(p.X,anchorX,anchorX,roundoff,budget),
            WorldGeometryEvents.Canonical(p.Y,anchorY,anchorY,roundoff,budget));
        foreach(var edge in selected)
        {
            budget.Charge();int target=edge.DownstreamSampleIndex!.Value;
            double width=definition.BaseWidth+definition.WidthPerRate*edge.Outgoing;
            if(!double.IsFinite(width)||width>definition.MaximumWidth)throw new ArgumentException("WORLD_WATER_WIDTH_LIMIT:"+edge.SampleIndex);
            var envelope=envelopes[edge.SampleIndex];
            IReadOnlyList<WorldPoint> candidate=Clip(envelope.Vertices,fieldBoundary,budget);
            if(surface.Definition.Boundary is {} boundary&&ConvexParcelGeometry.IsConvex(boundary))candidate=Clip(candidate,boundary,budget);
            var retained=new List<(WorldPolygon Polygon,WorldSurfaceSample Sample)>();bool covered=true;
            if(candidate.Count>=3&&Math.Abs(WorldRegionGeometry.Area(candidate))>0)
            {
                var polygon=new WorldPolygon(candidate);budget.Charge((long)candidate.Count*candidate.Count);polygon.Validate();
                areas.VisitRegions(polygon,(sample,vertices)=>{
                    budget.Charge();if(sample.Status==WorldSurfaceStatus.Ambiguous)throw new ArgumentException("WORLD_WATER_AMBIGUOUS_TERRAIN");
                    if(vertices.Count<3||WorldRegionGeometry.Area(vertices)==0)return true;
                    if(sample.Status!=WorldSurfaceStatus.Sampled){covered=false;return true;}
                    // Validate retained polygons after proving whether the whole
                    // footprint is one plane; its internal sweep seams need not
                    // become output geometry in that case.
                    var cell=new WorldPolygon(vertices);
                    if(retained.Count>=16384)throw new ArgumentException("WORLD_WATER_CELL_BUDGET");
                    retained.Add((cell,sample));return true;
                },budget,token);
                // A proved uniform plane needs no triangle/slab seams in the
                // resulting water patch. Other pieces remain explicit and exact.
                if(covered&&retained.Count>0&&retained.All(c=>Plane(c.Sample)==Plane(retained[0].Sample)))
                    retained=[(polygon,retained[0].Sample)];
            }
            if(retained.Count==0)throw new ArgumentException("WORLD_WATER_EMPTY_SEGMENT:"+edge.SampleIndex);
            var segmentIds=new List<string>();double area=0;
            foreach(var cell in retained)
            {
                var polygon=new WorldPolygon(WorldRegionGeometry.Clip(cell.Polygon.Vertices.Select(Canonical).ToArray(),_=>1,budget));
                budget.Charge(1L+polygon.Vertices.Count*polygon.Vertices.Count);polygon.Validate();
                if(patches.Count>=definition.MaximumPatches||patches.Count+surface.Definition.Patches.Count>=1024)throw new ArgumentException("WORLD_WATER_PATCH_BUDGET");
                string id=definition.Id+"-"+edge.SampleIndex+"-"+segmentIds.Count;if(!ids.Add(id))throw new ArgumentException("WORLD_WATER_PATCH_ID_CONFLICT:"+id);
                var sample=cell.Sample;
                patches.Add(new(id,definition.Kind,definition.Purpose,definition.SourceRuleId,polygon,definition.Priority+patches.Count,
                    sample.Position,sample.Elevation!.Value,sample.GradientX!.Value,sample.GradientY!.Value));
                patchRanks.Add(id,sourcePriorities.GetValueOrDefault(sample.SourceId!,long.MinValue));
                segmentIds.Add(id);area+=Math.Abs(polygon.SignedArea());
            }
            double clipped=Math.Abs(envelope.SignedArea())-area;
            if(clipped< -1e-8*Math.Max(1,area))throw new ArgumentException("WORLD_WATER_AREA_ACCOUNTING");
            segments.Add(new(edge.SampleIndex,target,edge.Outgoing,width,envelope,segmentIds.AsReadOnly(),area,clipped));
        }
        // Preserve authored overlay precedence on shared closed boundaries;
        // fragment discovery order must not decide which elevation plane wins.
        budget.Charge(patches.Count*(long)Math.Ceiling(Math.Log2(Math.Max(2,patches.Count))));
        patches=patches.Select((patch,index)=>(patch,index)).OrderBy(p=>patchRanks[p.patch.Id]).ThenBy(p=>p.index)
            .Select((p,index)=>p.patch with{Priority=definition.Priority+index}).ToList();
        var retainedBodies=PlanRetained(drainage,definition,surface,patches,ids,budget,token);
        // Qualify ordinary surface limits and ambiguity before returning a plan.
        _=ApplyCore(surface.Definition,patches,token);
        return new(1,definition,WorldGenerationDependencyIdentity.Digest(drainage),segments.AsReadOnly(),patches.AsReadOnly(),
            drainage.Nodes.Count(n=>n.DownstreamSampleIndex is not null&&n.Outgoing<definition.MinimumFlow),budget.Used,retainedBodies);
    }

    public void Validate(WorldDrainagePlan drainage,WorldWaterPlan plan,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(plan);
        var expected=Plan(drainage,plan.Definition,token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(plan,WorldJson.CreateOptions()))throw new ArgumentException("WORLD_WATER_PLAN_MISMATCH");
    }
    public WorldSurfaceDefinition Apply(WorldDrainagePlan drainage,WorldWaterPlan plan,CancellationToken token=default)
    {
        Validate(drainage,plan,token);return ApplyCore(drainage.Surface,plan.Patches,token);
    }
    private static WorldSurfaceDefinition ApplyCore(WorldSurfaceDefinition source,IReadOnlyList<WorldTerrainPatch> water,CancellationToken token)
        =>new WorldSurfaceQuery(source with{Patches=source.Patches.Concat(water).ToArray()},token).Definition;
    private static (double X,double Y,double C) Plane(WorldSurfaceSample s)
        =>(s.GradientX!.Value,s.GradientY!.Value,s.Elevation!.Value-s.GradientX.Value*s.Position.X-s.GradientY.Value*s.Position.Y);
    private static IReadOnlyList<WorldPoint> Clip(IReadOnlyList<WorldPoint> polygon,WorldPolygon boundary,WorldSpatialWorkBudget budget)
    {
        double sign=Math.Sign(boundary.SignedArea());var result=polygon;
        for(int i=0;i<boundary.Vertices.Count&&result.Count>=3;i++)
        {
            var a=boundary.Vertices[i];var b=boundary.Vertices[(i+1)%boundary.Vertices.Count];
            result=WorldRegionGeometry.Clip(result,p=>sign*((b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X)),budget);
        }
        return result;
    }
}
