using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalProgramChoiceTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional program choices: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static MetadataRegionalOwnershipRequest Example(bool larger=false)
    {
        const string id="complete-program";var profile=new WorldSuitabilityProfile("ground","Required game ground","choices:ground",["ground"],[],[]);
        var walk=new WorldTraversalProfile("walk","Every declared journey and return","choices:travel",.2,.5,new Dictionary<string,double>{{"ground",1}},1,1,1);
        var mix=new MetadataProgramMixRule("functions","Provide every required function with a complete program","choices:mix",
            [new("broad",1,MaximumCount:1),new("long",2,MaximumCount:1)],[new("function","units",4)],"budget",2,1);
        WorldMetadataArchetype Host(string name,double width)=>new(name,"facility","Complete functional program",new Dictionary<string,JsonElement>(),
            [new("payload","payload",1,"Whole required payload"),new("storage","storage",1,"Whole required storage")],
            [new("internal","local-travel",MetadataConnectionPattern.Pairs,"payload","storage")],[new("payload",1,1),new("equipment",1,1)],true,
            PhysicalFootprint:new("meters","Original "+name+" body and envelope","choices:"+name,Box(-width/2,-6,width,12),Box(-width/2-2,-8,width+4,16)),
            Access:new(MetadataOccupancyKind.Reservation,[new("east","Shared region entrance","choices:east",new(width/2,0),new(width/2+2,0),1),
                new("west","Shared region entrance","choices:west",new(-width/2,0),new(-width/2-2,0),1)]),
            Capacities:[new("function","units",4,"Four required functions","choices:function")]);
        var root=new WorldMetadataArchetype("root","world","Complete game-authored programs",new Dictionary<string,JsonElement>(),
            [new("hosts","broad",1,"One complete required functional program",ProgramMix:mix)],[],[new("facility",1,1)]);
        var payload=new WorldMetadataArchetype("payload","payload","Complete required payload",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Original18x2payload","choices:payload",Box(-9,-1,18,2)),
            Access:new(MetadataOccupancyKind.Solid,[new("south","Payload entrance","choices:payload-door",new(0,1),new(0,2),1)]));
        var storage=new WorldMetadataArchetype("storage","equipment","Complete required storage",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Original4x2storage","choices:storage",Box(-2,-1,4,2)),
            Access:new(MetadataOccupancyKind.Solid,[new("north","Storage entrance","choices:storage-door",new(0,-1),new(0,-2),1)]));
        var catalog=new WorldMetadataCatalog(1,"complete-program-catalog",[root,Host("broad",16),Host("long",larger?28:24),payload,storage]);
        var request=new WorldPlacementRequest("host","Complete program at derived regional anchor","choices:host",null,
            [new("host","Required complete host","choices:host",null,profile)],[],
            [new("pin","host","regional-origin","Follow qualified region anchor","choices:anchor",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var contents=new WorldPlacementRequest("contents","Keep every original required child","choices:contents",null,
            [new("payload","Complete required payload","choices:payload",null,profile),new("storage","Complete required storage","choices:storage",null,profile)],
            [new("payload-origin",new(0,-2)),new("storage-origin",new(0,3))],[new("payload-pin","payload","payload-origin","Payload position","choices:payload",0,0,0,1),
                new("storage-pin","storage","storage-origin","Storage position","choices:storage",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var programs=new[]{"broad","long"}.Select(name=>new MetadataNestedProgram("contents-"+name,"Every required program child","choices:contents",id,name,0,1,contents,
            [new("payload","payload",true),new("storage","storage",true)])).ToArray();
        var recipe=new WorldMetadataRecipe(1,id,"framework-reference",catalog.Id,"root","local",MetadataCoverage.PartialScope,
            ["Complete declared functions, bodies and local/regional journeys. Broad whole-world design and runtime remain open."],new Dictionary<string,int>(),new(["x","y"],"meters"),RequireAllPositions:true,
            Environment:new(1,"fields",[new("constant","Game-authored neutral field","choices:field","relative",1,[])]),
            Placement:new(request,[new("host",id+"/hosts-0",true)],Programs:programs),RouteRules:[new("internal","local-travel",walk,new(.1,[]),"south","north")],
            ProgramComposition:new("complete-options","Select complete functional programs","choices:options",MaximumAttempts:8));
        var boundary=Box(0,0,240,80);var surface=new WorldSurfaceDefinition(1,"terrain","meters",boundary,[],
            [new("ground","ground","Whole shared game terrain","choices:terrain",boundary,0,new(0,0),0,0,0)]);
        var regions=new[]{"alpha","beta","gamma"}.Select(name=>new MetadataRegionalProgramRegion(name,"Complete "+name+" program","choices:"+name,1,100,0,profile,"regional-origin",recipe,catalog,
            new("Reserve the selected complete program","choices:capacity",.7))).ToArray();
        var routes=Enumerable.Range(0,2).Select(i=>new MetadataRegionalConnection("journey-"+i,"regional-travel","All required regional journeys","choices:travel",
            new(regions[i].Id,id+"/hosts-0","east"),new(regions[i+1].Id,id+"/hosts-0","west"),regions[i].Id,boundary,walk,new(.1,[]),true)).ToArray();
        return new(1,"complete-program-world","Choose whole feasible programs on shared game terrain","choices:world",surface,regions,routes,new(Interior:new(.1,65536)),
            ProgramComposition:new("Qualify complete game-authored program choices","choices:regional"));
    }
    private static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return error;}throw new Exception("Missing choice refusal: "+code);}
    internal static async Task SemanticChoiceAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");
        var original=Example();var regions=original.Regions.Take(2).Select(r=>r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>
            a.Id=="payload"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-5,-1,10,2)}}:a).ToArray()}}).ToArray();
        var first=original.Connections[0];
        var options=new WorldNetworkOptions("Connect chosen generated hosts","choices:semantic","regional",first.Profile,first.Search,
            NearestNeighbors:1,MaximumPairChecks:1,MaximumWork:50000000);
        var network=new MetadataRegionalNetworkRule("selected-hosts","selected-travel",[
            new("alpha","Required long host","choices:alpha",new("alpha","","east",new("facility","long"))),
            new("beta","Other complete host","choices:beta",first.To)],options);
        var source=original with{Regions=regions,Connections=[first],NetworkRules=[network]};
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var definition=planner.Plan(source);var attempts=definition.OwnershipPlan!.CompositionPlan!.Attempts;
        Check(attempts.Count>=2&&attempts[^1].Accepted,"search reaches an alternative satisfying the generated role");
        Check(attempts.Any(a=>!a.Accepted&&a.FailureRegionId=="alpha"&&a.Rejection!.StartsWith("METADATA_REGIONAL_NETWORK_SELECTOR_COUNT:")&&a.Work>0),"missing role records owning region and charged work");
        Check(definition.OwnershipPlan.CompositionPlan.EnumerationWork+attempts.Sum(a=>a.Work)==definition.OwnershipPlan.Work,"failed and successful candidate work is fully accounted");
        var world=engine.Generate(definition);
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==8&&world.Connections.Count==2&&world.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"both complete programs and both directions retained");
        Check(world.Regions.Single(r=>r.RegionId=="alpha").World.Nodes.Any(n=>n.Kind=="facility"&&n.ArchetypeId=="long"),"selected actual content supplies required role");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"role-based choice and world replay");
        var limited=Reject(()=>planner.Plan(source with{ProgramComposition=source.ProgramComposition! with{MaximumAttempts=1}}),"METADATA_REGIONAL_COMPOSITION_ATTEMPT_LIMIT");
        Check(limited.Data["RegionalComposition"] is MetadataRegionalCompositionPlan {Attempts.Count:1} failed&&failed.Attempts[0].Rejection!.StartsWith("METADATA_REGIONAL_NETWORK_SELECTOR_COUNT:"),"attempt limit preserves role failure");
        Reject(()=>planner.Plan(source with{MaximumWork=definition.OwnershipPlan.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        var feedback=planner.Plan(source with{PhysicalFeedback=new("Qualify complete candidates","choices:physical",1)});
        Check(feedback.OwnershipPlan!.CompositionPlan!.Attempts.Any(a=>a.Rejection?.StartsWith("METADATA_REGIONAL_NETWORK_SELECTOR_COUNT:")==true)&&
            feedback.OwnershipPlan.CompositionPlan.Attempts[^1].Accepted,"role failure reaches program search through physical feedback");
        Console.WriteLine("PASS: a missing generated network role rejects a complete regional program, tries an authored alternative, accounts work and replays the complete physical world.");
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        AnalysisControls();
        var input=Example();var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();string before=Json(input);
        var definition=planner.Plan(input);Check(Json(input)==before,"caller preserved");Verify(definition);
        Check(Json(definition)==Json(planner.Plan(input with{Regions=input.Regions.Reverse().ToArray(),Connections=input.Connections.Reverse().ToArray()})),"canonical order");
        foreach(var (name,request) in new[]{("baseline",input),("larger-source",Example(larger:true)),("components",input with{
            PhysicalFeedback=new MetadataRegionalPhysicalFeedbackRule("Qualify every complete selected program and connection","choices:physical")})})
        {
            var plan=name=="baseline"?definition:planner.Plan(request);Verify(plan);var world=engine.Generate(plan);Complete(world,name=="larger-source");
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(request));await engine.PublishAsync(world,Path.Combine(output,name));
            Check(Json(await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json")))==Json(world),"saved native source/selection replay");
        }
        Reject(()=>planner.Plan(input with{ProgramComposition=null}),"METADATA_PROGRAM_ANALYSIS_COUPLED_INPUT_UNSUPPORTED");
        Reject(()=>planner.Plan(input with{ProgramComposition=input.ProgramComposition! with{MaximumAttempts=65}}),"METADATA_REGIONAL_COMPOSITION_RULE_INVALID");
        Reject(()=>planner.Plan(input with{Coupling=new("Count coupling without terrain demand","choices:unresolved")}),"METADATA_REGIONAL_COUPLING_UNUSED");
        var exhausted=Reject(()=>planner.Plan(input with{ProgramComposition=input.ProgramComposition! with{MaximumAttempts=1}}),"METADATA_REGIONAL_COMPOSITION_ATTEMPT_LIMIT");
        Check(exhausted.Data["RegionalComposition"] is MetadataRegionalCompositionPlan{Attempts.Count:1},"first complete failed program retained");
        await File.WriteAllTextAsync(Path.Combine(output,"exhausted.json"),Json((MetadataRegionalCompositionPlan)exhausted.Data["RegionalComposition"]!));
        Reject(()=>planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        Check(planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work}).OwnershipPlan!.Work==definition.OwnershipPlan.Work,"exact aggregate work");
        var region=definition.Regions[0];var recipe=MetadataRegionalWorldGenerator.EffectiveRecipe(definition,region,default);var generator=new WorldMetadataGenerator();
        var analysis=generator.AnalyzeProgram(recipe,region.Catalog);Check(analysis.Nodes.Count==4&&analysis.Nodes.Any(n=>n.ArchetypeId=="long"),"bound analysis retains complete selected program");
        Reject(()=>generator.AnalyzeProgram(recipe with{ProgramComposition=recipe.ProgramComposition! with{RequiredRanks=[1,0]}},region.Catalog),"METADATA_PROGRAM_COMPOSITION_REQUIRED_SELECTION_MISMATCH");
        Reject(()=>generator.AnalyzeProgram(recipe with{ProgramComposition=recipe.ProgramComposition! with{RequiredRanks=[]}},region.Catalog),"METADATA_PROGRAM_COMPOSITION_RULE_INVALID");
        Reject(()=>generator.Generate(recipe with{ProgramComposition=recipe.ProgramComposition! with{RequiredRanks=[0]}},region.Catalog),"METADATA_PROGRAM_COMPOSITION_SELECTED_UNRESOLVED");
        var proof=definition.OwnershipPlan!.CompositionPlan!;
        Reject(()=>engine.Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{CompositionPlan=proof with{EnumerationWork=proof.EnumerationWork+1}}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        using var cancel=new CancellationTokenSource();cancel.Cancel();try{planner.Plan(input,cancel.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: complete ranked program analysis, invariant nested-source rejection, selected-program regional capacity, full physical content and travel, larger original source, hard ranks/budgets/replay/tamper/cancel.");
    }
    private sealed class Probe(Action action):IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"choice-probe";public bool Changed;
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(Changed);
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {action();return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new double[dimensions]).ToArray();}
    }
    private static void AnalysisControls()
    {
        var generator=new WorldMetadataGenerator();var nested=WorldProgramCompositionTests.Example(true);
        var alternatives=generator.AnalyzeProgramChoices(nested.Recipe,nested.Catalog,new(5000000,default),default).Take(4).ToArray();
        Check(alternatives.Select(a=>string.Join(",",a.Ranks)).SequenceEqual(new[]{"0,0,0","0,0,1","0,1,0","0,1,1"}),"nested chooser uses shared ranked backtracking");
        var actual=generator.Generate(nested.Recipe,nested.Catalog);var selected=actual.ProgramCompositionPlan!.Attempts[^1].Selections;
        var ranks=selected.Select(s=>s.Request.ExcludedCounts?.Count??0).ToArray();
        var bound=nested.Recipe with{ProgramComposition=nested.Recipe.ProgramComposition! with{RequiredRanks=ranks}};
        var world=generator.Generate(bound,nested.Catalog);var analysis=generator.AnalyzeProgram(bound,nested.Catalog);
        Check(Json(world.Nodes)==Json(actual.Nodes)&&Json(world.AccessPlan)==Json(actual.AccessPlan)&&Json(world.RoutePlan)==Json(actual.RoutePlan),"explicit selected nested program preserves all physical geometry and journeys");
        Check(Json(analysis.Nodes.Select(n=>n with{Position=null}))==Json(world.Nodes.Select(n=>n with{Position=null})),"selected nested analysis matches full generation");
        Check(generator.AnalyzeProgram(bound,nested.Catalog,analysis.Work).Work==analysis.Work,"bound analysis exact work");
        Reject(()=>generator.AnalyzeProgram(bound,nested.Catalog,analysis.Work-1),"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET");
        var input=Example().Regions[0];var parameters=new Dictionary<string,int>{{"payload-count",1}};
        var recipe=input.Recipe with{Parameters=parameters,ProgramComposition=input.Recipe.ProgramComposition! with{RequiredRanks=[1]}};
        var catalog=input.Catalog with{Archetypes=input.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=a.Parts.Select(p=>p with{
            Layout=new("choice-probe",JsonSerializer.SerializeToElement(new{}))}).ToArray()}:a.Id is "broad" or "long"?a with{
            Parts=a.Parts.Select(p=>p.Id=="payload"?p with{CountParameter="payload-count"}:p).ToArray()}:a).ToArray()};
        var probe=new Probe(()=>parameters["payload-count"]=0);
        Check(new WorldMetadataGenerator(layouts:[probe]).AnalyzeProgram(recipe,catalog).Nodes.Count==4&&parameters["payload-count"]==0,"bound chooser captures caller before layout callback");
        parameters["payload-count"]=1;Probe? changing=null;changing=new Probe(()=>changing!.Changed=true);
        Reject(()=>new WorldMetadataGenerator(layouts:[changing]).AnalyzeProgram(recipe,catalog),"METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        using var cancel=new CancellationTokenSource();var cancelling=new Probe(cancel.Cancel);
        try{new WorldMetadataGenerator(layouts:[cancelling]).AnalyzeProgram(recipe,catalog,token:cancel.Token);throw new Exception("Missing bound callback cancellation");}catch(OperationCanceledException){}
    }
    private static void Verify(MetadataRegionalWorldDefinition definition)
    {
        var p=definition.OwnershipPlan!;var proof=p.CompositionPlan!;Check(proof.Attempts.Count==4,"only owning invariant program changes before complete success");
        Check(proof.Attempts.Take(3).Select(a=>a.FailureRegionId).SequenceEqual(new[]{"alpha","beta","gamma"}),"every actual owning failure retained");
        Check(proof.Attempts.Take(3).All(a=>!a.Accepted&&a.InvariantChoice is not null)&&proof.Attempts[^1].Accepted,"proved source-invariant pruning only");
        Check(proof.EnumerationWork+proof.Attempts.Sum(a=>a.Work)==p.Work,"all enumeration/candidate work accounted");
        Check(definition.Regions.All(r=>r.Recipe.ProgramComposition!.RequiredRanks!.SequenceEqual(new[]{1})),"actual full program selection explicitly bound");
        Check(p.ProgramDemands!.All(d=>d.Analysis.Nodes.Count==4&&d.Analysis.ProgramMixPlan!.Allocations.Single().Choices.Single().ArchetypeId=="long"),"actual selected complete catalog program measured");
    }
    private static void Complete(MetadataRegionalWorldDocument world,bool larger)
    {
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==12&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==9,"all required actual bodies retained");
        Check(world.Connections.Count==2&&world.Connections.All(r=>r.Reverse is not null&&r.Definition.Profile.Radius==.2),"all full-width regional round trips");
        foreach(var packet in world.Regions)
        {
            var w=packet.World;Check(w.RoutePlan!.Routes.Count==1&&w.RoutePlan.Routes.Single().Reverse is not null,"all local round trips");
            Check(w.ProgramCompositionPlan!.Attempts.Count==1&&w.ProgramCompositionPlan.Attempts[0].Prefix.SequenceEqual(new[]{1}),"bound generation cannot silently advance");
            Check(w.ProgramMixPlan!.Allocations.Single().Balances.Single().Required==4&&w.ProgramMixPlan.Allocations.Single().Balances.Single().Provided==4,"all required game functions retained");
            var host=w.Nodes.Single(n=>n.Id.EndsWith("/hosts-0"));var actual=w.AccessPlan!.Sites.Single(s=>s.NodeId==host.Id).Footprint;
            var source=world.Definition.Regions.Single(r=>r.Id==packet.RegionId).Catalog.Archetypes.Single(a=>a.Id==host.ArchetypeId).PhysicalFootprint!.Polygon;
            var binding=w.PlacementPlan!.Definition.Bindings.Single(b=>b.NodeId==host.Id);double angle=binding.RotationDegrees*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
            var expected=source.Vertices.Select(p=>new WorldPoint(host.Position![0]+p.X*c-p.Y*s,host.Position![1]+p.X*s+p.Y*c)).ToArray();
            Check(actual.Vertices.Count==expected.Length&&actual.Vertices.Zip(expected).All(pair=>pair.First.DistanceTo(pair.Second)<1e-7),"every host source vertex survives rigid placement without resizing or clipping");
            Check(Math.Abs(Math.Abs(actual.SignedArea())-(larger?28:24)*12)<1e-7,"actual original selected host dimensions within world geometry tolerance");
            Check(Math.Abs(Math.Abs(w.AccessPlan.Sites.Single(s=>s.NodeId.EndsWith("/payload-0")).Footprint.SignedArea())-36)<1e-7,"original18x2payload never shrunk");
        }
    }
}
