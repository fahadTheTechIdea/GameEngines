using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldNetworkTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Network: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldNetworkOptions Options()=>new("Connect required exploration locations","example:access","region",
        new("foot","Walk between destinations","example:walking",1,.5,new Dictionary<string,double>{{"meadow",1},{"mud",20}}),new(.5,[],MaximumWork:2000000),
        NearestNeighbors:2,MaximumWork:30000000);
    private static WorldNetworkDestination D(string id,double x,double y)=>new(id,"Required location "+id,new(x,y));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurfaceTests.Example();var surface=WorldSurfaceTests.Surface("flat");
        surface=surface with{Patches=[surface.Patches[0],surface.Patches[0] with{Id="water",Kind="water",Priority=1,Boundary=Box(45,0,10,20),Purpose="Keep the inlet open"}]};
        var root=e.Catalog.Archetypes[0] with{Parts=[new("locations","location",4,"Required exploration location",
            Layout:MetadataPathLayout.Parameters([[0,0],[80,0],[80,20],[0,20]],50))],Connections=[]};
        return(e.Recipe with{Id="destination-network",Surface=surface,RouteRules=null,
            RemainingScopes=["Authored terrain and destination anchors; generated landforms, suitability, regions, junctions, structures, art and runtime remain open"],
            NetworkRules=[new("regional-access","destination-network","destination",4,4,"regional-travel",Options() with{
                Connectivity=WorldNetworkConnectivity.SingleConnectionFailure,NearestNeighbors=1},"intent")]},e.Catalog with{Archetypes=[root,e.Catalog.Archetypes[1]]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("EXAMPLE_EXISTS");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        // Independently remove each edge in every graph of four vertices and count BFS components.
        string[] ids=["a","b","c","d"];var pairs=(from a in Enumerable.Range(0,4) from b in Enumerable.Range(a+1,3-a) select (a,b)).ToArray();
        int Components(WorldNetworkLink[] edges)
        {
            var visited=new HashSet<string>();int count=0;
            foreach(var id in ids)if(visited.Add(id))
            {
                count++;var queue=new Queue<string>();queue.Enqueue(id);
                while(queue.TryDequeue(out var current))foreach(var edge in edges)
                {
                    string? next=edge.FromId==current?edge.ToId:edge.ToId==current?edge.FromId:null;
                    if(next is not null&&visited.Add(next))queue.Enqueue(next);
                }
            }
            return count;
        }
        var analyzer=new WorldNetworkConnectivityAnalyzer();
        for(int mask=0;mask<64;mask++)
        {
            var edges=pairs.Select((p,i)=>(p,i)).Where(p=>(mask&(1<<p.i))!=0).Select(p=>new WorldNetworkLink("e"+p.i,ids[p.p.a],ids[p.p.b])).ToArray();
            var actual=analyzer.Analyze(ids,edges);int count=Components(edges);
            Check(actual.Components.Count==count,"independent graph components");
            Check(actual.Bridges.SequenceEqual(edges.Where(e=>Components(edges.Where(other=>other!=e).ToArray())>count).Select(e=>e.Id).Order()),"independent single-edge cut oracle");
        }
        Reject(()=>analyzer.Analyze(ids,[new("x","a","b"),new("y","b","a")]),"WORLD_NETWORK_GRAPH_PARALLEL_EDGE");
        var planner=new WorldDestinationNetworkPlanner();var surface=WorldSurfaceTests.Surface("flat");
        var request=new WorldNetworkRequest("sample",[D("a",10,10),D("b",30,10),D("c",20,30)],Options());
        var flat=planner.Plan(surface,request);
        Reject(()=>planner.Plan(surface,request with{Options=Options() with{Reuse=new(1),Search=Options().Search with{
            Objective=WorldRouteObjective.FirstFeasible,Rounding=new(.1)}}}),"WORLD_ROUTE_ROUNDING_SEARCH_UNSUPPORTED");
        bool Pair(WorldNetworkEdge e,string a,string b)=>new[]{e.Link.FromId,e.Link.ToId}.Order().SequenceEqual(new[]{a,b}.Order());
        Check(flat.Status==WorldNetworkStatus.Found && flat.Edges.Count==2 && flat.Edges.Any(e=>Pair(e,"a","b")),"shortest flat candidate backbone");
        var mud=surface with{Patches=[surface.Patches[0],surface.Patches[0] with{Id="mud",Kind="mud",Priority=1,Boundary=Box(19,0,2,18)}]};
        var weighted=planner.Plan(mud,request);
        Check(weighted.Status==WorldNetworkStatus.Found&&!weighted.Edges.Any(e=>Pair(e,"a","b")),"terrain route cost changes selected network links");
        var forbiddenRequest=request with{Options=Options() with{NearestNeighbors=1,ForbiddenConnections=[new("b","a")]}};
        var restricted=planner.Plan(surface,forbiddenRequest);
        Check(restricted.Status==WorldNetworkStatus.Found&&restricted.Edges.Count==2&&restricted.Edges.All(e=>!Pair(e,"a","b"))&&restricted.Attempts.All(a=>!Pair(new(a.Link,"",null!,null!),"a","b")),"forbidden direct pair is never evaluated or selected");
        Check(Json(restricted)==Json(planner.Plan(surface,forbiddenRequest with{Destinations=request.Destinations.Reverse().ToArray(),Options=forbiddenRequest.Options with{ForbiddenConnections=[new("a","b")]}})),"forbidden pair canonicalization");
        Check(planner.Plan(surface,request with{Options=Options() with{ForbiddenConnections=[new("a","b"),new("a","c")]}}).Status==WorldNetworkStatus.CandidateGraphDisconnected,"forbidden pairs can disconnect network");
        Check(planner.Plan(surface,request with{Options=Options() with{Connectivity=WorldNetworkConnectivity.SingleConnectionFailure,ForbiddenConnections=[new("a","b")]}}).Status==WorldNetworkStatus.RedundancyUnresolved,"forbidden pair cannot be invented for redundancy");
        Reject(()=>planner.Plan(surface,request with{Options=Options() with{ForbiddenConnections=[new("a","b"),new("b","a")]}}),"WORLD_NETWORK_FORBIDDEN_INVALID");
        Reject(()=>planner.Plan(surface,request with{Options=Options() with{ForbiddenConnections=[new("a","missing")]}}),"WORLD_NETWORK_FORBIDDEN_INVALID");
        Reject(()=>planner.Plan(surface,request with{Options=Options() with{RequiredConnections=[new("a","b")],ForbiddenConnections=[new("b","a")]}}),"WORLD_NETWORK_REQUIRED_FORBIDDEN_CONFLICT");
        var mandatory=planner.Plan(mud,request with{Options=Options() with{RequiredConnections=[new("a","b")]}});
        Check(mandatory.Edges.Any(e=>Pair(e,"a","b")&&e.Reason=="required"),"authored connection preserved despite cost");
        var redundant=planner.Plan(surface,request with{Options=Options() with{Connectivity=WorldNetworkConnectivity.SingleConnectionFailure}});
        Check(redundant.Status==WorldNetworkStatus.Found&&redundant.Edges.Count==3&&redundant.Analysis.Bridges.Count==0,"triangle survives each selected connection failure");
        Check(Json(redundant)==Json(planner.Plan(surface,request with{Destinations=request.Destinations.Reverse().ToArray(),Options=Options() with{Connectivity=WorldNetworkConnectivity.SingleConnectionFailure}})),"input permutation preserves exact plan");
        var two=request with{Destinations=request.Destinations.Take(2).ToArray(),Options=Options() with{Connectivity=WorldNetworkConnectivity.SingleConnectionFailure}};
        Check(planner.Plan(surface,two).Status==WorldNetworkStatus.RedundancyUnresolved,"cannot invent second parallel connection");
        var clusters=request with{Destinations=[D("a",10,10),D("b",12,10),D("c",10,12),D("d",95,10),D("e",97,10),D("f",95,12)],Options=Options() with{NearestNeighbors=1}};
        var expanded=planner.Plan(surface,clusters);
        Check(expanded.Status==WorldNetworkStatus.Found&&expanded.NearestNeighborsUsed>2&&expanded.Edges.Count==5,"bounded proximity expansion connects separated clusters");
        var wall=surface with{Exclusions=[new("barrier","Disconnected sides","test:barrier",Box(19,0,2,100))]};
        var separated=planner.Plan(wall,request with{Destinations=[D("a",10,10),D("b",30,10)]});
        Check(separated.Status==WorldNetworkStatus.CandidateGraphDisconnected&&separated.Analysis.Components.Count==2,"no fake bridge over exclusion");
        Check(planner.Plan(wall,request with{Destinations=[D("a",10,10),D("b",30,10)],Options=Options() with{RequiredConnections=[new("a","b")]}}).Status==WorldNetworkStatus.RequiredConnectionUnresolved,"required unreachable pair explicitly fails");
        Reject(()=>planner.Plan(surface,request with{Options=Options() with{MaximumPairChecks=1}}),"WORLD_NETWORK_PAIR_BUDGET");
        Reject(()=>planner.Plan(surface,request with{Options=Options() with{MaximumWork=1}}),"WORLD_NETWORK_WORK_BUDGET");
        Reject(()=>planner.Plan(surface,request with{Options=Options() with{RequiredConnections=[new("a","missing")]}}),"WORLD_NETWORK_REQUIRED_INVALID");
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();try{planner.Plan(surface,request,cancelled.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}}

        var example=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(example.Recipe,example.Catalog);
        Check(world.Nodes.Count==5&&world.NetworkPlan?.Networks.Count==1&&world.Connections.Count>=4,"actual metadata generates network from destination demand");
        var originalLink=world.NetworkPlan!.Networks[0].Plan.Edges[0].Link;
        var networkRule=example.Recipe.NetworkRules![0];
        var restrictedRecipe=example.Recipe with{NetworkRules=[networkRule with{Options=networkRule.Options with{ForbiddenConnections=[new(originalLink.ToId,originalLink.FromId)]}}]};
        var restrictedWorld=generator.Generate(restrictedRecipe,example.Catalog);
        Check(restrictedWorld.NetworkPlan!.Networks[0].Plan.Attempts.All(a=>a.Link.FromId!=originalLink.FromId||a.Link.ToId!=originalLink.ToId),"metadata preserves forbidden pair in generated network");
        new MetadataNetworkPlanner().ValidateDocument(restrictedWorld);

        Check(world.NetworkPlan!.Networks[0].Plan.Analysis.Bridges.Count==0,"metadata redundancy qualified");
        Check(world.LayerPlan.Layers.Single().ConnectionIds.Count==world.Connections.Count,"generated network edges included in semantic layer");
        Check(generator.Generate(example.Recipe with{NetworkRules=null},example.Catalog).Connections.Count==0,"roadless demand creates no network");
        var outer=new WorldMetadataArchetype("outer","world","Scoped regions",new Dictionary<string,JsonElement>(),
            [new("selected","root",1,"Selected region",Layout:MetadataLineLayout.Parameters([0,0],[0,0])),
             new("other","other-region",1,"Unselected region",Layout:MetadataLineLayout.Parameters([0,50],[0,0]))],[],[]);
        var other=new WorldMetadataArchetype("other-region","region","Other content",new Dictionary<string,JsonElement>(),
            [new("locations","location",2,"Unselected destinations",Layout:MetadataLineLayout.Parameters([0,0],[10,0]))],[],[]);
        var scoped=generator.Generate(example.Recipe with{RootArchetypeId="outer",NetworkRules=[example.Recipe.NetworkRules![0] with{
            RegionNodeId="destination-network/selected-0"}]},example.Catalog with{Archetypes=[..example.Catalog.Archetypes,outer,other]});
        Check(scoped.Nodes.Count(n=>n.Kind=="destination")==6&&scoped.NetworkPlan!.Networks[0].Plan.Destinations.Count==4&&
            scoped.NetworkPlan.Networks[0].Plan.Destinations.All(d=>d.Id.StartsWith("destination-network/selected-0/",StringComparison.Ordinal)),
            "scope selects descendants and preserves unrelated destinations without routes");
        Reject(()=>generator.Generate(example.Recipe with{NetworkRules=[example.Recipe.NetworkRules![0] with{MinimumDestinations=5,MaximumDestinations=5}]},example.Catalog),"METADATA_NETWORK_DESTINATION_COUNT");
        Reject(()=>generator.Generate(example.Recipe with{MaximumConnections=1},example.Catalog),"METADATA_CONNECTION_BUDGET");
        var group=world.NetworkPlan.Networks[0];var forged=world with{NetworkPlan=world.NetworkPlan with{Networks=[group with{Plan=group.Plan with{Edges=[]}}]}};
        Reject(()=>new MetadataNetworkPlanner().ValidateDocument(forged),"METADATA_NETWORK_PLAN_MISMATCH");
        Reject(()=>new MetadataNetworkPlanner().ValidateDocument(world with{NetworkPlan=null}),"METADATA_NETWORK_PLAN_REQUIRED");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-network-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);string recipe=Path.Combine(root,"recipe.json"),catalog=Path.Combine(root,"catalog.json"),output=Path.Combine(root,"world");
            await generator.GenerateFilesAsync(recipe,catalog,output);
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"))),"exact network persistence/rederivation");
            if(cli is not null)
            {
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"cli-world"))==0,"actual network CLI succeeds");
                await File.WriteAllTextAsync(recipe,Json(example.Recipe with{Surface=example.Recipe.Surface! with{Exclusions=[new("wall","Barrier","test:barrier",Box(45,0,10,100))]}}));
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"failed"))==1,"actual CLI refuses disconnected mandatory demand");
                Check(!Directory.Exists(Path.Combine(root,"failed")),"failed network publishes no world");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.failed")&&audit.Contains("world.metadata.succeeded"),"durable network outcomes");
            }
        }
        finally{if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-network-",StringComparison.Ordinal))Directory.Delete(root,true);}
        Console.WriteLine("PASS: 64 independently checked graphs; terrain-cost network selection, required edges, adaptive candidate expansion, connection-failure tolerance, scoped metadata demand, deterministic budgets/cancellation, persistence/tamper and actual CLI audit. No spatially disjoint paths, junction, region-placement or complete-world claim.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;
    }
}
