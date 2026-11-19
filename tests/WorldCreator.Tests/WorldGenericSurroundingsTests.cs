using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldGenericSurroundingsTests
{
    private static WorldPolygon Box(double x0,double y0,double x1,double y1)=>new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
    private static double Area(WorldPolygon p)=>Math.Abs(p.Vertices.Select((a,i)=>{var b=p.Vertices[(i+1)%p.Vertices.Count];return a.X*b.Y-a.Y*b.X;}).Sum())/2;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Generic surroundings: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurroundingsTests.Example();
        var source=new MetadataCatalogFootprint("meters","Game-authored work module","catalog:module",Box(-3,-6,5,4),Box(-5,-9,6,5),
            SiteRules:[new("repair-yard",SiteFeatureKind.LoadingYard,SiteBand.Rear,3,.25,MaximumDepthMeters:3.5),
                       new("arrival",SiteFeatureKind.Forecourt,SiteBand.Front,3,.25),
                       new("buffer",SiteFeatureKind.GreenBuffer,SiteBand.Left,1,.25),
                       new("optional-plant",SiteFeatureKind.CoolingPlant,SiteBand.Right,5,Required:false)]);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Structure=null,PhysicalFootprint=source,
            Access=new(MetadataOccupancyKind.Solid,[new("front","Declared module entrance","catalog:module-entry",new(1,-6),new(1,-10),1.5)])}:a).ToArray()};
        var recipe=e.Recipe with{Id="generic-surroundings",Placement=e.Recipe.Placement! with{Bindings=[e.Recipe.Placement.Bindings[0] with{NodeId="generic-surroundings/sites-0"}]},
            RemainingScopes=["Generic rectangular source surroundings and access only; complete game design, art and runtime remain separate."]};
        return(recipe,catalog);
    }
    private static WorldMetadataCatalog Source(WorldMetadataCatalog catalog,Func<MetadataCatalogFootprint,MetadataCatalogFootprint> change)=>
        catalog with{Archetypes=catalog.Archetypes.Select(a=>a.PhysicalFootprint is {} p?a with{PhysicalFootprint=change(p)}:a).ToArray()};
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Evidence exists");Directory.CreateDirectory(output);
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var plan=world.PlacementPlan!;var source=plan.Footprints.Single();
        Check(world.StructureDefinitions.Count==0&&world.Nodes.Count==2,"generic program needs no registered structure or city profile");
        Check(plan.SiteFeatures!.Count==4&&!plan.SiteFeatures.Any(f=>f.Kind==SiteFeatureKind.CoolingPlant),"all required zones and explicit optional omission");
        Check(Math.Abs(Area(source.Polygon)-80)<1e-8&&Math.Abs(Area(source.Reservation!)-216)<1e-8,"source dimensions and complete asymmetric directional reservation");
        var expectedAreas=new Dictionary<string,double>{{"access",6},{"repair-yard",34.5},{"arrival",40.25},{"buffer",14.25}};
        foreach(var feature in source.SiteFeatures!)
        {
            string key=expectedAreas.Keys.Single(k=>feature.Id.EndsWith("-"+k,StringComparison.Ordinal));
            Check(Math.Abs(Area(feature.Boundary)-expectedAreas[key])<1e-8,"independent fixed-source area: "+key);
            Check(feature.SourceRuleId=="catalog:module:"+key&&feature.EntrancePortId=="front","source and entrance provenance");
            var positioned=plan.SiteFeatures.Single(f=>f.Id==feature.Id);
            for(int i=0;i<feature.Boundary.Vertices.Count;i++)
                Check(positioned.Boundary.Vertices[i].DistanceTo(feature.Boundary.Vertices[i].Offset(50,50))<1e-7,"same rotated local and world geometry");
        }
        var binding=e.Recipe.Placement!.Bindings.Single();
        var widerSource=Source(e.Catalog,p=>p with{Reservation=Box(-7,-12,8,9)});
        widerSource=widerSource with{Archetypes=widerSource.Archetypes.Select(a=>a.Access is {} access?a with{Access=access with{Ports=[access.Ports[0] with{ApproachPosition=new(1,-12)}]}}:a).ToArray()};
        var widerWorld=generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{Bindings=[binding with{RotationDegrees=0}]}},widerSource);
        Check(Json(widerWorld.PlacementPlan!.Footprints.Single().Reservation)==Json(Box(-7,-12,8,9)),"larger asymmetric authored reservation is retained exactly, never shrunk to requested bands");
        var splitSource=Source(e.Catalog,p=>p with{SiteRules=p.SiteRules!.Select(r=>r.Id=="arrival"?r with{Kind=SiteFeatureKind.Garden}:r).ToArray()});
        var splitWorld=generator.Generate(e.Recipe,splitSource);
        Check(splitWorld.PlacementPlan!.SiteFeatures!.Count==5&&splitWorld.PlacementPlan.SiteFeatures.Count(f=>f.Kind==SiteFeatureKind.Garden)==2,"shared front-band splitting reserves the selected entrance");
        var southSource=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Access is {} access?a with{Access=access with{Ports=[access.Ports[0] with{BoundaryPosition=new(1,4),ApproachPosition=new(1,8)}]}}:a).ToArray()};
        var southWorld=generator.Generate(e.Recipe,southSource);
        Check(southWorld.PlacementPlan!.SiteFeatures!.Count==4,"opposite source entrance uses the same front/rear grammar");
        WorldMetadataCatalog Entrance(double x,bool north)=>e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Access is {} access?a with{
            Access=access with{Ports=[access.Ports[0] with{BoundaryPosition=new(x,north?-6:4),ApproachPosition=new(x,north?-10:8)}]}}:a).ToArray()};
        WorldMetadataRecipe Facing(double angle)=>e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=[binding with{RotationDegrees=angle}]}};
        WorldMetadataDocument? offCenter=null;
        foreach(bool north in new[]{true,false})foreach(double x in new[]{-1d,3.5})foreach(double rotation in new[]{0d,30d,90d})
        {
            var actual=generator.Generate(Facing(rotation),Entrance(x,north));var walkway=actual.PlacementPlan!.SiteFeatures!.Single(f=>f.Kind==SiteFeatureKind.AccessWalkway);
            var port=actual.AccessPlan!.Sites.Single().Ports.Single();
            Check(Math.Abs(Area(walkway.Boundary)-6)<1e-7,"off-center door retains the full-width source walkway");
            Check(walkway.Boundary.ContainsPoint(port.BoundaryPosition)&&walkway.Boundary.ContainsPoint(port.ApproachPosition),
                "the placed walkway connects the selected door and approach after rotation");
            Check(actual.PlacementPlan.SiteFeatures!.Count==4,"every required surrounding remains in the rotated placement");
            if(north&&x==-1&&rotation==30)offCenter=actual;
        }
        WorldMetadataCatalog SideEntrance(double y,bool east)=>e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Access is {} access?a with{
            Access=access with{Ports=[access.Ports[0] with{BoundaryPosition=new(east?5:-3,y),ApproachPosition=new(east?9:-7,y)}]}}:a).ToArray()};
        WorldMetadataDocument? sideWorld=null;
        foreach(bool east in new[]{true,false})foreach(double y in new[]{-3d,2d})foreach(double rotation in new[]{0d,30d,90d})
        {
            var actual=generator.Generate(Facing(rotation),SideEntrance(y,east));var walkway=actual.PlacementPlan!.SiteFeatures!.Single(f=>f.Kind==SiteFeatureKind.AccessWalkway);
            var port=actual.AccessPlan!.Sites.Single().Ports.Single();
            Check(Math.Abs(Area(walkway.Boundary)-6)<1e-7,"side entrance retains its complete walkway");
            Check(walkway.Boundary.ContainsPoint(port.BoundaryPosition)&&walkway.Boundary.ContainsPoint(port.ApproachPosition),
                "side walkway joins the exact source entrance and approach after rotation");
            Check(actual.PlacementPlan.SiteFeatures!.Count==4,"side entrance retains all required surrounding zones");
            if(east&&y==-3&&rotation==30)sideWorld=actual;
        }
        Reject(()=>generator.Generate(Facing(0),SideEntrance(3.5,true)),"METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        var misaligned=SideEntrance(-3,true) with{Archetypes=SideEntrance(-3,true).Archetypes.Select(a=>a.Access is {} access?a with{
            Access=access with{Ports=[access.Ports[0] with{ApproachPosition=new(9,-2)}]}}:a).ToArray()};
        Reject(()=>generator.Generate(Facing(0),misaligned),"METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        var sideSplit=splitSource with{Archetypes=splitSource.Archetypes.Select(a=>a.Access is {} access?a with{
            Access=access with{Ports=[access.Ports[0] with{BoundaryPosition=new(5,-3),ApproachPosition=new(9,-3)}]}}:a).ToArray()};
        var sideSplitWorld=generator.Generate(Facing(0),sideSplit);
        var sideBands=sideSplitWorld.PlacementPlan!.SiteFeatures!.Where(f=>f.Kind==SiteFeatureKind.Garden).OrderBy(f=>f.Boundary.Vertices.Min(p=>p.Y)).ToArray();
        Check(sideBands.Length==2&&sideBands[0].Boundary.Vertices.Max(p=>p.Y)<=46.25+1e-8&&sideBands[1].Boundary.Vertices.Min(p=>p.Y)>=47.75-1e-8,
            "side front bands split around the selected entrance in the original source frame");
        var asymmetric=Facing(0) with{Placement=Facing(0).Placement! with{Bindings=[binding with{RotationDegrees=0,
            SiteLayout=binding.SiteLayout! with{LeftDepthMeters=1,RightDepthMeters=3}}]}};
        var asymWorld=generator.Generate(asymmetric,SideEntrance(-3,true));var asym=asymWorld.PlacementPlan!.Footprints.Single().Reservation!;
        Check(asym.Vertices.Min(p=>p.X)==-7&&asym.Vertices.Max(p=>p.X)==9&&asym.Vertices.Min(p=>p.Y)==-9&&asym.Vertices.Max(p=>p.Y)==7,
            "asymmetric front, rear, left and right depths follow the side entrance frame");
        var offsetSplit=generator.Generate(Facing(0),Entrance(-1,true) with{Archetypes=splitSource.Archetypes.Select(a=>a.Access is {} access?a with{
            Access=access with{Ports=[access.Ports[0] with{BoundaryPosition=new(-1,-6),ApproachPosition=new(-1,-10)}]}}:a).ToArray()});
        var frontParts=offsetSplit.PlacementPlan!.SiteFeatures!.Where(f=>f.Kind==SiteFeatureKind.Garden).OrderBy(f=>f.Boundary.Vertices.Min(p=>p.X)).ToArray();
        Check(frontParts.Length==2&&frontParts[0].Boundary.Vertices.Max(p=>p.X)<=48.25+1e-8&&frontParts[1].Boundary.Vertices.Min(p=>p.X)>=49.75-1e-8,
            "front bands split around the selected offset entrance, rather than the building center");
        Reject(()=>generator.Generate(Facing(0),Entrance(4.5,true)),"METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        WorldMetadataRecipe Layout(MetadataSiteLayout layout)=>e.Recipe with{Placement=e.Recipe.Placement with{Bindings=[binding with{SiteLayout=layout}]}};
        Reject(()=>generator.Generate(Layout(binding.SiteLayout! with{RearDepthMeters=1}),e.Catalog),"METADATA_SITE_RULES_UNSATISFIED");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{Bindings=[binding with{SiteLayout=null}]}},e.Catalog),"METADATA_SITE_LAYOUT_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe,Source(e.Catalog,p=>p with{SiteRules=null})),"METADATA_SITE_RULES_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe,Source(e.Catalog,p=>p with{SiteRules=[p.SiteRules![0] with{MinimumDepthMeters=double.NaN}]})),"Invalid building site grammar");
        Reject(()=>generator.Generate(e.Recipe,Source(e.Catalog,p=>p with{Polygon=new([new(-3,-6),new(5,-6),new(1,4)])})),"METADATA_SITE_GEOMETRY_UNSUPPORTED");
        Reject(()=>MetadataFootprintResolver.Snapshot(e.Catalog.Archetypes.Single(a=>a.Id=="site").PhysicalFootprint! with{Unit="cells"},"cells"),"METADATA_SITE_RULES_INVALID");
        var narrow=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Access is {} access?a with{Access=access with{Ports=[access.Ports[0] with{BoundaryPosition=new(0,-6)}]}}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,narrow),"METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        double angle=Math.PI/6;var hazard=new WorldPoint(50+Math.Cos(angle)-6*Math.Sin(angle),50+Math.Sin(angle)+6*Math.Cos(angle));
        Reject(()=>generator.Generate(e.Recipe with{Surface=e.Recipe.Surface! with{Exclusions=[new("zone-hazard","Required zone must avoid this terrain","test",Box(hazard.X-.05,hazard.Y-.05,hazard.X+.05,hazard.Y+.05))]}},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(world with{PlacementPlan=plan with{SiteFeatures=plan.SiteFeatures.Skip(1).ToArray()}}),"METADATA_PLACEMENT_PLAN_MISMATCH");
        var offsetCatalog=SideEntrance(-3,true);
        var repeated=offsetCatalog with{Archetypes=offsetCatalog.Archetypes.Select(a=>a.Id==e.Recipe.RootArchetypeId?a with{Parts=[a.Parts[0] with{Count=2}],ChildRequirements=[new("site",2,2)]}:a).ToArray()};
        var repeatedRecipe=e.Recipe with{Placement=e.Recipe.Placement with{Request=e.Recipe.Placement.Request with{Anchors=[],Relations=[]},Bindings=[binding with{NodeId=e.Recipe.Id,PartId="sites"}]}};
        var repeatedWorld=generator.Generate(repeatedRecipe,repeated);Check(repeatedWorld.Nodes.Count==3&&repeatedWorld.PlacementPlan!.SiteFeatures!.Count==8,"every template instance retains its complete required surroundings");
        var parent=new WorldMetadataArchetype("compound","compound","Required compound containing modules",new Dictionary<string,JsonElement>(),[new("sites","site",2,"Required complete modules")],[],[new("site",2,2)],LayerId:"intent",
            PhysicalFootprint:new("meters","Declared compound reservation","catalog:compound",Box(-25,-25,25,25)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var nestedCatalog=offsetCatalog with{Archetypes=offsetCatalog.Archetypes.Select(a=>a.Id==e.Recipe.RootArchetypeId?a with{Parts=[new("compounds","compound",1,"Required compound")],ChildRequirements=[new("compound",1,1)]}:a).Append(parent).ToArray()};
        string parentId=e.Recipe.Id+"/compounds-0";
        var childRequest=repeatedRecipe.Placement!.Request with{Id="compound-members",Boundary=null};
        var nestedRecipe=e.Recipe with{Placement=e.Recipe.Placement with{
            Bindings=[binding with{NodeId=parentId,SiteLayout=null,RotationDegrees=0}],
            NestedStages=[new("modules",parentId,childRequest,[binding with{NodeId=parentId,PartId="sites"}])]}};
        var nestedWorld=generator.Generate(nestedRecipe,nestedCatalog);
        Check(nestedWorld.Nodes.Count==4&&nestedWorld.PlacementPlan!.NestedStages.Single().Footprints.Count==2&&nestedWorld.PlacementPlan.SiteFeatures!.Count==8,"nested generic structure programs use the same site geometry owner");
        var session=new MetadataGenerationSession(generator,e.Recipe,offsetCatalog);var before=session.Snapshot;
        var rotated=e.Recipe with{Placement=e.Recipe.Placement with{Bindings=[binding with{RotationDegrees=45}]}};
        var edited=session.Apply(new("rotate","Change source facing","test:orientation",before.World.RecipeSha256,before.World.CatalogSha256,rotated,offsetCatalog));
        Check(Json(edited.World)==Json(generator.Generate(rotated,offsetCatalog)),"site edit equals clean full generation");
        try{session.Apply(new("reject","Required yard cannot fit","test:yard",edited.World.RecipeSha256,edited.World.CatalogSha256,Layout(binding.SiteLayout! with{RearDepthMeters=1}),offsetCatalog));throw new Exception("Missing failed edit");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.StartsWith("METADATA_SITE_RULES_UNSATISFIED",StringComparison.Ordinal),"exact required-source failure");}
        Check(Json(session.Snapshot)==Json(edited),"failed edit preserves accepted geometry/cache/history");
        await session.PublishAsync(Path.Combine(output,"accepted"));
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"accepted/generation-session.json"))).Snapshot)==Json(edited),"saved source history replays complete site programs");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"baseline-world.json"),Json(world));await File.WriteAllTextAsync(Path.Combine(output,"repeated-world.json"),Json(repeatedWorld));
        await File.WriteAllTextAsync(Path.Combine(output,"nested-world.json"),Json(nestedWorld));
        await File.WriteAllTextAsync(Path.Combine(output,"wider-world.json"),Json(widerWorld));await File.WriteAllTextAsync(Path.Combine(output,"split-world.json"),Json(splitWorld));
        await File.WriteAllTextAsync(Path.Combine(output,"south-world.json"),Json(southWorld));
        await File.WriteAllTextAsync(Path.Combine(output,"off-center-world.json"),Json(offCenter));
        await File.WriteAllTextAsync(Path.Combine(output,"off-center-recipe.json"),Json(Facing(30)));
        await File.WriteAllTextAsync(Path.Combine(output,"off-center-catalog.json"),Json(Entrance(-1,true)));
        await File.WriteAllTextAsync(Path.Combine(output,"side-world.json"),Json(sideWorld));
        await File.WriteAllTextAsync(Path.Combine(output,"side-recipe.json"),Json(Facing(30)));
        await File.WriteAllTextAsync(Path.Combine(output,"side-catalog.json"),Json(SideEntrance(-3,true)));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"published"));
        Console.WriteLine("PASS: generic authored source site rules, off-center dimensions, asymmetric reservation, exact band/access areas, rotation/world placement, purpose/source provenance, optional rules, repeated/nested programs, terrain/shape/unit/entrance refusals, required source rollback, replay, publication and cancellation without a registered building.");
    }
}
