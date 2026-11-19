using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldContourFacingTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Contour facing: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=WorldContourPopulationTests.Example();
        var recipe=input.Recipe with{Placement=input.Recipe.Placement! with{Bindings=input.Recipe.Placement.Bindings.Select(b=>b.Population?.Region?.Contour is not {} contour?b:b with{
            RotationDegrees=15,Population=b.Population with{Facing=b.DemandId.StartsWith("lowland")?MetadataPopulationFacing.Along:b.DemandId.StartsWith("highland")?MetadataPopulationFacing.Left:MetadataPopulationFacing.Against,
            Region=b.Population.Region with{Contour=contour with{Contours=b.DemandId.StartsWith("lowland")?
                [contour.Contours[0] with{Id="first",Count=1},contour.Contours[0] with{Id="remaining",Count=2,StartInset=contour.Contours[0].StartInset+contour.Contours[0].Spacing}]:
                contour.Contours.Select(c=>c with{Reverse=b.DemandId.StartsWith("woodland")}).ToArray()}}}}).ToArray()}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,input.Catalog);var plan=world.PlacementPlan!;
        Check(world.Nodes.Count==37&&world.AccessPlan!.Sites.Count==36&&world.RoutePlan!.Routes.Count==19,"complete source, physical members and routes retained");
        Check(plan.PopulationOrientations!.Sum(t=>t.NodeIds.Count)==8&&plan.PopulationContours!.Count==3,"all contour members have orientation and contour witnesses");
        var bindings=plan.Definition.Bindings.Concat((plan.Definition.NestedStages??[]).SelectMany(s=>s.Bindings)).ToDictionary(b=>b.NodeId);
        foreach(var trace in plan.PopulationOrientations!){
            var witness=plan.PopulationContours!.Single(c=>c.StageId==trace.StageId&&c.DemandId==trace.DemandId);
            var grammar=bindings[trace.NodeIds[0]].Population!.Region!.Contour!;var expected=new List<WorldPopulationPose>();
            for(int i=0;i<grammar.Contours.Count;i++){
                var c=grammar.Contours[i];expected.AddRange(WorldPopulationGeometry.PathPoses(new(witness.Contours[i].Path,c.Spacing,c.StartInset,c.EndInset),c.Count));
            }
            double offset=trace.Facing switch{MetadataPopulationFacing.Along=>0,MetadataPopulationFacing.Against=>180,MetadataPopulationFacing.Left=>90,_=>-90};
            for(int i=0;i<trace.NodeIds.Count;i++){
                var node=world.Nodes.Single(n=>n.Id==trace.NodeIds[i]);double heading=expected[i].TangentDegrees;
                Check(expected[i].Position.DistanceTo(new(node.Position![0],node.Position[1]))<1e-7,"heading comes from actual generated contour position");
                Check(Math.Abs(trace.TangentDegrees[i]-heading)<1e-9&&Math.Cos((trace.Rotations[i]-heading-offset-15)*Math.PI/180)>1-1e-12,"exact segment and declared rotation");
                Check(Math.Abs(bindings[trace.NodeIds[i]].RotationDegrees-trace.Rotations[i])<1e-9,"physical binding owns orientation");
            }
        }
        WorldMetadataGenerator.ValidateDocument(world);
        var first=plan.PopulationOrientations[0];var bad=first with{TangentDegrees=first.TangentDegrees.Select((v,i)=>i==0?v+1:v).ToArray()};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=plan with{PopulationOrientations=[bad,..plan.PopulationOrientations.Skip(1)]}}),"PLAN_MISMATCH");
        string path=Path.Combine(output,"world.json");await File.WriteAllTextAsync(path,Json(world));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(path))==Json(world),"saved full world replay");
        var rightRecipe=recipe with{Placement=recipe.Placement! with{Bindings=recipe.Placement.Bindings.Select(b=>b.DemandId.StartsWith("lowland")?
            b with{Population=b.Population! with{Facing=MetadataPopulationFacing.Right}}:b).ToArray()}};
        var rightWorld=generator.Generate(rightRecipe,input.Catalog);
        var right=rightWorld.PlacementPlan!.PopulationOrientations!.Single(t=>t.DemandId.StartsWith("lowland"));
        Check(right.Facing==MetadataPopulationFacing.Right&&right.Rotations.Zip(right.TangentDegrees).All(p=>Math.Cos((p.First-p.Second+90-15)*Math.PI/180)>1-1e-12),"right facing uses exact segment with source offset");
        Check(rightWorld.Nodes.Count==37&&rightWorld.AccessPlan!.Sites.Count==36&&rightWorld.RoutePlan!.Routes.Count==19,"right facing retains complete physical source and paths");
        WorldMetadataGenerator.ValidateDocument(rightWorld);
        await File.WriteAllTextAsync(Path.Combine(output,"right-world.json"),Json(rightWorld));
        var invalid=recipe with{Placement=recipe.Placement! with{Bindings=recipe.Placement.Bindings.Select(b=>b.Population?.Region?.Contour is null?b:b with{Population=b.Population with{Facing=MetadataPopulationFacing.Inside}}).ToArray()}};
        Reject(()=>generator.Generate(invalid,input.Catalog),"METADATA_POPULATION_FACING_INVALID");
        var bent=Bent();var bentWorld=generator.Generate(bent.Recipe,bent.Catalog);
        var bentTrace=bentWorld.PlacementPlan!.PopulationOrientations!.Single();var bentPath=bentWorld.PlacementPlan.PopulationContours!.Single().Contours.Single().Path;
        Check(bentPath.Points.Count==7&&bentTrace.TangentDegrees.SequenceEqual(new double[]{90,90,45,45}),"actual six-edge hill contour produces independent analytic headings");
        var bentPositions=bentWorld.PlacementPlan.PopulationContours!.Single().Contours.Single().Positions;
        var expectedPositions=new[]{new WorldPoint(1,2),new WorldPoint(1,4),new WorldPoint(1+Math.Sqrt(.5),5+Math.Sqrt(.5)),new WorldPoint(1+3*Math.Sqrt(.5),5+3*Math.Sqrt(.5))};
        Check(bentPositions.Zip(expectedPositions).All(p=>p.First.DistanceTo(p.Second)<1e-9),"actual bent-contour positions match analytic terrain geometry");
        Check(bentTrace.NodeIds.Count==4&&bentWorld.AccessPlan!.Sites.Count==4,"all four bent-contour physical members retained");
        WorldMetadataGenerator.ValidateDocument(bentWorld);string bentFile=Path.Combine(output,"bent-world.json");
        await File.WriteAllTextAsync(bentFile,Json(bentWorld));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(bentFile))==Json(bentWorld),"bent terrain world replays");
        Console.WriteLine("PASS: eight contour-oriented physical members, four facing modes across two worlds, multiple/reversed contours, actual bent terrain, source paths, saved replay and tamper rejection.");
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Bent()
    {
        var mini=WorldTerrainDemandTests.Mini();var domain=new WorldPolygon([new(0,0),new(10,0),new(10,10),new(0,10)]);
        var height=new WorldHeightField(1,"hill","ground","Four-facet terrain","test:hill","meters",new(0,0),5,5,3,3,[0,0,0,0,5,0,0,0,0]);
        var source=mini.Recipe.PopulationSource!.Recipe;
        var surface=new WorldSurfaceDefinition(1,"bent-ground","meters",domain,[],[],HeightField:height);
        var fields=new WorldEnvironmentDefinition(1,"bent-fields",[new("height","Terrain height","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        source=source with{Surface=surface,Environment=fields,Regions=source.Regions! with{Boundary=domain,Seeds=[source.Regions.Seeds[0] with{Position=new(5,5)}]},Origin=[0,0]};
        var catalog=mini.Catalog with{Archetypes=mini.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=a.Parts.Select(p=>p.Id=="features"?p with{Count=4}:p).ToArray()}:a).ToArray()};
        var placement=mini.Recipe.Placement! with{Request=mini.Recipe.Placement.Request with{Boundary=domain},Bindings=[mini.Recipe.Placement.Bindings[0] with{
            Population=new("Follow an actual bent contour","test:bent",Region:new("home",Contour:new("height",[new("hill",1,new(5,5),20,4,2,1,1)])),Facing:MetadataPopulationFacing.Along)}]};
        return(source with{PopulationSource=new(source,mini.Recipe.PopulationSource.Catalog),Placement=placement,TerrainDemands=null},catalog);
    }

}
