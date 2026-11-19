using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldProgressionSelectorTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Progression selector: "+reason);}
    static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return error;}throw new Exception("Expected "+code);}
    static MetadataRegionalProgressionNode Role(string region,string kind,string? archetype=null)=>new(region,"",new(kind,archetype));
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var template=WorldRegionalNetworkTests.Example();var arrival=template.Regions.Single(r=>r.Id=="arrival");
        const string parent="region-west/compounds-0";
        var linked=arrival.Recipe with{AdditionalConnections=[..arrival.Recipe.AdditionalConnections!,new("entry","program-access",arrival.Recipe.Id,parent,true)]};
        var renamed=JsonSerializer.Deserialize<WorldMetadataRecipe>(Json(linked).Replace(parent,"region-west/sites-0",StringComparison.Ordinal)
            .Replace("region-west","alternate-program",StringComparison.Ordinal),WorldJson.CreateOptions())!;
        var renamedCatalog=arrival.Catalog with{Archetypes=arrival.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{
            Parts=a.Parts.Select(p=>p.Id=="compounds"?p with{Id="sites"}:p).ToArray()}).ToArray()};
        var wrong=arrival.Catalog with{Archetypes=arrival.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Kind="other-root"}:a).ToArray()};
        var network=template.NetworkRules![0];template=template with{NetworkRules=[network with{Destinations=network.Destinations.Select(d=>d with{
            Endpoint=d.Endpoint with{NodeId="",Selector=new("compound","compound")}}).ToArray()}]};
        string rootKind=arrival.Catalog.Archetypes.Single(a=>a.Id=="root").Kind;
        var rule=new MetadataRegionalProgressionRule("roles","Traverse a complete alternative program","test:roles",Role("arrival",rootKind,"root"),
            [Role("outpost","compound","compound")],["program-access","regional-travel"],[Role("grove","compound","compound")]);
        var request=new MetadataRegionalDesignRequest(1,"role-selection","Reusable semantic gameplay roles","test:selection",template,
            [new("arrival",[new("missing-role",0,linked,wrong),new("different-paths",1,renamed,renamedCatalog)])],[],2,
            WorldRequirements:[new("complete-modules","module",8,8,true),new("complete-parents","module",2,2,true,new("compound",4,4,true))],
            WorldProgression:[rule]);
        var engine=new MetadataRegionalDesignGenerator();string before=Json(request);var result=engine.Generate(request);
        Check(Json(request)==before,"caller source is immutable");
        Check(result.Receipt.Attempts.Count==2&&result.Receipt.Attempts[0].Rejection!.StartsWith("METADATA_REGIONAL_PROGRESSION_SELECTOR_COUNT:")&&
            result.Receipt.Attempts[0].Rejection!.EndsWith(":0")&&result.Receipt.Attempts[1].Accepted,"missing role rejects the whole candidate and selects a complete renamed alternative");
        Check(result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
            result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"all source content and physical regional routes remain");
        var planner=new MetadataRegionalProgressionPlanner();var plan=result.Receipt.Attempts[1].WorldProgression!;var path=plan.Results.Single().Paths.Single();
        CheckAggregateBudget(result.World.Regions[0].World.Nodes[0]);
        Check(path.Nodes[0]==new MetadataRegionalProgressionNode("arrival","alternate-program")&&path.Nodes.All(n=>n.Selector is null),"output witnesses retain actual selected IDs, never unresolved selectors");
        Check(plan.Results.Single().Bindings is {Count:3}&&plan.Results.Single().Bindings!.Any(b=>b.Source==rule.Start&&b.Resolved==path.Nodes[0]),"source role-to-node bindings are retained");
        var exact=rule with{Start=path.Nodes[0],Goals=[path.Goal],RequiredMilestones=[new("grove",parent)]};
        var exactPlan=planner.Plan([exact],result.World);
        Check(plan.Work-exactPlan.Work==18,"three unique selector scans charge each five-node region and lookup");
        Check(Json(planner.Plan([rule with{MaximumWork=plan.Work}],result.World))==Json(plan),"exact total work boundary succeeds");
        Reject(()=>planner.Plan([rule with{MaximumWork=plan.Work-1}],result.World),"METADATA_PROGRESSION_WORK_BUDGET");
        var ambiguous=rule with{Start=Role("arrival","module")};
        var ambiguity=Reject(()=>planner.Plan([ambiguous],result.World),"METADATA_REGIONAL_PROGRESSION_SELECTOR_COUNT");
        Check(ambiguity is MetadataRegionalProgressionSelectorException{Matches.Count:2,Reference.Selector.NodeKind:"module"},"ambiguous role retains all actual matches");
        Reject(()=>planner.Plan([rule with{Start=Role("arrival","absent")}],result.World),"METADATA_REGIONAL_PROGRESSION_SELECTOR_COUNT");
        Reject(()=>planner.Plan([rule with{Goals=[new("arrival","alternate-program")],RequiredMilestones=[]}],result.World),"METADATA_REGIONAL_PROGRESSION_SELECTOR_COLLISION");
        Reject(()=>engine.Generate(request with{WorldProgression=[rule with{Start=rule.Start with{NodeId="also-an-id"}}]}),"METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
        Reject(()=>engine.Generate(request with{WorldProgression=[rule with{Start=Role("arrival","invalid kind")}]}),"METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
        var gate=path.Links.Last();var gated=rule with{Capabilities=new(["key"],[],[new(rule.RequiredMilestones[0],["key"])],[new(gate,["key"])],true)};
        var gatedPlan=planner.Plan([gated],result.World);var gatedPath=gatedPlan.Results.Single().Paths.Single();
        Check(gatedPath.CapabilityStates!.Last().Contains("key")&&gatedPlan.Results.Single().Bindings!.Count==3,"milestone/grant reuse one binding with actual capability reachability");
        Reject(()=>planner.Plan([gated with{Capabilities=gated.Capabilities! with{Grants=[new(rule.Goals[0],["key"])]}}],result.World),"METADATA_PROGRESSION_UNREACHABLE");
        Reject(()=>planner.Plan([gated with{Capabilities=gated.Capabilities! with{Grants=[new(rule.RequiredMilestones[0],["key"]),new(Role("grove","compound"),["key"])]}}],result.World),"METADATA_REGIONAL_PROGRESSION_SELECTOR_COLLISION");
        var reordered=rule with{ConnectionKinds=rule.ConnectionKinds.Reverse().ToArray()};
        Check(Json(planner.Plan([reordered],result.World))==Json(plan),"input declaration order cannot change binding/path receipts");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan([rule],result.World,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var exhausted=Reject(()=>engine.Generate(request with{Slots=[request.Slots[0] with{Options=[request.Slots[0].Options[0]]}]}),"METADATA_REGIONAL_DESIGN_ALTERNATIVES_EXHAUSTED");
        Check(exhausted.Data["RegionalDesignAttempts"] is MetadataRegionalDesignAttempt[]{Length:1},"unresolved role failure retains candidate evidence");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));await File.WriteAllTextAsync(Path.Combine(output,"gated-rule.json"),Json(gated));
        await File.WriteAllTextAsync(Path.Combine(output,"gated-plan.json"),Json(gatedPlan));await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"saved source, alternatives, bindings, paths and complete world replay");
        var first=plan.Results.Single();var forged=plan with{Results=[first with{Bindings=[..first.Bindings!.Skip(1)]}]};
        var bad=result.Receipt with{Attempts=result.Receipt.Attempts.Select(a=>a.Accepted?a with{WorldProgression=forged}:a).ToArray()};
        Reject(()=>engine.Verify(bad,result.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(output,"checks.json"),Json(new{Nodes=20,PhysicalSources=12,RegionalRoutes=3,Bindings=3,SelectorWork=18,
            MissingAndAmbiguousRefused=true,CollisionsRefused=true,ExactWorkVerified=true,CapabilitiesVerified=true,ReplayVerified=true}));
        Console.WriteLine("PASS: semantic progression selects complete renamed programs; counts, physical routes, actual bindings, capability gates, ambiguity/collisions, exact budgets, cancellation and saved replay retained.");
    }

    static void CheckAggregateBudget(WorldMetadataNode prototype)
    {
        var nodes=new[]{prototype with{Id="root",ParentId=null},prototype with{Id="goal",ParentId="root"}};
        var links=new[]{new WorldMetadataConnection("edge","walk","root","goal",true,"test:work",new Dictionary<string,JsonElement>())};
        var rules=Enumerable.Range(0,5).Select(i=>new MetadataProgressionRule("rule-"+i,"Budget aggregation","test:work","root","root",["goal"],["walk"],[],10000000)).ToArray();
        var prior=rules.ToDictionary(r=>r.Id,_=>9000000L);var planner=new MetadataProgressionPlanner();
        var direct=planner.Plan(rules,nodes,links);var charged=planner.PlanWithPriorWork(rules,nodes,links,prior,default);
        Check(charged.Work==direct.Work+45000000&&charged.Results.All(r=>r.Work>9000000),"all already-executed rule projections are included exactly once");
        var sixth=rules.Append(rules[0] with{Id="rule-5"}).ToArray();prior.Add("rule-5",9000000);
        Reject(()=>planner.PlanWithPriorWork(sixth,nodes,links,prior,default),"METADATA_PROGRESSION_WORK_BUDGET");
        Reject(()=>planner.PlanWithPriorWork(rules,nodes,links,new Dictionary<string,long>{{"rule-0",10000001}},default),"METADATA_PROGRESSION_WORK_BUDGET");
    }

    internal static async Task DomainsAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDomainWorldDefinition>("examples/metadata/regional-domain-world/floors-definition.json");
        var template=original.RegionalDesign!.Template;var outpost=template.Regions.Single(r=>r.Id=="outpost");
        const string parent="region-west/compounds-0";
        var linked=outpost.Recipe with{AdditionalConnections=[..outpost.Recipe.AdditionalConnections!,new("entry","program-access",outpost.Recipe.Id,parent,true)]};
        var wrong=outpost.Catalog with{Archetypes=outpost.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Kind="other-root"}:a).ToArray()};
        var design=new MetadataRegionalDesignRequest(1,"domain-roles","Select complete domain gameplay roles","test:domain-roles",template,
            [new("outpost",[new("missing-role",0,linked,wrong),new("complete-role",1,linked,outpost.Catalog)])],
            [new("keep-parts","outpost","module",2,2,true)],2);
        string rootKind=outpost.Catalog.Archetypes.Single(a=>a.Id=="root").Kind;
        var start=new MetadataDomainProgressionNode("outpost","",new(rootKind,"root"));
        var key=new MetadataDomainProgressionNode("lower","generated-interior/rooms-0");
        var goal=new MetadataDomainProgressionNode("upper","generated-interior/rooms-2");
        var gate=new MetadataDomainProgressionLink(null,"stairs");
        var rule=new MetadataDomainProgressionRule("domain-roles","Reach a floor through declared capability gates","test:domain-progression",start,[goal],
            ["program-access","regional-travel","doorway","door","stairs"],[key],Capabilities:new(["key"],[],[new(key,["key"])],[new(gate,["key"])],true));
        var input=original with{RegionalWorld=null,RegionalDesign=design,Progression=[rule]};
        var engine=new MetadataRegionalDomainWorldGenerator();var world=engine.Generate(input);var result=world.Domains.Progression!.Results.Single();
        Check(world.Selection!.Attempts.Count==2&&world.Selection.Attempts[0].Rejection!.StartsWith("METADATA_REGIONAL_PROGRESSION_SELECTOR_COUNT:")&&
            world.Selection.Attempts[1].Accepted,"domain selector failure rejects a complete candidate and selects the next declared program");
        Check(result.Bindings is {Count:1}&&result.Bindings[0].Source==start&&result.Bindings[0].Resolved==new MetadataDomainProgressionNode("outpost",outpost.Recipe.Id),"domain projection retains semantic source and exact bound node");
        var path=result.Paths.Single();Check(path.Nodes[0]==result.Bindings![0].Resolved&&path.Nodes.All(n=>n.Selector is null),"domain paths contain only exact identities");
        int keyIndex=path.Nodes.ToList().IndexOf(key),gateIndex=path.Links.ToList().IndexOf(gate);
        Check(keyIndex>=0&&gateIndex>keyIndex&&path.CapabilityStates![gateIndex].Contains("key"),"key is acquired before the gated floor transition");
        Check(world.RegionalWorld.Regions.Count==4&&world.RegionalWorld.Regions.Sum(r=>r.World.Nodes.Count)==20&&world.Domains.Domains.Count==6&&
            world.Domains.Journeys!.Single().Forward.Any(s=>s.SourceId=="stairs")&&world.Domains.Journeys!.Single().Return is {Count:>0},"complete region/floor content and separately qualified physical return journey remain");
        var planner=new MetadataDomainProgressionPlanner();var domains=world.Domains.Domains;var transitions=world.Domains.Transitions;
        Check(Json(planner.Plan([rule],domains,transitions,default))==Json(world.Domains.Progression),"shared projection equals the composed plan");
        var exact=rule with{Start=result.Bindings[0].Resolved};var exactPlan=planner.Plan([exact],domains,transitions,default);
        Check(world.Domains.Progression.Work-exactPlan.Work==6,"domain selector resolution uses the same bounded kernel ledger");
        Reject(()=>planner.Plan([rule with{Start=new("outpost","",new("module"))}],domains,transitions,default),"METADATA_REGIONAL_PROGRESSION_SELECTOR_COUNT");
        Reject(()=>planner.Plan([rule with{Capabilities=rule.Capabilities! with{Grants=[]}}],domains,transitions,default),"METADATA_PROGRESSION_UNREACHABLE");
        var oneWay=transitions.Select(t=>t.Definition.Id=="stairs"?t with{Definition=t.Definition with{Bidirectional=false}}:t).ToArray();
        Reject(()=>planner.Plan([rule],domains,oneWay,default),"METADATA_PROGRESSION_RETURN_UNREACHABLE");
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(input));await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world/world.json")))==Json(world),"complete selected domains, source bindings, gates and physical journeys replay");
        var corrupted=world with{Domains=world.Domains with{Progression=world.Domains.Progression with{Results=[result with{Bindings=[]}]}}};
        Reject(()=>engine.Validate(corrupted),"METADATA_REGIONAL_DOMAIN_DOCUMENT_MISMATCH");
        Console.WriteLine("PASS: shared semantic progression across four regions/two physical floors, candidate rejection, key/stairs/return proof, bindings, exact work and whole-source replay.");
    }
}
