using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRelationCandidateTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool value,string reason){if(!value)throw new Exception("Relation candidates: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static (WorldSurfaceDefinition Surface,WorldEnvironmentDefinition Environment,WorldPlacementRequest Request,WorldPoint Upper,WorldPoint Lower)
        Example(double rotation=0,double x=0,double y=0,bool blockLower=true,bool reservation=false,double secondX=6,double firstRadius=5,double secondRadius=5)
    {
        WorldPoint Transform(WorldPoint p)=>new(x+p.X*Math.Cos(rotation)-p.Y*Math.Sin(rotation),y+p.X*Math.Sin(rotation)+p.Y*Math.Cos(rotation));
        WorldPolygon Shape(WorldPolygon p)=>new(p.Vertices.Select(Transform).ToArray());
        var boundary=Shape(Box(-2,-6,16,14));
        var surface=new WorldSurfaceDefinition(1,"relation-ground","meters",boundary,
            blockLower?[new("lower-obstacle","Protect the lower contact","test:lower",Shape(Box(2,-5,2,2)))]:[],
            [new("ground","ground","Supported ground","test:ground",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentDefinition(1,"relation-fields",[new("height","Ground height","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var profile=new WorldSuitabilityProfile("relation-purpose","Declare exact distance intent","test:purpose",["ground"],[],[]);
        var footprint=new WorldPolygon(Box(-.2,-.2,.4,.4).Vertices.Select(p=>new WorldPoint(p.X*Math.Cos(rotation)-p.Y*Math.Sin(rotation),p.X*Math.Sin(rotation)+p.Y*Math.Cos(rotation))).ToArray());
        var envelope=reservation?new WorldPolygon(Box(-.3,-.3,.6,.6).Vertices.Select(p=>new WorldPoint(p.X*Math.Cos(rotation)-p.Y*Math.Sin(rotation),p.X*Math.Sin(rotation)+p.Y*Math.Cos(rotation))).ToArray()):null;
        var request=new WorldPlacementRequest("relation-sites","Place required source at declared relation contacts","test:relations",boundary,
            [new("site","Required source-sized site","test:site",footprint,profile,envelope)],
            [new("first",Transform(new(0,0))),new("second",Transform(new(secondX,0)))],
            [new("first-band","site","first","First distance intent","test:first",firstRadius,firstRadius,firstRadius,1),
             new("second-band","site","second","Second distance intent","test:second",secondRadius,secondRadius,secondRadius,1)]);
        return(surface,environment,request,Transform(new(3,4)),Transform(new(3,-4)));
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) MetadataExample()
    {
        var e=Example(blockLower:false);var basis=WorldPopulationCoordinationTests.Example();var source=basis.Recipe.PopulationSource!;
        var catalog=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Count=2}]}:
            a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=e.Request.Demands[0].Footprint!}}).ToArray()};
        var request=e.Request with{Demands=[e.Request.Demands[0] with{Id="a",Footprint=null},e.Request.Demands[0] with{Id="b",Footprint=null}],MinimumSeparation=1,
            Relations=e.Request.Relations.SelectMany(r=>new[]{r with{Id="a-"+r.Id,FromId="a"},r with{Id="b-"+r.Id,FromId="b"}}).ToArray()};
        return(source.Recipe with{Id="relation-world",Surface=e.Surface,Environment=e.Environment,Regions=null,
            Placement=new(request,[new("a","relation-world/sites-0",true),new("b","relation-world/sites-1",true)])},catalog);
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var planner=new WorldSitePlacementPlanner();int count=0;
        foreach(var e in new[]{Example(),Example(.37,-200,50),Example(-.81,1000000,-1000000),Example(.37,9000000,-9000000)})
        {
            var query=new WorldEnvironmentQuery(new(e.Surface),e.Environment);var plan=planner.Plan(query,e.Request);
            Check(plan.Status==WorldPlacementStatus.Found&&plan.Sites.Single().Position.DistanceTo(e.Upper)<2e-8,"exact rotated/translated circle intersection found");
            Check(plan.Sites[0].CandidateSource.Contains("intersection",StringComparison.Ordinal),"analytic constraint provenance retained");
            Check(Json(plan)==Json(planner.Plan(query,e.Request)),"deterministic replay");
            var reversed=e.Request with{Demands=e.Request.Demands.Reverse().ToArray(),Anchors=e.Request.Anchors.Reverse().ToArray(),Relations=e.Request.Relations.Reverse().ToArray()};
            Check(Json(planner.Plan(query,reversed))==Json(plan),"declaration-order invariance");
            await File.WriteAllTextAsync(Path.Combine(folder,"analytic-"+count+++".json"),Json(plan));
        }
        foreach(var e in new[]{Example(blockLower:false,secondX:10),Example(blockLower:false,secondX:3,secondRadius:2)})
        {
            var plan=planner.Plan(new(new(e.Surface),e.Environment),e.Request);
            Check(plan.Status==WorldPlacementStatus.Found&&plan.Sites.Single().Position.DistanceTo(new(5,0))<1e-12,"external/internal tangent found");
        }
        var ordinary=Example();var q=new WorldEnvironmentQuery(new(ordinary.Surface),ordinary.Environment);var exact=planner.Plan(q,ordinary.Request);
        var oneBand=ordinary.Request with{Relations=[ordinary.Request.Relations[0]],Anchors=[ordinary.Request.Anchors[0]]};
        var directional=planner.Plan(q,oneBand);
        Check(directional.Status==WorldPlacementStatus.Found&&directional.Sites[0].CandidateSource.Contains(":direction:",StringComparison.Ordinal),"one-circle intent derives directions from semantic boundary vertices");
        var edgeBoundary=Box(2,-6,6,12);var edgeSurface=ordinary.Surface with{Boundary=edgeBoundary,Exclusions=[],Patches=ordinary.Surface.Patches.Select(p=>p with{Boundary=edgeBoundary}).ToArray()};
        var edgeFields=ordinary.Environment with{Fields=[new("x","Authored x field","test:x","meters",0,[new(WorldFieldInput.X,1)])]};
        var edgeProfile=ordinary.Request.Demands[0].Profile with{Preferences=[new("edge","x","Keep complete source at the declared boundary","test:edge",2,10,1)]};
        var edgeRequest=oneBand with{Boundary=edgeBoundary,Demands=[oneBand.Demands[0] with{Footprint=Box(0,0,.4,.4),Profile=edgeProfile}]};
        var edgePlan=planner.Plan(new(new(edgeSurface),edgeFields),edgeRequest);
        Check(edgePlan.Status==WorldPlacementStatus.Found&&edgePlan.Sites[0].CandidateSource.Contains(":boundary:",StringComparison.Ordinal)&&Math.Abs(edgePlan.Sites[0].Position.X-2)<1e-12&&Math.Abs(Math.Abs(edgePlan.Sites[0].Position.Y)-Math.Sqrt(21))<1e-12,"analytic circle-boundary contact retains the complete source");
        foreach(bool maximum in new[]{true,false})
        {
            var bandRequest=ordinary.Request with{Relations=ordinary.Request.Relations.Select(r=>r.Id!="first-band"?r:r with{Minimum=maximum?3:5,Ideal=maximum?4:6,Maximum=maximum?5:7}).ToArray()};
            var bandSurface=ordinary.Surface with{Exclusions=maximum?
                [new("below","Protect lower ground","test:band",Box(-2,-6,16,9.7))]:
                [new("below","Protect lower ground","test:band",Box(-2,-6,16,6)),new("above","Protect upper ground","test:band",Box(-2,4.3,16,3.7))]};
            var bandPlan=planner.Plan(new(new(bandSurface),ordinary.Environment),bandRequest);
            Check(bandPlan.Status==WorldPlacementStatus.Found&&bandPlan.Sites[0].Position.DistanceTo(new(3,4))<1e-12&&
                bandPlan.Sites[0].CandidateSource.Contains(maximum?":maximum":":minimum",StringComparison.Ordinal),"terrain selects the declared "+(maximum?"maximum":"minimum")+" distance radius without altering the band");
        }
        Reject(()=>planner.Plan(q,ordinary.Request with{MaximumWork=exact.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Check(planner.Plan(q,ordinary.Request with{MaximumWork=exact.Work}).Work==exact.Work,"exact work ceiling retained");
        Reject(()=>planner.Plan(q,ordinary.Request with{MaximumCandidates=8}),"WORLD_PLACEMENT_CANDIDATE_BUDGET");
        var narrow=ordinary.Request with{Relations=ordinary.Request.Relations.Select(r=>r with{Minimum=5-1e-10,Maximum=5+1e-10}).ToArray()};
        Check(planner.Plan(q,narrow).Status==WorldPlacementStatus.Found,"narrow nonzero band finds analytic ideal");
        var noIntersection=Example(firstRadius:1,secondRadius:1);
        Check(planner.Plan(new(new(noIntersection.Surface),noIntersection.Environment),noIntersection.Request).Status==WorldPlacementStatus.NoFeasibleAssignment,"disjoint distance constraints refuse without relaxation");
        var displaced=ordinary.Request with{Execution=WorldPlacementExecution.Prescribed,Anchors=[..ordinary.Request.Anchors,new("near",ordinary.Upper.Offset(.0001,0))],
            Relations=[..ordinary.Request.Relations,new("pin","site","near","Test a measurable out-of-band position","test:pin",0,0,0,1)]};
        Check(planner.Plan(q,displaced).Status==WorldPlacementStatus.NoFeasibleAssignment,"rounding bound cannot conceal a measurable constraint violation");
        Reject(()=>planner.Plan(q,oneBand with{Execution=WorldPlacementExecution.Prescribed,Anchors=[new("upper",new(3,4)),new("near",new(Math.BitIncrement(3d),4))],
            Relations=[new("pin","site","upper","Exact authored pin","test:pin",0,0,0,1),new("pin-near","site","near","Distinct authored pin","test:pin",0,0,0,1)]}),"WORLD_PLACEMENT_PRESCRIBED_ANCHOR_CONFLICT");
        var tiny=Example(x:9000000,y:-9000000,blockLower:false);
        foreach(double at in new[]{9000000d,Math.BitIncrement(9000000d)})
        {
            var request=tiny.Request with{Execution=WorldPlacementExecution.Prescribed,Anchors=[tiny.Request.Anchors[0],new("pin",new(at,-9000000))],
                Relations=[new("tiny","site","first","Retain a positive tiny radius","test:precision",1e-20,1e-20,1e-20,1),
                    new("pin","site","pin","Select an exactly representable pose","test:precision",0,0,0,1)]};
            Check(planner.Plan(new(new(tiny.Surface),tiny.Environment),request).Status==WorldPlacementStatus.NoFeasibleAssignment,"coordinate roundoff cannot erase an unresolved positive radius");
        }
        var reserved=Example(reservation:true);var hole=new WorldSurfaceExclusion("reservation-hole","Keep full reservation clear","test:hole",Box(3.24,3.95,.04,.1));
        var holes=reserved.Surface with{Exclusions=[..reserved.Surface.Exclusions,hole]};
        Check(planner.Plan(new(new(holes),reserved.Environment),reserved.Request).Status==WorldPlacementStatus.NoFeasibleAssignment,"full reservation still qualifies at an analytic center");
        Check(planner.Plan(new(new(holes),reserved.Environment),ordinary.Request).Status==WorldPlacementStatus.Found,"reservation-only hole does not intersect actual source footprint");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(q,ordinary.Request,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var metadata=MetadataExample();var generator=new WorldMetadataGenerator();var world=generator.Generate(metadata.Recipe,metadata.Catalog);
        Check(world.Nodes.Count==3&&world.AccessPlan!.Sites.Count==2,"both required catalog members retained");
        Check(world.Nodes.Where(n=>n.ParentId is not null).Select(n=>n.Position![1]).Order().SequenceEqual(new[]{-4d,4d}),"two sources occupy distinct relation intersections");
        WorldMetadataGenerator.ValidateDocument(world);
        var session=new MetadataGenerationSession(generator,metadata.Recipe,metadata.Catalog);var before=session.Snapshot;
        var changed=metadata.Recipe with{Placement=metadata.Recipe.Placement! with{Request=metadata.Recipe.Placement.Request with{
            Anchors=metadata.Recipe.Placement.Request.Anchors.Select(a=>a.Id=="second"?a with{Position=new(8,0)}:a).ToArray()}}};
        var after=session.Apply(new("anchor","Move authored distance source","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,metadata.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changed,metadata.Catalog)),"anchor edit equals clean generation");
        Check(after.World.Nodes.Where(n=>n.ParentId is not null).All(n=>n.Position!.SequenceEqual(new[]{4d,n.Position![1]})&&Math.Abs(n.Position[1])==3),"content adapts to edited distance intersections");
        var invalid=changed with{Placement=changed.Placement! with{Request=changed.Placement.Request with{Relations=changed.Placement.Request.Relations.Select(r=>r with{Minimum=1,Maximum=1,Ideal=1}).ToArray()}}};
        var accepted=session.Snapshot;try{session.Apply(new("refuse","Preserve accepted world","test:refuse",accepted.World.RecipeSha256,accepted.World.CatalogSha256,invalid,metadata.Catalog));throw new Exception("Infeasible edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"infeasible relation edit preserves accepted state");
        await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(metadata.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(metadata.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"packet"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"packet/world-metadata.json")))==Json(world),"saved metadata replay");
        Console.WriteLine("PASS: analytic relation intersections, rotated/translated/narrow/tangent geometry, complete catalog counts, reservations, invalid constraints, exact budgets, cancellation, replay, edits and rollback.");
    }
}
