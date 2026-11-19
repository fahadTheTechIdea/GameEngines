using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldDomainSessionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Domain session: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var definition=WorldDomainJourneyTests.Example();
        var session=new MetadataDomainGenerationSession(generator,definition);var initial=session.Snapshot;
        Check(initial.Report.Domains.All(d=>d.Stages.Count>0&&d.Stages.All(s=>!s.Reused)),"initial generation executes each domain");
        Check(initial.Report.CacheBytes==initial.Report.Domains.Sum(d=>d.CacheBytes)&&initial.Report.CacheBytes>0,"aggregate cache accounting");
        var mutable=session.Snapshot;((IList<double>)mutable.World.Domains[0].World.Nodes.First(n=>n.Position is not null).Position!)[0]=999;
        Check(Json(session.Snapshot)==Json(initial),"snapshot mutation cannot alter accepted world/cache");
        var refined=definition with{Journeys=[definition.Journeys![0] with{Purpose="Refined authored journey intent"}]};
        var journeyOnly=session.Apply(new("journey-only","Change collection intent without regenerating local geometry","domain-session:journey",initial.World.DefinitionSha256,refined));
        Check(journeyOnly.Report.Domains.All(d=>d.Stages.Count>0&&d.Stages.All(s=>s.Reused)),"journey-only edit reuses every local stage");
        Check(Json(journeyOnly.World)==Json(generator.GenerateDomains(refined)),"cached intent edit equals clean generation including proofs/work");
        var upper=WorldGeneratedInteriorTests.Example(count:3,width:8);var changed=refined with{Domains=refined.Domains.Select(d=>d.Id=="upper"?d with{Recipe=upper.Recipe,Catalog=upper.Catalog}:d).ToArray()};
        var floor=session.Apply(new("upper-width","Change one source-sized floor module","domain-session:floor",journeyOnly.World.DefinitionSha256,changed));
        Check(floor.Report.Domains.Where(d=>d.DomainId!="upper").All(d=>d.Stages.All(s=>s.Reused)),"unchanged domain stages reused");
        Check(floor.Report.Domains.Single(d=>d.DomainId=="upper").Stages.Any(s=>!s.Reused),"changed floor recomputes dependent stages");
        Check(Json(floor.World)==Json(generator.GenerateDomains(changed)),"one-floor cached edit equals complete clean generation");
        Check(Json(initial.World.Domains.Single(d=>d.Id=="lower").World)==Json(floor.World.Domains.Single(d=>d.Id=="lower").World),"lower source/geometry preserved");
        Check(Json(initial.World.Domains.Single(d=>d.Id=="upper").World)!=Json(floor.World.Domains.Single(d=>d.Id=="upper").World),"upper geometry actually changed");
        var before=session.Snapshot;
        var impossible=changed with{Journeys=[changed.Journeys![0] with{ConnectionKinds=["west-travel","doorway","stairs"]}]};
        try{session.Apply(new("impossible","Preserve accepted collection after failed journey","domain-session:rollback",before.World.DefinitionSha256,impossible));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException e)when(e.InnerException is ArgumentException cause&&cause.Message.StartsWith("METADATA_DOMAIN_JOURNEY_UNREACHABLE",StringComparison.Ordinal)){}
        Check(Json(session.Snapshot)==Json(before),"proof failure preserves every accepted domain, report and cache");
        Reject(()=>session.Apply(new("stale","Reject stale source edit","domain-session:stale",initial.World.DefinitionSha256,changed)),"METADATA_DOMAIN_EDIT_STALE_BASE");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{session.Apply(new("cancelled","Cancel proposed collection edit","domain-session:cancel",before.World.DefinitionSha256,changed),cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        Check(Json(session.Snapshot)==Json(before),"cancellation preserves accepted collection");
        var recovered=session.Apply(new("after-refusal","Reuse accepted caches after a rejected proposal","domain-session:recover",before.World.DefinitionSha256,changed));
        Check(recovered.Report.Domains.All(d=>d.Stages.All(s=>s.Reused)),"failed proposal cannot poison accepted caches");
        Check(Json(recovered.World)==Json(generator.GenerateDomains(changed)),"recovery remains clean-generation equivalent");
        string directory=Path.Combine(Path.GetTempPath(),"domain-session-"+Guid.NewGuid().ToString("N"));string output=Path.Combine(directory,"accepted");
        await session.PublishAsync(output);var restored=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"domain-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"checkpoint reconstructs source history and private caches by replay");
        var accepted=restored.Snapshot;var publishEdit=new MetadataDomainEditRequest("publish","Commit a complete edited collection atomically","domain-session:publish",accepted.World.DefinitionSha256,
            changed with{Purpose="Refined collection purpose"});
        try{await restored.ApplyAndPublishAsync(publishEdit,output);throw new Exception("Missing output refusal");}catch(IOException e)when(e.Message=="METADATA_DOMAIN_OUTPUT_EXISTS"){}
        Check(Json(restored.Snapshot)==Json(accepted),"publication failure does not commit proposed caches or world");
        var published=await restored.ApplyAndPublishAsync(publishEdit,Path.Combine(directory,"edited"));
        Check(Json(published.World)==Json(generator.GenerateDomains(publishEdit.Definition))&&published.Report.Domains.All(d=>d.Stages.All(s=>s.Reused)),"successful publication commits complete cache/world state");
        var publishedRestore=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(directory,"edited/domain-session.json"));
        Check(Json(publishedRestore.Snapshot)==Json(published),"published session replay reproduces acceptance report");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataDomainSessionCheckpoint>(Path.Combine(output,"domain-session.json"));
        string tampered=Path.Combine(directory,"tampered.json");await File.WriteAllTextAsync(tampered,Json(checkpoint with{WorldSha256="changed"}));
        try{await MetadataDomainGenerationSession.RestoreAsync(generator,tampered);throw new Exception("Missing checkpoint tamper refusal");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHECKPOINT_REPLAY_MISMATCH"){}
        Console.WriteLine($"PASS: transactional three-domain/21-node journey generation; all-stage intent reuse, one-floor dependent recomputation, unchanged-domain reuse, clean equality including proofs, snapshot isolation, stale/failure/cancel rollback and cache recovery. Cache bytes={recovered.Report.CacheBytes}; stages="+
            string.Join(",",floor.Report.Domains.Select(d=>d.DomainId+":"+d.Stages.Count(s=>s.Reused)+"/"+d.Stages.Count)));
    }
}
