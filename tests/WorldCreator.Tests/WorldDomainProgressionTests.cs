using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldDomainProgressionTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Domain progression: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDomainWorldDefinition>("examples/metadata/regional-domain-world/floors-definition.json");
        const string parent="region-west/compounds-0";
        var start=new MetadataDomainProgressionNode("outpost",parent);var key=new MetadataDomainProgressionNode("lower","generated-interior/rooms-0");
        var goal=new MetadataDomainProgressionNode("upper","generated-interior/rooms-2");
        var gate=new MetadataDomainProgressionLink(null,"stairs");
        var caps=new MetadataDomainProgressionCapabilities(["key"],[],[new(key,["key"])],[new(gate,["key"])],true);
        var rule=new MetadataDomainProgressionRule("progression","Reach the upper floor after acquiring a capability","test:progression",start,[goal],
            ["regional-travel","doorway","door","stairs"],[key],Capabilities:caps);
        var input=original with{Progression=[rule]};var engine=new MetadataRegionalDomainWorldGenerator();var world=engine.Generate(input);
        var plan=world.Domains.Progression!;var path=plan.Results.Single().Paths.Single();
        Check(path.Nodes.First()==start&&path.Nodes.Last()==goal&&path.Nodes.Contains(key)&&path.Nodes.Select(n=>n.DomainId).Distinct().Count()>=4,
            "region and floor identities retained through logical progression");
        int keyIndex=path.Nodes.ToList().IndexOf(key),gateIndex=path.Links.ToList().IndexOf(gate);
        Check(gateIndex>keyIndex&&path.CapabilityStates![gateIndex].Contains("key")&&path.Links.Count==path.Nodes.Count-1,
            "capability gained before gated explicit floor transition");
        Check(world.Domains.Journeys!.Single().Forward.Any(s=>s.SourceId=="stairs"),"separate physical journey proof is retained");
        var planner=new MetadataDomainProgressionPlanner();var domains=world.Domains.Domains;var transitions=world.Domains.Transitions;
        Reject(()=>planner.Plan([rule with{Capabilities=caps with{Grants=[]}}],domains,transitions,default),"METADATA_PROGRESSION_UNREACHABLE");
        var oneWay=transitions.Select(t=>t.Definition.Id=="stairs"?t with{Definition=t.Definition with{Bidirectional=false}}:t).ToArray();
        Reject(()=>planner.Plan([rule],domains,oneWay,default),"METADATA_PROGRESSION_RETURN_UNREACHABLE");
        Reject(()=>planner.Plan([rule with{MaximumWork=1}],domains,transitions,default),"METADATA_PROGRESSION_WORK_BUDGET");
        var broken=world with{Domains=world.Domains with{Progression=plan with{Work=0}}};
        Reject(()=>engine.Validate(broken),"METADATA_REGIONAL_DOMAIN_DOCUMENT_MISMATCH");
        await engine.PublishAsync(world,output);
        var html=await File.ReadAllTextAsync(Path.Combine(output,"index.html"));
        Check(html.Contains("Gameplay progression",StringComparison.Ordinal)&&html.Contains("capabilities: key",StringComparison.Ordinal),"progression and capability evidence visible");
        var replay=await engine.ReadAsync(Path.Combine(output,"world.json"));
        Check(Json(replay)==Json(world),"capabilities, transitions and physical journey replay");
        Console.WriteLine("PASS: regional-to-floor progression gains a required capability before a gated transition; missing grant, one-way return, budget, tamper and replay verified.");
    }
}
