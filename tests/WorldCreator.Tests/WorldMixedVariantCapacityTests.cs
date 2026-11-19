using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Catalogs.Modern;

internal static class WorldMixedVariantCapacityTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Mixed source capacity: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldCapacityTests.Example();var camp=e.Catalog.Archetypes.Single(a=>a.Id=="camp");
        var other=camp with{Id="camp-small",Capacities=[camp.Capacities![0] with{Quantity=7,SourceRuleId="catalog:small-rest"}]};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p.Id!="camps"?p:p with{
            Variation=new("camp-options","capacity-seed","Select complete authored camp forms","catalog:camp-options",["camp","camp-small"],new Dictionary<string,int>{{"camp",1},{"camp-small",2}})}).ToArray()}).Append(other).ToArray()};
        return(e.Recipe with{Parameters=new Dictionary<string,int>{{"visitors",20}}},catalog);
    }
    private static string Choice(int index)
    {
        string node="capacity-world/camps-"+index;var scores=new List<(string Score,string Id)>();
        foreach(var (id,tickets) in new[]{("camp",1),("camp-small",2)})for(int ticket=0;ticket<tickets;ticket++)
        {
            var data=new List<string>{"world-variation-v1","capacity-seed","camp-options","capacity-world","camps",node,id};
            if(ticket>0){data.Add("ticket");data.Add(ticket.ToString(System.Globalization.CultureInfo.InvariantCulture));}
            scores.Add((Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data,WorldJson.CreateOptions()))),id));
        }
        return scores.OrderBy(s=>s.Score,StringComparer.Ordinal).First().Id;
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var e=Example();var generator=new WorldMetadataGenerator();var expected=new List<string>();int quantity=0;
        while(quantity<20){var id=Choice(expected.Count);expected.Add(id);quantity+=id=="camp"?10:7;}
        var world=generator.Generate(e.Recipe,e.Catalog);var allocation=world.CapacityPlan!.Allocations.Single();
        Check(allocation.Count==expected.Count&&allocation.ProvidedQuantity==quantity,"sum actual selected source capacities without shrinking or dropping parts");
        var choices=world.Nodes.Where(n=>n.ParentId==world.Id&&n.Kind=="camp").OrderBy(n=>n.Id,StringComparer.Ordinal).Select(n=>n.ArchetypeId).ToArray();
        Check(choices.SequenceEqual(expected)&&world.PlacementPlan!.Placement.Sites.Count==expected.Count,"all selected exact sources are physically placed");
        var data=JsonSerializer.SerializeToElement(allocation,WorldJson.CreateOptions());
        Check(data.TryGetProperty("Contributions",out var contributions)&&contributions.GetArrayLength()>0&&contributions.EnumerateArray().Sum(c=>c.GetProperty("NodeIds").GetArrayLength())==expected.Count,"capacity receipt identifies every contributing source and member");
        var corrupted=allocation with{Contributions=allocation.Contributions.Select((c,i)=>i==0?c with{ProvidedQuantity=c.ProvidedQuantity+1}:c).ToArray()};
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(world with{CapacityPlan=world.CapacityPlan with{Allocations=[corrupted]}}),"METADATA_CAPACITY_PLAN_MISMATCH");
        var grown=generator.Generate(e.Recipe with{Parameters=new Dictionary<string,int>{{"visitors",25}}},e.Catalog);
        foreach(var n in world.Nodes){var later=grown.Nodes.Single(m=>m.Id==n.Id);Check(later.ArchetypeId==n.ArchetypeId&&later.ParentId==n.ParentId,"larger demand preserves earlier identities and variants");}
        var wrong=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="camp-small"?a:a with{Capacities=[a.Capacities![0] with{Unit="other"}]}).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,wrong),"METADATA_CAPACITY_UNIT_MISMATCH");
        var limited=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p.Id!="camps"?p:p with{Capacity=p.Capacity! with{MaximumCount=2}}).ToArray()}).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,limited),"METADATA_CAPACITY_INSTANCE_BUDGET");
        var logical=e.Recipe with{Placement=null,Surface=null,Environment=null,NetworkRules=null,RequireAllPositions=false,Origin=null,Parameters=new Dictionary<string,int>()};
        var registered=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id switch{
            "root"=>a with{Parts=a.Parts.Select(p=>p.Id!="camps"?p:p with{Capacity=new(null,true,100,"storage-units","Required registered storage","test:registered",MaximumCount:8)}).ToArray()},
            "camp"=>a with{Capacities=null,PhysicalFootprint=null,Parts=a.Parts.Select(p=>p with{Layout=null}).ToArray(),Structure=new(ModernCatalog.Selection,"warehouse","medium")},
            "camp-small"=>a with{Capacities=null,PhysicalFootprint=null,Parts=a.Parts.Select(p=>p with{Layout=null}).ToArray(),Structure=new(ModernCatalog.Selection,"warehouse","small")},
            _=>a with{PhysicalFootprint=null,Parts=a.Parts.Select(p=>p with{Layout=null}).ToArray()}}).ToArray()};
        var registeredWorld=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(logical,registered);var registeredAllocation=registeredWorld.CapacityPlan!.Allocations.Single();
        Check(registeredAllocation.Contributions.Count==2&&registeredAllocation.ProvidedQuantity>=100&&registeredAllocation.Contributions.All(c=>c.Source.SourceId.StartsWith("structure:",StringComparison.Ordinal)),"mixed registered variants use their actual independent source capacities");
        await File.WriteAllTextAsync(Path.Combine(output,"registered-world.json"),Json(registeredWorld));
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"packet"));
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"packet")))==Json(world),"complete mixed-source package replays");
        Console.WriteLine($"PASS: {expected.Count} weighted source-sized variants provide {quantity} capacity, with stable growth, exact provenance and replay.");
    }
}
