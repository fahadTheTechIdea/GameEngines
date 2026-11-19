using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldInteriorPopulationTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool v,string message){if(!v)throw new Exception("Interior population: "+message);}
    private static T Reject<T>(Action action)where T:Exception
    {try{action();}catch(T error){return error;}throw new Exception("Expected "+typeof(T).Name);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Source(int rooms,double rotation=0)
    {
        var e=WorldGeneratedInteriorTests.Example(count:rooms,rotation:rotation);
        if(rotation!=0)
        {
            // This declared counterfactual rotates room equipment with its floor,
            // retaining original source sizes and the 1.2m local fixture offset.
            double radians=rotation*Math.PI/180;var programs=e.Recipe.Placement!.Programs!.Select(p=>p with{
                Request=p.Request with{Anchors=p.Request.Anchors.Select(a=>a with{Position=new(a.Position.X*Math.Cos(radians)-a.Position.Y*Math.Sin(radians),
                    a.Position.X*Math.Sin(radians)+a.Position.Y*Math.Cos(radians))}).ToArray()},
                Bindings=p.Bindings.Select(b=>b with{RotationDegrees=rotation}).ToArray()}).ToArray();
            e=(e.Recipe with{Placement=e.Recipe.Placement with{Programs=programs}},e.Catalog);
        }
        return e;
    }
    internal static WorldMetadataArchetype Equipment(double markerWidth=.2)=>new("marker","required-equipment","Purposeful source-path equipment",new Dictionary<string,JsonElement>(),[],[],[],
        PhysicalFootprint:new("meters","Source-sized route-side equipment","interior-population:source",Box(-markerWidth/2,-.05,markerWidth,.1)),Access:new(MetadataOccupancyKind.Solid,[]));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int rooms=6,double rotation=0,double offset=-.8,double markerWidth=.2)
    {
        var e=Source(rooms,rotation);var source=new WorldMetadataGenerator().Generate(e.Recipe,e.Catalog);
        var marker=Equipment(markerWidth);
        var catalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id=="room"?a with{Parts=[..a.Parts,new("markers","marker",2,"Every required route-side equipment member")]}:a),marker]};
        var original=e.Recipe.Placement!;
        var program=original.Programs!.Single();
        // Select actual connection identities, never copy room/path coordinates.
        // Every member belongs inside its room alongside its original fixture.
        var stages=source.InteriorPlan!.Results.SelectMany(r=>r.RoomNodeIds).Order(StringComparer.Ordinal).Select((parent,index)=>{
            var link=source.Connections.Where(c=>c.Kind=="door"&&(c.FromId==parent||c.ToId==parent)).OrderBy(c=>c.Id,StringComparer.Ordinal).First();
            var bindings=program.Bindings.Select(b=>new MetadataPlacementBinding(b.DemandId,parent,b.UseCatalogFootprint,b.RotationDegrees,b.PartId,b.SiteLayout,b.Population)).ToList();
            bindings.Add(new("markers",parent,true,PartId:"markers",Population:new("Follow the accepted incident room passage","interior-population:route",
                Route:new(link.Id,.5,.6,NormalOffset:offset,Reverse:link.ToId==parent),Facing:MetadataPopulationFacing.Along)));
            return new MetadataNestedPlacement("room-content-"+index,parent,program.Request with{Demands=[..program.Request.Demands,
                new("markers","Required route-side content","interior-population:placement",null,program.Request.Demands[0].Profile)]},bindings);
        }).ToArray();
        var recipe=e.Recipe with{PopulationSource=new(e.Recipe,e.Catalog),Placement=original with{Programs=null,NestedStages=stages}};
        return(recipe,catalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Interior population evidence exists");Directory.CreateDirectory(output);
        var g=new WorldMetadataGenerator();var observations=new List<object>();
        foreach(var (name,count,rotation) in new[]{("branching",6,0d),("rotated",6,23d),("compact",3,0d)})
        {
            var e=Example(count,rotation);string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var world=g.Generate(e.Recipe,e.Catalog);var source=world.PlacementPlan!.Definition.SourceWorld!;
            Check(world.Nodes.Count==source.Nodes.Count+2*count&&world.AccessPlan!.Sites.Count==source.AccessPlan!.Sites.Count+2*count,"every source node and all required physical members retained");
            Check(Json(world.InteriorPlan!.Results)==Json(source.InteriorPlan!.Results),"exact rooms/walls/openings/directed source paths preserved");
            Check(world.PlacementPlan.PopulationOrientations!.Sum(t=>t.NodeIds.Count)==2*count,"all generated route poses have replayed tangents");
            WorldMetadataGenerator.ValidateDocument(world);await g.PublishAsync(e.Recipe,e.Catalog,world,Path.Combine(folder,"published"),null,CancellationToken.None);
            var read=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"published/world-metadata.json"));Check(Json(read)==Json(world),"native saved replay preserves full source and content");
            observations.Add(new{Name=name,Nodes=world.Nodes.Count,Physical=world.AccessPlan!.Sites.Count,Rooms=count,Directions=world.InteriorPlan.Results.Sum(r=>r.Routes.Sum(p=>p.Reverse is null?1:2)),Work=world.InteriorPlan.Work});
            Console.WriteLine("Qualified interior-derived population: "+name+"; "+world.Nodes.Count+" nodes.");
        }
        await Lifecycle(output,g);await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: interior-derived population source preservation, full-width blocking, source edits/rollback, checkpoint replay, tamper/work/cancellation and attributed stage refusal.");
    }
    private static async Task Lifecycle(string output,WorldMetadataGenerator g)
    {
        var e=Example(3);var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"interior-reference");
        var session=await MetadataGenerationSession.StartAsync(g,e.Recipe,e.Catalog,reporting);var initial=session.Snapshot;
        await session.PublishAsync(Path.Combine(output,"lifecycle-baseline"));
        var changed=Example(3,markerWidth:.3);
        var edit=new MetadataEditRequest("equipment-width","Edit exact equipment source width","interior-population:edit",initial.World.RecipeSha256,initial.World.CatalogSha256,changed.Recipe,changed.Catalog);
        await File.WriteAllTextAsync(Path.Combine(output,"edit.json"),Json(edit));
        var edited=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"lifecycle-edited"));
        Check(Json(edited.World)==Json(g.Generate(changed.Recipe,changed.Catalog))&&Json(initial.World)!=Json(edited.World),"source-size edit changes real geometry and equals clean generation");
        Check(Json(initial.World.InteriorPlan!.Results)==Json(edited.World.InteriorPlan!.Results),"source-size edit preserves original floors, walls, openings and every path");
        var restored=await MetadataGenerationSession.RestoreAsync(g,Path.Combine(output,"lifecycle-edited/generation-session.json"),reporting:reporting);
        Check(Json(restored.Snapshot)==Json(edited),"full source history restores edited geometry and proof");
        var source=initial.World.PlacementPlan!.Definition.SourceWorld!;
        WorldMetadataRecipe Fixed(WorldMetadataRecipe r)=>r with{PopulationSource=null,Placement=r.Placement! with{SourceWorld=source}};
        WorldMetadataRecipe Offset(double offset)=>Fixed(e.Recipe) with{Placement=Fixed(e.Recipe).Placement! with{
            NestedStages=e.Recipe.Placement!.NestedStages!.Select(s=>s with{Bindings=s.Bindings.Select(b=>b.Population?.Route is not {} p?b:
                b with{Population=b.Population with{Route=p with{NormalOffset=offset}}}).ToArray()}).ToArray()}};
        var blocked=Offset(0);await File.WriteAllTextAsync(Path.Combine(output,"blocked-recipe.json"),Json(blocked));await File.WriteAllTextAsync(Path.Combine(output,"blocked-catalog.json"),Json(e.Catalog));
        var refusal=Reject<ArgumentException>(()=>g.Generate(blocked,e.Catalog));
        Check(refusal.Message.Contains("METADATA_POPULATION_PASSAGE_BLOCKED",StringComparison.Ordinal),"content on a source path refuses rather than redesigning it");
        var missing=Fixed(e.Recipe) with{Interiors=null};Reject<ArgumentException>(()=>g.Generate(missing,e.Catalog));
        var altered=Fixed(e.Recipe) with{Interiors=e.Recipe.Interiors! with{Rules=[e.Recipe.Interiors.Rules[0] with{OpeningWidth=1.4}]}};
        Check(Reject<ArgumentException>(()=>g.Generate(altered,e.Catalog)).Message=="METADATA_POPULATION_SOURCE_INTERIOR_RULE_CHANGED","consumer cannot change source opening policy");
        var stage=Reject<ArgumentException>(()=>g.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumStageAttempts=1}},e.Catalog));
        Check(stage.Message=="METADATA_PLACEMENT_STAGE_ATTEMPT_BUDGET"&&stage.Data.Contains("PlacementFailures")&&stage.Data.Contains("FeasibilityAttempts"),"stage exhaustion retains bounded diagnostic evidence");
        var badWorld=initial.World with{InteriorPlan=initial.World.InteriorPlan! with{Work=initial.World.InteriorPlan.Work+1}};
        Reject<ArgumentException>(()=>new MetadataInteriorPlanner().ValidateDocument(badWorld));
        var forgedSource=source with{InteriorPlan=source.InteriorPlan! with{Work=source.InteriorPlan.Work+1}};
        var forged=initial.World with{PlacementPlan=initial.World.PlacementPlan! with{Definition=initial.World.PlacementPlan.Definition with{SourceWorld=forgedSource}}};
        Reject<ArgumentException>(()=>new MetadataInteriorPlanner().ValidateDocument(forged));
        string room=source.InteriorPlan!.Results[0].RoomNodeIds[0];
        var movedCenter=initial.World with{Nodes=initial.World.Nodes.Select(n=>n.Id!=room?n:n with{Position=[n.Position![0]+.1,n.Position![1]]}).ToArray()};
        Check(Reject<ArgumentException>(()=>new MetadataInteriorPlanner().ValidateDocument(movedCenter)).Message.StartsWith("METADATA_POPULATION_SOURCE_ROUTE_ENDPOINT_CHANGED",StringComparison.Ordinal),
            "standalone preserved replay qualifies current endpoint poses rather than accepting cached path endpoints");
        var workFailure=Reject<ArgumentException>(()=>new MetadataInteriorPlanner().Plan(source.InteriorPlan!.Definition,source.Surface!,source.Nodes,source.Connections,
            source.AccessPlan,new WorldSpatialWorkBudget(1,CancellationToken.None),CancellationToken.None,source.InteriorPlan));
        Check(workFailure.Message=="WORLD_ROUTE_WORK_BUDGET"&&Equals(workFailure.Data["MaximumWork"],1L),"preserved interior path validation charges the actual shared work ledger");
        var wall=source.InteriorPlan!.Results[0].Walls[0];var center=new WorldPoint((wall.From.X+wall.To.X)/2,(wall.From.Y+wall.To.Y)/2);
        var wallNode=source.Nodes.First(n=>n.Kind=="equipment") with{Id="wall-solid",ParentId=wall.RoomNodeId,Position=[center.X,center.Y]};
        var occupied=source.AccessPlan! with{Sites=[..source.AccessPlan!.Sites,new("wall-solid",MetadataOccupancyKind.Solid,"interior-population:wall",Box(center.X-.4,center.Y-.25,.8,.5),[])]};
        Check(Reject<ArgumentException>(()=>new MetadataInteriorPlanner().Plan(source.InteriorPlan.Definition,source.Surface!,source.Nodes.Append(wallNode).ToArray(),source.Connections,
            occupied,null,CancellationToken.None,source.InteriorPlan)).Message=="METADATA_INTERIOR_CONTENT_WALL_COLLISION:wall-solid","new full solid bodies cannot cross accepted walls");
        using(var cancellation=new CancellationTokenSource())
        {cancellation.Cancel();Reject<OperationCanceledException>(()=>new MetadataInteriorPlanner().Plan(source.InteriorPlan!.Definition,source.Surface!,source.Nodes,source.Connections,source.AccessPlan,null,cancellation.Token,source.InteriorPlan));}
        var failedEdit=new MetadataEditRequest("blocked","Preserve accepted world when content blocks the passage","interior-population:blocked",edited.World.RecipeSha256,edited.World.CatalogSha256,blocked,e.Catalog);
        try{await restored.ApplyAndPublishAsync(failedEdit,Path.Combine(output,"refused"));throw new Exception("Missing blocked edit refusal");}catch(ArgumentException){}
        Check(Json(restored.Snapshot)==Json(edited)&&!Directory.Exists(Path.Combine(output,"refused")),"failed content edit preserves complete accepted state and output");
        await File.WriteAllTextAsync(Path.Combine(output,"lifecycle-observations.json"),Json(new{Initial=initial.Report.WorldSha256,Edited=edited.Report.WorldSha256,
            InitialNodes=initial.World.Nodes.Count,EditedNodes=edited.World.Nodes.Count,PreservedResults=true,BlockedReason=refusal.Message}));
    }
}
