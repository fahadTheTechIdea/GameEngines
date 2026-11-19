using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

namespace WorldCreator.Godot;

public sealed record GodotMovementJourney(string Id,string Purpose,string SourceRuleId,WorldPoint From,WorldPoint To);
public sealed record GodotMovementProfile(string Id,string Purpose,double RadiusMeters,double ClearanceMarginMeters,
    double CellSizeMeters,double EndpointToleranceMeters,IReadOnlyList<GodotMovementJourney> Journeys);
/// <summary>Explicit exterior-domain navigation. Does not create interiors or open doors.</summary>
public sealed record GodotMovementSpecification(int Version,uint CollisionLayer,IReadOnlyList<GodotMovementProfile> Profiles);
public sealed record GodotMovementObstacle(string Id,string SourceRuleId,WorldPolygon Boundary);
public sealed record GodotMovementPacket(GodotMovementSpecification Specification,WorldPolygon Domain,
    IReadOnlyList<GodotMovementObstacle> Obstacles);

public sealed partial class GodotStructureLayerExporter
{
    private static GodotMovementPacket? MovementPacket(WorldMetadataDocument world,MetadataProductionAssembly assembly,
        GodotMovementSpecification? specification,CancellationToken token)
    {
        if(specification is null)return null;
        if(specification.Version!=1||specification.CollisionLayer==0||specification.Profiles is not {Count:>=1 and <=4}||
            specification.Profiles.Any(p=>p is null)||specification.Profiles.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=specification.Profiles.Count)
            throw new ArgumentException("GODOT_MOVEMENT_SPECIFICATION_INVALID");
        var domain=world.Surface!.Boundary!;
        var sites=world.AccessPlan?.Sites??[];
        if(assembly.Targets.Where(t=>t.Target.Kind==MetadataProductionTargetKind.Member).Any(t=>!sites.Any(s=>s.NodeId==t.Target.SourceId)))
            throw new ArgumentException("GODOT_MOVEMENT_OCCUPANCY_REQUIRED");
        var obstacles=world.Surface.Exclusions.Select(e=>new GodotMovementObstacle("exclusion:"+e.Id,e.SourceRuleId,e.Boundary))
            .Concat(sites.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid).Select(s=>new GodotMovementObstacle("solid:"+s.NodeId,s.SourceRuleId,s.Footprint)))
            .OrderBy(o=>o.Id,StringComparer.Ordinal).ToArray();
        if(obstacles.Sum(o=>o.Boundary.Vertices.Count)+domain.Vertices.Count>32768)throw new ArgumentException("GODOT_MOVEMENT_VERTEX_BUDGET");
        double width=domain.Vertices.Max(p=>p.X)-domain.Vertices.Min(p=>p.X),height=domain.Vertices.Max(p=>p.Y)-domain.Vertices.Min(p=>p.Y);
        double work=0;
        foreach(var profile in specification.Profiles)
        {
            token.ThrowIfCancellationRequested();
            if(!Id(profile.Id)||profile.Id=="Boundaries"||string.IsNullOrWhiteSpace(profile.Purpose)||!double.IsFinite(profile.RadiusMeters)||profile.RadiusMeters is <.001 or >128||
                !double.IsFinite(profile.ClearanceMarginMeters)||profile.ClearanceMarginMeters is <.001 or >16||
                !double.IsFinite(profile.CellSizeMeters)||profile.CellSizeMeters is <.001 or >1||
                !double.IsFinite(profile.EndpointToleranceMeters)||profile.EndpointToleranceMeters<.000001||profile.EndpointToleranceMeters>profile.CellSizeMeters||profile.Journeys is not {Count:>=1 and <=64}||
                profile.Journeys.Any(j=>j is null)||profile.Journeys.Select(j=>j.Id).Distinct(StringComparer.Ordinal).Count()!=profile.Journeys.Count)
                throw new ArgumentException("GODOT_MOVEMENT_PROFILE_INVALID");
            work+=width/profile.CellSizeMeters*(height/profile.CellSizeMeters);
            if(!double.IsFinite(work)||work>64000000)throw new ArgumentException("GODOT_MOVEMENT_BAKE_BUDGET");
            foreach(var journey in profile.Journeys)
                if(!Id(journey.Id)||string.IsNullOrWhiteSpace(journey.Purpose)||string.IsNullOrWhiteSpace(journey.SourceRuleId)||
                    !Point(journey.From)||!Point(journey.To)||journey.From.DistanceTo(journey.To)<1e-6||
                    !domain.ContainsPoint(journey.From)||!domain.ContainsPoint(journey.To)||
                    obstacles.Any(o=>o.Boundary.ContainsPoint(journey.From)||o.Boundary.ContainsPoint(journey.To)))
                    throw new ArgumentException("GODOT_MOVEMENT_JOURNEY_INVALID:"+journey.Id);
        }
        return new(specification,domain,obstacles);
        static bool Id(string value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=128&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
        static bool Point(WorldPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X)<=1e7&&Math.Abs(p.Y)<=1e7;
    }
}
