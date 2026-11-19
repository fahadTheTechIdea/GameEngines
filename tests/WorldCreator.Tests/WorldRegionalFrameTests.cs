using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalFrameTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Regional frame: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-6,message+": "+actual+" vs "+expected);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    private static WorldPolygon Translate(WorldPolygon shape,double x,double y)=>new(shape.Vertices.Select(p=>p.Offset(x,y)).ToArray());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double x=0,double y=0,bool power=false)
    {
        var e=WorldLayoutAdaptationTests.RotationExample();var source=e.Recipe.LocalLayouts!.Layouts[0];
        var local=source with{RegionFrame=new("Reusable content about the computed regional anchor","frame:content"),
            Programs=source.Programs.Select((p,i)=>p with{Anchor=new(i==0?-7:11,0),Column=power?null:p.Column,Row=power?null:p.Row}).ToArray(),
            Strategy=power?WorldLocalLayoutStrategy.PowerDiagram:source.Strategy,Grid=power?null:source.Grid! with{Origin=new(-20,-10)}};
        var surface=e.Recipe.Surface!;
        return(e.Recipe with{Origin=[x,y],Surface=surface with{Boundary=Translate(surface.Boundary!,x,y),
                Patches=surface.Patches.Select(p=>p with{Boundary=Translate(p.Boundary,x,y),Origin=p.Origin.Offset(x,y)}).ToArray()},
            Regions=e.Recipe.Regions! with{Boundary=Translate(e.Recipe.Regions.Boundary,x,y),Seeds=[],
                Intents=[new("west","Terrain-selected content region","frame:region",700,9)],AnchorPrecision=.1},
            LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[local]},LayoutAdaptation=power?null:e.Recipe.LayoutAdaptation,
            Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Boundary=Translate(e.Recipe.Placement.Request.Boundary!,x,y),Anchors=[]},
                AllocationAnchors=[new("a","west-a","Start first required structure at its program anchor","frame:a"),
                    new("b","west-b","Start second required structure at its program anchor","frame:b")]},
            RemainingScopes=["Relative regional content and allocation anchors; complete source-sized local program. Broad strategy synthesis, whole-world scale, artwork and Godot remain open."]},e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var (name,x,y,power) in new[]{("grid",0d,0d,false),("translated-grid",-80d,40d,false),("weighted",0d,0d,true)})
        {
            var e=Example(x,y,power);string path=Path.Combine(directory,name);Directory.CreateDirectory(path);
            await File.WriteAllTextAsync(Path.Combine(path,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(path,"catalog.json"),Json(e.Catalog));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();
        foreach(bool power in new[]{false,true})foreach(var (x,y) in new[]{(0d,0d),(-80d,40d)})
        {
            var e=Example(x,y,power);var world=generator.Generate(e.Recipe,e.Catalog);
            Check(Json(world.LocalLayoutPlan!.Definition.Layouts[0].RegionFrame)==Json(e.Recipe.LocalLayouts!.Layouts[0].RegionFrame),"source frame retained");
            Check(Json(world.LocalLayoutPlan.Definition.Layouts[0].Programs)==Json(e.Recipe.LocalLayouts.Layouts[0].Programs),"original offsets retained");
            var seed=world.RegionPlan!.Regions.Single().Seed.Position;Near(seed.X,20+x,"computed seed x");Near(seed.Y,10+y,"computed seed y");
            Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2&&world.RoutePlan!.Routes.Count==1,"all structures, constituents and physical connection");
            foreach(var source in e.Recipe.LocalLayouts.Layouts[0].Programs)
            {
                var allocation=world.LocalLayoutPlan.Allocations.Single(a=>a.Id==source.Id);
                Near(allocation.Program.Anchor.X,source.Anchor.X+20+x,"independent absolute anchor x");Near(allocation.Program.Anchor.Y,source.Anchor.Y+10+y,"independent absolute anchor y");
                var bound=e.Recipe.Placement!.AllocationAnchors!.Single(b=>b.AllocationId==source.Id);
                Check(world.PlacementPlan!.Definition.Request.Anchors.Single(a=>a.Id==bound.AnchorId).Position==allocation.Program.Anchor,"placement consumes computed allocation anchor");
            }
            foreach(var site in world.PlacementPlan!.Placement.Sites){Near(site.Footprint!.Footprint.SignedArea(),84,"source physical area");Near(site.Reservation!.Footprint.SignedArea(),128,"source reservation area");}
            Near(world.Nodes.Single(n=>n.Id=="local-layouts/sites-0").Position![0],13+x,"first structure follows terrain");
            Near(world.Nodes.Single(n=>n.Id=="local-layouts/sites-1").Position![1],10+y,"second structure follows terrain");
            Near(world.LocalLayoutPlan.Allocations.Single(a=>a.Id=="west-a").Shape.Area,power?440:400/Math.Cos(Math.PI/18),"analytic allocation area");
            WorldMetadataGenerator.ValidateDocument(world);
            if(!power)Check(world.LayoutAdaptationPlan!.Attempts.Count==2,"relative-frame layout composes with rotation search");
            Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(world with{LocalLayoutPlan=world.LocalLayoutPlan with{Definition=world.LocalLayoutPlan.Definition with{
                Layouts=[world.LocalLayoutPlan.Definition.Layouts[0] with{RegionFrame=null}]}}}),power?"WORLD_LAYOUT_PROGRAM_INVALID":"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
        }
        var input=Example();var original=input.Recipe.LocalLayouts!.Layouts[0];
        // A relative root allocation can host either a relative or a protected
        // absolute nested declaration. Nested frames remain region-relative.
        foreach(bool relative in new[]{false,true})
        {
            var child=new WorldLocalLayout("child-layout","west","Required nested allocation","frame:child",WorldLocalLayoutStrategy.PowerDiagram,
                [new("child-program","Nested content scope","frame:child",relative?new(-7,0):new(13,10),100)],
                ParentAllocationId:"west-a",RegionFrame:relative?new("Nested scope follows region","frame:nested"):null);
            var nested=generator.Generate(input.Recipe with{LocalLayouts=input.Recipe.LocalLayouts with{Layouts=[original,child]}},input.Catalog);
            Check(nested.LocalLayoutPlan!.Allocations.Single(a=>a.Id=="child-program").Program.Anchor==new WorldPoint(13,10),"nested relative and absolute frames resolve same authored point");
            Near(nested.LocalLayoutPlan.Allocations.Single(a=>a.Id=="child-program").Shape.Area,400/Math.Cos(Math.PI/18),"child retains complete parent geometry");
            WorldMetadataGenerator.ValidateDocument(nested);
        }
        foreach(bool adaptation in new[]{false,true})
        {
            var regional=WorldRegionFeedbackTests.Example(protectedLayout:!adaptation,adaptation:adaptation);var local=regional.Recipe.LocalLayouts!.Layouts[0];
            var moving=regional.Recipe with{LocalLayouts=regional.Recipe.LocalLayouts with{Layouts=[local with{
                    RegionFrame=new("Local program follows selected terrain component","frame:reselect"),
                    Programs=[local.Programs[0] with{Anchor=new(0,0)}],Grid=local.Grid is {} grid?grid with{Origin=new(-60,-10)}:null}]},
                Placement=regional.Recipe.Placement! with{RegionAnchors=null,AllocationAnchors=[new("arrival","program-area","Program arrival","frame:arrival")]}};
            var selected=generator.Generate(moving,regional.Catalog);
            Check(selected.ProgramCompositionPlan!.Attempts.Count==2&&selected.Nodes.Count==17&&selected.RoutePlan!.Routes.Count==11,"relative frame preserves complete program through regional reselection");
            Check(selected.LocalLayoutPlan!.Allocations.Single().Program.Anchor==new WorldPoint(120,0),"relative anchor follows second terrain component");
            if(adaptation)Check(selected.LayoutAdaptationPlan!.Attempts.Count==2,"regional choice composes with relative width adaptation");
            WorldConnectedGrowthTests.Independent(selected,regional.Catalog,false);
            WorldMetadataGenerator.ValidateDocument(selected);
            var impossible=moving.LocalLayouts!.Layouts[0] with{Programs=[moving.LocalLayouts.Layouts[0].Programs[0] with{Anchor=new(1000,0)}]};
            Reject(()=>generator.Generate(moving with{LocalLayouts=moving.LocalLayouts with{Layouts=[impossible]}},regional.Catalog),"METADATA_PROGRAM_COMPOSITION_ATTEMPT_BUDGET");
            Reject(()=>generator.Generate(moving with{LocalLayouts=moving.LocalLayouts with{Layouts=[impossible with{Programs=[impossible.Programs[0],impossible.Programs[0]]}]}},regional.Catalog),"WORLD_LAYOUT_PROGRAM_INVALID");
        }
        Reject(()=>generator.Generate(input.Recipe with{LocalLayouts=input.Recipe.LocalLayouts with{Layouts=[original with{RegionFrame=new("","frame:invalid")}] }},input.Catalog),"WORLD_LAYOUT_REGION_FRAME_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{Placement=input.Recipe.Placement! with{AllocationAnchors=[new("a","absent","Missing program","frame:missing")] }},input.Catalog),"METADATA_ALLOCATION_ANCHOR_REQUIRED");
        Reject(()=>generator.Generate(input.Recipe with{Placement=input.Recipe.Placement! with{Request=input.Recipe.Placement.Request with{Anchors=[new("a",new(13,10))]}}},input.Catalog),"METADATA_ALLOCATION_ANCHOR_BINDING_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{Placement=input.Recipe.Placement! with{RegionAnchors=[new("a","west","Duplicate source","frame:duplicate")] }},input.Catalog),"METADATA_ALLOCATION_ANCHOR_BINDING_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LocalLayouts=null,LayoutAdaptation=null},input.Catalog),"METADATA_ALLOCATION_ANCHOR_DEPENDENCIES_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LocalLayouts=input.Recipe.LocalLayouts with{MaximumWork=1}},input.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(input.Recipe,input.Catalog,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;var moved=Example(-80,40);
        var changed=session.Apply(new("translate","Move terrain while retaining reusable local program","frame:edit",before.World.RecipeSha256,before.World.CatalogSha256,moved.Recipe,moved.Catalog));
        Check(Json(changed.World)==Json(generator.Generate(moved.Recipe,moved.Catalog)),"terrain edit equals clean generation");
        string directory=Path.Combine(Path.GetTempPath(),"regional-frame-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(directory);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(directory,"generation-session.json"));Check(Json(changed)==Json(restored.Snapshot),"source and frame checkpoint replay");
        Console.WriteLine("PASS: four complete seven-node/two-structure terrain/grid/weighted worlds, nested relative/fixed allocations and two complete 17-node/11-route regional reselections; unchanged relative programs follow computed seeds/allocation anchors; independent coordinates/areas, full sources/routes, rotation/width adaptation, invalid/bounded/cancelled refusal and source-edit/checkpoint replay.");
    }
}
