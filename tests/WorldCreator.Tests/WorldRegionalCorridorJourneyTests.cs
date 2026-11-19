using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalCorridorJourneyTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string message){if(!value)throw new Exception("Regional corridor journey: "+message);}
    static void Reject(Action run,string code){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static MetadataRegionalDesignRequest Example()
    {
        var template=WorldRegionalNetworkTests.Example();var arrival=template.Regions.Single(r=>r.Id=="arrival");
        const string parent="region-west/compounds-0",first="region-west/compounds-0/modules-0",second="region-west/compounds-0/modules-1";
        var profile=template.NetworkRules!.Single().Options.Profile;
        var a=new WorldPolyline([new(21.5,20),new(21.5,22),new(29,22),new(28.5,20)]);
        var b=new WorldPolyline([new(25.5,20),new(25.5,22),new(30,22),new(32,20)]);
        var local=arrival.Recipe with{
            AdditionalConnections=[..arrival.Recipe.AdditionalConnections!,new("corridor-a","corridor-travel",first,second,true),
                new("corridor-b","corridor-travel",second,parent,true)],
            RouteRules=[..arrival.Recipe.RouteRules!,new("corridor-routes","corridor-travel",profile,new(.1,[]),"west","east",
                Alignments:[new("region-west/explicit/corridor-a",a,"test:alignment-a"),new("region-west/explicit/corridor-b",b,"test:alignment-b")])]};
        var connected=local with{Corridors=new([new("local-junctions","corridor-travel","test:junctions",0,"connect-at-grade")])};
        var journey=new MetadataRegionalJourneyRule("journey","Travel through a qualified junction","test:journey",
            new("arrival",first,"west"),new("outpost",parent,"east"),["corridor-travel","regional-travel"],profile,true);
        return new MetadataRegionalDesignRequest(1,"corridor-journey","Select qualified route junctions","test:world",template,
            [new("arrival",[new("no-junction",0,local,arrival.Catalog),new("qualified-junction",1,connected,arrival.Catalog)])],[],2,WorldJourneys:[journey]);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var request=Example();var journey=request.WorldJourneys!.Single();
        var engine=new MetadataRegionalDesignGenerator();var result=engine.Generate(request);var attempts=result.Receipt.Attempts;
        Check(attempts.Count==2&&!attempts[0].Accepted&&attempts[0].Rejection=="METADATA_REGIONAL_JOURNEY_UNREACHABLE:journey"&&
            attempts[1].Accepted&&attempts[1].Selections.Single().OptionId=="qualified-junction","only the explicit qualified junction connects the paths");
        var world=result.World;var region=world.Regions.Single(r=>r.RegionId=="arrival").World;var group=region.CorridorPlan!.Groups.Single();
        var proof=attempts[1].WorldJourneys!.Journeys.Single();
        Check(proof.Forward.Any(s=>s.CorridorSegmentId is not null)&&proof.Return!.Any(s=>s.CorridorSegmentId is not null),"both directions use actual corridor segments");
        Check(group.Junctions.Any(j=>j.Transitions.Count>2)&&group.SharedDependencies.Count>0,"qualified corridor has shared segments and explicit turns");
        void CheckPath(IReadOnlyList<MetadataRegionalJourneyStep> steps)
        {
            for(int i=1;i<steps.Count;i++)Check(steps[i-1].To==steps[i].From,"saved physical steps are continuous");
            foreach(var step in steps.Where(s=>s.CorridorSegmentId is not null))
            {
                var segment=group.Segments.Single(s=>s.Id==step.CorridorSegmentId);bool direction=step.CorridorForward!.Value;
                Check((direction?segment.From:segment.To)==step.From&&(direction?segment.To:segment.From)==step.To&&
                    (direction?segment.ForwardJourneys:segment.ReverseJourneys).Contains(step.RouteId),"corridor witness names a real permitted segment");
            }
        }
        CheckPath(proof.Forward);CheckPath(proof.Return!);
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==20&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&world.Connections.Count==3,
            "all regional content remains");
        var planner=new MetadataRegionalJourneyPlanner();
        Reject(()=>planner.Plan([journey with{MaximumWork=1}],world),"METADATA_REGIONAL_JOURNEY_WORK_BUDGET");
        var noGroups=region with{CorridorPlan=null};var altered=world with{Regions=world.Regions.Select(r=>r.RegionId=="arrival"?r with{World=noGroups}:r).ToArray()};
        Reject(()=>planner.Plan([journey],altered),"METADATA_REGIONAL_JOURNEY_UNREACHABLE");
        var noTurns=group with{Junctions=group.Junctions.Select(j=>j with{Transitions=[]}).ToArray()};
        var closed=region with{CorridorPlan=region.CorridorPlan! with{Groups=[noTurns]}};
        var closedWorld=world with{Regions=world.Regions.Select(r=>r.RegionId=="arrival"?r with{World=closed}:r).ToArray()};
        Reject(()=>planner.Plan([journey],closedWorld),"METADATA_REGIONAL_JOURNEY_UNREACHABLE");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"qualified junction selection replays");
        Console.WriteLine("PASS: explicit qualified shared corridor connects regional journey in both directions; undeclared crossing does not.");
    }
}
