using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Catalogs.Modern;

internal static class WorldProgramAnalysisTests
{
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    private static void Check(bool x,string message){if(!x)throw new Exception("Program analysis: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    internal static void Run()
    {
        var generator=new WorldMetadataGenerator();
        foreach(var name in new[]{"platformer","space","dungeon"})
        {
            var e=WorldMetadataTests.Example(name);var recipe=e.Recipe with{Origin=null};string source=Json(new{recipe,e.Catalog});
            var analysis=generator.AnalyzeProgram(recipe,e.Catalog);var world=generator.Generate(e.Recipe,e.Catalog);
            Check(analysis.Nodes.All(n=>n.Position is null),name+" has no fabricated coordinates");
            Same(analysis,world);Check(Json(new{recipe,e.Catalog})==source,"source unchanged");
            Check(analysis.RelativePositions.Count==(name=="platformer"?16:name=="space"?8:0),"all authored relative layouts retained");
            Check(Json(analysis)==Json(generator.AnalyzeProgram(recipe,e.Catalog,analysis.Work)),"exact work budget replays");
            Reject(()=>generator.AnalyzeProgram(recipe,e.Catalog,analysis.Work-1),"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET");
        }
        var capacity=WorldCapacityTests.Example();var measured=generator.AnalyzeProgram(capacity.Recipe with{Origin=null},capacity.Catalog);
        Same(measured,generator.Generate(capacity.Recipe,capacity.Catalog));Check(measured.CapacityPlan!.Allocations.Single().Count==3,"capacity expands every required instance");
        var envelopes=new MetadataPlacementPlanner().RegionSources(capacity.Recipe.Placement!,measured.Nodes,capacity.Recipe.Space.Unit,measured.FootprintDefinitions,
            measured.StructureDefinitions,null,new(1000000,default),default);
        Check(envelopes.Count==3&&envelopes.Select(s=>s.Source.NodeId).Distinct().Count()==3,"root direct-part binding measures every expanded capacity instance once");
        var mix=WorldProgramMixTests.Example();var mixed=generator.AnalyzeProgram(mix.Recipe with{Origin=null},mix.Catalog);
        Same(mixed,generator.Generate(mix.Recipe,mix.Catalog));Check(mixed.Nodes.Count==17&&mixed.ProgramMixPlan is not null,"whole selected program and nested members retained");
        var rooms=WorldMetadataTests.Example("dungeon");var room=rooms.Catalog.Archetypes.Single(a=>a.Id=="room");
        var varied=rooms.Catalog with{Archetypes=rooms.Catalog.Archetypes.Select(a=>a.Id=="floor"?a with{Parts=[a.Parts[0] with{
            Variation=new("rooms","analysis-seed","Declared room alternatives","test:analysis",["room","room-alt"])}]}:a).Append(room with{Id="room-alt"}).ToArray()};
        var variation=generator.AnalyzeProgram(rooms.Recipe,varied);Same(variation,generator.Generate(rooms.Recipe,varied));
        Check(variation.VariationPlan!.Allocations.Sum(a=>a.Choices.Count)==12,"all scoped variation decisions retained");
        var registered=room with{Structure=new(ModernCatalog.Selection,"warehouse","small")};
        var registeredCatalog=rooms.Catalog with{Archetypes=rooms.Catalog.Archetypes.Select(a=>a.Id=="room"?registered:a).ToArray()};
        var registeredAnalysis=new WorldMetadataGenerator(TestCatalogs.Registry).AnalyzeProgram(rooms.Recipe,registeredCatalog);
        Check(registeredAnalysis.StructureDefinitions.Count==1&&registeredAnalysis.Dependencies.Any(d=>d.Kind=="catalog"),"exact registered source provenance");
        var registeredPlacement=new MetadataPlacementDefinition(capacity.Recipe.Placement!.Request with{Boundary=null,Anchors=[],Relations=[],
            Demands=[capacity.Recipe.Placement.Request.Demands[0] with{Id="registered",Footprint=null,Reservation=null}]},
            [new("registered","dungeon/floors-0/rooms-0",true,RotationDegrees:90)]);
        var registeredSources=new MetadataPlacementPlanner().RegionSources(registeredPlacement,registeredAnalysis.Nodes,"meters",registeredAnalysis.FootprintDefinitions,
            registeredAnalysis.StructureDefinitions,null,new(1000000,default),default);
        var registeredDemand=MetadataRegionPlanner.MeasureEnvelopes(registeredSources.Select(s=>s.Source),new(1000000,default)).Single();
        var variant=registeredAnalysis.StructureDefinitions.Single().Value.Variant;
        double expectedArea=(variant.WidthMeters+2*variant.SiteClearanceMeters)*(variant.DepthMeters+2*variant.SiteClearanceMeters);
        Check(Math.Abs(registeredDemand.EnvelopeArea-expectedArea)<1e-8&&registeredDemand.Source.SourceId.StartsWith("structure:",StringComparison.Ordinal),"rotated registered source and its exact clearance reach demand measurement");
        var population=WorldVariationTests.Example();var populated=generator.AnalyzeProgram(population.Recipe,population.Catalog);
        Same(populated,generator.Generate(population.Recipe,population.Catalog));
        Check(populated.Nodes.Count==20&&populated.SourceProgram?.Nodes.Count==12&&populated.SourceProgram.VariationPlan is not null,"complete source/population variation analysis retains source provenance");
        var demanded=WorldTerrainDemandTests.Example();var terrain=generator.AnalyzeProgram(demanded.Recipe,demanded.Catalog);
        Same(terrain,generator.Generate(demanded.Recipe,demanded.Catalog));Check(terrain.TerrainAnalysis is not null,"resolved terrain demand retains source measurement");
        var composed=WorldProgramCompositionTests.Example();Reject(()=>generator.AnalyzeProgram(composed.Recipe,composed.Catalog),"METADATA_PROGRAM_ANALYSIS_COUPLED_INPUT_UNSUPPORTED");
        Reject(()=>generator.AnalyzeProgram(rooms.Recipe,rooms.Catalog,0),"METADATA_PROGRAM_ANALYSIS_WORK_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{generator.AnalyzeProgram(rooms.Recipe,rooms.Catalog,token:cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        Reject(()=>generator.AnalyzeProgram(rooms.Recipe,rooms.Catalog with{Archetypes=rooms.Catalog.Archetypes.Select(a=>a.Id=="floor"?a with{ChildRequirements=[new("room",13,13)]}:a).ToArray()}),"METADATA_CHILD_REQUIREMENT");
        Capture();Sizes();Console.WriteLine("PASS: unplaced multi-genre hierarchy, relative coordinates, physical capacity/mix parity, repeated root sources, variations, registered provenance, capture, dependency change, exact work, size and cancellation.");
    }
    private static void Same(MetadataProgramAnalysis a,WorldMetadataDocument w)
    {
        Check(Json(a.Nodes.Select(n=>n with{Position=null}))==Json(w.Nodes.Select(n=>n with{Position=null})),"same expanded members");
        var links=a.Connections.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        Check(Json(a.Connections)==Json(w.Connections.Where(c=>links.Contains(c.Id)))&&Json(a.CapacityPlan)==Json(w.CapacityPlan)&&Json(a.VariationPlan)==Json(w.VariationPlan)&&Json(a.ProgramMixPlan)==Json(w.ProgramMixPlan),"all source links and choices survive; physical network links remain downstream");
        Check(Json(a.FootprintDefinitions)==Json(w.FootprintDefinitions??new Dictionary<string,MetadataCatalogFootprint>())&&Json(a.StructureDefinitions)==Json(w.StructureDefinitions),"same complete source definitions");
    }
    private sealed class Probe(Action action):IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"probe";public bool Changed;public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(Changed);
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement p,int d,int count,CancellationToken token=default)
        {action();return Enumerable.Range(0,count).Select(i=>(IReadOnlyList<double>)new double[d]).ToArray();}
    }
    private static void Capture()
    {
        var e=WorldMetadataTests.Example("platformer");var parameters=e.Recipe.Parameters.ToDictionary(p=>p.Key,p=>p.Value);var recipe=e.Recipe with{Origin=null,Parameters=parameters};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a with{Parts=a.Parts.Select(p=>p.Layout is null?p:p with{Layout=new("probe",JsonSerializer.SerializeToElement(new{}))}).ToArray()}).ToArray()};
        var probe=new Probe(()=>parameters["levels"]=1);var generator=new WorldMetadataGenerator(layouts:[probe]);
        Check(generator.AnalyzeProgram(recipe,catalog).Nodes.Count==17&&parameters["levels"]==1,"caller mutation during callback cannot change captured counts");
        Probe? changing=null;changing=new Probe(()=>changing!.Changed=true);
        Reject(()=>new WorldMetadataGenerator(layouts:[changing]).AnalyzeProgram(recipe,catalog),"METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        using var cancel=new CancellationTokenSource();var cancelling=new Probe(cancel.Cancel);
        try{new WorldMetadataGenerator(layouts:[cancelling]).AnalyzeProgram(recipe,catalog,token:cancel.Token);throw new Exception("Missing callback cancellation");}catch(OperationCanceledException){}
    }
    private static void Sizes()
    {
        var e=WorldMetadataTests.Example("dungeon");var leaf=e.Catalog.Archetypes.Single(a=>a.Id=="room") with{
            Properties=new Dictionary<string,JsonElement>{{"payload",JsonSerializer.SerializeToElement(new string('x',60000))}}};
        var root=e.Catalog.Archetypes.Single(a=>a.Id=="root") with{Parts=[new("rooms","room",400,"All declared rooms")],Connections=[],ChildRequirements=[],RequireWeaklyConnectedChildren=false};
        var recipe=e.Recipe with{Parameters=new Dictionary<string,int>(),AdditionalConnections=[]};
        var catalog=e.Catalog with{Archetypes=[root,leaf]};var generator=new WorldMetadataGenerator();
        Reject(()=>generator.AnalyzeProgram(recipe,catalog),"METADATA_STORAGE_SNAPSHOT_LIMIT");
        Reject(()=>generator.AnalyzeProgram(recipe,catalog with{Archetypes=[root,..Enumerable.Range(0,300).Select(i=>leaf with{Id="leaf-"+i})]}),"METADATA_STORAGE_SNAPSHOT_LIMIT");
    }
}
