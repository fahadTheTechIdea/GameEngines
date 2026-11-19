using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldInterfaceTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool mixed=false)
    {
        var e=WorldLocalLayoutTests.Example();
        var site=e.Catalog.Archetypes.Single(a=>a.Id=="site") with{Access=new(MetadataOccupancyKind.Solid,[
            new("east","Eastern entrance","interface:east",new(4,0),new(5,0),.8),
            new("west","Western entrance","interface:west",new(-4,0),new(-5,0),.8),
            new("south","Southern entrance","interface:south",new(0,3),new(0,4),.8),
            new("north","Northern entrance","interface:north",new(0,-3),new(0,-4),.8)])};
        var move=new WorldTraversalProfile("walking","Connect required local programs","interface:walking",.3,1,new Dictionary<string,double>{{"ground",1}},AscentCost:2,DescentCost:.5);
        var rule=new MetadataRouteRule("west-route","west-travel",move,new(.1,[],MaximumCandidates:512,MaximumWork:5000000),"east","west",
            Interfaces:[new("local-layouts/explicit/west","west-link")]);
        var recipe=e.Recipe with{AdditionalConnections=[new("west","west-travel","local-layouts/sites-0","local-layouts/sites-1",true)],RouteRules=[rule],
            RemainingScopes=["Physical connections between local allocations, exact catalog-sized sites and constituent metadata. Adaptive repartition, full content, GameArts and native Godot acceptance remain open."]};
        if(mixed)
            return(recipe with{AdditionalConnections=[..recipe.AdditionalConnections!,new("east","east-travel","local-layouts/sites-4","local-layouts/sites-6",true)],
                RouteRules=[rule with{Search=rule.Search with{MaximumWork=2500000}},rule with{Id="east-route",ConnectionKind="east-travel",FromPortId="south",ToPortId="north",
                    Search=rule.Search with{MaximumWork=2500000},Interfaces=[new("local-layouts/explicit/east","east-link")]}]},
                e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?site:a).ToArray()});
        var boundary=Box(0,0,40,20);var source=recipe.Surface!;
        var region=recipe.Regions! with{Boundary=boundary,Seeds=[new("west","Required play area","interface:region",new(8,10),700)]};
        var local=recipe.LocalLayouts!.Layouts[0] with{Grid=new(new(0,0),0,20,20),Programs=[
            new("west-a","First required program","interface:a",new(8,10),300,Column:0,Row:0),
            new("west-b","Second required program","interface:b",new(32,10),300,Column:1,Row:0)]};
        var request=recipe.Placement!.Request;
        var demands=request.Demands.Take(2).ToArray();
        return(recipe with{Origin=[0,0],Surface=source with{Boundary=boundary,Exclusions=[],Patches=[source.Patches[0] with{Boundary=boundary,GradientX=.1,GradientY=0}]},Regions=region,
            LocalLayouts=new([local],[new("west-link","west-a","west-b","Required shared passage","interface:span",10)]),
            Placement=recipe.Placement with{Request=request with{Boundary=boundary,Demands=demands,Anchors=[new("a",new(8,10)),new("b",new(32,10))],
                Relations=[new("a","west-a","a","Preserve first position","interface:anchor",0,0,0,1),new("b","west-b","b","Preserve second position","interface:anchor",0,0,0,1)]},
                Bindings=recipe.Placement.Bindings.Take(2).ToArray()}},
            e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?site:a.Id==recipe.RootArchetypeId?a with{Parts=[a.Parts[0] with{Count=2}],ChildRequirements=[new("site",2,2)]}:a).ToArray()});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example(true);
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? output=null)
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var routed=world.RoutePlan!.Routes.Single();var passage=routed.Interface!;
        Check(passage is not null&&passage.Attempts.Count==1&&passage.Attempts[0].Accepted,"qualified midpoint accepted");
        Near(passage!.Center.X,20,"shared boundary x");Near(passage.Center.Y,10,"midpoint y");
        Near(routed.Forward.Path!.Points[0].X,13,"exact source entrance approach");Near(routed.Forward.Path.Points[^1].X,27,"exact destination approach");
        Near(routed.Forward.Cost!.Distance,14,"analytic straight path length");Near(routed.Forward.Cost.Ascent,2.8,"uphill cost");Near(routed.Reverse!.Cost!.Descent,.7,"reverse downhill cost");
        var entry=passage.Forward.Crossing.Path!.Points[0];var stepSource=e.Recipe.Surface!.Patches[0];
        var steppedRecipe=e.Recipe with{Surface=e.Recipe.Surface with{Patches=[stepSource,stepSource with{
            Id="join-step",Priority=1,Boundary=Box(entry.X,0,40-entry.X,20),Elevation=stepSource.Elevation+.005}]},
            RouteRules=[e.Recipe.RouteRules![0] with{Profile=e.Recipe.RouteRules[0].Profile with{MaximumHeightStep=.01}}]};
        var stepped=generator.Generate(steppedRecipe,e.Catalog);
        Near(stepped.RoutePlan!.Routes[0].Forward.Cost!.Ascent,2.81,"a composed interface join retains the permitted step climb");
        Near(stepped.RoutePlan.Routes[0].Reverse!.Cost!.Descent,.7025,"reverse composed join retains downhill step cost");
        if(output is not null)
        {
            if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve interface step evidence");Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(steppedRecipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
            await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
            Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(stepped),"composed step cost and source replay survive publication");
        }
        Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2,"all sites and constituents preserved");
        foreach(var placed in world.PlacementPlan!.Placement.Sites){Near(placed.Footprint.Footprint.SignedArea(),48,"source footprint");Near(placed.Reservation!.Footprint.SignedArea(),80,"source reservation");}
        CheckJourney(routed.Forward,passage.Forward,false);CheckJourney(routed.Reverse,passage.Reverse!,true);
        new MetadataRoutePlanner().ValidateDocument(world);new MetadataPlacementPlanner().ValidateDocument(world);
        var changed=routed with{Interface=passage with{Center=passage.Center.Offset(0,1)}};
        Reject(()=>new MetadataRoutePlanner().ValidateDocument(world with{RoutePlan=world.RoutePlan with{Routes=[changed]}}),"METADATA_ROUTE_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{RouteRules=[e.Recipe.RouteRules![0] with{Interfaces=[new("local-layouts/explicit/absent","west-link")]}]},e.Catalog),"METADATA_INTERFACE_BINDING_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{RouteRules=[e.Recipe.RouteRules![0] with{Search=e.Recipe.RouteRules[0].Search with{MaximumWork=1}}]},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{generator.Generate(e.Recipe,e.Catalog,c.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        // A traversal-only barrier does not alter allocation topology. It blocks the midpoint,
        // so the next declared crossing must be tried, rather than bypassing the interface.
        var surface=e.Recipe.Surface!;var barrier=surface.Patches[0] with{Id="barrier",Kind="water",Priority=1,Boundary=Box(19.5,9,1,2)};
        var fields=e.Recipe.Regions!.Profile with{AllowedKinds=["ground","water"]};
        var obstacleRecipe=e.Recipe with{Surface=surface with{Patches=[surface.Patches[0],barrier]},Regions=e.Recipe.Regions with{Profile=fields}};
        var obstacle=generator.Generate(obstacleRecipe,e.Catalog);var attempts=obstacle.RoutePlan!.Routes.Single().Interface!.Attempts;
        Check(attempts.Count>1&&!attempts[0].Accepted&&attempts[^1].Accepted,"blocked midpoint chooses a qualified alternative and keeps failures");
        Check(attempts[0].Center!=attempts[^1].Center,"alternate physical crossing");
        var thin=barrier with{Boundary=Box(19.8,10.2,.4,.01)};
        var widthWorld=generator.Generate(obstacleRecipe with{Surface=surface with{Patches=[surface.Patches[0],thin]}},e.Catalog);
        Check(widthWorld.RoutePlan!.Routes[0].Interface!.Attempts.Count>1,"off-center thin obstacle rejects full-width crossing although centerline is clear");
        // Hole decomposition creates additional boundary features; give this fixture an explicit
        // larger per-leg budget while retaining the planner's 200M aggregate ceiling.
        var hole=generator.Generate(e.Recipe with{Surface=surface with{Exclusions=[new("hole","Preserve source allocation hole","interface:hole",Box(16,9,1,2))]},
            RouteRules=[e.Recipe.RouteRules![0] with{Search=e.Recipe.RouteRules[0].Search with{MaximumWork=10000000}}]},e.Catalog);
        var detour=hole.RoutePlan!.Routes[0].Interface!.Forward.FromLeg.Path!.Points;
        Check(detour.Count>2,"source leg detours around enclosed allocation hole");
        foreach(var p in detour)Check(p.X+.3<=20+1e-8,"detour retains full width inside source allocation");
        Reject(()=>generator.Generate(obstacleRecipe with{RouteRules=[e.Recipe.RouteRules![0] with{Interfaces=[new("local-layouts/explicit/west","west-link",1)]}]},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var mixed=Example(true);var mixedWorld=generator.Generate(mixed.Recipe,mixed.Catalog);
        Check(mixedWorld.Nodes.Count==22&&mixedWorld.RoutePlan!.Routes.Count==2&&mixedWorld.RoutePlan.Routes.All(r=>r.Interface is not null),"mixed grid and irregular allocations have physical connections with all constituents");
        Check(mixedWorld.PlacementPlan!.Placement.Diagnostics.Any(d=>d.Rejections.Keys.Any(k=>k.StartsWith("METADATA_INTERFACE_ENTRANCE_OUTSIDE_DOMAIN",StringComparison.Ordinal))),"unary entrance failures are retained without enumerating unrelated assignments");
        var reversed=e.Recipe with{AdditionalConnections=[e.Recipe.AdditionalConnections![0] with{FromId="local-layouts/sites-1",ToId="local-layouts/sites-0"}],
            RouteRules=[e.Recipe.RouteRules![0] with{FromPortId="west",ToPortId="east"}]};
        var reversedWorld=generator.Generate(reversed,e.Catalog);Near(reversedWorld.RoutePlan!.Routes[0].Forward.Cost!.Descent,.7,"connection orientation independent of declared interface order");
        var reuseWorld=generator.Generate(e.Recipe with{RouteRules=[e.Recipe.RouteRules![0] with{Reuse=new(2)}]},e.Catalog);
        Near(reuseWorld.RoutePlan!.Routes[0].Reverse!.Cost!.ReusedDistance,14,"reverse journey reuses the qualified physical interface route");
        Reject(()=>generator.Generate(e.Recipe with{RouteRules=[e.Recipe.RouteRules![0] with{Profile=e.Recipe.RouteRules[0].Profile with{Radius=.5}}]},e.Catalog),"METADATA_ACCESS_ROUTE_TOO_WIDE");
        var badEntrance=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{
            Anchors=[new("a",new(8,10)),new("b",new(25,10))]}}};
        Reject(()=>generator.Generate(badEntrance,e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-interface-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"output"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"output/world-metadata.json"));Check(Json(saved)==Json(mixedWorld),"saved physical interface replay");
        Console.WriteLine("PASS: exact entrance endpoints and source sizes, analytic width/domain and directional costs, decomposed crossings, blocked midpoint alternatives, bounded failure/work/cancellation, replay/tamper, mixed layouts with seven sites/fourteen constituents and file export.");
    }
    private static void CheckJourney(WorldRouteResult combined,MetadataInterfaceJourney journey,bool reverse)
    {
        var expected=journey.FromLeg.Path!.Points.Concat(journey.Crossing.Path!.Points.Skip(1)).Concat(journey.ToLeg.Path!.Points.Skip(1));
        Check(combined.Path!.Points.SequenceEqual(expected),"exact composed path");
        foreach(var p in journey.FromLeg.Path.Points)Check(reverse?p.X>=20.3:p.X<=19.7,"full source leg width remains on its side");
        foreach(var p in journey.ToLeg.Path.Points)Check(reverse?p.X<=19.7:p.X>=20.3,"full destination leg width remains on its side");
        Check(journey.Crossing.Path!.Points.Count==2,"direct perpendicular crossing");
        Near(combined.Cost!.Total,journey.FromLeg.Cost!.Total+journey.Crossing.Cost!.Total+journey.ToLeg.Cost!.Total,"composed costs");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception("Interface: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
