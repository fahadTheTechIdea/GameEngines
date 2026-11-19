using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalProgressionTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool ok,string reason){if(!ok)throw new Exception("Regional progression: "+reason);}
    static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var arrival=template.Regions.Single(r=>r.Id=="arrival");
        const string node="region-west/compounds-0";
        var start=new MetadataRegionalProgressionNode("arrival",node);var milestone=new MetadataRegionalProgressionNode("grove",node);var goal=new MetadataRegionalProgressionNode("outpost",node);
        var rule=new MetadataRegionalProgressionRule("journey","Visit required landmark","test:journey",start,[goal],["regional-travel"],[milestone]);
        var request=new MetadataRegionalDesignRequest(1,"progression-world","Select a connected game world","test:world",template,
            [new("arrival",[new("complete",0,arrival.Recipe,arrival.Catalog)])],[],WorldProgression:[rule]);
        var engine=new MetadataRegionalDesignGenerator();var result=engine.Generate(request);
        var plan=result.Receipt.Attempts.Single().WorldProgression!;var path=plan.Results.Single().Paths.Single();
        Check(path.Nodes.First()==start&&path.Nodes.Last()==goal&&path.Nodes.Contains(milestone)&&path.Nodes.Select(n=>n.RegionId).Distinct().Count()>=3,"actual regional path includes the required milestone");
        Check(path.Nodes.All(n=>n.NodeId==node)&&path.Nodes.Select(n=>n.RegionId).Distinct().Count()==path.Nodes.Count,"reused source IDs remain distinct by region");
        Check(path.Links.Count==path.Nodes.Count-1&&path.Links.All(l=>l.RegionId is null)&&result.World.Connections.Count==3,"witness uses generated regional links");
        var graph=new MetadataRegionalProgressionPlanner();
        var gate=path.Links[1];var gated=rule with{Capabilities=new(["key"],[],[new(milestone,["key"])],[new(gate,["key"])],true)};
        var gatedPlan=graph.Plan([gated],result.World);Check(gatedPlan.Results.Single().Paths.Single().CapabilityStates!.Last().Contains("key"),"capability acquired before gated travel");
        Reject(()=>graph.Plan([gated with{Capabilities=gated.Capabilities! with{Grants=[]}}],result.World),"METADATA_PROGRESSION_UNREACHABLE");
        Reject(()=>graph.Plan([rule with{MaximumWork=1}],result.World),"METADATA_PROGRESSION_WORK_BUDGET");
        Reject(()=>graph.Plan([rule with{Start=new("arrival","missing-node")}],result.World),"METADATA_REGIONAL_PROGRESSION_NODE_REQUIRED");
        Reject(()=>graph.Plan([rule with{Start=new("missing",node)}],result.World),"METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
        var without=result.World with{Connections=result.World.Connections.Where(c=>c.Definition.Id!=path.Links[0].ConnectionId).ToArray()};
        Reject(()=>graph.Plan([rule],without),"METADATA_PROGRESSION_UNREACHABLE");
        var first=result.World.Connections.Single(c=>c.Definition.Id==path.Links[0].ConnectionId);
        var directed=result.World with{Connections=result.World.Connections.Select(c=>c==first?c with{Definition=c.Definition with{
            From=new(milestone.RegionId,milestone.NodeId),To=new(start.RegionId,start.NodeId),Bidirectional=false}}:c).ToArray()};
        Reject(()=>graph.Plan([rule],directed),"METADATA_PROGRESSION_UNREACHABLE");
        var bypass=result.World with{Connections=[..result.World.Connections,first with{Definition=first.Definition with{Id="test-bypass",
            From=new(start.RegionId,start.NodeId),To=new(goal.RegionId,goal.NodeId),Bidirectional=true}}]};
        Reject(()=>graph.Plan([rule],bypass),"METADATA_PROGRESSION_MILESTONE_BYPASS");
        var blocked=request with{WorldProgression=[gated with{Capabilities=gated.Capabilities! with{Grants=[]}}]};
        try{engine.Generate(blocked);throw new Exception("Blocked progression accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_REGIONAL_DESIGN_ALTERNATIVES_EXHAUSTED")
        {var attempts=(MetadataRegionalDesignAttempt[])e.Data["RegionalDesignAttempts"]!;Check(attempts.Length==1&&attempts[0].Rejection!.StartsWith("METADATA_PROGRESSION_UNREACHABLE",StringComparison.Ordinal),"design preserves progression refusal");}
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{graph.Plan([rule],result.World,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"world progression replays");
        var forged=result.Receipt with{Attempts=[result.Receipt.Attempts[0] with{WorldProgression=plan with{Work=0}}]};
        Reject(()=>engine.Verify(forged,result.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: directed regional progression preserves region identity, milestones, capabilities, refusal and replay.");
    }
}
