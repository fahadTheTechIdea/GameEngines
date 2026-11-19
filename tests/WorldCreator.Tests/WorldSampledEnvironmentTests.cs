using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSampledEnvironmentTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Sampled environment: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-9*Math.Max(1,Math.Abs(b)),message+$" ({a}/{b})");
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldSurfaceDefinition Surface(Func<int,int,double>? height=null)=>new(1,"land","meters",Box(0,0,4,4),[],[],
        HeightField:new(1,"terrain","land","Protected source elevation","scalar:terrain","meters",new(0,0),1,1,5,5,
            Enumerable.Range(0,25).Select(i=>height?.Invoke(i%5,i/5)??0).ToArray()));
    private static WorldSampledScalarField Scalar(WorldSurfaceDefinition surface,IReadOnlyList<double> values)=>new(1,"moisture-grid","Explicit scalar field","scalar:moisture","relative",
        surface.HeightField!.Origin,1,1,5,5,values);
    private static WorldEnvironmentDefinition Environment(WorldSampledScalarField scalar)=>new(1,"environment",
        [new("moisture","Interpolated moisture","scalar:sample","relative",0,[new(WorldFieldInput.SampledField,1,scalar.Id)]),
        new("capped","Clamped moisture","scalar:clamp","relative",0,[new(WorldFieldInput.Field,1,"moisture")],Maximum:.5),
        new("excess","Correlated excess","scalar:excess","relative",0,[new(WorldFieldInput.Field,1,"moisture"),new(WorldFieldInput.Field,-1,"capped")])],[scalar]);
    private static WorldSuitabilityProfile Dry()=>new("dry","Require dry ground","scalar:dry",["land"],[new("dry","moisture","Reject wet footprint","scalar:dry",0,.2)],[]);
    private static WorldWindMoistureDefinition Wind()=>new(1,"weather","Declared game moisture transport","wind:rule",new(1,0),1,.2,0,new Dictionary<string,double>());
    public static async Task RunAsync()
    {
        var surface=Surface();var values=Enumerable.Range(0,25).Select(i=>2.0*(i%5)+3*(i/5)).ToArray();
        var source=Scalar(surface,values);var geometry=WorldHeightFieldQuery.Describe(surface.HeightField!);var query=new WorldSampledScalarQuery(source,geometry);
        foreach(var p in new[]{new WorldPoint(.75,.25),new(.25,.75),new(2,2),new(4,4)})
        {var s=query.Sample(p);Near(s.Value,2*p.X+3*p.Y,"analytic plane");Near(s.GradientX,2,"X derivative");Near(s.GradientY,3,"Y derivative");}
        values[0]=999;Near(query.Sample(new(0,0)).Value,0,"input array isolated");
        var nonplane=source with{Values=Enumerable.Range(0,25).Select(i=>i==6?6.0:i==1?2:i==5?3:0).ToArray()};
        var triangles=new WorldSampledScalarQuery(nonplane,geometry);
        Near(triangles.Sample(new(.75,.25)).Value,2.5,"lower triangle");Near(triangles.Sample(new(.25,.75)).Value,3,"upper triangle");
        Near(triangles.Sample(new(.5,.5)).GradientX,2,"same diagonal ownership as elevation");
        Reject(()=>query.Sample(new(5,0)),"WORLD_SCALAR_SAMPLE_UNCOVERED:");
        Reject(()=>new WorldSampledScalarQuery(source with{StepX=2},geometry),"WORLD_SCALAR_FIELD_INVALID");
        Reject(()=>new WorldSampledScalarQuery(source with{Values=Enumerable.Repeat(double.NaN,25).ToArray()},geometry),"WORLD_SCALAR_VALUE_INVALID");
        var peak=Scalar(surface,Enumerable.Range(0,25).Select(i=>i==13?1d:0).ToArray());
        var environment=new WorldEnvironmentQuery(new(surface),Environment(peak));
        Check(new WorldPointSuitability(environment,Dry()).Evaluate(new(2,2)).Eligible,"center point misses off-center wet pocket");
        var footprint=new WorldFootprintSuitability(environment,Dry()).Evaluate(surface.Boundary!);
        Check(!footprint.Eligible&&footprint.CompleteCoverage,"whole area finds hidden scalar wet pocket");
        var regions=new WorldRegionPlanner().Plan(environment,new("dry-region","Required dry support","scalar:region",surface.Boundary!,Dry(),
            [new("dry-region","Required dry support","scalar:seed",new(.1,.1),1)]));
        Near(regions.SuitableArea,16-6*.5*.8*.8,"independent six triangle threshold area");
        var pieces=new List<double>();
        new WorldSurfaceAreaQuery(new(surface)).VisitRegions(surface.Boundary!, (sample,vertices)=>{
            if(sample.Status==WorldSurfaceStatus.Sampled)foreach(var piece in WorldEnvironmentRegions.Evaluate(environment,sample,vertices,4096,new(20000000,CancellationToken.None)))
                foreach(var p in piece.Vertices)pieces.Add(WorldEnvironmentRegions.Value(piece.Fields["excess"],sample.Position,p));
            return true;},new(20000000,CancellationToken.None),CancellationToken.None);
        Check(pieces.Any(v=>Math.Abs(v-.5)<1e-10)&&pieces.All(v=>v>=-1e-10&&v<=.5+1e-10),"clamp-created extrema preserve scalar correlations");
        var placement=new WorldPlacementRequest("site","Required exact footprint","scalar:site",surface.Boundary!,
            [new("site","Preserve exact dry footprint","scalar:site",Box(-.05,-.05,.1,.1),Dry())],[new("pin",new(3,2))],
            [new("site-pin","site","pin","Authored required site","scalar:pin",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        Check(new WorldSitePlacementPlanner().Plan(environment,placement).Status==WorldPlacementStatus.NoFeasibleAssignment,"wet scalar rejects exact site");
        Check(new WorldSitePlacementPlanner().Plan(environment,placement with{Anchors=[new("pin",new(1,1))]}).Status==WorldPlacementStatus.Found,"explicit dry site preserves full footprint");
        Reject(()=>new WorldEnvironmentQuery(new(surface),Environment(peak) with{SampledFields=null}),"WORLD_ENVIRONMENT_SCALAR_UNKNOWN:");
        Reject(()=>new WorldEnvironmentQuery(new(surface),Environment(peak) with{SampledFields=[peak,peak]}),"WORLD_ENVIRONMENT_SCALAR_DUPLICATE");
        Reject(()=>new WorldEnvironmentQuery(new(surface),Environment(peak) with{SampledFields=Enumerable.Repeat(peak,9).ToArray()}),"WORLD_ENVIRONMENT_SCALAR_BUDGET");
        var noFields=Environment(peak) with{Fields=[new("constant","No sampled capability","scalar:constant","relative",1,[])],SampledFields=[]};
        _=new WorldEnvironmentQuery(new(surface with{HeightField=null}),noFields);
        Reject(()=>new WorldEnvironmentQuery(new(surface with{HeightField=null}),Environment(peak)),"WORLD_ENVIRONMENT_SCALAR_TERRAIN_REQUIRED");
        var outside=surface with{Boundary=null,Patches=[new("outside","land","Explicit exterior overlay","scalar:outside",Box(4,0,2,2),1,new(4,0),0,0,0)]};
        Reject(()=>new WorldEnvironmentQuery(new(outside),Environment(peak)).Sample(new(5,1)),"WORLD_SCALAR_SAMPLE_UNCOVERED:");
        WindChecks();await MetadataChecks();
        Console.WriteLine("PASS: shared scalar/elevation triangles, analytic gradients, hidden wet pockets, clamp correlations, exact dry area/site, frames/snapshots/bounds; directional wind/ridge/evaporation, replay and metadata edits/rollback/publication.");
    }
    private static void WindChecks()
    {
        var surface=Surface();var planner=new WorldWindMoisturePlanner();var definition=Wind();var flat=planner.Plan(surface,definition);
        foreach(var node in flat.Nodes)
        {
            int column=node.SampleIndex%5;Near(node.Outgoing,Math.Pow(.8,column+1),"independent flat wind attenuation");
            Near(node.Precipitation,.2*Math.Pow(.8,column),"independent flat precipitation");
            Check(node.UpwindSamples.All(j=>flat.Nodes[j].Projection<node.Projection),"strict acyclic wind ordering");
            Near(node.Incoming+node.Evaporated,node.Outgoing+node.Precipitation,"local moisture ledger");
        }
        var reverse=planner.Plan(surface,definition with{Direction=new(-1,0)});
        foreach(var n in reverse.Nodes)Near(n.Outgoing,flat.Nodes[n.SampleIndex/5*5+4-n.SampleIndex%5].Outgoing,"wind reversal counterfactual");
        var ridge=Surface((x,y)=>x==2?2:0);var rain=planner.Plan(ridge,definition with{RiseCondensationPerUnit=.3});
        Check(rain.Nodes[12].Precipitation>flat.Nodes[12].Precipitation&&rain.Nodes[13].Outgoing<flat.Nodes[13].Outgoing,"preserved ridge produces windward condensation and dry lee");
        var wet=surface with{Patches=[new("source","water","Explicit evaporation source","wind:water",Box(0,0,.1,4),1,new(0,0),0,0,0)]};
        var evaporation=planner.Plan(wet,definition with{BoundaryHumidity=.2,EvaporationByKind=new Dictionary<string,double>{{"water",.4}}});
        Near(evaporation.Nodes[0].Evaporated,.4,"declared kind evaporation");Near(evaporation.Nodes[0].Outgoing,.48,"evaporation then condensation");
        var waterExample=WorldWaterMetadataTests.Example();var flow=new WorldDrainagePlanner().Plan(waterExample.Recipe.Surface!,waterExample.Recipe.Drainage!);
        var water=new WorldWaterPlanner();var occupied=water.Apply(flow,water.Plan(flow,waterExample.Recipe.Water!));
        var waterWind=definition with{BoundaryHumidity=.1,EvaporationByKind=new Dictionary<string,double>{{"water",.2}}};
        Check(planner.Plan(occupied,waterWind).Nodes[12].Outgoing>planner.Plan(flow.Surface,waterWind).Nodes[12].Outgoing,"actual generated water affects wind evaporation");
        Check(flat.Surface.Patches.Count==0&&Json(flat.Surface.HeightField)==Json(surface.HeightField),"inland air adds no ocean or terrain changes");
        var translated=surface with{HeightField=surface.HeightField! with{Origin=new(-70,40)},Boundary=Box(-70,40,4,4)};
        Check(Json(planner.Plan(translated,definition).Fields.Select(f=>f.Values))==Json(flat.Fields.Select(f=>f.Values)),"translation independent wind transport");
        Check(Json(planner.Plan(surface,definition with{Direction=new(double.Epsilon,0)}).Fields)==Json(flat.Fields),"finite subnormal direction normalization");
        planner.Validate(JsonSerializer.Deserialize<WorldWindMoisturePlan>(Json(rain),WorldJson.CreateOptions())!);
        Reject(()=>planner.Validate(flat with{Nodes=flat.Nodes.Select((n,i)=>i==0?n with{Outgoing=n.Outgoing+.1}:n).ToArray()}),"WORLD_WIND_MOISTURE_PLAN_MISMATCH");
        Reject(()=>planner.Validate(flat with{SurfaceSha256=new string('0',64)}),"WORLD_WIND_MOISTURE_PLAN_MISMATCH");
        Reject(()=>planner.Plan(surface,definition with{Direction=new(0,0)}),"WORLD_WIND_MOISTURE_DEFINITION_INVALID");
        Reject(()=>planner.Plan(surface,definition with{MaximumSamples=24}),"WORLD_WIND_MOISTURE_SAMPLE_BUDGET");
        Check(planner.Plan(surface,definition with{MaximumWork=flat.Work}).Work==flat.Work,"exact wind work limit");
        Reject(()=>planner.Plan(surface,definition with{MaximumWork=flat.Work-1}),"WORLD_WIND_MOISTURE_WORK_BUDGET");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{planner.Plan(surface,definition,cancellation.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}
    }
    private static async Task MetadataChecks()
    {
        var e=WorldDrainageMetadataTests.Example();var planner=new WorldWindMoisturePlanner();var wind=planner.Plan(e.Recipe.Surface!,Wind());
        var definition=new WorldEnvironmentDefinition(1,"wind-environment",[new("humidity","Derived humidity","wind:sample","relative",0,[new(WorldFieldInput.SampledField,1,"weather-humidity")])]);
        var recipe=e.Recipe with{Environment=planner.Apply(definition,wind),Drainage=null};var generator=new WorldMetadataGenerator();
        var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var before=session.Snapshot;
        var reverse=planner.Plan(e.Recipe.Surface!,Wind() with{Direction=new(-1,0)});var changed=recipe with{Environment=planner.Apply(definition,reverse)};
        var after=session.Apply(new("wind-edit","Explicit new derived field input","wind:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changed,e.Catalog)),"changed sampled field metadata equals clean generation");
        Check(Json(before.World.Nodes)==Json(after.World.Nodes)&&Json(before.World.Surface)==Json(after.World.Surface),"field edit preserves required three destinations and all terrain");
        var invalid=changed with{Environment=changed.Environment! with{SampledFields=changed.Environment!.SampledFields!.Select((f,i)=>i==0?f with{StepX=2}:f).ToArray()}};
        try{session.Apply(new("bad-frame","Refuse mismatched scalar frame","wind:bad",after.World.RecipeSha256,after.World.CatalogSha256,invalid,e.Catalog));throw new Exception("Missing refusal");}catch(MetadataRegenerationException){}
        Check(Json(after)==Json(session.Snapshot),"failed field edit leaves accepted world intact");
        string output=Path.Combine("generated","sampled-environment-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"sampled field source history checkpoint replays");
        string windFile=Path.Combine(output,"wind-core-reference.json");await File.WriteAllTextAsync(windFile,Json(reverse));
        _=await WorldMetadataGenerator.ReadAsync<WorldWindMoisturePlan>(windFile);
        await File.WriteAllTextAsync(Path.Combine(output,"wind-core-tampered.json"),Json(reverse with{SurfaceSha256=new string('0',64)}));
        try{await WorldMetadataGenerator.ReadAsync<WorldWindMoisturePlan>(Path.Combine(output,"wind-core-tampered.json"));throw new Exception("Missing wind replay refusal");}
        catch(ArgumentException error)when(error.Message.StartsWith("WORLD_WIND_MOISTURE_PLAN_MISMATCH",StringComparison.Ordinal)){}
        Console.WriteLine("Sampled environment packet: "+output);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        var planner=new WorldWindMoisturePlanner();
        foreach(var name in new[]{"flat","ridge-east","ridge-west","inland-dry"})
        {
            var surface=name.StartsWith("ridge",StringComparison.Ordinal)?Surface((x,y)=>x==2?2:0):Surface();
            var definition=Wind() with{Direction=name.EndsWith("west",StringComparison.Ordinal)?new(-1,0):new(1,0),
                BoundaryHumidity=name=="inland-dry"?0:1,RiseCondensationPerUnit=name.StartsWith("ridge",StringComparison.Ordinal)?.3:0};
            var plan=planner.Plan(surface,definition);await File.WriteAllTextAsync(Path.Combine(directory,name+".json"),Json(plan));
        }
    }
    public static async Task PhysicalAsync()
    {
        var e=WorldConnectedAreaTests.FullExample();var planner=new WorldWindMoisturePlanner();
        var wind=planner.Plan(e.Recipe.Surface!,Wind() with{RiseCondensationPerUnit=.3});
        WorldEnvironmentDefinition Derive(WorldEnvironmentDefinition source)=>planner.Apply(source with{Fields=source.Fields.Select(f=>f.Id=="landscape-density"?
            f with{Terms=[..f.Terms,new(WorldFieldInput.SampledField,.0001,"weather-humidity")]}:f).ToArray()},wind);
        var recipe=e.Recipe with{Environment=Derive(e.Recipe.Environment!),PopulationSource=e.Recipe.PopulationSource! with{
            Recipe=e.Recipe.PopulationSource!.Recipe with{Environment=Derive(e.Recipe.PopulationSource.Recipe.Environment!)}}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,e.Catalog);
        WorldLayeredIntegrationTests.Qualify(world,e.Catalog);
        Check(Json(world.Surface!.Terrain)==Json(e.Recipe.Surface!.Terrain)&&world.Surface.Terrain!.Operations[0].Height==1.8,
            "derived scalar input preserves original ridge controls");
        Check(world.EnvironmentPlan!.Definition.SampledFields!.Count==2&&world.TerrainDemandPlan!.Results.Single().Count>=16,
            "shared sampled humidity/rainfall drive actual density without truncating required content");
        string output=Path.Combine("generated","wind-physical-"+Guid.NewGuid().ToString("N"));
        await generator.PublishAsync(recipe,e.Catalog,world,output,null,CancellationToken.None);
        var read=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
        Check(Json(read)==Json(world),"physical scalar world publishes and replays exactly");
        await File.WriteAllTextAsync(Path.Combine(output,"wind-core-reference.json"),Json(wind));
        Console.WriteLine($"PASS: wind-derived sampled fields feed complete physical world {world.Nodes.Count}nodes/{world.AccessPlan!.Sites.Count}physical; seven complete compounds, source dimensions,16+ landscape members,82paths, original1.8m ridge and budgets preserved. Packet: {output}");
    }
}
