using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;
using WorldCreator.Catalogs.Modern;

internal static class WorldSurroundingsTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldClearanceTests.Example();
        var source=TestCatalogs.Registry.Resolve(ModernCatalog.Selection).Structures.Single(d=>d.Id=="village-house");
        var variant=source.SizeVariants!.Single(v=>v.Id=="cottage");
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId) with{Parts=[new("sites","site",1,"Required source site")],ChildRequirements=[new("site",1,1)],Connections=[]};
        var site=e.Catalog.Archetypes.Single(a=>a.Id=="site") with{PhysicalFootprint=null,Structure=new(ModernCatalog.Selection,source.Id,variant.Id),
            Access=new(MetadataOccupancyKind.Solid,[new("front","Declared front entrance","example:front",new(0,-5),new(0,-9),source.AccessWidthMeters)])};
        var surface=new WorldSurfaceDefinition(1,"surface","meters",new([new(0,0),new(100,0),new(100,100),new(0,100)]),[],[],
            HeightField:new(1,"flat","ground","Surroundings fixture","example","meters",new(0,0),50,50,3,3,new double[9]));
        var request=e.Recipe.Placement!.Request with{Boundary=surface.Boundary,Demands=[e.Recipe.Placement.Request.Demands[0]],
            Anchors=[new("center",new(50,50))],Relations=[new("center","a","center","Retain designed center","example",0,0,0,1)]};
        return(e.Recipe with{Id="surroundings-world",Origin=[50,50],Surface=surface,RouteRules=null,
            Placement=new(request,[new("a","surroundings-world/sites-0",true,30,SiteLayout:new(4,4,2,2,"front","Required garden and entrance","example:surroundings"))]),
            RemainingScopes=["Source site bands only. Shared service delivery, roads, boundary assemblies, wider world generation, art and native runtime remain open."]},
            e.Catalog with{Archetypes=[root,site]});
    }
    public static async Task WriteExampleAsync(string dir)
    {
        if(Directory.Exists(dir)||File.Exists(dir))throw new IOException("Example exists");Directory.CreateDirectory(dir);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var e=Example();var gen=new WorldMetadataGenerator(TestCatalogs.Registry);var world=gen.Generate(e.Recipe,e.Catalog);
        var plan=world.PlacementPlan!;var source=plan.Footprints.Single();var features=plan.SiteFeatures!;
        Check(features.Count==2&&features.Any(f=>f.Kind==SiteFeatureKind.Garden)&&features.Any(f=>f.Kind==SiteFeatureKind.AccessWalkway),"required real features");
        Check(Near(Edge(source.Polygon,0),8)&&Near(Edge(source.Polygon,1),10),"source dimensions unchanged");
        Check(Near(Edge(source.Reservation!,0),12)&&Near(Edge(source.Reservation!,1),18),"independent directional envelope oracle");
        var garden=features.Single(f=>f.Kind==SiteFeatureKind.Garden);Check(Near(Edge(garden.Boundary,0),11.5)&&Near(Edge(garden.Boundary,1),3.5),"catalog garden depth and inset");
        var walk=features.Single(f=>f.Kind==SiteFeatureKind.AccessWalkway);Check(Near(Edge(walk.Boundary,0),world.StructureDefinitions.Single().Value.Definition.AccessWidthMeters)&&Near(Edge(walk.Boundary,1),4),"source access width and authored front depth");
        foreach(var feature in features){var local=source.SiteFeatures!.Single(f=>f.Id==feature.Id);for(int i=0;i<4;i++)Check(feature.Boundary.Vertices[i]==local.Boundary.Vertices[i].Offset(50,50),"exact rigid world translation");}
        Check(!source.RemainingRequirements.Contains("catalog-site-rules")&&source.RemainingRequirements.Contains("catalog-siting-profile"),"bounded source policy acceptance");
        Check(world.AccessPlan!.Sites.Single().Footprint.Vertices.SequenceEqual(plan.Placement.Sites.Single().Footprint.Footprint.Vertices),"surroundings never become solid");
        var binding=e.Recipe.Placement!.Bindings.Single();
        WorldMetadataRecipe With(MetadataSiteLayout layout)=>e.Recipe with{Placement=e.Recipe.Placement with{Bindings=[binding with{SiteLayout=layout}]}};
        Reject(()=>gen.Generate(With(binding.SiteLayout! with{RearDepthMeters=1}),e.Catalog),"METADATA_SITE_RULES_UNSATISFIED");
        Reject(()=>gen.Generate(With(binding.SiteLayout! with{FrontDepthMeters=5}),e.Catalog),"METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        Reject(()=>gen.Generate(With(binding.SiteLayout! with{EntrancePortId="missing"}),e.Catalog),"METADATA_SITE_ENTRANCE_REQUIRED");
        Reject(()=>gen.Generate(With(binding.SiteLayout! with{RearDepthMeters=-1}),e.Catalog),"METADATA_SITE_LAYOUT_INVALID");
        Reject(()=>gen.Generate(With(binding.SiteLayout! with{RearDepthMeters=double.NaN}),e.Catalog),""); // Snapshot refuses non-JSON numbers before geometry.
        var hazardCenter=new WorldPoint(46.5,50+7*Math.Cos(Math.PI/6));
        var hazard=new WorldPolygon([hazardCenter.Offset(-.05,-.05),hazardCenter.Offset(.05,-.05),hazardCenter.Offset(.05,.05),hazardCenter.Offset(-.05,.05)]);
        Reject(()=>gen.Generate(e.Recipe with{Surface=e.Recipe.Surface! with{Exclusions=[new("garden-hazard","Forbidden garden ground","test",hazard)]}},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var repeated=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id==e.Recipe.RootArchetypeId?a with{Parts=[a.Parts.Single() with{Count=2}],ChildRequirements=[new("site",2,2)]}:a).ToArray()};
        var repeatedRecipe=e.Recipe with{Placement=e.Recipe.Placement with{Request=e.Recipe.Placement.Request with{Anchors=[],Relations=[]},Bindings=[binding with{NodeId=e.Recipe.Id,PartId="sites"}]}};
        Check(gen.Generate(repeatedRecipe,repeated).PlacementPlan!.SiteFeatures!.Count==4,"direct-part templates retain each source surroundings program");
        Reject(()=>gen.Generate(e.Recipe with{Placement=e.Recipe.Placement with{MaximumWork=1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        var missingAccess=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a with{Access=null}).ToArray()};
        Reject(()=>gen.Generate(e.Recipe,missingAccess),"METADATA_SITE_ENTRANCE_REQUIRED");
        var changed=world with{PlacementPlan=plan with{SiteFeatures=features.Where(f=>f.Kind!=SiteFeatureKind.Garden).ToArray()}};
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(changed),"METADATA_PLACEMENT_PLAN_MISMATCH");
        var definitions=world.StructureDefinitions.ToDictionary(p=>p.Key,p=>p.Value with{Definition=p.Value.Definition with{
            SitingProfile=p.Value.Definition.SitingProfile! with{RequiredSurroundings=new HashSet<SiteFeatureKind>{SiteFeatureKind.CoolingPlant}}}});
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(world with{StructureDefinitions=definitions}),"METADATA_SITE_REQUIRED_SURROUNDINGS_MISSING");
        var conflicting=world.StructureDefinitions.ToDictionary(p=>p.Key,p=>p.Value with{Definition=p.Value.Definition with{
            SiteRules=p.Value.Definition.SiteRules!.Select(r=>r with{Id="access"}).ToArray()}});
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(world with{StructureDefinitions=conflicting}),"METADATA_SITE_FEATURE_INVALID");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{gen.Generate(e.Recipe,e.Catalog,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-surroundings-"+Guid.NewGuid().ToString("N"));
        try{
            await WriteExampleAsync(dir);await gen.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output/world-metadata.json"))),"actual file replay");
            if(cli is not null){
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"cli"))==0,"actual CLI");
                await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(With(binding.SiteLayout! with{RearDepthMeters=1})));
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"failed"))==1&&!Directory.Exists(Path.Combine(dir,"failed")),"required surroundings cannot be omitted on failure");
                var audit=await File.ReadAllTextAsync(Path.Combine(dir,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"audit success and failure");
            }
        }finally{if(Directory.Exists(dir)&&Path.GetDirectoryName(Path.GetFullPath(dir))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(dir).StartsWith("worldcreator-surroundings-",StringComparison.Ordinal))Directory.Delete(dir,true);}
        Console.WriteLine("PASS: exact source dimensions, directional site/access/garden geometry, rotation, source policy/entrance rejection, required surroundings, cancellation, replay/tamper and CLI when supplied. Full siting/service/art/runtime remain open.");
    }
    private static double Edge(WorldPolygon polygon,int i)=>polygon.Vertices[i].DistanceTo(polygon.Vertices[(i+1)%polygon.Vertices.Count]);
    private static bool Near(double a,double b)=>Math.Abs(a-b)<1e-7;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static async Task<int> Run(string cli,string dir,params string[] args){var info=new ProcessStartInfo("dotnet"){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);using var process=Process.Start(info)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}Console.Write(await output);Console.Write(await errors);return process.ExitCode;}
}
