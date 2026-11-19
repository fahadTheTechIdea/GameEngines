using System.Numerics;
using System.Text.Json;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Streaming exact convex union with an explicit representation purpose.
/// TerrainFaces preserves source planes; Membership preserves eligibility and
/// source provenance while original field values remain in their query owners.
/// The retained-reference
/// budget bounds the active frontier as well as the final representation.</summary>
internal sealed class WorldRegionFaces(int maximumReferences,WorldSpatialWorkBudget budget,WorldRegionRepresentation representation=WorldRegionRepresentation.TerrainFaces)
{
    private static readonly JsonSerializerOptions options=WorldJson.CreateOptions();
    private sealed record Face(int Id,string Key,uint Profiles,WorldPolygon Polygon);
    private readonly Dictionary<int,Face> faces=[];
    private readonly Dictionary<(string Key,WorldPoint From,WorldPoint To),Face> edges=[];
    private int nextId,references;

    internal void Add(WorldSurfaceSample sample,WorldEnvironmentRegions.Piece piece,WorldPolygon polygon,uint profiles)
    {
        bool membership=representation==WorldRegionRepresentation.Membership;
        budget.Charge((membership?8:16+piece.Fields.Count*12)+polygon.Vertices.Count);
        string Constant(double value,double x,double y)=>WorldRegionExact.Constant(value,x,y,sample.Position);
        var key=membership?JsonSerializer.Serialize(new{profiles,sample.SourceId,sample.SourceRuleId,sample.Kind},options):
            JsonSerializer.Serialize(new{profiles,sample.SourceId,sample.SourceRuleId,sample.Kind,
                Terrain=new{C=Constant(sample.Elevation!.Value,sample.GradientX!.Value,sample.GradientY!.Value),X=sample.GradientX,Y=sample.GradientY},
                Fields=piece.Fields.OrderBy(f=>f.Key,StringComparer.Ordinal).Select(f=>new{Id=f.Key,C=Constant(f.Value.Constant,f.Value.X,f.Value.Y),f.Value.X,f.Value.Y}).ToArray()},options);
        var current=new Face(nextId++,key,profiles,polygon);
        while(true)
        {
            Face? partner=null;WorldPolygon? union=null;
            for(int i=0;i<current.Polygon.Vertices.Count;i++)
            {
                budget.Charge();var a=current.Polygon.Vertices[i];var b=current.Polygon.Vertices[(i+1)%current.Polygon.Vertices.Count];
                if(!edges.TryGetValue((key,b,a),out var candidate))continue;
                union=Union(current.Polygon,i,candidate.Polygon,budget);
                if(union is not null){partner=candidate;break;}
            }
            if(partner is null)break;
            Remove(partner);current=current with{Id=Math.Min(current.Id,partner.Id),Polygon=union!};
        }
        int count=BitOperations.PopCount(profiles);
        if(references+count>maximumReferences)throw new ArgumentException("WORLD_REGION_CELL_BUDGET");
        references+=count;faces.Add(current.Id,current);
        foreach(var (from,to) in Edges(current.Polygon))
        {budget.Charge();if(!edges.TryAdd((key,from,to),current))throw new ArgumentException("WORLD_REGION_FACE_TOPOLOGY_INVALID");}
    }

    internal IReadOnlyList<(WorldPolygon Polygon,uint Profiles)> Complete()
    {
        budget.Charge(faces.Count*(long)(1+Math.Ceiling(Math.Log2(faces.Count+1d))));
        return faces.Values.OrderBy(f=>f.Id).Select(f=>(f.Polygon,f.Profiles)).ToArray();
    }

    private void Remove(Face face)
    {
        budget.Charge();faces.Remove(face.Id);references-=BitOperations.PopCount(face.Profiles);
        foreach(var (from,to) in Edges(face.Polygon)){budget.Charge();edges.Remove((face.Key,from,to));}
    }

    private static IEnumerable<(WorldPoint From,WorldPoint To)> Edges(WorldPolygon polygon)
        =>polygon.Vertices.Select((p,i)=>(p,polygon.Vertices[(i+1)%polygon.Vertices.Count]));

    private static WorldPolygon? Union(WorldPolygon a,int edge,WorldPolygon b,WorldSpatialWorkBudget budget)
    {
        var av=a.Vertices;var bv=b.Vertices;int other=-1;
        for(int i=0;i<bv.Count;i++)
        {budget.Charge();if(bv[i]==av[(edge+1)%av.Count]&&bv[(i+1)%bv.Count]==av[edge]){other=i;break;}}
        if(other<0)return null;
        var points=new List<WorldPoint>();
        for(int i=0;i<av.Count;i++)points.Add(av[(edge+1+i)%av.Count]);
        for(int i=0;i<bv.Count-2;i++)points.Add(bv[(other+2+i)%bv.Count]);
        // Exact zero alone removes a redundant collinear vertex. No coordinate
        // snapping, area tolerance or convex hull can fill a gap or concavity.
        bool removed;
        do
        {
            removed=false;
            for(int i=0;i<points.Count&&points.Count>3;i++)
            {
                budget.Charge();var before=points[(i+points.Count-1)%points.Count];var current=points[i];var after=points[(i+1)%points.Count];
                if(WorldRegionExact.Orientation(before,current,after)==0){points.RemoveAt(i);removed=true;break;}
            }
        }while(removed);
        if(points.Count>64||points.Distinct().Count()!=points.Count)return null;
        for(int i=0;i<points.Count;i++)
        {budget.Charge();if(WorldRegionExact.Orientation(points[i],points[(i+1)%points.Count],points[(i+2)%points.Count])<=0)return null;}
        budget.Charge((long)points.Count*points.Count);
        var polygon=new WorldPolygon(Array.AsReadOnly(points.ToArray()));polygon.Validate();return polygon;
    }

}
