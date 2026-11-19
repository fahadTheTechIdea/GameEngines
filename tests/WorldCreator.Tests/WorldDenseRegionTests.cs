using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldDenseRegionTests
{
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private static void Check(bool value,string message){if(!value)throw new Exception("Dense regions: "+message);}
    private static void Near(double value,double expected,string message)=>Check(Math.Abs(value-expected)<=Math.Max(1,Math.Abs(expected))*1e-10,message+" "+value+" / "+expected);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static WorldSurfaceDefinition Surface(int size=513,bool basin=true)
    {
        int middle=(size-1)/2;var boundary=Box(0,0,size-1,size-1);
        return new(1,"dense-terrain","meters",boundary,[],[],HeightField:new(1,"dense-field","land","Full-resolution protected terrain","dense:terrain","meters",new(0,0),1,1,size,size,
            Enumerable.Range(0,size*size).Select(i=>basin?(double)(Math.Abs(i%size-middle)+Math.Abs(i/size-middle)):0).ToArray()));
    }
    private static WorldEnvironmentDefinition Fields()=>new(1,"dense-fields",[new("height","Exact source elevation","dense:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
    private static WorldSuitabilityProfile Profile(double maximum)=>new("usable","Required continuous terrain support","dense:usable",["land"],
        [new("altitude","height","Bounded game altitude","dense:altitude",0,maximum)],[]);
    internal static WorldRegionPlan Plan(WorldSurfaceDefinition surface,double maximum=128)
    {
        double middle=(surface.HeightField!.Columns-1)/2d;
        var query=new WorldEnvironmentQuery(new(surface),Fields());
        return new WorldRegionPlanner().Plan(query,new("dense-region","Preserve full-resolution shape within a small face budget","dense:region",surface.Boundary!,Profile(maximum),
            [new("play","Required connected play region","dense:seed",new(middle,middle),1)],MaximumCells:32,MaximumWork:200000000));
    }
    public static void Probe()
    {
        var watch=Stopwatch.StartNew();var plan=Plan(Surface());
        Near(plan.SuitableArea,32768,"analytic 128-radius diamond");
        Console.WriteLine($"PASS: dense 513x513 field, area={plan.SuitableArea}, retained={plan.Regions.Single().Cells.Count}, work={plan.Work}, seconds={watch.Elapsed.TotalSeconds}");
    }
    public static void Lake()
    {
        var source=Surface();string original=Json(source);var watch=Stopwatch.StartNew();
        var drainage=new WorldDrainagePlanner().Plan(source,new(1,"dense-drainage","Explicit retained basin and source","dense:flow","liters-per-second",
            [new("sink",256,256,WorldDrainageTerminalKind.RetainedBasin,"Protected terrain minimum","dense:sink")],
            [new("spring",512,256,16,"Explicit game inflow","dense:source")],MaximumSamples:263169,MaximumWork:200000000));
        var definition=new WorldWaterDefinition(1,"dense-water","water","Exact dense retained body","dense:water",1000,.5,MaximumPatches:32,MaximumWork:200000000,
            RetainedBasins:[new("lake","sink","Required retained basin","dense:lake",128,source.Boundary!,["land"],MinimumReceived:16)]);
        var owner=new WorldWaterPlanner();var water=owner.Plan(drainage,definition);var body=water.RetainedBodies!.Single();
        Near(body.Region.Area,32768,"independent dense diamond area");Near(body.Capacity,2d*128*128*128/3,"independent pyramid capacity");
        Check(body.Region.Cells.Count==4&&body.BedFaces.Count==4&&water.Patches.Count==4,"four exact native terrain planes, no raw mesh patch explosion");
        Check(body.BedFaces.All(f=>f.MinimumDepth>=-1e-8&&f.MaximumDepth<=128&&f.Capacity>0)&&body.Received==16,"all bed witnesses and source receipt retained");
        var occupied=owner.Apply(drainage,water);Check(Json(occupied.HeightField)==Json(source.HeightField)&&Json(source)==original,"full original field remains immutable");
        var route=new WorldSurfaceRoutePlanner().Plan(occupied,new("boat","Explicit level-water journey","dense:boat",new(255,256),new(257,256),
            new("boat","Declared level lake traversal","dense:boat",.1,0,new Dictionary<string,double>{{"water",1}}),new(.1,[],MaximumWork:20000000)));
        Check(route.Status==WorldRouteStatus.Found,"actual full-width boat path uses occupied water");
        string output=Path.Combine("generated","dense-lake-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"lake.json"),Json(new{Source=source,Drainage=drainage,Water=water,Route=route}));
        Console.WriteLine($"PASS: dense 513x513 drainage/lake pipeline, area={body.Region.Area}, capacity={body.Capacity}, patches={water.Patches.Count}, drainage work={drainage.Work}, water work={water.Work}, seconds={watch.Elapsed.TotalSeconds}. Packet: "+output);
    }
    public static async Task MetadataAsync(string recipeFile,string catalogFile,string cliPacket,bool partitionedHistory=false)
    {
        var generator=new WorldMetadataGenerator();var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(recipeFile);
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(catalogFile);
        var published=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(cliPacket,"world-metadata.json"));
        var session=new MetadataGenerationSession(generator,recipe,catalog);var before=session.Snapshot;
        Check(Json(before.World)==Json(published),"actual CLI packet replays and equals direct generation");
        Check(before.World.Nodes.Count==3&&before.World.Surface!.HeightField!.Heights.Count==263169&&before.World.RoutePlan!.Routes.Count==1,"complete dense metadata source, destinations and route retained");
        Check(before.World.RegionPlan!.SuitableArea==32768&&before.World.RegionPlan.Request.MaximumCells==32,"original hard region area and face bounds");
        var field=recipe.Surface!.HeightField!;var changed=recipe with{Surface=recipe.Surface with{HeightField=field with{
            Heights=Enumerable.Range(0,field.Heights.Count).Select(i=>(double)(Math.Abs(i%513-255)+Math.Abs(i/513-256))).ToArray()}}};
        MetadataEditRequest Edit(string id,MetadataSessionSnapshot state,WorldMetadataRecipe next)=>new(id,"Explicit source terrain edit","dense:"+id,state.World.RecipeSha256,state.World.CatalogSha256,next,catalog);
        var after=session.Apply(Edit("move-basin",before,changed));
        Check(Json(after.World)==Json(generator.Generate(changed,catalog)),"dense source edit equals clean generation");
        Check(after.World.RegionPlan!.SuitableArea==32768&&after.World.RegionPlan.Request.Seeds.Single().MinimumArea==32768,"terrain edit never lowers required area");
        Check(after.World.RegionPlan.Regions.Single().BoundaryEdges.SelectMany(e=>new[]{e.From.X,e.To.X}).Min()==127&&
            before.World.RegionPlan.Regions.Single().BoundaryEdges.SelectMany(e=>new[]{e.From.X,e.To.X}).Min()==128,"actual terrain region shifts one meter");
        Check(Json(after.World.Nodes)==Json(before.World.Nodes)&&after.Report.Stages.Single(s=>s.Id=="content").Reused&&!after.Report.Stages.Single(s=>s.Id=="regions").Reused,"all required content preserved and dependent geometry recomputed");
        var impossible=changed with{Surface=changed.Surface! with{HeightField=changed.Surface!.HeightField! with{Heights=changed.Surface.HeightField!.Heights.Select(h=>h+1).ToArray()}}};
        try{session.Apply(Edit("insufficient-support",after,impossible));throw new Exception("Missing hard minimum refusal");}
        catch(MetadataRegenerationException error)when(error.StageId=="regions"&&error.InnerException is ArgumentException cause&&cause.Message.StartsWith("WORLD_REGION_MINIMUM_AREA",StringComparison.Ordinal)){}
        Check(Json(session.Snapshot)==Json(after),"failed full-field edit preserves accepted world/cache/report");
        var recovered=session.Apply(Edit("recover",after,changed));Check(recovered.Report.Stages.All(s=>s.Reused),"failed terrain edit cannot poison accepted caches");
        if(partitionedHistory)
        {
            for(int i=0;i<20;i++)
            {
                var next=recovered.Recipe with{GameProfile="dense-history-"+i};
                recovered=session.Apply(Edit("label-"+i,recovered,next));
                Check(recovered.Report.Stages.All(s=>s.Id=="chunks"?!s.Reused:s.Reused),"editorial history reuses geometry and refreshes the full-world-bound chunk index");
            }
        }
        string output=Path.Combine("generated","dense-metadata-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        if(partitionedHistory)
        {
            var manifest=JsonSerializer.Deserialize<MetadataJsonManifest>(await File.ReadAllTextAsync(Path.Combine(output,"generation-session.json")),WorldJson.CreateOptions())!;
            Check(manifest.StorageFormat==MetadataJsonStorage.Format&&manifest.DocumentBytes>16777216,"complete large history partitioned without increasing per-file limit");
            var history=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(Path.Combine(output,"generation-session.json"));
            Check(history.Edits.Count==22&&history.Edits.All(e=>e.Request.Recipe.Surface!.HeightField!.Heights.Count==263169),"every complete native buffer remains in accepted history");
        }
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));
        Check(Json(restored.Snapshot)==Json(recovered),"complete 513x513 terrain edit history restores through normal checkpoint API");
        Console.WriteLine("PASS: actual dense CLI read/replay, full-field terrain relocation, protected 32768-area/32-face/budgets/content, clean equality, minimum-area refusal/rollback/cache recovery, complete normal publication and source-history checkpoint. Packet: "+output);
    }
    public static async Task BuffersAsync()
    {
        var e=WorldDrainageMetadataTests.Example();var recipe=e.Recipe with{Surface=Surface(65,false),Drainage=null};
        var generator=new WorldMetadataGenerator();var session=new MetadataGenerationSession(generator,recipe,e.Catalog,
            [new("sample",MetadataInputSource.Recipe,"/Surface/HeightField/Heights/0","Preserve individual source sample","dense:protected")]);
        var before=session.Snapshot;var values=Enumerable.Range(0,4225).Select(i=>i==0?0d:1).ToArray();
        var changed=recipe with{Surface=recipe.Surface! with{HeightField=recipe.Surface!.HeightField! with{Heights=values}}};
        MetadataEditRequest Edit(string id,MetadataSessionSnapshot state,WorldMetadataRecipe input)=>new(id,"Explicit numeric buffer replacement","dense:"+id,state.World.RecipeSha256,state.World.CatalogSha256,input,e.Catalog);
        var after=session.Apply(Edit("bulk",before,changed));var change=after.Report.Changes.Single();
        Check(change.Pointer=="/Surface/HeightField/Heights"&&change.BeforeSha256==MetadataExecutionCache.Hash(recipe.Surface!.HeightField!.Heights)&&
            change.AfterSha256==MetadataExecutionCache.Hash(values),"one buffer change authenticates every original and edited sample");
        Check(after.World.Surface!.HeightField!.Heights.SequenceEqual(values)&&Json(after.World)==Json(generator.Generate(changed,e.Catalog)),"all 4225 source values retained and clean generation equivalent");
        var protectedEdit=changed with{Surface=changed.Surface! with{HeightField=changed.Surface!.HeightField! with{Heights=values.Select((v,i)=>i==0?2:v).ToArray()}}};
        Reject(()=>session.Apply(Edit("protected",after,protectedEdit)),"METADATA_PROTECTED_INPUT_CONFLICT:sample:/Surface/HeightField/Heights/0");
        Check(Json(session.Snapshot)==Json(after),"numeric buffer grouping cannot bypass individual protected sample");
        var truncated=changed with{Surface=changed.Surface! with{HeightField=changed.Surface!.HeightField! with{Heights=values.SkipLast(1).ToArray()}}};
        try{session.Apply(Edit("truncated",after,truncated));throw new Exception("Missing malformed buffer refusal");}
        catch(MetadataRegenerationException error)when(error.StageId=="surface"&&error.ReasonCode=="WORLD_HEIGHTFIELD_INVALID"){}
        Check(Json(session.Snapshot)==Json(after),"invalid array shape cannot commit or drop a sample");
        string output=Path.Combine("generated","numeric-buffer-session-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"full buffer history and individual protection restore");
        Console.WriteLine("PASS: numeric buffers retain complete source and exact before/after digests within unchanged change-entry limit; protected element conflict, malformed-length refusal, rollback, clean equality and full checkpoint. Packet: "+output);
    }
    public static void Run()
    {
        var planner=new WorldRegionPlanner();var records=new List<object>();
        string output=Path.Combine("generated","dense-regions-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        Predicates(output);
        foreach(bool basin in new[]{false,true})
        {
            var surface=Surface(basin:basin);string original=Json(surface);var watch=Stopwatch.StartNew();var plan=Plan(surface);
            Near(plan.SuitableArea,basin?32768:262144,"independent full-field analytic area");
            Check(plan.Regions.Single().Cells.Count<=32,"original retained face budget");
            Near(plan.Regions.Single().BoundaryEdges.Sum(e=>e.From.DistanceTo(e.To)),basin?512*Math.Sqrt(2):2048,"analytic boundary length");
            foreach(var cell in plan.Regions.Single().Cells)
            {
                var sample=new WorldSurfaceQuery(surface).Sample(cell.Centroid());
                foreach(var point in cell.Vertices)
                {
                    double expected=basin?Math.Abs(point.X-256)+Math.Abs(point.Y-256):0;
                    Near(sample.Elevation!.Value+sample.GradientX!.Value*(point.X-sample.Position.X)+sample.GradientY!.Value*(point.Y-sample.Position.Y),expected,"every merged face retains exact native terrain plane");
                }
            }
            Check(Json(surface)==original,"original sample field immutable");
            records.Add(new{basin,samples=surface.HeightField!.Heights.Count,area=plan.SuitableArea,cells=plan.Regions.Single().Cells.Count,plan.Work,seconds=watch.Elapsed.TotalSeconds});
            File.WriteAllText(Path.Combine(output,basin?"basin.json":"flat.json"),Json(new{Surface=surface,Environment=Fields(),Plan=plan}));
            Console.WriteLine(Json(records[^1]));
        }
        var masked=Surface() with{Exclusions=[new("hole","Required protected terrain void","dense:hole",Box(250,250,4,4))]};
        var maskedPlan=Plan(masked);Near(maskedPlan.SuitableArea,32752,"analytic dense basin minus protected void");
        Near(maskedPlan.Regions.Single().BoundaryEdges.Sum(e=>e.From.DistanceTo(e.To)),512*Math.Sqrt(2)+16,"complete outer and hole perimeter");
        Check(!maskedPlan.Regions.Single().Cells.Any(c=>c.ContainsPoint(new(252,252))),"merging never fills the protected void");
        File.WriteAllText(Path.Combine(output,"masked.json"),Json(new{Surface=masked,Environment=Fields(),Plan=maskedPlan}));
        var scalarSurface=Surface(basin:false);var field=scalarSurface.HeightField!;
        var scalar=new WorldSampledScalarField(1,"moisture-grid","Protected sampled scalar plane","dense:scalar","relative",field.Origin,field.StepX,field.StepY,field.Columns,field.Rows,
            Enumerable.Range(0,field.Heights.Count).Select(i=>(double)(i%field.Columns)).ToArray());
        var environment=Fields() with{Fields=[..Fields().Fields,new("moisture","Sampled environmental plane","dense:moisture","relative",0,[new(WorldFieldInput.SampledField,1,scalar.Id)]),
            new("capped","Explicit environmental clamp","dense:clamp","relative",0,[new(WorldFieldInput.Field,1,"moisture")],Maximum:128)],SampledFields=[scalar]};
        var scalarRequest=new WorldRegionRequest("scalar-region","Preserve scalar and clamp faces","dense:scalar-region",scalarSurface.Boundary!,
            Profile(128) with{Requirements=[new("moisture-limit","moisture","Exact sampled threshold","dense:limit",0,256)]},
            [new("play","Required dry play region","dense:seed",new(64,256),1)],MaximumCells:32,MaximumWork:200000000);
        var scalarPlan=planner.Plan(new(new(scalarSurface),environment),scalarRequest);Near(scalarPlan.SuitableArea,131072,"analytic half-domain sampled scalar support");
        Check(scalarPlan.Regions.Single().Cells.Count==2,"actual unclamped and clamped affine faces stay distinct");
        File.WriteAllText(Path.Combine(output,"scalar.json"),Json(new{Surface=scalarSurface,Environment=environment,Plan=scalarPlan}));
        Console.WriteLine($"Dense mask cells={maskedPlan.Regions.Single().Cells.Count}; scalar cells={scalarPlan.Regions.Single().Cells.Count}; scalar work={scalarPlan.Work}");
        // A real terrain plane change cannot be merged away to satisfy a small budget.
        var rough=Surface(33,false);rough=rough with{HeightField=rough.HeightField! with{Heights=Enumerable.Range(0,1089).Select(i=>(double)((i%33+i/33)%2)).ToArray()}};
        Reject(()=>Plan(rough),"WORLD_REGION_CELL_BUDGET");
        var small=Surface(17,false);var fields=Fields();var query=new WorldEnvironmentQuery(new(small),fields);
        var request=new WorldRegionRequest("small","Bounded exact merging","dense:small",small.Boundary!,Profile(128),[new("play","Required play region","dense:seed",new(2,2),1)],MaximumCells:1);
        Reject(()=>planner.Plan(query,request),"WORLD_REGION_CELL_BUDGET"); // One cell cannot hold the two-column streaming frontier.
        var flat=Plan(small);Check(flat.Regions.Single().Cells.Count==1,"complete constant mesh reduces to its exact rectangle");Near(flat.SuitableArea,256,"flat small area");
        Check(planner.Plan(query,flat.Request with{MaximumWork=flat.Work}).Work==flat.Work,"exact shared work budget");
        Reject(()=>planner.Plan(query,flat.Request with{MaximumWork=flat.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        var noisy=scalar with{Columns=33,Rows=33,Values=Enumerable.Range(0,1089).Select(i=>(double)((i%33+i/33)%2)).ToArray()};
        var noisyEnvironment=new WorldEnvironmentQuery(new(Surface(33,false)),environment with{SampledFields=[noisy]});
        Reject(()=>planner.Plan(noisyEnvironment,scalarRequest with{Boundary=rough.Boundary!,Seeds=[scalarRequest.Seeds[0] with{Position=new(2,2)}]}),"WORLD_REGION_CELL_BUDGET");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(query,request,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        File.WriteAllText(Path.Combine(output,"measurements.json"),Json(records));
        Console.WriteLine("PASS: full 513x513 flat/four-plane field, exact terrain/area/perimeter, bounded face/work limits and nonmergeable refusal. Packet: "+output);
    }

    private static void Predicates(string output)
    {
        var sample=new WorldSurfaceSample(new(0,0),WorldSurfaceStatus.Sampled,"source","dense:source","land",0,0,0);
        WorldEnvironmentRegions.Piece Piece(double value)=>new([],new(StringComparer.Ordinal){{"field",new(value,0,0)}});
        int Count(WorldSurfaceSample second,WorldEnvironmentRegions.Piece piece,uint membership=1,WorldPolygon? left=null,WorldPolygon? right=null,WorldRegionRepresentation representation=WorldRegionRepresentation.TerrainFaces)
        {
            var faces=new WorldRegionFaces(4,new(20000000,CancellationToken.None),representation);
            faces.Add(sample,Piece(1),left??Box(0,0,1,1),1);faces.Add(second,piece,right??Box(1,0,1,1),membership);
            return faces.Complete().Count;
        }
        Check(Count(sample,Piece(1))==1,"identical adjacent convex faces merge");
        Check(Count(sample with{SourceId="other"},Piece(1))==2,"distinct terrain provenance remains separate");
        Check(Count(sample,Piece(Math.BitIncrement(1)))==2,"one-ULP environmental difference remains separate");
        Check(Count(sample with{GradientX=double.Epsilon},Piece(1))==2,"even a subnormal terrain slope remains separate");
        Check(Count(sample,Piece(1),2)==2,"different eligible profile sets remain separate");
        Check(Count(sample,Piece(1),left:Box(0,0,2,1),right:Box(0,1,1,1))==2,"no convex hull fills a concave union");
        Check(Count(sample,Piece(1),left:new([new(0,0),new(1,0),new(10,1)]),right:new([new(1,0),new(0,0),new(10,-1)]))==2,"full-edge adjacency alone cannot erase concavity");
        Check(Count(sample,Piece(1),right:Box(Math.BitIncrement(1),0,1,1))==2,"one-ULP gap is never joined");
        Check(Count(sample with{GradientX=double.Epsilon},Piece(Math.BitIncrement(1)),representation:WorldRegionRepresentation.Membership)==1,
            "membership unions eligibility and source provenance without discarding source field storage");
        Check(Count(sample with{SourceId="other"},Piece(1),representation:WorldRegionRepresentation.Membership)==2,"membership preserves distinct source identity");
        Check(Count(sample,Piece(1),2,representation:WorldRegionRepresentation.Membership)==2,"membership preserves exact eligible profile sets");
        Check(Count(sample,Piece(1),right:Box(Math.BitIncrement(1),0,1,1),representation:WorldRegionRepresentation.Membership)==2,"membership never fills an ULP gap");
        Check(Count(sample,Piece(1),left:Box(0,0,2,1),right:Box(0,1,1,1),representation:WorldRegionRepresentation.Membership)==2,"membership never fills a concavity");
        var rows=new List<object>();double n=8388608,delta=Math.ScaleB(1,-10);
        foreach(int power in new[]{-1000,-100,-10,0,10})
        {
            double scale=Math.ScaleB(1,power);
            foreach(int direction in new[]{-1,0,1})
            {
                var a=new WorldPoint(0,0);var b=new WorldPoint(scale,scale);var c=new WorldPoint(2*scale,(2+direction*.25)*scale);
                int actual=WorldRegionExact.Orientation(a,b,c);Check(actual==direction,"exact small/large orientation");rows.Add(new{a,b,c,actual});
            }
        }
        var aa=new WorldPoint(0,0);var bb=new WorldPoint(n+delta,n);var cc=new WorldPoint(n,n-delta);
        Check((bb.X*cc.Y-bb.Y*cc.X)==0&&WorldRegionExact.Orientation(aa,bb,cc)==-1,"rounded floating determinant must not erase a real corner");
        rows.Add(new{a=aa,b=bb,c=cc,actual=WorldRegionExact.Orientation(aa,bb,cc)});
        var origin=new WorldPoint(9999998,9999998);double value=1,other=Math.BitIncrement(value),gx=10000000,gy=-10000000;
        Check((value-gx*origin.X-gy*origin.Y)==(other-gx*origin.X-gy*origin.Y),"adversarial normalization actually rounds equal in binary64");
        Check(WorldRegionExact.Constant(value,gx,gy,origin)!=WorldRegionExact.Constant(other,gx,gy,origin),"exact plane identity preserves one-ULP field difference despite cancellation");
        Check(WorldRegionExact.Constant(5,2,3,new(0,0))==WorldRegionExact.Constant(10,2,3,new(1,1)),"same affine plane has origin-independent exact identity");
        File.WriteAllText(Path.Combine(output,"exact-predicates.json"),Json(new{Orientations=rows,Constants=new[]{new{value,gx,gy,origin,key=WorldRegionExact.Constant(value,gx,gy,origin)},new{value=other,gx,gy,origin,key=WorldRegionExact.Constant(other,gx,gy,origin)}}}));
    }
}
