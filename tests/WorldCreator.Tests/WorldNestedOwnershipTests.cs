using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldNestedOwnershipTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private static void Check(bool value,string message){if(!value)throw new Exception("Nested ownership: "+message);}
    private static ArgumentException Reject(Action run,string prefix)
    {try{run();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return error;}throw new Exception("Missing "+prefix);}
    internal static MetadataRegionalOwnershipRequest Example(bool larger=false,bool terrainEdit=false,double rotation=0,bool mixedPrograms=false)
    {
        var boundary=new WorldPolygon([new(0,0),new(300,0),new(300,150),new(250,180),new(0,180)]);
        var surface=new WorldSurfaceDefinition(1,"nested-world-ground","meters",boundary,
            [new("protected-water","Retain the protected terrain domain","nested:water",Box(terrainEdit?160:140,60,10,50))],
            [new("ground","ground","Actual continuous source terrain","nested:terrain",boundary,0,new(0,0),0,.01,.005)]);
        var input=mixedPrograms?WorldRegionDemandTests.CompositionExample():WorldRegionDemandTests.Example(larger);var roles=new[]{"alpha","beta","gamma"};
        var regions=roles.Select(id=>{
            var original=JsonSerializer.Deserialize<WorldMetadataRecipe>(Json(input.Recipe).Replace(input.Recipe.Id,"region-"+id,StringComparison.Ordinal),WorldJson.CreateOptions())!;
            var layout=roles.Select(role=>id=="beta"?new WorldLocalLayout(role+"-local",role,"Complete region-relative program allocation","nested:local",WorldLocalLayoutStrategy.PowerDiagram,
                [new(role+"-site","Room for the complete required source","nested:capacity",new(0,0),1200)],RegionFrame:new("Resolve from the actual terrain-selected region","nested:frame")):
                new WorldLocalLayout(role+"-local",role,"Purposeful complete region allocation","nested:local",WorldLocalLayoutStrategy.AreaBalanced,[],
                    ProgramIntents:[new(role+"-site","Complete required program area","nested:capacity",1200)],AreaPattern:new())).ToArray();
            var recipe=original with{Surface=null,Origin=null,Regions=original.Regions! with{Boundary=null!,AnchorPrecision=.25},
                LocalLayouts=new(layout,[]),ProgramComposition=new("nested-regions","Select actual terrain regions after ownership","nested:regions",MaximumAttempts:32),
                Placement=original.Placement! with{Request=original.Placement.Request with{Boundary=null,
                    Demands=original.Placement.Request.Demands.Select(d=>d with{AllocationId=d.RegionId+"-site"}).ToArray()}},
                RemainingScopes=["Complete nested location programs inside automatically computed ownership. No fixed world grid; broad world design, source-phase composition, locks, streaming, art and Godot remain separate."]};
            return new MetadataRegionalProgramRegion(id,"Required complete "+id+" location programs","nested:"+id,1,1,1,recipe.Regions.Profile,
                "ownership-anchor",recipe,input.Catalog,new("Reserve complete source envelopes","nested:ownership-capacity",.05),
                new("Bind nested terrain regions to computed ownership","nested:context"));
        }).ToArray();
        var walk=input.Recipe.RouteRules![0];
        var connections=roles.Zip(roles.Skip(1),(a,b)=>new MetadataRegionalConnection(a+"-"+b,"world-travel","Required travel between complete location programs","nested:travel",
            new(a,"region-"+a+"/first-0","east"),new(b,"region-"+b+"/first-0","west"),a,boundary,walk.Profile,walk.Search,true)).ToArray();
        return new(1,"nested-program-world","Complete reusable nested programs across actual terrain","nested:world",surface,regions,connections,new(AxisRotationDegrees:rotation,MaximumCells:1024),
            AnchorPrecision:.25,Coupling:new("Resolve ownership and real nested terrain regions","nested:coupling"),
            ProgramComposition:new("Qualify complete nested programs and every journey","nested:composition",32));
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var planner=new MetadataRegionalOwnershipPlanner(generator);var engine=new MetadataRegionalWorldGenerator(generator);
        var worlds=new List<MetadataRegionalWorldDocument>();
        foreach(var(name,source) in new[]{("baseline",Example()),("larger-source",Example(larger:true)),("terrain-edit",Example(terrainEdit:true)),("rotated-ownership",Example(rotation:27)),("mixed-programs",Example(mixedPrograms:true))})
        {
            string before=Json(source);var definition=planner.Plan(source);var world=engine.Generate(definition);worlds.Add(world);
            Check(Json(source)==before,"original reusable source remains unchanged");
            var plan=definition.OwnershipPlan!;var trace=plan.JointPlan!;
            Check(trace.Attempts[0].InputChoices.All(c=>c.RegionSelectionDeferred&&c.RequiredRanks.Count==(name=="mixed-programs"?1:0)),"initialization does not invent unknown region choices");
            Check(trace.Attempts[^1].Accepted&&trace.Attempts[^1].OutputChoices!.All(c=>!c.RegionSelectionDeferred&&c.RequiredRanks.Count==(name=="mixed-programs"?2:1)),"accepted world binds every actual region decision");
            if(name=="mixed-programs")Check(trace.Attempts.Any(a=>a.Rejection is not null)&&trace.Attempts[^1].OutputChoices!.All(c=>c.RequiredRanks[0]==1),"joint world allocation reconsiders infeasible whole programs without shrinking sources");
            Check(plan.ProgramDemands!.All(d=>!d.Analysis.RegionSelectionDeferred&&d.Analysis.RegionAnalysis is not null),"accepted physical analyses cannot remain deferred");
            Check(world.Regions.Sum(r=>r.World.Nodes.Count)==33&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==27,"all nine complete compounds and eighteen nested source modules retained");
            foreach(var packet in world.Regions)
            {
                var actual=packet.World;var region=definition.Regions.Single(r=>r.Id==packet.RegionId);
                Check(Json(actual.RegionPlan!.Request.Boundary)==Json(region.Ownership),"nested intent boundary is exact computed ownership");
                Check(actual.RegionPlan.Regions.Count==3&&actual.LocalLayoutPlan!.Allocations.Count==3,"three semantic terrain roles and complete local allocations per ownership");
                var analysis=plan.ProgramDemands!.Single(d=>d.RegionId==packet.RegionId).Analysis;
                Check(WorldMetadataGenerator.MatchesRegionAnalysis(analysis.RegionAnalysis,actual),"selected region/source-demand analysis matches actual physical world");
                Check(actual.RoutePlan!.Routes.Count==3&&actual.NetworkPlan!.Networks.Single().Plan.Edges.Count>=2,"every original local journey and complete-program network retained");
                Check(actual.AccessPlan!.Sites.Where(s=>s.NodeId.EndsWith("/second-0",StringComparison.Ordinal)).All(s=>Math.Abs(s.Footprint.SignedArea()-(name=="larger-source"?180:120))<1e-6),"exact full original or separately enlarged source footprint retained");
            }
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(source));
            await engine.PublishAsync(world,Path.Combine(output,name));
            var restored=await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDocument>(Path.Combine(output,name,"world-regions.json"));
            Check(Json(restored)==Json(world),"native full unbound context/source/trace replay");
        }
        Check(Json(worlds[0].Definition.Regions.Select(r=>r.Ownership))!=Json(worlds[3].Definition.Regions.Select(r=>r.Ownership)),"ownership orientation changes complete nested worlds");
        await BoundariesAsync(output,planner,generator,worlds[0]);
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",Worlds=worlds.Count,Nodes=worlds.Sum(w=>w.Regions.Sum(r=>r.World.Nodes.Count)),
            Bodies=worlds.Sum(w=>w.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)),NestedRegions=worlds.Sum(w=>w.Regions.Sum(r=>r.World.RegionPlan!.Regions.Count)),
            Allocations=worlds.Sum(w=>w.Regions.Sum(r=>r.World.LocalLayoutPlan!.Allocations.Count))}));
        Console.WriteLine("PASS: complete nested terrain/local programs inside automatic ownership, explicit deferred initialization, exact source/analysis bindings and original journeys under source/terrain/orientation edits.");
    }
}
