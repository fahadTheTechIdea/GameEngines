using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldTopologyCapacityTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Refuse(Action action,string code)
    {
        try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected "+code);
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Fixture(int stages,int members,int degree,int seed)
    {
        var e=WorldTopologyTests.Example();var rule=e.Recipe.TopologyRules![0];
        var parts=e.Catalog.Archetypes[0].Parts.Where(p=>stages==2||p.Id is "entry" or "gate" or "early")
            .Select(p=>p.Id is "early" or "late"?p with{Count=members}:p).ToArray();
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select((a,i)=>i==0?a with{Parts=parts}:a).ToArray()};
        rule=rule with{Stages=rule.Stages.Take(stages).Select(s=>s with{MinimumMembers=members,MaximumMembers=members}).ToArray(),
            MaximumDegree=degree,ExtraConnections=0,Seed="capacity-"+seed};
        return(e.Recipe with{TopologyRules=[rule]},catalog);
    }

    // Exhaust all edge subsets, independent of the production search order,
    // residual-capacity pruning and choice history. Small fixtures only.
    private static int MaximumLoops(WorldMetadataDocument tree,int maximumDegree)
    {
        var result=tree.TopologyPlan!.Results.Single();var edges=result.Connections;
        var vertices=result.Stages.SelectMany(s=>s.MemberNodeIds.Append(s.EntryNodeId).Append(s.ExitNodeId)).Distinct().Order().ToArray();
        int Index(string id)=>Array.IndexOf(vertices,id);
        var degree=new int[vertices.Length];var pairs=new HashSet<(int,int)>();
        foreach(var edge in edges)
        {
            int a=Index(edge.FromId),b=Index(edge.ToId);degree[a]++;degree[b]++;
            pairs.Add((Math.Min(a,b),Math.Max(a,b)));
        }
        var possible=new HashSet<(int,int)>();
        foreach(var stage in result.Stages)
        {
            var local=stage.MemberNodeIds.Append(stage.EntryNodeId).Append(stage.ExitNodeId).Select(Index).Order().ToArray();
            for(int i=0;i<local.Length;i++)for(int j=i+1;j<local.Length;j++)
                if(!pairs.Contains((local[i],local[j])))possible.Add((local[i],local[j]));
        }
        var candidates=possible.ToArray();Check(candidates.Length<=16,"independent exhaustive oracle bound");int best=0;
        for(int mask=0;mask<(1<<candidates.Length);mask++)
        {
            var counts=(int[])degree.Clone();int loops=0;bool valid=true;
            for(int i=0;i<candidates.Length;i++)if((mask&(1<<i))!=0)
            {
                loops++;var (a,b)=candidates[i];
                if(++counts[a]>maximumDegree||++counts[b]>maximumDegree){valid=false;break;}
            }
            if(valid)best=Math.Max(best,loops);
        }
        return best;
    }
    private static void Invariants(WorldMetadataDocument world,int degree,int loops)
    {
        var result=world.TopologyPlan!.Results.Single();
        var nodes=world.Nodes.Where(n=>n.ParentId is not null).Select(n=>n.Id).ToHashSet();
        var adjacency=nodes.ToDictionary(id=>id,_=>new HashSet<string>());
        foreach(var edge in result.Connections)
        {
            Check(edge.Bidirectional&&edge.FromId!=edge.ToId,"valid loop endpoints");
            Check(adjacency[edge.FromId].Add(edge.ToId)&&adjacency[edge.ToId].Add(edge.FromId),"unique pairs");
            Check(result.Stages.Any(s=>s.MemberNodeIds.Append(s.EntryNodeId).Append(s.ExitNodeId).Contains(edge.FromId)&&
                s.MemberNodeIds.Append(s.EntryNodeId).Append(s.ExitNodeId).Contains(edge.ToId)),"no cross-stage loop");
        }
        Check(result.Connections.Count==nodes.Count-1+loops,"exact tree and loop demand");
        Check(adjacency.All(p=>p.Value.Count<=degree),"degree is unchanged");
        HashSet<string> Reach(string? blocked)
        {
            var seen=new HashSet<string>();var pending=new Queue<string>();pending.Enqueue(result.Stages[0].EntryNodeId);
            while(pending.TryDequeue(out var id))if(id!=blocked&&seen.Add(id))foreach(var next in adjacency[id])pending.Enqueue(next);
            return seen;
        }
        Check(Reach(null).SetEquals(nodes),"all required members reachable");
        foreach(var stage in result.Stages.SkipLast(1))Check(!Reach(stage.ExitNodeId).Contains(result.Stages[^1].ExitNodeId),"milestone not bypassed");
        Check(world.Surface is null&&world.Nodes.All(n=>n.Position is null),"no terrain or grid inferred");
        WorldMetadataGenerator.ValidateDocument(world);
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();int accepted=0,refused=0;
        foreach(var (stages,members,degree) in new[]{(1,4,3),(2,2,3),(1,4,4)})for(int seed=0;seed<16;seed++)
        {
            var e=Fixture(stages,members,degree,seed);var tree=generator.Generate(e.Recipe,e.Catalog);
            int maximum=MaximumLoops(tree,degree);
            for(int loops=1;loops<=maximum;loops++)
            {
                var recipe=e.Recipe with{TopologyRules=[e.Recipe.TopologyRules![0] with{ExtraConnections=loops}]};
                var world=generator.Generate(recipe,e.Catalog);Invariants(world,degree,loops);accepted++;
                Check(Json(tree.TopologyPlan!.Results[0].Connections)==Json(world.TopologyPlan!.Results[0].Connections.Where(c=>c.Properties["reason"].GetString()=="stage-tree").ToArray()),"tree and seed unchanged");
            }
            var impossible=e.Recipe with{TopologyRules=[e.Recipe.TopologyRules![0] with{ExtraConnections=maximum+1}]};
            Refuse(()=>generator.Generate(impossible,e.Catalog),"LOOP_DEMAND_UNSATISFIED");refused++;
        }
        var probe=Fixture(1,4,3,1);var request=probe.Recipe with{TopologyRules=[probe.Recipe.TopologyRules![0] with{ExtraConnections=4}]};
        var fixedWorld=generator.Generate(request,probe.Catalog);Invariants(fixedWorld,3,4);
        Check(Json(fixedWorld)==Json(generator.Generate(request,probe.Catalog)),"deterministic full output");
        long work=fixedWorld.TopologyPlan!.Results.Single().Work;
        Refuse(()=>generator.Generate(request with{TopologyRules=[request.TopologyRules![0] with{MaximumWork=work-1}]},probe.Catalog),"WORK_BUDGET");
        var session=new MetadataGenerationSession(generator,request,probe.Catalog);var before=Json(session.Snapshot);
        Refuse(()=>session.Apply(new("impossible-loops","Retain exact demand","test:capacity",session.Snapshot.World.RecipeSha256,
            session.Snapshot.World.CatalogSha256,request with{TopologyRules=[request.TopologyRules![0] with{ExtraConnections=5}]},probe.Catalog)),"LOOP_DEMAND_UNSATISFIED");
        Check(Json(session.Snapshot)==before,"failed edit retains accepted graph and history");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{generator.Generate(request,probe.Catalog,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        string directory=Path.Combine("generated","topology-capacity-"+Guid.NewGuid().ToString("N"));
        string inputs=directory+"-inputs";Directory.CreateDirectory(inputs);
        await File.WriteAllTextAsync(Path.Combine(inputs,"recipe.json"),Json(request));
        await File.WriteAllTextAsync(Path.Combine(inputs,"catalog.json"),Json(probe.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(inputs,"recipe.json"),Path.Combine(inputs,"catalog.json"),directory);
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(directory,"world-metadata.json")))==Json(fixedWorld),"normal saved replay");
        var scale=Fixture(1,96,4,1);var scaleRecipe=scale.Recipe with{TopologyRules=[scale.Recipe.TopologyRules![0] with{ExtraConnections=99}]};
        var larger=generator.Generate(scaleRecipe,scale.Catalog);Invariants(larger,4,99);
        Check(larger.Nodes.Count==99&&larger.Connections.Count==196&&larger.TopologyPlan!.Work<=1000000,"98 destinations with exact degree-capacity saturation within original work declaration");
        Console.WriteLine($"PASS: {accepted} exact loop demands and {refused} independently infeasible demands across 48 seeded one/two-stage graphs, unchanged trees/members/degrees/milestones, work refusal, cancellation, edit rollback and saved replay. Scale: 98 destinations/196 connections/99 loops, work {larger.TopologyPlan!.Work}/1000000. Packet: {directory}");
    }
}
