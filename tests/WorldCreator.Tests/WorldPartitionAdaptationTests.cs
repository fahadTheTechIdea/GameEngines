using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPartitionAdaptationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Partition adaptation: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-7,message);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool negative=false,bool diagonal=false,bool negativeSteps=false)
    {
        var e=WorldBinaryPartitionTests.Example();var layout=e.Recipe.LocalLayouts!.Layouts[0];var cut=layout.Partition!.Cuts[0];
        cut=negative?cut with{Origin=new(4,0),NormalDegrees=180,NegativeId=cut.PositiveId,PositiveId=cut.NegativeId}:
            cut with{Origin=new(negativeSteps?4:0,0),NormalDegrees=diagonal?10:0};
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[layout with{Partition=layout.Partition with{Cuts=[cut]}}]},
            LayoutAdaptation=new(layout.Id,"Fit required source programs by bounded cut movement","cut-adapt:program",
                [new("shift",MetadataLayoutDialKind.PartitionOffset,1,negativeSteps?-2:0,negativeSteps?0:2,CutId:cut.Id)])},e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var (name,negative,diagonal,negativeSteps) in new[]{("positive",false,false,false),("negative",true,false,false),("diagonal",false,true,false),("negative-steps",false,false,true)})
        {
            string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);var e=Example(negative,diagonal,negativeSteps);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();
        foreach(var (negative,diagonal,negativeSteps) in new[]{(false,false,false),(true,false,false),(false,true,false),(false,false,true)})
        {
            var e=Example(negative,diagonal,negativeSteps);var source=e.Recipe.LocalLayouts!.Layouts[0];
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=null},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
            var world=generator.Generate(e.Recipe,e.Catalog);var proof=world.LayoutAdaptationPlan!;
            Check(proof.Attempts.Count==3&&proof.Attempts.Take(2).All(a=>!a.Accepted)&&proof.Attempts[2].Accepted,"all infeasible settings retained before nearest feasible setting");
            Check(proof.Attempts.Select(a=>a.Steps["shift"]).SequenceEqual(negativeSteps?[0,-1,-2]:[0,1,2]),"bounded deterministic candidate sequence");
            var actual=world.LocalLayoutPlan!.Definition.Layouts[0];var moved=actual.Partition!.Cuts[0];
            Near(moved.Origin.X,negative||negativeSteps?2:diagonal?1.969615506024416:2,"normal displacement X");
            Near(moved.Origin.Y,negative?0:diagonal?.34729635533386066:0,"normal displacement Y");
            Check(Json(source.Programs)==Json(actual.Programs)&&moved.NormalDegrees==source.Partition!.Cuts[0].NormalDegrees&&
                moved.NegativeId==source.Partition.Cuts[0].NegativeId&&moved.PositiveId==source.Partition.Cuts[0].PositiveId,"cut topology, angle and hard program contract unchanged");
            Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2,"all physical and nested content retained");
            foreach(var site in world.PlacementPlan!.Placement.Sites){Near(Math.Abs(site.Footprint.Footprint.SignedArea()),84,"source footprint");Near(Math.Abs(site.Reservation!.Footprint.SignedArea()),128,"source reservation");}
            WorldMetadataGenerator.ValidateDocument(world);
            var noFit=e.Recipe.LayoutAdaptation! with{Dials=[e.Recipe.LayoutAdaptation.Dials[0] with{MinimumSteps=negativeSteps?-1:0,MaximumSteps=negativeSteps?0:1}]};
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=noFit},e.Catalog),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{MaximumAttempts=2}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
        }
        var input=Example();var policy=input.Recipe.LayoutAdaptation!;var dial=policy.Dials[0];
        var baseline=generator.Generate(input.Recipe,input.Catalog);long work=baseline.LayoutAdaptationPlan!.Work;
        var exact=generator.Generate(input.Recipe with{LayoutAdaptation=policy with{MaximumWork=work}},input.Catalog);
        Check(exact.LayoutAdaptationPlan!.Work==work,"exact work boundary accepted");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{MaximumWork=work-1}},input.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        var translated=WorldBinaryPartitionTests.Example(-80,40);var shifted=generator.Generate(translated.Recipe with{LocalLayouts=input.Recipe.LocalLayouts,LayoutAdaptation=policy},translated.Catalog);
        foreach(var a in baseline.LocalLayoutPlan!.Allocations)Near(shifted.LocalLayoutPlan!.Allocations.Single(b=>b.Id==a.Id).Program.Anchor.DistanceTo(a.Program.Anchor.Offset(-80,40)),0,"generated regional frame moves with adapted program");
        Near(shifted.LocalLayoutPlan!.Areas[0].AllocatedArea,baseline.LocalLayoutPlan.Areas[0].AllocatedArea,"relative cut preserves area under world translation");
        void Bad(MetadataLayoutDial d,string code)=>Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{Dials=[d]}},input.Catalog),code);
        Bad(dial with{CutId="absent"},"METADATA_LAYOUT_DIAL_TARGET_INVALID");Bad(dial with{CutId=null},"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        Bad(dial with{ProgramId="west-a"},"METADATA_LAYOUT_DIAL_TARGET_INVALID");Bad(dial with{Step=1e8},"METADATA_LAYOUT_DIAL_RANGE_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{Dials=[dial,dial with{Id="duplicate"}]}},input.Catalog),"METADATA_LAYOUT_DIAL_INVALID");
        var grid=WorldLayoutAdaptationTests.Example();Reject(()=>generator.Generate(grid.Recipe with{LayoutAdaptation=grid.Recipe.LayoutAdaptation! with{Dials=[dial]}},grid.Catalog),"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        Reject(()=>generator.Generate(grid.Recipe with{LayoutAdaptation=grid.Recipe.LayoutAdaptation! with{Dials=[grid.Recipe.LayoutAdaptation.Dials[0] with{CutId="cut"}]}},grid.Catalog),"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{MaximumWork=1}},input.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{generator.Generate(input.Recipe,input.Catalog,c.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;
        var changed=Example(diagonal:true);var accepted=session.Apply(new("diagonal","Change permitted normal while retaining content","cut-adapt:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(Json(accepted.World)==Json(generator.Generate(changed.Recipe,changed.Catalog)),"incremental equals clean generation including witnesses");
        var failed=changed.Recipe with{LayoutAdaptation=changed.Recipe.LayoutAdaptation! with{Dials=[dial with{MaximumSteps=1}]}};
        try{session.Apply(new("no-fit","Do not discard content on infeasible range","cut-adapt:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,failed,changed.Catalog));throw new Exception("Missing rollback refusal");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"infeasible edit preserves accepted world/cache");
        string output=Path.Combine(Path.GetTempPath(),"cut-adaptation-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(accepted)==Json(restored.Snapshot),"source-history checkpoint replay");
        Console.WriteLine("PASS: complete seven-node/two-structure cut adaptation in positive, reversed, signed-negative and oblique directions; exact three-setting history/work boundary, retained topology/anchors/source sizes, regional translation, infeasible/budget/target/range/cancel refusal, clean/edit equality, rollback and checkpoint replay.");
    }
}
