using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldOffsetPhysicalStudy
{
    private sealed record Observation(int Barrier,int Offset,string Outcome,string? Failure,int PhysicalAttempts,int Contexts,long Milliseconds);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");
        Directory.CreateDirectory(output);var original=WorldRegionalPhysicalFeedbackTests.Example();var planner=new MetadataRegionalOwnershipPlanner();
        var observations=new List<Observation>();
        foreach(int barrier in new[]{115,150,200})foreach(int offset in new[]{-500,0,150,500})
        {
            var surface=original.Surface with{Exclusions=original.Surface.Exclusions.Select(e=>e.Id=="terrain-cut"?e with{Boundary=Box(barrier,0,10,448)}:e).ToArray()};
            var source=original with{Surface=surface,Pattern=original.Pattern with{PartitionSearch=new([0],Bend:-.4,BendOffset:offset)}};
            string id=$"barrier-{barrier}-offset-{offset}";
            await File.WriteAllTextAsync(Path.Combine(output,id+"-request.json"),Json(source));
            var clock=Stopwatch.StartNew();
            try
            {
                var definition=planner.Plan(source);clock.Stop();var feedback=definition.OwnershipPlan!.PhysicalFeedbackPlan!;
                observations.Add(new(barrier,offset,"Generated",null,feedback.Attempts.Count,feedback.Contexts.Count,clock.ElapsedMilliseconds));
            }
            catch(ArgumentException error)
            {
                clock.Stop();var feedback=error.Data["RegionalPhysicalFeedback"] as MetadataRegionalPhysicalFeedbackPlan;
                bool physical=feedback is {Attempts.Count:>0}&&feedback.Attempts.All(a=>!a.Accepted&&!a.PlanningRejected)&&
                    error.Message.StartsWith("METADATA_REGIONAL_PHYSICAL_COMPONENTS_EXHAUSTED",StringComparison.Ordinal);
                observations.Add(new(barrier,offset,physical?"PhysicalRefusal":"OtherRefusal",error.Message,feedback?.Attempts.Count??0,feedback?.Contexts.Count??0,clock.ElapsedMilliseconds));
            }
            await File.WriteAllTextAsync(Path.Combine(output,id+"-result.json"),Json(observations[^1]));
            Console.WriteLine($"{id}: {observations[^1].Outcome} {observations[^1].Failure} ({clock.ElapsedMilliseconds} ms)");
        }
        var pair=(from failed in observations where failed.Outcome=="PhysicalRefusal"
                  from success in observations where success.Barrier==failed.Barrier&&success.Outcome=="Generated"
                  select (failed,success)).FirstOrDefault();
        await File.WriteAllTextAsync(Path.Combine(output,"study.json"),Json(new{Version=1,Observations=observations,
            Selected=pair.failed is null?null:new{pair.failed.Barrier,Primary=pair.failed.Offset,Alternate=pair.success.Offset}}));
        if(pair.failed is null){Console.WriteLine("NO_COMPARABLE_PHYSICAL_FALLBACK: all declared case outcomes retained.");return;}
        var candidate=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>(Path.Combine(output,$"barrier-{pair.failed.Barrier}-offset-{pair.failed.Offset}-request.json"));
        var search=candidate.Pattern.PartitionSearch!;
        candidate=candidate with{Pattern=candidate.Pattern with{PartitionSearch=search with{AlternateBendOffsets=[pair.success.Offset]}}};
        await File.WriteAllTextAsync(Path.Combine(output,"fallback-request.json"),Json(candidate));
        var result=planner.Plan(candidate);var trace=result.OwnershipPlan!.PhysicalFeedbackPlan!;
        if(trace.Contexts.Count<2||trace.Attempts[0].Accepted||!trace.Attempts[^1].Accepted||
            trace.Contexts[0].SelectedPartitions!.Any(c=>c.BendOffset!=pair.failed.Offset)||
            !trace.Contexts[^1].SelectedPartitions!.Any(c=>c.BendOffset==pair.success.Offset))
            throw new InvalidOperationException("PHYSICAL_OFFSET_STUDY_FALLBACK_NOT_OBSERVED");
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(result);
        if(world.Regions.Count!=3||world.Regions.Sum(r=>r.World.Nodes.Count)!=48||world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)!=45||
            world.Connections.Count!=2||world.Connections.Any(c=>c.Forward.Path is null||c.Reverse?.Path is null))
            throw new InvalidOperationException("PHYSICAL_OFFSET_STUDY_CONTENT_INCOMPLETE");
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        if(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))!=Json(world))
            throw new InvalidOperationException("PHYSICAL_OFFSET_STUDY_REPLAY_MISMATCH");
        Console.WriteLine($"PASS: barrier {pair.failed.Barrier} rejects offset {pair.failed.Offset} physically and the unchanged program succeeds via offset {pair.success.Offset}; full world replayed.");
    }
}
