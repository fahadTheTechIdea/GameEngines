using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

public enum WorldTileChannelKind { Surface, Passage, Access, SiteFeature }
public sealed record WorldTileChannelSource(string Id,WorldTileChannelKind Kind,string SourceId,string Purpose,
    string SourceRuleId,string? ParentId,WorldPolygon? Boundary,WorldPolyline? Path,double? WidthMeters);
public readonly record struct WorldTileCell(int X,int Y);
public sealed record WorldTileTopologyRequest(int Version,string WorldSha256,WorldPoint Origin,
    double CellWidthMeters,double CellHeightMeters,double MaximumErrorBandMeters,
    int MaximumCells=500000,long MaximumWork=20000000);
public sealed record WorldTileChannel(WorldTileChannelSource Source,IReadOnlyList<WorldTileCell> Cells,int Components);
public sealed record WorldTileTopologyPlan(int Version,string Status,WorldTileTopologyRequest Request,
    double ErrorBandMeters,IReadOnlyList<WorldTileChannel> Channels,long Work,IReadOnlyList<string> RemainingGates);

/// <summary>Presentation sampling of existing continuous geometry. Does not lay out
/// the world or choose artwork. Godot owns terrain matching and native resources.</summary>
public sealed class WorldTileTopologyPlanner
{
    public static string Digest<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions())));

    internal static IReadOnlyList<WorldTileChannelSource> Sources(WorldMetadataDocument world)
    {
        var result=new List<WorldTileChannelSource>();
        foreach(var patch in world.Surface!.Patches)
            result.Add(new("surface:"+patch.Id,WorldTileChannelKind.Surface,patch.Id,patch.Purpose,patch.SourceRuleId,null,patch.Boundary,null,null));
        void Passage(string id,WorldRouteResult path,double radius)
        {
            if(path.Status!=WorldRouteStatus.Found||path.Path is null||radius<=0)
                throw new ArgumentException("WORLD_TILE_POSITIVE_PASSAGE_REQUIRED:"+id);
            result.Add(new("passage:"+id,WorldTileChannelKind.Passage,id,path.Purpose,path.SourceRuleId,null,null,path.Path,2*radius));
        }
        foreach(var route in world.RoutePlan?.Routes??[])
            Passage(route.ConnectionId,route.Forward,world.RoutePlan!.Rules.Single(r=>r.Id==route.RuleId).Profile.Radius);
        foreach(var network in world.NetworkPlan?.Networks??[])foreach(var edge in network.Plan.Edges)
            Passage(edge.Link.Id,edge.Forward,network.Plan.Options.Profile.Radius);
        foreach(var feature in world.PlacementPlan?.SiteFeatures??[])
            result.Add(new("site:"+feature.Id,WorldTileChannelKind.SiteFeature,feature.Id,feature.Purpose,feature.SourceRuleId,feature.NodeId,feature.Boundary,null,null));
        foreach(var site in world.AccessPlan?.Sites??[])foreach(var port in site.Ports)
            result.Add(new("access:"+site.NodeId+":"+port.Id,WorldTileChannelKind.Access,site.NodeId+":"+port.Id,
                port.Purpose,port.SourceRuleId,site.NodeId,MetadataPhysicalAccessPlanner.ApproachBoundary(port),new([port.BoundaryPosition,port.ApproachPosition]),port.Width));
        if(result.Count>10000||result.Select(s=>s.Id).Distinct(StringComparer.Ordinal).Count()!=result.Count)
            throw new ArgumentException("WORLD_TILE_CHANNEL_LIMIT_OR_DUPLICATE");
        return result.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();
    }

    public WorldTileTopologyPlan Plan(WorldMetadataDocument world,WorldTileTopologyRequest request,
        ISettlementCatalogRegistry? catalogs=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(world);ArgumentNullException.ThrowIfNull(request);
        world=JsonSerializer.Deserialize<WorldMetadataDocument>(JsonSerializer.Serialize(world,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        if(request.Version!=1||request.WorldSha256!=Digest(world)||world.Space.Unit!="meters"||world.Space.Axes.Count!=2||
            world.Surface?.Boundary is null||world.Surface.HeightField is not null||world.Surface.Terrain is not null||
            !double.IsFinite(request.Origin.X)||!double.IsFinite(request.Origin.Y)||Math.Abs(request.Origin.X)>1e7||Math.Abs(request.Origin.Y)>1e7||
            !double.IsFinite(request.CellWidthMeters)||!double.IsFinite(request.CellHeightMeters)||
            request.CellWidthMeters is <.001 or >1024||request.CellHeightMeters is <.001 or >1024||
            !double.IsFinite(request.MaximumErrorBandMeters)||request.MaximumErrorBandMeters is <=0 or >1024||
            request.MaximumCells is <1 or >1000000||request.MaximumWork is <1 or >200000000)
            throw new ArgumentException("WORLD_TILE_REQUEST_INVALID_OR_STALE");
        double error=Math.Sqrt(request.CellWidthMeters*request.CellWidthMeters+request.CellHeightMeters*request.CellHeightMeters)/2;
        if(error>request.MaximumErrorBandMeters)throw new ArgumentException("WORLD_TILE_RESOLUTION_EXCEEDS_ERROR_BAND");
        WorldMetadataGenerator.ValidateDocument(world,catalogs,token);
        var surface=new WorldSurfaceQuery(world.Surface,token);var sources=Sources(world);
        var budget=new WorldSpatialWorkBudget(request.MaximumWork,token);long cellCount=0;
        int sampleCost=1+(world.Surface.Boundary?.Vertices.Count??0)+world.Surface.Patches.Sum(p=>p.Boundary.Vertices.Count)+world.Surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count);
        var output=new List<WorldTileChannel>();
        WorldPoint Center(WorldTileCell c)=>new(request.Origin.X+(c.X+.5)*request.CellWidthMeters,request.Origin.Y+(c.Y+.5)*request.CellHeightMeters);
        int Coordinate(double point,double origin,double size)
        {
            double value=Math.Floor((point-origin)/size);
            if(value<int.MinValue+1||value>int.MaxValue-1)throw new ArgumentException("WORLD_TILE_COORDINATE_LIMIT");
            return(int)value;
        }
        foreach(var source in sources)
        {
            token.ThrowIfCancellationRequested();var points=source.Boundary?.Vertices??source.Path!.Points;
            double radius=source.Boundary is not null?0:source.WidthMeters!.Value/2;
            int x0=Coordinate(points.Min(p=>p.X)-radius,request.Origin.X,request.CellWidthMeters),
                x1=Coordinate(points.Max(p=>p.X)+radius,request.Origin.X,request.CellWidthMeters),
                y0=Coordinate(points.Min(p=>p.Y)-radius,request.Origin.Y,request.CellHeightMeters),
                y1=Coordinate(points.Max(p=>p.Y)+radius,request.Origin.Y,request.CellHeightMeters);
            long columns=(long)x1-x0+1,rows=(long)y1-y0+1;
            if(columns>request.MaximumCells*4L||rows>request.MaximumCells*4L)throw new ArgumentException("WORLD_TILE_CANDIDATE_LIMIT:"+source.Id);
            long candidates=columns*rows;
            if(candidates<0||candidates>request.MaximumCells*4L)throw new ArgumentException("WORLD_TILE_CANDIDATE_LIMIT:"+source.Id);
            var cells=new HashSet<WorldTileCell>();
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                budget.Charge(sampleCost+points.Count);var cell=new WorldTileCell(x,y);var center=Center(cell);
                var sample=surface.Sample(center,token);
                if(sample.Status==WorldSurfaceStatus.Ambiguous)throw new ArgumentException("WORLD_TILE_AMBIGUOUS_SURFACE");
                if(sample.Status!=WorldSurfaceStatus.Sampled)continue;
                bool inside=source.Kind==WorldTileChannelKind.Surface?sample.SourceId==source.SourceId:
                    source.Boundary is {} polygon?polygon.ContainsPoint(center):Distance(source.Path!,center)<=radius+1e-10;
                if(!inside)continue;
                cells.Add(cell);if(++cellCount>request.MaximumCells)throw new ArgumentException("WORLD_TILE_CELL_LIMIT");
            }
            if(cells.Count==0)throw new ArgumentException("WORLD_TILE_TARGET_HAS_NO_CELLS:"+source.Id);
            int components=0;var remaining=new HashSet<WorldTileCell>(cells);var queue=new Queue<WorldTileCell>();
            while(remaining.Count>0)
            {
                components++;var first=remaining.First();remaining.Remove(first);queue.Enqueue(first);
                while(queue.TryDequeue(out var cell))
                {
                    budget.Charge(4);
                    foreach(var next in new[]{new WorldTileCell(cell.X+1,cell.Y),new(cell.X-1,cell.Y),new(cell.X,cell.Y+1),new(cell.X,cell.Y-1)})
                        if(remaining.Remove(next))queue.Enqueue(next);
                }
            }
            if(source.Kind is WorldTileChannelKind.Passage or WorldTileChannelKind.Access)
            {
                if(components!=1)throw new ArgumentException("WORLD_TILE_PASSAGE_DISCONNECTED:"+source.Id);
                if(source.Path is {} path)foreach(var point in path.Points)
                {
                    var cell=new WorldTileCell(Coordinate(point.X,request.Origin.X,request.CellWidthMeters),Coordinate(point.Y,request.Origin.Y,request.CellHeightMeters));
                    // A source point may lie on a tile edge/corner; any selected
                    // incident cell is sufficient, but a nearby fallback is not.
                    bool touched=false;
                    foreach(var c in new[]{cell,new(cell.X-1,cell.Y),new(cell.X,cell.Y-1),new(cell.X-1,cell.Y-1)})
                    {
                        var center=Center(c);if(cells.Contains(c)&&Math.Abs(center.X-point.X)<=request.CellWidthMeters/2+1e-9&&
                            Math.Abs(center.Y-point.Y)<=request.CellHeightMeters/2+1e-9)touched=true;
                    }
                    if(!touched)throw new ArgumentException("WORLD_TILE_PATH_VERTEX_UNREPRESENTED:"+source.Id);
                }
            }
            output.Add(new(source,cells.OrderBy(c=>c.Y).ThenBy(c=>c.X).ToArray(),components));
        }
        return new(1,"PresentationMasksDraft",request,error,output,budget.Used,
            ["Center-classification error band is not a Hausdorff, minimum-width or visual-fidelity certificate",
             "Cross-channel topology, native collision/navigation clearance and whole-map visual acceptance",
             "Godot-native terrain combination inventory and complete approved transition artwork",
             "Native production compilation and save/reload"]);
    }

    public void Validate(WorldMetadataDocument world,WorldTileTopologyPlan plan,ISettlementCatalogRegistry? catalogs=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if(Digest(Plan(world,plan.Request,catalogs,token))!=Digest(plan))throw new ArgumentException("WORLD_TILE_PLAN_REPLAY_MISMATCH");
    }

    private static double Distance(WorldPolyline line,WorldPoint point)
    {
        double best=double.PositiveInfinity;
        for(int i=1;i<line.Points.Count;i++)
        {
            var a=line.Points[i-1];var b=line.Points[i];double dx=b.X-a.X,dy=b.Y-a.Y;
            double t=Math.Clamp(((point.X-a.X)*dx+(point.Y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
            best=Math.Min(best,point.DistanceTo(new(a.X+t*dx,a.Y+t*dy)));
        }
        return best;
    }
}
