using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Geometry;
using WorldCreator.Catalogs.Modern;

internal static class WorldProgramMixTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int beds=8)
    {
        var e=WorldProgramInstantiationTests.Example();var request=Request(beds);var source=e.Catalog.Archetypes.Single(a=>a.Id=="base");
        var names=new Dictionary<string,string>{{"compact","base"},{"large","large-base"},{"work","work-base"},{"power","power-base"}};
        var rule=request.Rule with{Offers=request.Rule.Offers.Select(o=>o with{ArchetypeId=names[o.ArchetypeId]}).ToArray()};
        var types=e.Catalog.Archetypes.Where(a=>a.Id!="base").Select(a=>a.Id=="world"?a with{
            Parts=[a.Parts[0] with{Count=1,CountParameter=null,ProgramMix=rule}],
            Connections=[new("regional-chain","regional-path",MetadataConnectionPattern.Chain,"bases")]}:a).ToList();
        foreach(var supply in request.Supplies)
        {
            var type=source with{Id=names[supply.ArchetypeId],Kind=supply.Kind,Capacities=supply.Provides,ProgramInputs=supply.Inputs};
            if(supply.ArchetypeId=="large")type=type with{
                PhysicalFootprint=source.PhysicalFootprint! with{Polygon=new WorldPolygon([new(-9,-6),new(9,-6),new(9,6),new(-9,6)])},
                Access=source.Access! with{Ports=[source.Access.Ports[0] with{BoundaryPosition=new(0,-6),ApproachPosition=new(0,-7)}]},
                Parts=source.Parts.Select(p=>p.Id=="hut"?p with{Count=2}:p).ToArray(),
                Connections=source.Connections.Select(c=>c.Kind=="local-access"?c with{Pattern=MetadataConnectionPattern.AllPairs}:c).ToArray()};
            types.Add(type);
        }
        var placement=e.Recipe.Placement!;var template=placement.Programs![0];
        var programs=names.Values.Select(id=>template with{Id="contents-"+id,ParentArchetypeId=id,MinimumParents=0}).Append(placement.Programs[1] with{MinimumParents=0,MaximumParents=128}).ToArray();
        return(e.Recipe with{TopologyRules=null,Parameters=new Dictionary<string,int>{{"beds",beds},{"jobs",4},{"cache-count",1}},
            Placement=placement with{Programs=programs}},e.Catalog with{Archetypes=types});
    }
    public static async Task Physical()
    {
        var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);
        Verify(world,e.Catalog,17,11,12);var larger=Example(16);var expanded=generator.Generate(larger.Recipe,larger.Catalog);Verify(expanded,larger.Catalog,23,18,18);
        Check(world.ProgramMixPlan!.Allocations.Single().Choices.Count(c=>c.ArchetypeId=="base")==2&&expanded.ProgramMixPlan!.Allocations.Single().Choices.Count(c=>c.ArchetypeId=="large-base")==2,"intent changes selected whole programs");
        var reordered=generator.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Reverse().ToArray()});
        Check(Json(world.Nodes)==Json(reordered.Nodes)&&Json(world.ProgramMixPlan)==Json(reordered.ProgramMixPlan)&&Json(world.RoutePlan)==Json(reordered.RoutePlan),"catalog order preserves selected members, geometry and routes");
        var planner=new MetadataProgramMixPlanner();var plan=world.ProgramMixPlan!;var allocation=plan.Allocations[0];
        Reject(()=>planner.ValidateDocument(world with{ProgramMixPlan=null}),"METADATA_PROGRAM_MIX_PLAN_REQUIRED");
        Reject(()=>planner.ValidateDocument(world with{ProgramMixPlan=plan with{Allocations=[allocation with{Cost=1}]}}),"METADATA_PROGRAM_MIX_PLAN_MISMATCH");
        Reject(()=>planner.ValidateDocument(world with{Nodes=world.Nodes.Where(n=>n.Id!=allocation.Choices[0].NodeId).ToArray()}),"METADATA_PROGRAM_MIX_MEMBERSHIP_INVALID");
        Reject(()=>planner.ValidateDocument(world with{Nodes=world.Nodes.Select(n=>n.Id==allocation.Choices[0].NodeId?n with{ProgramMixKey=null}:n).ToArray()}),"METADATA_PROGRAM_MIX_MEMBERSHIP_INVALID");
        Reject(()=>generator.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="world"?a with{Parts=[a.Parts[0] with{CountParameter="beds"}]}:a).ToArray()}),"METADATA_PROGRAM_MIX_COUNT_CONFLICT");
        Reject(()=>generator.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="power-base"?a with{Parts=[new("loop","world",1,"Invalid cycle")],Connections=[]}:a).ToArray()}),"METADATA_ARCHETYPE_CYCLE_OR_DEPTH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("more-capacity","More capacity from complete programs","test:mix-edit",before.World.RecipeSha256,before.World.CatalogSha256,
            larger.Recipe,larger.Catalog));
        Verify(after.World,e.Catalog,23,18,18);
        Check(!after.Report.Stages.Single(s=>s.Id=="content").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused,"intent edits invalidate content and geometry");
        try{session.Apply(new("impossible","Refuse impossible demand","test:mix-edit",after.World.RecipeSha256,after.World.CatalogSha256,
            larger.Recipe with{Parameters=new Dictionary<string,int>{{"beds",1000},{"jobs",4},{"cache-count",1}}},larger.Catalog));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_PROGRAM_MIX_UNSATISFIED"){}
        Check(Json(after)==Json(session.Snapshot),"failed intent edit rolls back complete snapshot");
        Registered();
        string dir=Path.Combine(Path.GetTempPath(),"program-mix-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
        Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output/world-metadata.json"))),"file replay");
        await session.PublishAsync(Path.Combine(dir,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(dir,"session/generation-session.json"));
        Check(Json(after)==Json(restored.Snapshot),"source history checkpoint replay");
        Console.WriteLine("PASS: complete intent variants 17/23 nodes, 16/22 physical members, 11/18 routes; source footprints, hierarchy, saved witnesses, edits and rollback. Files: "+dir);
    }
    private static void Registered()
    {
        var e=Example();var pack=TestCatalogs.Registry.Resolve(ModernCatalog.Selection);var building=pack.Structures.Single(b=>b.Id=="warehouse");var variant=building.SizeVariants!.Single(v=>v.Id=="small");
        var rule=new MetadataProgramMixRule("storage-needs","Selected variant owns available storage","test:mix-registry",
            [new("warehouse",1,MaximumCount:4,StructureCapacityId:"storage")],[new("storage",building.CapacityUnit,variant.ServiceCapacity+1)],"budget",10);
        var root=e.Catalog.Archetypes.Single(a=>a.Id=="world") with{Parts=[new("stores","warehouse",1,"Storage program",ProgramMix:rule)],Connections=[]};
        var type=e.Catalog.Archetypes.Single(a=>a.Id=="hut") with{Id="warehouse",PhysicalFootprint=null,Access=null,Structure=new(ModernCatalog.Selection,"warehouse","small"),Parts=[]};
        var catalog=e.Catalog with{Archetypes=[root,type]};var recipe=e.Recipe with{Placement=null,Surface=null,Environment=null,RouteRules=null,Origin=null,RequireAllPositions=false,Parameters=new Dictionary<string,int>()};
        var world=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(recipe,catalog);var allocation=world.ProgramMixPlan!.Allocations.Single();
        Check(allocation.Choices.Count==2&&allocation.Request.Supplies.Single().Provides.Single().Quantity==variant.ServiceCapacity,"registry variant owns capacity");
        var supply=allocation.Request.Supplies.Single();Reject(()=>new MetadataProgramMixPlanner().Solve(allocation.Request with{Supplies=[supply with{Provides=[supply.Provides[0] with{Quantity=1}]}]},world.StructureDefinitions),"METADATA_PROGRAM_MIX_STRUCTURE_SOURCE_INVALID");
    }
    public static async Task WriteExamples(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(int beds in new[]{8,16}){var e=Example(beds);string dir=Path.Combine(directory,"beds-"+beds);Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));}
    }
    private static void Verify(WorldMetadataDocument world,WorldMetadataCatalog catalog,int nodes,int routes,decimal cost)
    {
        Check(world.Nodes.Count==nodes&&world.AccessPlan!.Sites.Count==nodes-1&&world.RoutePlan!.Routes.Count==routes,"all required physical constituents and routes survive");
        Check(world.ProgramMixPlan!.Allocations.Single().Cost==cost,"minimum program cost retained");
        WorldConnectedGrowthTests.Independent(world,catalog);WorldMetadataGenerator.ValidateDocument(world);
    }
    private static MetadataCatalogCapacity Fact(string id,double q)=>new(id,"units",q,"Authored program fact","test:mix");
    internal static MetadataProgramMixRequest Request(int beds=8,int jobs=4)
    {
        var rule=new MetadataProgramMixRule("program-needs","Meet declared needs with complete programs","test:mix",
            [new("compact",3,MaximumCount:4),new("large",5,MaximumCount:4),new("work",4,MaximumCount:4),new("power",2,MaximumCount:4)],
            [new("beds","units",0,"beds"),new("jobs","units",0,"jobs")],"budget",100,16);
        return new("world","root","programs","compact",1,rule,
            [new("compact","housing",[Fact("beds",4)],[Fact("power",1)]),new("large","housing",[Fact("beds",8)],[Fact("power",3)]),
             new("work","workplace",[Fact("jobs",4)],[Fact("power",2)]),new("power","service",[Fact("power",4)],[])],
            new Dictionary<string,int>{{"beds",beds},{"jobs",jobs}});
    }
    // Exhaustive count-vector oracle: no suffix bounds, pruning, or planner helpers.
    private static (decimal Cost,int[] Counts)? Oracle(MetadataProgramMixRequest r)
    {
        var offers=r.Rule.Offers.OrderBy(o=>o.ArchetypeId,StringComparer.Ordinal).ToArray();
        var supplies=offers.Select(o=>r.Supplies.Single(s=>s.ArchetypeId==o.ArchetypeId)).ToArray();
        var ids=r.Rule.Demands.Select(d=>d.CapacityId).Concat(supplies.SelectMany(s=>s.Provides.Concat(s.Inputs)).Select(c=>c.Id)).Distinct().ToArray();
        int[] counts=new int[offers.Length];(decimal Cost,int[] Counts)? best=null;
        void Visit(int i)
        {
            if(i<offers.Length){for(int c=offers[i].MinimumCount;c<=offers[i].MaximumCount;c++){counts[i]=c;Visit(i+1);}return;}
            int total=counts.Sum();decimal cost=offers.Select((o,k)=>o.Cost*counts[k]).Sum();
            if(total<r.MinimumCount||total>r.Rule.MaximumCount||cost>r.Rule.MaximumCost)return;
            foreach(string id in ids)
            {
                decimal net=0;for(int k=0;k<counts.Length;k++)net+=counts[k]*(supplies[k].Provides.Where(c=>c.Id==id).Sum(c=>(decimal)c.Quantity)-supplies[k].Inputs.Where(c=>c.Id==id).Sum(c=>(decimal)c.Quantity));
                var d=r.Rule.Demands.SingleOrDefault(d=>d.CapacityId==id);decimal required=d is null?0:d.QuantityParameter is {} p?r.Parameters[p]:(decimal)d.Quantity;
                if(net<required)return;
            }
            if(best is null||cost<best.Value.Cost||cost==best.Value.Cost&&total<best.Value.Counts.Sum())best=(cost,counts.ToArray());
        }
        Visit(0);return best;
    }
    public static void Solver()
    {
        var planner=new MetadataProgramMixPlanner();int cases=0;
        for(int beds=0;beds<=24;beds+=2)for(int jobs=0;jobs<=12;jobs+=2)
        {
            if(beds+jobs==0)continue;var r=Request(beds,jobs);var expected=Oracle(r);
            if(expected is null)Reject(()=>planner.Solve(r),"METADATA_PROGRAM_MIX_UNSATISFIED");
            else
            {
                var actual=planner.Solve(r);var order=r.Rule.Offers.OrderBy(o=>o.ArchetypeId,StringComparer.Ordinal).ToArray();
                Check(actual.Cost==expected.Value.Cost&&order.Select(o=>actual.Choices.Count(c=>c.ArchetypeId==o.ArchetypeId)).SequenceEqual(expected.Value.Counts),"independent exhaustive optimum "+beds+"/"+jobs);
            }
            cases++;
        }
        var request=Request();var plan=planner.Solve(request);
        Check(plan.Cost==12&&plan.Choices.Count==4&&plan.Choices.Count(c=>c.ArchetypeId=="compact")==2,"prerequisite power defeats cheapest-per-bed greedy choice");
        Check(planner.Solve(Request(16)).Cost==18,"different intent selects larger programs");
        Check(Json(plan)==Json(planner.Solve(request with{Supplies=request.Supplies.Reverse().ToArray(),Rule=request.Rule with{Offers=request.Rule.Offers.Reverse().ToArray(),Demands=request.Rule.Demands.Reverse().ToArray()}})),"canonical declaration ordering");
        var fractional=request with{BaseArchetypeId="fraction",MinimumCount=0,Parameters=new Dictionary<string,int>(),Rule=request.Rule with{Offers=[new("fraction",.1m,MaximumCount:5)],Demands=[new("need","units",.3)]},Supplies=[new("fraction","service",[Fact("need",.1)],[])]};
        var fractions=planner.Solve(fractional);Check(fractions.Cost==.3m&&fractions.Choices.Count==3,"exact decimal quantities and costs");
        var tied=fractional with{Rule=fractional.Rule with{Offers=[new("fraction",.1m,MaximumCount:5),new("other",.1m,MaximumCount:5)]},Supplies=[fractional.Supplies[0],new("other","service",[Fact("need",.1)],[])]};
        Check(planner.Solve(tied).Choices.All(c=>c.ArchetypeId=="other"),"canonical lexicographic count tie");
        var mandatory=request with{MinimumCount=6,Rule=request.Rule with{Offers=request.Rule.Offers.Select(o=>o.ArchetypeId=="large"?o with{MinimumCount=1}:o).ToArray()}};
        var mandatoryOracle=Oracle(mandatory)!.Value;Check(planner.Solve(mandatory).Cost==mandatoryOracle.Cost&&planner.Solve(mandatory).Choices.Count>=6,"offer and part minima retained");
        Reject(()=>planner.Solve(request with{Rule=request.Rule with{MaximumCost=11}}),"METADATA_PROGRAM_MIX_UNSATISFIED");
        Check(planner.Solve(request with{Rule=request.Rule with{MaximumSearchStates=(int)plan.SearchStates,MaximumWork=plan.Work}}).Cost==12,"exact budgets accepted");
        Reject(()=>planner.Solve(request with{Rule=request.Rule with{MaximumSearchStates=(int)plan.SearchStates-1}}),"METADATA_PROGRAM_MIX_STATE_BUDGET");
        Reject(()=>planner.Solve(request with{Rule=request.Rule with{MaximumWork=plan.Work-1}}),"METADATA_PROGRAM_MIX_WORK_BUDGET");
        Reject(()=>planner.Solve(Request(0,0)),"METADATA_PROGRAM_MIX_EMPTY_INTENT");
        Reject(()=>planner.Solve(request with{Parameters=new Dictionary<string,int>{{"beds",8}}}),"METADATA_PROGRAM_MIX_PARAMETERS_INVALID");
        Reject(()=>planner.Solve(request with{Supplies=request.Supplies.Skip(1).ToArray()}),"METADATA_PROGRAM_MIX_SUPPLY_INVALID");
        Reject(()=>planner.Solve(request with{Supplies=request.Supplies.Select(s=>s.ArchetypeId=="compact"?s with{Provides=[Fact("beds",4) with{Unit="people"}]}:s).ToArray()}),"METADATA_PROGRAM_MIX_UNIT_MISMATCH");
        Reject(()=>planner.Solve(fractional with{Supplies=[fractional.Supplies[0] with{Provides=[Fact("need",double.NaN)]}]}),"METADATA_CAPACITY_DEFINITION_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Solve(request,token:cancel.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        Console.WriteLine($"PASS: {cases} independent exhaustive program-mix optima, dependency trap, decimals, minima, canonical ties, exact budgets, invalid facts and cancellation.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
}
