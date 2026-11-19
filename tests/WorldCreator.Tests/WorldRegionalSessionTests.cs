using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalSessionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional session: "+message);}
    private static async Task<T> Throws<T>(Func<Task> run)where T:Exception
    {try{await run();}catch(T error){return error;}throw new Exception("Expected "+typeof(T).Name);}
    private sealed class Counter(string region):IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"request-probe-"+region;public int Calls,Version;public Action? During;
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{Version});
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();if(count>0){Calls++;During?.Invoke();}
            return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new double[dimensions]).ToArray();
        }
    }
    private sealed class Resolver:IMetadataGenerationDependencyResolver
    {
        public WorldMetadataGenerator Resolve(IReadOnlyList<MetadataGenerationDependency> dependencies)
        {
            var expected=dependencies.Single(d=>d.Id=="request-probe-east").Implementation.ConfigurationSha256;
            int version=Enumerable.Range(0,2).Single(v=>WorldGenerationDependencyIdentity.Digest(new{Version=v})==expected);
            return new(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),new Counter("east"){Version=version},new Counter("west")]);
        }
    }
    private static MetadataRegionalWorldDefinition Instrument(MetadataRegionalWorldDefinition d)=>d with{Regions=d.Regions.Select(r=>r with{
        Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a with{Parts=a.Parts.Select(p=>p.Layout?.StrategyId!="request-probe"?p:
            p with{Layout=p.Layout with{StrategyId="request-probe-"+r.Id}}).ToArray()}).ToArray()}}).ToArray()};
    private static MetadataRegionalWorldDefinition Rich(MetadataRegionalWorldDefinition d,string regionId="west")=>d with{Regions=d.Regions.Select(r=>r.Id!=regionId?r:r with{
        Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.Id!="compound"?a:a with{
            Parts=a.Parts.Select(p=>p.Id!="modules"?p:p with{Count=3}).ToArray(),ChildRequirements=[new("module",3,3)]}).ToArray()},
        Recipe=r.Recipe with{AdditionalConnections=r.Recipe.AdditionalConnections!.Select(c=>c with{ToId=r.Recipe.Id+"/compounds-0/modules-2"}).ToArray(),
            Placement=r.Recipe.Placement! with{Programs=r.Recipe.Placement!.Programs!.Select(p=>p with{Bindings=p.Bindings.Select(b=>b with{
                Population=b.Population! with{Path=new(new([new(-4,0),new(4,0)]),4)}}).ToArray()}).ToArray()}}}).ToArray()};
    private static MetadataRegionalEditRequest Edit(MetadataRegionalGenerationSession s,string id,MetadataRegionalWorldDefinition d)
    {var snapshot=s.Snapshot;return new(id,"Explicit complete regional edit","regional-session:edit",snapshot.World.DefinitionSha256,snapshot.Report.WorldSha256,d);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var east=new Counter("east");var west=new Counter("west");var g=new WorldMetadataGenerator(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),east,west]);
        var engine=new MetadataRegionalWorldGenerator(g);var definition=Instrument(WorldRegionalContentTests.Example());
        var s=new MetadataRegionalGenerationSession(g,definition);var initial=s.Snapshot;
        Check(Json(initial.World)==Json(engine.Generate(definition)),"initial session equals complete clean generation");
        Check(initial.World.Regions.Sum(r=>r.World.Nodes.Count)==10&&initial.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==6,"complete nested10-node/6-source baseline");
        east.Calls=west.Calls=0;var noop=s.Apply(Edit(s,"noop",definition));
        Check(noop.Report.Regions.All(r=>r.Reused&&r.Stages.Count==0)&&east.Calls==0&&west.Calls==0,"no-op retains both private whole packets without content expansion");
        Check(noop.Report.WorldSha256==initial.Report.WorldSha256,"no-op preserves exact world");
        var rich=Rich(definition);var richer=s.Apply(Edit(s,"richer",rich));
        Check(east.Calls==0&&west.Calls>0&&richer.Report.Regions.Single(r=>r.RegionId=="east").Reused&&!richer.Report.Regions.Single(r=>r.RegionId=="west").Reused,"only richer west content expands after no-op; accepted cache remains usable");
        Check(richer.Report.Regions.Single(r=>r.RegionId=="west").Stages.Any(p=>p.Reused),"changed program still reuses valid common stages");
        Check(richer.World.Regions.Sum(r=>r.World.Nodes.Count)==11&&richer.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==7,"every richer required member and source retained");
        Check(Json(richer.World.Regions.Single(r=>r.RegionId=="east"))==Json(initial.World.Regions.Single(r=>r.RegionId=="east")),"unaffected packet remains byte-equivalent");
        Check(Json(richer.World)==Json(engine.Generate(rich)),"incremental rich world equals complete clean generation");
        var isolated=s.Snapshot;((IList<double>)isolated.World.Regions[0].World.Nodes[0].Position!)[0]+=1000;
        Check(Json(s.Snapshot)==Json(richer),"caller snapshot mutation cannot change accepted geometry");
        string before=Json(s.Snapshot);
        var stale=await Throws<ArgumentException>(()=>Task.FromResult(s.Apply(Edit(s,"stale",definition) with{ExpectedDefinitionSha256="changed"})));
        Check(stale.Message.StartsWith("METADATA_REGIONAL_EDIT_STALE_BASE"),"stale optimistic base refuses");
        var staleWorld=await Throws<ArgumentException>(()=>Task.FromResult(s.Apply(Edit(s,"stale-world",definition) with{ExpectedWorldSha256="changed"})));
        Check(staleWorld.Message.StartsWith("METADATA_REGIONAL_EDIT_STALE_BASE"),"world hash also protects dependency-only changes under the same definition");
        var overlap=rich with{Regions=rich.Regions.Select(r=>r.Id!="east"?r:r with{Recipe=r.Recipe with{Placement=r.Recipe.Placement! with{
            Request=r.Recipe.Placement.Request with{Anchors=[new("fixed",new(44,30))]}}}}).ToArray()};
        var rejected=await Throws<MetadataRegenerationException>(()=>Task.FromResult(s.Apply(Edit(s,"overlap",overlap))));
        Check(rejected.ReasonCode.StartsWith("METADATA_REGIONAL_PHYSICAL_OVERLAP"),"global source collision survives attributed refusal");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();await Throws<OperationCanceledException>(()=>Task.FromResult(s.Apply(Edit(s,"cancelled",definition),cancelled.Token)));}
        Check(Json(s.Snapshot)==before,"stale, impossible and cancelled edits preserve exact accepted snapshot");
        var budget=await Throws<MetadataRegenerationException>(()=>Task.FromResult(s.Apply(Edit(s,"tiny-budget",rich with{MaximumWork=1}))));
        Check(budget.ReasonCode.StartsWith("METADATA_REGIONAL_WORK_BUDGET")&&Json(s.Snapshot)==before,"shared work failure is explicit and preserves accepted state");
        var excessive=await Throws<ArgumentException>(()=>Task.FromResult(s.Apply(Edit(s,"excessive",rich with{Purpose=new string('x',17*1024*1024)}))));
        Check(excessive.Message=="METADATA_STORAGE_SNAPSHOT_LIMIT"&&Json(s.Snapshot)==before,"source16MiB limit refuses before acceptance");
        east.Calls=west.Calls=0;
        var connectionEdit=rich with{Connections=rich.Connections.Select(c=>c with{Profile=c.Profile with{Radius=.35}}).ToArray()};
        var width=s.Apply(Edit(s,"wider-journey",connectionEdit));
        Check(east.Calls==0&&west.Calls==0&&width.Report.Regions.All(r=>r.Reused),"connection-only edit retains both complete content packets");
        Check(width.World.Connections[0].Definition.Profile.Radius==.35&&Json(width.World)==Json(engine.Generate(connectionEdit)),"wider physical journeys requalify and equal complete generation");
        var terrainEdit=connectionEdit with{Surface=connectionEdit.Surface with{Terrain=connectionEdit.Surface.Terrain! with{GradientY=.006}}};
        var terrain=s.Apply(Edit(s,"shared-terrain",terrainEdit));
        Check(terrain.Report.Regions.All(r=>!r.Reused)&&Json(terrain.World)==Json(engine.Generate(terrainEdit)),"shared terrain invalidates both contexts and matches clean full geometry");
        var ownership=terrainEdit with{Regions=terrainEdit.Regions.Select(r=>r with{Ownership=new(r.Ownership.Vertices.Select(p=>p.X==45?p with{X=46}:p).ToArray())}).ToArray()};
        var boundaries=s.Apply(Edit(s,"ownership",ownership));
        Check(boundaries.Report.Regions.All(r=>!r.Reused)&&Json(boundaries.World)==Json(engine.Generate(ownership)),"ownership edit invalidates complete contexts without resizing content");
        var rollback=s.Apply(Edit(s,"rollback",definition));
        Check(rollback.Report.WorldSha256==initial.Report.WorldSha256,"explicit source rollback restores exact original world");
        east.Calls=west.Calls=0;east.Version++;
        var changedDependency=s.Apply(Edit(s,"dependency",definition));
        Check(east.Calls>0&&west.Calls==0&&!changedDependency.Report.Regions.Single(r=>r.RegionId=="east").Reused&&changedDependency.Report.Regions.Single(r=>r.RegionId=="west").Reused,"only changed registered source dependency invalidates packet");
        before=Json(s.Snapshot);west.During=()=>east.Version++;
        var drift=await Throws<MetadataRegenerationException>(()=>Task.FromResult(s.Apply(Edit(s,"drift",Rich(definition)))));west.During=null;
        Check(drift.ReasonCode.StartsWith("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN"),"reused dependency changed by later generation refuses before commit");
        east.Version=1;Check(Json(s.Snapshot)==before,"dependency failure cannot install partially changed packets or caches");
        var retried=s.Apply(Edit(s,"retry-drift",Rich(definition)));
        Check(Json(retried.World)==Json(engine.Generate(Rich(definition))),"failed candidate caches cannot poison the next accepted edit");
        await s.PublishAsync(Path.Combine(output,"accepted"));
        await Throws<ArgumentException>(()=>MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"accepted/generation-session.json")));
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"accepted/generation-session.json"),resolver:new Resolver());
        Check(Json(restored.Snapshot)==Json(s.Snapshot),"saved source history derives the same snapshot");
        await PortableHistory(output);
        await OperationCases(output);
        Console.WriteLine("PASS: complete regional transactional edits, whole/stage reuse, shared source and dependency invalidation, physical constraint rollback, immutable snapshots, history replay, atomic publication and reporting lifecycles.");
    }
    private static async Task PortableHistory(string output)
    {
        var d=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());var g=new WorldMetadataGenerator();var engine=new MetadataRegionalWorldGenerator(g);var s=new MetadataRegionalGenerationSession(g,d);
        await s.PublishAsync(Path.Combine(output,"portable-initial"));var baseline=s.Snapshot;
        var request=Edit(s,"richer",Rich(d));await File.WriteAllTextAsync(Path.Combine(output,"richer-edit.json"),Json(request));
        var rich=await s.ApplyAndPublishAsync(request,Path.Combine(output,"portable-richer"));
        Check(Json(rich.World)==Json(engine.Generate(request.Definition)),"portable richer publication matches complete generation");
        string before=Json(s.Snapshot);await Throws<IOException>(()=>s.ApplyAndPublishAsync(Edit(s,"existing",d),Path.Combine(output,"portable-richer")));
        Check(Json(s.Snapshot)==before,"existing output refuses without accepting proposal");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"portable-richer/generation-session.json"));
        Check(Json(restored.Snapshot)==before,"source-only checkpoint replay restores reports and complete world");
        var rollback=await restored.ApplyAndPublishAsync(Edit(restored,"rollback",d),Path.Combine(output,"portable-rollback"));
        Check(rollback.Report.WorldSha256==baseline.Report.WorldSha256,"saved history rollback restores exact original world");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(Path.Combine(output,"portable-richer/generation-session.json"));
        await File.WriteAllTextAsync(Path.Combine(output,"tampered-report.json"),Json(checkpoint with{Edits=checkpoint.Edits.Select(e=>e with{Report=e.Report with{WorldSha256="changed"}}).ToArray()}));
        await Throws<ArgumentException>(()=>MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"tampered-report.json")));
        await File.WriteAllTextAsync(Path.Combine(output,"tampered-world.json"),Json(checkpoint with{WorldSha256="changed"}));
        await Throws<ArgumentException>(()=>MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"tampered-world.json")));
        var forged=System.Text.Json.Nodes.JsonNode.Parse(Json(checkpoint))!;forged["Caches"]=new System.Text.Json.Nodes.JsonObject();
        await File.WriteAllTextAsync(Path.Combine(output,"forged-cache.json"),forged.ToJsonString());
        await Throws<JsonException>(()=>MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"forged-cache.json")));
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{InitialNodes=10,RichNodes=11,InitialSources=6,RichSources=7,WholePacketReuse=rich.Report.Regions.Count(r=>r.Reused),baseline.Report.WorldSha256,RichWorldSha256=rich.Report.WorldSha256,RollbackWorldSha256=rollback.Report.WorldSha256}));
    }
}
