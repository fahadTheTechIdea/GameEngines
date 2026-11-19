using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalDesignBindingTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return e;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var region=template.Regions.Single(r=>r.Id=="arrival");
        var renamedRecipe=JsonSerializer.Deserialize<WorldMetadataRecipe>(Json(region.Recipe).Replace("region-west/compounds-0","region-west/sites-0",StringComparison.Ordinal).Replace("region-west","new-region-program",StringComparison.Ordinal),WorldJson.CreateOptions())!;
        var renamedCatalog=region.Catalog with{Archetypes=region.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{
            Parts=a.Parts.Select(p=>p.Id=="compounds"?p with{Id="sites"}:p).ToArray()}).ToArray()};
        var wrongCatalog=region.Catalog with{Archetypes=region.Catalog.Archetypes.Select(a=>a.Id=="compound"?a with{Kind="other-compound"}:a.Id=="root"?a with{
            ChildRequirements=a.ChildRequirements.Select(c=>c.Kind=="compound"?c with{Kind="other-compound"}:c).ToArray()}:a).ToArray()};
        var request=new MetadataRegionalDesignRequest(1,"semantic-design","Connect diverse regional programs by their role","test:semantic-design",template,
            [new("arrival",[new("no-role",0,region.Recipe,wrongCatalog),new("renamed-path",1,renamedRecipe,renamedCatalog)])],
            [new("required-compound","arrival","compound",1,1,true)],MaximumAttempts:2,
            Bindings:[new("regional-journeys","arrival","compound","compound")]);
        var engine=new MetadataRegionalDesignGenerator();string original=Json(request);var result=engine.Generate(request);
        Check(Json(request)==original,"Source input stays unchanged.");
        Check(result.Receipt.Attempts.Count==2&&result.Receipt.Attempts[0].Rejection=="METADATA_REGIONAL_DESIGN_BINDING_COUNT:regional-journeys:arrival:0"&&
            result.Receipt.Attempts[1].Accepted,"Missing role advances to the next complete program.");
        var bound=result.Receipt.Attempts[1].BoundEndpoints!.Single();
        Check(bound.RegionId=="arrival"&&bound.NodeId=="new-region-program/sites-0"&&result.Receipt.Attempts.All(a=>a.BindingWork>0),"Receipt records resolved path and charged analysis.");
        var destination=result.World.Definition.NetworkRules!.Single().Destinations.Single(d=>d.Id=="arrival");
        Check(destination.Endpoint.NodeId==""&&destination.Endpoint.Selector?.NodeKind=="compound"&&destination.Endpoint.PortId=="east"&&
            result.World.NetworkPlan!.Networks.Single().ResolvedDestinations!.Single(d=>d.DestinationId=="arrival").Endpoint.NodeId==bound.NodeId,
            "Network resolves the generated node and preserves its access port.");
        Check(result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&result.World.Connections.Count==3&&
            result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"All complete physical programs and both directions survive.");
        var directRule=template.NetworkRules![0] with{Destinations=template.NetworkRules[0].Destinations.Select(d=>d.Id!="arrival"?d:d with{
            Endpoint=d.Endpoint with{NodeId="",Selector=new("compound","compound")}}).ToArray()};
        var direct=engine.Generate(request with{Template=template with{NetworkRules=[directRule]},Bindings=null});
        Check(direct.Receipt.Attempts.Count==2&&direct.Receipt.Attempts[0].Rejection!.StartsWith("METADATA_REGIONAL_NETWORK_SELECTOR_COUNT:")&&
            direct.Receipt.Attempts[1].Accepted&&direct.World.Regions.Single(r=>r.RegionId=="arrival").World.Nodes.Any(n=>n.Id=="new-region-program/sites-0"),
            "Direct selector rejects a missing role and permits the alternate root path.");
        var ambiguous=request with{Slots=[request.Slots[0] with{Options=[request.Slots[0].Options[1]]}],Bindings=[new("regional-journeys","arrival","module")]};
        var exhausted=Reject(()=>engine.Generate(ambiguous),"METADATA_REGIONAL_DESIGN_ALTERNATIVES_EXHAUSTED");
        Check(exhausted.Data["RegionalDesignAttempts"] is MetadataRegionalDesignAttempt[]{Length:1} failures&&failures[0].Rejection!.EndsWith(":2"),"Ambiguous roles are not guessed.");
        Reject(()=>engine.Generate(request with{MaximumBindingWork=1}),"METADATA_REGIONAL_DESIGN_BINDING_WORK_BUDGET");
        Reject(()=>engine.Generate(request with{Bindings=[new("absent","arrival","compound")]}),"METADATA_REGIONAL_DESIGN_BINDING_TARGET_INVALID");
        Reject(()=>engine.Generate(request with{Bindings=null}),"METADATA_REGIONAL_DESIGN_UNBOUND_EXTERNAL_ENDPOINT");
        using(var cts=new CancellationTokenSource()){cts.Cancel();try{engine.Generate(request,cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"Saved source, resolution and world replay.");
        var changed=result.Receipt with{Attempts=result.Receipt.Attempts.Select((a,i)=>i!=1?a:a with{BoundEndpoints=[bound with{NodeId="forged"}]}).ToArray()};
        Reject(()=>engine.Verify(changed,result.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: game-defined regional endpoint roles, alternate program paths, missing/ambiguous refusal, physical routing, bounded work, cancellation and saved replay.");
    }
}
