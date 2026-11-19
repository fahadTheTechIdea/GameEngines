using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPlacementTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldEnvironmentDefinition Fields()=>new(1,"exploration-values",
        [new("height","Ground height","example:height","meters",0,[new(WorldFieldInput.Elevation,1)]),
        new("slope","Ground steepness","example:slope","ratio",0,[new(WorldFieldInput.Slope,1)])]);
    private static WorldSuitabilityProfile Profile(double height)=>new("camp-ground","Gentle dry location at desired altitude","example:camp",["ground"],
        [new("dry","height","Above water level","example:dry",3,50),new("gentle","slope","Usable footprint slope","example:gentle",0,1.5)],
        [new("altitude","height","Preferred gameplay altitude","example:altitude",height,20,1)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurfaceTests.Example();
        var boundary=new WorldPolygon([new(0,0),new(80,5),new(75,75),new(30,80),new(0,65)]);
        var terrain=new WorldTerrainRecipe(1,"exploration-land","ground","Generate exploration terrain","example:terrain","meters",new(0,0),10,10,9,9,4,.08,.03,
            [new("ridge","High ground","example:ridge",WorldLandformMode.Add,[new(55,25),new(65,65)],25,12)]);
        var shape=new WorldPolygon([new(-3,-2),new(4,-2),new(2,3),new(-3,3)]);
        var placement=new WorldPlacementRequest("exploration-sites","Place camps for exploration","example:placement",boundary,
            [new("camp-a","Low-altitude rest camp","example:camp-a",shape,Profile(8)),new("camp-b","High-altitude lookout camp","example:camp-b",shape,Profile(17))],[],
            [new("journey","camp-a","camp-b","Separate destinations for travel","example:journey",20,70,35,1)],MinimumSeparation:10,MaximumWork:50000000);
        var root=e.Catalog.Archetypes[0] with{Parts=[new("camps","camp",2,"Required exploration camps")],Connections=[],ChildRequirements=[new("camp",2,2)]};
        var camp=new WorldMetadataArchetype("camp","camp","Camp with constituent stations",new Dictionary<string,JsonElement>(),
            [new("stations","station",2,"Camp constituents",Layout:MetadataLineLayout.Parameters([-1,0],[2,0]))],[],[new("station",2,2)],LayerId:"intent");
        var station=new WorldMetadataArchetype("station","station","Required camp station",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent");
        var network=WorldNetworkTests.Example().Recipe.NetworkRules![0];
        return(e.Recipe with{Id="terrain-placement",Origin=[35,35],Surface=new(1,"exploration","meters",boundary,[],[],Terrain:terrain),Environment=Fields(),
            Placement=new(placement,[new("camp-a","terrain-placement/camps-0"),new("camp-b","terrain-placement/camps-1")]),
            NetworkRules=[network with{RegionNodeId="terrain-placement",DestinationKind="camp",MinimumDestinations=2,MaximumDestinations=2,
                Options=network.Options with{Connectivity=WorldNetworkConnectivity.Connected,Profile=network.Options.Profile with{
                    KindCosts=new Dictionary<string,double>{{"ground",1}},MaximumSlope=1.5}}}],
            RemainingScopes=["Terrain-driven placement, reserved footprints and parent-relative constituent points; physical constituent geometry, route redesign, junctions, art and runtime remain open."]},
            e.Catalog with{Archetypes=[root,camp,station]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var rectangle=Box(0,0,20,10);var field=new WorldHeightField(1,"flat","ground","Backtracking fixture","test","meters",new(0,0),10,10,3,2,[0,0,0,0,0,0]);
        var flat=new WorldSurfaceDefinition(1,"flat","meters",rectangle,[],[],HeightField:field);
        var xfield=new WorldEnvironmentDefinition(1,"x-value",[new("x","Distance along authored frame","test","meters",0,[new(WorldFieldInput.X,1)])]);
        var profile=new WorldSuitabilityProfile("central","Prefer central sites","test",["ground"],[],[new("central-x","x","Prefer center","test",10,10,1)]);
        var demand=new WorldSiteDemand("a","Required site","test",Box(-4,-4,8,8),profile);
        var request=new WorldPlacementRequest("pair","Place both sites","test",rectangle,[demand,demand with{Id="b"}],[],[]);
        var planner=new WorldSitePlacementPlanner();var query=new WorldEnvironmentQuery(new(flat),xfield);
        var result=planner.Plan(query,request);
        Check(result.Status==WorldPlacementStatus.Found&&result.Sites.Count==2,"required pair fits");
        Check(result.SearchStates>3,"backtracking recovers after best central site blocks second demand");
        Check(result.Conflicts["footprint-separation"]>0,"rejected combinations attributed");
        Check(result.Sites.All(s=>s.Position.X>=4&&s.Position.X<=16&&s.Position.Y>=4&&s.Position.Y<=6)&&
            result.Sites.Sum(s=>s.Point.Score!.Value)>=1,"both eight-meter sources remain contained and score at least as well as the former (5,15) representatives");
        Check(Json(result)==Json(planner.Plan(query,request with{Demands=request.Demands.Reverse().ToArray()})),"declaration order canonical and deterministic");
        Check(planner.Plan(query,request with{MinimumSeparation=5}).Status==WorldPlacementStatus.NoFeasibleAssignment,"infeasible required separation never relaxed");
        var anchored=request with{Demands=[demand with{Profile=profile with{Preferences=[]}}],Anchors=[new("entry",new(5,5))],
            Relations=[new("entry-access","a","entry","Place at the required entry","test",0,0,0,1)]};
        Check(planner.Plan(query,anchored).Sites.Single().Position==new WorldPoint(5,5),"named anchor relation supplies purpose and exact distance constraint");
        var dense=flat with{HeightField=field with{StepX=.25,StepY=.25,Columns=513,Rows=513,Heights=new double[513*513]}};
        var pinned=anchored with{MaximumCandidates=1};
        var pinnedResult=planner.Plan(new(new(dense),xfield),pinned);
        Check(pinnedResult.Status==WorldPlacementStatus.Found&&pinnedResult.Diagnostics.Single().Candidates==1&&pinnedResult.Sites.Single().Position==new WorldPoint(5,5),
            "dense terrain retains the sole hard-anchor candidate within the original candidate budget");
        Check(planner.Plan(new(new(dense with{Exclusions=[new("pin-hazard","Blocked required pin","test",Box(4.9,4.9,.2,.2))]}),xfield),pinned).Status==WorldPlacementStatus.NoFeasibleAssignment,
            "candidate pruning still qualifies the complete footprint against actual hazards");
        Reject(()=>planner.Plan(new(new(dense),xfield),pinned with{Demands=[demand,demand with{Id="b"}]}),"WORLD_PLACEMENT_CANDIDATE_BUDGET");
        var slit=flat with{Exclusions=[new("slit","Thin forbidden strip","test",Box(4.9,0,.2,10))]};
        var obstructed=planner.Plan(new(new(slit),xfield),request);
        Check(obstructed.Status==WorldPlacementStatus.NoFeasibleAssignment&&obstructed.Diagnostics.Any(d=>d.Rejections.ContainsKey("surface:Excluded")),"thin interior footprint hazard prevents required pair");
        Reject(()=>planner.Plan(query,request with{MaximumSearchStates=1}),"WORLD_PLACEMENT_SEARCH_BUDGET");
        Reject(()=>planner.Plan(query,request with{MaximumCandidates=1}),"WORLD_PLACEMENT_CANDIDATE_BUDGET");
        Reject(()=>planner.Plan(query,request with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(query,request with{Demands=[demand with{Profile=profile with{Preferences=[]}}]}),"WORLD_PLACEMENT_PURPOSE_SCORE_REQUIRED");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(query,request,c.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==7&&world.Nodes.All(n=>n.Position is {Count:2}),"all required roots and constituent positions resolved");
        Check(world.PlacementPlan!.Placement.Sites.Count==2&&world.PlacementPlan.RelativePositions.Count==4,"two roots and four deferred constituents recorded");
        Check(world.NetworkPlan!.Networks.Single().Plan.Edges.Count==1,"network uses generated destinations");
        foreach(var b in world.PlacementPlan.Definition.Bindings)
        {
            var node=world.Nodes.Single(n=>n.Id==b.NodeId);var site=world.PlacementPlan.Placement.Sites.Single(s=>s.DemandId==b.DemandId);
            Check(site.Footprint.Eligible&&node.Position!.SequenceEqual(new[]{site.Position.X,site.Position.Y}),"positions match qualified placements");
            var children=world.Nodes.Where(n=>n.ParentId==node.Id).ToArray();
            Check(children.Length==2&&children[0].Position![0]==node.Position![0]-1&&children[1].Position![0]==node.Position![0]+1,"catalog relative composition preserved");
        }
        var site0=world.PlacementPlan.Placement.Sites[0];
        var movedSurface=e.Recipe.Surface! with{Exclusions=[new("closed-site","Terrain edit closes preferred camp","test",Box(site0.Position.X-5,site0.Position.Y-5,10,10))]};
        var moved=generator.Generate(e.Recipe with{Surface=movedSurface,NetworkRules=null},e.Catalog);
        Check(moved.PlacementPlan!.Placement.Sites[0].Position!=site0.Position&&moved.Nodes.Count==world.Nodes.Count,"terrain edit changes placement without dropping content");
        var tampered=world with{PlacementPlan=world.PlacementPlan with{Placement=world.PlacementPlan.Placement with{Work=0}}};
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
        var wrongPosition=world with{Nodes=world.Nodes.Select(n=>n.Id.EndsWith("stations-0",StringComparison.Ordinal)?n with{Position=[1,1]}:n).ToArray()};
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(wrongPosition),"METADATA_PLACEMENT_PLAN_MISMATCH");
        var positioned=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Layout=MetadataLineLayout.Parameters([0,0],[1,0])}]}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,positioned),"METADATA_PLACEMENT_BINDING_INVALID");
        var strict=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{MinimumSeparation=500}}};
        Reject(()=>generator.Generate(strict,e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-placement-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);string recipe=Path.Combine(root,"recipe.json"),catalog=Path.Combine(root,"catalog.json");
            await generator.GenerateFilesAsync(recipe,catalog,Path.Combine(root,"api"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(root,"api/world-metadata.json"))),"exact replay after persistence");
            await File.WriteAllTextAsync(Path.Combine(root,"bad.json"),Json(tampered));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(root,"bad.json")),"METADATA_PLACEMENT_PLAN_MISMATCH");
            await RejectAsync(()=>new WorldMetadataSchematicExporter().CreateAsync(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"cli"))==0,"actual CLI generates placement and network");
                await File.WriteAllTextAsync(recipe,Json(strict));Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"failed"))==1,"actual CLI rejects infeasibility");
                Check(!Directory.Exists(Path.Combine(root,"failed")),"failed placement not published");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"durable audit outcomes");
            }
        }
        finally{if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-placement-",StringComparison.Ordinal))Directory.Delete(root,true);}
        Console.WriteLine("PASS: terrain-derived placement, required count and separation, independent backtracking fixture, purpose scoring, deterministic input order, budgets/cancellation, terrain-edit relocation, nested catalog positions, generated network, persistence/tamper and CLI failure/audit. Finite candidates, not continuous optimal packing or whole-world/game acceptance.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){p.Kill(true);throw;}Console.Write(await stdout);Console.Write(await stderr);return p.ExitCode;
    }
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Placement: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
