using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldNetworkRegionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    private static void Check(bool value,string message){if(!value)throw new Exception("Network region: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Region evidence exists");Directory.CreateDirectory(output);
        PrimitiveChecks();
        var e=WorldNetworkPolicyTests.Example();var generator=new WorldMetadataGenerator();
        var seed=generator.Generate(e.Recipe with{NetworkRules=null},e.Catalog);var placement=e.Recipe.Placement!;
        var west=Box(0,0,50,80);var east=new WorldPolygon([new(50,0),new(100,0),new(100,65),new(85,80),new(50,80)]);
        var recipe=e.Recipe with{NetworkRules=e.Recipe.NetworkRules!.Select(r=>r with{RoutingBoundary=r.Id=="planned-supply"?west:east}).ToArray(),
            Placement=placement with{Request=placement.Request with{
                Anchors=[..placement.Request.Anchors,..seed.PlacementPlan!.Placement.Sites.Select(s=>new WorldPlacementAnchor("fixed-"+s.DemandId,s.Position))],
                Relations=[..placement.Request.Relations,..seed.PlacementPlan.Placement.Sites.Select(s=>new WorldPlacementRelation("fixed-"+s.DemandId,s.DemandId,"fixed-"+s.DemandId,"Preserve complete physical program across regional edits","region:fixed",0,0,0,1))]}}};
        var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var baseline=session.Snapshot;var observations=new List<object>();
        Validate(baseline.World);await Save("baseline",recipe,e.Catalog,baseline.World);
        WorldTerrainPatch Block(string rule,string id)
        {
            var edge=baseline.World.NetworkPlan!.Networks.Single(n=>n.RuleId==rule).Plan.Edges.OrderBy(e=>e.Forward.Cost!.Distance).First();
            var points=edge.Forward.Path!.Points;int i=Enumerable.Range(0,points.Count-1).OrderByDescending(i=>points[i].DistanceTo(points[i+1])).First();
            var center=new WorldPoint((points[i].X+points[i+1].X)/2,(points[i].Y+points[i+1].Y)/2);
            return recipe.Surface!.Patches[0] with{Id=id,Priority=1,Kind="water",Boundary=Box(center.X-.5,center.Y-.5,1,1),Purpose="Change a required regional passage at fixed source poses",SourceRuleId="region:"+id};
        }
        var eastWater=Block("field-paths","east-water");var westWater=Block("planned-supply","west-water");
        var wetEast=recipe with{Surface=recipe.Surface! with{Patches=[..recipe.Surface.Patches,eastWater]}};
        Apply("east-water",wetEast,e.Catalog,["field-paths"],["planned-supply"]);
        Check(Geometry(session.Snapshot.World,"field-paths")!=Geometry(baseline.World,"field-paths"),"east water changes actual regional paths");
        var both=recipe with{Surface=recipe.Surface! with{Patches=[..recipe.Surface.Patches,eastWater,westWater]}};
        Apply("both-water",both,e.Catalog,["planned-supply"],["field-paths"]);
        Check(Geometry(session.Snapshot.World,"planned-supply")!=Geometry(baseline.World,"planned-supply"),"west water changes actual regional paths");
        var onlyWest=recipe with{Surface=recipe.Surface! with{Patches=[..recipe.Surface.Patches,westWater]}};
        Apply("dry-east",onlyWest,e.Catalog,["field-paths"],["planned-supply"]);
        Check(Geometry(session.Snapshot.World,"field-paths")==Geometry(baseline.World,"field-paths"),"removing water discovers the original cheaper/clear passage");
        Apply("dry-all",recipe,e.Catalog,["planned-supply"],["field-paths"]);
        // A touching source must invalidate both domains, even when no selected path
        // currently crosses the common boundary and the sampled plane is equivalent.
        var touching=recipe with{Surface=recipe.Surface! with{Patches=[..recipe.Surface.Patches,recipe.Surface.Patches[0] with{
            Id="touching",Priority=1,Boundary=Box(49,70,2,1),Purpose="Shared domain-boundary dependency",SourceRuleId="region:touching"}]}};
        Apply("touching-source",touching,e.Catalog,["field-paths","planned-supply"],[]);
        var expanded=touching with{NetworkRules=touching.NetworkRules!.Select(r=>r.Id=="planned-supply"?r with{RoutingBoundary=Box(0,0,51,80)}:r).ToArray()};
        Apply("boundary-edit",expanded,e.Catalog,["planned-supply"],["field-paths"]);
        var before=session.Snapshot;
        Reject(()=>session.Apply(new("outside","Reject a region outside the concave world","region:bad",Hash(before.Recipe),Hash(before.Catalog),
            touching with{NetworkRules=touching.NetworkRules!.Select(r=>r.Id=="field-paths"?r with{RoutingBoundary=Box(50,0,50,80)}:r).ToArray()},e.Catalog)),"WORLD_SURFACE_REGION_OUTSIDE_DOMAIN");
        Check(Json(session.Snapshot)==Json(before),"regional rejection preserves accepted world/cache/report");
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();try{session.Apply(new("cancelled","Cancel region edit","region:cancel",Hash(before.Recipe),Hash(before.Catalog),recipe,e.Catalog),cancelled.Token);throw new Exception("Cancellation accepted");}catch(OperationCanceledException){}}
        Check(Json(session.Snapshot)==Json(before),"cancellation preserves accepted state");
        await session.PublishAsync(Path.Combine(output,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session","generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"exact source-history checkpoint replay");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: explicit full-width regional routing, disjoint/touching dependencies, terrain-driven wet/dry route changes, independent-policy reuse, complete 24-node/21-physical-source hierarchy, exact warm/clean world/work equality, bounds, mutation isolation, failures/cancellation and checkpoint replay.");
        void Apply(string id,WorldMetadataRecipe next,WorldMetadataCatalog catalog,string[] computed,string[] reused)
        {
            var previous=session.Snapshot;var result=session.Apply(new(id,"Qualified regional dependency edit","region:edit",Hash(previous.Recipe),Hash(previous.Catalog),next,catalog));
            var records=result.Report.NetworkExecutions??[];
            Check(records.Where(r=>r.Source=="Computed").Select(r=>r.RuleId).Order().SequenceEqual(computed.Order()),id+" computed policies");
            Check(records.Where(r=>r.Source=="Accepted").Select(r=>r.RuleId).Order().SequenceEqual(reused.Order()),id+" accepted policies");
            Check(Json(result.World)==Json(generator.Generate(next,catalog)),id+" equals clean world including canonical work");
            Check(Json(result.World.Nodes)==Json(baseline.World.Nodes)&&Json(result.World.AccessPlan!.Sites)==Json(baseline.World.AccessPlan!.Sites),id+" preserves all physical sources/poses");
            Validate(result.World);observations.Add(new{Edit=id,Records=records,result.Report.WorldSha256});
            Save(id,next,catalog,result.World).GetAwaiter().GetResult();
        }
        async Task Save(string mode,WorldMetadataRecipe r,WorldMetadataCatalog c,WorldMetadataDocument expected)
        {
            string folder=Path.Combine(output,mode,"input");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(r));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(c));
            await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(output,mode,"world"));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,mode,"world","world-metadata.json")))==Json(expected),mode+" exact published read replay");
        }
    }
    private static string Geometry(WorldMetadataDocument world,string rule)=>Json(world.NetworkPlan!.Networks.Single(n=>n.RuleId==rule).Plan.Edges.Select(e=>new{e.Link,e.Forward.Path,Reverse=e.Reverse.Path}));
    private static void Validate(WorldMetadataDocument world)
    {
        Check(world.Nodes.Count==24&&world.AccessPlan!.Sites.Count==21&&world.PlacementPlan!.ProgramInstances!.Count==7,"complete source program");
        var access=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,world.AccessPlan!);
        foreach(var network in world.NetworkPlan!.Networks)
        {
            var rule=world.NetworkPlan.Rules.Single(r=>r.Id==network.RuleId);var local=access.RestrictTo(rule.RoutingBoundary!);
            Check(network.Plan.Analysis.Components.Count==1,"all scoped destinations connected");
            foreach(var edge in network.Plan.Edges)foreach(var route in new[]{edge.Forward,edge.Reverse})
                Check(local.ContainsCorridor(route.Path!,network.Plan.Options.Profile.Radius),"complete route envelope inside region and outside solids");
        }
    }
    private static void PrimitiveChecks()
    {
        var region=Box(0,0,10,10);var ground=new WorldTerrainPatch("ground","ground","Ground","test:ground",Box(-5,-5,30,30),0,new(0,0),2,.01,.02);
        var remote=ground with{Id="remote",Boundary=Box(20,20,1,1),Priority=1};var touching=ground with{Id="touching",Boundary=Box(10,5,1,1),Priority=2};
        var crossing=new WorldSurfaceExclusion("crossing","Crossing exclusion","test:crossing",Box(8,4,5,1));var excluded=new WorldSurfaceExclusion("remote-excluded","Remote exclusion","test:excluded",Box(20,10,1,1));
        var source=new WorldSurfaceQuery(new(1,"regional-query","meters",Box(-5,-5,30,30),[crossing,excluded],[ground,remote,touching]),physicalObstacles:[new("remote-solid","Solid","test:solid",Box(15,15,1,1))]);
        region=new(region.Vertices.ToArray());var local=source.RestrictTo(region);
        Check(local.Definition.Patches.Select(p=>p.Id).Order().SequenceEqual(new[]{"ground","touching"}),"containing and touching patches retained, disjoint patch omitted");
        Check(local.Definition.Exclusions.Count==1&&local.PhysicalObstacles.Count==0,"crossing exclusion retained, remote masks/solids omitted");
        var movedSolid=new WorldSurfaceQuery(source.Definition,physicalObstacles:[new("remote-solid","Changed remote solid","test:solid",Box(18,17,2,1))]).RestrictTo(Box(0,0,10,10));
        Check(Hash(new{local.Definition,local.PhysicalObstacles})==Hash(new{movedSolid.Definition,movedSolid.PhysicalObstacles}),"disjoint physical edit preserves complete projected dependency hash");
        var nearSolid=new WorldSurfaceQuery(source.Definition,physicalObstacles:[new("near-solid","Touching solid","test:solid",Box(10,1,2,1))]).RestrictTo(Box(0,0,10,10));
        Check(nearSolid.PhysicalObstacles.Count==1,"touching physical source retained");
        var field=new WorldHeightField(1,"sampled","ground","Sampled terrain","test:field","meters",new(-5,-5),10,10,4,4,Enumerable.Repeat(0d,16).ToArray());
        var sampled=new WorldSurfaceQuery(source.Definition with{HeightField=field}).RestrictTo(Box(0,0,10,10));
        Check(sampled.Definition.HeightField!.Heights.Count==16,"sampled terrain retains complete source buffer");
        var changedHeights=field.Heights.ToArray();changedHeights[^1]=7;
        var changedField=new WorldSurfaceQuery(source.Definition with{HeightField=field with{Heights=changedHeights}}).RestrictTo(Box(0,0,10,10));
        Check(Hash(sampled.Definition)!=Hash(changedField.Definition),"complete source definition preserves remote edits independently of regional memo keys");
        for(int y=0;y<=20;y++)for(int x=0;x<=20;x++){var p=new WorldPoint(x*.5,y*.5);Check(Json(source.Sample(p))==Json(local.Sample(p)),"exact in-region sample semantics");}
        Check(local.Sample(new(11,2)).Status==WorldSurfaceStatus.OutsideDomain,"outside region cannot be traversed");
        ((WorldPoint[])region.Vertices)[0]=new(-100,-100);Check(local.Definition.Boundary!.Vertices[0]==new WorldPoint(0,0),"regional input detached");
        var concave=new WorldSurfaceQuery(new(1,"concave","meters",new([new(0,0),new(10,0),new(10,3),new(3,3),new(3,10),new(0,10)]),[],[ground]));
        Reject(()=>concave.RestrictTo(new([new(1,8),new(8,1),new(1,1)])),"WORLD_SURFACE_REGION_OUTSIDE_DOMAIN");
        Reject(()=>source.RestrictTo(new([new(double.NaN,0),new(1,0),new(0,1)])),"WORLD_SURFACE_REGION_INVALID");
        var barrier=new WorldSurfaceQuery(new(1,"barrier","meters",Box(0,0,20,20),[new("wall","Wall","test:wall",Box(4,0,2,10))],[ground]));
        var profile=new WorldTraversalProfile("walk","Walk","test:walk",.2,1,new Dictionary<string,double>{{"ground",1}},MaximumHeightStep:1);
        var request=new WorldRouteRequest("walk-test","Walk","test:walk",new(2,5),new(8,5),profile,new(.3,[],MaximumWork:20000000));
        Check(new WorldSurfaceRoutePlanner().Plan(barrier,request).Status==WorldRouteStatus.Found,"global route can detour outside local domain");
        Check(new WorldSurfaceRoutePlanner().Plan(barrier.RestrictTo(Box(0,0,10,10)),request).Status!=WorldRouteStatus.Found,"region is an enforced constraint, not a cache hint");
        var fullWidth=new WorldSurfaceQuery(new(1,"full-width","meters",Box(0,0,20,20),[],[ground]));
        var elbow=fullWidth.RestrictTo(concave.Definition.Boundary!);var elbowRoute=request with{From=new(1,8),To=new(8,1)};
        var bent=new WorldSurfaceRoutePlanner().Plan(elbow,elbowRoute);
        Check(bent.Status==WorldRouteStatus.Found&&elbow.ContainsCorridor(bent.Path!,profile.Radius)&&bent.Path!.Points.Count>2,
            "concave route domain produces a width-qualified bend rather than crossing its missing corner");
        var boundaryRoute=request with{From=new(.1,2),To=new(.1,8)};
        Check(new WorldSurfaceRoutePlanner().Plan(fullWidth,boundaryRoute).Status!=WorldRouteStatus.Found,"centerline inside a region is insufficient when the full envelope leaves it");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{source.RestrictTo(Box(0,0,10,10),cancellation.Token);throw new Exception("Region cancellation ignored");}catch(OperationCanceledException){}
    }
    private static void Reject(Action action,string code)
    {try{action();throw new Exception("Expected refusal: "+code);}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)||e.InnerException?.Message.Contains(code,StringComparison.Ordinal)==true){}}
}
