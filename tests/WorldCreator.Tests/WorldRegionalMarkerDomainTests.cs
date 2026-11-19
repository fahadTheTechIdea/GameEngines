using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalMarkerDomainTests
{
    private static void Check(bool value,string reason){if(!value)throw new Exception("Regional markers: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());
        source=source with{Regions=source.Regions.Select(r=>r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a with{
            Parts=a.Parts.Select(p=>p.Id=="markers"?p with{Layout=MetadataLineLayout.Parameters([0,30],[1,0])}:p).ToArray()}).ToArray()}}).ToArray()};
        var road=source.Connections.Single() with{From=new("west","region-west/markers-0"),To=new("east","region-east/markers-0")};
        source=source with{Connections=[road]};var profile=road.Profile;
        var catalog=new WorldMetadataCatalog(1,"marker-domain",[new("root","state","Another domain",new Dictionary<string,JsonElement>(),[],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"root","framework",catalog.Id,"root","topology",MetadataCoverage.PartialScope,["Framework test"],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        var start=new MetadataDomainEndpoint("west","region-west/markers-0",Profile:profile,UseNodePosition:true);
        var portal=new MetadataDomainEndpoint("east","region-east/markers-0",Profile:profile,UseNodePosition:true);
        var goal=new MetadataDomainEndpoint("logic","root");
        var request=new MetadataRegionalDomainWorldDefinition(1,"regional-marker-world","Connect outdoor markers and an independent domain","test:markers",
            source,[new("logic","Logical domain","test:logic",recipe,catalog)],[new("portal","portal","Explicit domain change","test:portal",portal,goal,true)],
            Journeys:[new("journey","Use the marker route and portal","test:journey",start,goal,["travel","portal"],
                new Dictionary<string,WorldTraversalProfile>{{"west",profile},{"east",profile}},true)]);
        var engine=new MetadataRegionalDomainWorldGenerator();var document=engine.Generate(request);
        var route=document.RegionalRoutes.Single();var transition=document.Domains.Transitions.Single(t=>t.Definition.Id==route.TransitionId);
        Check(transition.From.Definition.UseNodePosition&&transition.To.Definition.UseNodePosition&&transition.From.AvailableWidth is null&&transition.To.AvailableWidth is null,"marker endpoints retain selector and no fabricated width");
        Check(route.Forward.Path!.Points[0].X==transition.From.Position![0]&&route.Forward.Path.Points[0].Y==transition.From.Position[1]&&
            route.Forward.Path.Points[^1].X==transition.To.Position![0]&&route.Forward.Path.Points[^1].Y==transition.To.Position[1],"regional route uses the actual marker points");
        var journey=document.Domains.Journeys!.Single();
        Check(journey.Forward.Any(s=>s.SourceId==route.TransitionId)&&journey.Return!.Any(s=>s.SourceId==route.TransitionId&&s.Reverse)&&
            journey.Forward.Any(s=>s.SourceId=="portal"),"both directions use the qualified route and explicit portal");
        var west=document.Domains.Domains.Single(d=>d.Id=="west").World;
        var solid=start with{NodeId="region-west/compounds-0/modules-0"};
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(solid,west,new(20000000,default),default),"METADATA_ACCESS_PORT_REQUIRED");
        var marker=west.Nodes.Single(n=>n.Id==start.NodeId);
        var blocked=west with{Nodes=west.Nodes.Select(n=>n.Id==marker.Id?n with{Position=[-20,-20]}:n).ToArray()};
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(start,blocked,new(20000000,default),default),"METADATA_DOMAIN_ENDPOINT_BLOCKED");
        var center=west.AccessPlan!.Sites.Single(s=>s.NodeId==solid.NodeId).Footprint.Centroid();
        var occupied=west with{Nodes=west.Nodes.Select(n=>n.Id==marker.Id?n with{Position=[center.X,center.Y]}:n).ToArray()};
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(start,occupied,new(20000000,default),default),"METADATA_DOMAIN_ENDPOINT_BLOCKED");
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(start with{Profile=profile with{KindCosts=new Dictionary<string,double>{{"water",1}}}},west,new(20000000,default),default),"METADATA_DOMAIN_ENDPOINT_BLOCKED");
        var missing=west with{Nodes=west.Nodes.Select(n=>n.Id==marker.Id?n with{Position=null}:n).ToArray()};
        Reject(()=>WorldMetadataGenerator.ResolveDomainEndpoint(start,missing,new(20000000,default),default),"METADATA_ROUTE_ENDPOINT_REQUIRED");
        var conflicting=document.Domains.Definition.Transitions.Select(t=>t.Id=="portal"?t with{From=t.From with{AccessPortId="invented"}}:t).ToArray();
        Reject(()=>new WorldMetadataGenerator().GenerateDomains(document.Domains.Definition with{Transitions=conflicting}),"METADATA_DOMAIN_ENDPOINT_INVALID");
        var logical=document.Domains.Definition.Transitions.Select(t=>t.Id=="portal"?t with{To=t.To with{UseNodePosition=true}}:t).ToArray();
        Reject(()=>new WorldMetadataGenerator().GenerateDomains(document.Domains.Definition with{Transitions=logical}),"METADATA_DOMAIN_LOGICAL_ENDPOINT_INVALID");
        await engine.PublishAsync(document,output);var restored=await engine.ReadAsync(Path.Combine(output,"world.json"));
        Check(restored.RegionalRoutes.Count==1&&restored.Domains.Journeys!.Single().Return!.Count>0,"published result replays");
        Console.WriteLine("PASS: physical marker-to-marker regional route and domain round trip, solid/blocked/unpositioned/conflicting/logical refusal and publication replay.");
    }
}
