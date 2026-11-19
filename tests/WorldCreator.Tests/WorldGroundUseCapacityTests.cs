using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldGroundUseCapacityTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string reason){if(!value)throw new Exception("Ground capacity: "+reason);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double minimum,bool nested=false,bool unrelated=false,
        bool concave=false,double rotation=0,WorldPlacementExecution execution=WorldPlacementExecution.Search,double offset=0)
    {
        const string id="ground-capacity";
        var e=WorldSurfaceTests.Example();var boundary=Box(offset,offset,60,40);
        var profile=new WorldSuitabilityProfile("ground","Retain required source program","test:ground",["ground"],[],[]);
        var properties=new Dictionary<string,JsonElement>();
        var groundShape=concave?new WorldPolygon([new(-5,-5),new(5,-5),new(5,0),new(0,0),new(0,5),new(-5,5)]):Box(-5,-5,10,10);
        var root=new WorldMetadataArchetype("root","world","Capacity qualification",properties,
            unrelated?[new("ground","ground",1,"Required activity ground"),new("other","container",1,"Unrelated required scope")]:[new("ground","ground",1,"Required activity ground")],[],[],LayerId:"intent");
        var ground=new WorldMetadataArchetype("ground","ground-area","Declared activity ground",properties,
            unrelated?[]:nested?[new("inner","container",1,"Required reserved inner scope")]:[new("fixtures","fixture",2,"Required exact fixtures")],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Exact ground source","test:ground",groundShape,GroundUse:new("activity","Required open source ground","test:ground-use",minimum)),
            Access:new(MetadataOccupancyKind.Reservation,[]));
        var container=new WorldMetadataArchetype("container","compound","Required reserved scope",properties,[new("fixtures","fixture",2,"Required exact fixtures")],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Exact inner reservation","test:container",Box(-4,-4,8,8)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var fixture=new WorldMetadataArchetype("fixture","equipment","Required source-sized fixture",properties,[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Exact solid source","test:fixture",Box(-1,-1,2,2)),Access:new(MetadataOccupancyKind.Solid,[]));
        var request=new WorldPlacementRequest("ground","Retain declared ground","test:ground",boundary,
            unrelated?[new("ground","Required ground","test:ground",null,profile),new("other","Required unrelated scope","test:other",null,profile)]:[new("ground","Required ground","test:ground",null,profile)],
            unrelated?[new("center",new(offset+15,offset+15)),new("other-anchor",new(offset+40,offset+15))]:[new("center",new(offset+15,offset+15))],
            unrelated?[new("pin","ground","center","Retain authored anchor","test:pin",0,0,0,1),new("other","other","other-anchor","Retain unrelated anchor","test:pin",0,0,0,1)]:[new("pin","ground","center","Retain authored anchor","test:pin",0,0,0,1)],Execution:execution);
        string groundId=id+"/ground-0",fixtureParent=unrelated?id+"/other-0":nested?groundId+"/inner-0":groundId;
        var fixtureRequest=new WorldPlacementRequest("fixtures","Retain all source fixtures","test:fixtures",null,
            [new("a","First required fixture","test:a",null,profile),new("b","Second required fixture","test:b",null,profile)],
            [new("anchor-a",new(-2,concave?-2:0)),new("anchor-b",new(2,concave?-2:0))],
            [new("a","a","anchor-a","Retain first fixture anchor","test:a",0,0,0,1),new("b","b","anchor-b","Retain second fixture anchor","test:b",0,0,0,1)],Execution:execution);
        var stages=new List<MetadataNestedPlacement>();
        if(nested&&!unrelated)stages.Add(new("inner",groundId,new("inner","Retain inner reservation","test:inner",null,[new("inner","Required inner scope","test:inner",null,profile)],
            [new("center",new(0,0))],[new("pin","inner","center","Retain local origin","test:pin",0,0,0,1)],Execution:execution),[new("inner",fixtureParent,true)]));
        stages.Add(new("fixtures",fixtureParent,fixtureRequest,[new("a",fixtureParent+"/fixtures-0",true),new("b",fixtureParent+"/fixtures-1",true)]));
        return(e.Recipe with{Id=id,GameProfile="framework",Origin=[offset+25,offset+20],Surface=new(1,"flat","meters",boundary,[],[new("ground","ground","Capacity fixture terrain","test:terrain",boundary,0,new(0,0),0,0,0)]),
            Environment=new(1,"coordinates",[new("x","Declared coordinate","test:coordinate","meters",0,[new(WorldFieldInput.X,1)])]),RouteRules=null,NetworkRules=null,
            Placement=new(request,unrelated?[new("ground",groundId,true,rotation),new("other",id+"/other-0",true)]:[new("ground",groundId,true,rotation)],stages),
            RemainingScopes=["Required source-ground capacity qualification; terrain variety, traversal and whole-world design are separate."]},
            e.Catalog with{Archetypes=nested||unrelated?[root,ground,container,fixture]:[root,ground,fixture]});
    }

    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) DeepExample(double minimum)
    {
        const int count=12;var e=Example(minimum,nested:true);var ground=e.Catalog.Archetypes.Single(a=>a.Id=="ground");
        var container=e.Catalog.Archetypes.Single(a=>a.Id=="container");var types=e.Catalog.Archetypes.Where(a=>a.Id!="container"&&a.Id!="ground").ToList();
        types.Add(ground with{Parts=[new("inner","level-0",1,"Required nested source scope")]});
        var stages=new List<MetadataNestedPlacement>();string parent=e.Recipe.Id+"/ground-0";
        for(int i=0;i<count;i++)
        {
            types.Add(container with{Id="level-"+i,Parts=i==count-1?container.Parts:[new("inner","level-"+(i+1),1,"Required next source scope")]});
            string child=parent+"/inner-0";var request=e.Recipe.Placement!.NestedStages![0].Request with{Id="inner-"+i};
            stages.Add(new("inner-"+i,parent,request,[new("inner",child,true)]));parent=child;
        }
        stages.Add(new("fixtures",parent,e.Recipe.Placement!.NestedStages!.Last().Request,[new("a",parent+"/fixtures-0",true),new("b",parent+"/fixtures-1",true)]));
        return(e.Recipe with{Placement=e.Recipe.Placement with{NestedStages=stages}},e.Catalog with{Archetypes=types});
    }

    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Evidence exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var results=new List<object>();
        JsonElement Refuse(string name,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
        {
            try{generator.Generate(recipe,catalog);}
            catch(ArgumentException error)when(error.Message.StartsWith("METADATA_PLACEMENT_UNRESOLVED:GROUND_USE_CAPACITY:",StringComparison.Ordinal))
            {
                Check(error.Data["PlacementPoseInvariantFailure"] is true&&error.Data["PlacementWork"] is long w&&w>0,"charged pose-invariant failure");
                Check(!error.Data.Contains("PlacementAttempts")&&!error.Data.Contains("FeasibilityAttempts"),"no candidate or whole-assignment attempts before proof");
                var proof=JsonSerializer.SerializeToElement(error.Data["GroundUseCapacityWitness"],WorldJson.CreateOptions());
                Check(proof.GetProperty("SolidNodeIds").GetArrayLength()==2,"both required solid siblings in proof");
                Check(proof.GetProperty("RequiredSolidArea").GetDouble()==8,"source-sized solids, not reservation area");
                Check(proof.GetProperty("MaximumOpenFraction").GetDouble()<proof.GetProperty("MinimumOpenFraction").GetDouble(),"strict capacity witness");
                File.WriteAllText(Path.Combine(output,name+"-recipe.json"),Json(recipe));File.WriteAllText(Path.Combine(output,name+"-catalog.json"),Json(catalog));
                results.Add(new{Name=name,Status="REFUSED_BEFORE_SEARCH",Diagnostic=error.Message,Work=error.Data["PlacementWork"],Proof=proof});return proof;
            }
            throw new Exception("Expected source-ground capacity preflight for "+name);
        }
        var impossible=Example(.95);Refuse("two-solid-siblings",impossible.Recipe,impossible.Catalog);
        var limited=impossible.Recipe with{Placement=impossible.Recipe.Placement! with{Request=impossible.Recipe.Placement.Request with{MaximumSearchStates=1}}};
        Refuse("before-insufficient-search-budget",limited,impossible.Catalog);
        var prescribed=Example(.95,execution:WorldPlacementExecution.Prescribed);Refuse("prescribed",prescribed.Recipe,prescribed.Catalog);
        var rotated=Example(.95,rotation:23);Refuse("rotated",rotated.Recipe,rotated.Catalog);
        var nested=Example(.95,nested:true);Refuse("nested-reservation",nested.Recipe,nested.Catalog);
        var concave=Example(.92,concave:true);Refuse("concave",concave.Recipe,concave.Catalog);
        var deep=DeepExample(.95);Refuse("deep-containment",deep.Recipe,deep.Catalog);
        var sourceCatalog=impossible.Catalog with{Archetypes=impossible.Catalog.Archetypes.Select(a=>a.Id=="ground"?a with{Parts=[]}:a).ToArray()};
        var sourceRecipe=impossible.Recipe with{Placement=impossible.Recipe.Placement! with{NestedStages=null},
            Regions=new("home","Retain the complete source ground region","test:home",impossible.Recipe.Surface!.Boundary!,impossible.Recipe.Placement.Request.Demands[0].Profile,
                [new("home","Declared source region","test:home",new(15,15),100)])};
        try{generator.Generate(impossible.Recipe with{Regions=sourceRecipe.Regions,PopulationSource=new(sourceRecipe,sourceCatalog)},impossible.Catalog);throw new Exception("Missing coordinated capacity refusal");}
        catch(ArgumentException error)when(error.Message.Contains("GROUND_USE_CAPACITY",StringComparison.Ordinal))
        {
            Check(error.Data["CoordinationAttempts"] is IReadOnlyList<MetadataPopulationCoordinationAttempt> attempts&&attempts.Count==1&&attempts[0].AddedNogood is null,
                "pose-invariant capacity failure must not prohibit source positions or retry coordination");
        }
        try{generator.Generate(impossible.Recipe with{Placement=impossible.Recipe.Placement! with{MaximumWork=1}},impossible.Catalog);throw new Exception("Missing work refusal");}
        catch(ArgumentException error)when(error.Message=="WORLD_ROUTE_WORK_BUDGET"){Check(!error.Data.Contains("PlacementPoseInvariantFailure"),"budget exhaustion is not geometric impossibility");}
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{generator.Generate(impossible.Recipe,impossible.Catalog,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        }
        var fitting=Example(.9);var world=generator.Generate(fitting.Recipe,fitting.Catalog);
        Check(world.Nodes.Count==4&&world.AccessPlan!.Sites.Count==3,"all sources retained");
        var report=new MetadataPhysicalAccountingPlanner().Measure(world);
        Check(Math.Abs(report.GroundUses!.Single().OpenArea-92)<1e-8,"independent100-2x4 source-area oracle");
        foreach(var item in new[]{Example(.92),Example(.9200000005),Example(.9,nested:true),Example(.85,concave:true),Example(1,unrelated:true),Example(.9,rotation:23),Example(.9,offset:9999900)})
        {
            var accepted=generator.Generate(item.Recipe,item.Catalog);WorldMetadataGenerator.ValidateDocument(accepted);
            results.Add(new{Status="ACCEPTED",item.Recipe.Placement!.Request.Execution,Nodes=accepted.Nodes.Count,Report=new MetadataPhysicalAccountingPlanner().Measure(accepted)});
        }
        var withClearance=fitting.Catalog with{Archetypes=fitting.Catalog.Archetypes.Select(a=>a.Id=="fixture"?a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(-1.5,-1.5,3,3)}}:a).ToArray()};
        var clearanceWorld=generator.Generate(fitting.Recipe,withClearance);
        Check(Math.Abs(new MetadataPhysicalAccountingPlanner().Measure(clearanceWorld).GroundUses!.Single().OpenArea-92)<1e-8,
            "clearance reservations cannot inflate solid capacity");
        var deepFit=DeepExample(.9200000005);var deepWorld=generator.Generate(deepFit.Recipe,deepFit.Catalog);
        Check(deepWorld.Nodes.Count==16&&Math.Abs(new MetadataPhysicalAccountingPlanner().Measure(deepWorld).GroundUses!.Single().OpenArea-92)<1e-8,
            "twelve contained reservation levels retain all sources and near-threshold ground");
        WorldMetadataGenerator.ValidateDocument(deepWorld);
        var shadow=Example(.95,nested:true);
        var shadowCatalog=shadow.Catalog with{Archetypes=shadow.Catalog.Archetypes.Select(a=>a.Id=="container"?a with{
            Purpose="Solid parent containing an overlapping solid member",Parts=[new("fixtures","fixture",1,"Required nested solid")],
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-1,-1,2,2)},Access=new(MetadataOccupancyKind.Solid,[])}:a).ToArray()};
        var shadowStage=shadow.Recipe.Placement!.NestedStages!.Last();
        var shadowPlacement=shadow.Recipe.Placement with{NestedStages=[shadow.Recipe.Placement.NestedStages[0],shadowStage with{
            Request=shadowStage.Request with{Demands=[shadowStage.Request.Demands[0]],Anchors=[new("center",new(0,0))],
                Relations=[new("pin","a","center","Retain overlapping source hierarchy","test:shadow",0,0,0,1)]},Bindings=[shadowStage.Bindings[0]]}]};
        var shadowWorld=generator.Generate(shadow.Recipe with{Placement=shadowPlacement},shadowCatalog);
        var shadowReport=new MetadataPhysicalAccountingPlanner().Measure(shadowWorld);
        Check(shadowReport.SolidSources==2&&Math.Abs(shadowReport.GroundUses!.Single().OpenArea-96)<1e-8,
            "potentially overlapping solid groups from separate stages must not be added");
        WorldMetadataGenerator.ValidateDocument(shadowWorld);
        results.Add(new{Name="overlapping-stage-solids",Status="ACCEPTED_OVERLAPPING_STAGES",Report=shadowReport});
        var badRole=fitting.Catalog with{Archetypes=fitting.Catalog.Archetypes.Select(a=>a.Id=="ground"?a with{Access=new(MetadataOccupancyKind.Solid,[])}:a).ToArray()};
        try{generator.Generate(fitting.Recipe,badRole);throw new Exception("Missing role refusal");}
        catch(ArgumentException error)when(error.Message.StartsWith("METADATA_GROUND_USE_RESERVATION_REQUIRED:",StringComparison.Ordinal))
        {Check(!error.Data.Contains("PlacementPoseInvariantFailure"),"invalid role remains malformed declaration, not geometric proof");}
        var session=new MetadataGenerationSession(generator,fitting.Recipe,fitting.Catalog);var snapshot=Json(session.Snapshot);
        try{session.Apply(new("capacity-failure","Retain accepted program","test:edit",session.Snapshot.World.RecipeSha256,session.Snapshot.World.CatalogSha256,impossible.Recipe,impossible.Catalog));throw new Exception("Missing edit refusal");}
        catch(MetadataRegenerationException error){Check(error.Message.Contains("GROUND_USE_CAPACITY",StringComparison.Ordinal),"attributed edit refusal");}
        Check(Json(session.Snapshot)==snapshot,"failed capacity edit preserves exact accepted state");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(fitting.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(fitting.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"impossible-recipe.json"),Json(impossible.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"impossible-catalog.json"),Json(impossible.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"packet"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"packet/world-metadata.json")))==Json(world),"saved preflight replay");
        await File.WriteAllTextAsync(Path.Combine(output,"results.json"),Json(results));
        Console.WriteLine("PASS: conservative source-ground capacity proof before search; both sibling sources, nesting, rotations, concavity, boundary tolerance, unrelated scopes, exact source preservation, session rollback and saved replay.");
    }
}
