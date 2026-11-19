using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldGlobalComponentTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool value,string text){if(!value)throw new Exception("Complete component assignment: "+text);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static MetadataRegionalOwnershipRequest Example(bool authored=false,bool doubleChange=false)
    {
        var source=WorldRegionalNetworkTests.Example();var ids=doubleChange?new[]{"arrival","grove","outpost"}:new[]{"arrival","grove"};
        var network=source.NetworkRules![0];double width=doubleChange?300:100;var boundary=Box(0,0,width,80);
        var holes=new[]{new WorldSurfaceExclusion("terrain-cut","Keep the actual disconnected terrain components","component:cut",Box(0,30,width,5)),
            new WorldSurfaceExclusion("protected-ground","Retain the fixed destination component","component:protected",doubleChange?Box(200,35,100,45):Box(0,35,50,45))};
        var options=network.Options with{MaximumPairChecks=doubleChange?3:1,MaximumWork=100000000,
            RequiredConnections=doubleChange?[new("arrival","grove"),new("grove","outpost")]:[new("arrival","grove")]};
        source=source with{Regions=source.Regions.Where(r=>ids.Contains(r.Id)).ToArray(),Surface=source.Surface with{Boundary=boundary,Patches=[],Exclusions=holes},
            NetworkRules=[network with{Destinations=network.Destinations.Where(d=>ids.Contains(d.Id)).ToArray(),Options=options}],
            PhysicalFeedback=new("Revise complete assignments without rejecting viable local choices","component:physical",8)};
        if(authored)source=source with{NetworkRules=null,Connections=[new("required-route","regional-walk","Keep the required physical journey","component:journey",
            new("arrival","region-west/compounds-0","east"),new("grove","region-west/compounds-0","west"),"arrival",boundary,options.Profile,options.Search,true)]};
        return source;
    }
    private static ArgumentException Reject(Action action,string prefix)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return error;}throw new Exception("Missing "+prefix);}
    internal static async Task RunAsync(string output,string? only=null)
    {
        if(only is not null&&only is not ("network" or "authored" or "double-change"))throw new ArgumentException("Unknown complete component case");
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        foreach(var name in new[]{"network","authored","double-change"}.Where(n=>only is null||n==only))
        {
            var source=Example(name=="authored",name=="double-change");string original=Json(source);var definition=planner.Plan(source);var world=engine.Generate(definition);
            var proof=definition.OwnershipPlan!.PhysicalFeedbackPlan!;var first=proof.Attempts[0];var last=proof.Attempts[^1];
            Check(Json(source)==original,"source templates immutable");
            Check(world.Regions.Sum(r=>r.World.Nodes.Count)==source.Regions.Count*5&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==source.Regions.Count*3,"all original compound/module members remain");
            Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"all required directed paths realized");
            Check(proof.Contexts.Single().ComponentSearch?.Domains.Count==source.Regions.Count&&proof.Contexts[0].ComponentSearch!.Work>0,"complete source domains and real search work retained");
            Check(!first.Accepted&&last.Accepted&&proof.Attempts.Count==(name=="double-change"?4:2),"bounded breadth-first assignment history");
            string fixedRegion=name=="double-change"?"outpost":"arrival";
            Check(first.Selections.Single(c=>c.RegionId==fixedRegion)==last.Selections.Single(c=>c.RegionId==fixedRegion),"fixed first/unrelated program never moved");
            Check(first.Selections.Count(c=>c.Component!=last.Selections.Single(v=>v.RegionId==c.RegionId).Component)==(name=="double-change"?2:1),"only permitted complete design variables change");
            Check(Json(definition)==Json(planner.Plan(source with{Regions=source.Regions.Reverse().ToArray()})),"declaration order canonical");
            await engine.PublishAsync(world,Path.Combine(output,name));Check(Json(await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json")))==Json(world),"read/export replays failures and complete assignments");
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(source));
        }
        if(only is not null){Console.WriteLine("PASS: complete component case "+only);return;}
        var input=Example();var acceptedDefinition=planner.Plan(input);var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),input);var before=session.Snapshot;
        await session.PublishAsync(Path.Combine(output,"session-initial"));
        var failed=input with{PhysicalFeedback=input.PhysicalFeedback! with{MaximumAttempts=1}};
        var limited=Reject(()=>planner.Plan(failed),"METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT");Check(limited.Data["RegionalPhysicalFeedback"] is MetadataRegionalPhysicalFeedbackPlan{Attempts.Count:1},"failed attempt evidence retained");
        try{await session.ApplyDerivedAndPublishAsync(new("limited","Retain accepted design","component:edit",before.World.DefinitionSha256,before.Report.WorldSha256,failed),Path.Combine(output,"rejected"));throw new Exception("Missing failed source edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(before)&&!Directory.Exists(Path.Combine(output,"rejected")),"failed edit preserves accepted snapshot/output");
        var changed=input with{Purpose="Revised declared world purpose"};var edited=await session.ApplyDerivedAndPublishAsync(new("purpose","Revise authored intent","component:edit",before.World.DefinitionSha256,before.Report.WorldSha256,changed),Path.Combine(output,"session-edited"));
        Check(edited.Report.Regions.All(r=>r.Reused),"unchanged complete local packets reused");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session-edited/generation-session.json"));Check(Json(restored.Snapshot)==Json(edited),"complete assignment source history restores");
        var rollback=await session.ApplyDerivedAsync(new("rollback","Restore exact original source","component:edit",edited.World.DefinitionSha256,edited.Report.WorldSha256,input));Check(Json(rollback.World)==Json(before.World),"exact world rollback");
        var exhausted=input with{Surface=input.Surface with{Exclusions=[..input.Surface.Exclusions,new("protected-east","Leave no northern alternative","component:east",Box(50,0,50,30))]}};
        var impossible=Reject(()=>planner.Plan(exhausted),"METADATA_REGIONAL_PHYSICAL_PARTITIONS_EXHAUSTED");
        Check(impossible.Data["RegionalPhysicalFeedback"] is MetadataRegionalPhysicalFeedbackPlan spent&&
            spent.Contexts.Count==exhausted.Pattern.PartitionSearch!.AxisOffsetsDegrees.Count&&spent.Attempts.All(a=>!a.Accepted)&&
            spent.Contexts.Select(c=>(c.SelectedPartitions??c.RequestedPartitions)!.Single().AxisOffsetDegrees).Order().SequenceEqual(exhausted.Pattern.PartitionSearch.AxisOffsetsDegrees.Order()),
            "every declared ownership axis is exhausted without relaxing source or travel requirements");
        var workLimit=Reject(()=>planner.Plan(input with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Check(workLimit.Data["RegionalPhysicalFeedback"] is MetadataRegionalPhysicalFeedbackPlan{Attempts.Count:0},"work exhaustion is never a physical candidate retry");
        var anchor=acceptedDefinition.OwnershipPlan!.Anchors[0];var region=acceptedDefinition.Regions[0];var actual=WorldSurfaceQuery.CreateRegional(acceptedDefinition.Surface,region.GenerationDomain).Definition;
        Reject(()=>new MetadataRegionPlanner().Plan(anchor.Plan.Request with{RequiredAssignment=[-1]},region.Recipe.Environment!,actual,region.Recipe.Space),"WORLD_REGION_INTENT_ASSIGNMENT_INVALID");
        Reject(()=>new MetadataRegionPlanner().Plan(anchor.Plan.Request with{RequiredAssignment=[4095]},region.Recipe.Environment!,actual,region.Recipe.Space),"WORLD_REGION_INTENT_UNSATISFIED");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{planner.Plan(input,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        await File.WriteAllTextAsync(Path.Combine(output,"limited-request.json"),Json(failed));await File.WriteAllTextAsync(Path.Combine(output,"exhausted-request.json"),Json(exhausted));
        Console.WriteLine("PASS: authored/network complete-assignment redesign, preserved fixed programs, two simultaneous revisions,35nodes/21physical sources, canonical source/replay, true exhaustion versus attempt/work limits, exact component pins, source history/reuse/failure preservation/rollback and cancellation.");
    }
}
