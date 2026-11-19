using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Catalogs.Modern;
using WorldCreator.Core.Geometry;

internal static class MetadataArtHandoffTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static void Check(bool ok,string message){if(!ok)throw new Exception("Art handoff: "+message);}
    static void Reject(Action action,string code){try{action();}catch(ArgumentException ex)when(ex.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    static MetadataArtSpecification Scopes(WorldMetadataDocument world,Func<WorldMetadataNode,MetadataArtRole> role)=>new(1,"reference-workflow",StructureFirstPipeline.WorldDigest(world),
        world.Nodes.DistinctBy(n=>n.ArchetypeId).Select(n=>new MetadataArtBinding(n.ArchetypeId,role(n))).ToArray(),
        [new("overview",null,"Complete declared world overview",[]),..world.Nodes.Where(n=>n.ParentId==world.Id).Select((n,i)=>
            new MetadataArtScope("detail-"+i,"overview","Complete contents of "+n.Purpose,[n.Id]))]);

    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var owner=new StructureFirstPipeline();var generator=new WorldMetadataGenerator();
        var mixed=WorldMixedProgramTests.Example(true);var world=generator.Generate(mixed.Recipe,mixed.Catalog);
        var spec=Scopes(world,n=>n.Kind switch{"region" or "compound"=>MetadataArtRole.Group,"structure"=>MetadataArtRole.Structure,"ground-area"=>MetadataArtRole.Ground,_=>MetadataArtRole.Object});
        var result=owner.PlanMetadata(world,mixed.Recipe,mixed.Catalog,spec);
        Check(result.Members.Count==43&&result.Jobs.Count(j=>j.Kind=="ReferenceConcept")==4,"all compounds, structures, grounds and equipment retained; repeated sources deduplicated");
        Check(result.Jobs.Count(j=>j.Kind=="DetailMockup")==7&&result.Jobs[^1].NodeIds.Count==43,"all detail scopes feed complete overview including group context");
        Check(Json(result.World)==Json(world)&&Json(result.Catalog)==Json(mixed.Catalog),"geometry, routes, sources and remaining requirements unchanged");
        var ready=new HashSet<string>();foreach(var job in result.Jobs){Check(job.DependsOn.All(ready.Contains),"dependency order");Check(ready.Add(job.Id),"unique jobs");}
        Check(result.Status=="DraftInventoryOnly"&&result.RequiredGates.Count>=5,"planning grants no execution or art approval");
        Reject(()=>owner.ValidateMetadataHandoff(result with{Members=result.Members.Skip(1).ToArray()}),"METADATA_ART_HANDOFF_REPLAY_MISMATCH");
        Reject(()=>owner.ValidateMetadataHandoff(result with{Jobs=result.Jobs.Select(j=>j.Kind=="OverviewMockup"?j with{DependsOn=[]}:j).ToArray()}),"METADATA_ART_HANDOFF_REPLAY_MISMATCH");
        Reject(()=>owner.ValidateMetadataHandoff(result with{Status="Approved"}),"METADATA_ART_HANDOFF_REPLAY_MISMATCH");
        var reordered=spec with{Bindings=spec.Bindings.Reverse().ToArray(),Scopes=spec.Scopes.Reverse().ToArray()};
        Check(Json(result)==Json(owner.PlanMetadata(world,mixed.Recipe,mixed.Catalog,reordered)),"declaration-order independence");
        void Bad(MetadataArtSpecification bad,string code)=>Reject(()=>owner.PlanMetadata(world,mixed.Recipe,mixed.Catalog,bad),code);
        Bad(spec with{WorldSha256=new string('0',64)},"METADATA_ART_SPECIFICATION_INVALID_OR_STALE");
        Bad(spec with{Bindings=spec.Bindings.Skip(1).ToArray()},"METADATA_ART_BINDING_COVERAGE");
        Bad(spec with{Bindings=spec.Bindings.Append(spec.Bindings[0]).ToArray()},"METADATA_ART_BINDING_INVALID");
        Bad(spec with{Bindings=spec.Bindings.Select(b=>b.ArchetypeId=="shell"?b with{Role=MetadataArtRole.Group}:b).ToArray()},"METADATA_ART_GROUP_CONTENT_REQUIRED");
        Bad(spec with{Scopes=spec.Scopes.SkipLast(1).ToArray()},"METADATA_ART_MEMBER_SCOPE_REQUIRED");
        Bad(spec with{Scopes=spec.Scopes.Select(s=>s.Id=="detail-1"?s with{MemberRootIds=spec.Scopes[1].MemberRootIds}:s).ToArray()},"METADATA_ART_SCOPE_OVERLAP");
        Bad(spec with{Scopes=spec.Scopes.Select(s=>s.Id=="detail-0"?s with{ParentId="missing"}:s).ToArray()},"METADATA_ART_PARENT_REQUIRED");
        Bad(spec with{Scopes=spec.Scopes.Select(s=>s.Id=="detail-0"?s with{ParentId="detail-0"}:s).ToArray()},"METADATA_ART_SCOPE_CYCLE");
        var changed=world with{Nodes=world.Nodes.SkipLast(1).ToArray()};
        Reject(()=>owner.PlanMetadata(changed,mixed.Recipe,mixed.Catalog,spec with{WorldSha256=StructureFirstPipeline.WorldDigest(changed)}),"METADATA_ART_SOURCE_REPLAY_MISMATCH");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{owner.PlanMetadata(world,mixed.Recipe,mixed.Catalog,spec,token:cancel.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        await Save("mixed",mixed.Recipe,mixed.Catalog,world,spec,generator);

        var town=WorldMetadataTests.Example("town");
        var registered=town.Catalog with{Archetypes=town.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Structure=new(ModernCatalog.Selection,"warehouse","small")}:a).ToArray()};
        var registeredGenerator=new WorldMetadataGenerator(TestCatalogs.Registry);var registeredWorld=registeredGenerator.Generate(town.Recipe,registered);
        var registeredSpec=Scopes(registeredWorld,n=>n.Kind=="location"?MetadataArtRole.Structure:MetadataArtRole.Group);
        var registeredResult=owner.PlanMetadata(registeredWorld,town.Recipe,registered,registeredSpec,registeredGenerator);
        Check(registeredResult.Members.Count==19&&registeredResult.Jobs.Count(j=>j.Kind=="ReferenceConcept")==1,"15 registered instances use one exact variant reference");
        Check(registeredResult.World.StructureDefinitions.Single().Value.Variant.WidthMeters==12,"registered dimensions preserved");
        Check(registeredResult.World.Surface is null&&registeredResult.World.Nodes.All(n=>n.Position is null),"pre-mockup inventory does not force physical placement");
        await Save("registered",town.Recipe,registered,registeredWorld,registeredSpec,registeredGenerator);

        var dungeon=WorldMetadataTests.Example("dungeon");var dw=generator.Generate(dungeon.Recipe,dungeon.Catalog);
        var ds=Scopes(dw,n=>n.Kind=="room"?MetadataArtRole.Ground:MetadataArtRole.Group);
        var dh=owner.PlanMetadata(dw,dungeon.Recipe,dungeon.Catalog,ds);
        Check(dh.Jobs.Count(j=>j.Kind=="DetailMockup")==2&&dh.Jobs[^1].NodeIds.Count==15,"non-city room/floor program with no forced surface or settlement");
        await Save("interior-graph",dungeon.Recipe,dungeon.Catalog,dw,ds,generator);

        var custom=WorldPlacementTests.Example();var demands=custom.Recipe.Placement!.Request.Demands.ToArray();
        demands[1]=demands[1] with{Footprint=new WorldPolygon([new(-2,-2),new(2,-2),new(2,2),new(-2,2)])};
        var customRecipe=custom.Recipe with{NetworkRules=null,Placement=custom.Recipe.Placement with{Request=custom.Recipe.Placement.Request with{Demands=demands}}};
        var cw=generator.Generate(customRecipe,custom.Catalog);var cs=Scopes(cw,n=>n.ParentId is null?MetadataArtRole.Group:MetadataArtRole.Object);
        var ch=owner.PlanMetadata(cw,customRecipe,custom.Catalog,cs);
        Check(ch.Jobs.Count(j=>j.Kind=="ReferenceConcept"&&j.ArchetypeId=="camp")==2,"same archetype with different authored source geometry gets separate references");
        await Save("custom-shapes",customRecipe,custom.Catalog,cw,cs,generator);
        Console.WriteLine("PASS: complete generic/registered/non-city art inventories, nested ownership, exact geometry/variant replay, source-dependent deduplication, dependency order, omissions/overlaps/cycles/stale refusal, cancellation and new-file publication.");

        async Task Save(string name,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldMetadataDocument source,MetadataArtSpecification specification,WorldMetadataGenerator gen)
        {
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            foreach(var item in new[]{("recipe.json",Json(recipe)),("catalog.json",Json(catalog)),("world-metadata.json",Json(source)),("scopes.json",Json(specification))})
                await File.WriteAllTextAsync(Path.Combine(folder,item.Item1),item.Item2);
            string target=Path.Combine(folder,"handoff.json");var saved=await owner.ExportMetadataAsync(source,recipe,catalog,specification,target,gen);
            Check(Json(saved)==await File.ReadAllTextAsync(target),"saved exact handoff");
            owner.ValidateMetadataHandoff(await StructureFirstPipeline.ReadJsonAsync<MetadataArtHandoff>(target),gen);
            string before=await File.ReadAllTextAsync(target);bool refused=false;
            try{await owner.ExportMetadataAsync(source,recipe,catalog,specification,target,gen);}catch(IOException){refused=true;}
            Check(refused&&before==await File.ReadAllTextAsync(target),"existing output preserved");
            Console.WriteLine($"PASS: {name}: {saved.Members.Count} members; {saved.Jobs.Count} ordered jobs.");
        }
    }
}
