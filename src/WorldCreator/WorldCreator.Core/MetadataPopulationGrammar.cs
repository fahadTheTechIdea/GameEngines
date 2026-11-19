using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataPopulationSocket(string NodeId,WorldPoint Position);
public enum MetadataPopulationFacing { Along, Against, Left, Right, Inside, Outside }
public sealed record MetadataPopulationOrientationTrace(string StageId,string DemandId,string Purpose,string SourceRuleId,
    IReadOnlyList<string> NodeIds,MetadataPopulationFacing Facing,double RotationOffsetDegrees,IReadOnlyList<double> TangentDegrees,
    double? InsideNormalDegrees,IReadOnlyList<double> Rotations);
/// <summary>World-coordinate contour witnesses; local-frame translation applies only to placement anchors.</summary>
public sealed record MetadataPopulationContourTrace(string StageId,string DemandId,string RegionId,string? AllocationId,string FieldId,
    string Purpose,string SourceRuleId,IReadOnlyList<string> NodeIds,IReadOnlyList<WorldContourPopulationResult> Contours);
public sealed record MetadataPopulationAreaTrace(string StageId,string DemandId,string RegionId,string? AllocationId,
    string Purpose,string SourceRuleId,IReadOnlyList<string> NodeIds,WorldAreaPopulationPlan Plan);
/// <summary>Exactly one source in the placement stage's coordinate frame. Every required member gets one anchor.</summary>
public sealed record MetadataPopulationGrammar(string Purpose,string SourceRuleId,WorldPathPattern? Path=null,
    WorldBoundaryPattern? Boundary=null,IReadOnlyList<MetadataPopulationSocket>? Sockets=null,MetadataGeneratedRoutePattern? Route=null,MetadataGeneratedRegionPattern? Region=null,
    MetadataGeneratedBoundaryPattern? GeneratedBoundary=null,MetadataPopulationFacing? Facing=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationDomainPattern? Domain=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataSpiralLayout.Settings? Spiral=null);

public sealed partial class MetadataPlacementPlanner
{
    internal static void ValidatePopulationGrammar(MetadataPopulationGrammar grammar)
    {
        if(string.IsNullOrWhiteSpace(grammar.Purpose)||grammar.Purpose.Length>2048||string.IsNullOrWhiteSpace(grammar.SourceRuleId)||grammar.SourceRuleId.Length>2048||
            (grammar.Path is null?0:1)+(grammar.Boundary is null?0:1)+(grammar.Sockets is null?0:1)+(grammar.Route is null?0:1)+(grammar.Region is null?0:1)+(grammar.GeneratedBoundary is null?0:1)+(grammar.Domain is null?0:1)+(grammar.Spiral is null?0:1)!=1)
            throw new ArgumentException("METADATA_POPULATION_GRAMMAR_INVALID");
        if(grammar.Facing is {} facing&&(!Enum.IsDefined(facing)||grammar.Region is {Pattern:null,Contour:null}||grammar.Region?.Area is not null||grammar.Sockets is not null||grammar.Domain?.Area is not null||
            facing is MetadataPopulationFacing.Inside or MetadataPopulationFacing.Outside&&grammar.Boundary is null&&grammar.GeneratedBoundary is null))
            throw new ArgumentException("METADATA_POPULATION_FACING_INVALID");
    }
    private static IReadOnlyList<WorldPoint> PopulationPositions(MetadataPopulationGrammar grammar,IReadOnlyList<string> members,WorldSpatialWorkBudget budget,CancellationToken token,WorldMetadataDocument? source=null,string? parent=null,
        Action<IReadOnlyList<WorldContourPopulationResult>>? recordContours=null,Action<WorldAreaPopulationPlan>? recordArea=null,
        Action<MetadataGeneratedBoundaryResult>? recordBoundary=null,Action<IReadOnlyList<WorldPopulationPose>,double?>? recordOrientation=null,
        Action<MetadataPopulationDomainResult>? recordDomain=null)
    {
        token.ThrowIfCancellationRequested();
        ValidatePopulationGrammar(grammar);
        IReadOnlyList<WorldPoint> Orient(IReadOnlyList<WorldPopulationPose> poses,double? inside=null)
        {recordOrientation?.Invoke(poses,inside);return Array.AsReadOnly(poses.Select(p=>p.Position).ToArray());}
        if(grammar.Facing is not null)
        {
            if(grammar.Path is {} orientedPath)return Orient(WorldPopulationGeometry.PathPoses(orientedPath,members.Count,token));
            if(grammar.Boundary is {} orientedBoundary)return Orient(WorldPopulationGeometry.BoundaryPoses(orientedBoundary,members.Count,token),
                Math.Sign(orientedBoundary.Boundary.SignedArea())*90);
            if(grammar.Route is {} orientedRoute)return Orient(WorldPopulationGeometry.PathPoses(MetadataPopulationSources.RoutePath(orientedRoute,source,parent,budget),members.Count,token));
        }
        if(grammar.Spiral is {} spiral)
        {
            budget.Charge(members.Count*2L+1);
            if(grammar.Facing is not null)return Orient(MetadataSpiralLayout.PlanPoses2D(spiral,members.Count,token));
            return Array.AsReadOnly(MetadataSpiralLayout.PlanSettings(spiral,2,members.Count,token).Select(p=>new WorldPoint(p[0],p[1])).ToArray());
        }
        if(grammar.Path is {} path)return WorldPopulationGeometry.Path(path,members.Count,token);
        if(grammar.Boundary is {} boundary)return WorldPopulationGeometry.Boundary(boundary,members.Count,token);
        if(grammar.Route is {} route)return MetadataPopulationSources.Positions(route,members.Count,source,parent,budget,token);
        if(grammar.Region is {} region)
        {
            IReadOnlyList<WorldContourPopulationResult>? contours=null;
            var positions=MetadataPopulationSources.Positions(region,members.Count,source,parent,budget,token,
                result=>{contours=result;recordContours?.Invoke(result);},recordArea);
            if(grammar.Facing is null)return positions;
            if(region.Pattern is {} bands)return Orient(positions.Select(p=>new WorldPopulationPose(p,bands.DirectionDegrees)).ToArray());
            var requests=region.Contour!.Contours;var headings=new List<double>(members.Count);
            for(int i=0;i<requests.Count;i++)
            {
                token.ThrowIfCancellationRequested();var c=requests[i];var contourPath=contours![i].Path;
                budget.Charge((long)contourPath.Points.Count+c.Count);
                headings.AddRange(WorldPopulationGeometry.PathPoses(new(contourPath,c.Spacing,c.StartInset,c.EndInset),c.Count,token).Select(p=>p.TangentDegrees));
            }
            if(headings.Count!=positions.Count)throw new ArgumentException("METADATA_POPULATION_CONTOUR_ORIENTATION_MISMATCH");
            return Orient(positions.Select((p,i)=>new WorldPopulationPose(p,headings[i])).ToArray());
        }
        if(grammar.Domain is {} domain)
        {
            var result=MetadataPopulationSources.Domain(domain,members.Count,source,parent,budget,token);recordDomain?.Invoke(result);
            var origin=MetadataPopulationSources.ParentOrigin(source!,parent);
            var relative=result.WorldPositions.Select(p=>new WorldPoint(p.X-origin.X,p.Y-origin.Y)).ToArray();
            return grammar.Facing is null?relative:Orient(relative.Select(p=>new WorldPopulationPose(p,result.ResolvedBands!.DirectionDegrees)).ToArray());
        }
        if(grammar.GeneratedBoundary is {} generated)
        {
            var result=MetadataGeneratedBoundary.Sample(generated,members.Count,source,parent,budget,token,
                grammar.Facing is null?null:(poses,hole)=>recordOrientation?.Invoke(poses,hole?-90:90));recordBoundary?.Invoke(result);
            var origin=MetadataPopulationSources.ParentOrigin(source!,parent);
            return Array.AsReadOnly(result.Positions.Select(p=>new WorldPoint(p.X-origin.X,p.Y-origin.Y)).ToArray());
        }
        var sockets=grammar.Sockets!;
        if(sockets.Count!=members.Count||sockets.Any(s=>s is null||s.NodeId is null||!double.IsFinite(s.Position.X)||!double.IsFinite(s.Position.Y)||Math.Abs(s.Position.X)>1e7||Math.Abs(s.Position.Y)>1e7)||
            sockets.Select(s=>s.NodeId).Distinct(StringComparer.Ordinal).Count()!=sockets.Count||!sockets.Select(s=>s.NodeId).ToHashSet(StringComparer.Ordinal).SetEquals(members))
            throw new ArgumentException("METADATA_POPULATION_SOCKETS_MISMATCH");
        var lookup=sockets.ToDictionary(s=>s.NodeId,s=>s.Position,StringComparer.Ordinal);
        return members.Select(id=>lookup[id]).ToArray();
    }
}
