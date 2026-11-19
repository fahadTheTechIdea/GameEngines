using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldAnchorPartitionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Anchor partition: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-6,message+": "+actual+" / "+expected);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static (WorldEnvironmentQuery Query,WorldRegionPlan Regions) Region(WorldPolygon boundary,WorldPolygon? hole=null,WorldPoint? seed=null)
    {
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("height","Height","anchor:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var surface=new WorldSurfaceDefinition(1,"land","meters",boundary,hole is null?[]:[new("hole","Preserve excluded geometry","anchor:hole",hole)],
            [new("ground","ground","Ground","anchor:ground",boundary,0,new(0,0),0,0,0)]);
        var query=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),fields);
        var profile=new WorldSuitabilityProfile("usable","Required land","anchor:land",["ground"],[],[]);
        var regions=new WorldRegionPlanner().Plan(query,new("land","Required land","anchor:land",boundary,profile,[new("region","Required region","anchor:region",seed??new(.25,.25),1)]));
        return(query,regions);
    }
    private static WorldLocalLayoutDefinition Definition()=>new([
        new("layout","region","Divide required programs without authored cuts","anchor:layout",WorldLocalLayoutStrategy.AnchorBisection,
            [new("a","West program","anchor:a",new(2,5),30),new("b","Lower program","anchor:b",new(8,2),30),new("c","Upper program","anchor:c",new(8,8),30)])],
        [new("west-lower","a","b","Required span","anchor:span",4),new("lower-upper","b","c","Required span","anchor:span",6)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double x=0,double y=0)
    {
        var e=WorldBinaryPartitionTests.Example(x,y);var layout=e.Recipe.LocalLayouts!.Layouts[0];
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[layout with{Strategy=WorldLocalLayoutStrategy.AnchorBisection,Partition=null}]}},e.Catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        var source=WorldLayoutStrategyTests.Example();var preferred=source.Recipe.LocalLayouts!.Layouts[0];
        var alternative=preferred with{Strategy=WorldLocalLayoutStrategy.AnchorBisection,Grid=null,Partition=null,Programs=preferred.Programs.Select(p=>p with{Column=null,Row=null}).ToArray()};
        await File.WriteAllTextAsync(Path.Combine(directory,"alternative-recipe.json"),Json(source.Recipe with{LayoutAdaptation=source.Recipe.LayoutAdaptation! with{
            Alternatives=[new("derive","Derive partitions for required content","anchor:alternative",alternative)]}}));
    }
    public static async Task RunAsync()
    {
        var planner=new WorldLocalLayoutPlanner();var d=Definition();var r=Region(Box(0,0,12,10));var plan=planner.Plan(r.Query,r.Regions,d);
        Check(plan.Definition.Layouts[0].Partition is null&&plan.Definition.Layouts[0].Strategy==WorldLocalLayoutStrategy.AnchorBisection,"canonical source has no authored tree");
        Check(plan.GeneratedPartitions!.Count==1&&plan.GeneratedPartitions[0].Partition.Cuts.Count==2,"engine derives complete tree");
        foreach(var a in plan.Allocations){Near(a.Shape.Area,a.Id=="a"?50:35,"analytical generated area");
            foreach(var p in a.Shape.Cells.SelectMany(c=>c.Vertices))Check(a.Id=="a"?p.X<=5+1e-7:a.Id=="b"?p.X>=5-1e-7&&p.Y<=5+1e-7:p.X>=5-1e-7&&p.Y>=5-1e-7,"analytical half-planes");}
        Near(plan.Interfaces.Single(i=>i.Id=="west-lower").SelectedSpan.From.DistanceTo(plan.Interfaces.Single(i=>i.Id=="west-lower").SelectedSpan.To),5,"west/lower interface");
        Near(plan.Interfaces.Single(i=>i.Id=="lower-upper").SelectedSpan.From.DistanceTo(plan.Interfaces.Single(i=>i.Id=="lower-upper").SelectedSpan.To),7,"lower/upper interface");
        Check(Json(plan)==Json(planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{Programs=d.Layouts[0].Programs.Reverse().ToArray()}]})),"source order independent");
        var h=Region(Box(0,0,12,10),Box(1,1,1,1));var holed=planner.Plan(h.Query,h.Regions,d);Near(holed.Allocations.Single(a=>a.Id=="a").Shape.Area,49,"hole preserved");
        var notch=Region(new([new(0,0),new(12,0),new(12,8),new(10,8),new(10,10),new(0,10)]));var changed=planner.Plan(notch.Query,notch.Regions,d);
        Near(changed.Allocations.Single(a=>a.Id=="c").Shape.Area,31,"concave terrain changes upper allocation");
        var u=Region(new([new(0,0),new(12,0),new(12,10),new(9,10),new(9,3),new(3,3),new(3,10),new(0,10)]));
        var disconnected=d with{Layouts=[d.Layouts[0] with{Programs=[new("a","Lower","anchor:a",new(1,1),1),new("b","Upper","anchor:b",new(1,8),1)]}],Interfaces=[]};
        Reject(()=>planner.Plan(u.Query,u.Regions,disconnected),"WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
        double angle=Math.PI/6;WorldPoint R(WorldPoint p)=>new(-20+Math.Cos(angle)*p.X-Math.Sin(angle)*p.Y,15+Math.Sin(angle)*p.X+Math.Cos(angle)*p.Y);
        var rotatedRegion=Region(new(r.Regions.Request.Boundary.Vertices.Select(R).ToArray()),seed:R(new(.25,.25)));
        var rotated=planner.Plan(rotatedRegion.Query,rotatedRegion.Regions,d with{Layouts=[d.Layouts[0] with{Programs=d.Layouts[0].Programs.Select(p=>p with{Anchor=R(p.Anchor)}).ToArray()}]});
        foreach(var a in plan.Allocations)Near(rotated.Allocations.Single(b=>b.Id==a.Id).Shape.Area,a.Shape.Area,"rotated/translated partition area");
        var single=d with{Layouts=[d.Layouts[0] with{Programs=[d.Layouts[0].Programs[0]]}],Interfaces=[]};var one=planner.Plan(r.Query,r.Regions,single);
        Check(one.GeneratedPartitions![0].Partition.Cuts.Count==0&&one.GeneratedPartitions[0].Partition.RootId=="a","one leaf needs no cut");Near(one.Allocations[0].Shape.Area,120,"single allocation complete");
        var parent=d.Layouts[0] with{Id="parent",Programs=[new("host","Parent program","anchor:host",new(.25,.25),1)]};
        var nested=planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{ParentAllocationId="host"},parent]});
        Check(nested.GeneratedPartitions!.Count==2&&nested.Areas.Single(a=>a.LayoutId=="layout").ParentAllocationId=="host","nested generated tree uses existing parent owner");
        Near(nested.Areas.Single(a=>a.LayoutId=="layout").AllocatedArea,120,"complete parent geometry retained");
        var clash=d.Layouts[0].Programs.Select(p=>p.Id=="a"?p with{Id="generated-cut-00"}:p).ToArray();
        var collision=planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{Programs=clash}],Interfaces=[]});
        Check(collision.GeneratedPartitions![0].Partition.Cuts.All(c=>c.Id!="generated-cut-00"),"generated IDs cannot shadow required program IDs");
        var largeRegion=Region(Box(0,0,32,4));var many=d with{Layouts=[d.Layouts[0] with{Programs=Enumerable.Range(0,32).Select(i=>new WorldLocalProgram("member-"+i.ToString("D2"),"Required member","anchor:member",new(i+.5,2),3)).ToArray()}],Interfaces=[]};
        var full=planner.Plan(largeRegion.Query,largeRegion.Regions,many);Check(full.GeneratedPartitions![0].Partition.Cuts.Count==31&&full.Allocations.Count==32,"declared 32-leaf bound");
        foreach(var a in full.Allocations)Near(a.Shape.Area,4,"maximum-leaf analytical area");
        Reject(()=>planner.Plan(largeRegion.Query,largeRegion.Regions,many with{Layouts=[many.Layouts[0] with{Programs=[..many.Layouts[0].Programs,new("extra","Excess member","anchor:extra",new(.5,1),1)]}]}),"WORLD_LAYOUT_REQUEST_INVALID");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{Partition=new("a",[])}]}),"WORLD_LAYOUT_PARTITION_NOT_SELECTED");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{Programs=d.Layouts[0].Programs.Select(p=>p with{Influence=1}).ToArray()}]}),"WORLD_LAYOUT_PARTITION_INFLUENCE_NOT_SELECTED");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{Layouts=[d.Layouts[0] with{Programs=[d.Layouts[0].Programs[0] with{Anchor=new(2,2)},d.Layouts[0].Programs[1] with{Anchor=new(2+1e-8,2)}]}],Interfaces=[]}),"WORLD_LAYOUT_GENERATED_PARTITION_RESOLUTION");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{MaximumCells=1}),"WORLD_LAYOUT_CELL_BUDGET");
        Check(planner.Plan(r.Query,r.Regions,d with{MaximumWork=plan.Work}).Work==plan.Work,"exact work boundary");
        Reject(()=>planner.Plan(r.Query,r.Regions,d with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(r.Query,r.Regions,d,cancel.Token);throw new Exception("Missing cancel");}catch(OperationCanceledException){}}
        var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2,"complete source-sized content");
        foreach(var site in world.PlacementPlan!.Placement.Sites){Near(Math.Abs(site.Footprint.Footprint.SignedArea()),84,"source footprint");Near(Math.Abs(site.Reservation!.Footprint.SignedArea()),128,"source reservation");}
        WorldMetadataGenerator.ValidateDocument(world);
        var tampered=world with{LocalLayoutPlan=world.LocalLayoutPlan! with{GeneratedPartitions=[]}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_INTERFACE_LAYOUT_PLAN_MISMATCH");
        Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(tampered),"METADATA_LOCAL_LAYOUT_PLAN_MISMATCH");
        var strategyInput=WorldLayoutStrategyTests.Example();var preferred=strategyInput.Recipe.LocalLayouts!.Layouts[0];
        var alternative=preferred with{Strategy=WorldLocalLayoutStrategy.AnchorBisection,Grid=null,Partition=null,Programs=preferred.Programs.Select(p=>p with{Column=null,Row=null}).ToArray()};
        var chosen=generator.Generate(strategyInput.Recipe with{LayoutAdaptation=strategyInput.Recipe.LayoutAdaptation! with{
            Alternatives=[new("derive","Derive partitions for required content","anchor:alternative",alternative)]}},strategyInput.Catalog);
        Check(chosen.LayoutAdaptationPlan!.Attempts.Count==2&&chosen.LayoutAdaptationPlan.Attempts[1].Accepted&&chosen.LocalLayoutPlan!.GeneratedPartitions!.Count==1,"strategy selection generates tree after preferred grid fails");
        var invalid=e.Recipe.LocalLayouts!.Layouts[0] with{Partition=new("unused",[])};
        Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=new(invalid.Id,"Do not hide invalid unselected strategy","anchor:invalid",[],
            Alternatives:[new("invalid","Invalid unused tree input","anchor:invalid",invalid)])},e.Catalog),"WORLD_LAYOUT_PARTITION_NOT_SELECTED");
        var moved=Example(-80,40);var translated=generator.Generate(moved.Recipe,moved.Catalog);Near(translated.LocalLayoutPlan!.Areas[0].AllocatedArea,world.LocalLayoutPlan!.Areas[0].AllocatedArea,"regional frame translation");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var accepted=session.Apply(new("relocate","Move world domain retaining generated local programs","anchor:edit",before.World.RecipeSha256,before.World.CatalogSha256,moved.Recipe,moved.Catalog));
        Check(Json(accepted.World)==Json(translated),"incremental equals clean generation");
        var bad=moved.Recipe with{LocalLayouts=moved.Recipe.LocalLayouts! with{Layouts=[moved.Recipe.LocalLayouts.Layouts[0] with{Programs=moved.Recipe.LocalLayouts.Layouts[0].Programs.Select(p=>p with{MinimumArea=10000}).ToArray()}]}};
        try{session.Apply(new("impossible","Preserve required areas","anchor:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,bad,moved.Catalog));throw new Exception("Missing rollback");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"failed edit preserves accepted world");
        string output=Path.Combine(Path.GetTempPath(),"anchor-partition-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(accepted)==Json(restored.Snapshot),"source-history replay");
        Console.WriteLine("PASS: automatic anchor bisection without authored cuts; analytical three-program areas/spans, canonical order, holes/concavity, rotation/translation, single/nested scopes and ID collision; limits/cancel, complete seven-node physical program, source/tamper replay, edits/rollback/checkpoint.");
    }
}
