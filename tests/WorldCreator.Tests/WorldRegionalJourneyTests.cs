using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalJourneyTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Regional physical journey: "+reason);}
    static void Reject(Action run,string code){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var arrival=template.Regions.Single(r=>r.Id=="arrival");
        const string parent="region-west/compounds-0",child="region-west/compounds-0/modules-0";
        var logical=arrival.Recipe with{AdditionalConnections=[..arrival.Recipe.AdditionalConnections!,new("program-entry","program-access",child,parent,true)]};
        var profile=template.NetworkRules!.Single().Options.Profile;
        var physical=logical with{RouteRules=[..logical.RouteRules!,new("entry-route","program-access",profile,new(.1,[]),"east","east")]};
        var progress=new MetadataRegionalProgressionRule("progress","Reach the wider world","test:progress",new("arrival",child),
            [new("outpost",parent)],["program-access","regional-travel"],[new("arrival",parent)]);
        var journey=new MetadataRegionalJourneyRule("journey","Travel to the destination and return","test:journey",
            new("arrival",child,"east"),new("outpost",parent,"east"),["program-access","regional-travel"],profile,true,
            RequiredMilestones:[new("grove",parent,"east"),new("pass",parent,"east")]);
        var request=new MetadataRegionalDesignRequest(1,"physical-journey","Choose a physically connected world","test:world",template,
            [new("arrival",[new("logical-only",0,logical,arrival.Catalog),new("routed",1,physical,arrival.Catalog)])],[],2,
            WorldProgression:[progress],WorldJourneys:[journey]);
        var engine=new MetadataRegionalDesignGenerator();var result=engine.Generate(request);var attempts=result.Receipt.Attempts;
        Check(attempts.Count==2&&!attempts[0].Accepted&&attempts[0].WorldProgression is not null&&
            attempts[0].Rejection=="METADATA_REGIONAL_JOURNEY_UNREACHABLE:journey"&&attempts[1].Accepted&&
            attempts[1].Selections.Single().OptionId=="routed","logical reachability alone cannot satisfy physical journey");
        var proof=attempts[1].WorldJourneys!.Journeys.Single();
        Check(proof.Forward.Count>1&&proof.Return is {Count:>1}&&proof.Forward.Any(s=>s.RegionId=="arrival")&&
            proof.Forward.Any(s=>s.RegionId is null),"forward witness includes local and regional route segments");
        void Continuous(IReadOnlyList<MetadataRegionalJourneyStep> path)
        {for(int i=1;i<path.Count;i++)Check(path[i-1].To==path[i].From,"route segments meet exactly");}
        Continuous(proof.Forward);Continuous(proof.Return!);
        Check(proof.Milestones is {Count:2}&&proof.Forward.Any(s=>s.To==proof.Milestones[0].Position)&&
            proof.Forward.Any(s=>s.To==proof.Milestones[1].Position)&&
            proof.Forward.ToList().FindIndex(s=>s.To==proof.Milestones[0].Position)<proof.Forward.ToList().FindIndex(s=>s.To==proof.Milestones[1].Position),
            "actual path visits both required physical milestones in order");
        Check(proof.Forward[0].From==proof.From&&proof.Forward[^1].To==proof.To&&proof.Return![0].From==proof.To&&proof.Return[^1].To==proof.From,
            "both paths reach their declared access ports");
        Check(result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
            result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Reverse?.Path is not null),"complete source content and regional return routes retained");
        var planner=new MetadataRegionalJourneyPlanner();
        var baseline=planner.Plan([journey with{RequiredMilestones=null}],result.World).Journeys.Single();
        Check(Json(baseline.Forward)==Json(proof.Forward)&&baseline.Milestones is null,"omitting milestones preserves the original physical journey");
        Reject(()=>planner.Plan([journey with{RequiredMilestones=journey.RequiredMilestones!.Reverse().ToArray()}],result.World),"METADATA_REGIONAL_JOURNEY_MILESTONE_ORDER");
        Reject(()=>engine.Generate(request with{WorldJourneys=[journey with{RequiredMilestones=[journey.From]}]}),"METADATA_REGIONAL_JOURNEY_RULE_INVALID");
        // A controlled graph counterexample: the standalone planner does not
        // authenticate caller-supplied routes. This synthetic direct segment
        // tests bypass detection, not terrain qualification.
        var source=result.World.Connections[0];var bypass=source with{Definition=source.Definition with{Id="synthetic-bypass",From=journey.From,To=journey.To},
            Forward=source.Forward with{Id="synthetic-bypass/forward",Path=new WorldPolyline([proof.From,proof.To])},Reverse=null};
        var bypassWorld=result.World with{Connections=[..result.World.Connections,bypass]};
        Reject(()=>planner.Plan([journey],bypassWorld),"METADATA_REGIONAL_JOURNEY_MILESTONE_BYPASS");
        // Independent route traces meet at a point that is not a declared shared
        // port or qualified corridor junction. No switch is authorized there.
        var crossing=new WorldPoint(49,39);
        var first=bypass with{Definition=bypass.Definition with{Id="synthetic-first"},Forward=bypass.Forward with{
            Id="synthetic-first/forward",Path=new WorldPolyline([proof.From,crossing])}};
        var second=bypass with{Definition=bypass.Definition with{Id="synthetic-second"},Forward=bypass.Forward with{
            Id="synthetic-second/forward",Path=new WorldPolyline([crossing,proof.To])}};
        var crossingWorld=result.World with{Connections=[..result.World.Connections,first,second]};
        Check(planner.Plan([journey],crossingWorld).Journeys.Single().Forward.All(s=>!s.RouteId.StartsWith("synthetic-",StringComparison.Ordinal)),
            "coincident points on separate traces do not invent a junction or bypass");
        Reject(()=>planner.Plan([journey with{MaximumWork=1}],result.World),"METADATA_REGIONAL_JOURNEY_WORK_BUDGET");
        Reject(()=>planner.Plan([journey with{From=journey.From with{PortId="missing"}}],result.World),"METADATA_REGIONAL_JOURNEY_PORT_REQUIRED");
        Reject(()=>planner.Plan([journey with{Profile=profile with{Id="different"}}],result.World),"METADATA_REGIONAL_JOURNEY_UNREACHABLE");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan([journey],result.World,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"selected journey and rejected candidate replay");
        var tampered=result.Receipt with{Attempts=[attempts[0],attempts[1] with{WorldJourneys=attempts[1].WorldJourneys! with{Work=0}}]};
        Reject(()=>engine.Verify(tampered,result.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: a logical-only candidate is rejected; local and regional physical route segments connect both directions and replay.");
    }
}
