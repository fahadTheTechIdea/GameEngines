using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;
using System.Text.Json;

internal static partial class WorldTerrainProgramAnalysisTests
{
    private static async Task Context(string output,WorldMetadataGenerator generator)
    {
        var e=WorldTerrainDemandTests.Example();var p=e.Recipe.PopulationSource!;
        var request=p.Recipe.Placement!.Request;string[] anchors=request.Anchors.Select(a=>a.Id).ToArray();
        var source=p.Recipe with{Surface=null,Origin=null,Placement=p.Recipe.Placement with{Request=request with{Anchors=[]}}};
        var recipe=e.Recipe with{PopulationSource=new(source,p.Catalog,new(anchors))};
        var analysis=generator.AnalyzeProgram(recipe,e.Catalog);var actual=generator.Generate(recipe,e.Catalog);Same(analysis,actual);
        var coordination=actual.PopulationCoordinationPlan!;
        Check(coordination.SourceBindingWork==24&&coordination.Attempts.Sum(a=>a.Work)+coordination.SourceBindingWork==coordination.Work,"all pre-attempt context binding work remains attributed inside original budget");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(actual with{PopulationCoordinationPlan=coordination with{SourceBindingWork=0}}),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        Check(analysis.Nodes.Count==248&&analysis.TerrainAnalysis!.Plan.Results.Sum(r=>r.Count)==219,"explicit bound context retains complete measured program");
        Reject(()=>generator.AnalyzeProgram(recipe with{Surface=null,Origin=null},e.Catalog),"METADATA_POPULATION_CONTEXT_REQUIRED");
        Reject(()=>generator.AnalyzeProgram(recipe with{Placement=recipe.Placement! with{Request=recipe.Placement.Request with{Anchors=[]}}},e.Catalog),"METADATA_POPULATION_CONTEXT_ANCHOR_REQUIRED");
        var templateAnalysis=generator.AnalyzeProgram(e.Recipe with{Origin=null},e.Catalog);
        Check(templateAnalysis.Nodes.All(n=>n.Position is null),"resolved source counts do not need an invented consumer origin");
        await generator.PublishAsync(recipe,e.Catalog,actual,Path.Combine(output,"context"),null,default);
        await File.WriteAllTextAsync(Path.Combine(output,"context/program-analysis.json"),Json(analysis));
        await File.WriteAllTextAsync(Path.Combine(output,"context/unbound-consumer-analysis.json"),Json(templateAnalysis));
    }
    private static void Refusals(WorldMetadataGenerator generator)
    {
        var e=WorldTerrainDemandTests.Mini();var a=generator.AnalyzeProgram(e.Recipe,e.Catalog);var rule=e.Recipe.TerrainDemands!.Rules.Single();var source=e.Recipe.PopulationSource!;
        Check(a.TerrainAnalysis!.Plan.Results.Single().Count==4&&a.Work>a.TerrainAnalysis.Plan.Work,"known exact measured demand and aggregate analysis ledger");
        Check(generator.AnalyzeProgram(e.Recipe,e.Catalog,a.Work).Work==a.Work,"exact analysis work succeeds");
        Reject(()=>generator.AnalyzeProgram(e.Recipe,e.Catalog,a.Work-1),"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{TerrainDemands=new([rule],a.TerrainAnalysis.Plan.Work-1)},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{PopulationSource=null},e.Catalog),"METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{TerrainDemands=new([rule with{RegionId="missing"}])},e.Catalog),"WORLD_AREA_REGION_REQUIRED");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{TerrainDemands=new([rule with{DensityFieldId="negative"}])},e.Catalog),"WORLD_AREA_NEGATIVE_DENSITY");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{TerrainDemands=new([rule with{DensityFieldId="zero"}])},e.Catalog),"METADATA_TERRAIN_DEMAND_EMPTY");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{TerrainDemands=new([rule with{MaximumCount=3}])},e.Catalog),"METADATA_TERRAIN_DEMAND_COUNT_LIMIT");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{TerrainDemands=new([rule with{AmountPerMember=40/(4-1e-10)}])},e.Catalog),"METADATA_TERRAIN_DEMAND_COUNT_AMBIGUOUS");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{TerrainDemands=new([rule with{PartId="missing"}])},e.Catalog),"METADATA_TERRAIN_DEMAND_BINDING_UNUSED");
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{PopulationSource=source with{Recipe=source.Recipe with{Regions=null}}},e.Catalog),"METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        var minimum=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(t=>t.Id=="root"?t with{Parts=[t.Parts[0] with{Count=5}]}:t).ToArray()};
        Reject(()=>generator.AnalyzeProgram(e.Recipe,minimum),"METADATA_TERRAIN_DEMAND_BELOW_CATALOG_MINIMUM");
        var conflict=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(t=>t.Id=="root"?t with{Parts=[t.Parts[0] with{CountParameter="count"}]}:t).ToArray()};
        Reject(()=>generator.AnalyzeProgram(e.Recipe,conflict),"METADATA_TERRAIN_DEMAND_COUNT_CONFLICT");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{generator.AnalyzeProgram(e.Recipe,e.Catalog,token:cancel.Token);throw new Exception("Missing terrain-analysis cancellation");}catch(OperationCanceledException){}
        var composed=WorldProgramCompositionTests.Example();Reject(()=>generator.AnalyzeProgram(composed.Recipe,composed.Catalog),"METADATA_PROGRAM_ANALYSIS_COUPLED_INPUT_UNSUPPORTED");
        var original=generator.Generate(source.Recipe,source.Catalog);
        Reject(()=>generator.AnalyzeProgram(e.Recipe with{PopulationSource=null,Placement=e.Recipe.Placement! with{SourceWorld=original}},e.Catalog),"METADATA_PROGRAM_ANALYSIS_COUPLED_INPUT_UNSUPPORTED");
        var rootSources=new MetadataPlacementPlanner().RegionSources(e.Recipe.Placement!,a.Nodes,e.Recipe.Space.Unit,a.FootprintDefinitions,a.StructureDefinitions,null,new(1000000,default),default,unplaced:true);
        Check(rootSources.Count==4,"unplaced root expansion measures every dynamically required source without habitat positions");
        var binding=e.Recipe.Placement!.Bindings.Single();var path=new WorldPathPattern(new([new(1,1),new(9,1)]),2);
        var facing=e.Recipe.Placement with{Bindings=[binding with{Population=binding.Population! with{Region=null,Path=path,Facing=MetadataPopulationFacing.Along}}]};
        Reject(()=>new MetadataPlacementPlanner().RegionSources(facing,a.Nodes,e.Recipe.Space.Unit,a.FootprintDefinitions,a.StructureDefinitions,null,new(1000000,default),default,unplaced:true),"METADATA_REGION_DEMAND_POPULATION_FACING_UNRESOLVED");
        var malformed=e.Recipe.Placement with{Bindings=[binding with{Population=binding.Population! with{Path=path}}]};
        Reject(()=>new MetadataPlacementPlanner().RegionSources(malformed,a.Nodes,e.Recipe.Space.Unit,a.FootprintDefinitions,a.StructureDefinitions,null,new(1000000,default),default,unplaced:true),"METADATA_POPULATION_GRAMMAR_INVALID");
    }
    private sealed class SourceProbe(Action action):IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"terrain-source-probe";public bool Changed;
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(Changed);
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement p,int dimensions,int count,CancellationToken token=default)
        {action();return Enumerable.Range(0,count).Select(i=>(IReadOnlyList<double>)new double[dimensions]).ToArray();}
    }
    private static void Mutation()
    {
        var e=WorldTerrainDemandTests.Mini();var external=new Dictionary<string,int>{{"labels",1}};
        (WorldMetadataRecipe,WorldMetadataCatalog) Input()
        {
            var p=e.Recipe.PopulationSource!;
            var label=new WorldMetadataArchetype("label","label","Required metadata label",new Dictionary<string,JsonElement>(),[],[],[]);
            WorldMetadataCatalog Catalog(WorldMetadataCatalog c)=>c with{Archetypes=[..c.Archetypes.Select(t=>t.Id=="root"?t with{Parts=[..t.Parts,
                new("labels","label",1,"Required source label","labels",new("terrain-source-probe",JsonSerializer.SerializeToElement(new{})))]}:t),label]};
            return(e.Recipe with{Parameters=external,PopulationSource=new(p.Recipe with{Parameters=external},Catalog(p.Catalog))},Catalog(e.Catalog));
        }
        var input=Input();var original=MetadataExecutionCache.Clone(input.Item1);var probe=new SourceProbe(()=>external["labels"]=7);
        var generator=new WorldMetadataGenerator(layouts:[probe]);var a=generator.AnalyzeProgram(input.Item1,input.Item2);
        Check(external["labels"]==7&&a.Nodes.Count==6&&a.SourceProgram!.Nodes.Count==2,"source callbacks cannot mutate captured source/consumer counts");
        SameMini(a,generator.Generate(original,input.Item2));
        SourceProbe? changing=null;changing=new(()=>changing!.Changed=true);
        Reject(()=>new WorldMetadataGenerator(layouts:[changing]).AnalyzeProgram(original,input.Item2),"METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        using var cancel=new CancellationTokenSource();var cancelling=new SourceProbe(cancel.Cancel);
        try{new WorldMetadataGenerator(layouts:[cancelling]).AnalyzeProgram(original,input.Item2,token:cancel.Token);throw new Exception("Missing source-callback cancellation");}catch(OperationCanceledException){}
    }
    private static async Task Lifecycle(string output,WorldMetadataGenerator generator)
    {
        var e=WorldTerrainDemandTests.Mini();var changed=WorldTerrainDemandTests.Mini(true);
        var a=generator.AnalyzeProgram(e.Recipe,e.Catalog);var b=generator.AnalyzeProgram(changed.Recipe,changed.Catalog);
        Check(a.Nodes.Count==5&&b.Nodes.Count==4&&a.TerrainAnalysis!.Plan.Results[0].Count==4&&b.TerrainAnalysis!.Plan.Results[0].Count==3,"actual protected terrain changes required quantity above original minimum");
        Check(a.TerrainAnalysis!.HabitatSha256!=b.TerrainAnalysis!.HabitatSha256,"source terrain change changes exact measured identity");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("protect-habitat","Protect actual source terrain","analysis:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        SameMini(b,after.World);
        var raised=changed.Catalog with{Archetypes=changed.Catalog.Archetypes.Select(t=>t.Id=="root"?t with{Parts=[t.Parts[0] with{Count=5}]}:t).ToArray()};
        try{session.Apply(new("required-minimum","Refuse infeasible original habitat minimum","analysis:refusal",after.World.RecipeSha256,after.World.CatalogSha256,changed.Recipe,raised));throw new Exception("Missing refused edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed terrain edit preserves accepted source/world/history");
        string folder=Path.Combine(output,"session");await session.PublishAsync(folder);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"source-history restored exactly");
        foreach(var (name,example,analysis) in new[]{("mini",e,a),("protected",changed,b)})
        {
            var world=generator.Generate(example.Recipe,example.Catalog);SameMini(analysis,world);
            string dir=Path.Combine(output,name);await generator.PublishAsync(example.Recipe,example.Catalog,world,dir,null,default);
            await File.WriteAllTextAsync(Path.Combine(dir,"program-analysis.json"),Json(analysis));
        }
    }
    private static void SameMini(MetadataProgramAnalysis a,WorldMetadataDocument w)
    {
        Check(Json(a.Nodes.Select(n=>n with{Position=null}))==Json(w.Nodes.Select(n=>n with{Position=null}))&&Json(a.TerrainAnalysis!.Plan)==Json(w.TerrainDemandPlan),"small analytical program equals actual geometry/count result");
        Check(a.TerrainAnalysis!.HabitatSha256==MetadataTerrainDemandPlanner.HabitatSha256(MetadataTerrainDemandPlanner.Source(w.PlacementPlan!.Definition.SourceWorld!)),"small actual habitat identity");
    }
}
