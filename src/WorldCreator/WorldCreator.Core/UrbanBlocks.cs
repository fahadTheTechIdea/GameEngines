using System.Security.Cryptography;
using System.Text.Json;

namespace WorldCreator.Core.Geometry;

/// <summary>A directed street-facing edge of a block, with its exact road-side reservation.</summary>
public sealed record UrbanBlockFrontage(string RoadId, WorldPoint RoadStart, WorldPoint RoadEnd,
    WorldPoint BuildableStart, WorldPoint BuildableEnd, double OffsetMeters);
public sealed record UrbanBlock(string Id, IReadOnlyList<WorldPoint> Boundary, double AreaSquareMeters,
    WorldPolygon? StreetBoundary = null, IReadOnlyList<UrbanBlockFrontage>? Frontages = null);
public sealed record UrbanBlockResult(IReadOnlyList<UrbanBlock> Blocks, IReadOnlyList<string> Diagnostics);
public sealed record UrbanStreetFaceEdge(string RoadId,WorldPoint Start,WorldPoint End,double OffsetMeters);
/// <summary>Unreserved street-centerline face, for planning topology changes before road-width inset.</summary>
public sealed record UrbanStreetFace(string Id,WorldPolygon Boundary,IReadOnlyList<UrbanStreetFaceEdge> Edges);
public sealed record UrbanStreetFaceResult(IReadOnlyList<UrbanStreetFace> Faces,IReadOnlyList<string> Diagnostics);

/// <summary>Traverses bounded planar street faces and reserves road-side widths.
/// Non-simple faces, holes and offset topology changes are diagnosed, never replaced by rectangles.</summary>
public static partial class UrbanBlockExtractor
{
    private const double Tolerance = 1e-7;
    public static UrbanBlockResult Extract(IReadOnlyList<StreetCorridor> corridors,
        int maximumBlocks = 256, CancellationToken token = default)
    {
        var raw=ExtractStreetFaces(corridors,maximumBlocks,token);
        return raw.Diagnostics.Count!=0?new([],raw.Diagnostics):ReserveStreetFaces(raw.Faces,token);
    }
    public static UrbanBlockResult ReserveStreetFaces(IReadOnlyList<UrbanStreetFace> faces,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(faces);
        if(faces.Count>4096)return new([],["BLOCK_BUDGET_EXCEEDED"]);
        var blocks=new List<UrbanBlock>();var issues=new List<string>();
        foreach(var face in faces)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                ArgumentNullException.ThrowIfNull(face);ArgumentNullException.ThrowIfNull(face.Boundary);
                var boundary=face.Boundary;boundary.Validate();
                if(face.Edges is null || face.Edges.Count!=boundary.Vertices.Count || face.Edges.Where((e,i)=>
                    e is null||string.IsNullOrWhiteSpace(e.RoadId)||!double.IsFinite(e.OffsetMeters)||e.OffsetMeters<=0||
                    e.Start!=boundary.Vertices[i]||e.End!=boundary.Vertices[(i+1)%boundary.Vertices.Count]).Any())
                    throw new ArgumentException("INVALID_STREET_FACE_EDGES");
                var inset=Inset(boundary,face.Edges.Select(e=>e.OffsetMeters).ToArray(),token);
                var frontages=face.Edges.Select((e,i)=>new UrbanBlockFrontage(e.RoadId,e.Start,e.End,
                    inset.Vertices[i],inset.Vertices[(i+1)%inset.Vertices.Count],e.OffsetMeters)).ToArray();
                string digest=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(frontages)));
                blocks.Add(new("street-block-"+digest[..20],inset.Vertices,Math.Abs(inset.SignedArea()),boundary,frontages));
            }
            catch(ArgumentException exception){issues.Add("BLOCK_FACE_INVALID:"+exception.Message);}
            catch(InvalidOperationException exception){issues.Add("BLOCK_FACE_INVALID:"+exception.Message);}
        }
        return issues.Count!=0?new([],issues):new(blocks.OrderBy(b=>b.StreetBoundary!.Vertices[0].X)
            .ThenBy(b=>b.StreetBoundary!.Vertices[0].Y).ThenBy(b=>b.Id,StringComparer.Ordinal).ToArray(),[]);
    }
    public static UrbanStreetFaceResult ExtractStreetFaces(IReadOnlyList<StreetCorridor> corridors,
        int maximumBlocks=256,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(corridors);
        if (maximumBlocks is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(maximumBlocks));
        token.ThrowIfCancellationRequested();
        if(corridors.Count>4096)return new([], ["BLOCK_CORRIDOR_BUDGET_EXCEEDED"]);
        foreach(var road in corridors)
        {
            token.ThrowIfCancellationRequested();
            if(road?.Centerline?.Points is null || road.Profile is null ||
                road.Centerline.Points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>10000000||Math.Abs(p.Y)>10000000))
                throw new ArgumentException("BLOCK_ROAD_INVALID");
        }
        var topology = StreetTopologyBuilder.Build(corridors.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray(), token: token);
        if (topology.Diagnostics.Count != 0) return new([], topology.Diagnostics);
        if (topology.Edges.Count > 16000) return new([], ["BLOCK_GRAPH_BUDGET_EXCEEDED"]);
        var nodes = topology.Nodes.ToDictionary(n => n.Id, n => n.Location, StringComparer.Ordinal);
        var roads = corridors.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var outgoing = nodes.Keys.ToDictionary(id => id, _ => new List<int>(), StringComparer.Ordinal);
        var arcs = topology.Edges.SelectMany(e => new[] {
            new FaceArc(e.FromId,e.ToId,e.CorridorId,false),
            new FaceArc(e.ToId,e.FromId,e.CorridorId,true) }).ToArray();
        for (int i = 0; i < arcs.Length; i++) outgoing[arcs[i].From].Add(i);
        foreach (var (id, values) in outgoing)
        {
            token.ThrowIfCancellationRequested();
            var origin = nodes[id];
            values.Sort((a,b) => {
                var p = nodes[arcs[a].To]; var q = nodes[arcs[b].To];
                int order = Math.Atan2(p.Y-origin.Y,p.X-origin.X).CompareTo(Math.Atan2(q.Y-origin.Y,q.X-origin.X));
                return order != 0 ? order : StringComparer.Ordinal.Compare(arcs[a].RoadId,arcs[b].RoadId);
            });
        }
        var next = new int[arcs.Length];
        for (int i = 0; i < arcs.Length; i++)
        {
            var candidates = outgoing[arcs[i].To]; int twin = candidates.IndexOf(i ^ 1);
            next[i] = candidates[(twin+candidates.Count-1)%candidates.Count];
        }
        var visited = new bool[arcs.Length]; var faces = new List<UrbanStreetFace>(); var issues = new List<string>();
        for (int start = 0; start < arcs.Length; start++)
        {
            token.ThrowIfCancellationRequested();
            if (visited[start]) continue;
            var path = new List<int>(); int current = start;
            do
            {
                if (visited[current]) return new([], ["BLOCK_FACE_TRAVERSAL_INVALID"]);
                visited[current] = true; path.Add(current); current = next[current];
                token.ThrowIfCancellationRequested();
            } while (current != start);
            var points = path.Select(i => nodes[arcs[i].From]).ToArray();
            // Discard only the exterior/zero-area walks, never a failed bounded face.
            if (points.Length < 3 || StreetSurfaceBuilder.SignedArea(points) <= Tolerance) continue;
            // Degree-two or external-junction points on the same straight road are
            // topology terminals, not polygon corners. Keeping a point closer to a
            // corner than its inset miter can reverse a tiny offset edge.
            bool simplified;
            do
            {
                simplified=false;
                for(int i=0;i<path.Count&&path.Count>3;i++)
                {
                    int previous=(i+path.Count-1)%path.Count,nextPoint=(i+1)%path.Count;
                    if(arcs[path[previous]].RoadId!=arcs[path[i]].RoadId || arcs[path[previous]].Reversed!=arcs[path[i]].Reversed)continue;
                    var a=nodes[arcs[path[previous]].From];var b=nodes[arcs[path[i]].From];var c=nodes[arcs[path[nextPoint]].From];
                    double ux=b.X-a.X,uy=b.Y-a.Y,vx=c.X-b.X,vy=c.Y-b.Y;
                    if(ux*vx+uy*vy>0 && Math.Abs(ux*vy-uy*vx)<=1e-10*Math.Sqrt((ux*ux+uy*uy)*(vx*vx+vy*vy)))
                    {path.RemoveAt(i);simplified=true;break;}
                }
            }while(simplified);
            points=path.Select(i=>nodes[arcs[i].From]).ToArray();
            if (points.Length > 1024) return new([], ["BLOCK_FACE_VERTEX_BUDGET_EXCEEDED"]);
            int first = Enumerable.Range(0,points.Length).OrderBy(i => points[i].X).ThenBy(i => points[i].Y).First();
            path = path.Skip(first).Concat(path.Take(first)).ToList();
            points = path.Select(i => nodes[arcs[i].From]).ToArray();
            try
            {
                var boundary = new WorldPolygon(points); boundary.Validate();
                var edges=path.Select((arc,i)=>new UrbanStreetFaceEdge(arcs[arc].RoadId,points[i],points[(i+1)%points.Length],
                    RoadsideWidth(roads[arcs[arc].RoadId].Profile,arcs[arc].Reversed))).ToArray();
                string digest=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(edges)));
                faces.Add(new("street-face-"+digest[..20],boundary,edges));
                if (faces.Count > maximumBlocks) return new([], ["BLOCK_BUDGET_EXCEEDED"]);
            }
            catch (ArgumentException exception) { issues.Add("BLOCK_FACE_INVALID:"+exception.Message); }
            catch (InvalidOperationException exception) { issues.Add("BLOCK_FACE_INVALID:"+exception.Message); }
        }
        // Extraction is atomic: diagnostics cannot accompany an apparently complete partial map.
        return issues.Count != 0 ? new([],issues) : new(faces.OrderBy(b=>b.Boundary.Vertices[0].X)
            .ThenBy(b=>b.Boundary.Vertices[0].Y).ThenBy(b=>b.Id,StringComparer.Ordinal).ToArray(),[]);
    }
    private sealed record FaceArc(string From,string To,string RoadId,bool Reversed);
    private static double RoadsideWidth(RoadCrossSection p,bool reversed) =>
        (p.LaneWidthMeters*p.LaneCount+p.MedianMeters)/2 +
        (reversed?p.CurbRightMeters+p.SidewalkRightMeters:p.CurbLeftMeters+p.SidewalkLeftMeters);
}
