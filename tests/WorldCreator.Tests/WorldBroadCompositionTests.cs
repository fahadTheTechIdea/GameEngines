using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldBroadCompositionTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string why){if(!v)throw new Exception("Broad composition: "+why);}
    internal static async Task<MetadataWorldDomainDefinition> ExampleAsync(bool coastal=false)
    {
        string folder="examples/metadata/diverse-programs/"+(coastal?"coastal":"inland");
        var terrainRecipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(folder+"/recipe.json");
        var terrainCatalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(folder+"/catalog.json");
        var local=WorldMixedGrowthTests.Example(network:true);
        const string room="generated-interior/rooms-5";
        var props=new Dictionary<string,JsonElement>();
        var logicCatalog=new WorldMetadataCatalog(1,"progression-catalog",[
            new("root","progression","Independent required progression",props,[new("states","state",2,"Required progression states")],
                [new("advance","advance",MetadataConnectionPattern.Hub,"self","states")],[]),new("state","state","Required progression state",props,[],[],[])]);
        var logicRecipe=new WorldMetadataRecipe(1,"progression","framework-reference",logicCatalog.Id,"root","topology",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new(["phase"],"logical"));
        var terrainWalk=terrainRecipe.NetworkRules!.Single().Options.Profile;var localWalk=local.Recipe.Interiors!.Rules[0].Profile;
        MetadataDomainEndpoint Compound(string id)=>new("terrain",terrainRecipe.Id+"/"+id+"-0","front",Profile:terrainWalk);
        var localEntry=new MetadataDomainEndpoint("local","generated-interior/trail-1","entry",Profile:localWalk);
        var localExit=new MetadataDomainEndpoint("local",room,Profile:localWalk,InteriorOpening:new("door"));
        return new(1,"composed-world-reference","Complete terrain compounds, physical local program and abstract progression","broad:composition",
            [new("terrain","Complete terrain-driven compound and landscape program","broad:terrain",terrainRecipe,terrainCatalog),
                new("local","Complete outside/interior/catalog content program","broad:local",local.Recipe,local.Catalog),
                new("progression","Independent geometry-free progression","broad:progression",logicRecipe,logicCatalog)],
            [new("enter","portal","Explicit game transition between independent physical coordinate domains","broad:enter",Compound("workshop"),localEntry,true),
                new("advance","progression-gate","Explicit game transition into abstract progression","broad:advance",localExit,new("progression","progression"),true)],
            [new("round-trip","Required complete domain round trip","broad:reachability","terrain","progression",true)],
            Journeys:[new("complete","Required terrain-to-room-to-progression journey and return","broad:journey",Compound("lookout"),new("progression","progression/states-1"),
                [terrainRecipe.NetworkRules!.Single().ConnectionKind,"regional","entry","door","portal","progression-gate","advance"],
                new Dictionary<string,WorldTraversalProfile>{{"terrain",terrainWalk},{"local",localWalk}},true)]);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Broad composition evidence exists");Directory.CreateDirectory(output);
        var g=new WorldMetadataGenerator();var definition=await ExampleAsync();await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"framework-composition");
        var session=await MetadataDomainGenerationSession.StartAsync(g,definition,reporting);var initial=session.Snapshot;
        Check(initial.World.Domains.Sum(d=>d.World.Nodes.Count)==91&&initial.World.Domains.Sum(d=>d.World.AccessPlan?.Sites.Count??0)==86,"every declared compound/local/progression member and physical source retained");
        Check(initial.World.Domains.Single(d=>d.Id=="progression").World.Surface is null,"logical progression acquires no invented physical geometry");
        foreach(var input in definition.Domains)
            Check(Json(initial.World.Domains.Single(d=>d.Id==input.Id).World)==Json(g.Generate(input.Recipe,input.Catalog)),"complete local output equals its own independent generation");
        Check(initial.World.Journeys!.Single().Forward.Any(s=>s.DomainId=="terrain")&&initial.World.Journeys!.Single().Forward.Any(s=>s.DomainId=="local")&&
            initial.World.Journeys!.Single().Forward.Any(s=>s.Kind=="logical-connection")&&initial.World.Journeys!.Single().Return!.Count>0,"actual complete mixed physical/logical journey in both directions");
        await session.PublishAsync(Path.Combine(output,"baseline"));
        var changed=await ExampleAsync(coastal:true);var edit=new MetadataDomainEditRequest("coastal","Apply the complete coastal terrain/program counterfactual","broad:edit",initial.World.DefinitionSha256,changed);
        await File.WriteAllTextAsync(Path.Combine(output,"edit.json"),Json(edit));var accepted=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"edited"));
        Check(accepted.World.Domains.Sum(d=>d.World.Nodes.Count)==91&&Json(accepted.World)==Json(g.GenerateDomains(changed)),"full coastal declaration matches clean generation without count/source reduction");
        Check(accepted.Report.Domains.Where(d=>d.DomainId!="terrain").All(d=>d.Stages.All(s=>s.Reused)),"terrain counterfactual preserves unrelated domain stage caches");
        Check(initial.World.Domains.Where(d=>d.Id!="terrain").All(d=>Json(d.World)==Json(accepted.World.Domains.Single(e=>e.Id==d.Id).World)),"all unrelated local/progression data remains identical");
        var restored=await MetadataDomainGenerationSession.RestoreAsync(g,Path.Combine(output,"edited/domain-session.json"),reporting:reporting);
        Check(Json(restored.Snapshot)==Json(accepted),"complete source/checkpoint history reconstructs all worlds and journeys");
        var bad=changed with{Journeys=[changed.Journeys![0] with{ConnectionKinds=changed.Journeys[0].ConnectionKinds.Where(k=>k!="door").ToArray()}]};
        var failure=new MetadataDomainEditRequest("missing-rooms","Refuse a domain graph whose physical room journey is absent","broad:rollback",accepted.World.DefinitionSha256,bad);
        await File.WriteAllTextAsync(Path.Combine(output,"invalid-edit.json"),Json(failure));
        try{await restored.ApplyAndPublishAsync(failure,Path.Combine(output,"refused"));throw new Exception("Missing physical continuity refusal");}catch(ArgumentException){}
        Check(Json(restored.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"refused")),"failed broad composition cannot publish partial worlds or alter accepted state");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Nodes=91,Physical=86,Domains=3,Transitions=2,Initial=initial.Report.WorldSha256,Edited=accepted.Report.WorldSha256,
            InitialSteps=initial.World.Journeys!.Single().Forward.Count,InitialReturn=initial.World.Journeys!.Single().Return!.Count,EditedSteps=accepted.World.Journeys!.Single().Forward.Count,EditedReturn=accepted.World.Journeys!.Single().Return!.Count}));
        Console.WriteLine("PASS: complete 91-node/86-physical terrain/compound/outside/interior/progression composition, physical/logical round trips, source edits, stage reuse, rollback and checkpoint replay.");
    }
}
