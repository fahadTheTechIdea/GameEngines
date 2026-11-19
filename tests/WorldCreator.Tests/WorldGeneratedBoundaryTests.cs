using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldGeneratedBoundaryTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Generated boundary: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldMetadataArchetype Marker()=>new("marker","environment-member","Required purposeful surroundings",new Dictionary<string,JsonElement>(),[],[],[],
        PhysicalFootprint:new("meters","Exact surroundings source","boundary:marker",Box(-.2,-.2,.4,.4)),Access:new(MetadataOccupancyKind.Solid,[]));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Nested(int parents=2,double shift=0,double width=2)
    {
        var input=WorldNestedCoordinationTests.Example(parents);var source=input.Recipe.PopulationSource!.Recipe;var sourceCatalog=input.Catalog;
        var domain=Box(shift,0,20*parents,10);var placement=source.Placement!;
        sourceCatalog=sourceCatalog with{Archetypes=sourceCatalog.Archetypes.Select(a=>a.Id=="site"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-width/2,-1,width,2)}}:a).ToArray()};
        source=source with{Origin=[shift,0],Surface=source.Surface! with{Boundary=domain,Patches=source.Surface.Patches.Select(p=>p with{Boundary=domain}).ToArray()},
            Regions=source.Regions! with{Boundary=domain,Seeds=source.Regions.Seeds.Select(s=>s with{Position=new(s.Position.X+shift,s.Position.Y)}).ToArray()},
            Placement=placement with{Request=placement.Request with{Boundary=domain},Bindings=placement.Bindings.Select(b=>b with{Population=b.Population! with{
                Sockets=b.Population.Sockets!.Select(s=>s with{Position=new(s.Position.X+shift,s.Position.Y)}).ToArray()}}).ToArray()}};
        var catalog=sourceCatalog with{Archetypes=[..sourceCatalog.Archetypes.Select(a=>a.Id=="container"?
            a with{Parts=[..a.Parts,new("markers","marker",4,"All required perimeter members")]}:a),Marker()]};
        var program=source.Placement.Programs!.Single();var profile=program.Request.Demands[0].Profile;
        var grammar=new MetadataPopulationGrammar("Follow each actual first child footprint","boundary:child-perimeter",GeneratedBoundary:
            new(MetadataBoundarySource.Footprint,2,WorldBoundarySide.Outside,.75,1,PartId:"sites",PartIndex:0));
        var consumer=source with{PopulationSource=new(source,sourceCatalog),Placement=source.Placement with{Programs=[program with{
            Request=program.Request with{Execution=WorldPlacementExecution.Prescribed,MinimumSeparation=.25,
                Demands=[..program.Request.Demands,new("markers","Required surroundings","boundary:markers",null,profile)]},
            Bindings=[..program.Bindings,new("markers","markers",Population:grammar)]}]}};
        return(consumer,catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Root(MetadataBoundarySource kind,bool hole=false,bool disconnected=false)
    {
        var input=WorldPopulationCoordinationTests.Example();var original=input.Recipe.PopulationSource!;var source=original.Recipe;var sourceCatalog=original.Catalog;
        var domain=disconnected?Box(0,0,24,20):Box(0,0,18,10);
        source=source with{Surface=source.Surface! with{Boundary=domain,Patches=source.Surface.Patches.Select(p=>p with{Boundary=domain}).ToArray(),
                Exclusions=hole?[new("protected-hole","Retain an authored void","boundary:void",Box(1,2,2,2))]:
                    disconnected?[new("protected-channel","Retain the open channel","boundary:channel",Box(8,4,16,12))]:[]},
            Regions=source.Regions! with{Boundary=domain},Placement=source.Placement! with{Request=source.Placement.Request with{Boundary=domain}}};
        if(kind==MetadataBoundarySource.Reservation)sourceCatalog=sourceCatalog with{Archetypes=sourceCatalog.Archetypes.Select(a=>a.Id=="site"?
            a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(-2,-1.5,4,3)}}:a).ToArray()};
        if(kind==MetadataBoundarySource.Allocation)source=source with{LocalLayouts=new([
            new("halves","home","Allocate two declared programs","boundary:halves",WorldLocalLayoutStrategy.PowerDiagram,[
                new("west","Required western program","boundary:west",disconnected?new(6,2):new(4.5,5),80),
                new("east","Required eastern program","boundary:east",disconnected?new(18,2):new(13.5,5),80,AllowDisconnected:disconnected)])],[])};
        var catalog=sourceCatalog with{Archetypes=[..sourceCatalog.Archetypes.Select(a=>a.Id=="root"?
            a with{Parts=[..a.Parts,new("markers","marker",4,"All required edge members")]}:a),Marker()]};
        bool physical=kind is MetadataBoundarySource.Footprint or MetadataBoundarySource.Reservation;
        var pattern=new MetadataGeneratedBoundaryPattern(kind,2,physical?WorldBoundarySide.Outside:WorldBoundarySide.Inside,
            physical?(kind==MetadataBoundarySource.Reservation?0.5:0.75):(hole?0.4:0.5),1,PartId:physical?"sites":null,
            RegionId:physical?null:"home",AllocationId:kind==MetadataBoundarySource.Allocation?"east":null,LoopIndex:hole?1:disconnected?0:null);
        var request=source.Placement!.Request;var recipe=source with{PopulationSource=new(source,sourceCatalog),Placement=source.Placement with{
            Request=request with{Execution=WorldPlacementExecution.Prescribed,MinimumSeparation=.25,
                Demands=[..request.Demands,new("markers","Declared edge members","boundary:members",null,request.Demands[0].Profile)]},
            Bindings=[..source.Placement.Bindings,new("markers",source.Id,true,PartId:"markers",Population:
                new("Follow accepted source boundary","boundary:source",GeneratedBoundary:pattern))]}};
        return(recipe,catalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Boundary evidence exists");Directory.CreateDirectory(output);Geometry();
        var generator=new WorldMetadataGenerator();var facts=new List<object>();
        var variants=new[]{("nested",Nested()),("translated",Nested(2,3)),("wider",Nested(2,0,3)),
            ("footprint",Root(MetadataBoundarySource.Footprint)),("reservation",Root(MetadataBoundarySource.Reservation)),
            ("region",Root(MetadataBoundarySource.Region)),("hole",Root(MetadataBoundarySource.Region,true)),("allocation",Root(MetadataBoundarySource.Allocation)),
            ("disconnected-allocation",Root(MetadataBoundarySource.Allocation,disconnected:true))};
        var worlds=new Dictionary<string,WorldMetadataDocument>();
        foreach(var (name,e) in variants)
        {
            var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(name,world);var source=world.PlacementPlan!.Definition.SourceWorld!;
            int count=name is "nested" or "translated" or "wider"?15:6;
            Check(world.Nodes.Count==count&&world.AccessPlan!.Sites.Count==count-1,"complete source plus every declared surroundings member");
            foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"accepted source members remain exact");
            var traces=world.PlacementPlan.PopulationBoundaries!;Check(traces.Sum(t=>t.NodeIds.Count)==(count==15?8:4),"all generated positions have source witnesses");
            foreach(var trace in traces)
            foreach(var pair in trace.NodeIds.Zip(trace.Boundary.Positions))
            {var node=world.Nodes.Single(n=>n.Id==pair.First);Check(node.Position!.SequenceEqual(new[]{pair.Second.X,pair.Second.Y}),"world-coordinate witness equals placed node");}
            if(name=="hole")Check(traces.Single().Boundary is {Hole:true,LoopCount:2,LoopIndex:1}&&traces.Single().Boundary.Pattern.Side==WorldBoundarySide.Outside,"inside region uses the filled side of a hole");
            if(name=="disconnected-allocation")Check(traces.Single().Boundary is {Hole:false,LoopCount:2,LoopIndex:0},"clipped nonconvex allocation retains both disconnected loops");
            if(name is "nested" or "translated")Check(world.PopulationCoordinationPlan is not null,"witnessed source/edge conflict requires reconsideration");
            if(count==15&&world.PopulationCoordinationPlan is {} coordination)Check(coordination.Attempts.Where(a=>!a.Accepted).All(a=>a.AddedNogood is {WholeAssignmentSha256:null,Assignments.Count:2}),"nested source-owner/member guards exclude only attributed pairs");
            WorldMetadataGenerator.ValidateDocument(world);
            var changed=traces[0] with{Boundary=traces[0].Boundary with{SourceGeometrySha256=new string('0',64)}};
            Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=world.PlacementPlan with{PopulationBoundaries=[changed,..traces.Skip(1)]}}),
                world.PopulationCoordinationPlan is null?"METADATA_PLACEMENT_PLAN_MISMATCH":"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var saved=await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
            Check(Json(saved)==Json(world),"typed and actual file publication agree");
            facts.Add(new{Name=name,Nodes=world.Nodes.Count,Physical=world.AccessPlan!.Sites.Count,BoundaryMembers=traces.Sum(t=>t.NodeIds.Count),
                Traces=traces,Coordination=world.PopulationCoordinationPlan?.Attempts.Count,Work=world.PlacementPlan.Work});
            Console.WriteLine("Qualified generated boundary: "+name+"; "+count+" nodes; work "+world.PlacementPlan.Work);
        }
        foreach(var node in worlds["nested"].Nodes.Where(n=>n.Id.Contains("/markers-",StringComparison.Ordinal)))
        {var moved=worlds["translated"].Nodes.Single(n=>n.Id==node.Id);Check(Math.Abs(moved.Position![0]-node.Position![0]-3)<1e-8&&moved.Position[1]==node.Position[1],"actual parent frame translates generated surroundings exactly");}
        WorldMetadataRecipe Change(WorldMetadataRecipe recipe,Func<MetadataGeneratedBoundaryPattern,MetadataGeneratedBoundaryPattern> change)=>recipe with{
            Placement=recipe.Placement! with{Bindings=recipe.Placement.Bindings.Select(b=>b.Population?.GeneratedBoundary is not {} pattern?b:
                b with{Population=b.Population with{GeneratedBoundary=change(pattern)}}).ToArray()}};
        var holeInput=variants.Single(v=>v.Item1=="hole").Item2;
        Reject(()=>generator.Generate(Change(holeInput.Recipe,p=>p with{LoopIndex=null}),holeInput.Catalog),"METADATA_GENERATED_BOUNDARY_LOOP_REQUIRED");
        var footprintInput=variants.Single(v=>v.Item1=="footprint").Item2;
        Reject(()=>generator.Generate(Change(footprintInput.Recipe,p=>p with{Source=MetadataBoundarySource.Reservation}),footprintInput.Catalog),"METADATA_GENERATED_BOUNDARY_RESERVATION_MISSING");
        Reject(()=>generator.Generate(Change(footprintInput.Recipe,p=>p with{PartId="missing"}),footprintInput.Catalog),"METADATA_GENERATED_BOUNDARY_PART_MISSING");
        Reject(()=>generator.Generate(Change(footprintInput.Recipe,p=>p with{NodeId="another"}),footprintInput.Catalog),"METADATA_GENERATED_BOUNDARY_SELECTOR_INVALID");
        var disconnectedInput=variants.Single(v=>v.Item1=="disconnected-allocation").Item2;
        var connectedSource=disconnectedInput.Recipe.PopulationSource!;
        var withoutPolicy=connectedSource.Recipe with{LocalLayouts=connectedSource.Recipe.LocalLayouts! with{Layouts=
            connectedSource.Recipe.LocalLayouts.Layouts.Select(l=>l with{Programs=l.Programs.Select(p=>p with{AllowDisconnected=false}).ToArray()}).ToArray()}};
        Reject(()=>generator.Generate(withoutPolicy,connectedSource.Catalog),"WORLD_REGION_MINIMUM_AREA");
        Reject(()=>generator.Generate(Change(disconnectedInput.Recipe,p=>p with{LoopIndex=null}),disconnectedInput.Catalog),"METADATA_GENERATED_BOUNDARY_LOOP_REQUIRED");
        var baseline=variants[0].Item2;var session=new MetadataGenerationSession(generator,baseline.Recipe,baseline.Catalog);var before=session.Snapshot;
        WorldMetadataRecipe NestedChange(Func<MetadataGeneratedBoundaryPattern,MetadataGeneratedBoundaryPattern> change)=>baseline.Recipe with{
            Placement=baseline.Recipe.Placement! with{Programs=baseline.Recipe.Placement.Programs!.Select(p=>p with{Bindings=p.Bindings.Select(b=>
                b.Population?.GeneratedBoundary is not {} g?b:b with{Population=b.Population with{GeneratedBoundary=change(g)}}).ToArray()}).ToArray()}};
        foreach(var impossible in new[]{NestedChange(p=>p with{Offset=.1}),NestedChange(p=>p with{Spacing=.1})})
        {
            try{generator.Generate(impossible,baseline.Catalog);throw new Exception("Invariant overlap accepted");}
            catch(MetadataPlacementInfeasibleException error)
            {var attempts=error.Data["CoordinationAttempts"] as IReadOnlyList<MetadataPopulationCoordinationAttempt>;Check(attempts is {Count:1}&&attempts[0].AddedNogood is null,"same-owner source/member or future/future conflict refuses without unrelated source retries");}
        }
        var edited=session.Apply(new("wider-source","Recompute surrounding geometry after source size edit","boundary:edit",before.World.RecipeSha256,before.World.CatalogSha256,variants[2].Item2.Recipe,variants[2].Item2.Catalog));
        Check(Json(edited.World)==Json(worlds["wider"]),"source edit equals clean generation");
        var bad=baseline.Recipe with{Placement=baseline.Recipe.Placement! with{Programs=baseline.Recipe.Placement.Programs!.Select(p=>p with{Bindings=p.Bindings.Select(b=>b.Population?.GeneratedBoundary is not {} g?b:b with{Population=b.Population with{GeneratedBoundary=g with{Spacing=100}}}).ToArray()}).ToArray()}};
        try{session.Apply(new("no-capacity","Refuse insufficient boundary span","boundary:failed",edited.World.RecipeSha256,edited.World.CatalogSha256,bad,baseline.Catalog));throw new Exception("Capacity failure ignored");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited),"failed boundary edit preserves accepted state");
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();try{session.Apply(new("cancelled","Retain accepted boundaries","boundary:cancel",edited.World.RecipeSha256,edited.World.CatalogSha256,baseline.Recipe,baseline.Catalog),cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(Json(session.Snapshot)==Json(edited),"cancelled boundary proposal leaves accepted state exact");
        }
        await session.PublishAsync(Path.Combine(output,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(edited),"complete source/edit checkpoint replay");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(facts));
        Console.WriteLine("PASS: generated footprint/reservation/region/allocation boundaries, holes, repeated source frames, scoped owner coordination, exact source/counts, source edits, typed/file/checkpoint replay, tamper and rollback.");
    }
    private static void Geometry()
    {
        var rect=Box(0,0,4,2);WorldRegionEdge[] Edges(WorldPolygon p)=>p.Vertices.Select((v,i)=>new WorldRegionEdge(v,p.Vertices[(i+1)%p.Vertices.Count])).ToArray();
        var fragmented=new[]{new WorldRegionEdge(new(0,0),new(2,0)),new(new(2,0),new(4,0)),new(new(4,0),new(4,2)),new(new(4,2),new(0,2)),new(new(0,2),new(0,0))};
        var loops=MetadataGeneratedBoundary.Loops(fragmented,new(100000,default),default);
        Check(Json(loops.Single())==Json(rect),"exact collinear reduction preserves the rectangle");
        var reversed=MetadataGeneratedBoundary.Loops(fragmented.Reverse().Select(e=>new WorldRegionEdge(e.To,e.From)).ToArray(),new(100000,default),default);
        Check(Json(loops)==Json(reversed),"edge order and orientation cannot redesign canonical loops");
        var multiple=MetadataGeneratedBoundary.Loops([..Edges(rect),..Edges(Box(6,0,2,2))],new(100000,default),default);Check(multiple.Count==2,"disconnected outlines are retained");
        var second=Box(6,0,2,2);WorldRegionGeometry.Component[] components=[new([rect],Edges(rect),8),new([second],Edges(second),4)];
        var seed=new WorldRegionSeed("owner","Own both declared components","boundary:owner",new(1,1),10);
        var union=WorldRegionGeometry.FromComponents(components,[seed],new(100000,default),true,true).Single();
        Check(union.Cells.Count==2&&union.BoundaryEdges.Count==8&&union.Area==12,"explicit disconnected owner retains all original cells/edges/area and total minimum");
        Reject(()=>WorldRegionGeometry.FromComponents(components,[seed with{MinimumArea=1}],new(100000,default),true),"WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
        Reject(()=>WorldRegionGeometry.FromComponents(components,[seed with{MinimumArea=13}],new(100000,default),true,true),"WORLD_REGION_MINIMUM_AREA");
        Reject(()=>WorldRegionGeometry.FromComponents(components,[seed with{Position=new(5,1)}],new(100000,default),true,true),"WORLD_REGION_SEED_COMPONENT_AMBIGUOUS");
        Reject(()=>MetadataGeneratedBoundary.Loops([..Edges(rect),Edges(rect)[0]],new(100000,default),default),"METADATA_GENERATED_BOUNDARY_DUPLICATE_EDGE");
        Reject(()=>MetadataGeneratedBoundary.Loops(Edges(rect).Skip(1).ToArray(),new(100000,default),default),"METADATA_GENERATED_BOUNDARY_TOPOLOGY_UNRESOLVED");
        Reject(()=>MetadataGeneratedBoundary.Loops(fragmented,new(1,default),default),"WORLD_ROUTE_WORK_BUDGET");
        var concave=new WorldPolygon([new(0,0),new(6,0),new(6,2),new(2,2),new(2,6),new(0,6)]);
        var outside=WorldPopulationGeometry.Boundary(new(concave,0,2,1,.3,Side:WorldBoundarySide.Outside),12);
        Check(outside.All(p=>!concave.ContainsPoint(p)),"concave boundary sampling uses the requested side");
        Reject(()=>WorldPopulationGeometry.Boundary(new(rect,0,2,1,.5,Side:(WorldBoundarySide)99),2),"WORLD_BOUNDARY_PATTERN_INVALID");
        Reject(()=>WorldPopulationGeometry.Boundary(new(rect,0,100,1,.5,Side:WorldBoundarySide.Outside),2),"WORLD_BOUNDARY_WRAP_FORBIDDEN");
        Reject(()=>WorldPopulationGeometry.Boundary(new(rect,0,4,1,.5,SpanLength:2),2),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{MetadataGeneratedBoundary.Loops(fragmented,new(100000,cancelled.Token),cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: exact canonical/fragmented/concave/disconnected boundary geometry, side semantics, malformed topology, budget and cancellation.");
    }
}
