using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldProgressionTests
{
    private const string Root="encounter-journey";
    private static string Node(string part,int index=0)=>$"{Root}/{part}-{index}";
    public static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        WorldMetadataArchetype Type(string id,string kind,MetadataPart[]? parts=null)=>
            new(id,kind,"Authored exploration "+kind,new Dictionary<string,JsonElement>(),parts??[],[],[]);
        var catalog=new WorldMetadataCatalog(1,"encounter-catalog",[
            Type("journey","journey",[new("entry","encounter",1,"Journey entry"),new("choices","choice",2,"Alternative encounters"),
                new("relay","encounter",1,"Required first milestone"),new("vault","encounter",1,"Required second milestone"),new("exit","encounter",1,"Journey goal")]),
            Type("encounter","encounter"),Type("choice","encounter",[new("reward","reward",1,"Required nested reward")]),Type("reward","reward")]);
        MetadataExplicitConnection Link(string id,string from,string to)=>new(id,"advance",from,to,false);
        var links=new[]{Link("entry-left",Node("entry"),Node("choices")),Link("entry-right",Node("entry"),Node("choices",1)),
            Link("left-relay",Node("choices"),Node("relay")),Link("right-relay",Node("choices",1),Node("relay")),
            Link("relay-vault",Node("relay"),Node("vault")),Link("vault-exit",Node("vault"),Node("exit"))};
        var rule=new MetadataProgressionRule("main-journey","Reach exit through the relay then vault","game:journey",Root,Node("entry"),
            [Node("exit")],["advance"],[Node("relay"),Node("vault")]);
        var recipe=new WorldMetadataRecipe(1,Root,"branching-exploration",catalog.Id,"journey","encounter-graph",MetadataCoverage.PartialScope,
            ["Logical journey only; encounter behavior, reward interaction and spatial realization belong to the consuming game."],
            new Dictionary<string,int>(),new([],"none"),AdditionalConnections:links,ProgressionRules:[rule]);
        return(recipe,catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example destination exists");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var planner=new MetadataProgressionPlanner();var world=generator.Generate(e.Recipe,e.Catalog);
        var rule=e.Recipe.ProgressionRules![0];var plan=world.ProgressionPlan!;
        Check(world.Nodes.Count==9&&world.Nodes.Count(n=>n.Kind=="reward")==2,"all nested content retained");
        Check(world.Nodes.All(n=>n.Position is null)&&world.Surface is null&&world.RoutePlan is null,"no terrain or physical layout requirement");
        var path=plan.Results.Single().Paths.Single();
        Check(path.NodeIds.SequenceEqual(new[]{Node("entry"),Node("choices"),Node("relay"),Node("vault"),Node("exit")}),"deterministic branching witness");
        for(int i=0;i<path.ConnectionIds.Count;i++)
        {
            var link=world.Connections.Single(l=>l.Id==path.ConnectionIds[i]);
            Check(link.FromId==path.NodeIds[i]&&link.ToId==path.NodeIds[i+1],"independent directed witness edge check");
        }
        Check(Json(planner.Plan([rule],world.Nodes.Reverse().ToArray(),world.Connections.Reverse().ToArray()))==Json(plan),"input order does not change plan or work");
        var shortRule=rule with{Id="relay-only",GoalNodeIds=[Node("relay")],RequiredMilestones=[]};
        Check(Json(planner.Plan([shortRule,rule],world.Nodes,world.Connections))==Json(planner.Plan([rule,shortRule],world.Nodes,world.Connections)),"multiple rules ordered deterministically");
        var multiGoal=rule with{GoalNodeIds=[Node("vault"),Node("exit")],RequiredMilestones=[Node("relay")]};
        Check(planner.Plan([multiGoal],world.Nodes,world.Connections).Results[0].Paths.Count==2,"every declared goal receives a witness");
        var last=world.Connections.Single(l=>l.ToId==Node("exit"));
        Reject(()=>planner.Plan([rule],world.Nodes,world.Connections.Where(l=>l!=last).ToArray()),"METADATA_PROGRESSION_UNREACHABLE");
        Reject(()=>planner.Plan([rule],world.Nodes,world.Connections.Select(l=>l==last?l with{FromId=l.ToId,ToId=l.FromId}:l).ToArray()),"METADATA_PROGRESSION_UNREACHABLE");
        var reverse=world.Connections.Select(l=>l with{FromId=l.ToId,ToId=l.FromId,Bidirectional=true}).ToArray();
        Check(planner.Plan([rule],world.Nodes,reverse).Results.Single().Paths.Single().NodeIds.SequenceEqual(path.NodeIds),"bidirectional links permit reverse traversal");
        var bypass=last with{Id="bypass",FromId=Node("entry")};
        Reject(()=>planner.Plan([rule],world.Nodes,[..world.Connections,bypass]),"METADATA_PROGRESSION_MILESTONE_BYPASS");
        Reject(()=>planner.ValidateDocument(world with{Connections=[..world.Connections,bypass]}),"METADATA_PROGRESSION_MILESTONE_BYPASS");
        Reject(()=>planner.Plan([rule],world.Nodes.Select(n=>n.Id==Root?n with{ParentId=Node("relay")}:n).ToArray(),world.Connections),"METADATA_PROGRESSION_HIERARCHY_INVALID");
        Check(Json(planner.Plan([rule],world.Nodes,[..world.Connections,bypass with{Kind="look-at"}]).Results[0].Paths)==Json(plan.Results[0].Paths),"unselected relationship is not traversable");
        Reject(()=>planner.Plan([rule with{RequiredMilestones=rule.RequiredMilestones.Reverse().ToArray()}],world.Nodes,world.Connections),"METADATA_PROGRESSION_MILESTONE_ORDER");
        Reject(()=>planner.Plan([rule with{ScopeNodeId=Node("choices")}],world.Nodes,world.Connections),"METADATA_PROGRESSION_SCOPE_INVALID");
        var outside=world.Nodes[0] with{Id="outside",ParentId=null};
        var outsideLinks=new[]{bypass with{Id="out",ToId="outside"},bypass with{Id="back",FromId="outside"}};
        Check(planner.Plan([rule],[..world.Nodes,outside],[..world.Connections,..outsideLinks]).Results[0].Paths[0].NodeIds.SequenceEqual(path.NodeIds),"cannot escape scope to bypass milestones");
        Reject(()=>planner.Plan([rule,rule],world.Nodes,world.Connections),"METADATA_PROGRESSION_RULE_INVALID");
        Reject(()=>planner.Plan([rule with{RequiredMilestones=[Node("relay"),Node("relay")]}],world.Nodes,world.Connections),"METADATA_PROGRESSION_RULE_INVALID");
        Reject(()=>planner.Plan([rule],world.Nodes,[..world.Connections,last with{Id="bad",FromId=null!}]),"METADATA_PROGRESSION_LINK_INVALID");
        Reject(()=>planner.Plan([rule with{MaximumWork=plan.Work-1}],world.Nodes,world.Connections),"METADATA_PROGRESSION_WORK_BUDGET");
        Check(planner.Plan([rule with{MaximumWork=plan.Work}],world.Nodes,world.Connections).Work==plan.Work,"exact work bound");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{planner.Plan([rule],world.Nodes,world.Connections,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        var changed=world with{ProgressionPlan=plan with{Results=[plan.Results[0] with{Paths=[path with{NodeIds=[Node("entry"),Node("exit")]}]}]}};
        Reject(()=>planner.ValidateDocument(changed),"METADATA_PROGRESSION_PLAN_MISMATCH");
        try{await new WorldMetadataSchematicExporter().CreateAsync(changed);throw new Exception("Tampered export accepted");}
        catch(ArgumentException ex)when(ex.Message.StartsWith("METADATA_PROGRESSION_PLAN_MISMATCH",StringComparison.Ordinal)){}
        string folder=Path.Combine(Path.GetTempPath(),"worldcreator-progression-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(folder);
        await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"output"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"output/world-metadata.json")))==Json(world),"disk read replays complete progression");
        string bad=Path.Combine(folder,"tampered.json");await File.WriteAllTextAsync(bad,Json(changed));
        try{await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(bad);throw new Exception("Tampered read accepted");}
        catch(ArgumentException ex)when(ex.Message.StartsWith("METADATA_PROGRESSION_PLAN_MISMATCH",StringComparison.Ordinal)){}
        var invalid=e.Recipe with{AdditionalConnections=[..e.Recipe.AdditionalConnections!,new("bypass","advance",Node("entry"),Node("exit"),false)]};
        await File.WriteAllTextAsync(Path.Combine(folder,"bad-recipe.json"),Json(invalid));
        try{await generator.GenerateFilesAsync(Path.Combine(folder,"bad-recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"invalid-output"));throw new Exception("Bypass exported");}
        catch(ArgumentException ex)when(ex.Message.StartsWith("METADATA_PROGRESSION_MILESTONE_BYPASS",StringComparison.Ordinal)){}
        Check(!Directory.Exists(Path.Combine(folder,"invalid-output")),"invalid world not published");
        Console.WriteLine($"PASS: directed branching progression, nested content, mandatory milestone dominance/order, scope/kinds/direction, deterministic witness, work={plan.Work}, cancellation, API/file/export replay and rejection.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Progression: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException ex)when(ex.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
