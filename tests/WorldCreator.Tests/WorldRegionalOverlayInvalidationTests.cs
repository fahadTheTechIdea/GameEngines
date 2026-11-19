using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalOverlayInvalidationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional overlay invalidation: "+message);}
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private sealed class Probe(string region):IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"overlay-probe-"+region;public int Version,Calls;public Action? OnExpansion;
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{Version});
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {token.ThrowIfCancellationRequested();if(count>0){Calls++;OnExpansion?.Invoke();}return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new double[dimensions]).ToArray();}
    }
    private static MetadataRegionalEditRequest Edit(MetadataRegionalGenerationSession session,string id,MetadataRegionalWorldDefinition definition)
        =>new(id,"Apply a purposeful local world overlay","overlay:edit",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,definition);
    private static MetadataRegionalWorldDefinition Base()
    {
        var source=WorldRegionalContentTests.Example();return source with{Regions=source.Regions.Select(r=>r.Id!="west"?r:r with{Recipe=r.Recipe with{
            Placement=r.Recipe.Placement! with{Request=r.Recipe.Placement.Request with{Anchors=[new("fixed",new(24,30))]}}}}).ToArray()};
    }
    private static WorldTerrainPatch Patch(MetadataRegionalWorldDefinition source,string id,double x,double y,double width,double height,string kind="water")
    {
        var terrain=source.Surface.Terrain!;return new(id,kind,"Declared terrain affects complete world travel","overlay:"+id,Box(x,y,width,height),1,terrain.Origin,
            terrain.BaseElevation,terrain.GradientX,terrain.GradientY);
    }
    private static MetadataRegionalWorldDefinition West(MetadataRegionalWorldDefinition source)=>source with{Surface=source.Surface with{Patches=[Patch(source,"local-water",32,28,2,4)]}};
    private static MetadataRegionalWorldDefinition Moved(MetadataRegionalWorldDefinition source)=>source with{Surface=source.Surface with{Patches=[Patch(source,"local-water",78,28,2,4)]}};
    private static void Complete(MetadataRegionalWorldDocument world,int count=2)
    {
        Check(world.Regions.Count==count&&world.Regions.Sum(r=>r.World.Nodes.Count)==count*5&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==count*3,"all required source-sized programs remain complete");
        Check(world.Connections.Count==count-1&&world.Connections.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"every required regional round trip survives");
    }
    private static async Task Reject<T>(Func<Task> action,string code)where T:Exception
    {try{await action();}catch(T error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve overlay evidence");Directory.CreateDirectory(output);
        var west=new Probe("west");var east=new Probe("east");var generator=new WorldMetadataGenerator(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),west,east]);
        var engine=new MetadataRegionalWorldGenerator(generator);var bare=Base();var input=bare with{Regions=bare.Regions.Select(r=>r with{Catalog=r.Catalog with{
            Archetypes=r.Catalog.Archetypes.Select(a=>a with{Parts=a.Parts.Select(p=>p.Layout?.StrategyId!="request-probe"?p:p with{Layout=p.Layout with{StrategyId="overlay-probe-"+r.Id}}).ToArray()}).ToArray()}}).ToArray()};
        var session=new MetadataRegionalGenerationSession(generator,input);var initial=session.Snapshot;Complete(initial.World);
        await engine.PublishRegionAsync(initial.World.Definition,initial.World.Regions.Single(r=>r.RegionId=="east"),Path.Combine(output,"saved-east"));
        west.Calls=east.Calls=0;var changed=West(input);var request=Edit(session,"west-overlay",changed);var edited=session.Apply(request);Complete(edited.World);
        await File.WriteAllTextAsync(Path.Combine(output,"initial-world.json"),Json(initial.World));
        await File.WriteAllTextAsync(Path.Combine(output,"edited-world.json"),Json(edited.World));
        await File.WriteAllTextAsync(Path.Combine(output,"edited-report.json"),Json(edited.Report));
        Check(Json(initial.World.Connections[0].Forward.Path)!=Json(edited.World.Connections[0].Forward.Path),"local water actually changes the required whole-world route geometry");
        Check(edited.Report.Regions.Single(r=>r.RegionId=="east").Reused&&!edited.Report.Regions.Single(r=>r.RegionId=="west").Reused,"only west packet invalidates for its local overlay");
        Check(east.Calls==0&&Json(initial.World.Regions.Single(r=>r.RegionId=="east"))==Json(edited.World.Regions.Single(r=>r.RegionId=="east")),"unaffected east content is not expanded and its complete packet remains byte-identical");
        Check(edited.Report.Regions.Single(r=>r.RegionId=="west").Stages.Any(s=>!s.Reused)&&edited.Report.Regions.Single(r=>r.RegionId=="west").Stages.Any(s=>s.Reused),"affected region recomputes dependent stages and retains valid stage reuse");
        Check(Json(edited.World)==Json(engine.Generate(changed)),"localized regeneration equals complete clean generation");
        var loaded=await engine.ReadRegionAsync(Path.Combine(output,"saved-east"),changed);Check(Json(loaded)==Json(initial.World.Regions.Single(r=>r.RegionId=="east")),"saved untrusted east packet remains eligible through full current-source replay");
        await session.PublishAsync(Path.Combine(output,"edited"));
        Check(Json((await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(edited),"localized context and history authenticate on ordinary restore");
        var moved=Moved(input);var movedWorld=session.Apply(Edit(session,"moved-overlay",moved));Complete(movedWorld.World);
        Check(movedWorld.Report.Regions.All(r=>!r.Reused)&&Json(movedWorld.World)==Json(engine.Generate(moved)),"moving overlay invalidates old west and new east domains and matches clean generation");
        var touching=moved with{Surface=moved.Surface with{Patches=[..moved.Surface.Patches,Patch(moved,"boundary-contact",34,1,1,1,"ground")]}};
        var contact=session.Apply(Edit(session,"boundary-contact",touching));Check(contact.Report.Regions.All(r=>!r.Reused),"closed boundary contact retains the complete source in both dependency domains");
        var ordered=touching with{Surface=touching.Surface with{Patches=touching.Surface.Patches.Reverse().ToArray()}};
        var reordered=session.Apply(Edit(session,"source-order",ordered));Check(reordered.Report.Regions.All(r=>r.Reused)&&Json(reordered.World)==Json(engine.Generate(ordered)),"canonical surface source order preserves packet eligibility");
        var excluded=ordered with{Surface=ordered.Surface with{Exclusions=[new("local-exclusion","Keep declared ground unavailable","overlay:excluded",Box(1,1,1,1))]}};
        var exclusion=session.Apply(Edit(session,"local-exclusion",excluded));
        Check(exclusion.Report.Regions.Single(r=>r.RegionId=="east").Reused&&!exclusion.Report.Regions.Single(r=>r.RegionId=="west").Reused&&
            Json(exclusion.World)==Json(engine.Generate(excluded)),"exclusions use the same closed regional dependency projection without source clipping");ordered=excluded;
        var before=Json(session.Snapshot);var bad=ordered with{Surface=ordered.Surface with{Patches=[..ordered.Surface.Patches,Patch(ordered,"remote-invalid",500,500,1,1) with{Kind=""}]}};
        await Reject<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Edit(session,"invalid-remote",bad))),"WORLD_SURFACE_PATCH_INVALID");
        var badExclusion=ordered with{Surface=ordered.Surface with{Exclusions=[..ordered.Surface.Exclusions,new("remote-malformed","Reject remote malformed geometry","overlay:invalid",new([new(500,500),new(501,500)]))]}};
        await Reject<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Edit(session,"invalid-exclusion",badExclusion))),"WORLD_SURFACE_GEOMETRY_BUDGET_OR_INPUT_INVALID");
        var blocked=input with{Surface=input.Surface with{Patches=[Patch(input,"closed-passage",32,0,2,80)]}};
        await Reject<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Edit(session,"blocked-world",blocked))),"METADATA_REGIONAL_CONNECTION_UNRESOLVED");
        Check(Json(session.Snapshot)==before,"malformed remote source and globally blocked journey preserve accepted geometry/cache/history");
        east.Version++;var drift=session.Apply(Edit(session,"provider-change",ordered));Check(drift.Report.Regions.Single(r=>r.RegionId=="west").Reused&&!drift.Report.Regions.Single(r=>r.RegionId=="east").Reused,"installed provider drift invalidates its actual region independently of overlay locality");
        using(var cancellation=new CancellationTokenSource())
        {
            east.Version++;east.OnExpansion=cancellation.Cancel;before=Json(session.Snapshot);
            await Reject<OperationCanceledException>(()=>Task.FromResult(session.Apply(Edit(session,"cancelled",ordered),cancellation.Token)),"");east.OnExpansion=null;east.Version--;
            Check(Json(session.Snapshot)==before,"cancelled dependent generation commits no packet or history");
        }
        await PortableAsync(output,WorldRegionalContentTests.Portable(bare));await ScaleAsync(output);
        Console.WriteLine("PASS: actual terrain route redesign, exact untouched packets without expansion, old/new overlay domains, boundary contact, canonical source order, source/provider/cancellation failure preservation, saved authoritative replay and complete16-region locality.");
    }
    private static async Task PortableAsync(string output,MetadataRegionalWorldDefinition input)
    {
        var session=new MetadataRegionalGenerationSession(new(),input);await session.PublishAsync(Path.Combine(output,"portable-initial"));
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(input));
        foreach(var (id,definition) in new[]{("west-overlay",West(input)),("moved-overlay",Moved(input)),("shared-height",Moved(input) with{Surface=Moved(input).Surface with{Terrain=input.Surface.Terrain! with{BaseElevation=input.Surface.Terrain.BaseElevation+.0001}}})})
        {
            var request=Edit(session,id,definition);var snapshot=await session.ApplyAndPublishAsync(request,Path.Combine(output,"portable-"+id));Complete(snapshot.World);
            var expected=id=="west-overlay"?new[]{"east"}:Array.Empty<string>();
            Check(snapshot.Report.Regions.Where(r=>r.Reused).Select(r=>r.RegionId).SequenceEqual(expected),"public built-in context locality/global height invalidation: "+id);
            Check(Json(snapshot.World)==Json(new MetadataRegionalWorldGenerator().Generate(definition)),"portable complete clean equality: "+id);
            await File.WriteAllTextAsync(Path.Combine(output,id+"-edit.json"),Json(request));
        }
    }
    internal static async Task ScaleOnlyAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve overlay scale evidence");Directory.CreateDirectory(output);
        await ScaleAsync(output);Console.WriteLine("PASS: complete16-region overlay generation, fifteen exact retained packets, clean full equality and observed resources.");
    }
    private static async Task ScaleAsync(string output)
    {
        var input=await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>("examples/metadata/regional-overlay-invalidation/scale-16.json");
        var session=new MetadataRegionalGenerationSession(new(),input);var initial=session.Snapshot;Complete(initial.World,16);
        var changed=West(input);long bytes=GC.GetTotalAllocatedBytes(true);var timer=Stopwatch.StartNew();var snapshot=session.Apply(Edit(session,"one-of-sixteen",changed));timer.Stop();bytes=GC.GetTotalAllocatedBytes(true)-bytes;
        Complete(snapshot.World,16);
        await File.WriteAllTextAsync(Path.Combine(output,"scale-measurements.json"),Json(new{Regions=16,Nodes=80,Bodies=48,ReusedRegions=snapshot.Report.Regions.Count(r=>r.Reused),GeneratedRegions=snapshot.Report.Regions.Count(r=>!r.Reused),Seconds=timer.Elapsed.TotalSeconds,AllocatedBytes=bytes,CumulativePeakWorkingSet=Process.GetCurrentProcess().PeakWorkingSet64,CacheBytes=snapshot.Report.CacheBytes}));
        await File.WriteAllTextAsync(Path.Combine(output,"scale-world.json"),Json(snapshot.World));
        Check(snapshot.Report.Regions.Count(r=>r.Reused)==15&&!snapshot.Report.Regions.Single(r=>r.RegionId=="region-00").Reused,"one local overlay invalidates one of sixteen complete programs");
        Check(snapshot.Report.Regions.Where(r=>r.Reused).All(r=>Json(snapshot.World.Regions.Single(p=>p.RegionId==r.RegionId))==Json(initial.World.Regions.Single(p=>p.RegionId==r.RegionId))),"all fifteen unaffected complete packets remain exact");
        Check(Json(snapshot.World)==Json(new MetadataRegionalWorldGenerator().Generate(changed)),"sixteen-region localized result equals full clean generation");
        await new MetadataRegionalWorldGenerator().PublishAsync(snapshot.World,Path.Combine(output,"scale-16"));
    }
}
