using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldLayeredIntegrationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Layered integration: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6*Math.Max(1,Math.Abs(b)),message);
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool coastal=false)
    {
        var e=WorldMixedProgramTests.Example(coastal);var original=e.Recipe.LocalLayouts!;
        var layouts=original with{Layouts=original.Layouts.Select(l=>l with{
            Strategy=WorldLocalLayoutStrategy.AreaBalanced,Purpose="Generate required activity spaces in "+l.Id,Programs=[],Grid=null,Partition=null,RegionFrame=null,
            ProgramIntents=l.Programs.Select(p=>new WorldLocalProgramIntent(p.Id,p.Purpose,p.SourceRuleId,p.MinimumArea))
                .Concat(l.Id=="districts"?[new("landscape","Required landscape and environmental support","layered:landscape",2000)]:[]).ToArray(),AreaPattern=new()}).ToArray(),
            Interfaces=[new("macro-gateway","ordered","irregular","Retain a broad shared activity boundary","layered:gateway",10)]};
        var placement=e.Recipe.Placement!;
        var environment=e.Recipe.Environment! with{Fields=[..e.Recipe.Environment.Fields,
            new("landscape-density","Game-authored landscape demand","layered:density","relative",1,[new(WorldFieldInput.X,.002)])]};
        var sourceRecipe=e.Recipe with{GameProfile="framework-layered-reference",Environment=environment,LocalLayouts=layouts,
            Regions=e.Recipe.Regions! with{Seeds=[],Intents=[new("play-area","Terrain-selected complete activity domain","layered:region",25000,10)]},
            Placement=placement with{Request=placement.Request with{Anchors=[],Execution=WorldPlacementExecution.Prescribed,
                Relations=placement.Request.Demands.Select(d=>new WorldPlacementRelation(d.Id+"-position",d.Id,d.Id+"-derived","Use generated activity-space anchor","layered:position",0,0,0,1)).ToArray()},
                AllocationAnchors=placement.Request.Demands.Select(d=>new MetadataAllocationAnchor(d.Id+"-derived",d.AllocationId!,"Derive required activity position from its generated space","layered:anchor")).ToArray()},
            RemainingScopes=["Layered framework integration with complete seven-compound source content; source design/scale acceptance and Godot remain open."]};
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId);
        var feature=new WorldMetadataArchetype("habitat-feature","environment-feature","Required landscape contributor",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Exact landscape element","layered:feature",Box(-.5,-.5,1,1)),Access:new(MetadataOccupancyKind.Solid,[]));
        var catalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id==root.Id?a with{
            Parts=[..a.Parts,new("landscape-elements",feature.Id,16,"Required landscape demand, never below its catalog minimum")],
            ChildRequirements=[..a.ChildRequirements,new("environment-feature",16,64)]}:a),feature]};
        var demand=placement.Request.Demands[0];var finalPlacement=sourceRecipe.Placement!;
        var recipe=sourceRecipe with{PopulationSource=new(sourceRecipe,e.Catalog),
            TerrainDemands=new([new("landscape-demand","Derive complete landscape count from source support","layered:demand",sourceRecipe.Id,"landscape-elements","play-area",250,
                DensityFieldId:"landscape-density",AllocationId:"landscape",MaximumCount:64)]),
            Placement=finalPlacement with{Request=finalPlacement.Request with{Demands=[..finalPlacement.Request.Demands,
                new("landscape-elements","All required landscape contributors","layered:population",null,demand.Profile,RegionId:"play-area",AllocationId:"landscape")]},
                Bindings=[..finalPlacement.Bindings,new("landscape-elements",sourceRecipe.Id,true,PartId:"landscape-elements",
                    Population:new("Populate the complete declared landscape support","layered:population",Region:new("play-area",AllocationId:"landscape",Area:new("landscape-density"))))]},
            RemainingScopes=["Complete required seven-compound program and all derived landscape contributors; allocations are purposeful spaces, not occupied-world or full-game coverage. World-scale/design acceptance and Godot remain open."]};
        return(recipe,catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(bool coast in new[]{false,true})
        {
            string folder=Path.Combine(directory,coast?"coastal":"inland");Directory.CreateDirectory(folder);var e=Example(coast);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(bool coast in new[]{false,true})
        {
            var e=Example(coast);var timer=System.Diagnostics.Stopwatch.StartNew();var world=generator.Generate(e.Recipe,e.Catalog);timer.Stop();
            Qualify(world,e.Catalog);worlds.Add(world);
            Console.WriteLine($"Qualified {(coast?"coastal":"inland")}: {world.Nodes.Count} nodes, {world.AccessPlan!.Sites.Count} physical members, {world.RoutePlan!.Routes.Count} local routes, {world.NetworkPlan!.Networks[0].Plan.Edges.Count} regional edges, {world.TerrainDemandPlan!.Results[0].Count} landscape members; generate {timer.Elapsed.TotalSeconds:F3}s.");
        }
        Check(Json(worlds[0].Nodes.Where(n=>n.Kind!="environment-feature").Select(n=>n with{Position=null}))==
            Json(worlds[1].Nodes.Where(n=>n.Kind!="environment-feature").Select(n=>n with{Position=null})),"terrain retains all required source identities");
        Check(Json(worlds[0].Nodes.Select(n=>n.Position))!=Json(worlds[1].Nodes.Select(n=>n.Position)),"terrain changes generated physical layout");
        var input=Example();var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;var changed=Example(true);
        var accepted=session.Apply(new("coast","Recompute layered world from changed terrain","layered:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(Json(accepted.World)==Json(worlds[1]),"terrain edit equals clean complete world generation");
        var bad=changed.Recipe with{LocalLayouts=changed.Recipe.LocalLayouts! with{Layouts=changed.Recipe.LocalLayouts.Layouts.Select(l=>l with{
            ProgramIntents=l.ProgramIntents!.Select(i=>i with{MinimumArea=1000000}).ToArray()}).ToArray()}};
        try{session.Apply(new("impossible","Do not shrink required programs","layered:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,bad,changed.Catalog));throw new Exception("Missing refusal");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"failed layered edit preserves accepted world");
        string output=Path.Combine(Path.GetTempPath(),"layered-world-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(accepted)==Json(restored.Snapshot),"source history replays layered world");
        Console.WriteLine("PASS: layered terrain intent â†’ generated macro/activity spaces â†’ complete source-sized compounds â†’ local/regional paths â†’ terrain-funded landscape â†’ chunk ownership; inland/coastal counts/geometry/provenance and edit/rollback/checkpoint replay.");
    }
    internal static void Qualify(WorldMetadataDocument world,WorldMetadataCatalog catalog)
    {
        int count=world.TerrainDemandPlan!.Results.Single().Count;
        Check(count>=16&&count<=64&&world.Nodes.Count==43+count&&world.AccessPlan!.Sites.Count==42+count,"all source and derived members retained");
        Check(world.Nodes.Count(n=>n.Kind=="compound")==7&&world.Nodes.Count(n=>n.Kind=="structure")==14&&world.Nodes.Count(n=>n.Kind=="ground-area")==7&&world.Nodes.Count(n=>n.Kind=="equipment")==14,"all nested compound parts retained");
        Check(world.LocalLayoutPlan!.Definition.Layouts.Count==3&&world.LocalLayoutPlan.Definition.Layouts.All(l=>l.Strategy==WorldLocalLayoutStrategy.AreaBalanced&&l.Programs.Count==0&&l.RegionFrame is null),"all macro/activity positions generated from intent");
        Check(world.LocalLayoutPlan.GeneratedAreas!.Count==3&&world.RegionPlan!.Request.Seeds.Count==0&&world.RegionPlan.Request.Intents!.Count==1,"generated terrain region and three complete area plans");
        Check(world.RoutePlan!.Routes.Count==35&&world.NetworkPlan!.Networks.Single().Plan.Edges.Count==6,"every local route and connected regional tree retained");
        var trace=world.PlacementPlan!.PopulationAreas!.Single();Check(trace.NodeIds.Count==count&&trace.AllocationId=="landscape","complete landscape provenance");
        Near(trace.Plan.Mass,world.TerrainDemandPlan.Results[0].Measure.Mass,"demand/population share exact support measure");
        Check((int)Math.Floor(trace.Plan.Mass/250)==count,"whole derived count, no budget clamp");
        var source=world.PlacementPlan.Definition.SourceWorld!;
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"source member exact after population");
        foreach(var route in source.RoutePlan!.Routes)Check(Json(route)==Json(world.RoutePlan.Routes.Single(r=>r.ConnectionId==route.ConnectionId)),"source local passage exact");
        Check(Json(source.NetworkPlan!.Networks)==Json(world.NetworkPlan!.Networks),"source regional passage data exact");
        Check(world.ChunkPlan!.Members.Count==world.Nodes.Count+2*(35+6)&&world.ChunkPlan.Members.Select(m=>(m.Kind,m.Id)).Distinct().Count()==world.ChunkPlan.Members.Count,"canonical storage ownership for all nodes and directed paths");
        var lookup=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var sites=world.AccessPlan!.Sites.ToDictionary(s=>s.NodeId,StringComparer.Ordinal);
        foreach(var site in sites.Values)
        {
            var node=lookup[site.NodeId];var shape=catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId).PhysicalFootprint!.Polygon;
            Check(shape.Vertices.Count==site.Footprint.Vertices.Count&&shape.Vertices.Zip(site.Footprint.Vertices).All(p=>p.First.Offset(node.Position![0],node.Position[1]).DistanceTo(p.Second)<1e-7),"source dimensions/placement exact");
            if(node.ParentId!=world.Id)Check(sites[node.ParentId!].Footprint.ContainsPolygon(site.Footprint),"whole nested member inside exact parent");
        }
        IndependentPaths(world);
        WorldMetadataGenerator.ValidateDocument(world);
    }

    private static void IndependentPaths(WorldMetadataDocument world)
    {
        var sites=world.AccessPlan!.Sites.ToDictionary(s=>s.NodeId,StringComparer.Ordinal);
        var obstacles=sites.Values.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid).Select(s=>s.Footprint)
            .Concat(world.Surface!.Patches.Where(p=>p.Kind=="water").Select(p=>p.Boundary)).ToArray();
        var triangle=new WorldPolygon([new(0,0),new(2,0),new(0,2)]);
        Check(Math.Abs(SegmentPolygonDistance(new(3,0),new(3,2),triangle)-1)<1e-12&&
            Math.Abs(SegmentPolygonDistance(new(2,2),new(3,3),triangle)-Math.Sqrt(2))<1e-12&&
            SegmentPolygonDistance(new(.5,.5),new(.6,.6),triangle)==0&&
            SegmentPolygonDistance(new(-1,1),new(3,1),triangle)==0,"independent polygon oracle analytic separation, containment and crossing");
        int directions=0;var graph=sites.Values.Where(s=>s.Ports.Count>0).ToDictionary(s=>s.NodeId,_=>new HashSet<string>(StringComparer.Ordinal),StringComparer.Ordinal);
        void Path(WorldMetadataConnection link,string fromPort,string toPort,WorldRouteResult route,bool reverse,double radius)
        {
            string from=reverse?link.ToId:link.FromId,to=reverse?link.FromId:link.ToId;
            var start=sites[from].Ports.Single(p=>p.Id==(reverse?toPort:fromPort)).ApproachPosition;
            var end=sites[to].Ports.Single(p=>p.Id==(reverse?fromPort:toPort)).ApproachPosition;
            var points=route.Path!.Points;Check(points[0]==start&&points[^1]==end,"exact qualified endpoint continuity");directions++;graph[from].Add(to);
            foreach(var obstacle in obstacles)for(int i=1;i<points.Count;i++)
            {
                double distance=SegmentPolygonDistance(points[i-1],points[i],obstacle);
                double minX=obstacle.Vertices.Min(p=>p.X),maxX=obstacle.Vertices.Max(p=>p.X),minY=obstacle.Vertices.Min(p=>p.Y),maxY=obstacle.Vertices.Max(p=>p.Y);
                if(obstacle.Vertices.Count==4&&obstacle.Vertices.All(p=>(p.X==minX||p.X==maxX)&&(p.Y==minY||p.Y==maxY)))
                    Check(Math.Abs(distance-SegmentRectangleDistance(points[i-1],points[i],obstacle))<1e-7,"polygon oracle agrees with independent rectangle slab distance");
                Check(distance>=radius-1e-7,"independent full-width solid/water clearance");
            }
        }
        foreach(var route in world.RoutePlan!.Routes)
        {
            var rule=world.RoutePlan.Rules.Single(r=>r.Id==route.RuleId);var link=world.Connections.Single(c=>c.Id==route.ConnectionId);
            Path(link,rule.FromPortId!,rule.ToPortId!,route.Forward,false,rule.Profile.Radius);Path(link,rule.FromPortId!,rule.ToPortId!,route.Reverse!,true,rule.Profile.Radius);
        }
        foreach(var network in world.NetworkPlan!.Networks)
        {
            var rule=world.NetworkPlan.Rules.Single(r=>r.Id==network.RuleId);
            foreach(var edge in network.Plan.Edges){var link=world.Connections.Single(c=>c.Id==edge.Link.Id);Path(link,rule.PortId!,rule.PortId!,edge.Forward,false,rule.Options.Profile.Radius);Path(link,rule.PortId!,rule.PortId!,edge.Reverse!,true,rule.Options.Profile.Radius);}
        }
        Check(directions==82,"all source/local/regional directed paths independently checked");
        var reached=new HashSet<string>(StringComparer.Ordinal);var pending=new Queue<string>();pending.Enqueue(graph.Keys.Order(StringComparer.Ordinal).First());
        while(pending.TryDequeue(out var id)){if(!reached.Add(id))continue;foreach(string next in graph[id])pending.Enqueue(next);}
        Check(reached.Count==42&&reached.Count==graph.Count,"all declared interaction destinations connected");
    }
    // Independent test geometry: parity containment plus edge distances. No
    // production intersection, polygon containment or traversal owner is called.
    private static double SegmentPolygonDistance(WorldPoint a,WorldPoint b,WorldPolygon polygon)
    {
        bool Inside(WorldPoint p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Vertices.Count-1;i<polygon.Vertices.Count;j=i++)
            {
                var u=polygon.Vertices[i];var v=polygon.Vertices[j];
                if((u.Y>p.Y)!=(v.Y>p.Y)&&p.X<(v.X-u.X)*(p.Y-u.Y)/(v.Y-u.Y)+u.X)inside=!inside;
            }
            return inside;
        }
        static double Cross(WorldPoint p,WorldPoint q,WorldPoint r)=>(q.X-p.X)*(r.Y-p.Y)-(q.Y-p.Y)*(r.X-p.X);
        static double PointDistance(WorldPoint p,WorldPoint u,WorldPoint v)
        {
            double dx=v.X-u.X,dy=v.Y-u.Y,length=dx*dx+dy*dy;
            double t=length==0?0:Math.Clamp(((p.X-u.X)*dx+(p.Y-u.Y)*dy)/length,0,1);
            return Math.Sqrt(Math.Pow(p.X-u.X-t*dx,2)+Math.Pow(p.Y-u.Y-t*dy,2));
        }
        if(Inside(a)||Inside(b))return 0;
        double distance=double.PositiveInfinity;
        for(int i=0;i<polygon.Vertices.Count;i++)
        {
            var u=polygon.Vertices[i];var v=polygon.Vertices[(i+1)%polygon.Vertices.Count];
            double x=Cross(a,b,u),y=Cross(a,b,v),z=Cross(u,v,a),w=Cross(u,v,b);
            if((x<0&&y>0||x>0&&y<0)&&(z<0&&w>0||z>0&&w<0))return 0;
            distance=Math.Min(distance,Math.Min(Math.Min(PointDistance(a,u,v),PointDistance(b,u,v)),Math.Min(PointDistance(u,a,b),PointDistance(v,a,b))));
        }
        return distance;
    }
    private static double SegmentRectangleDistance(WorldPoint a,WorldPoint b,WorldPolygon rectangle)
    {
        double minX=rectangle.Vertices.Min(p=>p.X),maxX=rectangle.Vertices.Max(p=>p.X),minY=rectangle.Vertices.Min(p=>p.Y),maxY=rectangle.Vertices.Max(p=>p.Y),lo=0,hi=1;
        bool Slab(double origin,double delta,double min,double max)
        {
            if(delta==0)return origin>=min&&origin<=max;double t=(min-origin)/delta,u=(max-origin)/delta;if(t>u)(t,u)=(u,t);
            lo=Math.Max(lo,t);hi=Math.Min(hi,u);return lo<=hi;
        }
        if(Slab(a.X,b.X-a.X,minX,maxX)&&Slab(a.Y,b.Y-a.Y,minY,maxY))return 0;
        double PointToBox(WorldPoint p)=>Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(minX-p.X,p.X-maxX)),2)+Math.Pow(Math.Max(0,Math.Max(minY-p.Y,p.Y-maxY)),2));
        double dx=b.X-a.X,dy=b.Y-a.Y,length=dx*dx+dy*dy,result=Math.Min(PointToBox(a),PointToBox(b));
        foreach(var p in rectangle.Vertices){double t=length==0?0:Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length,0,1);result=Math.Min(result,p.DistanceTo(new(a.X+t*dx,a.Y+t*dy)));}
        return result;
    }
}
