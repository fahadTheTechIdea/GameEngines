using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;
namespace WorldCreator.Core.Metadata;

internal sealed record MetadataSpatialSource(MetadataSpatialMemberKind Kind,string Id,WorldPoint Anchor,IReadOnlyList<WorldPolygon> Polygons,
    WorldRouteResult? Route=null,WorldTraversalProfile? Profile=null);
/// <summary>One source extraction owner for physical spatial membership.
/// Preserves complete original node/source/path geometry; consumers choose ownership.</summary>
internal static class MetadataSpatialMembers
{
    internal static void Visit(WorldMetadataDocument world,WorldSpatialWorkBudget budget,CancellationToken token,
        Action<MetadataSpatialSource> member,Action<string> shared,
        Action<MetadataSpatialMemberKind,string,MetadataChunkCoverageKind,string,int?,WorldPolygon,SiteFeatureKind?>? coverage=null)
    {
        var shapes=new Dictionary<string,List<WorldPolygon>>(StringComparer.Ordinal);
        void Shape(string id,WorldPolygon polygon,MetadataChunkCoverageKind kind,string sourceId,SiteFeatureKind? featureKind=null)
        {if(!shapes.TryGetValue(id,out var list))shapes.Add(id,list=[]);list.Add(polygon);coverage?.Invoke(MetadataSpatialMemberKind.Node,id,kind,sourceId,null,polygon,featureKind);}
        if(world.PlacementPlan is {} placement)
        {
            var stages=new[]{(placement.Definition.Bindings,placement.Placement)}.Concat(placement.NestedStages.Select(s=>
                (placement.Definition.NestedStages!.Single(d=>d.Id==s.Id).Bindings,s.Placement)));
            foreach(var (bindings,plan) in stages)
            foreach(var binding in bindings)
            {
                budget.Charge();var site=plan.Sites.Single(s=>s.DemandId==binding.DemandId);
                Shape(binding.NodeId,site.Footprint.Footprint,MetadataChunkCoverageKind.Footprint,binding.DemandId);
                if(site.Reservation is {} reserve)Shape(binding.NodeId,reserve.Footprint,MetadataChunkCoverageKind.Reservation,binding.DemandId);
            }
            foreach(var feature in placement.SiteFeatures??[])Shape(feature.NodeId,feature.Boundary,MetadataChunkCoverageKind.SiteFeature,feature.Id,feature.Kind);
        }
        foreach(var access in world.AccessPlan?.Sites??[])
        foreach(var port in access.Ports)Shape(access.NodeId,WorldSurfaceTraversal.Envelope(port.BoundaryPosition,port.ApproachPosition,port.Width/2),MetadataChunkCoverageKind.AccessApproach,port.Id);
        foreach(var interior in world.InteriorPlan?.Results??[])
        foreach(var wall in interior.Walls)
        {
            token.ThrowIfCancellationRequested();budget.Charge(wall.Footprint.Vertices.Count+1L);
            Shape(wall.RoomNodeId,wall.Footprint,MetadataChunkCoverageKind.InteriorWall,MetadataChunkGeometrySnapshot.WallSourceId(interior.RuleId,wall));
        }
        foreach(var node in world.Nodes.OrderBy(n=>n.Id,StringComparer.Ordinal))
        {
            budget.Charge();
            if(node.Position is null){shared(node.Id);continue;}
            if(node.Position.Count!=2)throw new ArgumentException("METADATA_SPATIAL_POSITION_INVALID");
            member(new(MetadataSpatialMemberKind.Node,node.Id,new(node.Position[0],node.Position[1]),shapes.GetValueOrDefault(node.Id,[])));
        }
        void Path(WorldRouteResult? route,WorldTraversalProfile profile)
        {
            if(route is null)return;
            double radius=profile.Radius;
            if(route.Status!=WorldRouteStatus.Found||route.Path is not {Points.Count:>=2} path||!double.IsFinite(radius)||radius<=0)throw new ArgumentException("METADATA_SPATIAL_ROUTE_INVALID");
            var polygons=new List<WorldPolygon>();
            for(int i=1;i<path.Points.Count;i++)
            {
                budget.Charge(64);
                var polygon=WorldSurfaceTraversal.Envelope(path.Points[i-1],path.Points[i],radius);polygons.Add(polygon);
                coverage?.Invoke(MetadataSpatialMemberKind.Path,route.Id,MetadataChunkCoverageKind.PathSegment,route.Id,i-1,polygon,null);
            }
            member(new(MetadataSpatialMemberKind.Path,route.Id,path.Points[0],polygons,route,profile));
        }
        foreach(var route in world.RoutePlan?.Routes??[])
        {var profile=world.RoutePlan!.Rules.Single(r=>r.Id==route.RuleId).Profile;Path(route.Forward,profile);Path(route.Reverse,profile);}
        foreach(var interior in world.InteriorPlan?.Results??[])
        foreach(var route in interior.Routes)
        {var profile=world.InteriorPlan!.Definition.Rules.Single(r=>r.Id==interior.RuleId).Profile;Path(route.Forward,profile);Path(route.Reverse,profile);}
        foreach(var network in world.NetworkPlan?.Networks??[])
        foreach(var edge in network.Plan.Edges){Path(edge.Forward,network.Plan.Options.Profile);Path(edge.Reverse,network.Plan.Options.Profile);}
    }
}
