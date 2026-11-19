using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldBinaryPartitionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Binary partition: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-6,message+": "+actual+" / "+expected);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing refusal "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static (WorldEnvironmentQuery Environment,WorldRegionPlan Regions) Region(WorldPolygon boundary,WorldPolygon? hole=null)
    {
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("height","Height","partition:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var surface=new WorldSurfaceDefinition(1,"land","meters",boundary,hole is null?[]:[new("hole","Protected hole","partition:hole",hole)],
            [new("ground","ground","Ground","partition:ground",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),fields);
        var profile=new WorldSuitabilityProfile("usable","Required land","partition:land",["ground"],[],[]);
        var regions=new WorldRegionPlanner().Plan(environment,new("land","Required land","partition:land",boundary,profile,[new("region","Complete region","partition:region",new(.5,.5),1)]));
        return(environment,regions);
    }
    private static WorldLocalLayoutDefinition Definition()=>new([
        new("layout","region","Uneven recursive spaces","partition:layout",WorldLocalLayoutStrategy.BinaryPartition,
            [new("a","West activity","partition:a",new(2,5),30),new("b","Lower activity","partition:b",new(8,2),30),new("c","Upper activity","partition:c",new(8,8),30)],
            Partition:new("west-cut",[new("west-cut","Divide west from east","partition:west",new(4,0),0,"a","diagonal"),
                new("diagonal","Divide east diagonally","partition:east",new(8,5),45,"b","c")]))],
        [new("west-lower","a","b","Required continuous boundary","partition:interface",8)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double x=0,double y=0)
    {
        var e=WorldRegionalFrameTests.Example(x,y,power:true);var owner=e.Recipe.LocalLayouts!.Layouts[0];
        var local=owner with{Strategy=WorldLocalLayoutStrategy.BinaryPartition,Partition=new("cut",[
            new("cut","Separate complete source programs","partition:content",new(2,0),0,owner.Programs[0].Id,owner.Programs[1].Id)])};
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[local]}},e.Catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        var source=WorldLayoutStrategyTests.Example();var owner=source.Recipe.LocalLayouts!.Layouts[0];
        var alternative=owner with{Strategy=WorldLocalLayoutStrategy.BinaryPartition,Grid=null,Partition=e.Recipe.LocalLayouts!.Layouts[0].Partition,
            Programs=owner.Programs.Select(p=>p with{Column=null,Row=null}).ToArray()};
        await File.WriteAllTextAsync(Path.Combine(directory,"alternative-recipe.json"),Json(source.Recipe with{LayoutAdaptation=source.Recipe.LayoutAdaptation! with{
            Alternatives=[new("recursive","Fit complete source content with cuts","partition:alternative",alternative)]}}));
    }
    public static async Task RunAsync()
    {
        var planner=new WorldLocalLayoutPlanner();var d=Definition();var (environment,regions)=Region(Box(0,0,12,10));
        var plan=planner.Plan(environment,regions,d);
        foreach(var a in plan.Allocations)Near(a.Shape.Area,40,"analytical partition area");
        Near(plan.Areas[0].ResidualArea,0,"complete partition");Near(plan.Interfaces[0].SelectedSpan.From.DistanceTo(plan.Interfaces[0].SelectedSpan.To),9,"continuous west/lower span");
        // Independent half-plane oracle: x=4 and x+y=13, not a replay call to the planner.
        foreach(var a in plan.Allocations)foreach(var p in a.Shape.Cells.SelectMany(c=>c.Vertices))
            Check(a.Id=="a"?p.X<=4+1e-8:a.Id=="b"?p.X>=4-1e-8&&p.X+p.Y<=13+1e-8:p.X>=4-1e-8&&p.X+p.Y>=13-1e-8,"every vertex respects analytical planes");
        var h=Region(Box(0,0,12,10),Box(1,1,1,1));var holed=planner.Plan(h.Environment,h.Regions,d);
        Near(holed.Allocations.Single(a=>a.Id=="a").Shape.Area,39,"protected hole retained");Near(holed.Areas[0].AllocatedArea,119,"hole excluded from accounting");
        var notch=new WorldPolygon([new(0,0),new(12,0),new(12,8),new(10,8),new(10,10),new(0,10)]);var n=Region(notch);
        var notched=planner.Plan(n.Environment,n.Regions,d);Near(notched.Allocations.Single(a=>a.Id=="c").Shape.Area,36,"terrain notch changes only upper allocation");
        var u=Region(new([new(0,0),new(12,0),new(12,10),new(9,10),new(9,3),new(3,3),new(3,10),new(0,10)]));
        var separated=d with{Layouts=[d.Layouts[0] with{Programs=[new("a","Lower","partition:a",new(1,1),1),new("b","Upper","partition:b",new(1,8),1)],
            Partition=new("horizontal",[new("horizontal","Divide a concave region","partition:cut",new(0,5),90,"a","b")])}],Interfaces=[]};
        Reject(()=>planner.Plan(u.Environment,u.Regions,separated),"WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
        Check(Json(plan)==Json(planner.Plan(environment,regions,d with{Layouts=[d.Layouts[0] with{Programs=d.Layouts[0].Programs.Reverse().ToArray(),Partition=d.Layouts[0].Partition! with{Cuts=d.Layouts[0].Partition!.Cuts.Reverse().ToArray()}}]})),"declaration order canonical");
        var layout=d.Layouts[0];var partition=layout.Partition!;
        void Bad(WorldLocalPartition p,string code)=>Reject(()=>planner.Plan(environment,regions,d with{Layouts=[layout with{Partition=p}]}),code);
        Bad(partition with{Cuts=[partition.Cuts[0] with{PositiveId="west-cut"},partition.Cuts[1]]},"WORLD_LAYOUT_PARTITION_TREE_INVALID");
        Bad(partition with{Cuts=[partition.Cuts[0],partition.Cuts[1] with{NegativeId="a"}]},"WORLD_LAYOUT_PARTITION_TREE_INVALID");
        Bad(partition with{Cuts=[partition.Cuts[0],partition.Cuts[1] with{PositiveId="absent"}]},"WORLD_LAYOUT_PARTITION_REFERENCE_INVALID");
        Bad(partition with{RootId="a"},"WORLD_LAYOUT_PARTITION_TREE_INVALID");
        Bad(partition with{Cuts=[partition.Cuts[0] with{NormalDegrees=361},partition.Cuts[1]]},"WORLD_LAYOUT_PARTITION_CUT_INVALID");
        Reject(()=>planner.Plan(environment,regions,d with{Layouts=[layout with{Programs=layout.Programs.Select(p=>p.Id=="a"?p with{MinimumArea=41}:p).ToArray()}]}),"WORLD_REGION_MINIMUM_AREA");
        Reject(()=>planner.Plan(environment,regions,d with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(environment,regions,d with{MaximumCells=1}),"WORLD_LAYOUT_CELL_BUDGET");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(environment,regions,d,cancel.Token);throw new Exception("Ignored cancel");}catch(OperationCanceledException){}}
        var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2,"complete nested physical content retained");
        foreach(var site in world.PlacementPlan!.Placement.Sites){Near(Math.Abs(site.Footprint.Footprint.SignedArea()),84,"source size");Near(Math.Abs(site.Reservation!.Footprint.SignedArea()),128,"source reservation");}
        WorldMetadataGenerator.ValidateDocument(world);
        var strategyInput=WorldLayoutStrategyTests.Example();var preferred=strategyInput.Recipe.LocalLayouts!.Layouts[0];
        var alternative=preferred with{Strategy=WorldLocalLayoutStrategy.BinaryPartition,Grid=null,Partition=e.Recipe.LocalLayouts!.Layouts[0].Partition,
            Programs=preferred.Programs.Select(p=>p with{Column=null,Row=null}).ToArray()};
        var selected=generator.Generate(strategyInput.Recipe with{LayoutAdaptation=strategyInput.Recipe.LayoutAdaptation! with{
            Alternatives=[new("recursive","Fit complete source content with cuts","partition:alternative",alternative)]}},strategyInput.Catalog);
        Check(selected.LayoutAdaptationPlan!.Attempts.Count==2&&selected.LayoutAdaptationPlan.Attempts[1].Accepted&&selected.Nodes.Count==7,"failed preferred grammar advances to recursive partition preserving hard content");
        var invalidAlternative=alternative with{Partition=alternative.Partition! with{RootId="absent"}};
        Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=new(alternative.Id,"Validate unused grammar","partition:invalid",[],
            Alternatives:[new("invalid","Malformed unselected tree","partition:invalid",invalidAlternative)])},e.Catalog),"WORLD_LAYOUT_PARTITION_REFERENCE_INVALID");
        var translated=Example(-80,40);var moved=generator.Generate(translated.Recipe,translated.Catalog);
        Near(moved.LocalLayoutPlan!.Areas[0].AllocatedArea,world.LocalLayoutPlan!.Areas[0].AllocatedArea,"relative cuts translate with generated region");
        foreach(var a in world.LocalLayoutPlan.Allocations)Near(moved.LocalLayoutPlan.Allocations.Single(b=>b.Id==a.Id).Program.Anchor.DistanceTo(a.Program.Anchor.Offset(-80,40)),0,"region frame anchor translation");
        var source=world.LocalLayoutPlan.Definition.Layouts[0];var changed=e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts! with{Layouts=[source with{Partition=source.Partition! with{Cuts=[source.Partition.Cuts[0] with{Origin=new(1.5,0)}]}}]}};
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var accepted=session.Apply(new("cut-edit","Change recursive division","partition:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(Json(accepted.World)==Json(generator.Generate(changed,e.Catalog)),"cut edit equals clean generation");
        var bad=changed with{LocalLayouts=changed.LocalLayouts! with{Layouts=[changed.LocalLayouts.Layouts[0] with{Partition=source.Partition! with{Cuts=[source.Partition.Cuts[0] with{Origin=new(-7,0)}]}}]}};
        try{session.Apply(new("bad-cut","Keep required content","partition:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,bad,e.Catalog));throw new Exception("Missing physical refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted),"failed geometry edit preserves accepted world");
        string output=Path.Combine(Path.GetTempPath(),"binary-partition-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"checkpoint source replay");
        Console.WriteLine("PASS: recursive diagonal partitions with analytical areas/interfaces, holes/notches, canonical source, tree validation, budgets/cancel, complete seven-node physical content, regional-frame translation, source-edit equality/rollback and checkpoint replay.");
    }
}
