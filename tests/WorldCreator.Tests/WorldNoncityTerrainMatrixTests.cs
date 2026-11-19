using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

// Fixed counterfactuals over the same game-authored exploration program. Every
// declared terrain case is recorded, including any refusal or invalid output.
internal static class WorldNoncityTerrainMatrixTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions())));
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static void Preflight()
    {
        var source=WorldRegionalJointTests.TerrainExample();var original=Json(source);var ground=source.Surface.Patches.Single();
        var absent=source with{Surface=source.Surface with{Patches=[ground with{Kind="water"}]}};
        var planner=new MetadataRegionalOwnershipPlanner();
        try{planner.Plan(absent);throw new InvalidOperationException("Absent terrain was accepted");}
        catch(WorldRegionIntentUnsatisfiedException e)
        {
            if(e.Data["RegionalTerrainKindFailure"] is not MetadataRegionalTerrainKindFailure proof||proof.RegionId!="alpha"||
                !proof.AllowedKinds.SequenceEqual(["ground"])||!proof.AvailableKinds.SequenceEqual(["water"]))
                throw new InvalidOperationException("Missing exact terrain-kind witness");
        }
        var generatedTerrain=new WorldTerrainRecipe(1,"water-field","water","Generated water terrain","test:water","meters",
            new(0,0),5,5,17,9,0,0,0,[],MaximumWork:1000);
        var generated=source with{Surface=source.Surface with{Patches=[],Terrain=generatedTerrain}};
        try{planner.Plan(generated);throw new InvalidOperationException("Absent generated terrain was accepted");}
        catch(WorldRegionIntentUnsatisfiedException e)
        {
            if(e.Data["RegionalTerrainKindFailure"] is not MetadataRegionalTerrainKindFailure proof||proof.RegionId!="alpha"||
                !proof.AllowedKinds.SequenceEqual(["ground"])||!proof.AvailableKinds.SequenceEqual(["water"]))
                throw new InvalidOperationException("Missing generated terrain-kind witness");
        }
        try{planner.Plan(generated with{MaximumWork=100});throw new InvalidOperationException("Generated proof bypassed regional budget");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET",StringComparison.Ordinal)){}
        var mixed=generated with{Surface=generated.Surface with{Patches=[ground]},MaximumWork=1};
        try{planner.Plan(mixed);throw new InvalidOperationException("Expected bounded mixed-source planning");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET",StringComparison.Ordinal)&&
            !e.Data.Contains("RegionalTerrainKindFailure")){}
        try{planner.Plan(generated with{Surface=generated.Surface with{Terrain=generatedTerrain with{Columns=1}}});throw new InvalidOperationException("Invalid generated terrain ignored");}
        catch(ArgumentException e)when(e.Message.StartsWith("WORLD_TERRAIN_RECIPE_INVALID",StringComparison.Ordinal)){}
        try{planner.Plan(generated with{Surface=generated.Surface with{Terrain=generatedTerrain with{MaximumWork=1}}});throw new InvalidOperationException("Terrain budget ignored");}
        catch(ArgumentException e)when(e.Message.StartsWith("WORLD_TERRAIN_WORK_BUDGET",StringComparison.Ordinal)){}
        try{planner.Plan(absent with{MaximumWork=1});throw new InvalidOperationException("Budget ignored");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET",StringComparison.Ordinal)){}
        try{planner.Plan(source with{MaximumWork=1});throw new InvalidOperationException("Expected bounded valid-source planning");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET",StringComparison.Ordinal)&&
            !e.Data.Contains("RegionalTerrainKindFailure")){}
        var invalid=absent with{Regions=absent.Regions.Select(r=>r.Id!="beta"?r:r with{Profile=r.Profile with{
            Requirements=[new("bad","unknown-field","Invalid field","test:invalid",0,1)]}}).ToArray()};
        try{planner.Plan(invalid);throw new InvalidOperationException("Invalid profile ignored");}
        catch(ArgumentException e)when(e.Message.StartsWith("WORLD_SUITABILITY_REQUIREMENT_INVALID",StringComparison.Ordinal)){}
        try{planner.Plan(absent with{ProgramComposition=absent.ProgramComposition! with{MaximumAttempts=0}});throw new InvalidOperationException("Invalid composition ignored");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_COMPOSITION_RULE_INVALID",StringComparison.Ordinal)){}
        try{planner.Plan(absent with{Pattern=absent.Pattern with{MaximumDepth=0}});throw new InvalidOperationException("Invalid area pattern ignored");}
        catch(ArgumentException e)when(e.Message.StartsWith("WORLD_AREA_PATTERN_INVALID",StringComparison.Ordinal)){}
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(absent,c.Token);throw new InvalidOperationException("Cancellation ignored");}catch(OperationCanceledException){}}
        if(Json(source)!=original)throw new InvalidOperationException("Preflight mutated input");
        Console.WriteLine("PASS: absent terrain has an attributed hard refusal; invalid profile, work budget and cancellation remain distinct.");
    }
    internal static async Task GeneratedWorldAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/joint-terrain-world/generated-field-request.json");
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var definition=planner.Plan(source);var world=engine.Generate(definition);
        if(world.Regions.Count!=2||world.Regions.Sum(r=>r.World.Nodes.Count)!=22||world.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0)!=20||
            world.Connections.Count!=1||world.Connections[0].Forward.Path is null||world.Connections[0].Reverse?.Path is null||
            world.Definition.Surface.Terrain?.Detail?.Seed!=17)
            throw new InvalidOperationException("GENERATED_TERRAIN_JOINT_SCOPE_MISMATCH");
        await engine.PublishAsync(world,output);
        if(Json(await engine.ReadWorldAsync(Path.Combine(output,"world-regions.json")))!=Json(world))
            throw new InvalidOperationException("GENERATED_TERRAIN_JOINT_REPLAY_MISMATCH");
        Console.WriteLine("PASS: seeded generated terrain retains every discovery, source-sized member and bidirectional route through replay.");
    }
    internal static async Task SuppliedHeightfieldAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var recipe=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/joint-terrain-world/generated-field-request.json");
        var terrain=recipe.Surface.Terrain!;var field=new WorldTerrainGenerator().Generate(terrain);
        var input=recipe with{Surface=recipe.Surface with{Terrain=null,HeightField=field}};string source=Json(input);
        var planner=new MetadataRegionalOwnershipPlanner();var generator=new MetadataRegionalWorldGenerator();
        void Reject(MetadataRegionalOwnershipRequest request,string code)
        {try{planner.Plan(request);throw new InvalidOperationException("Expected "+code);}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){}}
        Reject(input with{Surface=input.Surface with{HeightField=field with{Heights=field.Heights.Take(field.Heights.Count-1).ToArray()}}},"WORLD_HEIGHTFIELD_INVALID");
        var corrupted=field.Heights.ToArray();corrupted[^1]+=1;
        Reject(input with{Surface=input.Surface with{Terrain=terrain,HeightField=field with{Heights=corrupted}}},"WORLD_TERRAIN_REPLAY_MISMATCH");
        try{planner.Plan(input with{Surface=input.Surface with{HeightField=field with{Kind="water"}}});throw new InvalidOperationException("Missing terrain accepted");}
        catch(WorldRegionIntentUnsatisfiedException e)
        {if(e.Data["RegionalTerrainKindFailure"] is not MetadataRegionalTerrainKindFailure proof||proof.RegionId!="alpha"||!proof.AvailableKinds.SequenceEqual(["water"]))throw;}
        Reject(input with{MaximumWork=1},"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(input,cancellation.Token);throw new InvalidOperationException("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),source);
        var definition=planner.Plan(input);
        try{generator.Generate(definition with{MaximumWork=1});throw new InvalidOperationException("Regional field validation bypassed its budget");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_WORK_BUDGET",StringComparison.Ordinal)){}
        var world=generator.Generate(definition);
        if(world.Regions.Count!=2||world.Regions.Sum(r=>r.World.Nodes.Count)!=22||world.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0)!=20||
            world.Connections.Count!=1||world.Connections[0].Forward.Path is null||world.Connections[0].Reverse?.Path is null||
            world.Regions.Any(r=>r.World.Surface?.HeightField is null||!r.World.Surface.HeightField.Heights.SequenceEqual(field.Heights)))
            throw new InvalidOperationException("SUPPLIED_HEIGHTFIELD_WORLD_SCOPE_MISMATCH");
        if(Json(input)!=source)throw new InvalidOperationException("SUPPLIED_HEIGHTFIELD_INPUT_MUTATED");
        await generator.PublishAsync(world,Path.Combine(output,"world"));
        if(Json(await generator.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))!=Json(world))throw new InvalidOperationException("SUPPLIED_HEIGHTFIELD_REPLAY_MISMATCH");
        Console.WriteLine("PASS: supplied terrain field retains both complete regions, physical content, paths and source replay; malformed, mismatched and absent kinds refuse.");
    }
    internal static async Task CapturedDesignReplayAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var authored=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/joint-terrain-world/captured-terrain-request.json");
        var captured=authored with{Surface=new WorldSurfaceQuery(authored.Surface).Definition};
        var request=new MetadataRegionalDesignRequest(1,captured.Id,captured.Purpose,captured.SourceRuleId,captured,[],[],SeedPlan:new("capture-replay",TerrainDetail:true));
        var designer=new MetadataRegionalDesignGenerator();var result=designer.Generate(request);
        if(result.Receipt.SourceCaptureValidationWork is not >0||result.World.Regions.Count!=2||
            result.World.Regions.Sum(r=>r.World.Nodes.Count)!=22||result.World.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0)!=20||
            result.World.Connections.Count!=1||result.World.Connections[0].Reverse?.Path is null)
            throw new InvalidOperationException("CAPTURED_DESIGN_SCOPE_MISMATCH");
        await designer.PublishAsync(result,output);
        if(Json(await designer.ReadAsync(output))!=Json(result))throw new InvalidOperationException("CAPTURED_DESIGN_REPLAY_MISMATCH");
        try{designer.Verify(result.Receipt with{SourceCaptureValidationWork=result.Receipt.SourceCaptureValidationWork+1},result.World);
            throw new InvalidOperationException("CAPTURED_DESIGN_TAMPER_ACCEPTED");}
        catch(ArgumentException error)when(error.Message.StartsWith("METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH",StringComparison.Ordinal)){}
        await File.WriteAllTextAsync(Path.Combine(output,"design-input.json"),Json(request));
        Console.WriteLine("PASS: saved captured terrain validates before seeding and survives complete design publication/replay; modified accounting is rejected.");
    }
    internal static async Task FineCapturedTerrainAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var request=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/joint-terrain-world/captured-fine-request.json");
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(new MetadataRegionalOwnershipPlanner().Plan(request));
        if(world.Regions.Count!=2||world.Regions.Sum(r=>r.World.Nodes.Count)!=22||world.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0)!=20||
            world.Connections.Count!=1||world.Connections[0].Forward.Path is null||world.Connections[0].Reverse?.Path is null||
            world.Regions.Any(r=>r.World.Surface?.TerrainCapturePlan?.Window is null||r.World.Surface.TerrainCapturePlan.Window.Source.StepX!=2.5))
            throw new InvalidOperationException("FINE_CAPTURED_TERRAIN_WORLD_SCOPE_MISMATCH");
        await engine.PublishAsync(world,output);
        if(Json(await engine.ReadWorldAsync(Path.Combine(output,"world-regions.json")))!=Json(world))throw new InvalidOperationException("FINE_CAPTURED_TERRAIN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: fine captured terrain retains all source-sized content and bidirectional connections through replay.");
    }
    internal static async Task CapturedTerrainAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/joint-terrain-world/generated-field-request.json");
        var terrain=original.Surface.Terrain! with{Origin=new(-5,-5),StepX=5,StepY=5,Columns=513,Rows=513,MaximumWork=20000};
        var capture=new WorldTerrainCaptureDefinition(1,"regional-source-window","Preserve source terrain for this world","test:capture");
        var authored=original with{Surface=original.Surface with{Terrain=terrain,TerrainCapture=capture,TerrainCapturePlan=null}};
        var captured=new WorldSurfaceQuery(authored.Surface).Definition;
        var window=captured.TerrainCapturePlan!.Window!;
        if(window.Heights.Count>=513*513||window.Source.Origin!=terrain.Origin||window.Work>=terrain.MaximumWork)
            throw new InvalidOperationException("CAPTURED_TERRAIN_NOT_BOUNDED");
        var reference=new WorldTerrainGenerator().Generate(terrain with{Columns=19,Rows=11});int checkedSamples=0;
        for(int row=0;row<window.Rows;row++)for(int column=0;column<window.Columns;column++)
        {
            int x=window.FirstColumn+column,y=window.FirstRow+row;
            if(x>=reference.Columns||y>=reference.Rows)continue;
            if(BitConverter.DoubleToInt64Bits(window.Heights[row*window.Columns+column])!=
                BitConverter.DoubleToInt64Bits(reference.Heights[y*reference.Columns+x]))throw new InvalidOperationException("CAPTURED_TERRAIN_SAMPLE_MISMATCH");
            checkedSamples++;
        }
        if(checkedSamples<100)throw new InvalidOperationException("CAPTURED_TERRAIN_SAMPLE_COVERAGE");
        try{new WorldTerrainGenerator().Generate(terrain);throw new InvalidOperationException("Full field unexpectedly fit");}
        catch(ArgumentException e)when(e.Message.StartsWith("WORLD_TERRAIN_WORK_BUDGET",StringComparison.Ordinal)){}
        var source=authored with{Surface=captured};var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var design=new MetadataRegionalDesignRequest(1,source.Id,source.Purpose,source.SourceRuleId,source,[],[],SeedPlan:new("captured-seed-test",TerrainDetail:true));
        var badDesign=design with{Template=source with{Surface=source.Surface with{TerrainCapturePlan=source.Surface.TerrainCapturePlan! with{SourceRecipeSha256=new string('0',64)}}}};
        try{new MetadataRegionalDesignGenerator().Generate(badDesign);throw new InvalidOperationException("Stale capture accepted before seeding");}
        catch(ArgumentException e)when(e.Message.StartsWith("WORLD_TERRAIN_CAPTURE_REPLAY_MISMATCH",StringComparison.Ordinal)){}
        var seeded=new MetadataRegionalDesignGenerator().Generate(design);
        if(seeded.Receipt.SourceCaptureValidationWork is not >0||seeded.World.Definition.Surface.Terrain!.Detail!.Seed==terrain.Detail!.Seed||
            seeded.World.Regions.Any(r=>r.World.Surface?.TerrainCapturePlan?.Window is null))throw new InvalidOperationException("CAPTURED_TERRAIN_SEED_MISMATCH");
        void Reject(MetadataRegionalOwnershipRequest request,string code)
        {try{planner.Plan(request);throw new InvalidOperationException("Expected "+code);}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){}}
        Reject(source with{Surface=source.Surface with{TerrainCapturePlan=source.Surface.TerrainCapturePlan! with{SourceRecipeSha256=new string('0',64)}}},"WORLD_TERRAIN_CAPTURE_REPLAY_MISMATCH");
        Reject(source with{Surface=source.Surface with{TerrainCapture=capture with{Domain=Box(0,0,1,1)},TerrainCapturePlan=null}},"WORLD_TERRAIN_CAPTURE_DOMAIN_INCOMPLETE");
        Reject(source with{Surface=source.Surface with{TerrainCapture=null}},"WORLD_TERRAIN_CAPTURE_DEFINITION_REQUIRED");
        Reject(source with{MaximumWork=1},"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        var water=authored with{Surface=authored.Surface with{Terrain=terrain with{Kind="water"}}};
        try{planner.Plan(water);throw new InvalidOperationException("Missing captured terrain accepted");}
        catch(WorldRegionIntentUnsatisfiedException e)
        {if(e.Data["RegionalTerrainKindFailure"] is not MetadataRegionalTerrainKindFailure p||p.RegionId!="alpha"||!p.AvailableKinds.SequenceEqual(["water"]))throw;}
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(source,cancellation.Token);throw new InvalidOperationException("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"authored-request.json"),Json(authored));
        await File.WriteAllTextAsync(Path.Combine(output,"captured-request.json"),Json(source));
        var definition=planner.Plan(source);
        try{engine.Generate(definition with{MaximumWork=1});throw new InvalidOperationException("Capture validation bypassed world budget");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_WORK_BUDGET",StringComparison.Ordinal)){}
        var world=engine.Generate(definition);
        if(world.Regions.Count!=2||world.Regions.Sum(r=>r.World.Nodes.Count)!=22||world.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0)!=20||
            world.Connections.Count!=1||world.Connections[0].Forward.Path is null||world.Connections[0].Reverse?.Path is null||
            world.Regions.Any(r=>r.World.Surface?.HeightField is not null||r.World.Surface?.TerrainCapturePlan?.Window is null))
            throw new InvalidOperationException("CAPTURED_TERRAIN_WORLD_SCOPE_MISMATCH");
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        if(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))!=Json(world))throw new InvalidOperationException("CAPTURED_TERRAIN_REPLAY_MISMATCH");
        Console.WriteLine($"PASS: captured original-lattice terrain ({window.Heights.Count} of {513*513} samples) retains both complete regions, source-sized content and routes through replay.");
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=WorldRegionalJointTests.TerrainExample();string original=Json(source);
        var ground=source.Surface.Patches.Single();
        var irregular=new WorldPolygon([new(0,0),new(80,0),new(80,40),new(15,40),new(0,30)]);
        var water=new WorldTerrainPatch("water","water","Forbidden terrain in the game-authored travel profile","study:water",
            Box(37,0,6,27),10,new(0,0),0,0,0);
        var cases=new (string Id,MetadataRegionalOwnershipRequest Request)[]{
            ("flat",source),
            ("slope",source with{Surface=source.Surface with{Patches=[ground with{GradientX=.04,GradientY=.03}]}}),
            ("protected",WorldRegionalJointTests.TerrainExample(exclusion:true)),
            ("water",source with{Surface=source.Surface with{Patches=[ground,water]}}),
            ("irregular",source with{Surface=source.Surface with{Boundary=irregular,Patches=[ground with{Boundary=irregular}]},
                Connections=source.Connections.Select(c=>c with{RoutingDomain=irregular}).ToArray()}),
            ("no-ground",source with{Surface=source.Surface with{Patches=[ground with{Kind="water"}]}})
        };
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var observations=new List<object>();var fingerprints=new HashSet<string>();var routeShapes=new HashSet<string>();
        Directory.CreateDirectory(output);MetadataRegionalWorldDocument? first=null;int generated=0,refused=0,invalid=0;
        foreach(var item in cases)
        {
            string file=Path.Combine(output,item.Id+"-request.json");await File.WriteAllTextAsync(file,Json(item.Request));var clock=Stopwatch.StartNew();
            try
            {
                var definition=planner.Plan(item.Request);var world=engine.Generate(definition);clock.Stop();
                bool complete=world.Regions.Count==2&&world.Connections.Count==1&&world.Connections.Single().Forward.Path is not null&&world.Connections.Single().Reverse?.Path is not null&&
                    world.Regions.All(r=>r.World.TerrainDemandPlan?.Results.Count==1&&r.World.TerrainDemandPlan.Results[0].Count is >0 and var n&&
                        r.World.Nodes.Count==1+2*n&&r.World.AccessPlan?.Sites.Count==2*n&&r.World.RoutePlan?.Routes.Count==n-1&&r.World.RoutePlan.Routes.All(p=>p.Reverse is not null));
                if(!complete||item.Id=="no-ground")
                {invalid++;observations.Add(new{item.Id,Status="InvalidOutput",Milliseconds=clock.ElapsedMilliseconds});Console.WriteLine(item.Id+": invalid output");continue;}
                string areas=Hash(world.Definition.Regions.Select(r=>new{r.Id,r.Ownership}).ToArray());
                string routes=Hash(world.Connections.Select(c=>new{Forward=c.Forward.Path,Reverse=c.Reverse!.Path}).ToArray());
                fingerprints.Add(areas);routeShapes.Add(routes);generated++;first??=world;
                await File.WriteAllTextAsync(Path.Combine(output,item.Id+"-world.json"),Json(world));
                observations.Add(new{item.Id,Status="Generated",Milliseconds=clock.ElapsedMilliseconds,
                    Counts=world.Regions.Select(r=>r.World.TerrainDemandPlan!.Results[0].Count).ToArray(),
                    Nodes=world.Regions.Sum(r=>r.World.Nodes.Count),Sites=world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count),
                    OwnershipSha256=areas,RoutesSha256=routes,PlanningWork=definition.OwnershipPlan!.Work,world.Work});
                Console.WriteLine(item.Id+": complete physical world generated");
            }
            catch(ArgumentException error)
            {clock.Stop();refused++;observations.Add(new{item.Id,Status="Refused",Milliseconds=clock.ElapsedMilliseconds,Reason=error.Message});Console.WriteLine(item.Id+": "+error.Message);}
        }
        if(original!=Json(source))throw new InvalidOperationException("NONCITY_MATRIX_MUTATED_SOURCE");
        bool replayed=false;if(first is not null){await engine.PublishAsync(first,Path.Combine(output,"replay"));
            replayed=Json(await engine.ReadWorldAsync(Path.Combine(output,"replay/world-regions.json")))==Json(first);}
        await File.WriteAllTextAsync(Path.Combine(output,"study.json"),JsonSerializer.Serialize(new{Version=1,Runtime=RuntimeInformation.FrameworkDescription,
            OS=RuntimeInformation.OSDescription,Processors=Environment.ProcessorCount,SourceSha256=Hash(source),Generated=generated,Refused=refused,Invalid=invalid,
            DistinctOwnership=fingerprints.Count,DistinctRoutes=routeShapes.Count,OnePublishedWorldReplayed=replayed,Observations=observations},WorldJson.CreateOptions(true)));
        if(invalid!=0||!replayed)throw new InvalidOperationException("NONCITY_MATRIX_OUTPUT_INVALID");
        Console.WriteLine($"PASS: recorded {generated} generated, {refused} refused, {invalid} invalid; {fingerprints.Count} region layouts and {routeShapes.Count} route shapes.");
    }
}
