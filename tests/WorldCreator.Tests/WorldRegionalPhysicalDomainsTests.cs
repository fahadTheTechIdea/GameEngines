using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalPhysicalDomainsTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Regional physical domains: "+reason);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var regional=new MetadataRegionalOwnershipPlanner().Plan(template);
        var floorSource=WorldDomainJourneyTests.Example();var indoor=floorSource.Domains.Where(d=>d.Id is "lower" or "upper").ToArray();
        var regionProfile=template.NetworkRules!.Single().Options.Profile;
        const string node="region-west/compounds-0";
        var exterior=new MetadataDomainEndpoint("arrival",node,"east",Profile:regionProfile);
        var start=new MetadataDomainEndpoint("outpost",node,"east",Profile:regionProfile);
        var end=floorSource.Journeys!.Single().To;
        var transitions=floorSource.Transitions.Select(t=>t.Id=="entry"?t with{From=exterior}:t).ToArray();
        var profiles=regional.Regions.ToDictionary(r=>r.Id,_=>regionProfile,StringComparer.Ordinal);
        foreach(var floor in indoor)profiles.Add(floor.Id,floor.Recipe.Interiors!.Rules[0].Profile);
        var input=new MetadataRegionalDomainWorldDefinition(1,"regional-physical-domains","Connect regional locations to independent physical floors","test:physical-world",
            regional,indoor,transitions,Journeys:[new("journey","Travel from the region into both floors and return","test:journey",start,end,
                ["regional-travel","door","doorway","stairs"],profiles,true)]);
        var engine=new MetadataRegionalDomainWorldGenerator();var world=engine.Generate(input);var journey=world.Domains.Journeys!.Single();
        void Trace(MetadataDomainEndpointResult first,MetadataDomainEndpointResult last,IReadOnlyList<MetadataDomainJourneyStep> steps)
        {
            string domain=first.Definition.DomainId;var point=new WorldPoint(first.Position![0],first.Position[1]);int region=0,floor=0,portals=0;
            foreach(var step in steps)
            {
                Check(step.DomainId==domain,"domain continuity");
                if(step.Kind=="transition")
                {
                    var link=world.Domains.Transitions.Single(t=>t.Definition.Id==step.SourceId);var from=step.Reverse?link.To:link.From;var to=step.Reverse?link.From:link.To;
                    Check(!step.Reverse||link.Definition.Bidirectional,"transition direction");
                    Check(from.Position is {Count:2}&&point==new WorldPoint(from.Position[0],from.Position[1]),"transition begins at current qualified position");
                    var route=world.RegionalRoutes.SingleOrDefault(r=>r.TransitionId==step.SourceId);
                    if(route is not null)
                    {
                        var actual=step.Reverse?route.Reverse:route.Forward;
                        Check(actual?.Path is not null&&actual.Path.Points[0]==point&&actual.Path.Points[^1]==new WorldPoint(to.Position![0],to.Position[1]),"regional edge follows exact source route");region++;
                    }
                    else {Check(step.SourceId is "entry" or "stairs","only explicitly authored floor changes");portals++;}
                    domain=to.Definition.DomainId;point=new(to.Position![0],to.Position[1]);
                }
                else
                {
                    Check(step.Kind=="route-segment","interior movement uses qualified route segments");
                    var local=world.Domains.Domains.Single(d=>d.Id==domain).World;
                    var routes=(local.RoutePlan?.Routes??[]).Concat(local.InteriorPlan?.Results.SelectMany(r=>r.Routes)??[]);
                    var actual=routes.Select(r=>step.Reverse?r.Reverse:r.Forward).Single(r=>r?.Id==step.SourceId)!;
                    var points=actual.Path!.Points;Check(step.SegmentIndex>=0&&step.SegmentIndex+1<points.Count&&points[step.SegmentIndex]==point,"qualified local segment begins at current position");
                    point=points[step.SegmentIndex+1];floor++;
                }
            }
            Check(domain==last.Definition.DomainId&&point==new WorldPoint(last.Position![0],last.Position[1])&&region>0&&floor>1&&portals==2,"continuous regional, floor and transition witnesses");
        }
        Trace(journey.From,journey.To,journey.Forward);Trace(journey.To,journey.From,journey.Return!);
        var worlds=world.Domains.Domains.ToDictionary(d=>d.Id,d=>d.World,StringComparer.Ordinal);
        try{WorldMetadataGenerator.PlanDomainJourneys(world.Domains.Definition,worlds,world.Domains.Transitions.Where(t=>t.Definition.Id!="stairs").ToArray(),
            new(20000000,default),new(1000000,default),default);throw new Exception("Missing floor transition accepted");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_DOMAIN_JOURNEY_UNREACHABLE:",StringComparison.Ordinal)){}
        await engine.PublishAsync(world,output);
        var index=await File.ReadAllTextAsync(Path.Combine(output,"index.html"));
        Check(index.Contains("Regional route ",StringComparison.Ordinal)&&index.Contains("Explicit domain change doorway",StringComparison.Ordinal)&&
            index.Contains("Explicit domain change stairs",StringComparison.Ordinal)&&index.Contains("route-segment",StringComparison.Ordinal),"inspector distinguishes generated geometry and explicit transitions");
        var replay=await engine.ReadAsync(Path.Combine(output,"world.json"));
        Check(Json(replay)==Json(world),"source, paths and floors replay");
        Console.WriteLine("PASS: four-region world, physical two-floor interiors, exact directional route continuity, missing stairs refusal and publication replay.");
    }
}
