using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldProgramSpaceTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Program spaces: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-6,message+": "+actual+" / "+expected);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static (WorldEnvironmentQuery Query,WorldRegionPlan Regions) Region(WorldPolygon? hole=null)
    {
        var boundary=Box(0,0,10,4);var fields=new WorldEnvironmentDefinition(1,"fields",[
            new("height","Height","space:height","meters",0,[new(WorldFieldInput.Elevation,1)]),
            new("density","Game-authored density","space:density","relative",1,[new(WorldFieldInput.Elevation,1)])]);
        var surface=new WorldSurfaceDefinition(1,"land","meters",boundary,hole is null?[]:[new("hole","Protected void","space:hole",hole)],
            [new("ground","ground","Ground","space:ground",boundary,0,new(0,0),0,1,0)]);
        var query=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),fields);
        var profile=new WorldSuitabilityProfile("usable","Required region","space:region",["ground"],[],[]);
        var regions=new WorldRegionPlanner().Plan(query,new("land","Required region","space:region",boundary,profile,[new("region","Required region","space:region",new(.25,.25),1)]));
        return(query,regions);
    }
    private static WorldLocalLayoutDefinition Definition(bool weighted=false)=>new([
        new("layout","region","Allocate required programs without coordinates","space:layout",WorldLocalLayoutStrategy.AreaBalanced,[],
            ProgramIntents:[new("a","First required program","space:a",5),new("b","Second required program","space:b",5)],AreaPattern:new(weighted?"density":null))],
        [new("shared","a","b","Required shared interface","space:interface",1)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool weighted=false,bool alternative=false,bool unequal=false)
    {
        var e=WorldAnchorPartitionTests.Example();var layout=e.Recipe.LocalLayouts!.Layouts[0];
        var generated=layout with{Strategy=WorldLocalLayoutStrategy.AreaBalanced,Programs=[],RegionFrame=null,
            ProgramIntents=layout.Programs.Select((p,i)=>new WorldLocalProgramIntent(p.Id,p.Purpose,p.SourceRuleId,unequal&&i==1?400:p.MinimumArea)).ToArray(),
            AreaPattern=new(weighted?"program-density":null,AxisRotationDegrees:alternative?90:0)};
        var fields=e.Recipe.Environment! with{Fields=[..e.Recipe.Environment.Fields,
            new("program-density","Density selected by game intent","space:density","relative",1,[new(WorldFieldInput.Elevation,.1)])]};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(-8,-6,16,12)}}:a).ToArray()};
        var recipe=e.Recipe with{Environment=fields,LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[generated]},
            RemainingScopes=["Required local program spaces and anchors generated from terrain; full declared structures and route retained. Broad world design, artwork and Godot remain open."]};
        if(alternative)recipe=recipe with{LayoutAdaptation=new(generated.Id,"Choose a grammar fitting complete source reservations","space:choice",[],
            Alternatives:[new("long-axis","Use the principal area axis","space:choice",generated with{AreaPattern=generated.AreaPattern! with{AxisRotationDegrees=0}})])};
        return(recipe,catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var (name,weighted,alternative,unequal) in new[]{("uniform",false,false,false),("weighted",true,false,false),("alternative",false,true,false),("unequal",false,false,true)})
        {
            string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);var e=Example(weighted,alternative,unequal);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }
    public static async Task RunAsync()
    {
        var planner=new WorldLocalLayoutPlanner();var r=Region();var d=Definition();var plan=planner.Plan(r.Query,r.Regions,d);
        Check(plan.Definition.Layouts[0].Programs.Count==0&&plan.GeneratedAreas!.Count==1,"source has intents only; area provenance saved");
        foreach(var a in plan.Allocations){Near(a.Shape.Area,20,"uniform area");Near(a.Program.Anchor.X,a.Id=="a"?2.5:7.5,"uniform centroid X");Near(a.Program.Anchor.Y,2,"uniform centroid Y");}
        Near(plan.Areas[0].ResidualArea,0,"full region accounted");
        var unequal=planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ProgramIntents=[d.Layouts[0].ProgramIntents![0],d.Layouts[0].ProgramIntents![1] with{MinimumArea=15}]}]});
        Near(unequal.Allocations[0].Shape.Area,10,"unequal required areas derive proportional first space");Near(unequal.Allocations[1].Shape.Area,30,"unequal required areas derive proportional second space");
        Near(unequal.Allocations[0].Program.Anchor.X,1.25,"unequal first centroid");Near(unequal.Allocations[1].Program.Anchor.X,6.25,"unequal second centroid");
        Check(unequal.GeneratedAreas![0].AreaPlan.RelativeWeights!.SequenceEqual([5d,15d]),"saved weights trace exact minimum-area intent");
        var odd=planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ProgramIntents=[new("a","First","space:a",5),new("b","Middle","space:b",10),new("c","Last","space:c",5)]}],Interfaces=[]});
        Near(odd.Allocations[0].Shape.Area,10,"odd weighted first area");Near(odd.Allocations[1].Shape.Area,20,"odd weighted middle area");Near(odd.Allocations[2].Shape.Area,10,"odd weighted final area");
        var weighted=planner.Plan(r.Query,r.Regions,Definition(true));double cut=Math.Sqrt(61)-1;
        double leftMoment=4*(cut*cut/2+cut*cut*cut/3),totalMoment=4*(50+1000d/3);
        Near(weighted.Allocations[0].Shape.Area,4*cut,"analytical weighted left area");
        Near(weighted.Allocations[0].Program.Anchor.X,leftMoment/120,"analytical weighted left centroid");
        Near(weighted.Allocations[1].Program.Anchor.X,(totalMoment-leftMoment)/120,"analytical weighted right centroid");
        foreach(var s in weighted.GeneratedAreas![0].AreaPlan.Strata)Near(s.Mass,120,"equal source mass per program");
        Check(Json(plan)==Json(planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ProgramIntents=d.Layouts[0].ProgramIntents!.Reverse().ToArray()}]})),"intent order canonical");
        var h=Region(Box(4,1,2,2));var holed=planner.Plan(h.Query,h.Regions,d);Near(holed.Areas[0].AllocatedArea,36,"protected hole retained");
        Near(holed.Allocations[0].Program.Anchor.X,41d/18,"holed-region centroid");
        var one=d with{Layouts=[d.Layouts[0] with{ProgramIntents=[d.Layouts[0].ProgramIntents![0]]}],Interfaces=[]};
        Reject(()=>planner.Plan(h.Query,h.Regions,one),"WORLD_AREA_CENTROID_OUTSIDE:");
        var parent=d.Layouts[0] with{Id="parent",ProgramIntents=[new("host","Parent scope","space:parent",1)]};
        var nested=planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ParentAllocationId="host"},parent]});
        Check(nested.GeneratedAreas!.Count==2&&nested.GeneratedAreas.Single(a=>a.LayoutId=="layout").AreaPlan.RegionId=="host","nested spaces use verified parent geometry");
        Near(nested.Areas.Single(a=>a.LayoutId=="layout").AllocatedArea,40,"nested parent fully covered");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{Programs=[new("unused","Forbidden authored coordinate","space:bad",new(1,1),1)]}]}),"WORLD_LAYOUT_REQUEST_INVALID");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ProgramIntents=null}]}),"WORLD_LAYOUT_PROGRAM_INTENTS_INVALID");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{AreaPattern=new("absent")}]}),"WORLD_AREA_PATTERN_INVALID");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ProgramIntents=[d.Layouts[0].ProgramIntents![0],d.Layouts[0].ProgramIntents![0]]}]}),"WORLD_LAYOUT_PROGRAM_INTENT_INVALID");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ProgramIntents=d.Layouts[0].ProgramIntents!.Select(p=>p with{MinimumArea=100}).ToArray()}]}),"WORLD_REGION_MINIMUM_AREA:");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{MaximumCells=1}),"WORLD_LAYOUT_CELL_BUDGET");
        Check(planner.Plan(r.Query,r.Regions,d with{MaximumWork=plan.Work}).Work==plan.Work,"exact shared work boundary");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(r.Query,r.Regions,d,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var generator=new WorldMetadataGenerator();var input=Example();var world=generator.Generate(input.Recipe,input.Catalog);
        var density=Example(true);var moved=generator.Generate(density.Recipe,density.Catalog);
        var differing=Example(unequal:true);var differentWorld=generator.Generate(differing.Recipe,differing.Catalog);
        Near(differentWorld.LocalLayoutPlan!.Allocations[0].Shape.Area,800*3d/7,"full physical program gets demand-proportional first area");
        foreach(var w in new[]{world,moved,differentWorld})
        {
            Check(w.Nodes.Count==7&&w.PlacementPlan!.Placement.Sites.Count==2&&w.RoutePlan!.Routes.Count==1,"complete physical content and required route retained");
            foreach(var site in w.PlacementPlan!.Placement.Sites){Near(Math.Abs(site.Footprint.Footprint.SignedArea()),84,"source footprint");Near(Math.Abs(site.Reservation!.Footprint.SignedArea()),192,"source reservation");}
            Check(w.LocalLayoutPlan!.Definition.Layouts[0].Programs.Count==0&&w.LocalLayoutPlan.Definition.Layouts[0].RegionFrame is null,"no local authored anchors/frame");WorldMetadataGenerator.ValidateDocument(w);
        }
        Check(Json(world.Nodes.Select(n=>n with{Position=null}))==Json(moved.Nodes.Select(n=>n with{Position=null})),"density changes no content identities");
        Check(Json(world.PlacementPlan!.Placement.Sites.Select(s=>s.Footprint.Footprint))!=Json(moved.PlacementPlan!.Placement.Sites.Select(s=>s.Footprint.Footprint)),"density moves physical members");
        var alt=Example(alternative:true);var selected=generator.Generate(alt.Recipe,alt.Catalog);
        Check(selected.LayoutAdaptationPlan!.Attempts.Count==2&&!selected.LayoutAdaptationPlan.Attempts[0].Accepted&&selected.LayoutAdaptationPlan.Attempts[1].Accepted,"grammar alternative preserves hard intents and source reservations");
        var tampered=world with{LocalLayoutPlan=world.LocalLayoutPlan! with{GeneratedAreas=[]}};
        Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(tampered),"METADATA_LOCAL_LAYOUT_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;
        var accepted=session.Apply(new("density","Move programs using declared terrain density","space:edit",before.World.RecipeSha256,before.World.CatalogSha256,density.Recipe,density.Catalog));
        Check(Json(accepted.World)==Json(moved),"density edit equals clean generation");
        var bad=density.Recipe with{LocalLayouts=density.Recipe.LocalLayouts! with{Layouts=[density.Recipe.LocalLayouts.Layouts[0] with{ProgramIntents=density.Recipe.LocalLayouts.Layouts[0].ProgramIntents!.Select(p=>p with{MinimumArea=10000}).ToArray()}]}};
        try{session.Apply(new("impossible","Preserve all required space","space:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,bad,density.Catalog));throw new Exception("Missing rollback");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"failed area edit preserves accepted world/cache");
        string output=Path.Combine(Path.GetTempPath(),"program-spaces-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(accepted)==Json(restored.Snapshot),"source-history replay");
        Console.WriteLine("PASS: required program intents generate terrain-derived demand-weighted spaces and centroid anchors without coordinates; analytical uniform/unequal/weighted/hole geometry, nested scopes, canonical order, budget/cancel/input/centroid refusal; complete seven-node physical program and route, density response, strategy selection, replay/tamper/edit rollback/checkpoint.");
    }
}
