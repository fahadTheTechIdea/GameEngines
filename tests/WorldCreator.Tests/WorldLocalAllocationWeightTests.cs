using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldProgramSpaceTests
{
    internal static async Task WeightsAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new WorldLocalLayoutPlanner();var region=Region();var original=Definition();var source=original.Layouts[0];
        var layout=source with{ProgramIntents=[source.ProgramIntents![0] with{AllocationWeight=1},source.ProgramIntents[1] with{AllocationWeight=3}]};
        var request=original with{Layouts=[layout]};var plan=planner.Plan(region.Query,region.Regions,request);
        Near(plan.Allocations.Single(a=>a.Id=="a").Shape.Area,10,"first area follows authored weight");
        Near(plan.Allocations.Single(a=>a.Id=="b").Shape.Area,30,"second area follows authored weight");
        Check(plan.Interfaces.Count==1&&plan.Interfaces[0].SelectedSpan.From.DistanceTo(plan.Interfaces[0].SelectedSpan.To)>=1,"required interface remains");
        var mixed=layout with{ProgramIntents=[layout.ProgramIntents![0] with{AllocationWeight=5},layout.ProgramIntents[1] with{AllocationWeight=null}]};
        var mixedPlan=planner.Plan(region.Query,region.Regions,original with{Layouts=[mixed]});
        foreach(var allocation in mixedPlan.Allocations)Near(allocation.Shape.Area,20,"omitted weight resolves to minimum area");
        var density=planner.Plan(region.Query,region.Regions,original with{Layouts=[layout with{AreaPattern=new("density")}]});
        var area=density.GeneratedAreas!.Single().AreaPlan;
        Near(area.Strata[0].Mass,area.Mass/4,"first weighted density mass");Near(area.Strata[1].Mass,area.Mass*3/4,"second weighted density mass");
        var parent=source with{Id="parent",ProgramIntents=[new("host","Parent space","space:parent",1)]};
        var nested=planner.Plan(region.Query,region.Regions,original with{Layouts=[layout with{ParentAllocationId="host"},parent]});
        Near(nested.Allocations.Single(a=>a.Id=="a").Shape.Area,10,"weighted nested first allocation");
        Near(nested.Allocations.Single(a=>a.Id=="b").Shape.Area,30,"weighted nested second allocation");
        foreach(double value in new[]{0d,-1d,double.NaN,double.PositiveInfinity,4e14+1})
            Reject(()=>planner.Plan(region.Query,region.Regions,original with{Layouts=[layout with{ProgramIntents=[layout.ProgramIntents![0] with{AllocationWeight=value},layout.ProgramIntents[1]]}]}),"WORLD_LAYOUT_PROGRAM_INTENT_INVALID");
        Reject(()=>planner.Plan(region.Query,region.Regions,original with{Layouts=[layout with{ProgramIntents=[layout.ProgramIntents![0] with{AllocationWeight=.0001},layout.ProgramIntents[1]]}]}),"WORLD_REGION_MINIMUM_AREA:");
        await File.WriteAllTextAsync(Path.Combine(output,"layout-plan.json"),Json(plan));
        var example=Example();var originalLayout=example.Recipe.LocalLayouts!.Layouts[0];
        WorldMetadataRecipe Physical(double first,double second)=>example.Recipe with{LocalLayouts=example.Recipe.LocalLayouts with{Layouts=[originalLayout with{
            ProgramIntents=originalLayout.ProgramIntents!.Select((intent,i)=>intent with{AllocationWeight=i==0?first:second}).ToArray()}]}};
        var generator=new WorldMetadataGenerator();
        foreach(var (first,second) in new[]{(2d,3d),(3d,2d)})
            Reject(()=>generator.Generate(Physical(first,second),example.Catalog),"METADATA_PLACEMENT_UNRESOLVED:");
        var recipe=Physical(11,10);var world=generator.Generate(recipe,example.Catalog);
        Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2&&world.RoutePlan!.Routes.Count==1,"all nested physical content and required route retained");
        Near(world.LocalLayoutPlan!.Allocations[0].Shape.Area,800d*11/21,"physical first area");Near(world.LocalLayoutPlan.Allocations[1].Shape.Area,800d*10/21,"physical second area");
        await generator.PublishAsync(recipe,example.Catalog,world,Path.Combine(output,"world"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"full source replay");
        var session=new MetadataGenerationSession(generator,example.Recipe,example.Catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("weights","Change relative local program areas","space:weights",before.World.RecipeSha256,before.World.CatalogSha256,recipe,example.Catalog),Path.Combine(output,"edited"));
        Check(Json(after.World)==Json(world),"weighted edit equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(after),"weighted edit checkpoint replay");
        try{session.Apply(new("blocked","Keep required access","space:blocked",after.World.RecipeSha256,after.World.CatalogSha256,Physical(2,3),example.Catalog));throw new Exception("Blocked access accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed access edit preserves world");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(example.Catalog));
        Console.WriteLine("PASS: local weighted program areas, density, shared interface, nested scopes, hard minima and full physical source replay.");
    }
}
