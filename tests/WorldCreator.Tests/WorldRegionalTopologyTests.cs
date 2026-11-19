using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalTopologyTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional topology: "+message);}
    private static void Refuse(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing refusal: "+prefix);}
    internal static MetadataRegionalOwnershipRequest Example(string seed="world-grammar-1")
    {
        var original=WorldRegionalOwnershipTests.Example();var template=original.Regions[0];
        var ids=new[]{"arrival","grove","outpost","pass","ruins","destination"};
        var kinds=new[]{"arrival","early-site","early-site","transition","late-site","goal"};
        var regions=ids.Select(id=>template with{Id=id,Purpose="Complete required "+id+" program",SourceRuleId="world-grammar:"+id}).ToArray();
        var destinations=ids.Select((id,i)=>new MetadataRegionalTopologyDestination(id,kinds[i],"Purposeful "+id,"world-grammar:"+id,
            new(id,"region-west/compounds-0","east"))).ToArray();
        string Node(string id)=>MetadataTopologyPlanner.RegionalDestinationId(original.Id,id);
        var rule=new MetadataTopologyRule("progressive-journeys","Connected regions with a required pass","world-grammar:progression",original.Id,Node("arrival"),"regional-trail",
            [new("exploration","Visit early sites before the pass",Node("pass"),["early-site"],2,2),
             new("destination","Proceed beyond the pass",Node("destination"),["late-site"],1,1)],seed,MaximumDegree:4,ExtraConnections:2);
        return original with{Regions=regions,Connections=[],Topology=new(1,"world-journeys","Generate staged world connections","world-grammar:topology",destinations,
            [new(rule,original.Connections[0].Profile,original.Connections[0].Search)])};
    }
    internal static async Task RunAsync(string output,bool directed=false,int returnConnections=0)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        MetadataRegionalOwnershipRequest Prepare(string seed)
        {
            var input=Example(seed);
            return directed?input with{Topology=input.Topology! with{Rules=[input.Topology.Rules[0] with{Topology=input.Topology.Rules[0].Topology with{Directed=true,ReturnConnections=returnConnections}}]}}:input;
        }
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();var source=Prepare("world-grammar-1");string original=Json(source);
        var definition=planner.Plan(source);var proof=definition.TopologyPlan!;var world=engine.Generate(definition);
        Check(Json(source)==original,"planning never mutates source");
        Check(source.Connections.Count==0&&definition.Connections.Count==7&&proof.Links.Count==7,"all seven macro links generated from grammar, including two exact loops");
        Check(world.Regions.Count==6&&world.Regions.Sum(r=>r.World.Nodes.Count)==30&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==18,"six complete local compound programs, all nested source members");
        Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&
            (directed?c.Reverse is null:c.Reverse?.Status==WorldRouteStatus.Found)),"all declared macro path directions physically qualified");
        Check(proof.Topology.Results.Single().Connections.All(c=>c.Bidirectional!=directed),"logical edge directions match declaration");
        Check(proof.Topology.Results.Single().Connections.Count(c=>c.Properties.TryGetValue("return",out var v)&&v.GetBoolean())==returnConnections,"exact return connections");
        Check(definition.Connections.All(c=>c.Bidirectional!=directed),"regional edge directions match topology");
        Check(proof.Topology.Results.Single().Connections.Count(c=>c.Properties["reason"].GetString()=="stage-loop")==2,"exact loop count");
        var canonical=source with{Regions=source.Regions.Reverse().ToArray(),Topology=source.Topology! with{Destinations=source.Topology.Destinations.Reverse().ToArray()}};
        Check(Json(planner.Plan(canonical))==Json(definition),"source declaration order has one canonical result");
        await engine.PublishAsync(world,Path.Combine(output,"initial"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"initial/world-regions.json")))==Json(world),"saved world replays topology, real endpoint bindings and geometry");
        var changedSource=Prepare("world-grammar-2");var changed=planner.Plan(changedSource);var editedWorld=engine.Generate(changed);
        Check(Json(changed.TopologyPlan!.Topology.Results.Single().Connections.Select(c=>new{c.FromId,c.ToId}))!=Json(proof.Topology.Results.Single().Connections.Select(c=>new{c.FromId,c.ToId})),"seed changes permitted branch topology");
        Check(Json(world.Regions)==Json(editedWorld.Regions),"topology seed edit preserves every exact complete local packet");
        await engine.PublishAsync(editedWorld,Path.Combine(output,"changed"));
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),source);var initial=session.Snapshot;
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest next)=>new(id,"Revise world grammar only","world-grammar:edit",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,next);
        var accepted=await session.ApplyDerivedAndPublishAsync(Edit("seed-change",changedSource),Path.Combine(output,"session"));
        Check(Json(accepted.World)==Json(editedWorld)&&accepted.Report.Regions.All(r=>r.Reused),"actual source edit reuses all exact local packets and reproduces whole generation");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(accepted),"saved source edit restores exact history");
        var impossible=source with{Topology=source.Topology! with{Rules=[source.Topology.Rules[0] with{Topology=source.Topology.Rules[0].Topology with{ExtraConnections=99}}]}};
        try{await session.ApplyDerivedAsync(Edit("impossible",impossible));throw new Exception("Missing impossible source edit refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted),"failed macro edit preserves accepted world");
        var rollback=await session.ApplyDerivedAsync(Edit("rollback",source));Check(Json(rollback.World)==Json(initial.World),"exact world rollback");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));await File.WriteAllTextAsync(Path.Combine(output,"changed-request.json"),Json(changedSource));
        await File.WriteAllTextAsync(Path.Combine(output,"source-edit.json"),Json(new MetadataRegionalSourceEditRequest("seed-change","Revise world grammar only","world-grammar:edit",initial.World.DefinitionSha256,initial.Report.WorldSha256,changedSource)));
        Refusals(planner,engine,source,definition,impossible);
        // A second control exercises the same generated links inside the physical
        // ownership feedback owner, including its nested aggregate-work receipt.
        var physical=planner.Plan(source with{PhysicalFeedback=new("Qualify generated journeys before acceptance","world-grammar:physical",1)});
        await engine.PublishAsync(engine.Generate(physical),Path.Combine(output,"physical"));
        Console.WriteLine($"PASS: six complete regions/30 nodes/18 physical sources; seven generated staged links/two shortcuts/{(directed?7:14)} directed terrain-qualified paths; canonical source order, source edit with exact six-packet reuse, restore, failed-edit preservation, rollback, physical feedback and typed bounds.");
    }
    private static void Refusals(MetadataRegionalOwnershipPlanner planner,MetadataRegionalWorldGenerator engine,MetadataRegionalOwnershipRequest source,
        MetadataRegionalWorldDefinition definition,MetadataRegionalOwnershipRequest impossible)
    {
        var t=source.Topology!;
        Refuse(()=>planner.Plan(impossible),"METADATA_TOPOLOGY_LOOP_DEMAND_UNSATISFIED");
        Refuse(()=>planner.Plan(source with{Topology=t with{Destinations=t.Destinations.Select((d,i)=>i==1?d with{Endpoint=t.Destinations[0].Endpoint}:d).ToArray()}}),"METADATA_REGIONAL_TOPOLOGY_DESTINATION_INVALID");
        Refuse(()=>planner.Plan(source with{Topology=t with{Rules=[t.Rules[0] with{Topology=t.Rules[0].Topology with{LayerId="unsupported"}}]}}),"METADATA_REGIONAL_TOPOLOGY_RULE_INVALID");
        Refuse(()=>planner.Plan(source with{Connections=[WorldRegionalOwnershipTests.Example().Connections[0] with{Kind=t.Rules[0].Topology.ConnectionKind}]}),"METADATA_REGIONAL_TOPOLOGY_RULE_INVALID");
        Refuse(()=>planner.Plan(source with{MaximumWork=1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        Refuse(()=>engine.Generate(definition with{TopologyPlan=null}),"METADATA_REGIONAL_TOPOLOGY_PLAN_REQUIRED_OR_UNEXPECTED");
        Refuse(()=>engine.Generate(definition with{TopologyPlan=definition.TopologyPlan! with{Work=definition.TopologyPlan.Work+1}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        Refuse(()=>engine.Generate(definition with{Connections=definition.Connections.Skip(1).ToArray()}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        var invalid=source with{Topology=t with{Destinations=t.Destinations.Select((d,i)=>i==0?d with{Endpoint=d.Endpoint with{PortId="missing"}}:d).ToArray()}};
        Refuse(()=>engine.Generate(planner.Plan(invalid)),"METADATA_ACCESS_PORT_MISSING:");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{planner.Plan(source,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
    }
}
