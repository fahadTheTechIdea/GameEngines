using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldCorridorResilienceTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Corridor resilience: "+message);}
    private static void Refuse(Action action,string code)
    {
        try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected "+code);
    }
    private static WorldTraversalProfile Profile()=>new("walk","Declared walking policy","test:walk",.2,.5,new Dictionary<string,double>{{"meadow",1}});
    private static WorldCorridorDefinition Definition()=>new("paths","walking","test:paths",0,"connect-at-grade",RequireSingleSegmentFailureTolerance:true);
    private static WorldCorridorJourneyInput Journey(string id,params WorldPoint[] points)=>new(id,id,"Required directed journey","test:"+id,new(points),Profile(),true);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool sharedTrunk=false)
    {
        if(sharedTrunk)
        {
            var e=WorldCorridorTests.Example();return(e.Recipe with{Corridors=e.Recipe.Corridors! with{
                Groups=e.Recipe.Corridors.Groups.Select(g=>g with{RequireSingleSegmentFailureTolerance=true}).ToArray()}},e.Catalog);
        }
        var source=WorldSurfaceTests.Example();const string id="physical-alternatives";
        var root=source.Catalog.Archetypes[0] with{Parts=[new("locations","location",4,"Required exploration destinations",
            Layout:MetadataArcLayout.Parameters([40,30],20))]};
        return(source.Recipe with{Id=id,Origin=[0,0],Surface=WorldSurfaceTests.Surface("flat"),
            AdditionalConnections=[new("close-loop","travel-intent",id+"/locations-3",id+"/locations-0")],
            RouteRules=[new("walking","travel-intent",Profile(),new(.5,[]))],
            Corridors=new([new("paths","travel-intent","test:paths",0,"connect-at-grade",true)]),
            RemainingScopes=["Declared physical-segment failure tolerance; broad world design, junction/area failures, artwork and runtime remain separate."]},
            source.Catalog with{Archetypes=[root,source.Catalog.Archetypes[1]]});
    }
    private static void Oracle(WorldCorridorPlan plan)
    {
        // Independent vertex reachability after removing each entire physical
        // segment, including both directions. Production searches directed step
        // states using explicit junction transitions instead.
        var byId=plan.Segments.ToDictionary(s=>s.Id);var receipt=plan.Resilience!;int expected=0;
        foreach(var journey in plan.Journeys)
        foreach(var failure in plan.Segments)
        {
            var reached=new HashSet<WorldPoint>{journey.From};bool changed;
            do
            {
                changed=false;
                foreach(var s in plan.Segments.Where(s=>s.Id!=failure.Id))
                {
                    if(s.ForwardJourneys.Count>0&&reached.Contains(s.From))changed|=reached.Add(s.To);
                    if(s.ReverseJourneys.Count>0&&reached.Contains(s.To))changed|=reached.Add(s.From);
                }
            }while(changed);
            Check(reached.Contains(journey.To),"independent all-segment failure reachability");
            var witnesses=receipt.Alternatives.Where(a=>a.JourneyId==journey.Id&&a.FailedSegmentId==failure.Id).ToArray();
            if(!journey.Steps.Any(s=>s.SegmentId==failure.Id)){Check(witnesses.Length==0,"unaffected original path needs no duplicate receipt");continue;}
            expected++;Check(witnesses.Length==1,"exact failure coverage");
            var cursor=journey.From;WorldCorridorStep? previous=null;
            foreach(var step in witnesses[0].Steps)
            {
                var s=byId[step.SegmentId];Check(s.Id!=failure.Id,"neither failed direction used");
                Check((step.Forward?s.ForwardJourneys:s.ReverseJourneys).Count>0,"no reverse permission invented");
                Check(cursor==(step.Forward?s.From:s.To),"alternate exact endpoint continuity");
                if(previous is not null)Check(plan.Junctions.Single(j=>j.Position==cursor).Transitions.Contains(
                    new(previous.SegmentId,previous.Forward,step.SegmentId,step.Forward)),"alternate obeys junction transition");
                cursor=step.Forward?s.To:s.From;previous=step;
            }
            Check(cursor==journey.To,"alternate preserves destination");
        }
        Check(receipt.Version==1&&receipt.Alternatives.Count==expected&&receipt.Work>0&&receipt.Work<plan.Work,"complete bounded receipt");
    }
    public static async Task RunAsync()
    {
        var surface=WorldSurfaceTests.Surface("flat");var planner=new WorldCorridorPlanner();var definition=Definition();
        WorldCorridorJourneyInput[] parallel=[Journey("direct",new(10,20),new(50,20)),Journey("detour",new(10,20),new(10,40),new(50,40),new(50,20))];
        var directed=planner.Plan(surface,definition,parallel);Oracle(directed);
        var twoWay=parallel.Concat(parallel.Select(j=>j with{Id=j.Id+"-reverse",Path=new(j.Path.Points.Reverse().ToArray())})).ToArray();
        var plan=planner.Plan(surface,definition,twoWay);Oracle(plan);
        Check(Json(plan)==Json(planner.Plan(surface,definition,twoWay.Reverse().ToArray())),"input order deterministic");
        var ordinary=planner.Plan(surface,definition with{RequireSingleSegmentFailureTolerance=false},twoWay);
        Check(ordinary.Resilience is null&&Json(ordinary.Segments)==Json(plan.Segments)&&Json(ordinary.Journeys)==Json(plan.Journeys),"checking does not redesign source paths");
        var reversedDetour=parallel[1] with{Path=new(parallel[1].Path.Points.Reverse().ToArray())};
        Refuse(()=>planner.Plan(surface,definition,[parallel[0],reversedDetour]),"RESILIENCE_UNSATISFIED");
        Refuse(()=>planner.Plan(surface,definition,[parallel[0]]),"RESILIENCE_UNSATISFIED");
        Refuse(()=>planner.Plan(surface,definition with{MaximumWork=plan.Work-1},twoWay),"WORK_BUDGET");
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();try{planner.Plan(surface,definition,twoWay,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);Oracle(world.CorridorPlan!.Groups.Single());
        var blocked=Example(true);Refuse(()=>generator.Generate(blocked.Recipe,blocked.Catalog),"RESILIENCE_UNSATISFIED");
        var hill=e.Recipe with{Surface=WorldSurfaceTests.Surface("hill")};var hillside=generator.Generate(hill,e.Catalog);Oracle(hillside.CorridorPlan!.Groups.Single());
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);string before=Json(session.Snapshot);
        Refuse(()=>session.Apply(new("break-loop","Preserve failure tolerance","test:break",session.Snapshot.World.RecipeSha256,
            session.Snapshot.World.CatalogSha256,e.Recipe with{AdditionalConnections=null},e.Catalog)),"RESILIENCE_UNSATISFIED");
        Check(Json(session.Snapshot)==before,"failed edit preserves accepted world/history");
        var updated=session.Apply(new("hill","Retain independent passages on changed terrain","test:hill",session.Snapshot.World.RecipeSha256,
            session.Snapshot.World.CatalogSha256,hill,e.Catalog));
        Check(Json(updated.World)==Json(hillside),"incremental equals clean generation");
        string root=Path.Combine("generated","physical-resilience-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        foreach(var (name,example) in new[]{("cycle",e),("shared-trunk",blocked)})
        {
            string inputs=Path.Combine(root,name);Directory.CreateDirectory(inputs);
            await File.WriteAllTextAsync(Path.Combine(inputs,"recipe.json"),Json(example.Recipe));
            await File.WriteAllTextAsync(Path.Combine(inputs,"catalog.json"),Json(example.Catalog));
        }
        string folder=Path.Combine(root,"cycle","world");await generator.GenerateFilesAsync(Path.Combine(root,"cycle","recipe.json"),Path.Combine(root,"cycle","catalog.json"),folder);
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world-metadata.json")))==Json(world),"typed source replay");
        bool noPartial=false;
        try{await generator.GenerateFilesAsync(Path.Combine(root,"shared-trunk","recipe.json"),Path.Combine(root,"shared-trunk","catalog.json"),Path.Combine(root,"shared-trunk","world"));}
        catch(ArgumentException error)when(error.Message.Contains("RESILIENCE_UNSATISFIED")){noPartial=!Directory.Exists(Path.Combine(root,"shared-trunk","world"));}
        Check(noPartial,"unsatisfied physical alternative publishes no partial directory");
        var group=world.CorridorPlan!.Groups.Single();var receipt=group.Resilience!;
        var tampered=world with{CorridorPlan=world.CorridorPlan with{Groups=[group with{Resilience=receipt with{Alternatives=receipt.Alternatives.Skip(1).ToArray()}}]}};
        Refuse(()=>WorldMetadataGenerator.ValidateDocument(tampered),"CORRIDOR_PLAN_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(root,"core-directed.json"),Json(directed));
        await File.WriteAllTextAsync(Path.Combine(root,"core-bidirectional.json"),Json(plan));
        Console.WriteLine($"PASS: exact directed and bidirectional physical-segment failure alternatives; independent all-segment reachability; shared logical-loop trunk and wrong-direction refusal; unchanged source paths, hill response, budgets/cancel, edit rollback, typed replay/tamper and atomic publication. Packet: {root}");
    }
}
