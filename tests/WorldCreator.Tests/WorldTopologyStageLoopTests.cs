using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldTopologyStageLoopTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Topology stage loops: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=WorldTopologyTests.Example();var baseRule=input.Recipe.TopologyRules![0];
        var rule=baseRule with{Stages=[baseRule.Stages[0] with{MinimumExtraConnections=1},baseRule.Stages[1] with{MinimumExtraConnections=2}]};
        var recipe=input.Recipe with{TopologyRules=[rule]};var generator=new WorldMetadataGenerator();
        var world=generator.Generate(recipe,input.Catalog);var graph=world.TopologyPlan!.Results.Single();
        Check(world.Nodes.Count==25&&world.Connections.Count==26,"all required members and exact total connections retained");
        var loops=graph.Connections.Where(c=>c.Properties["reason"].GetString()=="stage-loop").ToArray();
        Check(loops.Length==3&&loops.Count(c=>c.Properties["stage"].GetString()=="approach")==1&&loops.Count(c=>c.Properties["stage"].GetString()=="objective")==2,"exact stage minima and total");
        WorldMetadataGenerator.ValidateDocument(world);
        Check(Json(world)==Json(generator.Generate(recipe,input.Catalog)),"deterministic complete generation");
        string path=Path.Combine(output,"world.json");await File.WriteAllTextAsync(path,Json(world));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(path))==Json(world),"saved graph replay");
        var unconstrained=generator.Generate(input.Recipe,input.Catalog);
        int originalEarly=unconstrained.TopologyPlan!.Results.Single().Connections.Count(c=>c.Properties["reason"].GetString()=="stage-loop"&&c.Properties["stage"].GetString()=="approach");
        var forced=baseRule with{Stages=originalEarly<2?[baseRule.Stages[0] with{MinimumExtraConnections=2},baseRule.Stages[1]]:
            [baseRule.Stages[0],baseRule.Stages[1] with{MinimumExtraConnections=2}]};
        var forcedWorld=generator.Generate(input.Recipe with{TopologyRules=[forced]},input.Catalog);
        int forcedEarly=forcedWorld.TopologyPlan!.Results.Single().Connections.Count(c=>c.Properties["reason"].GetString()=="stage-loop"&&c.Properties["stage"].GetString()=="approach");
        Check(forcedEarly!=originalEarly,"stage requirement changes the original seeded loop allocation");
        for(int seed=0;seed<4;seed++){
            var variant=generator.Generate(recipe with{TopologyRules=[rule with{Seed="stage-loops-"+seed}]},input.Catalog);
            var varied=variant.TopologyPlan!.Results.Single().Connections.Where(c=>c.Properties["reason"].GetString()=="stage-loop").ToArray();
            Check(varied.Count(c=>c.Properties["stage"].GetString()=="approach")==1&&varied.Count(c=>c.Properties["stage"].GetString()=="objective")==2,"stage minima survive seeded variation");
        }
        WorldMetadataNode Node(string id,string kind,string? parent)=>new(id,id,kind,parent,"Required "+id,new Dictionary<string,JsonElement>(),null,null);
        var alternativeNodes=new[]{Node("root","world",null),Node("start","start","root"),Node("gate","gate","root"),Node("end","end","root"),
            Node("a0","early","root"),Node("a1","early","root"),Node("b0","late","root"),Node("b1","late","root"),Node("b2","late","root")};
        var alternativeRule=new MetadataTopologyRule("r","Two stages with required loops","test:alternative","root","start","passage",
            [new("s1","First stage","gate",["early"],2,2,2),new("s2","Second stage","end",["late"],3,3,1)],"seed1",MaximumDegree:3,ExtraConnections:3);
        var alternative=new MetadataTopologyPlanner().Plan("test",[alternativeRule],alternativeNodes,[]);
        Check(alternative.Results.Single().Connections.Count==10,"alternative stage tree preserves all seven tree links and three required loops");
        var alternativeEdges=alternative.Results.Single().Connections;
        Check(alternativeEdges.Count(c=>c.Properties["reason"].GetString()=="stage-loop"&&c.Properties["stage"].GetString()=="s1")==2&&
              alternativeEdges.Count(c=>c.Properties["reason"].GetString()=="stage-loop"&&c.Properties["stage"].GetString()=="s2")==1,"alternative satisfies each exact stage minimum");
        Check(alternativeEdges.SelectMany(c=>new[]{c.FromId,c.ToId}).GroupBy(id=>id).All(g=>g.Count()<=3),"alternative respects all degree bounds");
        Check(Json(alternative)==Json(new MetadataTopologyPlanner().Plan("test",[alternativeRule],alternativeNodes.Reverse().ToArray(),[])),"alternative tree is deterministic across input order");
        Reject(()=>new MetadataTopologyPlanner().Plan("test",[alternativeRule with{MaximumWork=alternative.Work-1}],alternativeNodes,[]),"METADATA_TOPOLOGY_WORK_BUDGET");
        var impossibleNodes=alternativeNodes.Where(n=>n.Id is not "a1" and not "b1" and not "b2").ToArray();
        var impossibleLoops=alternativeRule with{MaximumDegree=2,ExtraConnections=2,Stages=[alternativeRule.Stages[0] with{MinimumMembers=1,MaximumMembers=1,MinimumExtraConnections=1},
            alternativeRule.Stages[1] with{MinimumMembers=1,MaximumMembers=1,MinimumExtraConnections=1}]};
        Reject(()=>new MetadataTopologyPlanner().Plan("test",[impossibleLoops],impossibleNodes,[]),"METADATA_TOPOLOGY_LOOP_DEMAND_UNSATISFIED");
        await File.WriteAllTextAsync(Path.Combine(output,"alternative-graph.json"),Json(alternative));
        var cappedRule=baseRule with{Stages=[baseRule.Stages[0] with{MaximumExtraConnections=0},baseRule.Stages[1]]};
        var capped=generator.Generate(input.Recipe with{TopologyRules=[cappedRule]},input.Catalog);
        var cappedLoops=capped.TopologyPlan!.Results.Single().Connections.Where(c=>c.Properties["reason"].GetString()=="stage-loop").ToArray();
        Check(cappedLoops.Length==3&&cappedLoops.All(c=>c.Properties["stage"].GetString()=="objective"),"explicit zero cap moves all loops to allowed stage");
        Reject(()=>generator.Generate(recipe with{TopologyRules=[rule with{Stages=[rule.Stages[0] with{MaximumExtraConnections=0},rule.Stages[1]]}]},input.Catalog),"METADATA_TOPOLOGY_STAGE_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{TopologyRules=[baseRule with{Stages=baseRule.Stages.Select(st=>st with{MaximumExtraConnections=0}).ToArray()}]},input.Catalog),"METADATA_TOPOLOGY_STAGE_LOOP_TOTAL_INVALID");
        var regional=WorldRegionalTopologyTests.Example();var regionalRule=regional.Topology!.Rules[0];
        var regionalTopology=regionalRule.Topology with{Stages=regionalRule.Topology.Stages.Select(stage=>stage with{MinimumExtraConnections=1,MaximumExtraConnections=1}).ToArray()};
        var regionalInput=regional with{Topology=regional.Topology with{Rules=[regionalRule with{Topology=regionalTopology}]}};
        var regionalDefinition=new MetadataRegionalOwnershipPlanner().Plan(regionalInput);
        var regionalLoops=regionalDefinition.TopologyPlan!.Topology.Results.Single().Connections.Where(c=>c.Properties["reason"].GetString()=="stage-loop").ToArray();
        Check(regionalLoops.Length==2&&regionalLoops.GroupBy(c=>c.Properties["stage"].GetString()).Count()==2,"regional topology retains one loop in each authored stage");
        await File.WriteAllTextAsync(Path.Combine(output,"regional-definition.json"),Json(regionalDefinition));
        var regionalEngine=new MetadataRegionalWorldGenerator();var regionalWorld=regionalEngine.Generate(regionalDefinition);
        Check(regionalWorld.Regions.Count==6&&regionalWorld.Regions.Sum(r=>r.World.Nodes.Count)==30&&regionalWorld.Connections.Count==7&&regionalWorld.Connections.All(c=>c.Forward.Status==WorldCreator.Core.Spatial.WorldRouteStatus.Found&&c.Reverse?.Status==WorldCreator.Core.Spatial.WorldRouteStatus.Found),"six complete regional programs and fourteen physical directions survive stage minima");
        await regionalEngine.PublishAsync(regionalWorld,Path.Combine(output,"regional-world"));
        Check(Json(await regionalEngine.ReadWorldAsync(Path.Combine(output,"regional-world/world-regions.json")))==Json(regionalWorld),"complete physical regional world replay");
        var impossible=rule with{Stages=[rule.Stages[0] with{MemberKinds=[],MinimumMembers=0,MaximumMembers=0,MinimumExtraConnections=1},rule.Stages[1] with{MinimumExtraConnections=0}]};
        Reject(()=>new MetadataTopologyPlanner().Plan(world.Id,[impossible],world.Nodes,[]),"METADATA_TOPOLOGY_LOOP_DEMAND_UNSATISFIED");
        Reject(()=>generator.Generate(recipe with{TopologyRules=[rule with{ExtraConnections=2}]},input.Catalog),"METADATA_TOPOLOGY_STAGE_LOOP_TOTAL_INVALID");
        Reject(()=>generator.Generate(recipe with{TopologyRules=[rule with{Stages=[rule.Stages[0] with{MinimumExtraConnections=-1},rule.Stages[1]]}]},input.Catalog),"METADATA_TOPOLOGY_STAGE_INVALID");
        var altered=world with{TopologyPlan=world.TopologyPlan with{Rules=[rule with{Stages=[rule.Stages[0] with{MinimumExtraConnections=2},rule.Stages[1] with{MinimumExtraConnections=1}]}]}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(altered),"METADATA_TOPOLOGY_PLAN_MISMATCH");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();try{generator.Generate(recipe,input.Catalog,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: exact per-stage loop counts, deterministic connected world, replay, tamper, impossible and invalid demands, cancellation.");
    }
}
