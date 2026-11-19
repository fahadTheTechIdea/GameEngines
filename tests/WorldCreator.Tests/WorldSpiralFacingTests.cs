using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSpiralFacingTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Spiral facing: "+message);}
    private static double Norm(double angle)=>((angle+180)%360+360)%360-180;
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        foreach(var settings in new[]{new MetadataSpiralLayout.Settings([0,0],10,2,90),new MetadataSpiralLayout.Settings([0,0],5,-1,-90),
            new MetadataSpiralLayout.Settings([0,0],0,2,45,1,0),new MetadataSpiralLayout.Settings([0,0],10,0,90),
            new MetadataSpiralLayout.Settings([0,0],10,0,-90,1,0),new MetadataSpiralLayout.Settings([0,0],10,0,90,AxisRatio:.5,OrientationDegrees:30),
            new MetadataSpiralLayout.Settings([0,0],10,2,-60,1,0,AxisRatio:1.8,OrientationDegrees:-42)})
        {
            var poses=MetadataSpiralLayout.PlanPoses2D(settings,4,CancellationToken.None);
            for(int i=0;i<poses.Count;i++)
            {
                double[] At(double t){double radius=settings.StartRadius+t*settings.RadiusStep,angle=(settings.StartDegrees+t*settings.AngleStepDegrees)*Math.PI/180;
                    double u=radius*Math.Cos(angle),v=radius*Math.Sin(angle)*(settings.AxisRatio??1),o=(settings.OrientationDegrees??0)*Math.PI/180;
                    var point=new double[2];point[settings.AxisU]=u*Math.Cos(o)-v*Math.Sin(o);point[settings.AxisV]=u*Math.Sin(o)+v*Math.Cos(o);return point;}
                var a=At(i-1e-5);var b=At(i+1e-5);double expected=Math.Atan2(b[1]-a[1],b[0]-a[0])*180/Math.PI;
                Check(Math.Abs(Norm(expected-poses[i].TangentDegrees))<1e-5,"independent numerical tangent");
            }
        }
        var source=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>("examples/metadata/spiral-world-program/recipe.json");
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>("examples/metadata/spiral-world-program/catalog.json");
        var generator=new WorldMetadataGenerator();var worlds=new Dictionary<MetadataPopulationFacing,WorldMetadataDocument>();var recipes=new Dictionary<MetadataPopulationFacing,WorldMetadataRecipe>();
        foreach(var mode in new[]{MetadataPopulationFacing.Along,MetadataPopulationFacing.Against,MetadataPopulationFacing.Left,MetadataPopulationFacing.Right})
        {
            var placement=source.Placement!;var binding=placement.Bindings[0];
            var recipe=source with{Placement=placement with{Bindings=[binding with{Population=binding.Population! with{Facing=mode}}]}};
            var world=generator.Generate(recipe,catalog);var trace=world.PlacementPlan!.PopulationOrientations!.Single();
            Check(world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
                world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"all solid sources and both journeys retained");
            Check(trace.Facing==mode&&trace.NodeIds.Count==4,"complete orientation witness retained");
            foreach(var physical in world.AccessPlan!.Sites)
            {
                var vertices=physical.Footprint.Vertices;
                for(int i=0;i<vertices.Count;i++)Check(Math.Abs(vertices[i].DistanceTo(vertices[(i+1)%vertices.Count])-2)<1e-7,"source edge lengths preserved after rotation");
            }
            worlds.Add(mode,world);recipes.Add(mode,recipe);
            await generator.PublishAsync(recipe,catalog,world,Path.Combine(output,mode.ToString()),null,CancellationToken.None);
            Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,mode.ToString())))==Json(world),"published world source replays");
            await File.WriteAllTextAsync(Path.Combine(output,mode+"-recipe.json"),Json(recipe));
        }
        foreach(var (name,spiral) in new[]{("Swapped",new MetadataSpiralLayout.Settings([50,50],10,2,90,1,0)),
            ("Contracting",new MetadataSpiralLayout.Settings([50,50],10,-2,-90)),("Ring",new MetadataSpiralLayout.Settings([50,50],10,0,90)),
            ("Ellipse",new MetadataSpiralLayout.Settings([50,50],10,0,90,AxisRatio:.5,OrientationDegrees:30))})
        {
            var placement=source.Placement!;var binding=placement.Bindings[0];
            var recipe=source with{Placement=placement with{Bindings=[binding with{Population=binding.Population! with{Spiral=spiral,Facing=MetadataPopulationFacing.Along}}]}};
            var world=generator.Generate(recipe,catalog);
            Check(world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&world.PlacementPlan!.PopulationOrientations!.Single().NodeIds.Count==4&&
                world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
                "swapped/contracting physical sources and paths retained");
            await generator.PublishAsync(recipe,catalog,world,Path.Combine(output,name),null,CancellationToken.None);
        }
        var root=catalog.Archetypes.Single(a=>a.Id=="root");const string parentId="spiral-world/container-0";
        var parentShape=new WorldPolygon([new(-25,-25),new(25,-25),new(25,25),new(-25,25)]);
        var container=new WorldMetadataArchetype("container","container","Declared nested spiral scope",new Dictionary<string,JsonElement>(),root.Parts,[],[],
            PhysicalFootprint:new("meters","Parent reservation","spiral:parent",parentShape),Access:new(MetadataOccupancyKind.Reservation,[]));
        var nestedCatalog=catalog with{Archetypes=[..catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[new("container","container",1,"Required parent")]}:a),container]};
        var template=source.Placement!;var demand=template.Request.Demands[0];
        var parentRequest=template.Request with{Id="parent",Demands=[demand with{Id="container"}]};
        var nestedRequest=template.Request with{Id="nested",Boundary=null};
        var nestedBinding=template.Bindings[0] with{NodeId=parentId,Population=template.Bindings[0].Population! with{
            Spiral=new([0,0],10,2,90),Facing=MetadataPopulationFacing.Along}};
        var parentBinding=new MetadataPlacementBinding("container",parentId,true,Population:new("Pin parent to authored world position","spiral:parent",
            Sockets:[new(parentId,new(50,50))]));
        var nestedRecipe=source with{Placement=new(parentRequest,[parentBinding],[new("nested",parentId,nestedRequest,[nestedBinding])]),
            AdditionalConnections=[new("journey","walk",parentId+"/sites-0",parentId+"/sites-1",true)]};
        var nestedWorld=generator.Generate(nestedRecipe,nestedCatalog);
        Check(nestedWorld.Nodes.Count==6&&nestedWorld.AccessPlan!.Sites.Count==5&&nestedWorld.PlacementPlan!.PopulationOrientations!.Single().NodeIds.Count==4,
            "nested parent, physical children and orientation witnesses retained");
        var nestedFirst=nestedWorld.Nodes.Single(n=>n.Id==parentId+"/sites-0");
        Check(Math.Abs(nestedFirst.Position![0]-60)<1e-7&&Math.Abs(nestedFirst.Position[1]-50)<1e-7,"local spiral translated by actual parent position");
        await generator.PublishAsync(nestedRecipe,nestedCatalog,nestedWorld,Path.Combine(output,"Nested"),null,CancellationToken.None);
        var selector=new MetadataPopulationSelector(generator);
        var badPopulation=nestedBinding.Population! with{Spiral=new([22,0],10,2,90)};
        var badNested=nestedRecipe with{Placement=nestedRecipe.Placement! with{NestedStages=[new("nested",parentId,nestedRequest,[nestedBinding with{Population=badPopulation}])]}};
        var nestedSelected=selector.Select(badNested,nestedCatalog,new(MetadataPopulationTargetKind.NestedStage,"site","nested"),[new("inside",nestedBinding.Population!)]);
        Check(nestedSelected.Attempts.Count==2&&!nestedSelected.Attempts[0].Accepted&&nestedSelected.Attempts[1].Accepted&&
            Json(nestedSelected.Recipe)==Json(nestedRecipe)&&Json(nestedSelected.World)==Json(nestedWorld),"nested alternative retains complete original world");
        await generator.PublishAsync(nestedSelected.Recipe,nestedCatalog,nestedSelected.World,Path.Combine(output,"NestedSelected"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"NestedSelected")))==Json(nestedWorld),"selected nested world replays");
        try{selector.Select(badNested,nestedCatalog,new(MetadataPopulationTargetKind.NestedStage,"site","missing"),[new("inside",nestedBinding.Population!)]);throw new Exception("Unknown stage accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_SELECTION_BINDING_INVALID","unknown nested stage rejected");}
        var ambiguous=badNested with{Placement=badNested.Placement! with{NestedStages=[badNested.Placement.NestedStages![0],badNested.Placement.NestedStages[0]]}};
        try{selector.Select(ambiguous,nestedCatalog,new(MetadataPopulationTargetKind.NestedStage,"site","nested"),[new("inside",nestedBinding.Population!)]);throw new Exception("Ambiguous stage accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_SELECTION_BINDING_INVALID","ambiguous stage rejected before generation");}
        var noFacing=nestedBinding.Population! with{Facing=null};
        var nestedUnfaced=nestedRecipe with{Placement=nestedRecipe.Placement! with{NestedStages=[new("nested",parentId,nestedRequest,[nestedBinding with{Population=noFacing}])]}};
        var nestedUnfacedWorld=generator.Generate(nestedUnfaced,nestedCatalog);
        Check(nestedUnfacedWorld.Nodes.Count==6&&nestedUnfacedWorld.AccessPlan!.Sites.Count==5,"unfaced nested physical content retained");
        await generator.PublishAsync(nestedUnfaced,nestedCatalog,nestedUnfacedWorld,Path.Combine(output,"NestedUnfaced"),null,CancellationToken.None);
        foreach(var (name,population) in new[]{("Program",nestedBinding.Population!),("ProgramUnfaced",noFacing)})
        {
            var program=new MetadataNestedProgram("spiral-program","Reusable spiral children","spiral:program","spiral-world","container",1,1,
                nestedRequest,[new("site","sites",true,Population:population)]);
            var programRecipe=nestedRecipe with{Placement=nestedRecipe.Placement! with{NestedStages=null,Programs=[program]}};
            var programWorld=generator.Generate(programRecipe,nestedCatalog);
            Check(programWorld.Nodes.Count==6&&programWorld.AccessPlan!.Sites.Count==5&&programWorld.PlacementPlan!.ProgramInstances!.Count==1,
                "reusable program retains parent and all physical children");
            await generator.PublishAsync(programRecipe,nestedCatalog,programWorld,Path.Combine(output,name),null,CancellationToken.None);
        }
        var badProgram=new MetadataNestedProgram("spiral-program","Reusable spiral children","spiral:program","spiral-world","container",1,1,
            nestedRequest,[new("site","sites",true,Population:badPopulation)]);
        var badProgramRecipe=nestedRecipe with{Placement=nestedRecipe.Placement! with{NestedStages=null,Programs=[badProgram]}};
        var programSelected=selector.Select(badProgramRecipe,nestedCatalog,new(MetadataPopulationTargetKind.Program,"site","spiral-program"),[new("inside",nestedBinding.Population!)]);
        Check(programSelected.Attempts.Count==2&&!programSelected.Attempts[0].Accepted&&programSelected.Attempts[1].Accepted&&
            programSelected.World.Nodes.Count==6&&programSelected.World.AccessPlan!.Sites.Count==5&&programSelected.World.PlacementPlan!.ProgramInstances!.Count==1&&
            programSelected.World.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&programSelected.World.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "reusable program alternative retains complete physical content and both routes");
        await generator.PublishAsync(programSelected.Recipe,nestedCatalog,programSelected.World,Path.Combine(output,"ProgramSelected"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"ProgramSelected")))==Json(programSelected.World),"selected reusable program replays");
        var wide=new WorldPolygon([new(0,0),new(200,0),new(200,100),new(0,100)]);
        var twoCatalog=nestedCatalog with{Archetypes=nestedCatalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[new("container","container",2,"Both required parents")]}:a).ToArray()};
        var bothParents=parentBinding with{NodeId="spiral-world",PartId="container",Population=parentBinding.Population! with{
            Sockets=[new(parentId,new(40,50)),new("spiral-world/container-1",new(150,50))]}};
        var bothProgram=badProgram with{MinimumParents=2,MaximumParents=2};
        var twoRecipe=badProgramRecipe with{Surface=source.Surface! with{Boundary=wide,Patches=[source.Surface.Patches![0] with{Boundary=wide}]},
            Placement=badProgramRecipe.Placement! with{Request=parentRequest with{Boundary=wide},Bindings=[bothParents],Programs=[bothProgram]}};
        var twoSelected=selector.Select(twoRecipe,twoCatalog,new(MetadataPopulationTargetKind.Program,"site","spiral-program"),[new("inside",nestedBinding.Population!)]);
        Check(twoSelected.Attempts.Count==2&&!twoSelected.Attempts[0].Accepted&&twoSelected.Attempts[1].Accepted&&
            twoSelected.World.Nodes.Count==11&&twoSelected.World.AccessPlan!.Sites.Count==10&&twoSelected.World.PlacementPlan!.ProgramInstances!.Count==2&&
            twoSelected.World.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&twoSelected.World.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "one program choice retains both parents, all children and required routes");
        await generator.PublishAsync(twoSelected.Recipe,twoCatalog,twoSelected.World,Path.Combine(output,"ProgramTwoParents"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"ProgramTwoParents")))==Json(twoSelected.World),"two-parent selected program replays");
        var programTarget=new MetadataPopulationTarget(MetadataPopulationTargetKind.Program,"site","spiral-program");
        var localChoices=MetadataPopulationTranslation.Create(twoRecipe,programTarget,new("Move inside each parent","test:local-shift",[new(-22,0)],1));
        var localSelected=selector.Select(twoRecipe,twoCatalog,programTarget,localChoices);
        Check(Json(localSelected.Recipe)==Json(twoSelected.Recipe)&&Json(localSelected.World)==Json(twoSelected.World),
            "rule translates program in each parent's local frame without changing content");
        var integratedProgram=twoRecipe with{Placement=twoRecipe.Placement! with{PopulationAdaptation=new(programTarget,new("Move inside each parent","test:local-shift",[new(-22,0)],1))}};
        var integratedProgramWorld=generator.Generate(integratedProgram,twoCatalog);
        Check(integratedProgramWorld.PlacementPlan!.PopulationAdaptation!.Attempts.Count==2&&
            Json(integratedProgramWorld.Nodes)==Json(twoSelected.World.Nodes)&&integratedProgramWorld.AccessPlan!.Sites.Count==10&&
            integratedProgramWorld.PlacementPlan.ProgramInstances!.Count==2&&integratedProgramWorld.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            integratedProgramWorld.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"ordinary generation adapts both program parents and routes");
        await generator.PublishAsync(integratedProgram,twoCatalog,integratedProgramWorld,Path.Combine(output,"ProgramIntegrated"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"ProgramIntegrated")))==Json(integratedProgramWorld),"integrated reusable program replays");
        var rotatedProgram=twoRecipe with{Placement=twoRecipe.Placement! with{PopulationAdaptation=new(programTarget,null,
            Rotation:new("Rotate inside each parent","test:local-rotation",new(10,0),[180]))}};
        var rotatedProgramWorld=generator.Generate(rotatedProgram,twoCatalog);
        Check(rotatedProgramWorld.PlacementPlan!.PopulationAdaptation!.Attempts.Count==2&&rotatedProgramWorld.Nodes.Count==11&&
            rotatedProgramWorld.AccessPlan!.Sites.Count==10&&rotatedProgramWorld.PlacementPlan.ProgramInstances!.Count==2&&
            rotatedProgramWorld.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&rotatedProgramWorld.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "local rotation retains both parents, all sources and routes");
        NearPosition(rotatedProgramWorld.Nodes.Single(n=>n.Id==parentId+"/sites-0").Position!,28,50,"first rotated parent");
        NearPosition(rotatedProgramWorld.Nodes.Single(n=>n.Id=="spiral-world/container-1/sites-0").Position!,138,50,"second rotated parent");
        await generator.PublishAsync(rotatedProgram,twoCatalog,rotatedProgramWorld,Path.Combine(output,"ProgramRotated"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"ProgramRotated")))==Json(rotatedProgramWorld),"rotated program replays");
        var rigidProgram=twoRecipe with{Placement=twoRecipe.Placement! with{PopulationAdaptation=new(programTarget,null,Rigid:new(
            "Fit inside each parent","test:local-rigid",new("Turn","test:turn",new(0,0),[180]),new("Move","test:move",[new(20,0)],1)))}};
        var rigidProgramWorld=generator.Generate(rigidProgram,twoCatalog);
        Check(rigidProgramWorld.PlacementPlan!.PopulationAdaptation!.Attempts.Count==4&&
            rigidProgramWorld.PlacementPlan.PopulationAdaptation.Attempts[^1].ChoiceId=="rigid-r1-t1"&&
            rigidProgramWorld.Nodes.Count==rotatedProgramWorld.Nodes.Count&&rigidProgramWorld.AccessPlan!.Sites.Count==10&&
            rigidProgramWorld.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&rigidProgramWorld.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "combined rotation and translation fit both local parent programs and retain journeys");
        foreach(var node in rigidProgramWorld.Nodes)
        {var expected=rotatedProgramWorld.Nodes.Single(n=>n.Id==node.Id);if(node.Position is not null&&expected.Position is not null)NearPosition(node.Position,expected.Position[0],expected.Position[1],"equivalent local rigid position");else Check(node.Position is null&&expected.Position is null,"equivalent absent position");}
        await generator.PublishAsync(rigidProgram,twoCatalog,rigidProgramWorld,Path.Combine(output,"ProgramRigid"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"ProgramRigid")))==Json(rigidProgramWorld),"combined program source replays");
        var manyCatalog=nestedCatalog with{Archetypes=nestedCatalog.Archetypes.Select(a=>a.Id!="container"?a:a with{
            Parts=a.Parts.Select(p=>p.Id=="sites"?p with{Count=12}:p).ToArray()}).ToArray()};
        var manyPopulation=noFacing with{Spiral=new([0,0],2,1,135)};
        var manyProgram=new MetadataNestedProgram("spiral-program","Reusable larger spiral","spiral:program","spiral-world","container",1,1,
            nestedRequest,[new("site","sites",true,Population:manyPopulation)]);
        var manyRecipe=nestedRecipe with{Placement=nestedRecipe.Placement! with{NestedStages=null,Programs=[manyProgram]}};
        var manyWorld=generator.Generate(manyRecipe,manyCatalog);
        Check(manyWorld.Nodes.Count==14&&manyWorld.AccessPlan!.Sites.Count==13,"all twelve numerically ordered physical children retained");
        var tenth=manyWorld.Nodes.Single(n=>n.Id==parentId+"/sites-10");
        Check(Math.Abs(tenth.Position![0]-50)<1e-7&&Math.Abs(tenth.Position[1]-38)<1e-7,"member ten uses numeric index ten");
        await generator.PublishAsync(manyRecipe,manyCatalog,manyWorld,Path.Combine(output,"ProgramTwelve"),null,CancellationToken.None);
        var singleCatalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{
            Parts=a.Parts.Select(p=>p.Id=="sites"?p with{Count=1}:p).ToArray()}).ToArray()};
        var singleBinding=source.Placement!.Bindings[0] with{NodeId="spiral-world/sites-0",PartId=null};
        var singleRecipe=source with{AdditionalConnections=[],RouteRules=null,Placement=source.Placement with{Bindings=[singleBinding]}};
        var singleWorld=generator.Generate(singleRecipe,singleCatalog);
        Check(singleWorld.Nodes.Count==2&&singleWorld.AccessPlan!.Sites.Count==1,"single direct spiral binding retained");
        await generator.PublishAsync(singleRecipe,singleCatalog,singleWorld,Path.Combine(output,"Single"),null,CancellationToken.None);
        var session=new MetadataGenerationSession(generator,recipes[MetadataPopulationFacing.Along],catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("facing","Change declared spiral facing","spiral:facing",before.World.RecipeSha256,before.World.CatalogSha256,recipes[MetadataPopulationFacing.Right],catalog),Path.Combine(output,"edited"));
        Check(Json(after.World)==Json(worlds[MetadataPopulationFacing.Right]),"edit equals clean physical generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(after),"facing edit replays");
        var invalid=recipes[MetadataPopulationFacing.Right];var binding0=invalid.Placement!.Bindings[0];
        invalid=invalid with{Placement=invalid.Placement with{Bindings=[binding0 with{Population=binding0.Population! with{Facing=MetadataPopulationFacing.Inside}}]}};
        try{session.Apply(new("invalid","Reject boundary-only facing","spiral:invalid",after.World.RecipeSha256,after.World.CatalogSha256,invalid,catalog));throw new Exception("Boundary facing accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed edit retains accepted world");
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(catalog));
        Console.WriteLine("PASS: independent spiral derivatives, four facing modes, four full physical worlds and routes, source replay and failed-edit preservation.");
    }
    private static void NearPosition(IReadOnlyList<double> actual,double x,double y,string message)
        =>Check(Math.Abs(actual[0]-x)<1e-7&&Math.Abs(actual[1]-y)<1e-7,message);
}
