using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldFootprintAnchorTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Footprint anchor: "+message);}
    private static void Refuse(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool hole=false)
    {
        const string id="footprint-anchor";
        var boundary=new WorldPolygon(hole?[new(0,0),new(12,0),new(12,12),new(0,12)]:[new(0,0),new(12,0),new(12,8),new(8,12),new(0,12)]);
        var shape=new WorldPolygon([new(-2,-1),new(2,-1),new(2,1),new(-2,1)]);
        var profile=new WorldSuitabilityProfile("floor","Declared usable support","test:floor",["floor"],[],[]);
        var catalog=new WorldMetadataCatalog(1,"anchor-catalog",[
            new("world","world","Declared single activity",new Dictionary<string,JsonElement>(),[new("site","site",1,"Required source-sized object")],[],[]),
            new("site","structure","Exact source object",new Dictionary<string,JsonElement>(),[],[],[],
                PhysicalFootprint:new("meters","Exact source rectangle","test:shape",shape),Access:new(MetadataOccupancyKind.Solid,[]))]);
        var surface=new WorldSurfaceDefinition(1,"floor","meters",boundary,hole?[new("hole","Protected void","test:hole",new([new(5,5),new(7,5),new(7,7),new(5,7)]))]:[],
            [new("floor","floor","Supported activity space","test:floor",boundary,0,new(0,0),0,0,0)]);
        var request=new WorldPlacementRequest("required","Preserve exact source shape","test:shape",boundary,[new("site","Required shape","test:shape",null,profile,RegionId:"land",AllocationId:"activity")],[],
            [new("pin","site","target","Use the source-derived anchor","test:anchor",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        return(new(1,id,"framework-reference",catalog.Id,"world","activity",MetadataCoverage.PartialScope,["Analytic geometry fixture; broad world design remains separate."],new Dictionary<string,int>(),new(["x","y"],"meters"),[1,1],true,
            Surface:surface,Environment:new(1,"fields",[new("height","Declared elevation","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]),
            Regions:new("support","Required support","test:support",boundary,profile,[new("land","Complete usable support","test:land",new(1,1),1)]),
            LocalLayouts:new([new("activity","land","Purposeful activity allocation","test:activity",WorldLocalLayoutStrategy.PowerDiagram,
                [new("activity","Required activity","test:activity",hole?new(4,6):new(10,9),1)])],[]),
            Placement:new(request,[new("site",id+"/site-0",true)],AllocationAnchors:[new("target","activity","Fit the source shape near its intended activity","test:fit",Footprint:new("site",new(MinimumClearance:hole?.0001:0)))])),catalog);
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();string root=Path.Combine("generated","footprint-anchor-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        foreach(bool hole in new[]{false,true})
        {
            var e=Example(hole);var world=generator.Generate(e.Recipe,e.Catalog);var proof=world.PlacementPlan!.FootprintAnchorResults!.Single().Plan;
            var expected=hole?new WorldPoint(2.9999,6):new(9,8);
            Check(proof.Position.DistanceTo(expected)<1e-8,"independent analytic closest translation");
            Check(proof.Preferred==(hole?new WorldPoint(4,6):new(10,9))&&proof.Contacts>0&&proof.Candidates>0&&proof.Evaluations>1,"complete feature-search receipt");
            var vertices=world.AccessPlan!.Sites.Single().Footprint.Vertices;
            double twiceArea=vertices.Select((p,i)=>p.X*vertices[(i+1)%vertices.Count].Y-p.Y*vertices[(i+1)%vertices.Count].X).Sum();
            Check(world.Nodes.Count==2&&Math.Abs(twiceArea-16)<1e-8,"source dimensions and count unchanged");
            Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic complete receipt");
            var anchor=e.Recipe.Placement!.AllocationAnchors![0];
            Refuse(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{AllocationAnchors=[anchor with{Footprint=null}]}},e.Catalog),"PLACEMENT_UNRESOLVED");
            Refuse(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{AllocationAnchors=[anchor with{Interior=new(.1)}]}},e.Catalog),"ANCHOR_MODE_CONFLICT");
            foreach(var search in new[]{new WorldFootprintAnchorSearch(MaximumCandidates:1),new(MaximumContacts:1),new(MaximumWork:1)})
                Refuse(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{AllocationAnchors=[anchor with{Footprint=new("site",search)}]}},e.Catalog),"BUDGET");
            var plan=world.PlacementPlan;var saved=plan.FootprintAnchorResults![0];
            Refuse(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=plan with{FootprintAnchorResults=[saved with{Plan=proof with{Position=proof.Position.Offset(1,0)}}]}}),"PLACEMENT_PLAN_MISMATCH");
            string folder=Path.Combine(root,hole?"hole":"slanted");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world","world-metadata.json")))==Json(world),"typed source replay");
        }
        var source=Example();var tooWide=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{PhysicalFootprint=a.PhysicalFootprint! with{
            Polygon=new([new(-20,-20),new(20,-20),new(20,20),new(-20,20)])}}:a).ToArray()};
        Refuse(()=>generator.Generate(source.Recipe,tooWide),"FOOTPRINT_UNSATISFIED");
        var rotated=generator.Generate(source.Recipe with{Placement=source.Recipe.Placement! with{
            Bindings=source.Recipe.Placement!.Bindings.Select(b=>b with{RotationDegrees=90}).ToArray()}},source.Catalog);
        var rotatedProof=rotated.PlacementPlan!.FootprintAnchorResults!.Single().Plan;
        Check(rotatedProof.Position.DistanceTo(new(9,8))<1e-8,"rotated exact source uses the same analytic slanted contact");
        var rotatedVertices=rotated.AccessPlan!.Sites.Single().Footprint.Vertices;
        Check(Math.Abs(rotatedVertices.Max(p=>p.X)-rotatedVertices.Min(p=>p.X)-2)<1e-8&&
            Math.Abs(rotatedVertices.Max(p=>p.Y)-rotatedVertices.Min(p=>p.Y)-4)<1e-8,"rotation preserves exact source dimensions");
        foreach(var offset in new[]{new WorldPoint(-10000000,-10000000),new WorldPoint(9999988,9999988)})
        {
            WorldPolygon Move(WorldPolygon p)=>new(p.Vertices.Select(v=>v.Offset(offset.X,offset.Y)).ToArray());
            var r=source.Recipe;var moved=r with{Origin=[1+offset.X,1+offset.Y],Surface=r.Surface! with{
                Boundary=Move(r.Surface!.Boundary!),Patches=r.Surface.Patches.Select(p=>p with{Boundary=Move(p.Boundary)}).ToArray()},
                Regions=r.Regions! with{Boundary=Move(r.Regions!.Boundary),Seeds=r.Regions.Seeds.Select(s=>s with{Position=s.Position.Offset(offset.X,offset.Y)}).ToArray()},
                LocalLayouts=r.LocalLayouts! with{Layouts=r.LocalLayouts!.Layouts.Select(l=>l with{Programs=l.Programs.Select(p=>p with{Anchor=p.Anchor.Offset(offset.X,offset.Y)}).ToArray()}).ToArray()},
                Placement=r.Placement! with{Request=r.Placement!.Request with{Boundary=Move(r.Placement.Request.Boundary!)}}};
            var translated=generator.Generate(moved,source.Catalog).PlacementPlan!.FootprintAnchorResults!.Single().Plan;
            Check(translated.Position.DistanceTo(new(9+offset.X,8+offset.Y))<1e-7,"translation equivalence at both engine coordinate limits");
        }
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{generator.Generate(source.Recipe,source.Catalog,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var area=new WorldPolygon([new(3,3),new(4,3),new(4,4),new(3,4)]);var floor=source.Recipe.Surface!;
        bool Allowed(WorldSurfaceSample sample)=>sample.Status==WorldSurfaceStatus.Sampled&&sample.Kind=="floor"&&sample.Slope<=.1;
        Check(new WorldSurfaceAreaQuery(new(floor)).Matches(area,Allowed),"constant terrain remains allowed");
        var enclosure=new WorldPolygon([new(2,2),new(5,2),new(5,5),new(2,5)]);
        Check(!new WorldSurfaceAreaQuery(new(floor with{Exclusions=[new("enclosed","Whole query excluded","test:exclude",enclosure)]})).Matches(area,Allowed),
            "an exclusion containing the entire query still rejects without a crossing edge");
        var steep=floor with{Patches=[..floor.Patches,floor.Patches[0] with{Id="steep",Boundary=enclosure,Priority=1,GradientX=.5}]};
        Check(!new WorldSurfaceAreaQuery(new(steep)).Matches(area,Allowed),"constant steep overlay still rejects");
        Check(!new WorldSurfaceAreaQuery(new(steep)).Matches(new([new(1,1),new(6,1),new(6,6),new(1,6)]),Allowed),"crossing an unsuitable overlay retains the sweep");
        Console.WriteLine($"PASS: slanted-boundary closest anchor (9,8), protected-hole anchor (2.9999,6) with declared 0.0001 clearance, unchanged source area/count, deterministic typed replay/tamper, explicit mode conflict, candidate/contact/work refusal, oversized source refusal and cancellation. Packet: {root}");
    }
}
