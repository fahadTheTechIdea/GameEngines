using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldLayoutAdaptationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool power=false)
    {
        var e=WorldInterfaceTests.Example();var local=e.Recipe.LocalLayouts!;
        if(power)local=local with{Layouts=[local.Layouts[0] with{Strategy=WorldLocalLayoutStrategy.PowerDiagram,Grid=null,
            Programs=local.Layouts[0].Programs.Select(p=>p with{Column=null,Row=null}).ToArray()}]};
        var policy=new MetadataLayoutAdaptation(local.Layouts[0].Id,"Fit required source sizes and entrances","adaptation:required",
            [power?new("expand",MetadataLayoutDialKind.PowerInfluence,96,0,2,"west-a"):new("expand",MetadataLayoutDialKind.GridWidth,2,0,2)]);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-7,-3,14,6),Reservation=Box(-8,-4,16,8)},
            Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p.Id=="east"?p with{BoundaryPosition=new(7,0),ApproachPosition=new(8,0)}:
                p.Id=="west"?p with{BoundaryPosition=new(-7,0),ApproachPosition=new(-8,0)}:p).ToArray()}}:a).ToArray()};
        var recipe=e.Recipe with{LocalLayouts=local,LayoutAdaptation=policy,Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{
            Anchors=[new("a",new(13,10)),new("b",new(31,10))]}},
            RemainingScopes=["Bounded local repartition with preserved required structures and entrances. This is an algorithm fixture; arbitrary topology redesign, complete world content, GameArts production and native Godot acceptance remain open."]};
        return(recipe,catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) RotationExample()
    {
        var e=Example();var source=e.Recipe.LocalLayouts!.Layouts[0];
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[source with{
                Grid=source.Grid! with{CellHeight=30},ResidualPurpose="Terrain margins outside the rotated local allocation"}]},
            LayoutAdaptation=e.Recipe.LayoutAdaptation! with{Purpose="Rotate local allocations to fit full required programs",
                Dials=[new("orientation",MetadataLayoutDialKind.GridRotation,10,-1,1)]}},e.Catalog);
    }
    public static async Task WriteRotationExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=RotationExample();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RotationAsync()
    {
        var e=RotationExample();var generator=new WorldMetadataGenerator();
        Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=null},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.LayoutAdaptationPlan!;
        Check(plan.Attempts.Count==2&&!plan.Attempts[0].Accepted&&plan.Attempts[1].Accepted,"baseline fails and authorized rotation succeeds");
        Check(plan.Attempts[0].Steps["orientation"]==0&&plan.Attempts[1].Steps["orientation"]==-1,"nearest canonical rotation chosen");
        Near(world.LocalLayoutPlan!.Definition.Layouts[0].Grid!.RotationDegrees,-10,"selected degrees");
        Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2,"complete structures and constituents retained");
        foreach(var placed in world.PlacementPlan!.Placement.Sites){Near(placed.Footprint.Footprint.SignedArea(),84,"source footprint");Near(placed.Reservation!.Footprint.SignedArea(),128,"source reservation");}
        foreach(var source in e.Recipe.LocalLayouts!.Layouts[0].Programs)
            Check(world.LocalLayoutPlan.Allocations.Single(a=>a.Id==source.Id).Program.Anchor==source.Anchor,"protected program anchor");
        // Independent half-plane oracle: the shared edge is cos(10)x - sin(10)y=20.
        double radians=Math.PI/18,c=Math.Cos(radians),s=Math.Sin(radians);
        foreach(var edge in world.LocalLayoutPlan.Interfaces.Single().Segments)
            foreach(var point in new[]{edge.From,edge.To})Near(c*point.X-s*point.Y,20,"analytic rotated interface");
        Near(world.LocalLayoutPlan.Allocations.Single(a=>a.Id=="west-a").Shape.Area,400/c,"analytic first allocation area");
        // The first column loses a left-hand wedge and gains a right-hand wedge of equal area.
        foreach(var demand in e.Recipe.Placement!.Request.Anchors)
        {
            string id=demand.Id=="a"?"local-layouts/sites-0":"local-layouts/sites-1";
            var node=world.Nodes.Single(n=>n.Id==id);Near(node.Position![0],demand.Position.X,"fixed site x");Near(node.Position[1],demand.Position.Y,"fixed site y");
        }
        Check(world.RoutePlan!.Routes.Single().Interface is not null,"physical interface route recalculated");
        WorldMetadataGenerator.ValidateDocument(world);
        var positive=e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts! with{Layouts=[e.Recipe.LocalLayouts.Layouts[0] with{
                Grid=e.Recipe.LocalLayouts.Layouts[0].Grid! with{RotationDegrees=-20}}]},
            LayoutAdaptation=e.Recipe.LayoutAdaptation! with{Dials=[new("orientation",MetadataLayoutDialKind.GridRotation,10,0,1)]}};
        var positiveWorld=generator.Generate(positive,e.Catalog);
        Check(positiveWorld.LayoutAdaptationPlan!.Attempts.Count==2&&positiveWorld.LayoutAdaptationPlan.Attempts[1].Steps["orientation"]==1,"positive rotation advances to same viable layout");
        Check(Json(positiveWorld.LocalLayoutPlan)==Json(world.LocalLayoutPlan),"opposite searches reach identical physical local geometry");
        var exact=generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{MaximumWork=plan.Work,MaximumAttempts=2}},e.Catalog);
        Check(Json(exact.LocalLayoutPlan)==Json(world.LocalLayoutPlan),"exact bounded proof accepted");
        Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{MaximumAttempts=1}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{MaximumWork=plan.Work-1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{Dials=[new("orientation",MetadataLayoutDialKind.GridRotation,361,-1,1)]}},e.Catalog),"METADATA_LAYOUT_DIAL_RANGE_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{Dials=[new("orientation",MetadataLayoutDialKind.GridRotation,10,-1,1,"west-a")]}},e.Catalog),"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        var power=Example(true);
        Reject(()=>generator.Generate(power.Recipe with{LayoutAdaptation=power.Recipe.LayoutAdaptation! with{Dials=[new("orientation",MetadataLayoutDialKind.GridRotation,10,-1,1)]}},power.Catalog),"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancelled.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        var changed=world.LocalLayoutPlan.Definition.Layouts[0];
        var tampered=world with{LocalLayoutPlan=world.LocalLayoutPlan with{Definition=world.LocalLayoutPlan.Definition with{Layouts=[changed with{Grid=changed.Grid! with{RotationDegrees=0}}]}}};
        Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(tampered),"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_INTERFACE_LAYOUT_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        try{session.Apply(new("invalid-rotation","Keep original accepted world on invalid edit","rotation:rollback",before.World.RecipeSha256,before.World.CatalogSha256,
            e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{Dials=[new("orientation",MetadataLayoutDialKind.GridRotation,361,-1,1)]}},e.Catalog));throw new Exception("Missing rollback refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_LAYOUT_DIAL_RANGE_INVALID"){}
        Check(Json(before)==Json(session.Snapshot),"invalid rotation edit rolls back");
        string directory=Path.Combine(Path.GetTempPath(),"worldcreator-rotation-"+Guid.NewGuid().ToString("N"));
        await session.PublishAsync(directory);var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(directory,"generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"rotated world and attempt history survive checkpoint restoration");
        Console.WriteLine($"PASS: complete seven-node/two-structure world fits after bounded rotation; independent half-plane/area, protected anchors, exact source geometry, physical route, attempt/work/range refusal and tamper replay. Work={plan.Work}");
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();WorldMetadataDocument? grid=null;
        foreach(bool power in new[]{false,true})
        {
            var e=Example(power);
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=null},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
            var world=generator.Generate(e.Recipe,e.Catalog);if(!power)grid=world;
            var plan=world.LayoutAdaptationPlan!;Check(plan.Attempts.Count==2&&!plan.Attempts[0].Accepted&&plan.Attempts[1].Accepted,"baseline failure then bounded repair");
            Check(plan.Attempts[0].Rejection!.StartsWith("METADATA_PLACEMENT_UNRESOLVED",StringComparison.Ordinal),"spatial failure retained");
            Check(plan.Attempts[0].Steps["expand"]==0&&plan.Attempts[1].Steps["expand"]==1,"nearest authorized parameter adjustment");
            var allocation=world.LocalLayoutPlan!.Allocations.Single(a=>a.Id=="west-a");Near(allocation.Shape.Area,440,"analytical boundary expands to x=22");
            Near(world.LocalLayoutPlan.Allocations.Single(a=>a.Id=="west-b").Shape.Area,360,"neighbor remains viable");
            Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2,"both structures and all four constituents retained");
            foreach(var source in e.Recipe.LocalLayouts!.Layouts[0].Programs)
                Check(world.LocalLayoutPlan.Allocations.Single(a=>a.Id==source.Id).Program.Anchor==source.Anchor,"protected program anchor unchanged");
            foreach(var placed in world.PlacementPlan!.Placement.Sites){Near(placed.Footprint.Footprint.SignedArea(),84,"exact physical source");Near(placed.Reservation!.Footprint.SignedArea(),128,"exact source reservation");}
            Near(world.Nodes.Single(n=>n.Id=="local-layouts/sites-0").Position![0],13,"first authored site anchor");
            Near(world.Nodes.Single(n=>n.Id=="local-layouts/sites-1").Position![0],31,"second authored site anchor");
            Near(world.RoutePlan!.Routes.Single().Interface!.Center.X,22,"physical interface recomputed after repartition");
            new MetadataLocalLayoutPlanner().ValidateDocument(world);new MetadataPlacementPlanner().ValidateDocument(world);
            Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(world with{LayoutAdaptationPlan=plan with{Attempts=plan.Attempts.Skip(1).ToArray()}}),"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{MaximumAttempts=1}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{MaximumWork=plan.Work-1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
            var limited=e.Recipe.LayoutAdaptation! with{Dials=[e.Recipe.LayoutAdaptation.Dials[0] with{Step=power?12:.25,MaximumSteps=1}]};
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=limited},e.Catalog),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
        }
        var input=Example();
        // A second terrain component has its own protected allocation. Repartitioning the
        // first owner must not reshape it, even though both participate in shared replay.
        var extended=Box(0,0,60,20);var surface=input.Recipe.Surface!;
        var reserve=new WorldLocalLayout("reserve-layout","reserve","Protected separate program","adaptation:reserve",WorldLocalLayoutStrategy.PowerDiagram,
            [new("reserve-program","Required reserve","adaptation:reserve",new(50,10),300)]);
        var isolated=input.Recipe with{Surface=surface with{Boundary=extended,Exclusions=[new("gap","Separate terrain components","adaptation:gap",Box(40,0,2,20))],Patches=[surface.Patches[0] with{Boundary=extended}]},
            Regions=input.Recipe.Regions! with{Boundary=extended,Seeds=[..input.Recipe.Regions.Seeds,new("reserve","Protected component","adaptation:reserve",new(50,10),300)]},
            LocalLayouts=input.Recipe.LocalLayouts! with{Layouts=[..input.Recipe.LocalLayouts.Layouts,reserve]}};
        var preserved=generator.Generate(isolated,input.Catalog);
        var initialLayout=new MetadataLocalLayoutPlanner().Plan(isolated.LocalLayouts!,preserved.RegionPlan!,isolated.Environment!,preserved.Surface!,isolated.Space);
        Check(Json(initialLayout.Allocations.Single(a=>a.LayoutId==reserve.Id))==Json(preserved.LocalLayoutPlan!.Allocations.Single(a=>a.LayoutId==reserve.Id)),"unrelated layout geometry and source program remain byte-identical");
        var invalidInput=input.Catalog with{Archetypes=input.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Access=a.Access! with{Ports=[a.Access.Ports[0] with{ApproachPosition=new(0,0)}]}}:a).ToArray()};
        Reject(()=>generator.Generate(input.Recipe,invalidInput),"METADATA_ACCESS_DIRECTION_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=input.Recipe.LayoutAdaptation! with{Dials=[input.Recipe.LayoutAdaptation.Dials[0] with{ProgramId="wrong"}]}},input.Catalog),"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        using(var c=new CancellationTokenSource()){c.Cancel();try{generator.Generate(input.Recipe,input.Catalog,c.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-adaptation-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"output"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"output/world-metadata.json"));Check(Json(saved)==Json(grid),"saved adapted geometry and failed attempts replay");
        Console.WriteLine("PASS: failed source/access fit repaired by grid and power boundary changes, analytical area/interface, preserved source dimensions/counts/anchors, rejection history, finite all-fail/attempt/work limits, malformed dial refusal, cancellation, replay/tamper and file export.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception("Adaptation: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
