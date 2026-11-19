using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldTopologyTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        const string id="encounter-expedition";
        WorldMetadataArchetype Type(string name,string kind,MetadataPart[]? parts=null)=>new(name,kind,"Purposeful "+name,new Dictionary<string,JsonElement>(),parts??[],[],[]);
        var catalog=new WorldMetadataCatalog(1,"encounter-program",[
            Type("world","expedition",[new("entry","entry",1,"Arrival"),new("gate","gate",1,"Mandatory transition"),new("goal","goal",1,"Final objective"),
                new("early","early",12,"Encounter before transition"),new("late","late",9,"Encounter after transition")]),
            Type("entry","entry"),Type("gate","transition"),Type("goal","goal"),Type("early","early-encounter"),Type("late","late-encounter")]);
        var rule=new MetadataTopologyRule("journey","Explore branches before and after the transition","program:expedition",id,id+"/entry-0","passage",
            [new("approach","Before transition",id+"/gate-0",["early-encounter"],12,12),
             new("objective","After transition",id+"/goal-0",["late-encounter"],9,9)],"expedition-001",MaximumDegree:6,ExtraConnections:3);
        return(new(1,id,"game-authored-exploration",catalog.Id,"world","encounter-world",MetadataCoverage.PartialScope,
            ["Generated topology only; physical domain, local content, game-specific interaction and artwork are separate."],new Dictionary<string,int>(),new(["x","y"],"meters"),TopologyRules:[rule]),catalog);
    }

    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) PhysicalExample(bool south)
    {
        var e=WorldTerrainCompoundTests.Example(south);string root=e.Recipe.Id;
        var trail=e.Recipe.AdditionalConnections!.Single(c=>c.Id=="trail");
        var rule=new MetadataTopologyRule("outpost-link","Connect the complete expedition compounds","program:outpost-link",root,
            trail.FromId,trail.Kind,[new("crossing","Terrain-qualified connection",trail.ToId,[],0,0)],"outpost-001");
        string edge=root+"/topology/outpost-link/edge-00000";
        return(e.Recipe with{AdditionalConnections=e.Recipe.AdditionalConnections!.Where(c=>c.Id!="trail").ToArray(),TopologyRules=[rule],
            RouteRules=e.Recipe.RouteRules!.Select(r=>r.Interfaces is null?r:r with{Interfaces=r.Interfaces.Select(i=>i with{ConnectionId=edge}).ToArray()}).ToArray()},e.Catalog);
    }

    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var name in new[]{"encounter","north-inlet","south-inlet"})
        {
            var e=name=="encounter"?Example():PhysicalExample(name=="south-inlet");string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
        }
    }

    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==25&&world.Connections.Count==26,"24 connected destinations, 23 tree edges and three loops");
        Check(world.Surface is null&&world.Nodes.All(n=>n.Position is null),"no forced terrain or positions");
        Check(world.ProgressionPlan!.Rules.Count==1&&world.TopologyPlan!.Results.Count==1,"generated graph has progression provenance");
        Independent(world);Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic whole document");
        var variants=new HashSet<string>();
        for(int seed=0;seed<16;seed++)
        {
            var recipe=e.Recipe with{TopologyRules=[e.Recipe.TopologyRules![0] with{Seed="variant-"+seed}]};
            var candidate=generator.Generate(recipe,e.Catalog);Independent(candidate);
            variants.Add(Json(candidate.Connections.Select(c=>new{c.FromId,c.ToId})));
        }
        Check(variants.Count>=8,"seeds change permitted graph structure, not just colors");
        Reject(()=>generator.Generate(e.Recipe with{TopologyRules=[e.Recipe.TopologyRules![0] with{ExtraConnections=32768}]},e.Catalog),"LOOP_DEMAND_UNSATISFIED");
        Reject(()=>generator.Generate(e.Recipe with{TopologyRules=[e.Recipe.TopologyRules![0] with{MaximumWork=1}]},e.Catalog),"WORK_BUDGET");
        var stage=e.Recipe.TopologyRules![0].Stages[0];var rule=e.Recipe.TopologyRules[0];
        Reject(()=>generator.Generate(e.Recipe with{TopologyRules=[rule with{Stages=[stage with{MinimumMembers=13,MaximumMembers=13},rule.Stages[1]]}]},e.Catalog),"MEMBER_COUNT");
        Reject(()=>generator.Generate(e.Recipe with{TopologyRules=[rule with{Stages=[stage with{ExitNodeId=e.Recipe.Id},rule.Stages[1]]}]},e.Catalog),"SCOPE_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{TopologyRules=[rule with{Stages=[stage,rule.Stages[1] with{MemberKinds=stage.MemberKinds}]}]},e.Catalog),"STAGE_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{AdditionalConnections=[new("conflict",rule.ConnectionKind,rule.StartNodeId,stage.ExitNodeId)]},e.Catalog),"RULE_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{MaximumConnections=2},e.Catalog),"CONNECTION_BUDGET");
        var changed=world with{Connections=world.Connections.Skip(1).ToArray()};
        changed=changed with{LayerPlan=new MetadataLayerPlanner().Plan([],changed.Nodes,changed.Connections)};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(changed),"TOPOLOGY_PLAN_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgressionPlan=null}),"PROGRESSION_MISSING_OR_CHANGED");
        var snapshot=world.TopologyPlan!;
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{TopologyPlan=snapshot with{Rules=null!}}),"TOPOLOGY_PLAN_INVALID");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{TopologyPlan=snapshot with{Work=snapshot.Work+1}}),"TOPOLOGY_PLAN_MISMATCH");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();bool cancelled=false;
            try{generator.Generate(e.Recipe,e.Catalog,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"cancellation honored");
        }
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var edited=e.Recipe with{TopologyRules=[rule with{Seed="edited"}]};
        var after=session.Apply(new("topology-edit","Recompose graph","test:topology",before.World.RecipeSha256,before.World.CatalogSha256,edited,e.Catalog));
        Check(after.Report.Stages.Single(s=>s.Id=="content").Reused&&!after.Report.Stages.Single(s=>s.Id=="topology").Reused&&
            !after.Report.Stages.Single(s=>s.Id=="allocation").Reused&&!after.Report.Stages.Single(s=>s.Id=="routes").Reused,"topology invalidates physical dependents without re-expanding content");
        Check(Json(after.World)==Json(generator.Generate(edited,e.Catalog)),"session equals clean generation");
        var physical=new List<WorldMetadataDocument>();
        foreach(bool south in new[]{false,true})
        {
            var p=PhysicalExample(south);var result=generator.Generate(p.Recipe,p.Catalog);physical.Add(result);
            Check(result.Nodes.Count==9&&result.AccessPlan!.Sites.Count==8&&result.RoutePlan!.Routes.Count==3,"complete compounds and routes preserved");
            Check(result.RoutePlan!.Routes.Any(r=>r.ConnectionId.Contains("/topology/",StringComparison.Ordinal)),"generated graph feeds physical routing");
            WorldMetadataGenerator.ValidateDocument(result);
        }
        Check(Json(physical[0].RoutePlan)!=Json(physical[1].RoutePlan),"terrain changes physical connection geometry");
        string temp=Path.Combine(Path.GetTempPath(),"world-topology-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            await File.WriteAllTextAsync(temp,Json(world));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(temp))==Json(world),"saved document replay");
            await File.WriteAllTextAsync(temp,Json(changed));bool refused=false;
            try{await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(temp);}
            catch(ArgumentException error)when(error.Message.Contains("TOPOLOGY_PLAN_MISMATCH")){refused=true;}
            Check(refused,"changed persisted topology refused");
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
        Console.WriteLine("PASS: procedural topology, 16 structural variants, independent graph cuts, physical terrain response, replay, budgets, negative cases and session invalidation.");
    }

    private static void Independent(WorldMetadataDocument world)
    {
        var edges=world.Connections;var nodes=world.Nodes.Where(n=>n.ParentId is not null).Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        var adjacency=nodes.ToDictionary(n=>n,_=>new List<string>(),StringComparer.Ordinal);
        foreach(var edge in edges){Check(edge.Bidirectional&&edge.FromId!=edge.ToId,"non-self bidirectional edges");adjacency[edge.FromId].Add(edge.ToId);adjacency[edge.ToId].Add(edge.FromId);}
        Check(adjacency.All(p=>p.Value.Count<=6),"independent maximum degree");
        Check(edges.Select(c=>string.CompareOrdinal(c.FromId,c.ToId)<0?(c.FromId,c.ToId):(c.ToId,c.FromId)).Distinct().Count()==edges.Count,"unique unordered edges");
        HashSet<string> Reach(string? blocked)
        {
            var seen=new HashSet<string>();var pending=new Queue<string>();pending.Enqueue(world.Id+"/entry-0");
            while(pending.TryDequeue(out var id)){if(id==blocked||!seen.Add(id))continue;foreach(string next in adjacency[id])pending.Enqueue(next);}
            return seen;
        }
        Check(Reach(null).SetEquals(nodes),"all selected destinations connected");
        Check(!Reach(world.Id+"/gate-0").Contains(world.Id+"/goal-0"),"no mandatory-transition bypass");
        foreach(var edge in edges)Check(!(edge.FromId.Contains("/early-")&&edge.ToId.Contains("/late-")||edge.FromId.Contains("/late-")&&edge.ToId.Contains("/early-")),"stage membership preserved");
        WorldMetadataGenerator.ValidateDocument(world);
    }
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected rejection: "+code);
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
