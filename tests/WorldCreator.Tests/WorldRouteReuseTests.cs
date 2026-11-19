using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRouteReuseTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string text){if(!ok)throw new Exception("Route reuse: "+text);}
    private static void Near(double a,double b,string text)=>Check(Math.Abs(a-b)<1e-6,text+": "+a+" / "+b);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldTraversalProfile Profile()=>new("foot","Shared exploration trails","test:foot",1,.5,new Dictionary<string,double>{{"meadow",1}});
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurfaceTests.Example();
        MetadataPart Part(string id,double x,double y)=>new(id,"location",1,"Required exploration destination",Layout:MetadataLineLayout.Parameters([x,y],[0,0]));
        var root=e.Catalog.Archetypes[0] with{Parts=[Part("a",10,20),Part("b",90,20),Part("c",40,25),Part("d",60,25)],Connections=[]};
        return(e.Recipe with{Id="corridor-reuse",Origin=[0,0],Surface=WorldSurfaceTests.Surface("flat"),
            AdditionalConnections=[new("a-trunk","travel","corridor-reuse/a-0","corridor-reuse/b-0"),new("b-branch","travel","corridor-reuse/c-0","corridor-reuse/d-0")],
            RouteRules=[new("trails","travel",Profile(),new(.5,[],MaximumWork:20000000),Reuse:new(5))],
            Corridors=new([new("trails","travel","example:trails",0,"connect-at-grade")]),
            RemainingScopes=["Diagnostic reuse-cost example; adaptive regions, shared capacity, world design, artwork and native runtime remain open"]},e.Catalog with{Archetypes=[root,e.Catalog.Archetypes[1]]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("EXAMPLE_EXISTS");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var surface=WorldSurfaceTests.Surface("flat");var profile=Profile();var planner=new WorldSurfaceRoutePlanner();
        var source=new WorldRouteReusePath("trunk",new([new(10,20),new(90,20)]),profile,true);
        var request=new WorldRouteRequest("branch","Reach destination","test:branch",new(40,25),new(60,25),profile,new(.5,[]));
        var baseline=planner.Plan(surface,request);Near(baseline.Cost!.Distance,20,"baseline direct distance");
        var reuse=request with{Reuse=new(new(5),[source])};var shared=planner.Plan(surface,reuse);
        Check(shared.Status==WorldRouteStatus.Found&&shared.Path!.Points.Count==4,"reuse changes real geometry to two connectors and trunk");
        Near(shared.Cost!.Distance,30,"analytic physical distance");Near(shared.Cost.ReusedDistance,20,"analytic reused trunk");
        Near(shared.Cost.NewPassage,50,"10m new passage times declared cost 5");Near(shared.Cost.Total,80,"physical and construction costs stay distinct");
        Check(shared.Path!.Points.SequenceEqual(new WorldPoint[]{new(40,25),new(40,20),new(60,20),new(60,25)}),"source projections create explicit joins without snapping endpoints");
        WorldPoint Rotate(WorldPoint p)=>new(p.X*Math.Cos(.41)-p.Y*Math.Sin(.41)-100,p.X*Math.Sin(.41)+p.Y*Math.Cos(.41)-100);
        WorldPolygon Polygon(WorldPolygon p)=>new(p.Vertices.Select(Rotate).ToArray());
        var obliqueSurface=surface with{Boundary=Polygon(surface.Boundary!),Patches=surface.Patches.Select(p=>p with{Boundary=Polygon(p.Boundary),Origin=Rotate(p.Origin)}).ToArray()};
        var oblique=planner.Plan(obliqueSurface,reuse with{From=Rotate(request.From),To=Rotate(request.To),
            Reuse=new(new(5),[source with{Path=new(source.Path.Points.Select(Rotate).ToArray())}])});
        Near(oblique.Cost!.Distance,30,"oblique negative-coordinate passage distance");Near(oblique.Cost.ReusedDistance,20,"oblique sharing is not tied to axes");
        var cheap=planner.Plan(surface,reuse with{Reuse=reuse.Reuse! with{Settings=new(.1)}});Near(cheap.Cost!.Distance,20,"small new-passage cost does not justify detour");
        Near(cheap.Cost.NewPassage,2,"direct passage charged normally");
        var zero=planner.Plan(surface,reuse with{Reuse=reuse.Reuse! with{Settings=new(0)}});Near(zero.Cost!.Total,20,"zero cost preserves direct geometry");
        var reverse=planner.Plan(surface,reuse with{From=request.To,To=request.From,Reuse=new(new(5),[source with{Bidirectional=false}])});
        Near(reverse.Cost!.Distance,20,"one-way source cannot be reused in reverse");Near(reverse.Cost.ReusedDistance,0,"no reverse permission invented");
        var overlap=source with{Id="overlap",Path=new([new(30,20),new(70,20)])};
        var union=planner.Plan(surface,reuse with{Reuse=new(new(5),[source,overlap])});Near(union.Cost!.ReusedDistance,20,"overlapping sources counted once");Near(union.Cost.NewPassage,50,"union does not double-discount construction");
        Check(Json(union)==Json(planner.Plan(surface,reuse with{Reuse=new(new(5),[overlap,source])})),"source declaration order is deterministic");
        Reject(()=>planner.Plan(surface,reuse with{Reuse=new(new(5),[source with{Profile=profile with{Radius=2}}])}),"WORLD_ROUTE_REUSE_PROFILE_INCOMPATIBLE");
        Reject(()=>planner.Plan(surface,reuse with{Reuse=new(new(5,1),[source,overlap])}),"WORLD_ROUTE_REUSE_EDGE_BUDGET");
        Reject(()=>planner.Plan(surface,reuse with{Reuse=new(new(-1),[source])}),"WORLD_ROUTE_REUSE_SETTINGS_INVALID");
        Reject(()=>planner.Plan(surface,reuse with{Search=request.Search with{MaximumWork=1}}),"WORLD_ROUTE_WORK_BUDGET");
        var obstacle=new WorldPolygon([new(12,19),new(13,19),new(13,21),new(12,21)]);
        Reject(()=>planner.Plan(surface with{Exclusions=[new("edit","Changed terrain","test:edit",obstacle)]},reuse),"WORLD_ROUTE_REUSE_SOURCE_UNRESOLVED");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(surface,reuse,c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var branch=world.RoutePlan!.Routes.Single(r=>r.ConnectionId.EndsWith("b-branch",StringComparison.Ordinal));
        Near(branch.Forward.Cost!.ReusedDistance,20,"metadata second connection reuses actual previous route");
        Near(branch.Reverse!.Cost!.NewPassage,0,"return journey can reuse established bidirectional passage");
        Check(world.CorridorPlan!.Groups.Single().Segments.Count==5,"realized paths become one shared trunk and connectors");
        new MetadataRoutePlanner().ValidateDocument(world);new MetadataCorridorPlanner().ValidateDocument(world);
        var independent=generator.Generate(e.Recipe with{RouteRules=[e.Recipe.RouteRules![0] with{Reuse=null}]},e.Catalog);
        Near(independent.RoutePlan!.Routes.Single(r=>r.ConnectionId.EndsWith("b-branch",StringComparison.Ordinal)).Forward.Cost!.Distance,20,"unselected metadata reuse retains independent routing");
        var options=new WorldNetworkOptions("Connect game destinations","test:network","region",profile,new(.5,[],MaximumWork:20000000),MaximumWork:100000000,
            RequiredConnections:[new("a","b"),new("c","d")]);
        WorldNetworkDestination[] destinations=[new("a","West",new(10,20)),new("b","East",new(90,20)),new("c","Southwest",new(40,25)),new("d","Southeast",new(60,25))];
        var networkPlanner=new WorldDestinationNetworkPlanner();var netRequest=new WorldNetworkRequest("network",destinations,options);
        var old=networkPlanner.Plan(surface,netRequest);var net=networkPlanner.Plan(surface,netRequest with{Options=options with{Reuse=new(5)}});
        Check(net.Status==WorldNetworkStatus.Found&&net.Edges.Select(x=>x.Link.Id).SequenceEqual(old.Edges.Select(x=>x.Link.Id)),"reuse realization preserves selected logical topology");
        Check(net.Edges.Any(x=>x.Reason=="required"&&x.Forward.Path!.Points.Count>2&&x.Forward.Cost!.ReusedDistance>0),"required network route bends onto earlier passage");
        Check(Json(net)==Json(networkPlanner.Plan(surface,netRequest with{Destinations=destinations.Reverse().ToArray(),Options=options with{Reuse=new(5)}})),"network destination order does not alter sequential realization");
        var metadataNetwork=generator.Generate(e.Recipe with{AdditionalConnections=null,RouteRules=null,
            NetworkRules=[new("network","corridor-reuse","destination",4,4,"travel",options with{Reuse=new(5),RequiredConnections=[new("corridor-reuse/a-0","corridor-reuse/b-0"),new("corridor-reuse/c-0","corridor-reuse/d-0")]},"intent")]},e.Catalog);
        new MetadataNetworkPlanner().ValidateDocument(metadataNetwork);new MetadataCorridorPlanner().ValidateDocument(metadataNetwork);
        var forged=world with{RoutePlan=world.RoutePlan with{Routes=world.RoutePlan.Routes.Select(r=>r==branch?r with{Forward=r.Forward with{Cost=r.Forward.Cost! with{ReusedDistance=999}}}:r).ToArray()}};
        Reject(()=>new MetadataRoutePlanner().ValidateDocument(forged),"METADATA_ROUTE_PLAN_MISMATCH");
        var packet=await new WorldMetadataSchematicExporter().CreateAsync(world);Check(packet.Html.Contains("NewPassage"),"export retains explicit cost accounting");
        var f=WorldPlacementFeedbackTests.Example();var feedbackWorld=generator.Generate(f.Recipe with{RouteRules=[f.Recipe.RouteRules![0] with{Reuse=new(5)}]},f.Catalog);
        Check(feedbackWorld.PlacementPlan!.FeasibilityAttempts!.Count>1,"reuse retains bounded placement reconsideration");
        new MetadataPlacementPlanner().ValidateDocument(feedbackWorld);new MetadataRoutePlanner().ValidateDocument(feedbackWorld);
        string directory=Path.Combine(Path.GetTempPath(),"worldcreator-reuse-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(directory);var output=Path.Combine(directory,"output");
            await generator.GenerateFilesAsync(Path.Combine(directory,"recipe.json"),Path.Combine(directory,"catalog.json"),output);
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"))),"reuse settings, costs and geometry survive exact disk replay");
            var bad=Path.Combine(directory,"forged.json");await File.WriteAllTextAsync(bad,Json(forged));
            try{await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(bad);throw new Exception("Forged file admitted");}
            catch(ArgumentException ex)when(ex.Message=="METADATA_ROUTE_PLAN_MISMATCH"){}
        }
        finally{if(Directory.Exists(directory)&&Path.GetDirectoryName(Path.GetFullPath(directory))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(directory).StartsWith("worldcreator-reuse-",StringComparison.Ordinal))Directory.Delete(directory,true);}
        Console.WriteLine("PASS: analytic reuse attraction, cost threshold, directed overlap union, profile/terrain/budget/cancellation rejection, metadata sequential routes, network realization and replay/tamper/export. No globally optimal network, shared service-capacity, adaptive-region or full-world claim.");
    }
}
