using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public enum WorldRetainedShorelinePolicy { RequireTerrainShoreline, ClipToBoundary }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldRetainedWaterDefinition(string Id,string TerminalId,string Purpose,string SourceRuleId,double Level,
    WorldPolygon Boundary,IReadOnlyList<string> InundatableKinds,double MinimumArea=.001,double MinimumReceived=0,
    WorldRetainedShorelinePolicy ShorelinePolicy=WorldRetainedShorelinePolicy.RequireTerrainShoreline,double ShorelineTolerance=1e-8);
public sealed record WorldRetainedWaterBedFace(int CellIndex,WorldPoint Origin,double Elevation,double GradientX,double GradientY,
    double MinimumDepth,double MaximumDepth,double Capacity);
public sealed record WorldRetainedWaterBody(WorldRetainedWaterDefinition Definition,double Received,WorldRegion Region,
    IReadOnlyList<WorldRetainedWaterBedFace> BedFaces,IReadOnlyList<string> PatchIds,double Capacity,
    int UnresolvedShorelineSpans,double MaximumShorelineDeficit,bool BoundaryClipped);

public sealed partial class WorldWaterPlanner
{
    private static WorldWaterDefinition SnapshotRetained(WorldWaterDefinition definition,CancellationToken token)
    {
        if(definition.RetainedBasins is null)return definition;
        if(definition.RetainedBasins.Count>16)throw new ArgumentException("WORLD_RETAINED_WATER_BODY_BUDGET");
        var ids=new HashSet<string>(StringComparer.Ordinal);var bodies=new List<WorldRetainedWaterDefinition>();
        foreach(var body in definition.RetainedBasins)
        {
            token.ThrowIfCancellationRequested();
            if(body is null||!WorldEnvironmentQuery.Id(body.Id)||body.Id.Length>40||!ids.Add(body.Id)||!WorldEnvironmentQuery.Id(body.TerminalId)||
                !WorldEnvironmentQuery.Text(body.Purpose)||!WorldEnvironmentQuery.Text(body.SourceRuleId)||!WorldHeightFieldQuery.Number(body.Level)||
                !double.IsFinite(body.MinimumArea)||body.MinimumArea is <=0 or >4e14||!double.IsFinite(body.MinimumReceived)||body.MinimumReceived is <0 or >1e15||
                !Enum.IsDefined(body.ShorelinePolicy)||!double.IsFinite(body.ShorelineTolerance)||body.ShorelineTolerance is <0 or >.001||
                body.InundatableKinds is not {Count:>0 and <=32}||body.InundatableKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||
                body.InundatableKinds.Distinct(StringComparer.Ordinal).Count()!=body.InundatableKinds.Count||body.Boundary?.Vertices is not {Count:>=3 and <=64}||
                body.Boundary.Vertices.Any(p=>!WorldHeightFieldQuery.Point(p)))throw new ArgumentException("WORLD_RETAINED_WATER_DEFINITION_INVALID");
            var boundary=new WorldPolygon(Array.AsReadOnly(body.Boundary.Vertices.ToArray()));boundary.Validate();
            bodies.Add(body with{Boundary=boundary,InundatableKinds=Array.AsReadOnly(body.InundatableKinds.Order(StringComparer.Ordinal).ToArray())});
        }
        return definition with{RetainedBasins=Array.AsReadOnly(bodies.OrderBy(b=>b.Id,StringComparer.Ordinal).ToArray())};
    }
    private static IReadOnlyList<WorldRetainedWaterBody>? PlanRetained(WorldDrainagePlan drainage,WorldWaterDefinition definition,
        WorldSurfaceQuery surface,List<WorldTerrainPatch> patches,HashSet<string> ids,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition.RetainedBasins is null)return null;
        var bodies=new List<WorldRetainedWaterBody>();var occupied=new List<WorldPolygon>();
        var field=surface.HeightField?.CompleteField??throw new ArgumentException("WORLD_WATER_HEIGHTFIELD_REQUIRED");
        long vertices=(surface.Definition.Boundary?.Vertices.Count??0)+surface.Definition.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+
            surface.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count);
        foreach(var body in definition.RetainedBasins)
        {
            budget.Charge(drainage.Definition.Terminals.Count+drainage.Nodes.Count+drainage.Terminals.Count);
            var terminal=drainage.Definition.Terminals.SingleOrDefault(t=>t.Id==body.TerminalId);
            if(terminal?.Kind!=WorldDrainageTerminalKind.RetainedBasin)throw new ArgumentException("WORLD_RETAINED_WATER_TERMINAL_REQUIRED:"+body.Id);
            var node=drainage.Nodes.Single(n=>n.SampleIndex==terminal.Row*field.Columns+terminal.Column);
            double received=drainage.Terminals.Single(t=>t.TerminalId==terminal.Id).Received;
            if(received<body.MinimumReceived)throw new ArgumentException("WORLD_RETAINED_WATER_RECEIPT_MINIMUM:"+body.Id);
            if(node.Elevation>=body.Level)throw new ArgumentException("WORLD_RETAINED_WATER_SEED_NOT_SUBMERGED:"+body.Id);
            var depthEnvironment=new WorldEnvironmentQuery(surface,new(1,"retained-water-fields",
                [new("retained-depth",body.Purpose,body.SourceRuleId,surface.Definition.Unit,body.Level,[new(WorldFieldInput.Elevation,-1)])]),token);
            // The least representable positive value expresses strict wetness.
            // Zero-depth plateaus and saddle seams are not occupied connectors;
            // positive wet faces retain the common exact contour geometry.
            var profile=new WorldSuitabilityProfile("retained-water-bed",body.Purpose,body.SourceRuleId,body.InundatableKinds,
                [new("below-level","retained-depth",body.Purpose,body.SourceRuleId,double.Epsilon,1000000000)],[]);
            var request=new WorldRegionRequest("retained-water-region",body.Purpose,body.SourceRuleId,body.Boundary,profile,
                [new(body.Id,body.Purpose,body.SourceRuleId,node.Position,body.MinimumArea)],definition.MaximumPatches,definition.MaximumWork);
            var region=CanonicalRetainedRegion(surface,body,new WorldRegionPlanner().Plan(depthEnvironment,request,budget,token).Regions.Single(),
                depthEnvironment,profile,budget,token);
            foreach(var cell in region.Cells)foreach(var previous in occupied)
            {
                budget.Charge(1+cell.Vertices.Count+previous.Vertices.Count);
                if(cell.Vertices.Max(p=>p.X)<=previous.Vertices.Min(p=>p.X)||previous.Vertices.Max(p=>p.X)<=cell.Vertices.Min(p=>p.X)||
                    cell.Vertices.Max(p=>p.Y)<=previous.Vertices.Min(p=>p.Y)||previous.Vertices.Max(p=>p.Y)<=cell.Vertices.Min(p=>p.Y))continue;
                IReadOnlyList<WorldPoint> intersection=cell.Vertices;double sign=Math.Sign(previous.SignedArea());
                for(int i=0;i<previous.Vertices.Count&&intersection.Count>=3;i++)
                {
                    var a=previous.Vertices[i];var b=previous.Vertices[(i+1)%previous.Vertices.Count];
                    intersection=WorldRegionGeometry.ClipForMeasure(intersection,p=>sign*((b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X)),budget);
                }
                if(Math.Abs(WorldRegionGeometry.Area(intersection))>0)throw new ArgumentException("WORLD_RETAINED_WATER_OVERLAP:"+body.Id);
            }
            int unresolved=0;double deficit=0;
            foreach(var edge in region.BoundaryEdges)
            {
                budget.Charge(8*(vertices+1)*(vertices+1)+8*(field.Columns+field.Rows)*(vertices+1));
                foreach(var span in surface.TraceTerrain(edge.From,edge.To,token))
                {
                    budget.Charge();var sample=span.Sample;
                    if(sample.Status!=WorldSurfaceStatus.Sampled){unresolved++;continue;}
                    double Height(WorldPoint p)=>sample.Elevation!.Value+sample.GradientX!.Value*(p.X-sample.Position.X)+sample.GradientY!.Value*(p.Y-sample.Position.Y);
                    deficit=Math.Max(deficit,body.Level-Math.Min(Height(span.From),Height(span.To)));
                }
            }
            bool clipped=unresolved>0||deficit>body.ShorelineTolerance;
            if(clipped&&body.ShorelinePolicy==WorldRetainedShorelinePolicy.RequireTerrainShoreline)
                throw new ArgumentException("WORLD_RETAINED_WATER_OPEN_BOUNDARY:"+body.Id);
            var bedFaces=new List<WorldRetainedWaterBedFace>();var patchIds=new List<string>();double capacity=0;
            for(int i=0;i<region.Cells.Count;i++)
            {
                var cell=region.Cells[i];budget.Charge(1+vertices+cell.Vertices.Count);var center=cell.Centroid();var sample=surface.SampleTerrain(center,token);
                if(sample.Status!=WorldSurfaceStatus.Sampled)throw new ArgumentException("WORLD_RETAINED_WATER_BED_MISSING:"+body.Id);
                double Depth(WorldPoint p)=>body.Level-(sample.Elevation!.Value+sample.GradientX!.Value*(p.X-center.X)+sample.GradientY!.Value*(p.Y-center.Y));
                double min=cell.Vertices.Min(Depth),max=cell.Vertices.Max(Depth),volume=Math.Abs(WorldRegionGeometry.Area(cell.Vertices))*Depth(center);
                if(min< -body.ShorelineTolerance||volume<=0)throw new ArgumentException("WORLD_RETAINED_WATER_DEPTH_RESOLUTION:"+body.Id);
                bedFaces.Add(new(i,center,sample.Elevation!.Value,sample.GradientX!.Value,sample.GradientY!.Value,min,max,volume));capacity+=volume;
                if(patches.Count>=definition.MaximumPatches||patches.Count+surface.Definition.Patches.Count>=1024)throw new ArgumentException("WORLD_WATER_PATCH_BUDGET");
                string id=definition.Id+"-lake-"+body.Id+"-"+i;if(!ids.Add(id))throw new ArgumentException("WORLD_WATER_PATCH_ID_CONFLICT:"+id);
                patchIds.Add(id);patches.Add(new(id,definition.Kind,body.Purpose,body.SourceRuleId,cell,definition.Priority+patches.Count,center,body.Level,0,0));
            }
            occupied.AddRange(region.Cells);
            bodies.Add(new(body,received,region,bedFaces.AsReadOnly(),patchIds.AsReadOnly(),capacity,unresolved,deficit,clipped));
        }
        return bodies.AsReadOnly();
    }
    private static WorldRegion CanonicalRetainedRegion(WorldSurfaceQuery surface,WorldRetainedWaterDefinition body,WorldRegion region,
        WorldEnvironmentQuery environment,WorldSuitabilityProfile profile,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var field=surface.HeightField?.CompleteField??throw new ArgumentException("WORLD_WATER_HEIGHTFIELD_REQUIRED");
        var original=surface.Definition.Patches.Select(p=>p.Boundary).Concat(surface.Definition.Exclusions.Select(e=>e.Boundary)).Append(body.Boundary)
            .Concat(surface.Definition.Boundary is {} boundary?new[]{boundary}:[]).SelectMany(p=>p.Vertices).ToArray();
        var generated=region.Cells.SelectMany(c=>c.Vertices).ToArray();
        int count=original.Length+generated.Length+field.Columns+field.Rows;
        budget.Charge(count*(long)Math.Ceiling(Math.Log2(Math.Max(2,count))));
        var authoredX=new SortedSet<double>(original.Select(p=>p.X));var authoredY=new SortedSet<double>(original.Select(p=>p.Y));
        for(int c=0;c<field.Columns;c++)authoredX.Add(field.Origin.X+c*field.StepX);
        for(int r=0;r<field.Rows;r++)authoredY.Add(field.Origin.Y+r*field.StepY);
        double scale=Math.Max(1,original.Concat(generated).Max(p=>Math.Max(Math.Abs(p.X),Math.Abs(p.Y))));
        double tolerance=4*(Math.BitIncrement(scale)-scale);
        Dictionary<double,double> Map(IEnumerable<double> values,SortedSet<double> authored)
        {
            var existing=new SortedSet<double>(authored);var map=new Dictionary<double,double>();
            foreach(double value in values.Distinct().Order())
            {
                double canonical=WorldGeometryEvents.Canonical(value,existing,authored,tolerance,budget);
                existing.Add(canonical);map.Add(value,canonical);
            }
            return map;
        }
        // Only derived contour events are canonicalized. Original source axes
        // remain distinct, using the same operation precision as area sweeps.
        var xs=Map(generated.Select(p=>p.X),authoredX);var ys=Map(generated.Select(p=>p.Y),authoredY);
        var cells=region.Cells.Select(cell=>{
            var points=cell.Vertices.Select(p=>new WorldPoint(xs[p.X],ys[p.Y])).ToArray();
            if(WorldRegionGeometry.Area(points)==0)throw new ArgumentException("WORLD_RETAINED_WATER_GEOMETRY_RESOLUTION");
            var polygon=new WorldPolygon(WorldRegionGeometry.Clip(points,_=>1,budget));polygon.Validate();return polygon;
        }).ToArray();
        return WorldRegionGeometry.Assemble(environment,profile,cells,[region.Seed],budget,token,requireAllCells:true).Single();
    }
}
