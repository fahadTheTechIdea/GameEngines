using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalJointTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional joint: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return e;}throw new Exception("Missing joint refusal: "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();var input=Example();string original=Json(input);
        var definition=planner.Plan(input);Check(Json(input)==original,"input capture unchanged");Verify(definition);
        AnalysisControls(definition);
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));await File.WriteAllTextAsync(Path.Combine(output,"definition-attempt.json"),Json(definition));
        var world=engine.Generate(definition);Complete(world);
        var sample=world.Regions[0].World;var allocation=sample.ProgramMixPlan!.Allocations.Single(a=>a.Request.PartId=="landmarks");
        var countRequest=allocation.Request;
        Check(countRequest.ExactCount==sample.TerrainDemandPlan!.Results.Single().Count&&allocation.Choices.Count==countRequest.ExactCount,"terrain owns exact mixed program count");
        Reject(()=>new MetadataProgramMixPlanner().Solve(countRequest with{ExactCount=countRequest.MinimumCount-1}),"METADATA_PROGRAM_MIX_EXACT_COUNT_INVALID");
        Reject(()=>new MetadataProgramMixPlanner().Solve(countRequest with{ExcludedCounts=null,Rule=countRequest.Rule with{Demands=[new("function","units",countRequest.MinimumCount+1)]}}),"METADATA_PROGRAM_MIX_UNSATISFIED");
        Reject(()=>new MetadataTerrainDemandPlanner().ValidateDocument(sample with{ProgramMixPlan=sample.ProgramMixPlan with{Allocations=sample.ProgramMixPlan.Allocations.Select(a=>a.Key!=allocation.Key?a:a with{Request=a.Request with{ExactCount=null}}).ToArray()}}),"METADATA_TERRAIN_DEMAND_MIX_COUNT_MISMATCH");
        await engine.PublishAsync(world,Path.Combine(output,"baseline"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"baseline/world-regions.json")))==Json(world),"saved complete joint source replay");
        Check(Json(planner.Plan(input with{Regions=input.Regions.Reverse().ToArray(),Connections=input.Connections.Reverse().ToArray()}))==Json(definition),"canonical order");
        Reject(()=>planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        Check(planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work}).OwnershipPlan!.Work==definition.OwnershipPlan.Work,"exact aggregate work");
        var limited=Reject(()=>planner.Plan(input with{ProgramComposition=input.ProgramComposition! with{MaximumAttempts=1}}),"METADATA_REGIONAL_JOINT_ATTEMPT_LIMIT");
        Check(limited.Data["RegionalJoint"] is MetadataRegionalJointPlan{Attempts.Count:1},"failed search history retained");
        await File.WriteAllTextAsync(Path.Combine(output,"limited.json"),Json(limited.Data["RegionalJoint"]));
        var depth=Reject(()=>planner.Plan(input with{Coupling=input.Coupling! with{MaximumIterations=1}}),"METADATA_PROGRAM_COMPOSITION_ALTERNATIVE_BUDGET");
        Check(depth.Data["RegionalJoint"] is MetadataRegionalJointPlan depthTrace&&depthTrace.Attempts.All(a=>a.Iteration==1&&!a.Accepted)&&
            depthTrace.Attempts.Any(a=>a.Rejection=="METADATA_REGIONAL_JOINT_ITERATION_LIMIT"),"insufficient branch depth is retained; exhausted logical frontier remains a hard budget failure, not a proof of universal infeasibility");
        var proof=definition.OwnershipPlan!.JointPlan!;
        Reject(()=>engine.Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{JointPlan=proof with{EnumerationWork=proof.EnumerationWork+1}}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        using var cancel=new CancellationTokenSource();cancel.Cancel();try{planner.Plan(input,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: joint actual habitat/program search, changing nested choice counts, source-invariant physical rejection, full original physical programs and journeys, exact finite budgets and source replay.");
    }
    private static void Verify(MetadataRegionalWorldDefinition definition)
    {
        var p=definition.OwnershipPlan!;var proof=p.JointPlan!;
        Check(proof.Attempts.Count>1&&proof.Attempts[^1].Accepted&&proof.Attempts.Take(proof.Attempts.Count-1).All(a=>!a.Accepted),"only final complete qualified branch accepted");
        Check(proof.EnumerationWork+proof.Attempts.Sum(a=>a.Work)==p.Work,"all enumeration and branch work retained");
        Check(proof.Attempts.Any(a=>a.InputChoices.Zip(a.OutputChoices??[]).Any(pair=>pair.First.RequiredRanks.Count!=pair.Second.RequiredRanks.Count)),"actual terrain changes nested decision topology");
        Check(proof.Attempts.Any(a=>a.InvariantChoice is not null),"actual infeasible preferred complete source retained");
        Check(definition.Regions.All(r=>r.Recipe.ProgramComposition!.RequiredRanks!.Any(rank=>rank>0)),"actual physically feasible complete source explicitly bound");
        for(int i=0;i<proof.Attempts.Count;i++)
        {
            var a=proof.Attempts[i];Check(a.Ordinal==i+1&&a.Iteration<=p.Source.Coupling!.MaximumIterations&&a.Work>=0,"finite ordered branches");
            if(a.ParentOrdinal is int parent)Check(parent<a.Ordinal&&a.InputSha256==proof.Attempts[parent-1].OutputSha256,"actual parent output becomes child input");
            if(a.Accepted)Check(a.Stable&&a.InputSha256==a.OutputSha256&&a.GeometrySha256 is not null,"space and complete choices converge before physical acceptance");
        }
    }
    private static void Complete(MetadataRegionalWorldDocument world)
    {
        foreach(var packet in world.Regions)
        {
            var w=packet.World;int count=w.TerrainDemandPlan!.Results.Single().Count;Check(count>=4,"original minimum retained");
            Check(w.Nodes.Count(n=>n.Kind=="marker")==count&&w.Nodes.Count(n=>n.Kind=="discovery")==count,"every terrain-driven parent retains nested content");
            Check(w.Nodes.Where(n=>n.Kind=="discovery").All(n=>n.ArchetypeId=="landmark-alt"),"complete larger discovery parents selected");
            Check(w.ProgramCompositionPlan!.Definition.RequiredRanks!.Count==count+1&&w.ProgramCompositionPlan.Attempts.Count==1,"all actual nested decisions bound exactly once");
            Check(w.RoutePlan!.Routes.Count==count+1&&w.RoutePlan.Routes.All(r=>r.Reverse is not null),"all original and discovery return journeys");
            Check(w.AccessPlan!.Sites.Count(s=>s.NodeId.EndsWith("/marker-0")&&Math.Abs(Math.Abs(s.Footprint.SignedArea())-2.4*2.4)<1e-7)==count,"every original2.4m nested marker never reduced");
            var source=w.PlacementPlan!.Definition.SourceWorld!;Check(source.Nodes.All(n=>Json(n)==Json(w.Nodes.Single(m=>m.Id==n.Id))),"every original complete compound source member preserved");
            Check(Json(source.RegionPlan)==Json(w.RegionPlan),"inherited source habitat is exact, without an extra consumer region decision");
        }
        Check(world.Connections.Count==2&&world.Connections.All(c=>c.Reverse is not null&&c.Definition.Profile.Radius==.2),"all original regional round trips");
    }
}
