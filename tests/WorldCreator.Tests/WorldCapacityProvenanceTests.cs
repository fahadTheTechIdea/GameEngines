using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldCapacityProvenanceTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Capacity provenance: "+why);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output,bool prior=false)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var e=WorldMixedVariantCapacityTests.Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        if(prior){new MetadataCapacityPlanner().ValidateDocument(world with{CapacityPlan=null});await File.WriteAllTextAsync(Path.Combine(output,"observation.json"),Json(new{MissingPlanAccepted=true,OriginalMembers=world.Nodes.Count,OriginalAllocations=world.CapacityPlan!.Allocations.Count}));Console.WriteLine("PASS: prior validator accepts the same complete world after its capacity plan is removed.");return;}
        var allocation=world.CapacityPlan!.Allocations.Single();var json=JsonSerializer.SerializeToElement(world,WorldJson.CreateOptions());
        var marked=json.GetProperty("Nodes").EnumerateArray().Where(n=>n.TryGetProperty("CapacityKey",out var key)&&key.ValueKind==JsonValueKind.String).ToArray();
        Check(marked.Length==allocation.Count&&marked.All(n=>n.GetProperty("CapacityKey").GetString()=="capacity-world/capacity/camps"),"every capacity-selected physical member retains its allocation identity");
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(world with{CapacityPlan=null}),"METADATA_CAPACITY_PLAN_REQUIRED");
        var rogue=JsonSerializer.Deserialize<WorldMetadataNode>(Json(world.Nodes.First(n=>n.ParentId is not null)).Replace("\"CapacityKey\":\"capacity-world/capacity/camps\"","\"CapacityKey\":\"unrelated/capacity/wrong\"",StringComparison.Ordinal),WorldJson.CreateOptions())!;
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(world with{Nodes=world.Nodes.Select(n=>n.Id==rogue.Id?rogue:n).ToArray()}),"METADATA_CAPACITY_MEMBERSHIP_MISMATCH");
        var omitted=world with{CapacityPlan=world.CapacityPlan with{Allocations=[]}};
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(omitted),"METADATA_CAPACITY_PLAN_INVALID");
        var orphanJson=JsonSerializer.SerializeToNode(world.Nodes.First(n=>n.Kind=="station"),WorldJson.CreateOptions())!.AsObject();orphanJson["CapacityKey"]="orphan/capacity/member";
        var orphan=orphanJson.Deserialize<WorldMetadataNode>(WorldJson.CreateOptions())!;
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(world with{Nodes=world.Nodes.Select(n=>n.Id==orphan.Id?orphan:n).ToArray()}),"METADATA_CAPACITY_COVERAGE");
        var terrain=WorldTerrainCapacityTests.Example();var terrainWorld=generator.Generate(terrain.Recipe,terrain.Catalog);
        Check(terrainWorld.Nodes.Count(n=>JsonSerializer.SerializeToElement(n,WorldJson.CreateOptions()).TryGetProperty("CapacityKey",out _))==4,"terrain-owned exact count preserves all capacity links");
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(terrainWorld with{CapacityPlan=null}),"METADATA_CAPACITY_PLAN_REQUIRED");
        string missingFile=Path.Combine(output,"missing-capacity-plan.json");await File.WriteAllTextAsync(missingFile,Json(world with{CapacityPlan=null}));
        try{await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(missingFile);throw new Exception("Stored world missing capacity plan was accepted");}
        catch(ArgumentException error)when(error.Message.Contains("METADATA_CAPACITY_PLAN_REQUIRED",StringComparison.Ordinal)){}
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"packet"));
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"packet")))==Json(world),"full source replay retains capacity provenance");
        var zeroRecipe=e.Recipe with{Placement=null,Surface=null,Environment=null,NetworkRules=null,RequireAllPositions=false,Origin=null,Parameters=new Dictionary<string,int>{{"visitors",0}}};
        var zeroCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="root"?a with{PhysicalFootprint=null,Parts=a.Parts.Select(p=>p with{Layout=null}).ToArray()}:a with{
            PhysicalFootprint=null,Parts=a.Parts.Select(p=>p with{Count=0}).ToArray(),ChildRequirements=[]}).ToArray()};
        var zero=generator.Generate(zeroRecipe,zeroCatalog);Check(zero.CapacityPlan!.Allocations.Single().Count==0,"zero demand declares no capacity members");
        new MetadataCapacityPlanner().ValidateDocument(zero with{CapacityPlan=null});
        string zeroDir=Path.Combine(output,"zero");Directory.CreateDirectory(zeroDir);
        await File.WriteAllTextAsync(Path.Combine(zeroDir,"recipe.json"),Json(zeroRecipe));await File.WriteAllTextAsync(Path.Combine(zeroDir,"catalog.json"),Json(zeroCatalog));
        await generator.GenerateFilesAsync(Path.Combine(zeroDir,"recipe.json"),Path.Combine(zeroDir,"catalog.json"),Path.Combine(zeroDir,"packet"));
        await File.WriteAllTextAsync(Path.Combine(zeroDir,"original-world.json"),Json(zero));await File.WriteAllTextAsync(Path.Combine(zeroDir,"packet/world-metadata.json"),Json(zero with{CapacityPlan=null}));
        try{await generator.ReadPublishedAsync(Path.Combine(zeroDir,"packet"));throw new Exception("Zero-member source mismatch accepted");}
        catch(ArgumentException error)when(error.Message.Contains("METADATA_PUBLICATION_REPLAY_MISMATCH",StringComparison.Ordinal)){}
        Console.WriteLine("PASS: capacity member bindings, missing plan, altered binding, orphan coverage, terrain-exact composition and source replay.");
    }
}
