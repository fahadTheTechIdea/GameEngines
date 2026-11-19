using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainDemandTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Terrain demand: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7*Math.Max(1,Math.Abs(b)),message);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool richer=false)
    {
        var e=WorldAreaPopulationTests.Example();var source=e.Recipe.PopulationSource!;double scale=richer?1.1:1;
        var environment=source.Recipe.Environment! with{Fields=source.Recipe.Environment!.Fields.Select(f=>f.Id=="fertility"?f with{Offset=f.Offset*scale,Terms=f.Terms.Select(t=>t with{Coefficient=t.Coefficient*scale}).ToArray()}:f).ToArray()};
        var rules=new[]{"lowland","highland","woodland"}.Select(id=>new MetadataTerrainDemandRule(id,"Derive required content from the complete "+id+" habitat","demand:habitat:"+id,e.Recipe.Id,id+"-features",id,id=="woodland"?80:50,"fertility",id+"-habitat")).ToArray();
        return(e.Recipe with{Environment=environment,PopulationSource=source with{Recipe=source.Recipe with{Environment=environment}},TerrainDemands=new(rules),
            RemainingScopes=["Complete source compounds plus all terrain-demanded habitat members. Catalog minima and source sizes survive; amount per member and world intent are authored. Automatic world design, additional strategies and world-scale acceptance remain open. Godot deferred."]},e.Catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Mini(bool excluded=false)
    {
        const string id="terrain-demand-mini";var boundary=Box(0,0,10,4);var properties=new Dictionary<string,JsonElement>();
        var root=new WorldMetadataArchetype("root","region","Source habitat",properties,[],[],[]);var sourceCatalog=new WorldMetadataCatalog(1,"terrain-demand-mini-catalog",[root]);
        var surface=new WorldSurfaceDefinition(1,"habitat","meters",boundary,excluded?[new("strip","Protected strip reduces habitat","test:strip",Box(8,0,2,4))]:[],[new("ground","ground","Ground habitat","test:ground",boundary,0,new(0,0),0,0,0)]);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("density","Linear habitat density","test:density","weight",1,[new(WorldFieldInput.X,.1)]),new("zero","No habitat demand","test:zero","weight",0,[]),new("negative","Invalid density","test:negative","weight",-1,[])]);
        var profile=new WorldSuitabilityProfile("ground","Required ground","test:ground",["ground"],[],[]);
        var source=new WorldMetadataRecipe(1,id,"framework-reference",sourceCatalog.Id,"root","habitat",MetadataCoverage.PartialScope,["Technical source habitat only"],new Dictionary<string,int>(),new(["x","y"],"meters"),[1,1],Surface:surface,Environment:fields,
            Regions:new("habitat-regions","Measure usable habitat","test:regions",boundary,profile,[new("home","Required habitat","test:home",new(2,2),1)]));
        var feature=WorldPrescribedPopulationTests.Example().Catalog.Archetypes.Single(a=>a.Id=="feature");
        var catalog=sourceCatalog with{Archetypes=[root with{Parts=[new("features","feature",2,"At least two required features")]},feature]};
        var placement=new MetadataPlacementDefinition(new("habitat-placement","All terrain-derived habitat content","test:placement",boundary,[new("features","Required features","test:features",null,profile,RegionId:"home")],[],[],Execution:WorldPlacementExecution.Prescribed),
            [new("features",id,true,PartId:"features",Population:new("Represent the complete habitat","test:area",Region:new("home",Area:new())))]);
        return(source with{PopulationSource=new(source,sourceCatalog),Placement=placement,TerrainDemands=new([new("features","Derive content from suitable habitat","test:demand",id,"features","home",10)])},catalog);
    }
    public static async Task MeasureAsync()
    {
        var generator=new WorldMetadataGenerator();var planner=new MetadataTerrainDemandPlanner();var e=Mini();var source=generator.Generate(e.Recipe.PopulationSource!.Recipe,e.Recipe.PopulationSource.Catalog);
        var environment=new WorldEnvironmentQuery(new(source.Surface!),source.EnvironmentPlan!.Definition);var domain=new WorldAreaPopulationDomain(source.RegionPlan!,"home");
        var uniform=WorldPopulationGeometry.MeasureArea(domain,environment);Near(uniform.Area,40,"uniform exact area");Near(uniform.Mass,40,"uniform exact mass");
        Near(WorldPopulationGeometry.MeasureArea(domain,environment,"density").Mass,60,"independent linear integral");Near(WorldPopulationGeometry.MeasureArea(domain,environment,"zero").Mass,0,"zero measure without a centroid");
        var hole=source.Surface! with{Exclusions=[new("hole","Missing terrain","test:hole",Box(4,1,2,2))]};var holeEnvironment=new WorldEnvironmentQuery(new(hole),source.EnvironmentPlan.Definition);
        var holeRegions=new WorldRegionPlanner().Plan(holeEnvironment,source.RegionPlan!.Request);
        Near(WorldPopulationGeometry.MeasureArea(new(holeRegions,"home"),holeEnvironment).Area,36,"holed measure independent of invalid centroid");
        Reject(()=>WorldPopulationGeometry.Area(new(holeRegions,"home"),holeEnvironment,new(),1),"WORLD_AREA_CENTROID_OUTSIDE");
        Reject(()=>WorldPopulationGeometry.MeasureArea(domain,environment,"negative"),"WORLD_AREA_NEGATIVE_DENSITY");
        var rule=e.Recipe.TerrainDemands!.Rules[0];var plan=planner.Plan(e.Recipe.TerrainDemands,source);Check(plan.Results[0].Count==4,"exact whole count");
        Check(planner.Plan(new([rule with{DensityFieldId="density",AmountPerMember=11}]),source).Results[0].Count==5,"fractional weighted count floors explicitly");
        Reject(()=>planner.Plan(new([rule with{AmountPerMember=40/(4-1e-10)}]),source),"METADATA_TERRAIN_DEMAND_COUNT_AMBIGUOUS");
        Reject(()=>planner.Plan(new([rule with{DensityFieldId="zero"}]),source),"METADATA_TERRAIN_DEMAND_EMPTY");
        Reject(()=>planner.Plan(new([rule with{MaximumCount=3}]),source),"METADATA_TERRAIN_DEMAND_COUNT_LIMIT");
        Reject(()=>planner.Plan(new([rule with{AmountPerMember=0}]),source),"METADATA_TERRAIN_DEMAND_RULE_INVALID");
        Reject(()=>planner.Plan(new([rule with{AmountPerMember=double.NaN}]),source),"METADATA_TERRAIN_DEMAND_RULE_INVALID");
        Reject(()=>planner.Plan(new([rule with{AmountPerMember=double.PositiveInfinity}]),source),"METADATA_TERRAIN_DEMAND_RULE_INVALID");
        Reject(()=>planner.Plan(new([rule,rule with{Id="duplicate-binding"}]),source),"METADATA_TERRAIN_DEMAND_RULE_INVALID");
        Reject(()=>planner.Plan(new([rule],MaximumWork:plan.Work-1),source),"WORLD_ROUTE_WORK_BUDGET");Check(planner.Plan(new([rule],MaximumWork:plan.Work),source).Work==plan.Work,"exact shared work cap");
        Reject(()=>planner.Plan(new([rule with{AllocationId="missing"}]),source),"WORLD_AREA_ALLOCATION_REQUIRED");
        Reject(()=>planner.Plan(new([rule with{DensityFieldId="missing"}]),source),"WORLD_AREA_PATTERN_INVALID");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(new([rule]),source,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var world=generator.Generate(e.Recipe,e.Catalog);Check(world.Nodes.Count==5&&world.Nodes.Count(n=>n.TerrainDemandRuleId=="features")==4,"four derived source-sized members");
        var siblingCatalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[..a.Parts,new("features-extra","label",1,"Unrelated sibling program")]}:a),new("label","label","Nonphysical metadata label",new Dictionary<string,JsonElement>(),[],[],[])]};
        var siblingWorld=generator.Generate(e.Recipe,siblingCatalog);Check(siblingWorld.Nodes.Count==6&&siblingWorld.Nodes.Count(n=>n.TerrainDemandRuleId=="features")==4,"part-name prefixes do not merge unrelated programs");
        var duplicate=world.Nodes.First(n=>n.TerrainDemandRuleId is not null);
        Reject(()=>new MetadataTerrainDemandPlanner().ValidateDocument(world with{Nodes=[..world.Nodes,duplicate]}),"METADATA_TERRAIN_DEMAND_MEMBERS_MISMATCH");
        Check(world.PlacementPlan!.PopulationAreas!.Single().NodeIds.Count==4,"derived content feeds physical area grammar");
        var wet=Mini(true);var changed=generator.Generate(wet.Recipe,wet.Catalog);Check(changed.TerrainDemandPlan!.Results[0].Count==3&&changed.Nodes.Count==4,"protected terrain changes quantity and retained geometry");
        Near(changed.TerrainDemandPlan.Results[0].Measure.Area,32,"independent remaining habitat area");
        var raised=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Count=5}]}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,raised),"METADATA_TERRAIN_DEMAND_BELOW_CATALOG_MINIMUM");
        var conflict=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{CountParameter="manual"}]}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,conflict),"METADATA_TERRAIN_DEMAND_COUNT_CONFLICT");
        Reject(()=>generator.Generate(e.Recipe with{TerrainDemands=new([rule with{PartId="missing"}])},e.Catalog),"METADATA_TERRAIN_DEMAND_BINDING_UNUSED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{TerrainDemandPlan=null}),"METADATA_TERRAIN_DEMAND_PLAN_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{TerrainDemandPlan=world.TerrainDemandPlan! with{Results=[world.TerrainDemandPlan.Results[0] with{Count=3}]}}),"METADATA_TERRAIN_DEMAND_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("terrain","Protect additional ground","test:terrain",before.World.RecipeSha256,before.World.CatalogSha256,wet.Recipe,wet.Catalog));
        Check(Json(after.World)==Json(changed),"terrain edit equals clean generation");Check(after.Report.Stages.Single(s=>s.Id=="content").DependsOn.Contains("terrain-demand")&&!after.Report.Stages.Single(s=>s.Id=="content").Reused,"measured demand invalidates content");
        try{session.Apply(new("too-few","Required catalog minimum no longer fits habitat","test:min",after.World.RecipeSha256,after.World.CatalogSha256,wet.Recipe,raised));throw new Exception("Invalid edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed edit preserves accepted world");
        string folder=Path.Combine(Path.GetTempPath(),"terrain-demand-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(folder);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"source-history checkpoint replay");
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world-metadata.json")))==Json(changed),"document read replays demand source and content");
        Console.WriteLine("PASS: independent uniform/linear/holed/zero measurement, exact/fractional/ambiguous counts, bounds/cancellation, minima/conflicts/unused binding, terrain-driven physical counts, provenance/tamper, incremental content invalidation, rollback and source-history/file replay.");
    }
    public static async Task RunAsync()
    {
        await MeasureAsync();var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);Qualify(world,e.Catalog,false);
        var richer=Example(true);var counter=generator.Generate(richer.Recipe,richer.Catalog);Qualify(counter,richer.Catalog,true);
        var a=world.PlacementPlan!.Definition.SourceWorld!;var b=counter.PlacementPlan!.Definition.SourceWorld!;
        Check(Json(a.Nodes)==Json(b.Nodes)&&Json(a.RoutePlan)==Json(b.RoutePlan)&&Json(a.RegionPlan)==Json(b.RegionPlan)&&Json(a.LocalLayoutPlan)==Json(b.LocalLayoutPlan),"density changes demanded content while source compounds/regions/allocations/passages remain exact");
        Check(world.Nodes.Count==248&&counter.Nodes.Count==270,"complete generated content grows from measured habitat");
        Check(counter.AccessPlan!.Sites.Count(s=>s.Occupancy==MetadataOccupancyKind.Solid)>256,"positive case exceeds the search-surface solid projection limit");
        Check(new MetadataPhysicalAccessPlanner().RouteSurface(counter.Surface!,counter.AccessPlan).PhysicalObstacles.Count==counter.AccessPlan!.Sites.Count(s=>s.Occupancy==MetadataOccupancyKind.Solid),"every solid survives the separate query layer");
        var longest=counter.RoutePlan!.Routes.SelectMany(r=>r.Forward.Path!.Points.Zip(r.Forward.Path.Points.Skip(1),(a,b)=>(A:a,B:b))).MaxBy(s=>s.A.DistanceTo(s.B));
        var middle=new WorldPoint((longest.A.X+longest.B.X)/2,(longest.A.Y+longest.B.Y)/2);string changedId=counter.Nodes.First(n=>n.TerrainDemandRuleId is not null).Id;
        var blocked=counter.AccessPlan with{Sites=counter.AccessPlan.Sites.Select(s=>s.NodeId==changedId?s with{Footprint=Box(middle.X-.2,middle.Y-.2,.4,.4)}:s).ToArray()};
        Reject(()=>new MetadataRoutePlanner().Plan(counter.RoutePlan.Rules,counter.Surface!,counter.Space,counter.Nodes,counter.Connections,
            accessPlan:blocked,regions:counter.RegionPlan,localLayouts:counter.LocalLayoutPlan,placement:counter.PlacementPlan,environment:counter.EnvironmentPlan!.Definition,budgetDefinition:counter.RoutePlan.Budget),"METADATA_POPULATION_PASSAGE_BLOCKED");
        await new WorldMetadataSchematicExporter().CreateAsync(world);
        Console.WriteLine("PASS: terrain-derived expansion retains six complete compounds, every catalog minimum and nineteen source connections; baseline 219 environmental members/248 nodes/247 physical; richer density 241 environmental members/270 nodes/269 physical. Automatic world intent and broad world-scale acceptance remain open.");
    }
    private static void Qualify(WorldMetadataDocument world,WorldMetadataCatalog catalog,bool richer)
    {
        var expected=richer?new[]{110,129,2}:new[]{100,117,2};var ids=new[]{"lowland","highland","woodland"};
        for(int i=0;i<ids.Length;i++){var result=world.TerrainDemandPlan!.Results.Single(r=>r.RuleId==ids[i]);Check(result.Count==expected[i],"independent rational-integral floor for "+ids[i]);var trace=world.PlacementPlan!.PopulationAreas!.Single(t=>t.RegionId==ids[i]);Check(trace.NodeIds.Count==expected[i],"all derived members have physical area positions");Near(trace.Plan.Mass,result.Measure.Mass,"count and position share mass owner");}
        Check(world.AccessPlan!.Sites.Count==world.Nodes.Count-1&&world.RoutePlan!.Routes.Count==19&&world.PlacementPlan!.ProgramInstances!.Count==14,"all physical constituents and nested programs retained");
        var placement=world.PlacementPlan!;var routes=world.RoutePlan!;
        var source=placement.Definition.SourceWorld!;foreach(var n in source.Nodes)Check(Json(n)==Json(world.Nodes.Single(x=>x.Id==n.Id)),"source member unchanged");foreach(var route in source.RoutePlan!.Routes)Check(Json(route)==Json(routes.Routes.Single(r=>r.ConnectionId==route.ConnectionId)),"source passage unchanged");
        WorldConnectedGrowthTests.Independent(world,catalog);WorldMetadataGenerator.ValidateDocument(world);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example destination exists");
        foreach(var (name,richer) in new[]{("baseline",false),("richer-density",true)}){var e=Example(richer);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));}
    }
}
