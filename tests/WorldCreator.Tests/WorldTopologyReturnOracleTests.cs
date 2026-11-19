using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Metadata;

internal static class WorldTopologyReturnOracleTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Return topology oracle: "+message);}
    private static string Rank(string seed,string stage,string a)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Seed=seed,Id="r",purpose="member-order",stage,a,b=(string?)null})));
    private static bool Oracle(string seed,int degree,int total,int[] minima,int[] maxima,int secondCount,int returnsRequired)
    {
        var members=new[]{new[]{"a0","a1"},Enumerable.Range(0,secondCount).Select(i=>"b"+i).ToArray()};
        var entries=new[]{"start","gate"};var exits=new[]{"gate","end"};var names=new[]{"s1","s2"};
        var stages=Enumerable.Range(0,2).Select(i=>new[]{entries[i]}.Concat(members[i]).Append(exits[i]).Order(StringComparer.Ordinal).ToArray()).ToArray();
        var steps=new List<(int Stage,string Node,string[] Parents)>();
        for(int stage=0;stage<2;stage++){
            var prior=new List<string>{entries[stage]};
            foreach(var id in members[stage].OrderBy(n=>Rank(seed,names[stage],n),StringComparer.Ordinal).ThenBy(n=>n,StringComparer.Ordinal).Append(exits[stage])){
                steps.Add((stage,id,prior.ToArray()));prior.Add(id);
            }
        }
        var selected=new List<(int Stage,string A,string B)>();var degrees=new Dictionary<string,int>(StringComparer.Ordinal);
        static (string,string) Pair(string a,string b)=>string.CompareOrdinal(a,b)<0?(a,b):(b,a);
        bool LoopSearch(){
            var tree=selected.Select(e=>Pair(e.A,e.B)).ToHashSet();var extra=new List<(int Stage,string A,string B)>();
            for(int stage=0;stage<2;stage++)for(int a=0;a<stages[stage].Length;a++)for(int b=a+1;b<stages[stage].Length;b++)
                if(!tree.Contains((stages[stage][a],stages[stage][b])))extra.Add((stage,stages[stage][a],stages[stage][b]));
            var counts=new int[2];
            bool Ancestor(string a,string b)
            {
                var parents=selected.ToDictionary(e=>e.B,e=>e.A,StringComparer.Ordinal);
                while(parents.TryGetValue(b,out var parent)){if(parent==a)return true;b=parent;}return false;
            }
            bool Eligible((int Stage,string A,string B) edge)=>Ancestor(edge.A,edge.B)||Ancestor(edge.B,edge.A);
            bool Search(int start,int left,int returns){
                if(left==0)return returns>=returnsRequired&&counts[0]>=minima[0]&&counts[1]>=minima[1];
                for(int i=start;i<extra.Count;i++){
                    var edge=extra[i];if(degrees.GetValueOrDefault(edge.A)>=degree||degrees.GetValueOrDefault(edge.B)>=degree||counts[edge.Stage]>=maxima[edge.Stage])continue;
                    degrees[edge.A]=degrees.GetValueOrDefault(edge.A)+1;degrees[edge.B]=degrees.GetValueOrDefault(edge.B)+1;counts[edge.Stage]++;
                    bool found=Search(i+1,left-1,returns+(Eligible(edge)?1:0));counts[edge.Stage]--;degrees[edge.A]--;degrees[edge.B]--;if(found)return true;
                }
                return false;
            }
            return Search(0,total,0);
        }
        bool Trees(int index){
            if(index==steps.Count)return LoopSearch();var step=steps[index];
            foreach(string parent in step.Parents){
                if(degrees.GetValueOrDefault(parent)>=degree||degrees.GetValueOrDefault(step.Node)>=degree)continue;
                degrees[parent]=degrees.GetValueOrDefault(parent)+1;degrees[step.Node]=degrees.GetValueOrDefault(step.Node)+1;selected.Add((step.Stage,parent,step.Node));
                bool found=Trees(index+1);selected.RemoveAt(selected.Count-1);degrees[parent]--;degrees[step.Node]--;if(found)return true;
            }
            return false;
        }
        return Trees(0);
    }
    internal static void Run()
    {
        WorldMetadataNode Node(string id,string kind,string? parent)=>new(id,id,kind,parent,"Required "+id,new Dictionary<string,JsonElement>(),null,null);
        var nodes=new[]{Node("root","world",null),Node("start","start","root"),Node("gate","gate","root"),Node("end","end","root"),
            Node("a0","early","root"),Node("a1","early","root"),Node("b0","late","root"),Node("b1","late","root"),Node("b2","late","root")};
        int cases=0,accepted=0,refused=0;
        foreach(int secondCount in new[]{2,3})foreach(int degree in new[]{2,3,4})foreach(string seed in new[]{"seed0","seed1","seed2"})
        for(int total=0;total<=3;total++)for(int first=0;first<=Math.Min(total,2);first++)for(int second=0;second<=Math.Min(total-first,2);second++)
        foreach(var cap in new int[]?[]{null,new[]{0,total},new[]{total,0},new[]{1,total},new[]{total,1}})
        for(int returnCount=1;returnCount<=total;returnCount++){
            var minima=new[]{first,second};var maxima=cap??new[]{total,total};
            if(maxima[0]<first||maxima[1]<second||maxima.Sum()<total)continue;
            bool expected=Oracle(seed,degree,total,minima,maxima,secondCount,returnCount);
            var rule=new MetadataTopologyRule("r","Qualify finite graph","oracle:topology","root","start","passage",
                [new("s1","First","gate",["early"],2,2,first,cap?[0]),new("s2","Second","end",["late"],secondCount,secondCount,second,cap?[1])],seed,MaximumDegree:degree,ExtraConnections:total,MaximumWork:2000000,Directed:true,ReturnConnections:returnCount);
            MetadataTopologyPlan? plan=null;
            try{plan=new MetadataTopologyPlanner().Plan("test",[rule],nodes.Where(n=>secondCount==3||n.Id!="b2").ToArray(),[]);}
            catch(ArgumentException e)when(e.Message.StartsWith("METADATA_TOPOLOGY_LOOP_DEMAND_UNSATISFIED",StringComparison.Ordinal)){}
            Check((plan is not null)==expected,$"seed={seed}, members=2/{secondCount}, degree={degree}, total={total}, minima={first}/{second}, maxima={maxima[0]}/{maxima[1]}, returns={returnCount}, expected={expected}");
            if(plan is not null){
                accepted++;var edges=plan.Results.Single().Connections;var loops=edges.Where(e=>e.Properties["reason"].GetString()=="stage-loop").ToArray();
                Check(loops.Length==total&&loops.Count(e=>e.Properties["stage"].GetString()=="s1")>=first&&loops.Count(e=>e.Properties["stage"].GetString()=="s2")>=second&&
                    loops.Count(e=>e.Properties["stage"].GetString()=="s1")<=maxima[0]&&loops.Count(e=>e.Properties["stage"].GetString()=="s2")<=maxima[1],"exact loop requirements");
                var treeEdges=edges.Where(e=>e.Properties["reason"].GetString()=="stage-tree").ToArray();
                var returns=edges.Where(e=>e.Properties.TryGetValue("return",out var v)&&v.GetBoolean()).ToArray();
                Check(returns.Length==returnCount&&edges.All(e=>!e.Bidirectional),"exact directed return edges");
                foreach(var back in returns){var seen=new HashSet<string>{back.ToId};var pending=new Queue<string>();pending.Enqueue(back.ToId);
                    while(pending.TryDequeue(out var id))foreach(var e in treeEdges.Where(e=>e.FromId==id))if(seen.Add(e.ToId))pending.Enqueue(e.ToId);
                    Check(seen.Contains(back.FromId),"return must close an existing tree path");}
                var allReached=new HashSet<string>{"start"};var allPending=new Queue<string>();allPending.Enqueue("start");
                while(allPending.TryDequeue(out var id))foreach(var e in edges.Where(e=>e.FromId==id))if(allReached.Add(e.ToId))allPending.Enqueue(e.ToId);
                Check(allReached.Count==nodes.Length-1-(secondCount==2?1:0),"all declared nodes reachable");
                Check(edges.SelectMany(e=>new[]{e.FromId,e.ToId}).GroupBy(x=>x).All(g=>g.Count()<=degree),"degree bound");
                var reached=new HashSet<string>(StringComparer.Ordinal){"start"};var queue=new Queue<string>();queue.Enqueue("start");
                while(queue.TryDequeue(out var current))foreach(var e in edges){
                    if(e.FromId=="gate"||e.ToId=="gate")continue;string? next=e.FromId==current?e.ToId:null;
                    if(next is not null&&reached.Add(next))queue.Enqueue(next);
                }
                Check(!reached.Contains("end"),"mandatory gate is an articulation point");
            }else refused++;
            cases++;
        }
        Check(accepted>0&&refused>0,"matrix covers feasible and impossible graphs");
        Console.WriteLine($"PASS: exhaustive independent ordered-tree/return-loop oracle agrees for {cases} cases ({accepted} feasible, {refused} impossible).");
    }
}
