using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
using WorldCreator.Catalogs.Modern;

internal static class WorldHierarchyPlacementTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldPlacementTests.Example();var request=e.Recipe.Placement!.Request;
        var root=request with{Demands=request.Demands.Select(d=>d with{Footprint=null}).ToArray()};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id switch{
            "camp"=>a with{Parts=[a.Parts[0] with{Layout=null}],PhysicalFootprint=new("meters","Camp reservation","catalog:camp",Box(-10,-8,20,16))},
            "station"=>a with{Parts=[new("objects","object",1,"Required station component")],ChildRequirements=[new("object",1,1)],
                PhysicalFootprint=new("meters","Station ground footprint","catalog:station",Box(-3,-2.5,6,5))},_=>a}).Append(
                new("object","object","Station component",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
                    PhysicalFootprint:new("meters","Component footprint","catalog:component",Box(-.5,-.5,1,1)))).ToArray()};
        var nested=new List<MetadataNestedPlacement>();
        for(int i=0;i<2;i++)
        {
            string parent="terrain-placement/camps-"+i;var profile=request.Demands[i].Profile;
            var demands=Enumerable.Range(0,2).Select(j=>new WorldSiteDemand("station-"+j,"Required station","catalog:station",null,profile)).ToArray();
            nested.Add(new("camp-"+i,parent,new("camp-members","Allocate station footprints","catalog:camp",null,demands,[],[],MinimumSeparation:1,MaximumWork:20000000),
                demands.Select((d,j)=>new MetadataPlacementBinding(d.Id,parent+"/stations-"+j,true)).ToArray()));
            for(int j=0;j<2;j++)
            {
                string station=parent+"/stations-"+j;
                nested.Add(new("station-"+i+"-"+j,station,new("station-member","Allocate station component","catalog:station",null,
                    [new("component","Required component","catalog:component",null,profile)],[new("center",new(0,0))],
                    [new("center-access","component","center","Keep component near station center","catalog:component",0,3,0,1)]),[new("component",station+"/objects-0",true)]));
            }
        }
        return(e.Recipe with{Placement=new(root,e.Recipe.Placement.Bindings.Select(b=>b with{UseCatalogFootprint=true}).ToArray(),nested),
            RemainingScopes=["Hierarchical reserved and physical catalog polygons; access/capacity, route collision with constituents, junctions, art and native runtime remain open."]},catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var boundary=Box(0,0,20,10);var field=new WorldHeightField(1,"flat","ground","Hierarchy test","test","meters",new(0,0),10,10,3,2,[0,0,0,0,0,0]);
        var surface=new WorldSurfaceDefinition(1,"flat","meters",boundary,[],[],HeightField:field);
        var environment=new WorldEnvironmentDefinition(1,"x-field",[new("x","World x","test","meters",0,[new(WorldFieldInput.X,1)])]);
        var profile=new WorldSuitabilityProfile("central","Prefer central region","test",["ground"],[],[new("center","x","Near center","test",10,10,1)]);
        var root=new WorldPlacementRequest("parent","Reserve required parent","test",boundary,[new("parent","Required parent","test",Box(-4,-4,8,8),profile)],[],[]);
        var childProfile=profile with{Requirements=[new("west","x","Child requires western terrain","test",0,5)]};
        var child=new MetadataNestedPlacement("child","world/parent",new("child","Fit child in parent","test",null,
            [new("child","Required child","test",Box(-1,-1,2,2),childProfile)],[],[]),[new("child","world/parent/child")]);
        var definition=new MetadataPlacementDefinition(root,[new("parent","world/parent")],[child]);
        List<WorldMetadataNode> Nodes()=>[new("world","world","region",null,"World",new Dictionary<string,JsonElement>(),[10,5],null),
            new("world/parent","parent","site","world","Required parent",new Dictionary<string,JsonElement>(),null,null),
            new("world/parent/child","child","part","world/parent","Required child",new Dictionary<string,JsonElement>(),null,null)];
        var nodes=Nodes();var planner=new MetadataPlacementPlanner();var plan=planner.Apply(definition,environment,surface,new(["x","y"],"meters"),nodes,[]);
        Check(plan.Placement.Sites.Single().Position.X==5,"child infeasibility moves parent from preferred x=10 to x=5");
        Check(plan.Placement.Conflicts["dependent-allocation"]>0&&plan.Attempts.Any(a=>a.StageId=="child"&&a.Status==WorldPlacementStatus.NoFeasibleAssignment),"dependent rejection evidence retained");
        Check(plan.Placement.Sites.Single().Footprint.Footprint.ContainsPolygon(plan.NestedStages.Single().Placement.Sites.Single().Footprint.Footprint),"full child polygon inside exact parent");
        var unmodified=Nodes();
        Reject(()=>planner.Apply(definition with{MaximumStageAttempts=1},environment,surface,new(["x","y"],"meters"),unmodified,[]),"METADATA_PLACEMENT_STAGE_ATTEMPT_BUDGET");
        Check(unmodified[1].Position is null&&unmodified[2].Position is null,"failed dependent allocation is atomic");
        Reject(()=>planner.Apply(definition with{MaximumWork=1},environment,surface,new(["x","y"],"meters"),Nodes(),[]),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Apply(definition with{NestedStages=[child with{ParentNodeId="world"}]},environment,surface,new(["x","y"],"meters"),Nodes(),[]),"METADATA_PLACEMENT_BINDING_INVALID");
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==11&&world.Nodes.All(n=>n.Position is {Count:2}),"complete required three-level content");
        var placement=world.PlacementPlan!;Check(placement.NestedStages.Count==6&&placement.RelativePositions.Count==0,"six dependent stages use actual physical allocation");
        var shapes=new Dictionary<string,WorldPolygon>();
        foreach(var binding in placement.Definition.Bindings)shapes.Add(binding.NodeId,placement.Placement.Sites.Single(s=>s.DemandId==binding.DemandId).Footprint.Footprint);
        foreach(var stage in placement.NestedStages)
        {
            var declared=placement.Definition.NestedStages!.Single(s=>s.Id==stage.Id);
            foreach(var binding in declared.Bindings)
            {
                var shape=stage.Placement.Sites.Single(s=>s.DemandId==binding.DemandId).Footprint.Footprint;
                Check(shapes[stage.ParentNodeId].ContainsPolygon(shape),"exact nested containment");shapes.Add(binding.NodeId,shape);
                var source=stage.Footprints.Single(f=>f.NodeId==binding.NodeId);var node=world.Nodes.Single(n=>n.Id==binding.NodeId);
                var catalogShape=world.FootprintDefinitions![node.ArchetypeId].Polygon;
                Check(Json(source.Polygon)==Json(catalogShape),"source polygon unscaled and retained");
            }
        }
        Check(shapes.Count==10&&world.FootprintDefinitions!.Count==3,"ten qualified polygons from three catalog definitions");
        var reordered=generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{NestedStages=e.Recipe.Placement.NestedStages!.Reverse().ToArray()}},e.Catalog);
        Check(Json(world.Nodes)==Json(reordered.Nodes)&&Json(placement.NestedStages)==Json(reordered.PlacementPlan!.NestedStages),"dependency order independent of declaration order");
        var conflicting=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Select(d=>d with{Footprint=Box(-1,-1,2,2)}).ToArray()}}};
        Reject(()=>generator.Generate(conflicting,e.Catalog),"METADATA_PLACEMENT_FOOTPRINT_SOURCE_CONFLICT");
        var missing=e.Recipe with{Placement=e.Recipe.Placement! with{NestedStages=e.Recipe.Placement.NestedStages!.Where(s=>s.Id!="station-1-1").ToArray()}};
        Reject(()=>generator.Generate(missing,e.Catalog),"METADATA_PHYSICAL_ALLOCATION_REQUIRED");
        var tampered=world with{PlacementPlan=placement with{Work=0}};
        Reject(()=>planner.ValidateDocument(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
        var changedSources=world.FootprintDefinitions!.ToDictionary(p=>p.Key,p=>p.Key=="station"?p.Value with{SourceRuleId="changed"}:p.Value);
        Reject(()=>planner.ValidateDocument(world with{FootprintDefinitions=changedSources}),"METADATA_PLACEMENT_PLAN_MISMATCH");
        using(var c=new CancellationTokenSource()){c.Cancel();try{generator.Generate(e.Recipe,e.Catalog,c.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var single=WorldPlacementTests.Example();
        var structureCatalog=single.Catalog with{Archetypes=single.Catalog.Archetypes.Select(a=>a.Id switch{
            "root"=>a with{Parts=[a.Parts[0] with{Count=1}],ChildRequirements=[new("camp",1,1)]},
            "camp"=>a with{Parts=[],ChildRequirements=[],Structure=new(ModernCatalog.Selection,"warehouse","small")},_=>a}).ToArray()};
        var structureRequest=single.Recipe.Placement!.Request with{Demands=[single.Recipe.Placement.Request.Demands[0] with{Footprint=null}],Relations=[]};
        var structureWorld=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(single.Recipe with{NetworkRules=null,
            Placement=new(structureRequest,[single.Recipe.Placement.Bindings[0] with{UseCatalogFootprint=true,RotationDegrees=30}])},structureCatalog);
        var variant=structureWorld.StructureDefinitions.Single().Value.Variant;var sourceShape=structureWorld.PlacementPlan!.Footprints.Single();
        Check(sourceShape.SourceId.StartsWith("structure:",StringComparison.Ordinal)&&Math.Abs(sourceShape.Polygon.Vertices[0].DistanceTo(sourceShape.Polygon.Vertices[1])-variant.WidthMeters)<1e-7&&
            Math.Abs(sourceShape.Polygon.Vertices[1].DistanceTo(sourceShape.Polygon.Vertices[2])-variant.DepthMeters)<1e-7,"registered variant width/depth preserved under authored rotation");
        Check(sourceShape.RemainingRequirements.Contains("catalog-access")&&sourceShape.RemainingRequirements.Contains("catalog-context-and-capacity"),"dimension bridge does not silently certify catalog siting/access policy");
        string directory=Path.Combine(Path.GetTempPath(),"worldcreator-hierarchy-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(directory);await generator.GenerateFilesAsync(Path.Combine(directory,"recipe.json"),Path.Combine(directory,"catalog.json"),Path.Combine(directory,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(directory,"output/world-metadata.json"))),"full hierarchy persistence and replay");
            await File.WriteAllTextAsync(Path.Combine(directory,"bad.json"),Json(tampered));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(directory,"bad.json")),"METADATA_PLACEMENT_PLAN_MISMATCH");
            await RejectAsync(()=>new WorldMetadataSchematicExporter().CreateAsync(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
            if(cli is not null)
            {
                string recipe=Path.Combine(directory,"recipe.json"),catalogFile=Path.Combine(directory,"catalog.json");
                Check(await Run(cli,directory,recipe,catalogFile,Path.Combine(directory,"cli"))==0,"actual CLI publishes hierarchical polygons");
                await File.WriteAllTextAsync(recipe,Json(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumStageAttempts=1}}));
                Check(await Run(cli,directory,recipe,catalogFile,Path.Combine(directory,"failed"))==1,"actual CLI refuses incomplete dependent allocation");
                Check(!Directory.Exists(Path.Combine(directory,"failed")),"failed hierarchy not published");
                string audit=await File.ReadAllTextAsync(Path.Combine(directory,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"durable hierarchy outcomes");
            }
        }
        finally{if(Directory.Exists(directory)&&Path.GetDirectoryName(Path.GetFullPath(directory))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(directory).StartsWith("worldcreator-hierarchy-",StringComparison.Ordinal))Directory.Delete(directory,true);}
        Console.WriteLine("PASS: child infeasibility backtracks parent, full catalog sizes and three-level polygon containment, complete required content, deterministic dependency order, source conflicts/missing physical members, aggregate work/stage limits and persistence/tamper. Access/capacity, route-child collision and game/art/runtime gates remain open.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){p.Kill(true);throw;}Console.Write(await stdout);Console.Write(await stderr);return p.ExitCode;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Hierarchy: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
