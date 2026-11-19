using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldDomainJourneyCapabilitiesTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Capability journey: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var input=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDomainWorldDefinition>("examples/metadata/regional-domain-world/progression-definition.json");
        var journey=input.Journeys!.Single();var entry=input.Transitions.Single(t=>t.Id=="entry").To;
        var gate=new MetadataDomainProgressionGate(new(null,"stairs"),["key"]);
        var capabilities=new MetadataDomainJourneyCapabilities(["key"],[],[new(entry,["key"])],[gate]);
        input=input with{Journeys=[journey with{Capabilities=capabilities}]};
        var engine=new MetadataRegionalDomainWorldGenerator();var world=engine.Generate(input);var result=world.Domains.Journeys!.Single();
        int stair=result.Forward.ToList().FindIndex(s=>s.Kind=="transition"&&s.SourceId=="stairs");
        Check(stair>=0&&result.ForwardCapabilityStates is {} forward&&forward.Count==result.Forward.Count+1&&forward[stair].Contains("key")&&
            result.ReturnCapabilityStates is {} back&&back.Count==result.Return!.Count+1&&back[0].Contains("key"),"physical path reaches key before gate and retains it for return");
        var local=world.Domains.Domains.ToDictionary(d=>d.Id,d=>d.World,StringComparer.Ordinal);
        IReadOnlyList<MetadataDomainJourneyResult>? Plan(MetadataDomainJourney changed,IReadOnlyList<MetadataDomainTransitionResult>? transitions=null)
            =>WorldMetadataGenerator.PlanDomainJourneys(world.Domains.Definition with{Journeys=[changed]},local,transitions??world.Domains.Transitions,
                new(20000000,default),new(1000000,default),default);
        var lowerRoute=local["lower"].InteriorPlan!.Results.Single().Routes[0];
        var localGate=new MetadataDomainProgressionGate(new("lower",lowerRoute.ConnectionId),["key"]);
        var gatedLocal=Plan(journey with{Capabilities=capabilities with{Gates=[gate,localGate]}})!.Single();
        int localIndex=gatedLocal.Forward.ToList().FindIndex(s=>s.SourceId==lowerRoute.Forward.Id||s.SourceId==lowerRoute.Reverse?.Id);
        Check(localIndex>=0&&gatedLocal.ForwardCapabilityStates![localIndex].Contains("key"),"local physical connection gate checks the acquired capability");
        Reject(()=>Plan(journey with{Capabilities=capabilities with{Grants=[new(journey.To,["key"])],Gates=[localGate]}}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        Reject(()=>Plan(journey with{Capabilities=capabilities with{Grants=[]}}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        Reject(()=>Plan(journey with{Capabilities=capabilities with{Grants=[new(journey.To,["key"])]}}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        Reject(()=>Plan(journey with{Capabilities=capabilities with{MaximumStates=1}}),"METADATA_DOMAIN_JOURNEY_STATE_BUDGET");
        var oneWay=world.Domains.Transitions.Select(t=>t.Definition.Id=="stairs"?t with{Definition=t.Definition with{Bidirectional=false}}:t).ToArray();
        Reject(()=>Plan(journey with{Capabilities=capabilities},oneWay),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        var forged=result with{ForwardCapabilityStates=result.ForwardCapabilityStates!.Select((s,i)=>i==stair?(IReadOnlyList<string>)[]:s).ToArray()};
        Reject(()=>engine.Validate(world with{Domains=world.Domains with{Journeys=[forged]}}),"METADATA_REGIONAL_DOMAIN_DOCUMENT_MISMATCH");

        var properties=new Dictionary<string,JsonElement>();var catalog=new WorldMetadataCatalog(1,"logical-key",[
            new("root","state","Start",properties,[new("next","leaf",1,"Key location")],[new("local","local-step",MetadataConnectionPattern.Hub,"self","next")],[]),
            new("leaf","state","Key location",properties,[],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"logic","framework",catalog.Id,"root","topology",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        var a=new MetadataDomainEndpoint("a","logic");var b=new MetadataDomainEndpoint("b","logic");var key=new MetadataDomainEndpoint("a","logic/next-0");
        var logic=new MetadataWorldDomainDefinition(1,"return-alternative","A later arrival permits return","test:return",
            [new("a","First domain","test:a",recipe,catalog),new("b","Second domain","test:b",recipe,catalog)],
            [new("exit","portal","One-way exit","test:exit",a,b),new("return","portal","Key-gated return","test:return",b,a)],
            Journeys:[new("round-trip","Acquire a key before returning","test:round-trip",a,b,["local-step","portal"],new Dictionary<string,WorldTraversalProfile>(),true,
                Capabilities:new(["key"],[],[new(key,["key"])],[new(new(null,"return"),["key"])]))]);
        var logical=new WorldMetadataGenerator().GenerateDomains(logic).Journeys!.Single();
        Check(logical.Forward.Any(s=>s.Kind=="logical-connection")&&logical.ReturnCapabilityStates![0].Contains("key"),
            "search chooses an attainable goal state that can return, instead of stopping at an earlier dead end");
        await engine.PublishAsync(world,output);
        string html=await File.ReadAllTextAsync(Path.Combine(output,"index.html"));int entryIndex=html.IndexOf("Explicit domain change stairs",StringComparison.Ordinal);
        Check(entryIndex>=0&&html[entryIndex..html.IndexOf("</li>",entryIndex,StringComparison.Ordinal)].Contains("capabilities: key",StringComparison.Ordinal),"journey inspector shows capability at gated transition");
        var replay=await engine.ReadAsync(Path.Combine(output,"world.json"));
        Check(Json(replay)==Json(world),"physical capability witnesses replay");
        Console.WriteLine("PASS: qualified physical journeys track key grants and gates, preserve return state and find a later returnable goal state.");
    }
}
