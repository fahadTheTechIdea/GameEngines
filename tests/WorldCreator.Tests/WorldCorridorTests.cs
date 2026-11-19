using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldCorridorTests
{
    private static WorldTraversalProfile Profile()=>new("foot","Exploration passage","test:foot",1,.5,new Dictionary<string,double>{{"meadow",1}});
    private static WorldCorridorDefinition Definition()=>new("trails","walking","test:trails",0,"connect-at-grade");
    private static WorldCorridorJourneyInput J(string id,string connection,params WorldPoint[] points)=>new(id,connection,"Reach declared destination","test:"+id,new(points),Profile(),true);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Corridors: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static void Oracle(WorldCorridorPlan plan,IReadOnlyList<WorldCorridorJourneyInput> input)
    {
        var segments=plan.Segments.ToDictionary(s=>s.Id);
        foreach(var journey in plan.Journeys)
        {
            var source=input.Single(j=>j.Id==journey.Id);var cursor=source.Path.Points[0];double length=0;
            Check(journey.From==cursor&&journey.To==source.Path.Points[^1],"original endpoints retained");
            for(int i=0;i<journey.Steps.Count;i++)
            {
                var step=journey.Steps[i];var s=segments[step.SegmentId];var a=step.Forward?s.From:s.To;var b=step.Forward?s.To:s.From;
                Check(a==cursor,"ordered directed journey continuity");cursor=b;length+=a.DistanceTo(b);
                Check((step.Forward?s.ForwardJourneys:s.ReverseJourneys).Contains(journey.Id),"segment membership matches traversal direction");
                if(i>0){var previous=journey.Steps[i-1];Check(plan.Junctions.Single(j=>j.Position==a).Transitions.Contains(new(previous.SegmentId,previous.Forward,step.SegmentId,step.Forward)),"every required turn exists");}
            }
            Check(cursor==source.Path.Points[^1],"journey ends at original destination");
            double expected=source.Path.Points.Zip(source.Path.Points.Skip(1)).Sum(p=>p.First.DistanceTo(p.Second));
            Check(Math.Abs(length-expected)<1e-7,"independent source path length preserved");
        }
        foreach(var junction in plan.Junctions)foreach(var t in junction.Transitions)
        {
            var a=segments[t.FromSegmentId];var b=segments[t.ToSegmentId];
            Check((t.FromForward?a.To:a.From)==junction.Position&&(t.ToForward?b.From:b.To)==junction.Position,"turn endpoints meet geometrically");
            Check((t.FromForward?a.ForwardJourneys:a.ReverseJourneys).Count>0&&(t.ToForward?b.ForwardJourneys:b.ReverseJourneys).Count>0,"no invented reverse permission");
        }
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurfaceTests.Example();var root=e.Catalog.Archetypes[0] with{
            Parts=[new("locations","location",3,"Required exploration destinations",Layout:MetadataLineLayout.Parameters([0,0],[20,0])),
                new("crossing","location",2,"Crossing exploration destinations",Layout:MetadataLineLayout.Parameters([20,-10],[0,30]))],
            Connections=[new("links","travel-intent",MetadataConnectionPattern.Chain,"locations"),new("cross","travel-intent",MetadataConnectionPattern.Chain,"crossing")]};
        return(e.Recipe with{Id="shared-trails",Origin=[10,20],Surface=WorldSurfaceTests.Surface("flat"),
            AdditionalConnections=[new("end-to-end","travel-intent","shared-trails/locations-0","shared-trails/locations-2")],
            RouteRules=[new("walking","travel-intent",Profile(),new(.5,[]))],
            Corridors=new([new("trails","travel-intent","example:trails",0,"connect-at-grade")]),
            RemainingScopes=["Bounded shared surface passages; adaptive regions, reuse-aware routing, art and native runtime remain open"]},
            e.Catalog with{Archetypes=[root,e.Catalog.Archetypes[1]]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("EXAMPLE_EXISTS");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var surface=WorldSurfaceTests.Surface("flat");var planner=new WorldCorridorPlanner();var definition=Definition();
        WorldCorridorJourneyInput[] input=[J("a","a",new(10,20),new(60,20)),J("b","b",new(10,20),new(40,20),new(40,60)),
            J("b-reverse","b",new(40,60),new(40,20),new(10,20)),J("cross","cross",new(30,10),new(30,40))];
        var plan=planner.Plan(surface,definition,input);Oracle(plan,input);
        Check(plan.Segments.Count==6&&plan.Junctions.Count==7,"analytic shared branch and crossing counts");
        Check(Math.Abs(plan.Segments.Sum(s=>s.From.DistanceTo(s.To))-120)<1e-7,"physical length counts shared trunk once");
        Check(plan.SharedDependencies.Count==2&&plan.SharedDependencies.All(d=>d.ConnectionIds.SequenceEqual(new[]{"a","b"})),"shared failure dependencies do not count reverse journey as independent connection");
        Check(plan.Junctions.Single(j=>j.Position==new WorldPoint(30,20)).SegmentIds.Count==4,"crossing is explicitly connected");
        Check(Json(plan)==Json(planner.Plan(surface,definition,input.Reverse().ToArray())),"deterministic input order");
        WorldPoint Rotate(WorldPoint p)=>new(p.X*Math.Cos(.37)-p.Y*Math.Sin(.37)-100,p.X*Math.Sin(.37)+p.Y*Math.Cos(.37)-100);
        WorldPolygon Polygon(WorldPolygon p)=>new(p.Vertices.Select(Rotate).ToArray());
        var rotatedSurface=surface with{Boundary=Polygon(surface.Boundary!),Patches=surface.Patches.Select(p=>p with{Boundary=Polygon(p.Boundary),Origin=Rotate(p.Origin)}).ToArray()};
        var rotatedInput=input.Select(j=>j with{Path=new(j.Path.Points.Select(Rotate).ToArray())}).ToArray();
        var rotatedPlan=planner.Plan(rotatedSurface,definition,rotatedInput);Oracle(rotatedPlan,rotatedInput);
        Check(rotatedPlan.Segments.Count==6&&Math.Abs(rotatedPlan.Segments.Sum(s=>s.From.DistanceTo(s.To))-120)<1e-7,"oblique negative-coordinate domain preserves topology and length");
        var augmented=planner.Plan(surface,definition,[..input,J("remote","remote",new(10,80),new(60,80))]);
        Check(plan.Segments.All(s=>augmented.Segments.Any(a=>a.Id==s.Id&&Json(a)==Json(s))),"unrelated segment IDs and facts remain stable");
        var reverseOnly=new[]{J("reverse","r",new(60,20),new(10,20))};var reversePlan=planner.Plan(surface,definition,reverseOnly);Oracle(reversePlan,reverseOnly);
        Check(reversePlan.Segments.Single().ForwardJourneys.Count==0,"one-way reverse creates no forward permission");
        Reject(()=>planner.Plan(surface,definition,input.Select((j,i)=>i==1?j with{Profile=j.Profile with{Radius=2}}:j).ToArray()),"WORLD_CORRIDOR_PROFILE_INCOMPATIBLE");
        Reject(()=>planner.Plan(surface,definition with{Level=1},input),"WORLD_CORRIDOR_LEVEL_UNSUPPORTED");
        Reject(()=>planner.Plan(surface,definition with{MaximumWork=1},input),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(surface,definition,[J("near","near",new(10,20),new(40,20)),J("near-two","near-two",new(10+1e-10,20),new(40,40))]),"WORLD_CORRIDOR_AMBIGUOUS_SOURCE_POINT");
        Reject(()=>planner.Plan(surface,definition,[J("straight","straight",new(10,20),new(60,20)),J("shallow","shallow",new(10,20-2e-8),new(60,20+2e-8))]),"WORLD_CORRIDOR_AMBIGUOUS_INTERSECTION");
        var obstacle=new WorldPolygon([new(29.9,20.7),new(30.1,20.7),new(30.1,20.8),new(29.9,20.8)]);
        Reject(()=>planner.Plan(surface with{Exclusions=[new("pocket","Protected off-center ground","test:pocket",obstacle)]},definition,input),"WORLD_CORRIDOR_SEGMENT_UNRESOLVED");
        // Each half is individually level; joining them would silently transfer elevation without this junction check.
        var west=new WorldPolygon([new(0,0),new(40,0),new(40,100),new(0,100)]);
        var east=new WorldPolygon([new(40,0),new(120,0),new(120,40),new(40,40)]);
        var stepSurface=surface with{Patches=[surface.Patches[0] with{Boundary=west},surface.Patches[0] with{Id="high",Boundary=east,Elevation=20,Priority=1}]};
        Reject(()=>planner.Plan(stepSurface,definition,[J("west","w",new(10,20),new(40,20)),J("east","e",new(40,20),new(60,20))]),"WORLD_CORRIDOR_JUNCTION_UNRESOLVED");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{planner.Plan(surface,definition,input,cancelled.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}}
        var example=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(example.Recipe,example.Catalog);
        Check(world.CorridorPlan?.Groups.Single().Segments.Count==4&&world.CorridorPlan.Groups.Single().Journeys.Count==8,"metadata bidirectional routes share two passages and connect a crossing");
        Check(world.CorridorPlan!.Groups.Single().SharedDependencies.Count==2,"logical all-pairs redundancy still shares physical dependencies");
        new MetadataCorridorPlanner().ValidateDocument(world);
        var f=WorldPlacementFeedbackTests.Example();var feedbackWorld=generator.Generate(f.Recipe with{
            Corridors=new([new("access","travel","test:access",0,"connect-at-grade")])},f.Catalog);
        Check(feedbackWorld.PlacementPlan!.FeasibilityAttempts!.Count>1&&feedbackWorld.CorridorPlan!.Groups.Single().Journeys.Count==2,"placement feedback preserves corridor obligation through candidate rejection");
        new MetadataPlacementPlanner().ValidateDocument(feedbackWorld);new MetadataCorridorPlanner().ValidateDocument(feedbackWorld);
        var roadless=generator.Generate(example.Recipe with{Corridors=null,RouteRules=null},example.Catalog);
        Check(roadless.CorridorPlan is null&&roadless.RoutePlan is null,"logical world does not acquire physical corridors");
        Reject(()=>generator.Generate(example.Recipe with{RouteRules=null},example.Catalog),"METADATA_CORRIDOR_COVERAGE_REQUIRED");
        var n=WorldNetworkTests.Example();var network=generator.Generate(n.Recipe with{Corridors=new([new("network","regional-travel","test:network",0,"connect-at-grade")])},n.Catalog);
        Check(network.CorridorPlan!.Groups.Single().Journeys.Count==2*network.NetworkPlan!.Networks.Single().Plan.Edges.Count,"selected network edges feed common physical owner");
        var group=world.CorridorPlan.Groups.Single();var forged=world with{CorridorPlan=world.CorridorPlan with{Groups=[group with{Segments=group.Segments.Select((s,i)=>i==0?s with{Width=99}:s).ToArray()}]}};
        Reject(()=>new MetadataCorridorPlanner().ValidateDocument(forged),"METADATA_CORRIDOR_PLAN_MISMATCH");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-corridors-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);string recipe=Path.Combine(root,"recipe.json"),catalog=Path.Combine(root,"catalog.json"),output=Path.Combine(root,"world");
            await generator.GenerateFilesAsync(recipe,catalog,output);
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"))),"exact saved replay");
            string bad=Path.Combine(root,"forged.json");await File.WriteAllTextAsync(bad,Json(forged));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(bad),"METADATA_CORRIDOR_PLAN_MISMATCH");
            await RejectAsync(()=>new WorldMetadataSchematicExporter().CreateAsync(forged),"METADATA_CORRIDOR_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"cli"))==0,"actual CLI generation");
                await File.WriteAllTextAsync(recipe,Json(example.Recipe with{Corridors=new([example.Recipe.Corridors!.Groups[0] with{Level=1}])}));
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"failed"))==1,"unsupported elevation refuses CLI");
                Check(!Directory.Exists(Path.Combine(root,"failed")),"failed geometry cannot publish partial world");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.failed")&&audit.Contains("world.metadata.succeeded"),"durable success/failure audit");
            }
        }
        finally{if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-corridors-",StringComparison.Ordinal))Directory.Delete(root,true);}
        Console.WriteLine("PASS: shared physical segments, directed branch/crossing continuity, level/profile rejection, hazards, stable IDs, dependency groups, deterministic replay, work/cancellation bounds, metadata/network/CLI/read/export. No reuse-aware search, multi-level transfer, vehicle turns or complete-world acceptance.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;
    }
}
