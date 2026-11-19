using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldHierarchicalGroundCapacityTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Hierarchical ground capacity: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double minimum)
    {
        var e=WorldGroundUseCapacityTests.Example(minimum,nested:true,execution:WorldPlacementExecution.Prescribed);string ground=e.Recipe.Id+"/ground-0";
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id switch{
            "ground"=>a with{Parts=[a.Parts[0] with{Count=2}],PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-10,-10,20,20)}},
            "container"=>a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-4,-8,8,16)}},
            "fixture"=>a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-3,-3,6,6)}},_=>a}).ToArray()};
        var profile=e.Recipe.Placement!.Request.Demands[0].Profile;
        var branches=new MetadataNestedPlacement("branches",ground,new("branches","Keep both required source branches","hierarchy:branches",null,
            [new("left","Left source branch","hierarchy:left",null,profile),new("right","Right source branch","hierarchy:right",null,profile)],
            [new("a",new(-5,0)),new("b",new(5,0))],[new("a","left","a","Declared branch anchor","hierarchy:a",0,0,0,1),new("b","right","b","Declared branch anchor","hierarchy:b",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed),
            [new("left",ground+"/inner-0",true),new("right",ground+"/inner-1",true)]);
        var stages=new List<MetadataNestedPlacement>{branches};
        for(int i=0;i<2;i++)
        {
            string parent=ground+"/inner-"+i;
            stages.Add(new("fixtures-"+i,parent,new("fixtures-"+i,"Retain every exact fixture","hierarchy:fixtures",null,
                [new("a","First required fixture","hierarchy:fixture",null,profile),new("b","Second required fixture","hierarchy:fixture",null,profile)],
                [new("anchor-a",new(0,-4)),new("anchor-b",new(0,4))],[new("a","a","anchor-a","Declared fixture anchor","hierarchy:a",0,0,0,1),new("b","b","anchor-b","Declared fixture anchor","hierarchy:b",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed),
                [new("a",parent+"/fixtures-0",true),new("b",parent+"/fixtures-1",true)]));
        }
        return(e.Recipe with{Placement=e.Recipe.Placement with{NestedStages=stages}},catalog);
    }
    internal static async Task RunAsync(string output,bool control=false)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);var generator=new WorldMetadataGenerator();var e=Example(.75);
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        try{generator.Generate(e.Recipe,e.Catalog);throw new Exception("Impossible combined source program accepted");}
        catch(ArgumentException error)
        {
            bool preflight=error.Data["PlacementPoseInvariantFailure"] is true&&error.Data["GroundUseCapacityWitness"] is not null;
            if(control){Check(!preflight,"prior control is a late capacity refusal");Check(error.Message.Contains("GROUND_USE",StringComparison.Ordinal),"actual prior ground-use failure: "+error.Message);}
            else
            {
                Check(preflight,"combined branches refuse before placement");Check(!error.Data.Contains("PlacementAttempts")&&!error.Data.Contains("FeasibilityAttempts"),"no candidate attempts before intrinsic proof");
                var witness=JsonSerializer.SerializeToElement(error.Data["GroundUseCapacityWitness"],WorldJson.CreateOptions());
                Check(witness.GetProperty("SolidNodeIds").GetArrayLength()==4&&witness.GetProperty("RequiredSolidArea").GetDouble()==144,"every required solid across both branches in proof");
                Check(witness.GetProperty("SourceStageIds").EnumerateArray().Select(v=>v.GetString()).SequenceEqual(new[]{"fixtures-0","fixtures-1"}),"actual contributing stages remain attributed");
            }
            await File.WriteAllTextAsync(Path.Combine(output,"refusal.json"),Json(new{error.Message,Preflight=preflight,Witness=error.Data["GroundUseCapacityWitness"]}));
        }
        var fit=Example(.6);var world=generator.Generate(fit.Recipe,fit.Catalog);Check(world.Nodes.Count==8&&world.AccessPlan!.Sites.Count==7,"all source nodes and bodies remain in fitting control");
        await File.WriteAllTextAsync(Path.Combine(output,"fit-recipe.json"),Json(fit.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"fit-catalog.json"),Json(fit.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"fit-recipe.json"),Path.Combine(output,"fit-catalog.json"),Path.Combine(output,"fit"));
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"fit")))==Json(world),"complete fitting source replay");
        if(!control)
        {
            var stages=new[]{new MetadataGroundUsePlacementStage(fit.Recipe.Placement!.Request.Id,null,fit.Recipe.Placement.Bindings.Select(b=>b.NodeId).ToArray())}.Concat(
                fit.Recipe.Placement.NestedStages!.Select(s=>new MetadataGroundUsePlacementStage(s.Id,s.ParentNodeId,s.Bindings.Select(b=>b.NodeId).ToArray(),s.ParentBoundary!=MetadataParentBoundary.Reservation))).ToArray();
            var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var sources=world.PlacementPlan!.Footprints.Concat(world.PlacementPlan.NestedStages.SelectMany(s=>s.Footprints)).ToDictionary(s=>s.NodeId,StringComparer.Ordinal);
            var definitions=world.FootprintDefinitions!;
            IReadOnlyDictionary<string,MetadataCatalogFootprint> Minimum(double value)=>definitions.ToDictionary(p=>p.Key,p=>p.Value.GroundUse is {} ground?p.Value with{GroundUse=ground with{MinimumOpenFraction=value}}:p.Value,StringComparer.Ordinal);
            var access=world.AccessPlan!.Definitions.ToDictionary(p=>p.Key,p=>p.Key=="container"?p.Value with{Occupancy=MetadataOccupancyKind.Solid}:p.Value,StringComparer.Ordinal);
            var accounting=new MetadataPhysicalAccountingPlanner();
            // Two 128m² solid ancestors cover their own contained fixtures.
            // Their union leaves 36% open; adding descendant areas would be false.
            accounting.ValidateContainedCapacity(stages,nodes,Minimum(.35),sources,access,"meters",new(1000000,default),default);
            try{accounting.ValidateContainedCapacity(stages,nodes,Minimum(.4),sources,access,"meters",new(1000000,default),default);throw new Exception("Solid ancestor shortfall accepted");}
            catch(MetadataPlacementInfeasibleException error)
            {
                var proof=(MetadataGroundUseCapacityWitness)error.Data["GroundUseCapacityWitness"]!;
                Check(proof.RequiredSolidArea==256&&proof.SolidNodeIds.Count==2&&proof.StageId=="branches"&&proof.SourceStageIds is null,"solid ancestors cover descendants exactly once in single-stage witness");
            }
            // Reservation-scoped escape has no guaranteed footprint containment.
            // Declining a preflight proof does not certify the eventual world.
            var escaped=stages.Select(s=>s.Id.StartsWith("fixtures-",StringComparison.Ordinal)?s with{InParentFootprint=false}:s).ToArray();
            accounting.ValidateContainedCapacity(escaped,nodes,Minimum(.99),sources,world.AccessPlan.Definitions,"meters",new(1000000,default),default);
            var parentBudget=new WorldSpatialWorkBudget(1,default);
            try{accounting.ValidateContainedCapacity(stages,nodes,definitions,sources,world.AccessPlan.Definitions,"meters",new(1000000,default,parentBudget),default);throw new Exception("Shared work limit ignored");}
            catch(ArgumentException error){Check(error.Message=="WORLD_ROUTE_WORK_BUDGET","shared work failure remains explicit");}
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            try{accounting.ValidateContainedCapacity(stages,nodes,definitions,sources,world.AccessPlan.Definitions,"meters",new(1000000,cancelled.Token),cancelled.Token);throw new Exception("Cancellation ignored");}
            catch(OperationCanceledException){}
            var session=new MetadataGenerationSession(generator,fit.Recipe,fit.Catalog);var accepted=session.Snapshot;
            try{session.Apply(new("required-open-space","Retain all required parts and refuse insufficient open space","hierarchy:edit",accepted.World.RecipeSha256,accepted.World.CatalogSha256,e.Recipe,e.Catalog));throw new Exception("Impossible open-space edit accepted");}catch(MetadataRegenerationException){}
            Check(Json(accepted)==Json(session.Snapshot),"early combined refusal preserves accepted state");
            await File.WriteAllTextAsync(Path.Combine(output,"controls.json"),Json(new{SolidAncestorNotDoubleCounted=true,SolidAncestorWitness=true,ReservationEscapeNotAssumedContained=true,SharedWorkLimit=true,Cancellation=true,FailedEditPreserved=true}));
        }
        Console.WriteLine(control?"PASS: prior separate-stage preflight misses combined shortfall; final accounting refuses, and complete fitting control survives.":"PASS: combined nested source preflight preserves all four required solids, eight-node/seven-source fitting control and full source replay.");
    }
}
