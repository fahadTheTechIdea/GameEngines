namespace WorldCreator.Core.Geometry;

/// <summary>Bounded immutable arc-length sampling shared by metadata and visual grammars.
/// At an exact bend the incoming segment owns its normal. Offsets are planar only.</summary>
public sealed class WorldArcLengthPath
{
    private readonly double[][] points;
    private readonly double[] lengths;
    public double Length {get;}
    public int Dimensions {get;}
    public WorldArcLengthPath(IReadOnlyList<IReadOnlyList<double>> source,int dimensions,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(dimensions is <1 or >8||source is not {Count:>=2 and <=4096}||source.Any(p=>p is null||p.Count!=dimensions||p.Any(v=>!double.IsFinite(v))))
            throw new ArgumentException("WORLD_PATTERN_PATH_INVALID");
        Dimensions=dimensions;points=source.Select(p=>p.ToArray()).ToArray();lengths=new double[points.Length-1];
        for(int i=0;i<lengths.Length;i++)
        {
            token.ThrowIfCancellationRequested();double squared=0;
            for(int axis=0;axis<dimensions;axis++){double delta=points[i+1][axis]-points[i][axis];squared+=delta*delta;}
            lengths[i]=Math.Sqrt(squared);Length+=lengths[i];
            if(!double.IsFinite(Length)||lengths[i]<=1e-9)throw new ArgumentException("WORLD_PATTERN_SEGMENT_INVALID");
        }
    }
    public IReadOnlyList<IReadOnlyList<double>> Sample(int count,double spacing,double start=0,double normalOffset=0,double endInset=0,CancellationToken token=default)
        =>SampleCore(count,spacing,start,normalOffset,endInset,token,null);

    internal IReadOnlyList<WorldPopulationPose> SamplePlanar(int count,double spacing,double start,double normalOffset,double endInset,CancellationToken token)
    {
        if(Dimensions!=2)throw new ArgumentException("WORLD_PATTERN_PLANAR_REQUIRED");
        token.ThrowIfCancellationRequested();
        if(count is <0 or >100000)throw new ArgumentException("WORLD_PATTERN_PARAMETERS_INVALID");
        var headings=new double[count];var positions=SampleCore(count,spacing,start,normalOffset,endInset,token,headings);
        return Array.AsReadOnly(positions.Select((p,i)=>new WorldPopulationPose(new(p[0],p[1]),headings[i])).ToArray());
    }
    private IReadOnlyList<IReadOnlyList<double>> SampleCore(int count,double spacing,double start,double normalOffset,double endInset,CancellationToken token,double[]? headings)
    {
        token.ThrowIfCancellationRequested();
        if(count is <0 or >100000||!double.IsFinite(spacing)||spacing<=0||!double.IsFinite(start)||start<0||
            !double.IsFinite(endInset)||endInset<0||!double.IsFinite(normalOffset)||normalOffset!=0&&Dimensions!=2)
            throw new ArgumentException("WORLD_PATTERN_PARAMETERS_INVALID");
        double last=start+Math.Max(0,count-1)*spacing;
        if(!double.IsFinite(last)||last>Length-endInset+1e-9)throw new ArgumentException("WORLD_PATTERN_CAPACITY_EXCEEDED");
        var result=new IReadOnlyList<double>[count];int segment=0;double preceding=0;
        for(int i=0;i<count;i++)
        {
            token.ThrowIfCancellationRequested();double distance=start+i*spacing;
            while(segment<lengths.Length-1&&distance>preceding+lengths[segment])preceding+=lengths[segment++];
            double t=Math.Clamp((distance-preceding)/lengths[segment],0,1);
            if(headings is not null)headings[i]=Math.Atan2(points[segment+1][1]-points[segment][1],points[segment+1][0]-points[segment][0])*180/Math.PI;
            var position=points[segment].Select((v,axis)=>v*(1-t)+points[segment+1][axis]*t).ToArray();
            if(normalOffset!=0)
            {
                position[0]-=(points[segment+1][1]-points[segment][1])/lengths[segment]*normalOffset;
                position[1]+=(points[segment+1][0]-points[segment][0])/lengths[segment]*normalOffset;
            }
            if(position.Any(v=>!double.IsFinite(v)))throw new ArgumentException("WORLD_PATTERN_POSITION_INVALID");
            result[i]=Array.AsReadOnly(position);
        }
        return Array.AsReadOnly(result);
    }
}

public sealed record WorldPathPattern(WorldPolyline Path,double Spacing,double StartDistance=0,double EndInset=0,double NormalOffset=0);
/// <summary>Position and the exact selected segment's mathematical heading: local +X, degrees counterclockwise.</summary>
public sealed record WorldPopulationPose(WorldPoint Position,double TangentDegrees);
public enum WorldBoundarySide { Inside, Outside }
public sealed record WorldBoundaryPattern(WorldPolygon Boundary,int StartEdgeIndex,double Spacing,double StartDistance=0,double InwardOffset=0,double? SpanLength=null,
    WorldBoundarySide Side=WorldBoundarySide.Inside);

/// <summary>Geometric patterns only. Physical footprints, terrain and access are checked by their existing planners.</summary>
public static partial class WorldPopulationGeometry
{
    public static IReadOnlyList<WorldPopulationPose> PathPoses(WorldPathPattern pattern,int count,CancellationToken token=default)
    {
        if(pattern?.Path?.Points is null)throw new ArgumentException("WORLD_PATTERN_PATH_INVALID");
        return new WorldArcLengthPath(pattern.Path.Points.Select(p=>(IReadOnlyList<double>)new[]{p.X,p.Y}).ToArray(),2,token)
            .SamplePlanar(count,pattern.Spacing,pattern.StartDistance,pattern.NormalOffset,pattern.EndInset,token);
    }
    public static IReadOnlyList<WorldPoint> Path(WorldPathPattern pattern,int count,CancellationToken token=default)
    {
        if(pattern?.Path?.Points is null)throw new ArgumentException("WORLD_PATTERN_PATH_INVALID");
        return Points(new WorldArcLengthPath(pattern.Path.Points.Select(p=>(IReadOnlyList<double>)new[]{p.X,p.Y}).ToArray(),2,token)
            .Sample(count,pattern.Spacing,pattern.StartDistance,pattern.NormalOffset,pattern.EndInset,token));
    }
    public static (WorldArcLengthPath Path,double Span) BoundaryPath(WorldBoundaryPattern pattern,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(pattern?.Boundary?.Vertices is not {Count:>=3 and <=256}||pattern.StartEdgeIndex<0||pattern.StartEdgeIndex>=pattern.Boundary.Vertices.Count||
            !double.IsFinite(pattern.InwardOffset)||pattern.InwardOffset<0||!double.IsFinite(pattern.StartDistance)||pattern.StartDistance<0||
            !double.IsFinite(pattern.Spacing)||pattern.Spacing<=0||!Enum.IsDefined(pattern.Side))
            throw new ArgumentException("WORLD_BOUNDARY_PATTERN_INVALID");
        pattern.Boundary.Validate();var vertices=pattern.Boundary.Vertices;
        var ordered=Enumerable.Range(0,vertices.Count+1).Select(i=>vertices[(pattern.StartEdgeIndex+i)%vertices.Count])
            .Select(p=>(IReadOnlyList<double>)new[]{p.X,p.Y}).ToArray();
        var path=new WorldArcLengthPath(ordered,2,token);double span=pattern.SpanLength??path.Length-pattern.StartDistance;
        if(!double.IsFinite(span)||span<0||pattern.StartDistance+span>path.Length+1e-9)throw new ArgumentException("WORLD_BOUNDARY_SPAN_INVALID");
        return(path,span);
    }
    public static IReadOnlyList<WorldPoint> Boundary(WorldBoundaryPattern pattern,int count,CancellationToken token=default)
        =>Array.AsReadOnly(BoundaryPoses(pattern,count,token).Select(p=>p.Position).ToArray());

    public static IReadOnlyList<WorldPopulationPose> BoundaryPoses(WorldBoundaryPattern pattern,int count,CancellationToken token=default)
    {
        var (path,span)=BoundaryPath(pattern,token);double last=pattern.StartDistance+Math.Max(0,count-1)*pattern.Spacing;
        if(last>=path.Length-1e-9&&count>0)throw new ArgumentException("WORLD_BOUNDARY_WRAP_FORBIDDEN");
        var poses=path.SamplePlanar(count,pattern.Spacing,pattern.StartDistance,Math.Sign(pattern.Boundary.SignedArea())*pattern.InwardOffset*
            (pattern.Side==WorldBoundarySide.Inside?1:-1),
            path.Length-pattern.StartDistance-span,token);
        foreach(var pose in poses)
        {
            token.ThrowIfCancellationRequested();
            var point=pose.Position;
            bool inside=pattern.Boundary.ContainsPoint(point);
            if(pattern.Side==WorldBoundarySide.Inside&&!inside||pattern.Side==WorldBoundarySide.Outside&&pattern.InwardOffset>0&&inside)
                throw new ArgumentException(pattern.Side==WorldBoundarySide.Inside?"WORLD_BOUNDARY_OFFSET_OUTSIDE":"WORLD_BOUNDARY_OFFSET_WRONG_SIDE");
        }
        return poses;
    }
    private static IReadOnlyList<WorldPoint> Points(IReadOnlyList<IReadOnlyList<double>> values)=>Array.AsReadOnly(values.Select(p=>new WorldPoint(p[0],p[1])).ToArray());
}
