using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldMixedDomainTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Mixed domains: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    internal static MetadataWorldDomainDefinition Example()
    {
        var outside=WorldRetainedWaterMetadataTests.Example();
        var walk=outside.Recipe.RouteRules![0].Profile;
        var recipe=outside.Recipe with{RouteRules=null,NetworkRules=[new("encounter-network","retained-world","landmark",2,2,"terrain-travel",
            new("Connect required encounters around retained water","mixed:network","land",walk,new(.05,[],MaximumWork:20000000),MaximumWork:40000000))]};
        var floor=WorldGeneratedInteriorTests.Example();var generator=new WorldMetadataGenerator();
        var interior=generator.Generate(floor.Recipe,floor.Catalog);
        MetadataDomainEndpoint Room(string id)=>new("interior",id,InteriorConnectionId:interior.InteriorPlan!.Results.Single().Openings.First(o=>o.FromId==id||o.ToId==id).ConnectionId,
            Profile:floor.Recipe.Interiors!.Rules[0].Profile);
        MetadataDomainEndpoint Destination(int index)=>new("terrain","retained-world/sites-"+index,Profile:walk,NetworkRuleId:"encounter-network");
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"progression-catalog",[
            new("root","progression","Geometry-free game progression",properties,[new("states","state",2,"Required progression state")],
                [new("enter","advance",MetadataConnectionPattern.Hub,"self","states")],[]),
            new("state","state","Required progression state",properties,[],[],[])]);
        var logic=new WorldMetadataRecipe(1,"progression","abstract-program",catalog.Id,"root","topology",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new(["phase"],"logical"));
        return new(1,"mixed-domain-reference","Terrain encounters, generated interior and abstract progression","mixed:program",
            [new("terrain","Terrain-aware outdoor program","mixed:terrain",recipe,outside.Catalog),
             new("interior","Complete generated room and fixture program","mixed:interior",floor.Recipe,floor.Catalog),
             new("progression","Independent logical state domain","mixed:progression",logic,catalog)],
            [new("enter","portal","Explicit game transition to local rooms","mixed:entry",Destination(1),Room("generated-interior/rooms-0"),true),
             new("advance","progression-gate","Explicit transition from physical to abstract state","mixed:advance",Room("generated-interior/rooms-5"),new("progression","progression"),true)],
            [new("round-trip","Required domain graph traversal","mixed:round-trip","terrain","progression",true)],
            Journeys:[new("complete","Complete physical and logical round trip","mixed:journey",Destination(0),new("progression","progression/states-1"),
                ["terrain-travel","door","portal","progression-gate","advance"],new Dictionary<string,WorldTraversalProfile>{{"terrain",walk},{"interior",floor.Recipe.Interiors!.Rules[0].Profile}},true)]);
    }

    // Independently walk each published source witness, including domain changes.
    private static void Trace(MetadataWorldDomainDocument document,MetadataDomainEndpointResult start,MetadataDomainEndpointResult end,IReadOnlyList<MetadataDomainJourneyStep> steps)
    {
        string domain=start.Definition.DomainId;double[]? point=start.Position?.ToArray();string? node=point is null?start.Definition.NodeId:null;
        bool At(MetadataDomainEndpointResult endpoint)=>domain==endpoint.Definition.DomainId&&(point is null?node==endpoint.Definition.NodeId:point.SequenceEqual(endpoint.Position!));
        int segments=0,logical=0,transitions=0;
        foreach(var step in steps)
        {
            Check(step.DomainId==domain,"source domain continuity");var local=document.Domains.Single(d=>d.Id==domain).World;
            if(step.Kind=="transition")
            {
                var transition=document.Transitions.Single(t=>t.Definition.Id==step.SourceId);
                Check(!step.Reverse||transition.Definition.Bidirectional,"transition direction");
                var from=step.Reverse?transition.To:transition.From;var to=step.Reverse?transition.From:transition.To;
                Check(At(from)&&step.ToDomainId==to.Definition.DomainId,"exact transition endpoints");
                domain=to.Definition.DomainId;point=to.Position?.ToArray();node=point is null?to.Definition.NodeId:null;transitions++;
            }
            else if(step.Kind=="logical-connection")
            {
                Check(local.Surface is null&&point is null,"logical domain stays geometry-free");
                var link=local.Connections.Single(c=>c.Id==step.SourceId);Check(!step.Reverse||link.Bidirectional,"logical direction");
                Check(node==(step.Reverse?link.ToId:link.FromId),"logical source");node=step.Reverse?link.FromId:link.ToId;logical++;
            }
            else
            {
                Check(step.Kind=="route-segment"&&point is not null,"physical source witness required");
                var routes=(local.InteriorPlan?.Results.SelectMany(r=>r.Routes).SelectMany(r=>new[]{r.Forward,r.Reverse})??[])
                    .Concat(local.NetworkPlan?.Networks.SelectMany(n=>n.Plan.Edges).SelectMany(e=>new[]{e.Forward,e.Reverse})??[]);
                var route=routes.Single(r=>r?.Id==step.SourceId)!;var a=route.Path!.Points[step.SegmentIndex];var b=route.Path.Points[step.SegmentIndex+1];
                Check(point!.SequenceEqual(new[]{a.X,a.Y}),"exact saved local segment start");point=[b.X,b.Y];segments++;
            }
        }
        Check(At(end)&&segments>6&&logical>0&&transitions==2,"complete mixed-domain journey without fabricated joins");
    }

    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"definition.json"),Json(Example()));
    }

    public static void NetworkEndpoints()
    {
        var outside=WorldPhysicalAccessTests.Example();var rule=outside.Recipe.NetworkRules!.Single();
        var source=Example();var logic=source.Domains.Single(d=>d.Id=="progression");
        MetadataDomainEndpoint Endpoint(int index)=>new("terrain","physical-access/sites-"+index,Profile:rule.Options.Profile,NetworkRuleId:rule.Id);
        var definition=source with{Id="solid-network-domains",Domains=[new("terrain","Exact solids and declared entrances","mixed:solids",outside.Recipe,outside.Catalog),logic],
            Transitions=[new("gate","portal","Explicit network entrance transition","mixed:solid-gate",Endpoint(0),new("progression","progression"),true)],
            Journeys=[new("solid-journey","Use terrain network from a solid entrance","mixed:solid-journey",Endpoint(2),new("progression","progression/states-1"),
                [rule.ConnectionKind,"portal","advance"],new Dictionary<string,WorldTraversalProfile>{{"terrain",rule.Options.Profile}},true)]};
        var generator=new WorldMetadataGenerator();var world=generator.GenerateDomains(definition);var endpoint=world.Transitions.Single().From;
        var terrain=world.Domains.Single(d=>d.Id=="terrain").World;
        var port=terrain.AccessPlan!.Sites.Single(s=>s.NodeId==endpoint.Definition.NodeId).Ports.Single(p=>p.Id==rule.PortId);
        Check(endpoint.Position!.SequenceEqual(new[]{port.ApproachPosition.X,port.ApproachPosition.Y})&&endpoint.AvailableWidth==2,"network endpoints preserve actual solid entrance coordinates and width");
        Check(!endpoint.Position!.SequenceEqual(terrain.Nodes.Single(n=>n.Id==endpoint.Definition.NodeId).Position!),"solid network endpoint never substitutes solid center");
        var exact=generator.GenerateDomains(definition with{MaximumTransitionWork=world.TransitionWork,MaximumGraphWork=world.GraphWork});
        Check(exact.TransitionWork==world.TransitionWork&&exact.GraphWork==world.GraphWork,"exact cumulative endpoint/journey budgets accepted");
        Reject(()=>generator.GenerateDomains(definition with{MaximumTransitionWork=world.TransitionWork-1}),"METADATA_DOMAIN_TRANSITION_WORK_BUDGET");
        Reject(()=>generator.GenerateDomains(definition with{MaximumGraphWork=world.GraphWork-1}),"METADATA_DOMAIN_GRAPH_WORK_BUDGET");
        var journey=definition.Journeys!.Single();
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{From=journey.From with{NetworkRuleId="../invalid"}}]}),"METADATA_DOMAIN_ENDPOINT_INVALID");
        Reject(()=>generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0] with{From=Endpoint(0) with{InteriorConnectionId="invented"}}]}),"METADATA_DOMAIN_ENDPOINT_INVALID");
        Reject(()=>generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0] with{From=Endpoint(0) with{Profile=rule.Options.Profile with{MaximumHeightStep=rule.Options.Profile.MaximumHeightStep+1}}}]}),"METADATA_DOMAIN_NETWORK_PROFILE_MISMATCH");
        Reject(()=>generator.GenerateDomains(definition with{Reachability=null,Transitions=[definition.Transitions[0] with{Bidirectional=false}]}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        var refined=rule.Options.Profile with{Purpose="Refined editorial intent",SourceRuleId="mixed:editorial"};
        var editorial=generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0] with{From=Endpoint(0) with{Profile=refined}}]});
        Check(Json(editorial.Journeys)==Json(world.Journeys),"editorial source text does not change movement compatibility");
        generator.ValidateDomains(world);
        Console.WriteLine("PASS: solid network destinations retain actual entrance approach/width; complete network/logical round trip, exact and exceeded cumulative budgets, exclusive selectors, movement-profile identity, editorial invariance and one-way return refusal.");
    }

    public static async Task RunAsync()
    {
        var definition=Example();var generator=new WorldMetadataGenerator();var session=new MetadataDomainGenerationSession(generator,definition);var initial=session.Snapshot;
        void Inspect(MetadataWorldDomainDocument document)
        {
            Check(document.Domains.Sum(d=>d.World.Nodes.Count)==19,"all encounters, rooms, fixtures and logical states retained");
            var terrain=document.Domains.Single(d=>d.Id=="terrain").World;var interior=document.Domains.Single(d=>d.Id=="interior").World;
            var logic=document.Domains.Single(d=>d.Id=="progression").World;
            Check(terrain.WaterPlan!.RetainedBodies!.Count==1&&terrain.WindMoisturePlan is not null&&terrain.RegionPlan is not null,"actual optional terrain/environment pipeline");
            Check(terrain.NetworkPlan!.Networks.Single().Plan.Edges.Single().Forward.Path!.Points.Count>2,"network bends around occupied lake");
            Check(interior.InteriorPlan!.Results.Single().Openings.Count==5&&interior.Nodes.Count(n=>n.Kind=="equipment")==6,"every generated opening and fixture retained");
            Check(logic.Surface is null&&logic.Nodes.All(n=>n.Position is null)&&logic.Space.Axes.Count==1,"logical domain has no imposed terrain or coordinates");
            var journey=document.Journeys!.Single();Trace(document,journey.From,journey.To,journey.Forward);Trace(document,journey.To,journey.From,journey.Return!);
            Check(journey.From.AvailableWidth is null,"marker destination has no fabricated entrance width");
            var source=terrain.NetworkPlan.Networks.Single().Plan.Destinations.Single(d=>d.Id==journey.From.Definition.NodeId);
            Check(journey.From.Position!.SequenceEqual(new[]{source.Position.X,source.Position.Y}),"exact saved network destination");
        }
        Inspect(initial.World);generator.ValidateDomains(initial.World);
        var journey=definition.Journeys!.Single();
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{From=journey.From with{NetworkRuleId="missing"}}]}),"METADATA_DOMAIN_NETWORK_REQUIRED");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{From=journey.From with{NodeId="retained-world"}}]}),"METADATA_DOMAIN_NETWORK_DESTINATION_REQUIRED");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{From=journey.From with{AccessPortId="invented"}}]}),"METADATA_DOMAIN_ENDPOINT_INVALID");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{From=journey.From with{NetworkRuleId=null}}]}),"METADATA_DOMAIN_PHYSICAL_ENDPOINT_REQUIRED");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{From=journey.From with{Profile=journey.From.Profile! with{Radius=.1}}}]}),"METADATA_DOMAIN_NETWORK_PROFILE_MISMATCH");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{To=journey.To with{NetworkRuleId="encounter-network"}}]}),"METADATA_DOMAIN_LOGICAL_ENDPOINT_INVALID");
        Reject(()=>generator.GenerateDomains(definition with{Journeys=[journey with{ConnectionKinds=journey.ConnectionKinds.Where(k=>k!="door").ToArray()}]}),"METADATA_DOMAIN_JOURNEY_UNREACHABLE");
        var changed=definition with{Domains=definition.Domains.Select(d=>d.Id=="terrain"?d with{Recipe=d.Recipe with{
            Water=d.Recipe.Water! with{RetainedBasins=[d.Recipe.Water.RetainedBasins![0] with{Level=1.5}]}}}:d).ToArray()};
        var edited=session.Apply(new("lake-level","Raise authored water level; preserve independent domains","mixed:level",initial.World.DefinitionSha256,changed));
        Inspect(edited.World);Check(Json(edited.World)==Json(generator.GenerateDomains(changed)),"cached mixed-domain edit equals clean generation");
        Check(edited.Report.Domains.Where(d=>d.DomainId!="terrain").All(d=>d.Stages.All(s=>s.Reused)),"all unrelated domain stages reused");
        Check(!edited.Report.Domains.Single(d=>d.DomainId=="terrain").Stages.Single(s=>s.Id=="networks").Reused,"lake edit recomputes actual terrain network");
        var impossible=changed with{Journeys=[journey with{ConnectionKinds=journey.ConnectionKinds.Where(k=>k!="terrain-travel").ToArray()}]};
        try{session.Apply(new("disconnect","Reject disconnected full-world journey","mixed:failure",edited.World.DefinitionSha256,impossible));throw new Exception("Missing failed edit");}
        catch(MetadataRegenerationException e)when(e.InnerException is ArgumentException cause&&cause.Message.StartsWith("METADATA_DOMAIN_JOURNEY_UNREACHABLE",StringComparison.Ordinal)){}
        Check(Json(session.Snapshot)==Json(edited),"failed journey preserves accepted world, cache and report");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{session.Apply(new("cancel","Cancelled proposal","mixed:cancel",edited.World.DefinitionSha256,definition),cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        Check(Json(session.Snapshot)==Json(edited),"cancelled proposal preserves accepted collection");
        var recovered=session.Apply(new("recover","Reuse accepted state after failure","mixed:recover",edited.World.DefinitionSha256,changed));
        Check(recovered.Report.Domains.All(d=>d.Stages.All(s=>s.Reused)),"failed proposals do not poison caches");
        string output=Path.Combine("generated","mixed-domains-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(output,"world-domains.json"));Check(Json(restored)==Json(recovered.World),"strict published source replay");
        var checkpoint=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"domain-session.json"));Check(Json(checkpoint.Snapshot)==Json(recovered),"full source-history checkpoint replay");
        Reject(()=>generator.ValidateDomains(restored with{Journeys=[restored.Journeys![0] with{Forward=restored.Journeys[0].Forward.Skip(1).ToArray()}]}),"METADATA_DOMAIN_DOCUMENT_MISMATCH");
        Console.WriteLine("PASS: 19-node terrain/lake/wind/network, six generated rooms/fixtures and geometry-free progression; exact forward/return source continuity, endpoint/profile refusals, clean edits, domain cache isolation, failure/cancel rollback, publication and checkpoint. Packet: "+output);
    }
}
