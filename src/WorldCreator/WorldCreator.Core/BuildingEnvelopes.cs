using WorldCreator.Core.Geometry;
namespace WorldCreator.Core.Geometry;
public sealed record BuildingEnvelopeRules(string BuildingType, double FrontSetbackMeters,
    double RearSetbackMeters, double SideSetbackMeters, double MinimumWidthMeters,
    double MinimumDepthMeters)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BuildingType)) throw new ArgumentException("Building type required.");
        if (new[] { FrontSetbackMeters, RearSetbackMeters, SideSetbackMeters }.Any(v => !double.IsFinite(v) || v < 0))
            throw new ArgumentException("Setbacks must be finite and nonnegative.");
        if (!double.IsFinite(MinimumWidthMeters) || !double.IsFinite(MinimumDepthMeters) ||
            MinimumWidthMeters <= 0 || MinimumDepthMeters <= 0)
            throw new ArgumentException("Minimum footprint dimensions must be positive and finite.");
    }
}
public sealed record BuildingEnvelopeResult(WorldPolygon? Footprint, IReadOnlyList<string> Diagnostics)
{
    public bool IsFeasible => Footprint is not null && Diagnostics.Count == 0;
}
public sealed record PhysicalSiteGeometry(WorldPolygon? Footprint,WorldPolyline? Access,IReadOnlyList<string> Issues);
public sealed record BuildingFrontageCenterRange(double MinimumMeters, double MaximumMeters);
/// <summary>Street-fronted rectangular parcel envelope solver; does not silently approximate concave parcels.</summary>
public static class BuildingEnvelopePlanner
{
    private const double Tolerance = 1e-8;
    /// <summary>Fits one source-sized surface/object and its declared approach within an authored frontage site.</summary>
    public static PhysicalSiteGeometry PlanPhysicalSite(BuildableParcel parcel,BuildingEnvelopeRules rules,double width,double depth,
        double clearance,double accessWidth,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(!double.IsFinite(accessWidth)||accessWidth<0)throw new ArgumentException("PHYSICAL_SITE_ACCESS_INVALID");
        var envelope=Plan(parcel,rules);
        if(!envelope.IsFeasible)return new(null,null,envelope.Diagnostics);
        var vertices=parcel.Boundary.Vertices;double roadY=parcel.StreetAccess.Y;
        bool north=Math.Abs(roadY-vertices.Min(p=>p.Y))<Tolerance;
        var footprint=FitPhysicalRectangle(envelope.Footprint!,width,depth,clearance,north);
        if(footprint is null)return new(null,null,["PHYSICAL_SITE_SIZE_DOES_NOT_FIT"]);
        WorldPolyline? access=null;
        if(accessWidth>0)
        {
            double cx=footprint.Vertices.Average(p=>p.X),y=north?footprint.Vertices.Min(p=>p.Y):footprint.Vertices.Max(p=>p.Y);
            if(cx-accessWidth/2<vertices.Min(p=>p.X)-Tolerance||cx+accessWidth/2>vertices.Max(p=>p.X)+Tolerance||Math.Abs(y-roadY)<Tolerance)
                return new(null,null,["PHYSICAL_SITE_ACCESS_DOES_NOT_FIT"]);
            access=new([new(cx,roadY),new(cx,y)]);
        }
        return new(footprint,access,[]);
    }
    /// <summary>Fits an unscaled physical rectangle inside an axis-aligned buildable envelope. Shared by building and object variants.</summary>
    public static WorldPolygon? FitPhysicalRectangle(WorldPolygon envelope,double width,double depth,double clearance,bool frontNorth=true)
    {
        envelope.Validate();
        if(!new[]{width,depth,clearance}.All(double.IsFinite)||width<=0||depth<=0||clearance<0)throw new ArgumentException("Invalid physical rectangle");
        var xs=envelope.Vertices.Select(p=>p.X).Distinct().Order().ToArray();var ys=envelope.Vertices.Select(p=>p.Y).Distinct().Order().ToArray();
        if(envelope.Vertices.Count!=4||xs.Length!=2||ys.Length!=2)throw new ArgumentException("PHYSICAL_ENVELOPE_UNSUPPORTED");
        if(width+2*clearance>xs[1]-xs[0]+Tolerance||depth+2*clearance>ys[1]-ys[0]+Tolerance)return null;
        double left=(xs[0]+xs[1]-width)/2,top=frontNorth?ys[0]+clearance:ys[1]-clearance-depth;
        return new([new(left,top),new(left+width,top),new(left+width,top+depth),new(left,top+depth)]);
    }
    /// <summary>Fits an unscaled rectangle at the declared frontage setback in a convex local parcel.
    /// Local frontage is y=0; all parcel edges retain the requested setback.</summary>
    public static BuildingEnvelopeResult PlanFixedFrontageFootprint(WorldPolygon localParcel,double width,double depth,double setback)
    {
        var range = FixedFrontageCenters(localParcel, width, depth, setback);
        if (range is null) return new(null, [ConvexParcelGeometry.IsConvex(localParcel) ? "INSUFFICIENT_BUILDABLE_ENVELOPE" : "UNSUPPORTED_CONCAVE_PARCEL"]);
        double center = (range.MinimumMeters + range.MaximumMeters) / 2;
        return new(new([new(center-width/2,setback),new(center+width/2,setback),new(center+width/2,setback+depth),new(center-width/2,setback+depth)]),[]);
    }
    /// <summary>Exact feasible center interval at the declared frontage setback, without choosing an arbitrary position.
    /// Shared by physical-envelope placement and source-constrained frontage subdivision.</summary>
    public static BuildingFrontageCenterRange? FixedFrontageCenters(WorldPolygon localParcel,double width,double depth,double setback)
    {
        if(!new[]{width,depth,setback}.All(double.IsFinite)||width<=0||depth<=0||setback<0)throw new ArgumentException("Invalid fixed footprint");
        if(!ConvexParcelGeometry.IsConvex(localParcel))return null;
        double low=double.NegativeInfinity,high=double.PositiveInfinity,orientation=Math.Sign(localParcel.SignedArea());
        for(int i=0;i<localParcel.Vertices.Count;i++)
        {
            var a=localParcel.Vertices[i];var b=localParcel.Vertices[(i+1)%localParcel.Vertices.Count];double length=a.DistanceTo(b);
            double nx=-(b.Y-a.Y)/length*orientation,ny=(b.X-a.X)/length*orientation,c=nx*a.X+ny*a.Y+setback;
            foreach(double dx in new[]{-width/2,width/2})foreach(double y in new[]{setback,setback+depth})
            {
                double bound=c-nx*dx-ny*y;
                if(Math.Abs(nx)<Tolerance){if(bound>Tolerance)return null;}
                else if(nx>0)low=Math.Max(low,bound/nx);else high=Math.Min(high,bound/nx);
            }
        }
        if(!double.IsFinite(low)||!double.IsFinite(high)||high<low-Tolerance)return null;
        return new(low,high);
    }
    public static BuildingEnvelopeResult Plan(BuildableParcel parcel, BuildingEnvelopeRules rules)
    {
        ArgumentNullException.ThrowIfNull(parcel);
        ArgumentNullException.ThrowIfNull(rules);
        rules.Validate();
        parcel.Boundary.Validate();
        var vertices = parcel.Boundary.Vertices;
        var xs = vertices.Select(v => v.X).Distinct().Order().ToArray();
        var ys = vertices.Select(v => v.Y).Distinct().Order().ToArray();
        if (vertices.Count != 4 || xs.Length != 2 || ys.Length != 2)
            return new(null, new[] { "UNSUPPORTED_PARCEL_GEOMETRY" });
        double left = xs[0] + rules.SideSetbackMeters;
        double right = xs[1] - rules.SideSetbackMeters;
        bool frontSouth = Math.Abs(parcel.StreetAccess.Y - ys[1]) < Tolerance;
        bool frontNorth = Math.Abs(parcel.StreetAccess.Y - ys[0]) < Tolerance;
        if (!frontNorth && !frontSouth)
            return new(null, new[] { "STREET_FRONTAGE_NOT_RECOGNIZED" });
        if (parcel.StreetAccess.X < xs[0] - Tolerance || parcel.StreetAccess.X > xs[1] + Tolerance)
            return new(null, new[] { "STREET_ACCESS_OUTSIDE_FRONTAGE" });
        double top = ys[0] + (frontNorth ? rules.FrontSetbackMeters : rules.RearSetbackMeters);
        double bottom = ys[1] - (frontSouth ? rules.FrontSetbackMeters : rules.RearSetbackMeters);
        if (right - left < rules.MinimumWidthMeters - Tolerance ||
            bottom - top < rules.MinimumDepthMeters - Tolerance)
            return new(null, new[] { "INSUFFICIENT_BUILDABLE_ENVELOPE" });
        return new(new WorldPolygon(new[]
        {
            new WorldPoint(left, top), new WorldPoint(right, top),
            new WorldPoint(right, bottom), new WorldPoint(left, bottom)
        }), Array.Empty<string>());
    }
}
