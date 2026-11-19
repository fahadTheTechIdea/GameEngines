using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldNestedSourceFitTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool value,string reason){if(!value)throw new Exception("Nested source fit: "+reason);}
    private static MetadataPlacementInfeasibleException Refuse(Action action)
    {
        try{action();}catch(MetadataPlacementInfeasibleException error)when(error.Message.Contains("NESTED_SOURCE_ENVELOPE_TOO_LARGE",StringComparison.Ordinal))
        {Check(error.Data["PlacementPoseInvariantFailure"] is true&&error.Data["PlacementWork"] is long work&&work>0,"attributed intrinsic refusal and charged work");return error;}
        throw new Exception("Expected intrinsic nested-source refusal");
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double parentRotation,double childRotation,bool reservation=false)
    {
        var e=WorldProvisionalRelationTests.NestedExample();string id=e.Recipe.Id;var boundary=Box(-30,-30,60,60);var profile=e.Recipe.Placement!.Request.Demands[0].Profile;
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id switch{
            "root"=>a with{Parts=[a.Parts[0] with{Count=1}]},
            "container"=>a with{Parts=[new("child","site",1,"Required exact source")],PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-8,-7,16,14)}},
            _=>a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=reservation?Box(-.2,-.2,.4,.4):Box(-9,-.2,18,.4),Reservation=reservation?Box(-9,-.3,18,.6):null}}}).ToArray()};
        var child=new WorldPlacementRequest("child","Preserve required exact source","test:child",null,[new("child","Required child","test:child",null,profile)],
            [new("center",new(0,0))],[new("pin","child","center","Preserve declared local origin","test:pin",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var program=new MetadataNestedProgram("child","Required single source in its parent","test:child",id,"container",1,1,child,[new("child","child",RotationDegrees:childRotation)]);
        var root=e.Recipe.Placement.Request with{Boundary=boundary};
        return(e.Recipe with{Surface=e.Recipe.Surface! with{Boundary=boundary,Patches=e.Recipe.Surface.Patches.Select(p=>p with{Boundary=boundary}).ToArray()},
            Placement=new(root,[new("parents",id,true,parentRotation,PartId:"containers",Population:new("Declared parent frame","test:parent",Sockets:[new(id+"/containers-0",new(0,0))]))],Programs:[program])},catalog);
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);var generator=new WorldMetadataGenerator();
        var straight=Example(0,0);Refuse(()=>generator.Generate(straight.Recipe,straight.Catalog));
        var rotated=Example(45,45);Refuse(()=>generator.Generate(rotated.Recipe,rotated.Catalog));
        var reservation=Example(0,0,true);Refuse(()=>generator.Generate(reservation.Recipe,reservation.Catalog));
        var fitting=Example(45,0);var world=generator.Generate(fitting.Recipe,fitting.Catalog);
        Check(world.Nodes.Count==3&&world.AccessPlan!.Sites.Count==2,"rotated parent contains the complete unrotated 18m source without shrinking");
        WorldMetadataGenerator.ValidateDocument(world);
        var nested=WorldProvisionalRelationTests.NestedExample();var catalog=nested.Catalog with{Archetypes=nested.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-9,-.2,18,.4)}}:a).ToArray()};
        var container=nested.Catalog.Archetypes.Single(a=>a.Id=="container");var sourceCatalog=nested.Catalog with{Archetypes=[nested.Catalog.Archetypes.Single(a=>a.Id=="root"),container with{Parts=[]}]};
        var source=nested.Recipe with{Placement=nested.Recipe.Placement! with{Programs=null},Regions=new("home","Keep the complete parent region","test:home",nested.Recipe.Surface!.Boundary!,nested.Recipe.Placement.Request.Demands[0].Profile,[new("home","Declared home region","test:home",new(0,0),504)])};
        var consumer=nested.Recipe with{Regions=source.Regions,PopulationSource=new(source,sourceCatalog)};
        var failed=Refuse(()=>generator.Generate(consumer,catalog));
        Check(failed.Data["CoordinationAttempts"] is IReadOnlyList<MetadataPopulationCoordinationAttempt> attempts&&attempts.Count==1&&attempts[0].AddedNogood is null,
            "pose-invariant child size failure never creates a source-pose prohibition");
        var session=new MetadataGenerationSession(generator,fitting.Recipe,fitting.Catalog);var accepted=session.Snapshot;
        try{session.Apply(new("refuse","Preserve accepted world on intrinsic failure","test:refuse",accepted.World.RecipeSha256,accepted.World.CatalogSha256,rotated.Recipe,rotated.Catalog));throw new Exception("Invalid source edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted),"intrinsic failure preserves accepted session");
        await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(fitting.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(fitting.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"packet"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"packet/world-metadata.json")))==Json(world),"saved source-fit replay");
        Console.WriteLine("PASS: intrinsic nested directional-width refusal; exact straight/rotated/reservation sources, a fitting cross-rotation, no pose nogood for invariant failure, source-cache replay and session preservation.");
    }
}
