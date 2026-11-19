using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldDomainTransferTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Domain route transfer: "+reason);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var fixture=WorldRegionalCorridorJourneyTests.Example();var option=fixture.Slots.Single().Options.Single(o=>o.Id=="no-junction");
        var routeRule=option.Recipe.RouteRules!.Single(r=>r.Id=="corridor-routes");
        var aligned=routeRule.Alignments!.Select(a=>a.ConnectionId.EndsWith("/corridor-a",StringComparison.Ordinal)?a with{
            Path=new WorldPolyline([new(21.5,20),new(21.5,22),new(25.5,22),new(29,22),new(28.5,20)])}:a).ToArray();
        var recipe=option.Recipe with{RouteRules=option.Recipe.RouteRules!.Select(r=>r.Id==routeRule.Id?r with{Alignments=aligned}:r).ToArray()};
        var source=fixture.Template with{Regions=fixture.Template.Regions.Select(r=>r.Id=="arrival"?r with{Recipe=recipe,Catalog=option.Catalog}:r).ToArray()};
        var definition=new MetadataRegionalOwnershipPlanner().Plan(source);var region=definition.Regions.Single(r=>r.Id=="arrival");
        var physical=MetadataRegionalWorldGenerator.EffectiveRecipe(definition,region,default);var generator=new WorldMetadataGenerator();
        var observed=generator.Generate(physical,region.Catalog);var routes=observed.RoutePlan!.Routes.Where(r=>r.RuleId=="corridor-routes").ToArray();
        Check(routes.Length==2&&routes.All(r=>r.Forward.Path!.Points.Contains(new WorldPoint(25.5,22))),"two genuinely qualified routes share an interior point");
        var properties=new Dictionary<string,JsonElement>();var catalog=new WorldMetadataCatalog(1,"logical-domain",
            [new("root","state","Logical destination",properties,[],[],[])]);
        var logical=new WorldMetadataRecipe(1,"logic-root","framework",catalog.Id,"root","topology",MetadataCoverage.PartialScope,
            ["Framework test source"],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        var profile=fixture.WorldJourneys!.Single().Profile;
        var from=new MetadataDomainEndpoint("physical","region-west/compounds-0/modules-0","west",Profile:profile);
        var doorway=new MetadataDomainEndpoint("physical","region-west/compounds-0","east",Profile:profile);
        var target=new MetadataDomainEndpoint("logical","logic-root");
        var input=new MetadataWorldDomainDefinition(1,"source-qualified","Preserve distinct route identities","test:domains",
            [new("physical","Physical program","test:physical",physical,region.Catalog),new("logical","Logical destination","test:logical",logical,catalog)],
            [new("portal","portal","Explicit domain transition","test:portal",doorway,target,true)],
            Journeys:[new("journey","Reach the other domain","test:journey",from,target,["corridor-travel","portal"],
                new Dictionary<string,WorldCreator.Core.Spatial.WorldTraversalProfile>{{"physical",profile}},true)]);
        Directory.CreateDirectory(output);var closedInput=Path.Combine(output,"closed.json");await File.WriteAllTextAsync(closedInput,Json(input));
        try{await generator.GenerateDomainFilesAsync(closedInput,Path.Combine(output,"closed"));throw new Exception("Unrelated shared point became a junction");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_DOMAIN_JOURNEY_UNREACHABLE:",StringComparison.Ordinal)){}
        Check(!Directory.Exists(Path.Combine(output,"closed")),"refused world publishes no partial packet");
        var direct=physical with{AdditionalConnections=[..physical.AdditionalConnections!,new("direct","corridor-travel",
            "region-west/compounds-0/modules-0","region-west/compounds-0",true)]};
        var open=input with{Domains=input.Domains.Select(d=>d.Id=="physical"?d with{Recipe=direct}:d).ToArray()};
        var openInput=Path.Combine(output,"open.json");await File.WriteAllTextAsync(openInput,Json(open));
        var accepted=await generator.GenerateDomainFilesAsync(openInput,Path.Combine(output,"open"));var journey=accepted.Journeys!.Single();
        Check(journey.Forward.Any(s=>s.Kind=="route-segment"&&s.SourceId.Contains("/direct/",StringComparison.Ordinal))&&
            journey.Forward.Any(s=>s.Kind=="transition")&&journey.Return is {Count:>0},"explicit physical link and domain transition yield a two-way journey");
        Check(Json(await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(output,"open/world-domains.json")))==Json(accepted),"qualified domain output replays");
        var corridorRecipe=physical with{Corridors=fixture.Slots.Single().Options.Single(o=>o.Id=="qualified-junction").Recipe.Corridors};
        var corridorInput=input with{Domains=input.Domains.Select(d=>d.Id=="physical"?d with{Recipe=corridorRecipe}:d).ToArray()};
        var corridorFile=Path.Combine(output,"corridor.json");await File.WriteAllTextAsync(corridorFile,Json(corridorInput));
        var corridorWorld=await generator.GenerateDomainFilesAsync(corridorFile,Path.Combine(output,"corridor"));
        var proof=corridorWorld.Journeys!.Single();var physicalWorld=corridorWorld.Domains.Single(d=>d.Id=="physical").World;
        var group=physicalWorld.CorridorPlan!.Groups.Single();
        Check(proof.Forward.Any(s=>s.Kind=="corridor-segment")&&proof.Return!.Any(s=>s.Kind=="corridor-segment"),"qualified corridor connects both directions");
        void CheckSteps(IReadOnlyList<MetadataDomainJourneyStep> steps)
        {
            foreach(var step in steps.Where(s=>s.Kind=="corridor-segment"))
            {
                var segment=group.Segments.Single(s=>s.Id==step.CorridorSegmentId);bool forward=step.CorridorForward!.Value;
                Check(step.DomainId=="physical"&&step.CorridorGroupId==group.Definition.Id&&step.SegmentIndex==-1&&
                    (forward?segment.ForwardJourneys:segment.ReverseJourneys).Contains(step.SourceId),"source-qualified corridor direction and segment retained");
            }
        }
        CheckSteps(proof.Forward);CheckSteps(proof.Return!);
        var corridorLinks=group.Journeys.Select(j=>j.ConnectionId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Check(corridorLinks.Length>=2&&group.SharedDependencies.Count>0,"multiple physical source connections share corridor segments");
        var shared=group.Segments.First(s=>s.ForwardJourneys.Count>1||s.ReverseJourneys.Count>1);
        bool sharedForward=shared.ForwardJourneys.Count>1;var expectedSources=sharedForward?shared.ForwardJourneys:shared.ReverseJourneys;
        int vertexId=0;var tracks=group.Journeys.ToDictionary(j=>("physical",j.Id),j=>
            new MetadataCorridorRouteTrack<(WorldPoint Point,int Id)>((j.From,vertexId++),(j.To,vertexId++),j.Id.EndsWith("/reverse",StringComparison.Ordinal)));
        var emitted=new List<MetadataCorridorJourneyWitness>();
        MetadataCorridorJourneyGraph.Add(new Dictionary<string,WorldMetadataDocument>{{"physical",physicalWorld}},["corridor-travel"],
            new Dictionary<string,WorldTraversalProfile>{{"physical",group.Profile}},tracks,(_,point)=>(point,vertexId++),v=>v.Item1,
            (_,_,w)=>{if(w is not null)emitted.Add(w);},new(1000000,default));
        Check(expectedSources.All(id=>emitted.Any(w=>w.SegmentId==shared.Id&&w.Forward==sharedForward&&w.RouteId==id)),
            "every contributing source is preserved separately on a shared directed segment");
        var gates=corridorLinks.Select(id=>new MetadataDomainProgressionGate(new("physical",id),["key"])).ToArray();
        var gatedCorridor=corridorInput.Journeys!.Single() with{Capabilities=new(["key"],[],[new(from,["key"])],gates)};
        var qualifiedWorlds=corridorWorld.Domains.ToDictionary(d=>d.Id,d=>d.World,StringComparer.Ordinal);
        var gatedInput=corridorInput with{Journeys=[gatedCorridor]};var gatedFile=Path.Combine(output,"gated.json");await File.WriteAllTextAsync(gatedFile,Json(gatedInput));
        var gatedWorld=await generator.GenerateDomainFilesAsync(gatedFile,Path.Combine(output,"gated"));var gated=gatedWorld.Journeys!.Single();
        void CheckGated(IReadOnlyList<MetadataDomainJourneyStep> steps,IReadOnlyList<IReadOnlyList<string>> states)
        {
            Check(states.Count==steps.Count+1,"capability states align with each physical step");
            for(int i=0;i<steps.Count;i++)if(steps[i].Kind=="corridor-segment")
            {
                var s=steps[i];var original=group.Journeys.Single(j=>j.Id==s.SourceId);var segment=group.Segments.Single(x=>x.Id==s.CorridorSegmentId);
                Check(corridorLinks.Contains(original.ConnectionId)&&states[i].Contains("key")&&
                    (s.CorridorForward!.Value?segment.ForwardJourneys:segment.ReverseJourneys).Contains(s.SourceId),"gate and source direction preserved on each corridor segment");
            }
        }
        CheckGated(gated.Forward,gated.ForwardCapabilityStates!);CheckGated(gated.Return!,gated.ReturnCapabilityStates!);
        var firstSource=group.Journeys.Single(j=>j.Id==proof.Forward.First(s=>s.Kind=="corridor-segment").SourceId).ConnectionId;
        var singleGate=gatedCorridor with{Capabilities=new(["key"],[],[new(target,["key"])],
            [new(new("physical",firstSource),["key"])])};
        try{WorldMetadataGenerator.PlanDomainJourneys(corridorInput with{Journeys=[singleGate]},qualifiedWorlds,corridorWorld.Transitions,
            new(20000000,default),new(1000000,default),default);throw new Exception("Gated source bypassed through a shared segment");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_DOMAIN_JOURNEY_UNREACHABLE:",StringComparison.Ordinal)){}
        var inaccessible=gatedCorridor with{Capabilities=gatedCorridor.Capabilities! with{Grants=[new(target,["key"])]}};
        try{WorldMetadataGenerator.PlanDomainJourneys(corridorInput with{Journeys=[inaccessible]},qualifiedWorlds,corridorWorld.Transitions,
            new(20000000,default),new(1000000,default),default);throw new Exception("Inaccessible corridor key accepted");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_DOMAIN_JOURNEY_UNREACHABLE:",StringComparison.Ordinal)){}
        Check(Json(await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(output,"gated/world-domains.json")))==Json(gatedWorld),"gated corridor source replays");
        var closedGroup=group with{Junctions=group.Junctions.Select(j=>j with{Transitions=[]}).ToArray()};
        var closedWorld=physicalWorld with{CorridorPlan=physicalWorld.CorridorPlan! with{Groups=[closedGroup]}};
        var worlds=corridorWorld.Domains.ToDictionary(d=>d.Id,d=>d.Id=="physical"?closedWorld:d.World,StringComparer.Ordinal);
        try{WorldMetadataGenerator.PlanDomainJourneys(corridorInput,worlds,corridorWorld.Transitions,new(20000000,default),new(1000000,default),default);throw new Exception("Missing junction turns accepted");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_DOMAIN_JOURNEY_UNREACHABLE:",StringComparison.Ordinal)){}
        Check(Json(await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(output,"corridor/world-domains.json")))==Json(corridorWorld),"corridor and domain transition replay");
        Console.WriteLine("PASS: undeclared crossing rejects; explicit route or qualified directed corridor connects a two-domain return journey.");
    }
}
