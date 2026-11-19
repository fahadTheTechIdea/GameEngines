using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldDomainJourneyTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Domain journeys: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static MetadataWorldDomainDefinition Example()
    {
        var source=WorldDomainTests.Example();var upper=source.Domains.Single(d=>d.Id=="upper");var known=new WorldMetadataGenerator().Generate(upper.Recipe,upper.Catalog);
        string exit="generated-interior/rooms-2";var door=known.InteriorPlan!.Results.SelectMany(r=>r.Openings).First(o=>o.FromId==exit||o.ToId==exit);
        var outside=source.Transitions.Single(t=>t.Id=="entry").From with{NodeId="local-layouts/sites-1",AccessPortId="west"};
        var end=new MetadataDomainEndpoint("upper",exit,InteriorConnectionId:door.ConnectionId,Profile:upper.Recipe.Interiors!.Rules[0].Profile);
        var profiles=source.Domains.ToDictionary(d=>d.Id,d=>d.Recipe.Interiors?.Rules[0].Profile??d.Recipe.RouteRules![0].Profile,StringComparer.Ordinal);
        return source with{Journeys=[new("complete-journey","Reach the final floor through actual local passages","journey:complete",outside,end,
            ["west-travel","door","doorway","stairs"],profiles,true)]};
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"definition.json"),Json(Example()));
    }
    private static void Continuity(MetadataWorldDomainDocument world,MetadataDomainEndpointResult from,MetadataDomainEndpointResult to,IReadOnlyList<MetadataDomainJourneyStep> steps)
    {
        string domain=from.Definition.DomainId;var position=new WorldPoint(from.Position![0],from.Position[1]);int local=0,transitions=0;
        foreach(var step in steps)
        {
            Check(step.DomainId==domain,"source domain continuity");
            if(step.Kind=="transition")
            {
                var source=world.Transitions.Single(t=>t.Definition.Id==step.SourceId);var a=step.Reverse?source.To:source.From;var b=step.Reverse?source.From:source.To;
                Check(a.Definition.DomainId==domain&&position==new WorldPoint(a.Position![0],a.Position[1]),"exact transition source endpoint");
                domain=b.Definition.DomainId;position=new(b.Position![0],b.Position[1]);Check(step.ToDomainId==domain,"explicit destination domain");transitions++;
            }
            else
            {
                Check(step.Kind=="route-segment","physical journey uses only source route segments");var doc=world.Domains.Single(d=>d.Id==domain).World;
                var routes=(doc.RoutePlan?.Routes??[]).Concat(doc.InteriorPlan?.Results.SelectMany(r=>r.Routes)??[]);
                var matches=routes.Select(r=>step.Reverse?r.Reverse:r.Forward).Where(r=>r?.Id==step.SourceId).ToArray();Check(matches.Length==1,"unique qualified route source");
                var points=matches[0]!.Path!.Points;Check(step.SegmentIndex>=0&&step.SegmentIndex+1<points.Count&&points[step.SegmentIndex]==position,"exact source segment start");
                position=points[step.SegmentIndex+1];local++;
            }
        }
        Check(domain==to.Definition.DomainId&&position==new WorldPoint(to.Position![0],to.Position[1]),"exact destination endpoint");
        Check(local>2&&transitions==2,"required local travel and both transitions retained");
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var definition=Example();var world=generator.GenerateDomains(definition);var journey=world.Journeys!.Single();
        Check(world.Domains.Count==3&&world.Domains.Sum(d=>d.World.Nodes.Count)==21,"all local content retained");
        Continuity(world,journey.From,journey.To,journey.Forward);Continuity(world,journey.To,journey.From,journey.Return!);
        var baseline=generator.GenerateDomains(definition with{Journeys=null});Check(Json(world.Domains)==Json(baseline.Domains)&&Json(world.Transitions)==Json(baseline.Transitions),"journey proof does not modify child worlds or transitions");
        generator.ValidateDomains(world);
        var requirement=definition.Journeys!.Single();
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[requirement with{ConnectionKinds=["west-travel","doorway","stairs"]}]}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[requirement with{Profiles=requirement.Profiles.ToDictionary(p=>p.Key,p=>p.Key=="lower"?p.Value with{Radius=.1}:p.Value,StringComparer.Ordinal)}]}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        Reject(()=>generator.GenerateDomains(definition with{Reachability=null,Transitions=definition.Transitions.Select(t=>t.Id=="stairs"?t with{Bidirectional=false}:t).ToArray()}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        Reject(()=>generator.GenerateDomains(definition with{MaximumGraphWork=world.GraphWork-1}),"METADATA_DOMAIN_GRAPH_WORK_BUDGET");
        Reject(()=>generator.GenerateDomains(definition with{MaximumTransitionWork=world.TransitionWork-1}),"METADATA_DOMAIN_TRANSITION_WORK_BUDGET");
        Check(generator.GenerateDomains(definition with{MaximumGraphWork=world.GraphWork,MaximumTransitionWork=world.TransitionWork}).GraphWork==world.GraphWork,"exact cumulative ledgers");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[]}),"METADATA_DOMAIN_JOURNEYS_INVALID");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[requirement with{Profiles=new Dictionary<string,WorldTraversalProfile>()}]}),"METADATA_DOMAIN_JOURNEY_PROFILE_MISMATCH");
        Reject(()=>generator.ValidateDomains(world with{Journeys=[journey with{Forward=journey.Forward.Skip(1).ToArray()}]}),"METADATA_DOMAIN_DOCUMENT_MISMATCH");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.GenerateDomains(definition,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string directory=Path.Combine(Path.GetTempPath(),"domain-journeys-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string input=Path.Combine(directory,"definition.json");
        await File.WriteAllTextAsync(input,Json(definition));await generator.GenerateDomainFilesAsync(input,Path.Combine(directory,"output"));
        var restored=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(directory,"output/world-domains.json"));Check(Json(restored)==Json(world),"source/route/transition/journey read replay");
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"logical-journey",[
            new("root","state","Logical entry",properties,[new("next","leaf",1,"Logical objective")],[new("local","local-step",MetadataConnectionPattern.Hub,"self","next")],[]),
            new("leaf","state","Logical objective",properties,[],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"logic","abstract-game",catalog.Id,"root","topology",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        var logical=new MetadataWorldDomainDefinition(1,"logical-journeys","Cross-domain abstract progression","journey:logic",
            [new("a","First logical domain","journey:a",recipe,catalog),new("b","Second logical domain","journey:b",recipe,catalog)],
            [new("portal","portal","Logical domain transition","journey:portal",new("a","logic/next-0"),new("b","logic"),true)],
            Journeys:[new("abstract-path","Required logical journey","journey:abstract",new("a","logic"),new("b","logic/next-0"),["local-step","portal"],new Dictionary<string,WorldTraversalProfile>(),true)]);
        var abstractWorld=generator.GenerateDomains(logical);var abstractPath=abstractWorld.Journeys!.Single();
        Check(abstractWorld.Domains.All(d=>d.World.Surface is null)&&abstractPath.From.Position is null&&abstractPath.Forward.Select(s=>s.Kind).SequenceEqual(new[]{"logical-connection","transition","logical-connection"})&&abstractPath.Return!.Count==3,"geometry-free logical connections and domain transitions need no physical profiles");
        generator.ValidateDomains(abstractWorld);
        var logicalRule=logical.Journeys!.Single();
        var checkpointA=new MetadataDomainEndpoint("a","logic/next-0");
        var checkpointB=new MetadataDomainEndpoint("b","logic");
        var withCheckpoints=logical with{Journeys=[logicalRule with{RequiredMilestones=[checkpointA,checkpointB]}]};
        var checkpointWorld=generator.GenerateDomains(withCheckpoints);
        Check(checkpointWorld.Journeys!.Single().Milestones!.Select(m=>m.Definition).SequenceEqual(new[]{checkpointA,checkpointB}),"ordered checkpoint witnesses saved");
        generator.ValidateDomains(checkpointWorld);
        Reject(()=>generator.GenerateDomains(logical with{Journeys=[logicalRule with{RequiredMilestones=[checkpointB,checkpointA]}]}),"METADATA_DOMAIN_JOURNEY_MILESTONE_ORDER");
        Reject(()=>generator.GenerateDomains(logical with{Journeys=[logicalRule with{RequiredMilestones=[checkpointA,checkpointA]}]}),"METADATA_DOMAIN_JOURNEY_MILESTONES_INVALID");
        var bypass=withCheckpoints with{Transitions=[..withCheckpoints.Transitions,new("shortcut","portal","Shortcut bypasses checkpoints","test:bypass",logicalRule.From,logicalRule.To,true)]};
        Reject(()=>generator.GenerateDomains(bypass),"METADATA_DOMAIN_JOURNEY_MILESTONE_BYPASS");
        var physicalCheckpoint=definition.Transitions.Single(t=>t.Id=="entry").From;
        var physicalDefinition=definition with{Journeys=[requirement with{RequiredMilestones=[physicalCheckpoint]}]};
        var physicalWorld=generator.GenerateDomains(physicalDefinition);
        Check(physicalWorld.Journeys!.Single().Milestones!.Single().Position is {Count:2},"physical checkpoint resolves qualified position");
        generator.ValidateDomains(physicalWorld);
        Reject(()=>generator.ValidateDomains(checkpointWorld with{Journeys=[checkpointWorld.Journeys!.Single() with{Milestones=[]}]}),"METADATA_DOMAIN_DOCUMENT_MISMATCH");
        var checkpointInput=Path.Combine(directory,"checkpoints.json");await File.WriteAllTextAsync(checkpointInput,Json(withCheckpoints));
        await generator.GenerateDomainFilesAsync(checkpointInput,Path.Combine(directory,"checkpoints"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(directory,"checkpoints/world-domains.json")))==Json(checkpointWorld),"checkpoint source and witness replay");
        Console.WriteLine($"PASS: three domains/21 nodes retain complete forward and return journeys using exact source segments and two transitions; independent continuity/domain/direction oracle, unchanged worlds, missing-kind/profile/return refusal, exact shared budgets, tamper/cancel and export/read replay. Forward steps={journey.Forward.Count}; return steps={journey.Return!.Count}; vertices={journey.Vertices}; edges={journey.Edges}; endpoint work={world.TransitionWork}; graph work={world.GraphWork}");
    }
}
