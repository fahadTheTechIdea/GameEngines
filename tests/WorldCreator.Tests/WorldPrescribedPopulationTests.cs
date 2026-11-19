using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPrescribedPopulationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double offset=0)
    {
        var e=WorldContourPopulationTests.Example(offset);var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{Parts=a.Parts.Select(p=>p.Id is "lowland-features" or "highland-features"?p with{Count=96}:p).ToArray()}).ToArray()};
        var bindings=e.Recipe.Placement!.Bindings.Select(b=>b.Population?.Region?.Contour is not {} original||b.Population.Region.RegionId=="woodland"?b:b with{Population=b.Population with{
            Region=b.Population.Region with{Contour=original with{Contours=Enumerable.Range(0,6).Select(i=>new WorldPopulationContour("field-belt-"+i,
                b.Population.Region.RegionId=="lowland"?-6.4+.2*i:5+.2*i,b.Population.Region.RegionId=="lowland"?new(-60,-10):new(60,10),20,16,1.8,5,.6)).ToArray()}}}}).ToArray();
        return(e.Recipe with{Placement=e.Recipe.Placement with{Bindings=bindings,Request=e.Recipe.Placement.Request with{Execution=WorldPlacementExecution.Prescribed}},
            RemainingScopes=["Complete six-compound program with 194 contour-derived environmental members, exact source footprints, unchanged separation and all nineteen bidirectional source connections. Regional intent, environmental belts and required counts remain authored. Broader area distributions, automatic intent, larger solid obstacle backends and world-scale streaming/performance remain open. Godot deferred."]},catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var (name,offset) in new[]{("baseline",0d),("raised-terrain",.2)})
        {var e=Example(offset);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));}
    }
    public static void NestedProgram()
    {
        const string id="prescribed-nested";var basis=WorldSurfaceTests.Example();var domain=Box(-50,-50,100,100);var properties=new Dictionary<string,JsonElement>();
        var root=new WorldMetadataArchetype("root","region","Required parent program",properties,[new("parent","parent",1,"Required container")],[],[]);
        var parent=new WorldMetadataArchetype("parent","container","Source-sized container",properties,[new("features","feature",32,"All required local features")],[],[],
            PhysicalFootprint:new("meters","Source parent boundary","prescribed:parent",Box(-40,-40,80,80)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var feature=Example().Catalog.Archetypes.Single(a=>a.Id=="feature");var catalog=new WorldMetadataCatalog(1,"prescribed-nested-catalog",[root,parent,feature]);
        var profile=new WorldSuitabilityProfile("slots","Purposeful supported slots","prescribed:slots",["ground"],[],[]);
        var request=new WorldPlacementRequest("parent","Place required parent","prescribed:parent",domain,[new("parent","Required source parent","prescribed:parent",null,profile)],[],[],Execution:WorldPlacementExecution.Prescribed);
        var local=request with{Id="local-features",Boundary=null,Demands=[new("features","Required complete local population","prescribed:features",null,profile)]};
        var program=new MetadataNestedProgram("features","Populate every actual container","prescribed:program",id,"parent",1,1,local,
            [new("features","features",Population:new("Purposeful local feature span","prescribed:span",Path:new(new([new(-30,0),new(30,0)]),1.8,2)))]);
        var recipe=basis.Recipe with{Id=id,CatalogId=catalog.Id,Origin=[0,0],RouteRules=null,NetworkRules=null,
            Surface=new(1,"nested-ground","meters",domain,[],[new("ground","ground","Supported container terrain","prescribed:ground",domain,0,new(0,0),0,0,0)]),
            Environment=new(1,"nested-fields",[new("height","Ground level","prescribed:height","meters",0,[new(WorldFieldInput.Elevation,1)])]),
            Placement=new(request,[new("parent",id+"/parent-0",true,Population:new("Fixed parent socket","prescribed:parent",Sockets:[new(id+"/parent-0",new(0,0))]))],Programs:[program]),
            RemainingScopes=["Nested prescribed-stage qualification; no runtime or complete-world claim."]};
        var world=new WorldMetadataGenerator().Generate(recipe,catalog);var stage=world.PlacementPlan!.NestedStages.Single();
        Check(world.Nodes.Count==34&&world.AccessPlan!.Sites.Count==33&&world.PlacementPlan.ProgramInstances!.Single().ChildNodeIds.Count==32,"all reusable program members expanded above search bound");
        Check(stage.Placement.Request.Execution==WorldPlacementExecution.Prescribed&&stage.Placement.SearchStates==1&&stage.Placement.Sites.Count==32,"nested program uses one prescribed assignment");
        for(int i=0;i<32;i++){var node=world.Nodes.Single(n=>n.Id==id+"/parent-0/features-"+i);Near(node.Position![0],-28+i*1.8,"independent nested span coordinate");Near(node.Position[1],0,"nested span Y");}
        WorldMetadataGenerator.ValidateDocument(world);
        Console.WriteLine("PASS: reusable nested prescribed program expands and qualifies all 32 source-sized members in one assignment, with exact parent containment and replay.");
    }
    public static async Task RunAsync()
    {
        Geometry();LargeReservationCase();var generator=new WorldMetadataGenerator();var e=Example();var watch=Stopwatch.StartNew();var world=generator.Generate(e.Recipe,e.Catalog);watch.Stop();
        Qualify(world,e.Catalog);Console.WriteLine($"Large prescribed world baseline: 223 nodes, 222 physical members, 194 environmental features, 19 routes; {watch.Elapsed.TotalSeconds:F3}s; placement work {world.PlacementPlan!.Work}; root work {world.PlacementPlan.Placement.Work}; peak working set {Process.GetCurrentProcess().PeakWorkingSet64} bytes.");
        var changed=Example(.2);watch.Restart();var raised=generator.Generate(changed.Recipe,changed.Catalog);watch.Stop();Qualify(raised,changed.Catalog);
        Console.WriteLine($"Large prescribed world raised: {watch.Elapsed.TotalSeconds:F3}s; placement work {raised.PlacementPlan!.Work}; root work {raised.PlacementPlan.Placement.Work}.");
        Check(Json(world.Connections)==Json(raised.Connections),"terrain preserves complete topology");
        foreach(var node in world.Nodes.Where(n=>n.Kind=="environment-feature"))Check(Json(node.Position)!=Json(raised.Nodes.Single(n=>n.Id==node.Id).Position),"terrain moves every required feature");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Execution=WorldPlacementExecution.Search}}},e.Catalog),"METADATA_PLACEMENT_PART_BINDING_COUNT");
        var altered=world with{PlacementPlan=world.PlacementPlan! with{Placement=world.PlacementPlan.Placement with{SearchStates=2}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(altered),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("terrain","Move all prescribed belts with terrain","prescribed:terrain",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(Json(after.World)==Json(raised)&&!after.Report.Stages.Single(s=>s.Id=="population-source").Reused,"incremental terrain output equals clean and invalidates source");
        var oversized=changed.Catalog with{Archetypes=changed.Catalog.Archetypes.Select(a=>a.Id!="feature"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-1,-1,2,2),Reservation=Box(-1.2,-1.2,2.4,2.4)}}).ToArray()};
        try{session.Apply(new("invalid","Required source features no longer fit their prescribed belts","prescribed:invalid",after.World.RecipeSha256,after.World.CatalogSha256,changed.Recipe,oversized));throw new Exception("Expected refusal");}catch(MetadataRegenerationException){}
        Check(Json(after)==Json(session.Snapshot),"failed large assignment preserves complete accepted state");
        string temp=Path.Combine(Path.GetTempPath(),"prescribed-world-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        await session.PublishAsync(Path.Combine(temp,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temp,"session/generation-session.json"));Check(Json(after)==Json(restored.Snapshot),"saved large world and edit history replay");
        await new WorldMetadataSchematicExporter().CreateAsync(world);
        Console.WriteLine("PASS: prescribed scale, all source-sized compounds and 194 environmental members, source paths/widths, terrain response, replay, edits and transactional failure; broad area distributions and world-scale throughput remain open.");
    }
    private static void Qualify(WorldMetadataDocument world,WorldMetadataCatalog catalog)
    {
        Check(world.Nodes.Count==223&&world.AccessPlan!.Sites.Count==222,"all required world members retained");
        Check(world.Nodes.Count(n=>n.Kind=="environment-feature")==194&&world.RoutePlan!.Routes.Count==19,"all population and routes retained");
        var plan=world.PlacementPlan!;Check(plan.Placement.Request.Execution==WorldPlacementExecution.Prescribed&&plan.Placement.Sites.Count==200&&plan.Placement.SearchStates==1,"one complete prescribed root assignment");
        Check(plan.Placement.Diagnostics.All(d=>d.Candidates==1&&d.Eligible==1)&&plan.ProgramInstances!.Count==14,"one exact candidate each and every nested program");
        Check(plan.PopulationContours!.Count==3&&plan.PopulationContours.Sum(t=>t.Contours.Count)==13,"all connected terrain belts inspectable");
        var source=plan.Definition.SourceWorld!;
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"source member exact");
        foreach(var route in source.RoutePlan!.Routes)Check(Json(route)==Json(world.RoutePlan!.Routes.Single(r=>r.ConnectionId==route.ConnectionId)),"full source route exact");
        var map=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach(var trace in plan.PopulationContours)
        {
            var requests=plan.TemplateDefinition!.Bindings.Single(b=>b.DemandId==trace.DemandId).Population!.Region!.Contour!.Contours;int index=0;
            for(int i=0;i<trace.Contours.Count;i++)foreach(var position in trace.Contours[i].Positions)
            {
                var node=map[trace.NodeIds[index++]];Near(node.Position![0],position.X,"witness X");Near(node.Position[1],position.Y,"witness Y");
                double offset=world.Surface!.Patches.Single(p=>p.Id=="land").Elevation;Near(offset+.1*position.X+.04*position.Y,requests[i].Value,"independent affine environmental level");
            }
        }
        // Independent rectangle-distance oracle for every environmental reservation.
        var sites=plan.Placement.Sites;var bindings=plan.Definition.Bindings.ToDictionary(b=>b.DemandId,StringComparer.Ordinal);
        foreach(var (a,i) in sites.Select((s,i)=>(s,i)))foreach(var b in sites.Skip(i+1))
        {
            if(map[bindings[a.DemandId].NodeId].Kind!="environment-feature"||map[bindings[b.DemandId].NodeId].Kind!="environment-feature")continue;
            double dx=Math.Max(0,Math.Abs(a.Position.X-b.Position.X)-.6),dy=Math.Max(0,Math.Abs(a.Position.Y-b.Position.Y)-.6);
            Check(Math.Sqrt(dx*dx+dy*dy)>=1-1e-7,"all 194 fixed environmental reservations preserve unchanged one-meter separation");
        }
        WorldConnectedGrowthTests.Independent(world,catalog);
    }
    private static void Geometry()
    {
        var boundary=Box(-110,-110,220,220);var surface=new WorldSurfaceDefinition(1,"prescribed-floor","meters",boundary,[],[new("floor","ground","Supported domain","prescribed:floor",boundary,0,new(0,0),0,0,0)]);
        var fields=new WorldEnvironmentDefinition(1,"prescribed-fields",[new("height","Altitude","prescribed:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var env=new WorldEnvironmentQuery(new(surface),fields);var profile=new WorldSuitabilityProfile("fixed","Required supported slots","prescribed:fixed",["ground"],[],[]);
        var points=Enumerable.Range(0,256).Select(i=>new WorldPoint(100*Math.Cos(i*2*Math.PI/256),100*Math.Sin(i*2*Math.PI/256))).ToArray();
        var demands=points.Select((p,i)=>new WorldSiteDemand("slot-"+i,"Required ring slot","prescribed:slot",Box(-.2,-.2,.4,.4),profile)).ToArray();
        var anchors=points.Select((p,i)=>new WorldPlacementAnchor("anchor-"+i,p)).ToArray();
        var relations=points.Select((p,i)=>new WorldPlacementRelation("pin-"+i,"slot-"+i,"anchor-"+i,"Exact prescribed slot","prescribed:pin",0,0,0,1)).ToArray();
        var request=new WorldPlacementRequest("slots","Complete prescribed ring","prescribed:slots",boundary,demands,anchors,relations,MinimumSeparation:1,MaximumSearchStates:1,Execution:WorldPlacementExecution.Prescribed);
        var owner=new WorldSitePlacementPlanner();var result=owner.Plan(env,request);Check(result.Status==WorldPlacementStatus.Found&&result.Sites.Count==256&&result.SearchStates==1,"256 exact slots without combinatorial search");
        foreach(var site in result.Sites){int i=int.Parse(site.DemandId[5..],System.Globalization.CultureInfo.InvariantCulture);Check(site.Position==points[i],"every prescribed coordinate exact");}
        foreach(var (p,i) in points.Select((p,i)=>(p,i)))foreach(var q in points.Skip(i+1))Check(p.DistanceTo(q)>=1+Math.Sqrt(.32),"independent enclosing-circle separation bound");
        Reject(()=>owner.Plan(env,request with{Execution=WorldPlacementExecution.Search}),"WORLD_PLACEMENT_REQUEST_INVALID");
        Reject(()=>owner.Plan(env,request with{Relations=relations.Skip(1).ToArray()}),"WORLD_PLACEMENT_PRESCRIBED_ANCHOR_REQUIRED");
        Reject(()=>owner.Plan(env,request with{Anchors=[..anchors,new("different",new(0,0))],Relations=[..relations,new("conflict","slot-0","different","Conflicting exact intent","prescribed:conflict",0,0,0,1)]}),"WORLD_PLACEMENT_PRESCRIBED_ANCHOR_CONFLICT");
        var shifted=request with{Anchors=anchors.Select((a,i)=>i==1?a with{Position=points[0]}:a).ToArray()};var rejected=owner.Plan(env,shifted);Check(rejected.Status==WorldPlacementStatus.NoFeasibleAssignment&&rejected.Sites.Count==0,"cross-member overlap refuses entire prescribed assignment");
        var outOfBounds=request with{Anchors=anchors.Select((a,i)=>i==0?a with{Position=new(110,0)}:a).ToArray()};Check(owner.Plan(env,outOfBounds).Status==WorldPlacementStatus.NoFeasibleAssignment,"whole footprint boundary retained");
        var impossible=request with{Relations=[..relations,new("between","slot-0","slot-128","Required impossible separation","prescribed:relation",0,1,0,1)]};Check(owner.Plan(env,impossible).Status==WorldPlacementStatus.NoFeasibleAssignment,"cross-member relation preserved");
        Reject(()=>owner.Plan(env,request with{MaximumCandidates=255}),"WORLD_PLACEMENT_CANDIDATE_BUDGET");
        Reject(()=>owner.Plan(env,request with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>owner.Plan(env,request with{Execution=(WorldPlacementExecution)42}),"WORLD_PLACEMENT_EXECUTION_INVALID");
        Reject(()=>owner.Plan(env,request with{Growth=new("growth","Conflicting algorithm","prescribed:growth","slot-0","anchor-0",[],[0],1,10)}),"WORLD_PLACEMENT_PRESCRIBED_GROWTH_CONFLICT");
        Reject(()=>owner.Plan(env,request with{Demands=Enumerable.Range(0,1025).Select(i=>demands[0] with{Id="too-many-"+i}).ToArray()}),"WORLD_PLACEMENT_REQUEST_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{owner.Plan(env,request,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Console.WriteLine($"PASS: 256 exact prescribed slots; independent separation, whole-footprint boundary, complete failure, relationships, missing/conflicting pins, unchanged search cap, work/candidate/count/mode/growth/cancellation refusal; work {result.Work}.");
    }
    private static void LargeReservationCase()
    {
        // A local storage program isolates the removed reservation-projection cap. These
        // reservations are deliberately nonsolid; they still must preserve source passages.
        var e=WorldRoutePopulationTests.Example();var input=e.Recipe.PopulationSource!;var generator=new WorldMetadataGenerator();
        var source=generator.Generate(input.Recipe,input.Catalog);var path=source.RoutePlan!.Routes[0].Forward.Path!.Points;
        var midpoint=new WorldPoint((path[0].X+path[1].X)/2,(path[0].Y+path[1].Y)/2);var intent=midpoint.Offset(0,1);
        var root=input.Catalog.Archetypes.Single(a=>a.Id==input.Recipe.RootArchetypeId);
        var reserve=new WorldMetadataArchetype("reserve","storage-reservation","Required storage reservation",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Source-sized reserved storage slot","prescribed:reserve",Box(-.05,-.05,.1,.1),Box(-.06,-.06,.12,.12)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var catalog=input.Catalog with{Archetypes=[..input.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{Parts=[..a.Parts,new("reserves","reserve",299,"Local storage slots"),new("crossing","crossing-reserve",1,"Reserve beside the source passage")]}),reserve,reserve with{Id="crossing-reserve"}]};
        var placement=input.Recipe.Placement!;var demand=placement.Request.Demands[0] with{Id="reserves"};
        var recipe=input.Recipe with{PopulationSource=input,Placement=placement with{Request=placement.Request with{Execution=WorldPlacementExecution.Prescribed,Demands=[..placement.Request.Demands,demand,demand with{Id="crossing"}]},
            Bindings=[..placement.Bindings,new("reserves",input.Recipe.Id,true,PartId:"reserves",Population:new("Local storage slot sockets","prescribed:storage",Sockets:Enumerable.Range(0,299).Select(i=>new MetadataPopulationSocket(input.Recipe.Id+"/reserves-"+i,new(.5+(i%100)*.45,1.5+(i/100)))).ToArray())),
                new("crossing",input.Recipe.Id,true,PartId:"crossing",Population:new("Reservation beside the protected passage","prescribed:crossing",Sockets:[new(input.Recipe.Id+"/crossing-0",intent)]))]}};
        var accepted=generator.Generate(recipe,catalog);Check(accepted.AccessPlan!.Sites.Count==307&&accepted.PlacementPlan!.Placement.Sites.Count==307,"all 300 reservations plus seven source sites survive one complete stage");
        var blocked=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id!="crossing-reserve"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(-.06,-1.06,.12,1.12)}}).ToArray()};
        Reject(()=>generator.Generate(recipe,blocked),"METADATA_POPULATION_PASSAGE_BLOCKED");
        Console.WriteLine("PASS: 307-member prescribed storage program preserves source routes; one reservation-only passage conflict rejects with unchanged solid footprints, beyond the former 256 projected-shape limit.");
    }
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected refusal: "+code);}
}
