using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldDirectedReturnTopologyTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static async Task RunAsync(string output)
    {
        var input=WorldTopologyTests.Example();var rule=input.Recipe.TopologyRules![0];var generator=new WorldMetadataGenerator();
        var recipe=input.Recipe with{TopologyRules=[rule with{Directed=true,ReturnConnections=2}]};
        var world=generator.Generate(recipe,input.Catalog);
        Check(world.Nodes.Count==25&&world.Connections.Count==26,"exact content and links retained");
        var edges=world.Connections;var returns=edges.Where(e=>e.Properties.TryGetValue("return",out var v)&&v.GetBoolean()).ToArray();
        Check(returns.Length==2&&edges.All(e=>!e.Bidirectional),"exact one-way returns");
        var tree=edges.Where(e=>e.Properties["reason"].GetString()=="stage-tree").ToArray();
        bool Reach(string from,string to,IEnumerable<WorldMetadataConnection> links,string? banned=null)
        {var seen=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(from);while(queue.TryDequeue(out var n)){if(n==banned||!seen.Add(n))continue;if(n==to)return true;foreach(var e in links.Where(e=>e.FromId==n))queue.Enqueue(e.ToId);}return false;}
        foreach(var e in returns)Check(Reach(e.ToId,e.FromId,tree),"return closes pre-existing directed tree path");
        foreach(var n in world.Nodes.Where(n=>n.ParentId!=null))Check(Reach(rule.StartNodeId,n.Id,edges),"all destinations reachable");
        Check(!Reach(rule.StartNodeId,rule.Stages[^1].ExitNodeId,edges,rule.Stages[0].ExitNodeId),"mandatory transition remains required");
        Check(Json(world)==Json(generator.Generate(recipe,input.Catalog)),"deterministic whole world");
        var measuredWork=world.TopologyPlan!.Results.Single().Work;
        var exact=recipe with{TopologyRules=[rule with{Directed=true,ReturnConnections=2,MaximumWork=measuredWork}]};
        Check(generator.Generate(exact,input.Catalog).TopologyPlan!.Results.Single().Work==measuredWork,"exact topology budget accepted");
        bool budgetRefused=false;try{generator.Generate(recipe with{TopologyRules=[rule with{Directed=true,ReturnConnections=2,MaximumWork=measuredWork-1}]},input.Catalog);}catch(ArgumentException e)when(e.Message.StartsWith("METADATA_TOPOLOGY_WORK_BUDGET")){budgetRefused=true;}
        Check(budgetRefused,"insufficient topology budget refuses");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();bool cancelled=false;try{generator.Generate(recipe,input.Catalog,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancellation honored");}
        for(int seed=0;seed<8;seed++)
        {
            var varied=generator.Generate(recipe with{TopologyRules=[rule with{Directed=true,ReturnConnections=2,Seed="return-"+seed}]},input.Catalog);
            var variedTree=varied.Connections.Where(e=>e.Properties["reason"].GetString()=="stage-tree").ToArray();
            var variedReturns=varied.Connections.Where(e=>e.Properties.TryGetValue("return",out var flag)&&flag.GetBoolean()).ToArray();
            Check(variedReturns.Length==2&&variedReturns.All(e=>Reach(e.ToId,e.FromId,variedTree)),"seeded return witnesses");
        }
        bool refused=false;try{generator.Generate(input.Recipe with{TopologyRules=[rule with{ReturnConnections=1}]},input.Catalog);}catch(ArgumentException e)when(e.Message.StartsWith("METADATA_TOPOLOGY_RULE_INVALID")){refused=true;}
        Check(refused,"returns require directed topology");
        refused=false;try{generator.Generate(input.Recipe with{TopologyRules=[rule with{Directed=true,ReturnConnections=4}]},input.Catalog);}catch(ArgumentException e)when(e.Message.StartsWith("METADATA_TOPOLOGY_RULE_INVALID")){refused=true;}
        Check(refused,"returns cannot exceed extra connections");
        var emptyCatalog=input.Catalog with{Archetypes=input.Catalog.Archetypes.Select(a=>a.Id=="world"?a with{Parts=a.Parts.Select(p=>p.Id is "early" or "late"?p with{Count=0}:p).ToArray()}:a).ToArray()};
        var emptyStages=rule.Stages.Select(stage=>stage with{MinimumMembers=0,MaximumMembers=0}).ToArray();
        var impossible=input.Recipe with{TopologyRules=[rule with{Directed=true,ReturnConnections=1,ExtraConnections=1,Stages=emptyStages}]};
        refused=false;try{generator.Generate(impossible,emptyCatalog);}catch(ArgumentException e)when(e.Message.StartsWith("METADATA_TOPOLOGY_LOOP_DEMAND_UNSATISFIED")){refused=true;}
        Check(refused,"valid but impossible return demand refuses");
        var changed=world.Connections.ToArray();var index=Array.FindIndex(changed,e=>e.Properties.ContainsKey("return"));changed[index]=changed[index] with{FromId=changed[index].ToId,ToId=changed[index].FromId};
        refused=false;try{WorldMetadataGenerator.ValidateDocument(world with{Connections=changed});}catch(ArgumentException e)when(e.Message.Contains("TOPOLOGY_PLAN_MISMATCH")){refused=true;}
        Check(refused,"tampered return refuses");
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Destination exists");Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(input.Catalog));await File.WriteAllTextAsync(Path.Combine(output,"world.json"),Json(world));
        Console.WriteLine("PASS: two explicit directed cycles, retained stage gate/content, deterministic replay and invalid request refusal.");
    }
}
