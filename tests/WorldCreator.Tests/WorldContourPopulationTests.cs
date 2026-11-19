using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldContourPopulationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    private static WorldEnvironmentDefinition Fields()=>new(1,"contour-fields",[new("height","Terrain altitude","contour:height","meters",0,[new(WorldFieldInput.Elevation,1)]),
        new("clamped","Clamped terrain altitude","contour:clamp","meters",0,[new(WorldFieldInput.Field,1,"height")],0,5)]);
    private static WorldSurfaceDefinition Surface(WorldPolygon boundary,double gx=1,double gy=1)=>new(1,"contour-surface","meters",boundary,[],[new("ground","ground","Contour terrain","contour:terrain",boundary,0,new(0,0),0,gx,gy)]);
    private static WorldRegion Region(WorldSurfaceDefinition surface,WorldEnvironmentDefinition fields,WorldPoint seed)
        =>new WorldRegionPlanner().Plan(new(new(surface),fields),new("contour-region","Region containing terrain-derived detail","contour:region",surface.Boundary!,new("usable","All supported terrain","contour:usable",["ground"],[],[]),[new("home","Required region","contour:home",seed,1)],MaximumCells:4096)).Regions.Single();
    private static WorldContourPopulationPattern Pattern(double value,WorldPoint anchor,int count=3,double spacing=2,double inset=1,string field="height")
        =>new(field,[new("feature-contour",value,anchor,20,count,spacing,inset,1)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double offset=0)
    {
        var source=WorldRegionCompositionTests.Example(offset);var root=source.Catalog.Archetypes.Single(a=>a.Id==source.Recipe.RootArchetypeId);
        var feature=new WorldMetadataArchetype("feature","environment-feature","Required terrain-following world feature",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Source-sized environmental feature with clearance","contour:feature",Box(-.2,-.2,.4,.4),Box(-.3,-.3,.6,.6)),Access:new(MetadataOccupancyKind.Solid,[]));
        var groups=new[]{("lowland",3,-4d,new WorldPoint(-45,10),12d,10d),("highland",3,4d,new WorldPoint(40,0),12d,8d),("woodland",2,0d,new WorldPoint(-12,28),1.9,.6)};
        var catalog=source.Catalog with{Archetypes=[..source.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{Parts=[..a.Parts,..groups.Select(g=>new MetadataPart(g.Item1+"-features","feature",g.Item2,"Required "+g.Item1+" environmental features"))]}),feature]};
        var placement=source.Recipe.Placement!;
        var newDemands=groups.Select(g=>new WorldSiteDemand(g.Item1+"-features","Required terrain-derived "+g.Item1+" features","contour:"+g.Item1,null,
            g.Item1=="lowland"?source.Recipe.Regions!.Profile:source.Recipe.Regions!.Seeds.Single(s=>s.Id==g.Item1).Profile!)).ToArray();
        var bindings=groups.Select(g=>new MetadataPlacementBinding(g.Item1+"-features",source.Recipe.Id,true,PartId:g.Item1+"-features",
            Population:new("Follow the environmental contour in "+g.Item1,"contour:population:"+g.Item1,Region:new(g.Item1,Contour:new("height",[new("terrain-features",g.Item3,g.Item4,20,g.Item2,g.Item5,g.Item6,.6)]))))).ToArray();
        return(source.Recipe with{PopulationSource=new(source.Recipe,source.Catalog),Placement=placement with{Bindings=[..placement.Bindings,..bindings],Request=placement.Request with{Demands=[..placement.Request.Demands,..newDemands]}},
            RemainingScopes=["Complete six-compound program plus eight field-contour-derived environmental members. Intent seeds, field levels and counts are authored; broader area population, automatic intent, multiple floors and world-scale performance remain open. Godot deferred."]},catalog);
    }
    private static WorldMetadataRecipe Change(WorldMetadataRecipe recipe,Func<WorldContourPopulationPattern,WorldContourPopulationPattern> change)=>recipe with{Placement=recipe.Placement! with{
        Bindings=recipe.Placement.Bindings.Select(b=>b.Population?.Region?.Contour is not {} c?b:b with{Population=b.Population with{Region=b.Population.Region with{Contour=change(c)}}}).ToArray()}};
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var (name,offset) in new[]{("baseline",0d),("raised-terrain",.2)})
        {var e=Example(offset);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));}
    }
    public static async Task RunAsync()
    {
        Geometry();var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==37&&world.AccessPlan!.Sites.Count==36,"all six complete compounds and eight environmental members retained");
        Check(world.RoutePlan!.Routes.Count==19&&world.PlacementPlan!.ProgramInstances!.Count==14,"every required route and nested program retained");
        Check(world.PlacementPlan!.PopulationContours is {Count:3}&&world.PlacementPlan.PopulationContours.Sum(t=>t.NodeIds.Count)==8,"all contour paths and world-coordinate member witnesses retained");
        var source=world.PlacementPlan!.Definition.SourceWorld!;
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"source member remains exact");
        foreach(var route in source.RoutePlan!.Routes)Check(Json(route)==Json(world.RoutePlan.Routes.Single(r=>r.ConnectionId==route.ConnectionId)),"full source passage and search witness preserved");
        WorldConnectedGrowthTests.Independent(world,e.Catalog);WorldMetadataGenerator.ValidateDocument(world);
        var trace=world.PlacementPlan.PopulationContours![0];var changedTrace=trace with{Contours=trace.Contours.Select(c=>c with{AnchorDistance=c.AnchorDistance+1}).ToArray()};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=world.PlacementPlan with{PopulationContours=[changedTrace,..world.PlacementPlan.PopulationContours.Skip(1)]}}),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        var movedInput=Example(.2);var moved=generator.Generate(movedInput.Recipe,movedInput.Catalog);
        foreach(var node in world.Nodes.Where(n=>n.Kind=="environment-feature"))
        {
            var changed=moved.Nodes.Single(n=>n.Id==node.Id);Check(Json(node.Position)!=Json(changed.Position),"terrain moves every contour-derived member");
            var group=node.Id.Contains("/lowland-",StringComparison.Ordinal)?"lowland":node.Id.Contains("/highland-",StringComparison.Ordinal)?"highland":"woodland";
            double value=group=="lowland"?-4:group=="highland"?4:0;
            Near(.1*node.Position![0]+.04*node.Position[1],value,"independent base affine field value");
            Near(.2+.1*changed.Position![0]+.04*changed.Position[1],value,"independent edited affine field value");
        }
        WorldConnectedGrowthTests.Independent(moved,movedInput.Catalog);WorldMetadataGenerator.ValidateDocument(moved);
        Reject(()=>generator.Generate(Change(e.Recipe,p=>p with{Contours=p.Contours.Select(c=>c with{Spacing=10000}).ToArray()}),e.Catalog),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        Reject(()=>generator.Generate(Change(e.Recipe,p=>p with{FieldId="missing"}),e.Catalog),"WORLD_CONTOUR_PATTERN_INVALID");
        var dual=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.Population?.Region?.Contour is null?b:b with{Population=b.Population with{Region=b.Population.Region with{Pattern=new(0,[new("conflict",1,0,1)])}}}).ToArray()}};
        Reject(()=>generator.Generate(dual,e.Catalog),"METADATA_POPULATION_REGION_GRAMMAR_INVALID");
        var altered=world with{PlacementPlan=world.PlacementPlan with{TemplateDefinition=world.PlacementPlan.TemplateDefinition! with{Bindings=world.PlacementPlan.TemplateDefinition!.Bindings.Select(b=>b.Population?.Region?.Contour is not {} c?b:b with{Population=b.Population with{Region=b.Population.Region with{Contour=c with{FieldId="missing"}}}}).ToArray()}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(altered),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var detail=Change(e.Recipe,p=>p with{Contours=p.Contours.Select(c=>c with{StartInset=c.StartInset+.1}).ToArray()});
        var edited=session.Apply(new("contour-inset","Change contour population intent","contour:edit",before.World.RecipeSha256,before.World.CatalogSha256,detail,e.Catalog));
        var previousSource=before.Report.Stages.Single(s=>s.Id=="population-source");
        var editedSource=edited.Report.Stages.Single(s=>s.Id=="population-source");
        // Consumer-specific coordination guards are part of the qualified source
        // input. A population edit may change that proof even when the authored
        // source recipe is unchanged; it must not report reuse of another input.
        Check(editedSource.Reused==(previousSource.InputSha256==editedSource.InputSha256),"source reuse follows complete source/coordination input identity");
        Check(Json(edited.World)==Json(generator.Generate(detail,e.Catalog)),"population edit equals clean coordinated generation");
        var raised=session.Apply(new("terrain-change","Raise source terrain and regenerate contour population","contour:terrain-edit",edited.World.RecipeSha256,edited.World.CatalogSha256,movedInput.Recipe,movedInput.Catalog));
        Check(!raised.Report.Stages.Single(s=>s.Id=="population-source").Reused&&Json(raised.World)==Json(moved),"terrain edit invalidates source and equals clean generation");
        try{session.Apply(new("failed-contour","Insufficient contour capacity","contour:failed",raised.World.RecipeSha256,raised.World.CatalogSha256,Change(movedInput.Recipe,p=>p with{Contours=p.Contours.Select(c=>c with{Spacing=10000}).ToArray()}),movedInput.Catalog));throw new Exception("Expected edit refusal");}catch(MetadataRegenerationException){}
        Check(Json(raised)==Json(session.Snapshot),"failed population edit leaves accepted world intact");
        string temporary=Path.Combine(Path.GetTempPath(),"world-contour-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
        await session.PublishAsync(Path.Combine(temporary,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temporary,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(raised),"saved source/edit history replay");
        await new WorldMetadataSchematicExporter().CreateAsync(world);
        Console.WriteLine($"PASS: complete mixed-profile contour population, 37 nodes/36 physical members/19 routes, source preservation, terrain response, replay, edits and rollback. Placement work {world.PlacementPlan.Work}/{moved.PlacementPlan!.Work}; broad world generation remains unfinished.");
    }
    private static void Geometry()
    {
        var boundary=Box(0,0,10,10);var surface=Surface(boundary);var fields=Fields();var env=new WorldEnvironmentQuery(new(surface),fields);var region=Region(surface,fields,new(2,2));
        var pattern=Pattern(8,new(3,5));var result=WorldPopulationGeometry.Contour(region,env,pattern,3).Single();
        Check(!result.Closed,"planar contour open");Near(result.Path.Points[0].X,0,"canonical boundary start");Near(result.Path.Points[0].Y,8,"analytical contour intercept");
        for(int i=0;i<3;i++){Near(result.Positions[i].X,(1+2*i)/Math.Sqrt(2),"independent slope arc-length X");Near(result.Positions[i].Y,8-(1+2*i)/Math.Sqrt(2),"independent slope arc-length Y");}
        var reverse=WorldPopulationGeometry.Contour(region,env,pattern with{Contours=[pattern.Contours[0] with{Reverse=true}]},3).Single();Near(reverse.Positions[0].X,8-1/Math.Sqrt(2),"explicit reversed orientation");
        foreach(int size in new[]{3,5})
        {
            double step=10d/(size-1);var height=new WorldHeightField(1,"plane","ground","Equivalent triangulated plane","contour:plane","meters",new(0,0),step,step,size,size,Enumerable.Range(0,size*size).Select(i=>(i%size+i/size)*step).ToArray());
            var mesh=surface with{Patches=[],HeightField=height};var actual=WorldPopulationGeometry.Contour(Region(mesh,fields,new(2,2)),new(new(mesh),fields),pattern,3).Single();
            for(int i=0;i<3;i++){Near(actual.Positions[i].X,result.Positions[i].X,"triangulation-invariant X");Near(actual.Positions[i].Y,result.Positions[i].Y,"triangulation-invariant Y");}
        }
        var hole=surface with{Exclusions=[new("hole","Protected contour gap","contour:hole",Box(3,3,2,2))]};var holeRegion=Region(hole,fields,new(2,2));var holeEnv=new WorldEnvironmentQuery(new(hole),fields);
        var left=WorldPopulationGeometry.Contour(holeRegion,holeEnv,Pattern(8,new(1,7),2,1,.5),2).Single();Check(left.Path.Points.All(p=>p.X<=3+1e-8),"hole not concatenated across disconnected components");
        var right=WorldPopulationGeometry.Contour(holeRegion,holeEnv,Pattern(8,new(7,1),2,1,.5),2).Single();Check(right.Path.Points.All(p=>p.X>=5-1e-8),"purpose anchor chooses other true component");
        Reject(()=>WorldPopulationGeometry.Contour(holeRegion,holeEnv,Pattern(8,new(4,4)),3),"WORLD_CONTOUR_COMPONENT_AMBIGUOUS");
        Reject(()=>WorldPopulationGeometry.Contour(holeRegion,holeEnv,Pattern(8,new(1,7),3,3,1),3),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        Reject(()=>WorldPopulationGeometry.Contour(region,env,Pattern(5,new(2,3),field:"clamped"),3),"WORLD_CONTOUR_PLATEAU");
        Reject(()=>WorldPopulationGeometry.Contour(region,env,pattern with{Contours=[pattern.Contours[0] with{Anchor=new(100,100),MaximumAnchorDistance=1}]},3),"WORLD_CONTOUR_COMPONENT_MISSING");
        Reject(()=>WorldPopulationGeometry.Contour(region,env,pattern,4),"WORLD_CONTOUR_COUNT_MISMATCH");
        Reject(()=>WorldPopulationGeometry.Contour(region,env,pattern,3,maximumWork:1),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{WorldPopulationGeometry.Contour(region,env,pattern,3,token:cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        // The fixed mesh diagonal makes a six-sided elevation-one contour:
        // (1,1),(1,5),(5,9),(9,9),(9,5),(5,1). Canonical sampling starts up the x=1 edge.
        var hill=new WorldHeightField(1,"hill","ground","Four-facet hill","contour:hill","meters",new(0,0),5,5,3,3,[0,0,0,0,5,0,0,0,0]);
        var hillSurface=surface with{Patches=[],HeightField=hill};var hillRegion=Region(hillSurface,fields,new(5,5));var hillEnv=new WorldEnvironmentQuery(new(hillSurface),fields);
        var loop=WorldPopulationGeometry.Contour(hillRegion,hillEnv,Pattern(1,new(5,5),3,1,1),3).Single();Check(loop.Closed,"closed field contour retained");
        for(int i=0;i<3;i++){Near(loop.Positions[i].X,1,"independent closed-loop X");Near(loop.Positions[i].Y,2+i,"independent closed-loop Y");}
        foreach(var position in loop.Positions)Near(hillEnv.Sample(position).Values["height"],1,"closed-loop terrain field");
        Reject(()=>WorldPopulationGeometry.Contour(hillRegion,hillEnv,Pattern(1,new(5,5),2,100,0),2),"WORLD_CONTOUR_WRAP_FORBIDDEN");
        var saddleSurface=surface with{Patches=[],HeightField=hill with{Id="saddle",Heights=[1,-1,1,-1,0,-1,1,-1,1]}};
        Reject(()=>WorldPopulationGeometry.Contour(Region(saddleSurface,fields,new(5,5)),new(new(saddleSurface),fields),Pattern(0,new(5,5)),3),"WORLD_CONTOUR_BRANCH_AMBIGUOUS");
        var derived=fields with{Fields=[..fields.Fields,new("moisture","Game-authored environmental scalar","contour:moisture","ratio",0,[new(WorldFieldInput.X,2),new(WorldFieldInput.Y,-1)])]};
        var moisture=WorldPopulationGeometry.Contour(region,new(new(surface),derived),Pattern(6,new(5,4),field:"moisture"),3).Single();
        Near(moisture.Positions[0].X,3+1/Math.Sqrt(5),"independent non-altitude contour X");Near(moisture.Positions[0].Y,2/Math.Sqrt(5),"independent non-altitude contour Y");
        Console.WriteLine("PASS: analytic affine positions, reverse, equivalent mesh, holes, component ambiguity/capacity, plateaus, closed contours, field/count/work/cancellation refusal.");
    }
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected refusal: "+code);}
}
