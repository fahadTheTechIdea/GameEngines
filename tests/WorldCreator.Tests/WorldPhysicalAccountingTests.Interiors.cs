using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldPhysicalAccountingTests
{
    private static async Task InteriorCoverageCasesAsync(string output,WorldMetadataDocument baseline)
    {
        var planner=new MetadataPhysicalAccountingPlanner();var cases=new List<object>();
        void Check(bool value,string message){if(!value)throw new Exception("Interior accounting: "+message);}
        void Near(double value,double expected,string message)=>Check(Math.Abs(value-expected)<1e-7,message+": "+value+" / "+expected);
        void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
        var template=WorldGeneratedInteriorTests.Example().Recipe.Interiors!.Rules[0] with{ScopeNodeId="root"};
        WorldMetadataDocument Input(double offset=2)
        {
            // Analytic union/difference fixture, not a generated room assembly.
            var root=baseline.Nodes[0] with{Id="root",ParentId=null};
            var nodes=new[]{root,root with{Id="a",ParentId="root",Kind="room"},root with{Id="b",ParentId="root",Kind="room"},
                root with{Id="solid",ParentId="a",Kind="equipment"},root with{Id="wrapper",ParentId="root",Kind="reservation"}};
            var sites=new[]{new MetadataPhysicalSite("a",MetadataOccupancyKind.Reservation,"test:a",Box(0,0,4,4),[]),
                new("b",MetadataOccupancyKind.Reservation,"test:b",Box(offset,0,4,4),[]),
                new("solid",MetadataOccupancyKind.Solid,"test:solid",Box(1,.25,1,1),[]),
                new("wrapper",MetadataOccupancyKind.Reservation,"test:wrapper",Box(-1,-1,10,8),[])};
            var walls=new[]{new MetadataInteriorWall("wall-a","a",0,new(0,0),new(4,0),Box(0,0,4,.5)),
                new("wall-b","b",0,new(offset,0),new(offset+4,0),Box(offset,0,4,.5))};
            return baseline with{Nodes=nodes,FootprintDefinitions=null,PlacementPlan=null,
                Surface=baseline.Surface! with{Boundary=Box(-20,-20,40,40)},AccessPlan=new(new Dictionary<string,MetadataCatalogAccess>(),sites,0),
                InteriorPlan=new(new([template]),[new(template.Id,["a","b"],walls,[],[])],0)};
        }
        void Measure(string name,WorldMetadataDocument world,double floor,double wall,double clear)
        {
            var report=planner.Measure(world);Check(report.Status==MetadataAccountingStatus.Measured&&report.InteriorCoverage is {Count:1},name+" measured");
            var coverage=report.InteriorCoverage![0];Near(coverage.FloorUnionArea,floor,name+" floor union");Near(coverage.WallUnionArea,wall,name+" wall union");
            Near(coverage.ClearFloorArea,clear,name+" clear floor");Check(report.GrossDomainArea==1600,name+" search domain remains distinct");
            Check(coverage.RoomNodeIds.SequenceEqual(new[]{"a","b"})&&coverage.Walls.Count==2,"exact ownership retained");
            planner.Validate(report,world);
            cases.Add(new{Name=name,World=world,Report=report});
        }
        var analytic=Input();Measure("overlapping-floors-walls-and-solid",analytic,24,3,20.25);
        var contact=Input(4);Measure("contacting-floors-and-walls",contact,32,4,27.25);
        var contained=Input(0);Measure("identical-floor-and-wall-sources",contained,16,2,13.25);
        const double rotation=23*Math.PI/180;
        WorldPolygon Rotate(WorldPolygon p)=>new(p.Vertices.Select(v=>new WorldPoint(v.X*Math.Cos(rotation)-v.Y*Math.Sin(rotation),v.X*Math.Sin(rotation)+v.Y*Math.Cos(rotation))).ToArray());
        var rotated=analytic with{AccessPlan=analytic.AccessPlan! with{Sites=analytic.AccessPlan.Sites.Select(s=>s with{Footprint=Rotate(s.Footprint)}).ToArray()},
            InteriorPlan=analytic.InteriorPlan! with{Results=analytic.InteriorPlan.Results.Select(r=>r with{Walls=r.Walls.Select(w=>w with{Footprint=Rotate(w.Footprint)}).ToArray()}).ToArray()}};
        Measure("rotated-overlapping-coverage",rotated,24,3,20.25);
        // Assemblies in one coordinate domain cannot ignore one another's walls.
        var extraRoot=analytic.Nodes[0] with{Id="other-root"};
        var extraNodes=new[]{extraRoot,extraRoot with{Id="c",ParentId="other-root",Kind="room"},extraRoot with{Id="d",ParentId="other-root",Kind="room"}};
        var extraSites=new[]{new MetadataPhysicalSite("c",MetadataOccupancyKind.Reservation,"test:c",Box(0,0,4,4),[]),
            new("d",MetadataOccupancyKind.Reservation,"test:d",Box(0,0,4,4),[])};
        var otherRule=template with{Id="other",ScopeNodeId="other-root"};
        var sharedDomain=analytic with{Nodes=analytic.Nodes.Concat(extraNodes).ToArray(),
            AccessPlan=analytic.AccessPlan! with{Sites=analytic.AccessPlan.Sites.Concat(extraSites).ToArray()},
            InteriorPlan=new(new([template,otherRule]),[analytic.InteriorPlan!.Results[0],
                new("other",["c","d"],[new("wall-c","c",0,new(0,2),new(4,2),Box(0,2,4,.5))],[],[])],0)};
        var sharedReport=planner.Measure(sharedDomain);Check(sharedReport.InteriorCoverage is {Count:2},"separate assembly observations");
        Near(sharedReport.InteriorCoverage![0].ClearFloorArea,11.25,"other assembly includes original walls");
        Near(sharedReport.InteriorCoverage[1].ClearFloorArea,18.25,"original assembly includes other walls");
        Near(sharedReport.InteriorCoverage[0].WallUnionArea,2,"each wall category retains its own assembly provenance");
        cases.Add(new{Name="overlapping-independent-assemblies",World=sharedDomain,Report=sharedReport});
        foreach(bool satisfies in new[]{false,true})
        {
            var requiredWorld=sharedDomain with{InteriorPlan=sharedDomain.InteriorPlan! with{Definition=new([
                template with{FloorRequirement=new(satisfies?18:19,.75)},otherRule with{FloorRequirement=new(11,.7)}])}};
            var requiredReport=planner.Measure(requiredWorld);var row=requiredReport.InteriorCoverage!.Single(r=>r.RuleId==template.Id);
            Check(row.MinimumSatisfied==satisfies,"minimum clear area uses all assembly blockers");
            Check(requiredReport.InteriorCoverage!.Single(r=>r.RuleId==otherRule.Id).MinimumSatisfied==true,"independent rule minimum");
            planner.Validate(requiredReport,requiredWorld);
            cases.Add(new{Name="overlapping-assembly-requirements-"+satisfies,World=requiredWorld,Report=requiredReport});
        }
        var first=planner.Measure(analytic);var assembly=analytic.InteriorPlan!.Results[0];
        var reverse=analytic with{Nodes=analytic.Nodes.Reverse().ToArray(),AccessPlan=analytic.AccessPlan! with{Sites=analytic.AccessPlan.Sites.Reverse().ToArray()},
            InteriorPlan=analytic.InteriorPlan with{Results=[assembly with{RoomNodeIds=assembly.RoomNodeIds.Reverse().ToArray(),Walls=assembly.Walls.Reverse().ToArray()}]}};
        Check(Json(first)==Json(planner.Measure(reverse)),"order-independent measurements, identities and work");
        Reject(()=>planner.Validate(first with{InteriorCoverage=[first.InteriorCoverage![0] with{ClearFloorArea=24}]},analytic),"METADATA_ACCOUNTING_SOURCE_MISMATCH");
        var changed=analytic with{InteriorPlan=analytic.InteriorPlan with{Results=[assembly with{Walls=[assembly.Walls[0] with{Footprint=Box(0,0,4,.6)},assembly.Walls[1]]}]}};
        Reject(()=>planner.Validate(first,changed),"METADATA_ACCOUNTING_SOURCE_MISMATCH");
        Reject(()=>planner.Measure(analytic with{InteriorPlan=analytic.InteriorPlan with{Results=[assembly with{RoomNodeIds=["a","missing"]}]}}),"METADATA_ACCOUNTING_INTERIOR_ROOM_SOURCE_MISMATCH");
        Reject(()=>planner.Measure(analytic with{InteriorPlan=analytic.InteriorPlan with{Results=[assembly with{Walls=[assembly.Walls[0] with{RoomNodeId="solid"}]}]}}),"METADATA_ACCOUNTING_INTERIOR_WALL_SOURCE_MISMATCH");
        Reject(()=>planner.Measure(analytic with{InteriorPlan=analytic.InteriorPlan with{Results=[assembly with{Walls=[assembly.Walls[0] with{Footprint=Box(-1,0,4,.5)}]}]}}),"METADATA_ACCOUNTING_INTERIOR_WALL_OUTSIDE_ROOM");
        Reject(()=>planner.Measure(analytic with{AccessPlan=analytic.AccessPlan! with{Sites=analytic.AccessPlan.Sites.Select(s=>s.NodeId=="solid"?s with{Occupancy=MetadataOccupancyKind.Unspecified}:s).ToArray()}}),"METADATA_ACCOUNTING_INTERIOR_OCCUPANCY_REQUIRED");
        Check(planner.Measure(analytic with{InteriorPlan=null}).InteriorCoverage is null,"reservation sources alone never infer floor coverage");
        var limited=planner.Measure(analytic,maximumWork:2000);
        Check(limited.Status==MetadataAccountingStatus.Unmeasured&&limited.InteriorCoverage is null&&limited.SolidUnionArea is null,"finite work never publishes partial coverage");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Measure(analytic,token:cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(output,"interior-analytic-cases.json"),Json(cases));
        // Exercise actual generated walls, file publication and saved-world replay.
        foreach(bool hexagonal in new[]{false,true})
        {
            string name=hexagonal?"hexagonal":"rectangular";var input=WorldGeneratedInteriorTests.Example(hexagonal:hexagonal);
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(input.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(input.Catalog));
            await new WorldMetadataGenerator().GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"published"));
            var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"published","world-metadata.json"));
            var report=await WorldMetadataGenerator.ReadAsync<MetadataPhysicalAccounting>(Path.Combine(folder,"published","physical-accounting.json"));
            planner.Validate(report,saved);var coverage=report.InteriorCoverage!.Single();
            Near(coverage.FloorUnionArea,hexagonal?6*13.5*Math.Sqrt(3):180,name+" exact module area sum");
            Check(coverage.RoomNodeIds.Count==6&&coverage.Walls.Count==saved.InteriorPlan!.Results.Single().Walls.Count,"every generated room and wall measured");
            Near(coverage.ClearFloorArea,coverage.FloorUnionArea-coverage.WallUnionArea-6*.8*.5,name+" disjoint generated walls and fixtures");
            var review=await File.ReadAllTextAsync(Path.Combine(folder,"published","review.html"));
            Check(review.Contains("Constructed interior floors")&&review.Contains("available search space"),"published review explains realized floor scope");
            var summary=await File.ReadAllTextAsync(Path.Combine(folder,"published","summary.json"));Check(summary.Contains("InteriorCoverage"),"summary includes native coverage");
        }
        Console.WriteLine("PASS: interior floor/wall/clear unions, overlap/contact/rotation, source ownership, no inferred floors, tamper, finite work/cancellation, generated rectangular/hexagonal publication and replay.");
    }
}
