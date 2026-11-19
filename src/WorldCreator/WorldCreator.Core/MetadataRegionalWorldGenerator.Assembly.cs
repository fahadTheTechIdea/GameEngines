using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalWorldGenerator
{
    private MetadataRegionalWorldDocument AssemblePrepared(MetadataRegionalWorldDefinition definition,IReadOnlyList<MetadataGeneratedRegion> packets,CancellationToken token,long preparationWork)
    {
        var budget=Budget(definition,token);budget.Charge(preparationWork);var worlds=packets.ToDictionary(p=>p.RegionId,p=>p.World,StringComparer.Ordinal);
        if(worlds.Count!=definition.Regions.Count||definition.Regions.Any(r=>!worlds.ContainsKey(r.Id)))throw new ArgumentException("METADATA_REGIONAL_PACKETS_REQUIRED");
        if(worlds.Values.Sum(w=>(long)w.Nodes.Count)>definition.MaximumTotalNodes||worlds.Values.Sum(w=>(long)w.Connections.Count)+definition.Connections.Count>definition.MaximumTotalConnections)
            throw new ArgumentException("METADATA_REGIONAL_CONTENT_BUDGET");
        var members=new List<MetadataRegionalMember>();var identities=new HashSet<(string Owner,MetadataSpatialMemberKind Kind,string Id)>();int references=0;
        var solids=new List<(string Region,string Node,WorldPolygon Polygon)>();
        var sources=new Dictionary<string,List<MetadataSpatialSource>>(StringComparer.Ordinal);
        foreach(var packet in packets.OrderBy(p=>p.RegionId,StringComparer.Ordinal))
        {
            var region=definition.Regions.Single(r=>r.Id==packet.RegionId);var world=packet.World;var list=new List<MetadataSpatialSource>();sources.Add(region.Id,list);
            var root=world.Nodes.Single(n=>n.ParentId is null);
            if(root.Position is not {Count:2} origin||!region.Ownership.ContainsPoint(new(origin[0],origin[1])))throw new ArgumentException("METADATA_REGIONAL_ROOT_OWNERSHIP_REQUIRED:"+region.Id);
            MetadataSpatialMembers.Visit(world,budget,token,m=>{list.Add(m);Member(m,region.Id);},id=>Member(new(MetadataSpatialMemberKind.Node,id,new(origin[0],origin[1]),[]),region.Id));
            foreach(var source in list.Where(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Polygons.Count>0))
                if(world.AccessPlan?.Sites.Any(s=>s.NodeId==source.Id)!=true)throw new ArgumentException("METADATA_REGIONAL_OCCUPANCY_REQUIRED:"+source.Id);
            foreach(var site in world.AccessPlan?.Sites??[])
                if(site.Occupancy==MetadataOccupancyKind.Solid)solids.Add((region.Id,site.NodeId,site.Footprint));
            var occupied=MetadataInteriorPlanner.WithWalls(world.Surface!,world.InteriorPlan);
            foreach(var wall in occupied.Exclusions.Where(e=>!world.Surface!.Exclusions.Any(b=>b.Id==e.Id)))solids.Add((region.Id,wall.Id,wall.Boundary));
        }
        // Different generation domains may overlap to hold a whole seam-spanning
        // structure. This never grants permission for independently owned bodies
        // or reserved/source feature geometry to collide.
        var placed=sources.SelectMany(r=>r.Value.Where(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Polygons.Count>0).Select(m=>(Region:r.Key,Source:m))).ToArray();
        if(placed.Length>0)
        {
            // A node's complete envelope includes its reservation, features and
            // access approaches, not just its solid footprint. Closed bounds
            // retain touching candidates for the existing exact polygon test.
            var bounds=placed.Select(p=>
            {
                double minX=double.PositiveInfinity,minY=double.PositiveInfinity,maxX=double.NegativeInfinity,maxY=double.NegativeInfinity;
                foreach(var polygon in p.Source.Polygons)foreach(var point in polygon.Vertices)
                {
                    budget.Charge();minX=Math.Min(minX,point.X);minY=Math.Min(minY,point.Y);maxX=Math.Max(maxX,point.X);maxY=Math.Max(maxY,point.Y);
                }
                return new WorldPlacementEnvelopeIndex.Box(minX,minY,maxX,maxY);
            }).ToArray();
            // Local node IDs may repeat across reusable regional templates.
            // Original ordinal ranks give unique index keys and conflict order.
            var keys=Enumerable.Range(0,placed.Length).Select(i=>i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var ranks=keys.Select((key,rank)=>(key,rank)).ToDictionary(p=>p.key,p=>p.rank,StringComparer.Ordinal);
            var index=new WorldPlacementEnvelopeIndex(keys.Select((key,i)=>(key,bounds[i])).ToArray(),budget,definition.MaximumTotalNodes);
            for(int i=0;i<placed.Length;i++)
            {
                foreach(string key in index.Query(bounds[i],0,key=>ranks[key]<i&&placed[ranks[key]].Region!=placed[i].Region))
                {
                    budget.Charge();int j=ranks[key];
                    foreach(var a in placed[i].Source.Polygons)foreach(var b in placed[j].Source.Polygons)
                        if(Touches(a,b,budget)&&Intersection(a,b,budget)>1e-8)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_OVERLAP:"+placed[i].Source.Id+":"+placed[j].Source.Id);
                }
            }
        }
        var obstacles=solids.Select(s=>new WorldSurfaceExclusion("regional-"+MetadataExecutionCache.Hash(new{s.Region,s.Node}),
            "Physical source retained across generation regions",s.Region+":"+s.Node,s.Polygon)).ToArray();
        // Requalify existing local paths against foreign solids as well. A local
        // success cannot certify a path crossing an independently generated body.
        foreach(var region in definition.Regions)
        {
            var query=WorldSurfaceQuery.CreateRegional(definition.Surface,region.GenerationDomain,token,obstacles);
            foreach(var source in sources[region.Id].Where(s=>s.Route is not null))
            {
                var traversal=new WorldSurfaceTraversal(query,source.Profile!,token);var points=source.Route!.Path!.Points;
                for(int i=1;i<points.Count;i++)
                    if(traversal.Evaluate(points[i-1],points[i],budget,token) is null)throw new ArgumentException("METADATA_REGIONAL_LOCAL_PATH_BLOCKED:"+source.Id);
            }
        }
        var routes=new List<MetadataRegionalRoute>();
        foreach(var connection in definition.Connections)
        {
            token.ThrowIfCancellationRequested();var query=WorldSurfaceQuery.CreateRegional(definition.Surface,connection.RoutingDomain,token,obstacles);
            WorldPoint Endpoint(MetadataRegionalEndpoint endpoint)=>RegionalEndpoint(definition,worlds,obstacles,connection.RoutingDomain,connection.Profile,endpoint,budget,token);
            var from=Endpoint(connection.From);var to=Endpoint(connection.To);
            if(definition.OwnershipPlan?.Source.PhysicalFeedback is not null&&WorldSurfaceRoutePlanner.DisconnectedDomain(query,from,to,connection.Profile.Radius,budget,token) is {} disconnected)
                throw new MetadataRegionalConnectionInfeasibleException(connection.OwnerRegionId,connection.Id,WorldRouteStatus.CandidateGraphDisconnected,budget.Used,disconnected);
            WorldRouteResult Route(bool reverse)
            {
                var route=new WorldSurfaceRoutePlanner().Plan(query,new(connection.Id+(reverse?"/reverse":"/forward"),connection.Purpose,connection.SourceRuleId,
                    reverse?to:from,reverse?from:to,connection.Profile,connection.Search),token,budget);
                if(route.Status!=WorldRouteStatus.Found)throw new MetadataRegionalConnectionInfeasibleException(connection.OwnerRegionId,connection.Id,route.Status,budget.Used);
                var polygons=route.Path!.Points.Zip(route.Path.Points.Skip(1),(a,b)=>WorldSurfaceTraversal.Envelope(a,b,connection.Profile.Radius)).ToArray();
                Member(new(MetadataSpatialMemberKind.Path,route.Id,route.Path.Points[0],polygons,route,connection.Profile),connection.OwnerRegionId);
                return route;
            }
            var forward=Route(false);var reverse=connection.Bidirectional?Route(true):null;routes.Add(new(connection,from,to,forward,reverse));
        }
        MetadataRegionalEndpoint ResolveNetworkEndpoint(string ruleId,string destinationId,MetadataRegionalEndpoint endpoint)
        {
            if(endpoint.Selector is not {} selector)return endpoint;
            token.ThrowIfCancellationRequested();var world=worlds[endpoint.RegionId];budget.Charge(world.Nodes.Count+1);
            var matches=world.Nodes.Where(n=>n.Kind==selector.NodeKind&&(selector.ArchetypeId is null||n.ArchetypeId==selector.ArchetypeId)).ToArray();
            if(matches.Length!=1)throw new MetadataRegionalNetworkSelectorException(ruleId,destinationId,endpoint,matches.Length,budget.Used);
            return new(endpoint.RegionId,matches[0].Id,endpoint.PortId);
        }
        var networks=new MetadataNetworkPlanner().PlanRegional(definition,obstacles,(e,domain,profile)=>RegionalEndpoint(definition,worlds,obstacles,domain,profile,e,budget,token),ResolveNetworkEndpoint,budget,token);
        if(worlds.Values.Sum(w=>(long)w.Connections.Count)+routes.Count+networks.Routes.Count>definition.MaximumTotalConnections)
            throw new ArgumentException("METADATA_REGIONAL_CONTENT_BUDGET");
        foreach(var route in networks.Routes)
        {
            foreach(var direction in new[]{route.Forward,route.Reverse!})
            {
                var polygons=direction.Path!.Points.Zip(direction.Path.Points.Skip(1),(a,b)=>WorldSurfaceTraversal.Envelope(a,b,route.Definition.Profile.Radius)).ToArray();
                Member(new(MetadataSpatialMemberKind.Path,direction.Id,direction.Path.Points[0],polygons,direction,route.Definition.Profile),route.Definition.OwnerRegionId);
            }
            routes.Add(route);
        }
        foreach(var packet in packets)ValidateDependencies(definition,packet,token);
        return new(1,definition,MetadataExecutionCache.Hash(definition),packets.OrderBy(p=>p.RegionId,StringComparer.Ordinal).ToArray(),
            members.OrderBy(m=>m.Kind).ThenBy(m=>m.OwnerRegionId,StringComparer.Ordinal).ThenBy(m=>m.Id,StringComparer.Ordinal).ToArray(),routes,budget.Used,networks.Plan);

        void Member(MetadataSpatialSource source,string owner)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(!identities.Add((owner,source.Kind,source.Id)))throw new ArgumentException("METADATA_REGIONAL_MEMBER_IDENTITY_CONFLICT:"+owner+":"+source.Id);
            var touched=new SortedSet<string>(StringComparer.Ordinal){owner};
            foreach(var region in definition.Regions)
            {
                budget.Charge(region.Ownership.Vertices.Count);
                if(region.Ownership.ContainsPoint(source.Anchor)||source.Polygons.Any(p=>Touches(p,region.Ownership,budget)))touched.Add(region.Id);
            }
            if((references+=touched.Count)>definition.MaximumReferences)throw new ArgumentException("METADATA_REGIONAL_REFERENCE_BUDGET");
            members.Add(new(source.Kind,source.Id,owner,touched.ToArray()));
        }
    }
    private static bool Touches(WorldPolygon a,WorldPolygon b,WorldSpatialWorkBudget budget)
    {
        budget.Charge(4L*(a.Vertices.Count+b.Vertices.Count));
        if(a.Vertices.Max(p=>p.X)<b.Vertices.Min(p=>p.X)||b.Vertices.Max(p=>p.X)<a.Vertices.Min(p=>p.X)||
            a.Vertices.Max(p=>p.Y)<b.Vertices.Min(p=>p.Y)||b.Vertices.Max(p=>p.Y)<a.Vertices.Min(p=>p.Y))return false;
        budget.Charge(4L*a.Vertices.Count*b.Vertices.Count);
        return a.Vertices.Any(b.ContainsPoint)||b.Vertices.Any(a.ContainsPoint)||a.Vertices.Select((p,i)=>(A:p,B:a.Vertices[(i+1)%a.Vertices.Count])).Any(e=>
            b.Vertices.Select((p,i)=>(A:p,B:b.Vertices[(i+1)%b.Vertices.Count])).Any(f=>PlanarIntersections.Intersect(e.A,e.B,f.A,f.B).Kind!=IntersectionKind.None));
    }
}
