using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSemanticOpeningTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Semantic opening: "+why);}
    private static void Reject(Action run,string prefix)
    {try{run();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    private static WorldSpatialWorkBudget Budget(long maximum=1000000,CancellationToken token=default)=>new(maximum,token);
    public static async Task RunAsync(string output)
    {
        await WorldBroadCompositionTests.RunAsync(output);
        var generator=new WorldMetadataGenerator();var definition=await WorldBroadCompositionTests.ExampleAsync();
        var world=generator.GenerateDomains(definition);var transition=world.Transitions.Single(t=>t.Definition.Id=="advance");
        var local=world.Domains.Single(d=>d.Id=="local").World;var endpoint=transition.From.Definition;
        var expected=local.Connections.Where(c=>c.Kind=="door"&&(c.FromId==endpoint.NodeId||c.ToId==endpoint.NodeId)).OrderBy(c=>c.Id,StringComparer.Ordinal).First();
        Check(transition.From.ResolvedInteriorConnectionId==expected.Id,"saved exact selected connection provenance");
        var explicitEndpoint=endpoint with{InteriorOpening=null,InteriorConnectionId=expected.Id};
        var explicitDefinition=definition with{Transitions=definition.Transitions.Select(t=>t.Id=="advance"?t with{From=explicitEndpoint}:t).ToArray()};
        var explicitWorld=generator.GenerateDomains(explicitDefinition);
        Check(Json(world.Domains)==Json(explicitWorld.Domains),"semantic selector changes no local world data or member");
        Check(Json(world.Journeys)==Json(explicitWorld.Journeys),"exact same complete forward/return source steps");
        Check(Json(transition.From.Position)==Json(explicitWorld.Transitions.Single(t=>t.Definition.Id=="advance").From.Position)&&
            transition.From.AvailableWidth==explicitWorld.Transitions.Single(t=>t.Definition.Id=="advance").From.AvailableWidth,"exact explicit opening approach and width");
        Check(!Json(explicitEndpoint).Contains("InteriorOpening")&&!Json(explicitWorld.Transitions.Single(t=>t.Definition.Id=="advance").From).Contains("ResolvedInteriorConnectionId"),"unused optional selector/proof fields omitted");
        // Exercise selectors directly against every actual room, both endpoint
        // orientations and reversed source ordering. This is independent full-sort
        // selection; production retains only the bounded ordinal prefix.
        var observations=new List<object>();int cases=0;
        foreach(string room in local.InteriorPlan!.Results.SelectMany(r=>r.RoomNodeIds))
        {
            var links=local.Connections.Where(c=>c.Kind=="door"&&(c.FromId==room||c.ToId==room)).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
            for(int index=0;index<links.Length;index++)
            {
                var select=new MetadataInteriorOpeningSelector("door",index);var e=endpoint with{NodeId=room,InteriorOpening=select};
                var resolved=WorldMetadataGenerator.ResolveDomainEndpoint(e,local,Budget(),default);
                var opening=local.InteriorPlan.Results.SelectMany(r=>r.Openings).Single(o=>o.ConnectionId==links[index].Id);
                var point=opening.FromId==room?opening.FromApproach:opening.ToApproach;
                Check(resolved.ResolvedInteriorConnectionId==links[index].Id&&resolved.Position!.SequenceEqual(new[]{point.X,point.Y})&&resolved.AvailableWidth==opening.Width,"actual incident opening ID, side and width");
                Check(Json(resolved)==Json(WorldMetadataGenerator.ResolveDomainEndpoint(e,local with{Connections=local.Connections.Reverse().ToArray()},Budget(),default)),"input order does not change selection");
                observations.Add(new{Room=room,Index=index,Connection=links[index].Id,Position=resolved.Position,Width=resolved.AvailableWidth});cases++;
            }
        }
        var many=Enumerable.Range(0,10005).Select(i=>expected with{Id="edge-"+i.ToString("D5"),FromId=endpoint.NodeId,ToId="other-"+i}).Reverse().ToArray();
        Check(WorldMetadataGenerator.SelectDomainOpeningConnection(endpoint.NodeId,new("door",127),many,Budget())=="edge-00127","maximum ordinal with 10005 incident edges");
        Reject(()=>WorldMetadataGenerator.SelectDomainOpeningConnection(endpoint.NodeId,new("door",128),many,Budget()),"METADATA_DOMAIN_ENDPOINT_INVALID");
        Reject(()=>WorldMetadataGenerator.SelectDomainOpeningConnection(endpoint.NodeId,new("door",-1),many,Budget()),"METADATA_DOMAIN_ENDPOINT_INVALID");
        Reject(()=>WorldMetadataGenerator.SelectDomainOpeningConnection(endpoint.NodeId,new(""),many,Budget()),"METADATA_DOMAIN_ENDPOINT_INVALID");
        Reject(()=>WorldMetadataGenerator.SelectDomainOpeningConnection(endpoint.NodeId,new("missing"),many,Budget()),"METADATA_DOMAIN_OPENING_REQUIRED");
        Reject(()=>WorldMetadataGenerator.SelectDomainOpeningConnection(endpoint.NodeId,new("door"),many,Budget(1)),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{WorldMetadataGenerator.SelectDomainOpeningConnection(endpoint.NodeId,new("door"),many,Budget(token:c.Token));throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        // Direct resolver forgery cases deliberately are not valid world documents.
        var firstMissing=expected with{Id="000-no-opening"};
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(endpoint,local with{Connections=[firstMissing,..local.Connections]},Budget(),default),"METADATA_DOMAIN_OPENING_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(endpoint with{Profile=endpoint.Profile! with{Radius=10}},local,Budget(),default),"METADATA_DOMAIN_ENDPOINT_TOO_WIDE");
        var logical=world.Domains.Single(d=>d.Id=="progression").World;
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(endpoint with{NodeId=logical.Nodes[0].Id},logical,Budget(),default),"METADATA_DOMAIN_LOGICAL_ENDPOINT_INVALID");
        foreach(var invalid in new[]{endpoint with{InteriorConnectionId=expected.Id},endpoint with{AccessPortId="west"},endpoint with{NetworkRuleId="network"},endpoint with{InteriorOpening=new("door",128)}})
            Reject(()=>generator.GenerateDomains(definition with{Transitions=definition.Transitions.Select(t=>t.Id=="advance"?t with{From=invalid}:t).ToArray()}),"METADATA_DOMAIN_ENDPOINT_INVALID");
        var journey=definition.Journeys!.Single();
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{To=endpoint with{InteriorConnectionId=expected.Id}}]}),"METADATA_DOMAIN_ENDPOINT_INVALID");
        var selectedJourney=generator.GenerateDomains(definition with{Journeys=[journey with{To=endpoint} ]});
        Check(selectedJourney.Journeys!.Single().To.ResolvedInteriorConnectionId==expected.Id,"journey endpoints use the same semantic resolver");
        Reject(()=>generator.ValidateDomains(world with{Transitions=world.Transitions.Select(t=>t.Definition.Id=="advance"?t with{From=t.From with{ResolvedInteriorConnectionId="forged"}}:t).ToArray()}),"METADATA_DOMAIN_DOCUMENT_MISMATCH");
        var restored=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/domain-session.json"));
        var accepted=restored.Snapshot;var badDefinition=accepted.World.Definition with{Transitions=accepted.World.Definition.Transitions.Select(t=>t.Id=="advance"?
            t with{From=t.From with{InteriorOpening=new("missing")}}:t).ToArray()};
        var badEdit=new MetadataDomainEditRequest("missing-opening","Refuse a semantic endpoint without its exact generated opening","opening:rollback",accepted.World.DefinitionSha256,badDefinition);
        await File.WriteAllTextAsync(Path.Combine(output,"missing-opening-edit.json"),Json(badEdit));
        try{await restored.ApplyAndPublishAsync(badEdit,Path.Combine(output,"missing-opening-refused"));throw new Exception("Missing semantic endpoint refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_DOMAIN_OPENING_REQUIRED"){}
        Check(Json(accepted)==Json(restored.Snapshot)&&!Directory.Exists(Path.Combine(output,"missing-opening-refused")),"missing selected opening leaves full accepted history/world/output intact");
        await File.WriteAllTextAsync(Path.Combine(output,"opening-selection-oracle.json"),Json(new{Cases=cases,Selections=observations,LargeIncidentEdges=many.Length,
            WorldNodes=91,Physical=86,ExplicitWorldAndJourneys="IDENTICAL",Scope="Full-sort actual incident connection/opening/side/width equations plus direct synthetic selector/refusal cases; synthetic forged worlds are not accepted source documents."}));
        Console.WriteLine("PASS: semantic interior endpoints without authoring prepass; "+cases+" actual opening oracles, full 91-node world equivalence, exact journeys, 10005-edge bounded prefix, invalid/conflicting/missing/no-fallback/width/work/cancel/tamper checks.");
    }
}
