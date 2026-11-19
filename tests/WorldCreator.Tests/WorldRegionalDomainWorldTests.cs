using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalDomainWorldTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional domains: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=WorldRegionalNetworkTests.Example();var regional=new MetadataRegionalOwnershipPlanner().Plan(source);
        var profile=source.NetworkRules!.Single().Options.Profile;
        var catalog=new WorldMetadataCatalog(1,"regional-domain-logic",[new("root","state","Independent destination",new Dictionary<string,JsonElement>(),[],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"logical-root","framework",catalog.Id,"root","topology",MetadataCoverage.PartialScope,
            ["Framework test"],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        var extra=new MetadataWorldDomain("logical","Independent game domain","test:logical",recipe,catalog);
        const string node="region-west/compounds-0";
        var arrival=new MetadataDomainEndpoint("arrival",node,"east",Profile:profile);
        var from=new MetadataDomainEndpoint("outpost",node,"east",Profile:profile);
        var to=new MetadataDomainEndpoint("logical","logical-root");
        var profiles=regional.Regions.ToDictionary(r=>r.Id,_=>profile,StringComparer.Ordinal);
        var request=new MetadataRegionalDomainWorldDefinition(1,"regional-and-domain","Regional travel to another world domain","test:combined",
            regional,[extra],[new("portal","portal","Explicit game transition","test:portal",arrival,to,true)],
            Journeys:[new("journey","Travel through the regional network and return","test:journey",from,to,
                ["regional-travel","portal"],profiles,true)]);
        var engine=new MetadataRegionalDomainWorldGenerator();var world=engine.Generate(request);
        Reject(()=>engine.Generate(request with{AdditionalDomains=[extra with{Id="arrival"}]}),"METADATA_REGIONAL_DOMAIN_ID_INVALID");
        Check(world.RegionalWorld.Regions.Count==4&&world.Domains.Domains.Count==5&&world.RegionalRoutes.Count>0,"regional content and independent domain retained");
        var journey=world.Domains.Journeys!.Single();
        void CheckRoute(IReadOnlyList<MetadataDomainJourneyStep> steps)
        {
            var edges=steps.Where(s=>s.Kind=="transition"&&s.SourceId.StartsWith("regional-route-",StringComparison.Ordinal)).ToArray();
            Check(edges.Length>0&&steps.Any(s=>s.SourceId=="portal"),"regional route and explicit portal both present");
            foreach(var edge in edges)
            {
                var witness=world.RegionalRoutes.Single(r=>r.TransitionId==edge.SourceId);var route=edge.Reverse?witness.Reverse:witness.Forward;
                var link=world.Domains.Transitions.Single(t=>t.Definition.Id==edge.SourceId);
                var a=edge.Reverse?link.To:link.From;var b=edge.Reverse?link.From:link.To;
                Check(route?.Path is not null&&route.Path.Points[0].X==a.Position![0]&&route.Path.Points[0].Y==a.Position[1]&&
                    route.Path.Points[^1].X==b.Position![0]&&route.Path.Points[^1].Y==b.Position[1],"exact source route endpoints and direction");
            }
        }
        CheckRoute(journey.Forward);CheckRoute(journey.Return!);
        var localWorlds=world.Domains.Domains.ToDictionary(d=>d.Id,d=>d.World,StringComparer.Ordinal);
        Reject(()=>WorldMetadataGenerator.PlanDomainJourneys(world.Domains.Definition,localWorlds,
            world.Domains.Transitions.Where(t=>t.Definition.Id!="portal").ToArray(),new(20000000,default),new(1000000,default),default),
            "METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        Reject(()=>engine.Validate(world with{RegionalRoutes=world.RegionalRoutes.Skip(1).ToArray()}),"METADATA_REGIONAL_DOMAIN_DOCUMENT_MISMATCH");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{engine.Generate(request,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        await engine.PublishAsync(world,output);
        Check(File.Exists(Path.Combine(output,"regions/schematic.html"))&&File.Exists(Path.Combine(output,"domains/index.html")),"both existing inspectors published");
        var restored=await engine.ReadAsync(Path.Combine(output,"world.json"));Check(Json(restored)==Json(world),"published composition replays");
        try{await engine.PublishAsync(world,output);throw new Exception("Existing output overwritten");}catch(IOException){}
        Console.WriteLine("PASS: generated regional terrain routes connect an independent domain in both directions; route endpoints, replay, tamper and cancellation verified.");
    }
}
