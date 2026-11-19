using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldCapabilityProgressionTests
{
    private const string Root="capability-journey";
    private static string N(string id)=>Root+"/"+id+"-0";
    private static string L(string id)=>Root+"/explicit/"+id;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Capability progression: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"capability-catalog",[
            new("journey","journey","Reusable capability-gated journey",properties,[..new[]{"entry","hall","key","vault","exit"}.Select(id=>new MetadataPart(id,"location",1,"Required "+id))],[],[]),
            new("location","location","Game-authored location",properties,[],[],[])]);
        MetadataExplicitConnection Link(string id,string from,string to)=>new(id,"travel",N(from),N(to),true);
        var links=new[]{Link("entry-hall","entry","hall"),Link("hall-key","hall","key"),Link("hall-vault","hall","vault"),Link("vault-exit","vault","exit")};
        var caps=new MetadataProgressionCapabilities(["bronze","silver"],[],[new(N("key"),["bronze"]),new(N("vault"),["silver"])],
            [new(L("hall-vault"),["bronze"]),new(L("vault-exit"),["bronze","silver"])],RequireReturnToStart:true);
        var rule=new MetadataProgressionRule("journey","Reach every goal with persistent abilities and a safe return","game:capability-journey",Root,N("entry"),[N("exit")],["travel"],[N("key"),N("vault")],Capabilities:caps);
        var recipe=new WorldMetadataRecipe(1,Root,"capability-reference",catalog.Id,"journey","capability-graph",MetadataCoverage.PartialScope,
            ["Logical persistent-capability progression only; no physical layout or runtime inventory behavior."],new Dictionary<string,int>(),new([],"none"),AdditionalConnections:links,ProgressionRules:[rule]);
        return(recipe,catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example destination exists");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? output=null)
    {
        if(output is not null&&(Directory.Exists(output)||File.Exists(output)))throw new IOException("Evidence output exists");
        var e=Example();var generator=new WorldMetadataGenerator();var planner=new MetadataProgressionPlanner();var world=generator.Generate(e.Recipe,e.Catalog);
        var rule=e.Recipe.ProgressionRules![0];var caps=rule.Capabilities!;var plan=world.ProgressionPlan!;var path=plan.Results[0].Paths[0];
        Check(path.NodeIds.SequenceEqual(new[]{N("entry"),N("hall"),N("key"),N("hall"),N("vault"),N("exit")}),"revisit junction with acquired key");
        Check(path.CapabilityStates![1].Count==0&&path.CapabilityStates[3].SequenceEqual(new[]{"bronze"})&&path.CapabilityStates[^1].SequenceEqual(new[]{"bronze","silver"}),"saved exact acquisition trace");
        Check(world.Nodes.Count==6&&world.Nodes.All(n=>n.Position is null),"complete nonspatial program");
        Check(Json(planner.Plan([rule],world.Nodes.Reverse().ToArray(),world.Connections.Reverse().ToArray()))==Json(plan),"permuted graph deterministic");
        var permuted=rule with{Capabilities=caps with{CapabilityIds=caps.CapabilityIds.Reverse().ToArray(),Grants=caps.Grants.Reverse().ToArray(),Gates=caps.Gates.Reverse().Select(g=>g with{RequiredCapabilityIds=g.RequiredCapabilityIds.Reverse().ToArray()}).ToArray()}};
        Check(Json(planner.Plan([permuted],world.Nodes,world.Connections))==Json(plan),"normalized capability declarations");
        var selfLocked=caps with{Grants=[new(N("vault"),["bronze","silver"])]};
        Reject(()=>planner.Plan([rule with{Capabilities=selfLocked}],world.Nodes,world.Connections),"METADATA_PROGRESSION_UNREACHABLE");
        var mutual=caps with{Grants=[new(N("exit"),["bronze"]),new(N("vault"),["silver"])],Gates=[new(L("hall-vault"),["bronze"]),new(L("vault-exit"),["silver"]) ]};
        Reject(()=>planner.Plan([rule with{Capabilities=mutual}],world.Nodes,world.Connections),"METADATA_PROGRESSION_UNREACHABLE");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{InitialCapabilityIds=["bronze"]}}],world.Nodes,world.Connections),"METADATA_PROGRESSION_MILESTONE_BYPASS");
        var oneWay=world.Connections.Select(l=>l.Id==L("vault-exit")?l with{Bidirectional=false}:l).ToArray();
        Reject(()=>planner.Plan([rule],world.Nodes,oneWay),"METADATA_PROGRESSION_RETURN_UNREACHABLE");
        var dead=world.Nodes.Single(n=>n.Id==N("key")) with{Id=N("dead")};
        var trap=world.Connections[0] with{Id="optional-trap",FromId=N("entry"),ToId=dead.Id,Bidirectional=false};
        Reject(()=>planner.Plan([rule],[..world.Nodes,dead],[..world.Connections,trap]),"METADATA_PROGRESSION_RETURN_UNREACHABLE");
        Check(planner.Plan([rule with{Capabilities=caps with{RequireReturnToStart=false}}],world.Nodes,oneWay).Results.Count==1,"one-way journey valid when explicitly allowed");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{MaximumStates=1}}],world.Nodes,world.Connections),"METADATA_PROGRESSION_STATE_BUDGET");
        Check(planner.Plan([rule with{Capabilities=caps with{MaximumStates=plan.Results[0].ReachableStates!.Value}}],world.Nodes,world.Connections).Results[0].ReachableStates==plan.Results[0].ReachableStates,"exact state cap");
        Reject(()=>planner.Plan([rule with{MaximumWork=plan.Work-1}],world.Nodes,world.Connections),"METADATA_PROGRESSION_WORK_BUDGET");
        Check(planner.Plan([rule with{MaximumWork=plan.Work}],world.Nodes,world.Connections).Work==plan.Work,"exact work cap");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{InitialCapabilityIds=["unknown"]}}],world.Nodes,world.Connections),"METADATA_PROGRESSION_CAPABILITY_UNKNOWN");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{CapabilityIds=["bronze","silver","unused"]}}],world.Nodes,world.Connections),"METADATA_PROGRESSION_CAPABILITY_UNUSED");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{Grants=[..caps.Grants,caps.Grants[0]]}}],world.Nodes,world.Connections),"METADATA_PROGRESSION_GRANT_INVALID");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{Gates=[new("missing",["bronze"])]}}],world.Nodes,world.Connections),"METADATA_PROGRESSION_GATE_INVALID");
        var outside=dead with{Id="outside",ParentId=null};
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{Grants=[new(outside.Id,["bronze","silver"])]}}],[..world.Nodes,outside],world.Connections),"METADATA_PROGRESSION_GRANT_INVALID");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{Gates=[new(L("hall-vault"),["bronze"])]}}],world.Nodes,world.Connections.Select(l=>l.Id==L("hall-vault")?l with{Kind="unselected"}:l).ToArray()),"METADATA_PROGRESSION_GATE_INVALID");
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{CapabilityIds=Enumerable.Range(0,17).Select(i=>"key"+i).ToArray()}}],world.Nodes,world.Connections),"METADATA_PROGRESSION_CAPABILITIES_INVALID");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan([rule],world.Nodes,world.Connections,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        // Independent finite-set closure: no production BFS, bit masks or predecessor code.
        var matrix=new List<object>();int accepted=0,refused=0;
        foreach(var milestones in new IReadOnlyList<string>[]{[],[N("key")],[N("vault")],[N("key"),N("vault")],[N("vault"),N("key")]})
        for(int variant=0;variant<64;variant++)
        {
            var grants=new[]{new MetadataCapabilityGrant(N((variant&1)==0?"key":"vault"),["bronze"]),new MetadataCapabilityGrant(N((variant&2)==0?"vault":"exit"),["silver"])};
            // Coalesce grants when both game rules select the same location.
            grants=grants.GroupBy(g=>g.NodeId).Select(g=>new MetadataCapabilityGrant(g.Key,g.SelectMany(x=>x.CapabilityIds).ToArray())).ToArray();
            var trial=rule with{RequiredMilestones=milestones,Capabilities=caps with{Grants=grants,InitialCapabilityIds=(variant&4)==0?[]:["bronze"],RequireReturnToStart=(variant&8)!=0,
                Gates=[new(L("hall-vault"),(variant&16)==0?["bronze"]:["bronze","silver"]),new(L("vault-exit"),["silver"])]}};
            var links=world.Connections.Select(l=>(variant&32)!=0&&l.Id==L("vault-exit")?l with{Bidirectional=false}:l).ToArray();
            var oracle=Oracle(trial,links);
            MetadataProgressionPlan? actual=null;string? diagnostic=null;
            try{actual=planner.Plan([trial],world.Nodes,links);Check(oracle.Valid,"solver accepted oracle refusal variant "+variant);Check(actual.Results[0].ReachableStates==oracle.Count,"oracle exact reachable states variant "+variant);accepted++;}
            catch(ArgumentException error)when(error.Message.StartsWith("METADATA_PROGRESSION_UNREACHABLE")||error.Message.StartsWith("METADATA_PROGRESSION_RETURN_UNREACHABLE")||
                error.Message.StartsWith("METADATA_PROGRESSION_MILESTONE_BYPASS")||error.Message.StartsWith("METADATA_PROGRESSION_MILESTONE_ORDER"))
            {Check(!oracle.Valid,"solver refused oracle acceptance variant "+variant);diagnostic=error.Message;refused++;}
            matrix.Add(new{Variant=variant,Milestones=milestones,Rule=trial,Connections=links,Accepted=actual is not null,Plan=actual,Diagnostic=diagnostic});
        }
        var allCapabilities=Enumerable.Range(0,16).Select(i=>"ability-"+i.ToString("D2")).ToArray();
        var upperRecipe=e.Recipe with{ProgressionRules=[rule with{Capabilities=caps with{CapabilityIds=allCapabilities,InitialCapabilityIds=[],
            Grants=[new(N("key"),allCapabilities)],Gates=[new(L("hall-vault"),allCapabilities),new(L("vault-exit"),allCapabilities)]}}]};
        var upperWorld=generator.Generate(upperRecipe,e.Catalog);
        Check(upperWorld.ProgressionPlan!.Results[0].Paths[0].CapabilityStates![^1].SequenceEqual(allCapabilities),"supported sixteen-capability conjunction retains every ability");
        var tampered=world with{ProgressionPlan=plan with{Results=[plan.Results[0] with{ReachableStates=1}]}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_PROGRESSION_PLAN_MISMATCH");
        var changedPath=path with{CapabilityStates=path.CapabilityStates!.Select((s,i)=>i==3?(IReadOnlyList<string>)[]:s).ToArray()};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgressionPlan=plan with{Results=[plan.Results[0] with{Paths=[changedPath]}]}}),"METADATA_PROGRESSION_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var edited=e.Recipe with{ProgressionRules=[rule with{RequiredMilestones=[N("vault")],Capabilities=caps with{InitialCapabilityIds=["bronze"]}}]};
        var after=session.Apply(new("initial-key","Change declared initial capability","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,edited,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(edited,e.Catalog)),"incremental equals clean");
        try{session.Apply(new("locked","Required key placed behind its own lock","test:locked",after.World.RecipeSha256,after.World.CatalogSha256,e.Recipe with{ProgressionRules=[rule with{Capabilities=selfLocked}]},e.Catalog));throw new Exception("Self-lock edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(after)==Json(session.Snapshot),"invalid edit preserves state");
        string folder=output??Path.Combine(Path.GetTempPath(),"capability-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(folder);
        await File.WriteAllTextAsync(Path.Combine(folder,"matrix.json"),Json(new{Nodes=world.Nodes,Cases=matrix,Accepted=accepted,Refused=refused}));
        await File.WriteAllTextAsync(Path.Combine(folder,"upper-recipe.json"),Json(upperRecipe));
        await File.WriteAllTextAsync(Path.Combine(folder,"upper-world.json"),Json(upperWorld));
        await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"output"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"output/world-metadata.json")))==Json(world),"file replay and export");
        await session.PublishAsync(Path.Combine(folder,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"checkpoint history replay");
        await File.WriteAllTextAsync(Path.Combine(folder,"bad-recipe.json"),Json(e.Recipe with{ProgressionRules=[rule with{Capabilities=selfLocked}]}));
        try{await generator.GenerateFilesAsync(Path.Combine(folder,"bad-recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"refused"));throw new Exception("Self-locked world published");}catch(ArgumentException error)when(error.Message.StartsWith("METADATA_PROGRESSION_UNREACHABLE")){}
        Check(!Directory.Exists(Path.Combine(folder,"refused")),"no partial publication");
        Console.WriteLine($"PASS: capability acquisition/revisits, self/mutual locks, 16-capability conjunction, stateful milestones, return safety, {matrix.Count} independent finite-set oracle cases ({accepted} accepted/{refused} refused), deterministic normalization, work/state/cancellation refusal, tamper, file/session replay and rollback; states={plan.Results[0].ReachableStates}, work={plan.Work}.");
    }
    private static (bool Valid,int Count) Oracle(MetadataProgressionRule rule,IReadOnlyList<WorldMetadataConnection> links)
    {
        var c=rule.Capabilities!;
        string Acquire(string node,string inventory)=>string.Join("|",inventory.Split('|',StringSplitOptions.RemoveEmptyEntries).Concat(c.Grants.Where(g=>g.NodeId==node).SelectMany(g=>g.CapabilityIds)).Distinct().Order(StringComparer.Ordinal));
        bool Allowed(string inventory,string id)=>c.Gates.Where(g=>g.ConnectionId==id).SelectMany(g=>g.RequiredCapabilityIds).All(inventory.Split('|').Contains);
        (HashSet<(string Node,string Inventory)> States,HashSet<((string Node,string Inventory) From,(string Node,string Inventory) To)> Transitions) Closure(string? excluded=null)
        {
            var reached=new HashSet<(string Node,string Inventory)>{(rule.StartNodeId,Acquire(rule.StartNodeId,string.Join("|",c.InitialCapabilityIds)))};
            var edges=new HashSet<((string Node,string Inventory) From,(string Node,string Inventory) To)>();
            bool expanding=true;while(expanding){expanding=false;foreach(var state in reached.ToArray())foreach(var link in links){if(!rule.ConnectionKinds.Contains(link.Kind)||!Allowed(state.Inventory,link.Id))continue;
                string? target=link.FromId==state.Node?link.ToId:link.Bidirectional&&link.ToId==state.Node?link.FromId:null;if(target is null||target==excluded)continue;
                var next=(target,Acquire(target,state.Inventory));edges.Add((state,next));expanding|=reached.Add(next);}}
            return(reached,edges);
        }
        var (states,transitions)=Closure();
        bool goals=rule.GoalNodeIds.Concat(rule.RequiredMilestones).All(g=>states.Any(s=>s.Node==g));
        bool milestones=true;
        for(int i=0;i<rule.RequiredMilestones.Count;i++)
        {
            var without=Closure(rule.RequiredMilestones[i]).States.Select(s=>s.Node).ToHashSet(StringComparer.Ordinal);
            milestones&=!rule.GoalNodeIds.Any(without.Contains)&&(i+1==rule.RequiredMilestones.Count||!without.Contains(rule.RequiredMilestones[i+1]));
        }
        var safe=states.Where(s=>s.Node==rule.StartNodeId).ToHashSet();bool changed=true;while(changed){changed=false;foreach(var edge in transitions)if(safe.Contains(edge.To))changed|=safe.Add(edge.From);}
        return(goals&&milestones&&(!c.RequireReturnToStart||safe.SetEquals(states)),states.Count);
    }
}
