using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldComposedResourceTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Composed resources: "+why);}
    internal static async Task<MetadataWorldDomainDefinition> ExampleAsync(double amount,bool larger=false)
    {
        var d=larger?await WorldMorphologyTests.ExampleAsync():await WorldBroadCompositionTests.ExampleAsync();
        return d with{Domains=d.Domains.Select(x=>x.Id!="terrain"?x:x with{
            Recipe=x.Recipe with{TerrainDemands=x.Recipe.TerrainDemands! with{Rules=x.Recipe.TerrainDemands.Rules.Select(r=>r with{AmountPerMember=amount,MaximumCount=1024}).ToArray()}},
            Catalog=x.Catalog with{Archetypes=x.Catalog.Archetypes.Select(a=>a.Id!=x.Recipe.RootArchetypeId?a:a with{
                ChildRequirements=a.ChildRequirements.Select(r=>r.Kind=="environment-feature"?r with{Maximum=1024}:r).ToArray()}).ToArray()}}).ToArray()};
    }
    public static async Task RunAsync(double amount,string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Composed resource evidence exists");Directory.CreateDirectory(output);
        var observations=new List<object>();string phase="authoring";var clock=new Stopwatch();long allocations=0;
        void Start(string name){phase=name;allocations=GC.GetTotalAllocatedBytes(false);clock.Restart();}
        void End(object? details=null)
        {
            clock.Stop();using var process=Process.GetCurrentProcess();process.Refresh();
            observations.Add(new{Phase=phase,Seconds=clock.Elapsed.TotalSeconds,AllocatedBytes=GC.GetTotalAllocatedBytes(false)-allocations,
                PeakWorkingSetBytes=process.PeakWorkingSet64,WorkingSetBytes=process.WorkingSet64,Details=details});
        }
        void Save(string status,string? error=null)=>File.WriteAllText(Path.Combine(output,"resources.json"),Json(new{Status=status,AmountPerMember=amount,Error=error,Phase=phase,
            ProcessId=Environment.ProcessId,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            LogicalProcessors=Environment.ProcessorCount,Observations=observations}));
        var definition=await ExampleAsync(amount);var changed=await ExampleAsync(amount,true);
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        var generator=new WorldMetadataGenerator();var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"framework-resources");
        try
        {
            Start("create");var session=await MetadataDomainGenerationSession.StartAsync(generator,definition,reporting);var initial=session.Snapshot;
            var terrain=initial.World.Domains.Single(d=>d.Id=="terrain").World;int landscape=terrain.TerrainDemandPlan!.Results.Single().Count;
            End(new{Nodes=initial.World.Domains.Sum(d=>d.World.Nodes.Count),Physical=initial.World.Domains.Sum(d=>d.World.AccessPlan?.Sites.Count??0),Landscape=landscape,
                initial.Report.CacheBytes,PlacementWork=terrain.PlacementPlan!.Work,RootPlacementWork=terrain.PlacementPlan.Placement.Work});Save("IN PROGRESS");
            Check(initial.World.Domains.Sum(d=>d.World.Nodes.Count)==73+landscape&&initial.World.Domains.Sum(d=>d.World.AccessPlan?.Sites.Count??0)==68+landscape,"every original member plus entire derived landscape retained");
            Start("validate-baseline");WorldDiverseProgramTests.Qualify(terrain,definition.Domains.Single(d=>d.Id=="terrain").Catalog,amount);End();Save("IN PROGRESS");
            Start("publish");await session.PublishAsync(Path.Combine(output,"baseline"));
            End(new{PacketBytes=Directory.EnumerateFiles(Path.Combine(output,"baseline"),"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length)});Save("IN PROGRESS");
            var edit=new MetadataDomainEditRequest("larger-source","Keep the full scaled landscape around the exact larger source","resources:source",initial.World.DefinitionSha256,changed);
            await File.WriteAllTextAsync(Path.Combine(output,"edit.json"),Json(edit));
            Start("edit-publish");var accepted=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"edited"));var editedTerrain=accepted.World.Domains.Single(d=>d.Id=="terrain").World;
            End(new{Nodes=accepted.World.Domains.Sum(d=>d.World.Nodes.Count),Physical=accepted.World.Domains.Sum(d=>d.World.AccessPlan?.Sites.Count??0),Landscape=editedTerrain.TerrainDemandPlan!.Results.Single().Count,
                accepted.Report.CacheBytes,PlacementWork=editedTerrain.PlacementPlan!.Work});Save("IN PROGRESS");
            Check(editedTerrain.TerrainDemandPlan.Results.Single().Count==landscape,"source-size edit never drops a funded landscape member");
            Start("validate-edited");WorldDiverseProgramTests.Qualify(editedTerrain,changed.Domains.Single(d=>d.Id=="terrain").Catalog,amount);
            foreach(string domain in new[]{"local","progression"})Check(Json(initial.World.Domains.Single(d=>d.Id==domain))==Json(accepted.World.Domains.Single(d=>d.Id==domain)),"unrelated domain unchanged");
            Check(accepted.World.Journeys!.Single().Forward.Count>0&&accepted.World.Journeys!.Single().Return!.Count>0,"complete physical/logical journey and return");End();Save("IN PROGRESS");
            Start("clean-comparison");var clean=generator.GenerateDomains(changed);Check(Json(clean)==Json(accepted.World),"full current clean world equality");End();Save("IN PROGRESS");
            Start("restore");var restored=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/domain-session.json"),reporting:reporting);
            Check(Json(restored.Snapshot)==Json(accepted),"complete source history reconstructs exact scaled world/checkpoint");
            End();
            Save("PASS");Console.WriteLine("PASS: full composed resource lifecycle amount="+amount+"; "+(73+landscape)+" nodes / "+(68+landscape)+" physical.");
        }
        catch(ArgumentException error)
        {
            End(new{Outcome="Rejected"});Save("REJECTED",error.Message);
            Console.WriteLine("REJECTED: amount="+amount+"; phase="+phase+"; "+error.Message);
        }
        catch(Exception error){End(new{Outcome="Failed"});Save("FAILED",error.ToString());throw;}
    }
}
