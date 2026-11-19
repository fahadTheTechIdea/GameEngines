using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Catalogs.Modern;

internal static class WorldVariationTests
{
    private static WorldPolygon Box(double w,double h)=>new([new(-w/2,-h/2),new(w/2,-h/2),new(w/2,h/2),new(-w/2,h/2)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldProtectionTests.Example();
        WorldMetadataCatalog Vary(WorldMetadataCatalog catalog)
        {
            var marker=catalog.Archetypes.Single(a=>a.Id=="marker") with{
                Parts=[new("attachment","badge",1,"Required marker identity",Layout:MetadataLineLayout.Parameters([0d,0d],[0d,0d]),
                    Variation:new("badge-variants","badge-01","Choose the required badge form","catalog:badge",["badge","badge-round"]))],
                ChildRequirements=[new("component",1,1)]};
            var badge=new WorldMetadataArchetype("badge","component","Required marker identity",new Dictionary<string,JsonElement>(),[],[],[]);
            WorldMetadataArchetype Shape(string id,double w,double h)=>marker with{Id=id,PhysicalFootprint=new("meters","Exact "+id,"catalog:"+id,Box(w,h),Box(w+.2,h+.2))};
            return catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="marker"?marker:a.Id=="root"?a with{
                Parts=a.Parts.Select(p=>p.Id is "markers" or "boundary"?p with{Variation=new(p.Id=="markers"?"detail-variants":"border-variants",
                    p.Id=="markers"?"detail-01":"border-01","Choose a permitted marker form","catalog:marker",["marker","marker-wide","marker-tall"])}:p).ToArray()}:a)
                .Concat([Shape("marker-wide",1.4,.8),Shape("marker-tall",.8,1.4),badge,badge with{Id="badge-round",Properties=new Dictionary<string,JsonElement>{{"form",JsonSerializer.SerializeToElement("round")}}}]).ToArray()};
        }
        return(e.Recipe with{PopulationSource=e.Recipe.PopulationSource! with{Catalog=Vary(e.Recipe.PopulationSource.Catalog)},
            RemainingScopes=["Complete declared varied program, exact footprints, nested badges and protected gameplay spaces. Broad expressive range, scale, art and native Godot remain open."]},Vary(e.Catalog));
    }
    private static WorldMetadataCatalog ChangeRule(WorldMetadataCatalog catalog,string part,Func<MetadataVariationRule,MetadataVariationRule> change)=>catalog with{
        Archetypes=catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=a.Parts.Select(p=>p.Id==part?p with{Variation=change(p.Variation!)}:p).ToArray()}:a).ToArray()};
    internal static WorldMetadataCatalog ChangedSeed(WorldMetadataCatalog catalog)=>ChangeRule(catalog,"markers",r=>r with{Seed="detail-02"});
    internal static WorldMetadataCatalog Oversized(WorldMetadataCatalog catalog)=>catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="marker-wide"?a with{
        PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(60,60),Reservation=Box(61,61)}}:a).ToArray()};
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"changed-seed.json"),Json(ChangedSeed(e.Catalog)));
        await File.WriteAllTextAsync(Path.Combine(directory,"oversized.json"),Json(Oversized(e.Catalog)));
    }
    public static async Task RunAsync()
    {
        RegisteredCase();
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var sample=world.VariationPlan!.Allocations.First(a=>a.Request.PartId=="markers").Request;
        var ids=sample.Rule.ArchetypeIds.Order(StringComparer.Ordinal).ToArray();
        var equal=sample with{Rule=sample.Rule with{Weights=ids.ToDictionary(id=>id,_=>1,StringComparer.Ordinal)}};
        var weighted=sample with{Rule=sample.Rule with{Weights=ids.ToDictionary(id=>id,id=>id==ids[0]?12:1,StringComparer.Ordinal)}};
        int equalCount=0,weightedCount=0;
        for(int i=0;i<1000;i++)
        {
            var baseline=MetadataVariationPlanner.Select(sample,i,default);Check(Json(baseline)==Json(MetadataVariationPlanner.Select(equal,i,default)),"unit weights preserve exact unweighted selection");
            var choice=MetadataVariationPlanner.Select(weighted,i,default);
            var reversed=weighted with{Rule=weighted.Rule with{ArchetypeIds=ids.Reverse().ToArray()}};
            Check(Json(choice)==Json(MetadataVariationPlanner.Select(reversed,i,default)),"weighted selection is independent of candidate order");
            if(baseline.ArchetypeId==ids[0])equalCount++;if(choice.ArchetypeId==ids[0])weightedCount++;
        }
        Check(weightedCount>equalCount+300&&weightedCount>750,"declared weight affects deterministic population distribution");
        var weightedCatalog=ChangeRule(e.Catalog,"markers",r=>r with{Weights=r.ArchetypeIds.ToDictionary(id=>id,id=>id==ids[0]?12:1,StringComparer.Ordinal)});
        var weightedWorld=generator.Generate(e.Recipe,weightedCatalog);WorldMetadataGenerator.ValidateDocument(weightedWorld);
        Check(weightedWorld.Nodes.Count==world.Nodes.Count&&weightedWorld.AccessPlan!.Sites.Count==world.AccessPlan!.Sites.Count,"weighted choices retain every required member and physical source");
        Reject(()=>generator.Generate(e.Recipe,ChangeRule(e.Catalog,"markers",r=>r with{Weights=new Dictionary<string,int>{{ids[0],0}}})),"METADATA_VARIATION_WEIGHTS_INVALID");
        Reject(()=>generator.Generate(e.Recipe,ChangeRule(e.Catalog,"markers",r=>r with{Weights=r.ArchetypeIds.ToDictionary(id=>id,_=>17,StringComparer.Ordinal)})),"METADATA_VARIATION_WEIGHTS_INVALID");
        var heavy=sample with{Count=100000,Rule=sample.Rule with{Weights=ids.ToDictionary(id=>id,_=>16,StringComparer.Ordinal)}};
        Reject(()=>new MetadataVariationPlanner().Plan([heavy],world.Nodes),"METADATA_VARIATION_WORK_BUDGET");
        var source=world.PlacementPlan!.Definition.SourceWorld!;var plan=world.VariationPlan!;
        Check(world.Nodes.Count==20&&source.Nodes.Count==12&&world.AccessPlan!.Sites.Count==11,"all physical members and nested required badges retained");
        Check(plan.Allocations.Count==10&&plan.Allocations.Sum(a=>a.Choices.Count)==16,"root and nested variation receipts");
        var expected=new[]{"marker-wide","marker-tall","marker","marker-tall"};
        for(int i=0;i<4;i++)Check(world.Nodes.Single(n=>n.Id=="population/markers-"+i).ArchetypeId==expected[i],"independent expected SHA-256 ranking "+i);
        foreach(var node in world.Nodes.Where(n=>n.ArchetypeId.StartsWith("marker",StringComparison.Ordinal)))
        {
            Check(world.Nodes.Count(n=>n.ParentId==node.Id&&n.Kind=="component")==1,"variant retains complete nested program");
            var binding=world.PlacementPlan.Definition.Bindings.Single(b=>b.NodeId==node.Id);
            var site=world.PlacementPlan.Placement.Sites.Single(s=>s.DemandId==binding.DemandId);
            var sourceShape=e.Catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId).PhysicalFootprint!;
            var translated=sourceShape.Polygon.Vertices.Select(p=>p.Offset(node.Position![0],node.Position[1])).ToArray();
            Check(Json(site.Footprint.Footprint.Vertices)==Json(translated),"selected exact source size reaches physical placement");
        }
        foreach(var n in source.Nodes)Check(Json(n)==Json(world.Nodes.Single(m=>m.Id==n.Id)),"source instance unchanged");
        foreach(var a in source.VariationPlan!.Allocations)Check(Json(a)==Json(plan.Allocations.Single(b=>b.Key==a.Key)),"source rule and choice unchanged");
        WorldMetadataGenerator.ValidateDocument(world);
        var altered=generator.Generate(e.Recipe,ChangedSeed(e.Catalog));
        Check(Json(altered.Nodes.Where(n=>!n.Id.StartsWith("population/markers-",StringComparison.Ordinal)))==Json(world.Nodes.Where(n=>!n.Id.StartsWith("population/markers-",StringComparison.Ordinal))),"part seed edits leave every unrelated node identical");
        Check(Json(altered.Nodes.Where(n=>n.Id.StartsWith("population/markers-",StringComparison.Ordinal)))!=Json(world.Nodes.Where(n=>n.Id.StartsWith("population/markers-",StringComparison.Ordinal))),"new seed changes eligible forms");
        Check(Json(altered.ContextPlan!.Protections)==Json(world.ContextPlan!.Protections),"variation cannot move protected spaces");
        var reordered=e.Catalog with{Archetypes=e.Catalog.Archetypes.Reverse().Select(a=>a with{Parts=a.Parts.Reverse().Select(p=>p.Variation is {} v?p with{Variation=v with{ArchetypeIds=v.ArchetypeIds.Reverse().ToArray()}}:p).ToArray()}).ToArray()};
        var stable=generator.Generate(e.Recipe,reordered);
        Check(Json(stable.Nodes)==Json(world.Nodes)&&Json(stable.VariationPlan)==Json(plan),"catalog, parts and candidate order do not reshuffle choices");
        var unrelated=e.Catalog with{Archetypes=e.Catalog.Archetypes.Append(new("unused","optional","Unrelated catalog addition",new Dictionary<string,JsonElement>(),[],[],[])).ToArray()};
        Check(Json(generator.Generate(e.Recipe,unrelated).Nodes)==Json(world.Nodes),"unrelated catalog additions do not reshuffle content");
        var more=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{
            Parts=a.Parts.Select(p=>p.Id=="markers"?p with{Count=5}:p).ToArray(),ChildRequirements=a.ChildRequirements.Select(r=>r.Kind=="marker"?r with{Minimum=9,Maximum=9}:r).ToArray()}:a).ToArray()};
        var moreRecipe=e.Recipe with{Context=e.Recipe.Context! with{Sites=e.Recipe.Context.Sites.Append(new("population/markers-4","play-space",["obstacle"])).ToArray()}};
        var grown=generator.Generate(moreRecipe,more);
        Check(grown.Nodes.Count==22&&world.Nodes.All(n=>Json(n)==Json(grown.Nodes.Single(m=>m.Id==n.Id))),"count growth appends identities without reshuffling existing members");
        Reject(()=>generator.Generate(e.Recipe,Oversized(e.Catalog)),"METADATA_PLACEMENT_UNRESOLVED");
        Reject(()=>generator.Generate(e.Recipe,ChangeRule(e.Catalog,"markers",r=>r with{ArchetypeIds=["marker","missing"]})),"METADATA_VARIATION_CANDIDATE_INVALID");
        Reject(()=>generator.Generate(e.Recipe,ChangeRule(e.Catalog,"markers",r=>r with{ArchetypeIds=["marker","gate"]})),"METADATA_VARIATION_CANDIDATE_INVALID");
        Reject(()=>generator.Generate(e.Recipe,ChangeRule(e.Catalog,"markers",r=>r with{ArchetypeIds=["marker","marker"]})),"METADATA_VARIATION_RULE_INVALID");
        Reject(()=>generator.Generate(e.Recipe,ChangeRule(e.Catalog,"markers",r=>r with{Seed=""})),"METADATA_VARIATION_RULE_INVALID");
        Reject(()=>generator.Generate(e.Recipe,ChangeRule(e.Catalog,"boundary",r=>r with{Purpose="Changed source obligation"})),"METADATA_POPULATION_BASE_VARIATION_CHANGED");
        var cyclic=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="marker-wide"?a with{Parts=[new("recursive","marker-wide",1,"Invalid recursive program")]}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,cyclic),"METADATA_ARCHETYPE_CYCLE_OR_DEPTH");
        var capacity=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=a.Parts.Select(p=>p.Id=="markers"?p with{Capacity=new("units",false,4,"units","Mixed capacity","test")}:p).ToArray()}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,capacity),"METADATA_CAPACITY_SOURCE_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe with{MaximumNodes=19},e.Catalog),"METADATA_PART_COUNT_OR_NODE_BUDGET");
        var owner=new MetadataVariationPlanner();
        Reject(()=>owner.ValidateDocument(world with{VariationPlan=null}),"METADATA_VARIATION_PLAN_REQUIRED");
        Reject(()=>owner.ValidateDocument(world with{VariationPlan=plan with{Allocations=plan.Allocations.Skip(1).ToArray()}}),"METADATA_VARIATION_COVERAGE");
        Reject(()=>owner.ValidateDocument(world with{VariationPlan=plan with{Allocations=plan.Allocations.Select((a,i)=>i==0?a with{Choices=a.Choices.Select((c,j)=>j==0?c with{ScoreSha256=new string('0',64)}:c).ToArray()}:a).ToArray()}}),"METADATA_VARIATION_PLAN_MISMATCH");
        var first=world.Nodes.First(n=>n.VariationKey is not null);
        Reject(()=>owner.ValidateDocument(world with{Nodes=world.Nodes.Select(n=>n.Id==first.Id?n with{ArchetypeId="invented"}:n).ToArray()}),"METADATA_VARIATION_MEMBERSHIP_MISMATCH");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancelled.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("seed","Change detail forms","game:seed",before.World.RecipeSha256,before.World.CatalogSha256,e.Recipe,ChangedSeed(e.Catalog)));
        Check(Json(after.World)==Json(altered)&&after.Report.Stages.Single(s=>s.Id=="population-source").Reused&&!after.Report.Stages.Single(s=>s.Id=="content").Reused,"incremental equals full; unrelated source cached and changed content recalculated");
        try{session.Apply(new("oversized","Refuse oversized form","game:oversized",after.World.RecipeSha256,after.World.CatalogSha256,e.Recipe,Oversized(e.Catalog)));throw new Exception("Expected rejected edit");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("METADATA_PLACEMENT_UNRESOLVED"),"physical failure attributed");}
        Check(Json(session.Snapshot)==Json(after),"failed variant edit leaves accepted state untouched");
        var temp=Path.Combine(Path.GetTempPath(),"worldcreator-variation-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"packet"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"packet/world-metadata.json"));Check(Json(saved)==Json(world),"saved choices, dimensions and nested content replay");
        await session.PublishAsync(Path.Combine(temp,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temp,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"checkpoint history replays variant edits");
        Console.WriteLine("PASS: scoped deterministic catalog alternatives, stable identities/order/count growth, nested choices, exact physical source dimensions, complete source/protection preservation, malformed/cyclic/capacity/tamper/budget/cancellation refusal, physical infeasibility, replay, incremental equality, rollback and checkpoint history. Broad variation range, mixed capacity, scale, art and native runtime remain open.");
    }
    private static void RegisteredCase()
    {
        var e=WorldPlacementFeedbackTests.Example(false);var registry=new SettlementCatalogRegistry([new ModernCatalog()]);
        var generator=new WorldMetadataGenerator(registry);var basis=e.Catalog.Archetypes.Single(a=>a.Id=="site") with{
            PhysicalFootprint=null,Access=null,Structure=new(ModernCatalog.Selection,"village-house","cottage",FacilityTechnology.Standard)};
        var family=basis with{Id="site-family",Structure=basis.Structure! with{VariantId="family-house"}};
        var recipe=e.Recipe with{RouteRules=null,AdditionalConnections=null,Placement=e.Recipe.Placement! with{
            Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Select(d=>d with{Profile=d.Profile with{Requirements=[]}}).ToArray()}}};
        foreach(var (seed,variant,w,h) in new[]{("house-1","family-house",12d,16d),("house-2","cottage",8d,10d)})
        {
            var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?basis:a.Id==e.Recipe.RootArchetypeId?a with{
                Parts=a.Parts.Select(p=>p.Id=="sites"?p with{Variation=new("houses",seed,"Choose the registered house form","catalog:house",["site","site-family"])}:p).ToArray()}:a with{Access=null}).Append(family).ToArray()};
            var world=generator.Generate(recipe,catalog);var selected=world.StructureDefinitions.Values.Single();
            Check(selected.Variant.Id==variant&&selected.Variant.WidthMeters==w&&selected.Variant.DepthMeters==h,"registered variant selected with authenticated dimensions");
            var shape=world.PlacementPlan!.Placement.Sites.Single().Footprint.Footprint;
            Check(Math.Abs(Math.Abs(shape.SignedArea())-w*h)<1e-8&&world.Nodes.Count==4,"registered physical geometry and required child preserved");
            WorldMetadataGenerator.ValidateDocument(world,registry);
        }
        Console.WriteLine("PASS: scoped variation selects registered cottage/family-house source variants with exact 8x10/12x16 footprints and required children.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Variation: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
