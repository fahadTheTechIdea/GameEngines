using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldLayoutStrategyTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Layout strategies: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException ex)when(ex.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool reverse=false)
    {
        var e=WorldRegionalFrameTests.Example(power:reverse);var source=e.Recipe.LocalLayouts!.Layouts[0];
        if(reverse)source=source with{Programs=source.Programs.Select((p,i)=>p with{Influence=i==0?-72:0}).ToArray()};
        var alternative=reverse?source with{Strategy=WorldLocalLayoutStrategy.OrientedGrid,Grid=new(new(-20,-10),0,22,30),
                Programs=source.Programs.Select((p,i)=>p with{Influence=0,Column=i,Row=0}).ToArray()}:
            source with{Strategy=WorldLocalLayoutStrategy.PowerDiagram,Grid=null,Programs=source.Programs.Select(p=>p with{Column=null,Row=null}).ToArray()};
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[source]},
            LayoutAdaptation=new(source.Id,"Choose a compatible local grammar for the full source program","strategy:fit",[],
                Alternatives:[new(reverse?"local-grid":"weighted","Declared alternate spatial grammar","strategy:alternative",alternative)])},e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(bool reverse in new[]{false,true})
        {
            var e=Example(reverse);string folder=Path.Combine(directory,reverse?"weighted-to-grid":"grid-to-weighted");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();
        foreach(bool reverse in new[]{false,true})
        {
            var e=Example(reverse);Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=null},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
            var world=generator.Generate(e.Recipe,e.Catalog);var receipt=world.LayoutAdaptationPlan!;
            Check(world.Nodes.Count==7&&world.PlacementPlan!.Placement.Sites.Count==2&&world.RoutePlan!.Routes.Count==1,"complete unchanged physical program");
            Check(receipt.Attempts.Count==2&&!receipt.Attempts[0].Accepted&&receipt.Attempts[0].AlternativeId is null&&receipt.Attempts[1].Accepted&&
                receipt.Attempts[1].AlternativeId==e.Recipe.LayoutAdaptation!.Alternatives![0].Id&&receipt.Attempts.All(a=>a.Steps.Count==0),"baseline-first ordered strategy search");
            var allocation=world.LocalLayoutPlan!.Allocations.Single(a=>a.Id=="west-a");Check(Math.Abs(allocation.Shape.Area-440)<1e-6,"independent area oracle");
            Check(world.LocalLayoutPlan.Interfaces.Single().Segments.All(s=>Math.Abs(s.From.X-22)<1e-6&&Math.Abs(s.To.X-22)<1e-6),"independent interface x=22");
            foreach(var site in world.PlacementPlan!.Placement.Sites){Check(Math.Abs(site.Footprint!.Footprint.SignedArea()-84)<1e-6,"source footprint unchanged");Check(Math.Abs(site.Reservation!.Footprint.SignedArea()-128)<1e-6,"source reservation unchanged");}
            foreach(var source in e.Recipe.LocalLayouts!.Layouts[0].Programs)
                Check(world.LocalLayoutPlan.Allocations.Single(a=>a.Id==source.Id).Program.Anchor==source.Anchor.Offset(20,10),"declared program anchor unchanged");
            WorldMetadataGenerator.ValidateDocument(world);
            var policy=e.Recipe.LayoutAdaptation!;
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=policy with{MaximumAttempts=1}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=policy with{MaximumWork=receipt.Work-1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
            var exact=generator.Generate(e.Recipe with{LayoutAdaptation=policy with{MaximumWork=receipt.Work,MaximumAttempts=2}},e.Catalog);
            Check(Json(exact.LocalLayoutPlan)==Json(world.LocalLayoutPlan),"exact original work and attempts accepted");
            Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(world with{LayoutAdaptationPlan=receipt with{Attempts=receipt.Attempts.Select((a,i)=>i==1?a with{AlternativeId="missing"}:a).ToArray()}}),"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
            var changed=policy.Alternatives![0];
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=policy with{Alternatives=[changed with{Layout=changed.Layout with{Programs=changed.Layout.Programs.Select(p=>p with{MinimumArea=p.MinimumArea-1}).ToArray()}}]}},e.Catalog),"METADATA_LAYOUT_ALTERNATIVE_INVALID");
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=policy with{Dials=[new("width",MetadataLayoutDialKind.GridWidth,1,0,1)]}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_INVALID");
        }
        var input=Example();var owner=input.Recipe.LocalLayouts!.Layouts[0];var policySource=input.Recipe.LayoutAdaptation!;
        var stillFails=owner with{Grid=owner.Grid! with{CellWidth=19}};
        var ordered=generator.Generate(input.Recipe with{LayoutAdaptation=policySource with{Alternatives=[new("narrow-grid","Explicit unsuccessful grammar","strategy:narrow",stillFails),..policySource.Alternatives!]}},input.Catalog);
        Check(ordered.LayoutAdaptationPlan!.Attempts.Count==3&&ordered.LayoutAdaptationPlan.Attempts[1].AlternativeId=="narrow-grid"&&ordered.LayoutAdaptationPlan.Attempts[2].AlternativeId=="weighted","all rejected strategies retained in declaration order");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policySource with{Alternatives=[new("narrow-grid","No fit","strategy:narrow",stillFails)]}},input.Catalog),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
        var successful=WorldRegionalFrameTests.Example(power:true);var good=successful.Recipe.LocalLayouts!.Layouts[0];
        var malformed=good with{Strategy=WorldLocalLayoutStrategy.OrientedGrid,Grid=new(new(-20,-10),0,0,30),Programs=good.Programs.Select((p,i)=>p with{Column=i,Row=0}).ToArray()};
        Reject(()=>generator.Generate(successful.Recipe with{LayoutAdaptation=new(good.Id,"Do not hide invalid unused option","strategy:invalid",[],Alternatives:[new("bad-grid","Invalid unselected option","strategy:bad",malformed)])},successful.Catalog),"WORLD_LAYOUT_GRID_INVALID");
        var regional=WorldRegionFeedbackTests.Example(protectedLayout:true);var original=regional.Recipe.LocalLayouts!.Layouts[0];
        var relative=original with{RegionFrame=new("Same content follows each region choice","strategy:region"),
            ResidualPurpose="Regional terrain outside the local program",Programs=[original.Programs[0] with{Anchor=new(0,0)}]};
        var gridOption=relative with{Strategy=WorldLocalLayoutStrategy.OrientedGrid,Grid=new(new(-60,-10),0,120,20),Programs=[relative.Programs[0] with{Column=0,Row=0}]};
        var regionInput=regional.Recipe with{LocalLayouts=regional.Recipe.LocalLayouts with{Layouts=[relative]},
            Placement=regional.Recipe.Placement! with{RegionAnchors=null,AllocationAnchors=[new("arrival","program-area","Required arrival follows allocation","strategy:arrival")]},
            LayoutAdaptation=new(relative.Id,"Exhaust local grammars before advancing region","strategy:region",[],Alternatives:[new("local-grid","Alternative local allocation","strategy:grid",gridOption)])};
        var chosenRegion=generator.Generate(regionInput,regional.Catalog);
        Check(chosenRegion.Nodes.Count==17&&chosenRegion.RoutePlan!.Routes.Count==11&&chosenRegion.ProgramCompositionPlan!.Attempts.Count==2&&
            chosenRegion.ProgramCompositionPlan.Attempts[0].Rejection!.StartsWith("METADATA_LAYOUT_ADAPTATION_UNRESOLVED",StringComparison.Ordinal)&&
            chosenRegion.LayoutAdaptationPlan!.Attempts.Count==1&&chosenRegion.LocalLayoutPlan!.Allocations.Single().Program.Anchor==new WorldPoint(120,0),"all local strategies exhausted before region advances; viable next region keeps preferred strategy");
        WorldConnectedGrowthTests.Independent(chosenRegion,regional.Catalog,false);WorldMetadataGenerator.ValidateDocument(chosenRegion);
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(input.Recipe,input.Catalog,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;
        try{session.Apply(new("bad-strategy","Refuse invalid strategy source without replacing accepted world","strategy:rollback",before.World.RecipeSha256,before.World.CatalogSha256,
            input.Recipe with{LayoutAdaptation=policySource with{Alternatives=[policySource.Alternatives![0] with{Layout=owner with{Purpose="Changed protected requirement"}}]}},input.Catalog));throw new Exception("Missing edit refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_LAYOUT_ALTERNATIVE_INVALID"){}
        Check(Json(before)==Json(session.Snapshot),"invalid strategy edit rolls back");
        var reverseInput=Example(true);var changedSnapshot=session.Apply(new("alternate-source","Change authored grammar preferences","strategy:edit",before.World.RecipeSha256,before.World.CatalogSha256,reverseInput.Recipe,reverseInput.Catalog));
        Check(Json(changedSnapshot.World)==Json(generator.Generate(reverseInput.Recipe,reverseInput.Catalog)),"source edit equals clean realization");
        string directory=Path.Combine(Path.GetTempPath(),"layout-strategies-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(directory);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(directory,"generation-session.json"));Check(Json(changedSnapshot)==Json(restored.Snapshot),"source strategy and rejected history checkpoint replay");
        Console.WriteLine("PASS: complete seven-node physical programs choose grid/weighted alternatives in both directions; complete 17-node/11-route world exhausts local strategies before region reselection; independent area/interface/source-size/anchor oracles, ordered failures/exhaustion, malformed unused option and contract-change refusal, exact budgets, cancellation, tamper and edit/rollback/checkpoint replay.");
    }
}
