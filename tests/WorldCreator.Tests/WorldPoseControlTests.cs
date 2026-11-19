using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPoseControlTests
{
    private const string Root="connected-wilds";
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Pose controls: "+message);}
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected "+code);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldConnectedGrowthTests.Example();
        return(e.Recipe with{Placement=e.Recipe.Placement! with{PoseControls=[
            new("outer-base",Root+"/bases-4","Keep the outer expedition base at the authored location","developer:outer-base",new(30,25),RotationDegrees:20),
            new("shelter-facing",Root+"/bases-0/hut-0","Turn the complete shelter towards the work area","developer:shelter",new(-2,0),MetadataPlacementPoseFrame.Stage,90),
            new("work-area",Root+"/bases-0/yard-0","Retain the work area beside the shelter","developer:work-area",new(2.5,0),MetadataPlacementPoseFrame.Stage)
        ]}},e.Catalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve pose evidence");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var e=Example();var controls=e.Recipe.Placement!.PoseControls!;
        var world=generator.Generate(e.Recipe,e.Catalog);CheckWorld(world,e.Catalog);
        WorldMetadataGenerator.ValidateDocument(world);
        Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"exact deterministic full replay");
        var tampered=world with{PlacementPlan=world.PlacementPlan! with{PoseResults=world.PlacementPlan!.PoseResults!.Select((p,i)=>i==0?p with{WorldPosition=p.WorldPosition.Offset(.1,0)}:p).ToArray()}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
        bool missingRejected=false;try{JsonSerializer.Deserialize<MetadataPlacementPoseControl>("{\"Id\":\"missing\",\"NodeId\":\"member\",\"Purpose\":\"Position is required\",\"SourceRuleId\":\"test:required\"}",WorldJson.CreateOptions());}catch(JsonException){missingRejected=true;}
        Check(missingRejected,"missing JSON position cannot silently become the origin");
        var reordered=generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{PoseControls=controls.Reverse().ToArray()}},e.Catalog);
        Check(Json(world.Nodes)==Json(reordered.Nodes)&&Json(world.PlacementPlan!.PoseResults)==Json(reordered.PlacementPlan!.PoseResults),"control order does not change geometry or resolved poses");
        await Write(world,e.Recipe,e.Catalog,output,"controlled");
        var measured=new MetadataPlacementPlanner().RegionSources(e.Recipe.Placement,world.Nodes,"meters",
            e.Catalog.Archetypes.Where(a=>a.PhysicalFootprint is not null).ToDictionary(a=>a.Id,a=>a.PhysicalFootprint!),
            new Dictionary<string,MetadataStructureDefinition>(),null,new(20000000,default),default,unplaced:true);
        var rootSource=measured.Single(s=>s.Source.NodeId==Root+"/bases-4").Source;
        var rootBody=world.AccessPlan!.Sites.Single(s=>s.NodeId==Root+"/bases-4").Footprint;
        Check(rootSource.Polygon.Vertices.Zip(rootBody.Vertices).All(p=>p.First.Offset(30,25).DistanceTo(p.Second)<1e-7),"regional capacity resolves the same per-object orientation as placement");
        await FrameControls(generator,output);
        var baseExample=WorldConnectedGrowthTests.Example();var session=new MetadataGenerationSession(generator,baseExample.Recipe,baseExample.Catalog);
        var baseline=session.Snapshot;
        Check(!Json(baseline.World.PlacementPlan).Contains("PoseResults",StringComparison.Ordinal),"unused optional controls do not create a no-op receipt");
        var edited=session.Apply(new("author-poses","Choose exact object placements","developer:edit",baseline.World.RecipeSha256,baseline.World.CatalogSha256,e.Recipe,e.Catalog));
        Check(Json(edited.World)==Json(world),"session edit equals clean generation");
        var larger=WorldConnectedGrowthTests.Example(hutWidth:5);
        var grown=session.Apply(new("larger-shelter","Change source dimensions while retaining selected poses","developer:source",edited.World.RecipeSha256,edited.World.CatalogSha256,e.Recipe,larger.Catalog));
        CheckWorld(grown.World,larger.Catalog);Check(Json(grown.World)==Json(generator.Generate(e.Recipe,larger.Catalog)),"source edit equals clean generation");
        var adjusted=grown.World.PlacementPlan!.NestedStages.Single(s=>s.Id=="base-4").Placement;
        var yard=adjusted.Sites.Single(s=>s.DemandId=="yard");var hut=adjusted.Sites.Single(s=>s.DemandId=="hut");
        Check(yard.CandidateSource.Contains(":boundary-span-",StringComparison.Ordinal),"larger complete source uses boundary-derived contact provenance");
        var yardEnvelope=yard.Reservation!.Footprint;var hutEnvelope=hut.Reservation!.Footprint;
        Check(Math.Abs(yardEnvelope.Vertices.Min(p=>p.X)-hutEnvelope.Vertices.Max(p=>p.X)-adjusted.Request.Growth!.Gap)<1e-7,"normal supporting-plane gap remains exact");
        Check(Math.Min(yardEnvelope.Vertices.Max(p=>p.Y),hutEnvelope.Vertices.Max(p=>p.Y))>Math.Max(yardEnvelope.Vertices.Min(p=>p.Y),hutEnvelope.Vertices.Min(p=>p.Y)),"shifted contact keeps a positive source-face span");
        Check(grown.World.AccessPlan!.Sites.Single(s=>s.NodeId==Root+"/bases-4").Footprint.ContainsPolygon(yardEnvelope),"whole reserved source fits the actually rotated parent");
        await Write(grown.World,e.Recipe,larger.Catalog,output,"larger-source");
        var protectedState=Json(session.Snapshot);var badSurface=e.Recipe.Surface!;
        var water=new WorldPolygon([new(23,18),new(37,18),new(37,32),new(23,32)]);
        var blocked=e.Recipe with{Surface=badSurface with{Patches=badSurface.Patches.Append(new WorldTerrainPatch("blocked","water","Block the authored outer base","developer:terrain",water,1,new(0,0),0,0,0)).ToArray()}};
        Reject(()=>session.Apply(new("blocked-terrain","Respect terrain at selected pose","developer:terrain",grown.World.RecipeSha256,grown.World.CatalogSha256,blocked,larger.Catalog)),"METADATA_PLACEMENT_UNRESOLVED");
        Check(Json(session.Snapshot)==protectedState,"impossible terrain preserves accepted session, history and inputs");
        var rootConflict=e.Recipe with{Placement=e.Recipe.Placement with{PoseControls=controls.Append(new("root-move",Root+"/bases-0","Conflicting root intent","developer:root",new(10,10))).ToArray()}};
        Reject(()=>generator.Generate(rootConflict,e.Catalog),"METADATA_PLACEMENT_POSE_ROOT_CONFLICT");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{PoseControls=[controls[0],controls[0] with{Id="duplicate"}]}},e.Catalog),"POSE_CONTROL_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{PoseControls=[controls[0] with{NodeId="missing"}]}},e.Catalog),"POSE_TARGET_MISSING");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{PoseControls=[]}},e.Catalog),"POSE_CONTROL_LIMIT");
        var original=generator.Generate(baseExample.Recipe,baseExample.Catalog);
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{SourceWorld=original}},e.Catalog),"POSE_SOURCE_CONFLICT");
        var cancelBefore=Json(session.Snapshot);using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{session.Apply(new("cancelled","Cancel authored pose edit","developer:cancel",grown.World.RecipeSha256,grown.World.CatalogSha256,e.Recipe,e.Catalog),cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
        }
        Check(Json(session.Snapshot)==cancelBefore,"cancellation preserves accepted state");
        await session.PublishAsync(Path.Combine(output,"checkpoint"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"checkpoint","generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"saved source/edit history exactly replays selected poses");
        var unlock=e.Recipe with{Placement=e.Recipe.Placement with{PoseControls=null}};
        var released=session.Apply(new("release-poses","Resume procedural choice explicitly","developer:unlock",grown.World.RecipeSha256,grown.World.CatalogSha256,unlock,larger.Catalog));
        Check(Json(released.World)==Json(generator.Generate(unlock,larger.Catalog))&&released.World.PlacementPlan!.PoseResults is null,"explicit removal restores ordinary procedural generation");
        Console.WriteLine("PASS: complete controlled physical program, rigid source orientation, ordered replay, source edit, terrain/root/source conflicts, duplicates/missing controls, cancellation, exact checkpoint and explicit removal.");
    }
    private static async Task Write(WorldMetadataDocument world,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,string root,string name)
    {
        string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(catalog));
        await File.WriteAllTextAsync(Path.Combine(folder,"world-metadata.json"),Json(world));
    }
    private static void CheckWorld(WorldMetadataDocument world,WorldMetadataCatalog catalog)
    {
        Check(world.Nodes.Count==21&&world.AccessPlan!.Sites.Count==20&&world.RoutePlan!.Routes.Count==15,"every original compound, constituent and required path retained");
        var poses=world.PlacementPlan!.PoseResults!;Check(poses.Count==3,"exact coverage of authored controls");
        foreach(var pose in poses)
        {
            var node=world.Nodes.Single(n=>n.Id==pose.Definition.NodeId);Check(node.Position!.SequenceEqual(new[]{pose.WorldPosition.X,pose.WorldPosition.Y}),"resolved pose equals actual node");
            var source=catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId).PhysicalFootprint!.Polygon;
            var actual=world.AccessPlan!.Sites.Single(s=>s.NodeId==node.Id).Footprint;double angle=pose.RotationDegrees*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
            Check(actual.Vertices.Count==source.Vertices.Count&&actual.Vertices.Zip(source.Vertices).All(v=>v.First.DistanceTo(new(v.Second.X*c-v.Second.Y*s+pose.WorldPosition.X,v.Second.X*s+v.Second.Y*c+pose.WorldPosition.Y))<1e-7),"rigid source vertices retained without scaling/clipping");
        }
        Check(poses.Single(p=>p.Definition.Id=="outer-base").WorldPosition==new WorldPoint(30,25),"authored world position retained");
        var root=world.Nodes.Single(n=>n.Id==Root+"/bases-0").Position!;
        Check(poses.Single(p=>p.Definition.Id=="shelter-facing").WorldPosition==new WorldPoint(root[0]-2,root[1]),"stage position follows actual parent translation");
        Check(poses.Single(p=>p.Definition.Id=="shelter-facing").RotationDegrees==90,"individual orientation overrides only that member's default");
    }

    private static async Task FrameControls(WorldMetadataGenerator generator,string output)
    {
        var boundary=new WorldPolygon([new(-20,-20),new(30,-20),new(30,20),new(-20,20)]);
        var container=new WorldPolygon([new(-3,-3),new(3,-3),new(3,3),new(-3,3)]);
        var equipment=new WorldPolygon([new(-.4,-.25),new(.4,-.25),new(.4,.25),new(-.4,.25)]);
        var profile=new WorldSuitabilityProfile("floor","Fit complete sources","frame:floor",["floor"],[],[]);
        const string root="pose-frame",parent=root+"/containers-0",child=parent+"/equipment-0";
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"frame-sources",[
            new("world","world","Coordinate frame test",properties,[new("containers","container",1,"Required complete container")],[],[]),
            new("container","container","Movable containing area",properties,[new("equipment","equipment",1,"Required contained equipment")],[],[],
                PhysicalFootprint:new("meters","Full containing source","frame:container",container),Access:new(MetadataOccupancyKind.Reservation,[])),
            new("equipment","equipment","Required movable source",properties,[],[],[],PhysicalFootprint:new("meters","Full equipment source","frame:equipment",equipment),Access:new(MetadataOccupancyKind.Solid,[]))]);
        var request=new WorldPlacementRequest("containers","Choose a purposeful containing location","frame:location",null,
            [new("container","Required container","frame:container",null,profile)],[new("preferred",new(0,0)),new("alternate",new(12,0))],
            [new("location","container","preferred","Prefer the initial location within the authored domain","frame:location",0,15,0,1)]);
        var contents=new WorldPlacementRequest("contents","Keep equipment at the source origin","frame:contents",null,
            [new("equipment","Required equipment","frame:equipment",null,profile)],[new("center",new(0,0))],
            [new("center","equipment","center","Preserve exact stage offset","frame:contents",0,0,0,1)]);
        var control=new MetadataPlacementPoseControl("absolute-child",child,"Keep the required equipment at its authored domain pose","frame:control",new(12,0));
        var recipe=new WorldMetadataRecipe(1,root,"coordinate-frame-controls",catalog.Id,"world","local-domain",MetadataCoverage.PartialScope,
            ["Analytic hierarchy/frame control; complete playable-world design, artwork and runtime are outside this fixture."],new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:[0,0],RequireAllPositions:true,
            Surface:new(1,"frame-domain","meters",boundary,[],[new("floor","floor","Frame domain","frame:floor",boundary,0,new(0,0),0,0,0)]),
            Environment:new(1,"fields",[new("height","Floor height","frame:height","meters",0,[])]),
            Placement:new(request,[new("container",root,true,PartId:"containers")],[new("contents",parent,contents,[new("equipment",child,true)])],PoseControls:[control]));
        var world=generator.Generate(recipe,catalog);WorldMetadataGenerator.ValidateDocument(world);
        Check(world.Nodes.Single(n=>n.Id==parent).Position!.SequenceEqual(new[]{12d,0}),"world-frame child reconsiders its parent's initially preferred pose");
        Check(world.PlacementPlan!.Attempts.Any(a=>a.StageId=="contents"&&a.Status==WorldPlacementStatus.NoFeasibleAssignment),"actual failed child allocation drives parent reconsideration");
        Check(world.Nodes.Count==3&&world.AccessPlan!.Sites.Count==2,"both complete required sources remain");
        await Write(world,recipe,catalog,output,"absolute-child");
        var following=recipe with{Placement=recipe.Placement! with{PoseControls=[
            control with{Id="stage-child",Frame=MetadataPlacementPoseFrame.Stage,Position=new(0,0)},
            new("parent-pose",parent,"Move and turn the complete containing source","frame:parent",new(12,0),RotationDegrees:90)]}};
        var shifted=generator.Generate(following,catalog);WorldMetadataGenerator.ValidateDocument(shifted);
        Check(shifted.Nodes.Single(n=>n.Id==child).Position!.SequenceEqual(new[]{12d,0}),"stage-frame child follows actual parent translation");
        await Write(shifted,following,catalog,output,"stage-child");
        var impossible=following with{Placement=following.Placement! with{PoseControls=[control,following.Placement.PoseControls![1] with{Position=new(6,0)}]}};
        var session=new MetadataGenerationSession(generator,following,catalog);var before=session.Snapshot;
        Reject(()=>session.Apply(new("frame-conflict","Keep incompatible parent and child intents visible","frame:conflict",before.World.RecipeSha256,before.World.CatalogSha256,impossible,catalog)),"METADATA_PLACEMENT_POSE_CONFLICT");
        Check(Json(session.Snapshot)==Json(before),"frame conflict cannot move an explicit parent or corrupt accepted state");
        Console.WriteLine("PASS: absolute child feedback repositions its parent; stage translation, rigid parent rotation, fixed-intent conflict and rollback.");
    }
}
