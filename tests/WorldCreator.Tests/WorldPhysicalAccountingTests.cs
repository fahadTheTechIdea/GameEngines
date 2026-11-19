using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldPhysicalAccountingTests
{
    private sealed class CancellingNodes(IReadOnlyList<WorldMetadataNode> nodes,CancellationTokenSource cancellation):IReadOnlyList<WorldMetadataNode>
    {
        public int Count=>nodes.Count;
        public WorldMetadataNode this[int index]=>nodes[index];
        public IEnumerator<WorldMetadataNode> GetEnumerator(){for(int i=0;i<nodes.Count;i++){if(i==1)cancellation.Cancel();yield return nodes[i];}}
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output))throw new IOException("Evidence exists");Directory.CreateDirectory(output);
        var e=WorldSurfaceTests.Example();var baseline=new WorldMetadataGenerator().Generate(e.Recipe,e.Catalog);
        var planner=new MetadataPhysicalAccountingPlanner();var cases=new List<object>();
        WorldMetadataDocument Input(WorldPolygon[] shapes,MetadataOccupancyKind[] kinds,WorldPolygon? boundary=null)
        {
            // Analytic measurement inputs, not generated catalog/world-design fixtures.
            var nodes=shapes.Select((_,i)=>baseline.Nodes[0] with{Id="source-"+i,ArchetypeId="source-"+i,Purpose="Required source "+i}).ToArray();
            return baseline with{Nodes=nodes,FootprintDefinitions=null,Surface=baseline.Surface! with{Boundary=boundary??Box(-20,-20,40,40)},
                AccessPlan=new(new Dictionary<string,MetadataCatalogAccess>(),shapes.Select((shape,i)=>new MetadataPhysicalSite(nodes[i].Id,kinds[i],"test:source-"+i,shape,[])).ToArray(),0)};
        }
        void Check(bool value,string message){if(!value)throw new Exception("Physical accounting: "+message);}
        void Near(double? value,double expected,string message)=>Check(value is {} area&&Math.Abs(area-expected)<1e-7,message+": "+value+" / "+expected);
        void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
        MetadataPhysicalAccounting Measure(string name,WorldMetadataDocument world,double solid,double reservation,double combined,double? physical=null)
        {
            var report=planner.Measure(world);Check(report.Status==MetadataAccountingStatus.Measured,name+" measured");
            Near(report.SolidUnionArea,solid,name+" solid union");Near(report.ReservationUnionArea,reservation,name+" reservation union");Near(report.CombinedUnionArea,combined,name+" combined union");
            Near(report.PhysicalFootprintUnionArea,physical??combined,name+" physical footprint union");
            planner.Validate(report,world);cases.Add(new{Name=name,Domain=world.Surface!.Boundary,Sites=world.AccessPlan!.Sites,
                Clearances=world.PlacementPlan is {} placement?MetadataPhysicalAccessPlanner.Allocations(placement).Where(a=>a.Reservation is not null).Select(a=>new{a.NodeId,a.Reservation}).ToArray():[],Report=report});return report;
        }
        var solid=MetadataOccupancyKind.Solid;var reserved=MetadataOccupancyKind.Reservation;
        var world=Input([Box(0,0,4,4),Box(2,0,4,4),Box(-1,-1,8,6)],[solid,solid,reserved]);
        var first=Measure("overlap-and-parent-reservation",world,24,48,48);
        Check(first.SolidSources==2&&first.ReservationSources==1&&first.UnclassifiedSources==0,"counts are separate from areas");
        Check(first.GroundUses is null&&first.DeclaredGroundUnionArea is null&&first.OpenGroundUnionArea is null,"parent reservations do not infer intended open ground");
        Near(first.GrossDomainArea,1600,"gross domain is not usable or built area");
        var reversed=world with{Nodes=world.Nodes.Reverse().ToArray(),AccessPlan=world.AccessPlan! with{Sites=world.AccessPlan.Sites.Reverse().ToArray()}};
        Check(Json(first)==Json(planner.Measure(reversed)),"declaration-order invariant report and work");
        var moved=world with{AccessPlan=world.AccessPlan! with{Sites=world.AccessPlan.Sites.Select((s,i)=>i==1?s with{Footprint=Box(4,0,4,4)}:s).ToArray()}};
        Reject(()=>planner.Validate(first,moved),"METADATA_ACCOUNTING_SOURCE_MISMATCH");
        Reject(()=>planner.Validate(first with{CombinedUnionArea=72},world),"METADATA_ACCOUNTING_SOURCE_MISMATCH");
        Check(Json(first)==Json(planner.Measure(world)),"later measurement cannot mutate saved source identities");
        Measure("crossing-triangles",Input([new([new(0,0),new(4,0),new(0,4)]),new([new(0,0),new(4,0),new(4,4)])],[solid,solid]),12,0,12);
        Measure("concave-overlap",Input([new([new(0,0),new(6,0),new(6,2),new(2,2),new(2,6),new(0,6)]),Box(1,1,4,4)],[solid,solid]),29,0,29);
        Measure("boundary-contact",Input([Box(0,0,2,2),Box(2,0,2,2)],[solid,solid]),8,0,8);
        Measure("identical-and-reversed",Input([Box(0,0,4,4),new(Box(0,0,4,4).Vertices.Reverse().ToArray())],[reserved,reserved]),0,16,16);
        var groundWorld=Input([Box(0,0,10,10),Box(5,0,10,10),Box(2,2,8,6),Box(6,1,3,8),Box(-1,-1,17,12)],
            [reserved,reserved,solid,solid,reserved]);
        groundWorld=groundWorld with{FootprintDefinitions=new Dictionary<string,MetadataCatalogFootprint>{
            ["source-0"]=new("meters","Courtyard surface","test:yard",Box(0,0,10,10),GroundUse:new("courtyard","Shared activity ground","test:ground",.45)),
            ["source-1"]=new("meters","Garden surface","test:garden",Box(5,0,10,10),GroundUse:new("garden","Garden circulation","test:ground",.65))}};
        var groundReport=Measure("explicit-overlapping-ground-uses",groundWorld,54,204,204);
        Near(groundReport.DeclaredGroundUnionArea,150,"overlapping declared ground union");
        Near(groundReport.OpenGroundUnionArea,96,"ground union minus overlapping solids; wrapper is excluded");
        Check(groundReport.GroundUses is {Count:2},"every explicit use retained");
        Near(groundReport.GroundUses![0].OpenArea,46,"first exact clear area");Near(groundReport.GroundUses[1].OpenArea,64,"second clipped clear area");
        Check(groundReport.GroundUses[0].MinimumSatisfied&&!groundReport.GroundUses[1].MinimumSatisfied,"minimum uses actual union, not sum of overlaps");
        var reversedGround=groundWorld with{Nodes=groundWorld.Nodes.Reverse().ToArray(),AccessPlan=groundWorld.AccessPlan! with{Sites=groundWorld.AccessPlan.Sites.Reverse().ToArray()},
            FootprintDefinitions=groundWorld.FootprintDefinitions!.Reverse().ToDictionary(p=>p.Key,p=>p.Value)};
        Check(Json(groundReport)==Json(planner.Measure(reversedGround)),"ground-use order invariance");
        var limitedGround=planner.Measure(groundWorld,maximumWork:1000);
        Check(limitedGround.Status==MetadataAccountingStatus.Unmeasured&&limitedGround.GroundUses is null&&limitedGround.OpenGroundUnionArea is null,
            "ground-use resource exhaustion cannot retain partial success measurements");
        const double radians=23*Math.PI/180;
        var rotatedGround=groundWorld with{AccessPlan=groundWorld.AccessPlan! with{Sites=groundWorld.AccessPlan.Sites.Select(s=>s with{Footprint=new(s.Footprint.Vertices.Select(p=>new WorldPoint(p.X*Math.Cos(radians)-p.Y*Math.Sin(radians),p.X*Math.Sin(radians)+p.Y*Math.Cos(radians))).ToArray())}).ToArray()}};
        var rotatedReport=planner.Measure(rotatedGround);Near(rotatedReport.OpenGroundUnionArea,96,"rotated ground exact clear union");Near(rotatedReport.GroundUses![0].OpenArea,46,"rotated source clear area");
        Reject(()=>planner.Validate(groundReport with{OpenGroundUnionArea=150},groundWorld),"METADATA_ACCOUNTING_SOURCE_MISMATCH");
        var changedUse=groundWorld with{FootprintDefinitions=groundWorld.FootprintDefinitions!.ToDictionary(p=>p.Key,p=>p.Value with{GroundUse=p.Value.GroundUse! with{Purpose="Changed intent"}})};
        Reject(()=>planner.Validate(groundReport,changedUse),"METADATA_ACCOUNTING_SOURCE_MISMATCH");
        Reject(()=>planner.Measure(groundWorld with{AccessPlan=null}),"METADATA_GROUND_USE_PHYSICAL_SOURCE_REQUIRED");
        Reject(()=>planner.Measure(groundWorld with{AccessPlan=groundWorld.AccessPlan! with{Sites=groundWorld.AccessPlan.Sites.Select(s=>s.NodeId=="source-0"?s with{Occupancy=solid}:s).ToArray()}}),"METADATA_GROUND_USE_RESERVATION_REQUIRED");
        Reject(()=>planner.Measure(groundWorld with{AccessPlan=groundWorld.AccessPlan! with{Sites=groundWorld.AccessPlan.Sites.Select(s=>s.NodeId=="source-2"?s with{Occupancy=MetadataOccupancyKind.Unspecified}:s).ToArray()}}),"METADATA_GROUND_USE_OCCUPANCY_REQUIRED");
        foreach(double invalid in new[]{-.01,1.01,double.NaN,double.PositiveInfinity})
            Reject(()=>planner.Measure(groundWorld with{FootprintDefinitions=groundWorld.FootprintDefinitions!.ToDictionary(p=>p.Key,p=>p.Value with{GroundUse=p.Value.GroundUse! with{MinimumOpenFraction=invalid}})}),"METADATA_GROUND_USE_INVALID");
        var concaveGround=Input([new([new(0,0),new(6,0),new(6,2),new(2,2),new(2,6),new(0,6)]),Box(1,1,4,4)],[reserved,solid]);
        concaveGround=concaveGround with{FootprintDefinitions=new Dictionary<string,MetadataCatalogFootprint>{
            ["source-0"]=new("meters","Concave ground source","test:concave",concaveGround.AccessPlan!.Sites[0].Footprint,GroundUse:new("activity","Concave activity ground","test:concave-ground",.6))}};
        var concaveGroundReport=Measure("concave-ground-minus-crossing-solid",concaveGround,16,20,29);
        Near(concaveGroundReport.OpenGroundUnionArea,13,"exact concave ground difference");
        var coveredGround=Input([Box(0,0,2,2),Box(-1,-1,4,4)],[reserved,solid]);
        coveredGround=coveredGround with{FootprintDefinitions=new Dictionary<string,MetadataCatalogFootprint>{
            ["source-0"]=new("meters","Covered ground source","test:covered",Box(0,0,2,2),GroundUse:new("activity","Required open ground","test:covered-ground",.1))}};
        var coveredReport=Measure("fully-covered-ground",coveredGround,16,4,16);Near(coveredReport.OpenGroundUnionArea,0,"fully covered ground has no false open area");
        Check(!coveredReport.GroundUses![0].MinimumSatisfied,"fully covered ground fails its positive requirement");
        const double offset=9999980;
        Measure("large-coordinate-translation",Input([Box(offset,offset,4,4),Box(offset+2,offset,4,4)],[solid,solid],Box(offset-5,offset-5,20,20)),24,0,24);
        var clearance=WorldClearanceTests.Example();var clearanceWorld=new WorldMetadataGenerator().Generate(clearance.Recipe,clearance.Catalog);
        var envelopes=Measure("declared-rotated-clearances",clearanceWorld,32,160,160,32);
        Check(envelopes.ReservationSources==0&&envelopes.ClearanceReservations==2&&envelopes.Sources.All(s=>s.ClearanceSha256 is not null),"clearance envelopes stay distinct from reservation-role sources");
        var concaveDomain=new WorldPolygon([new(0,0),new(6,0),new(6,6),new(4,6),new(4,2),new(2,2),new(2,6),new(0,6)]);
        Reject(()=>planner.Measure(Input([Box(1,1,4,4)],[solid],concaveDomain)),"METADATA_ACCOUNTING_SOURCE_OUTSIDE_DOMAIN");
        var topology=baseline with{Surface=null,AccessPlan=null};var unmeasured=planner.Measure(topology);
        Check(unmeasured.Status==MetadataAccountingStatus.NotApplicable&&unmeasured.CombinedUnionArea is null&&unmeasured.GrossDomainArea is null,"topology has no invented zero physical area");
        Check(planner.Measure(topology with{Space=new([],"abstract")}).Status==MetadataAccountingStatus.NotApplicable,"zero-axis topology remains a supported metadata world");
        Check(planner.Measure(world with{AccessPlan=null}).Reason=="NO_CLASSIFIED_PHYSICAL_SOURCE","missing declaration does not imply zero occupancy");
        Check(planner.Measure(world with{Space=new(["x","y","z"],"meters")}).CombinedUnionArea is null,"no volume flattening");
        Reject(()=>planner.Measure(world,maximumWork:1),"METADATA_ACCOUNTING_WORK_BUDGET");
        var rods=Enumerable.Range(0,24).Select(i=>{double angle=i*Math.PI/24;return new WorldPolygon(Box(-10,-.25,20,.5).Vertices.Select(p=>new WorldPoint(p.X*Math.Cos(angle)-p.Y*Math.Sin(angle),p.X*Math.Sin(angle)+p.Y*Math.Cos(angle))).ToArray());}).ToArray();
        var limited=planner.Measure(Input(rods,Enumerable.Repeat(solid,24).ToArray()),maximumWork:20000);
        Check(limited.Status==MetadataAccountingStatus.Unmeasured&&limited.Reason=="METADATA_ACCOUNTING_WORK_BUDGET"&&limited.Sources.Count==24&&limited.SolidUnionArea is null&&limited.CombinedUnionArea is null,"bounded failure never exposes partial union as measured");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Measure(world,token:cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        using(var cancel=new CancellationTokenSource())
        {
            try{planner.Measure(topology with{Nodes=new CancellingNodes(topology.Nodes,cancel)},token:cancel.Token);throw new Exception("Missing cancellation during source enumeration");}
            catch(OperationCanceledException){}
        }
        await File.WriteAllTextAsync(Path.Combine(output,"analytic-cases.json"),Json(cases));
        await File.WriteAllTextAsync(Path.Combine(output,"bounded-result.json"),Json(limited));
        await InteriorCoverageCasesAsync(output,baseline);
        var interior=WorldInteriorTests.Example();
        var groundCatalog=interior.Catalog with{Archetypes=interior.Catalog.Archetypes.Select(a=>a.Kind=="room"?a with{
            PhysicalFootprint=a.PhysicalFootprint! with{GroundUse=new("activity-floor","Room activity and circulation ground","test:room-ground",.95)}}:a).ToArray()};
        var generator=new WorldMetadataGenerator();var generated=generator.Generate(interior.Recipe,groundCatalog);
        var generatedReport=planner.Measure(generated);
        Check(generatedReport.GroundUses is {Count:3}&&generatedReport.GroundUses.All(g=>g.MinimumSatisfied),"real generation qualifies all required room ground uses");
        foreach(var use in generatedReport.GroundUses!){Near(use.SourceArea,36,"room source area");Near(use.OpenArea,35,"room source minus exact fixture");}
        var failedCatalog=groundCatalog with{Archetypes=groundCatalog.Archetypes.Select(a=>a.PhysicalFootprint?.GroundUse is {} use?
            a with{PhysicalFootprint=a.PhysicalFootprint with{GroundUse=use with{MinimumOpenFraction=1}}}:a).ToArray()};
        Reject(()=>generator.Generate(interior.Recipe,failedCatalog),"METADATA_PLACEMENT_UNRESOLVED:GROUND_USE_CAPACITY:");
        await File.WriteAllTextAsync(Path.Combine(output,"ground-recipe.json"),Json(interior.Recipe));
        await File.WriteAllTextAsync(Path.Combine(output,"ground-catalog.json"),Json(groundCatalog));
        await File.WriteAllTextAsync(Path.Combine(output,"failed-ground-catalog.json"),Json(failedCatalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"ground-recipe.json"),Path.Combine(output,"ground-catalog.json"),Path.Combine(output,"published-ground"));
        var groundReload=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"published-ground","world-metadata.json"));
        planner.Validate(await WorldMetadataGenerator.ReadAsync<MetadataPhysicalAccounting>(Path.Combine(output,"published-ground","physical-accounting.json")),groundReload);
        try{await generator.GenerateFilesAsync(Path.Combine(output,"ground-recipe.json"),Path.Combine(output,"failed-ground-catalog.json"),Path.Combine(output,"refused-ground"));throw new Exception("Missing ground-use publication refusal");}
        catch(ArgumentException error)when(error.Message.StartsWith("METADATA_PLACEMENT_UNRESOLVED:GROUND_USE_CAPACITY:",StringComparison.Ordinal)){}
        Check(!Directory.Exists(Path.Combine(output,"refused-ground")),"unsatisfied required ground cannot publish");
        var groundReview=await File.ReadAllTextAsync(Path.Combine(output,"published-ground","review.html"));
        Check(groundReview.Contains("Declared ground uses")&&groundReview.Contains("Room activity and circulation ground"),"publication explains actual declared use and result");
        var scale=WorldScaleTests.Example(16,7);var scaleWorld=new WorldMetadataGenerator().Generate(scale.Recipe,scale.Catalog);
        var scaleReport=planner.Measure(scaleWorld);Check(scaleReport.Status==MetadataAccountingStatus.Measured&&scaleReport.Sources.Count==576,"complete577-node program measured without source reduction");
        Near(scaleReport.CombinedUnionArea,16*52*60,"nested reservations counted once at full scale");
        await File.WriteAllTextAsync(Path.Combine(output,"scale-world.json"),Json(scaleWorld));
        await File.WriteAllTextAsync(Path.Combine(output,"scale-recipe.json"),Json(scale.Recipe));
        await File.WriteAllTextAsync(Path.Combine(output,"scale-catalog.json"),Json(scale.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"scale-accounting.json"),Json(scaleReport));
        var reloaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"scale-world.json"));
        planner.Validate(await WorldMetadataGenerator.ReadAsync<MetadataPhysicalAccounting>(Path.Combine(output,"scale-accounting.json")),reloaded);
        Console.WriteLine("PASS: analytic union areas, explicit ground-use clear fractions and union, no inferred intent from wrappers, rotated/overlapping sources, role/intent/tamper failures, required generation/read/publication refusal, finite work/cancellation and unchanged577-node accounting.");
    }
}
