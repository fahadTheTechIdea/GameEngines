using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldPlacementEnvelopeIndexTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Placement index: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Index evidence exists");Directory.CreateDirectory(output);int comparisons=0,queries=0;
        var regionalEntries=Enumerable.Range(0,2048).Select(i=>(Id:"region-source-"+i,Bounds:new WorldPlacementEnvelopeIndex.Box(i*2,-1,i*2+1,0))).ToArray();
        Reject(()=>new WorldPlacementEnvelopeIndex(regionalEntries,new(20000000,default)),"WORLD_PLACEMENT_INDEX_INVALID");
        Reject(()=>new WorldPlacementEnvelopeIndex(regionalEntries,new(20000000,default),100001),"WORLD_PLACEMENT_INDEX_INVALID");
        Reject(()=>new WorldPlacementEnvelopeIndex(regionalEntries,new(20000000,default),0),"WORLD_PLACEMENT_INDEX_INVALID");
        var regional=new WorldPlacementEnvelopeIndex(regionalEntries,new(20000000,default),2048);
        Check(regional.Query(new(4094,-1,4095,0),0,_=>true).SequenceEqual(new[]{"region-source-2047"}),"explicit regional cap retains last source; prescribed default unchanged");
        foreach(var (count,offset) in new[]{(32,0d),(257,-9999000d),(1024,0d)})
        {
            var entries=Enumerable.Range(0,count).Select(i=>(Id:"site-"+i,Bounds:new WorldPlacementEnvelopeIndex.Box(
                offset+(i*73%257)*4-512,(i*37%251)*3-377,offset+(i*73%257)*4-511+(i%7)*.25,(i*37%251)*3-376+(i%9)*.25))).ToArray();
            var original=entries.ToArray();var budget=new WorldSpatialWorkBudget(20000000,default);var index=new WorldPlacementEnvelopeIndex(entries,budget);
            entries[0]=("mutated",new(-1e7,-1e7,-1e7+1,-1e7+1));
            for(int q=0;q<32;q++)foreach(double gap in new[]{0d,.5,2,8})
            {
                var a=original[(q*31)%count].Bounds;var bounds=new WorldPlacementEnvelopeIndex.Box(a.MinX-.5,a.MinY-.25,a.MaxX+(q%5)*32,a.MaxY+(q%3)*24);
                var selected=original.Where((_,i)=>q%2==0?i<(q*17)%count:i%3!=0).Select(e=>e.Id).ToHashSet(StringComparer.Ordinal);
                var expected=original.Where(e=>selected.Contains(e.Id)).Where(e=>{
                    double dx=Math.Max(0,Math.Max(bounds.MinX-e.Bounds.MaxX,e.Bounds.MinX-bounds.MaxX));
                    double dy=Math.Max(0,Math.Max(bounds.MinY-e.Bounds.MaxY,e.Bounds.MinY-bounds.MaxY));comparisons++;
                    return dx*dx+dy*dy<=gap*gap;
                }).Select(e=>e.Id).ToArray();
                Check(index.Query(bounds,gap,selected.Contains).SequenceEqual(expected),"complete conservative candidates in original order; immutable input bounds");queries++;
            }
            var exact=new WorldSpatialWorkBudget(budget.Used,default);_ = new WorldPlacementEnvelopeIndex(original,exact);
        }
        var input=new[]{("z",new WorldPlacementEnvelopeIndex.Box(0,0,1,1)),("a",new WorldPlacementEnvelopeIndex.Box(1.5,0,2.5,1)),("m",new WorldPlacementEnvelopeIndex.Box(0,0,1,1))};
        var b=new WorldSpatialWorkBudget(10000,default);var contact=new WorldPlacementEnvelopeIndex(input,b);
        Check(contact.Query(new(1,0,1,1),.5,_=>true).SequenceEqual(new[]{"z","a","m"}),"touch and exact minimum cannot be omitted; rank is not spatial or ID order");
        Reject(()=>new WorldPlacementEnvelopeIndex(input,new(1,default)),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>new WorldPlacementEnvelopeIndex([("bad",new(double.NaN,0,1,1))],new(100,default)),"WORLD_PLACEMENT_INDEX_INVALID");
        Reject(()=>new WorldPlacementEnvelopeIndex([input[0],input[0]],new(100,default)),"WORLD_PLACEMENT_INDEX_INVALID");
        Reject(()=>contact.Query(new(2,0,1,1),0,_=>true),"WORLD_PLACEMENT_INDEX_QUERY_INVALID");
        Reject(()=>contact.Query(new(0,0,1,1),double.NaN,_=>true),"WORLD_PLACEMENT_INDEX_QUERY_INVALID");
        var buildBudget=new WorldSpatialWorkBudget(10000,default);_ =new WorldPlacementEnvelopeIndex(input,buildBudget);
        var tight=new WorldSpatialWorkBudget(buildBudget.Used,default);var full=new WorldPlacementEnvelopeIndex(input,tight);
        Reject(()=>full.Query(new(0,0,1,1),0,_=>true),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancellation=new CancellationTokenSource())
        {
            var cancellable=new WorldPlacementEnvelopeIndex(input,new(10000,cancellation.Token));cancellation.Cancel();
            try{cancellable.Query(new(0,0,1,1),0,_=>true);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
        }
        var domain=Box(-20,-20,40,40);var environment=new WorldEnvironmentQuery(new(new(1,"ground","meters",domain,[],[new("ground","ground","Supported plane","index:test",domain,0,new(0,0),0,0,0)])),new(1,"fields",[new("height","Plane height","index:test","meters",0,[])]));
        var profile=new WorldSuitabilityProfile("ground","Exact supported sources","index:test",["ground"],[],[]);
        var pairFacts=new List<object>();
        foreach(bool reserved in new[]{false,true})foreach(bool contactAllowed in new[]{false,true})foreach(double separation in new[]{0d,.5,1})foreach(double distance in new[]{0d,.5,1,1.2,1.5,2.25,4})
        {
            var shape=Box(-.5,-.25,1,.5);var reservation=reserved?Box(-.75,-.5,1.5,1):null;
            var request=new WorldPlacementRequest("pair","Qualify complete pair","index:test",domain,
                [new("a","First source","index:a",shape,profile,reservation),new("b","Second source","index:b",shape,profile,reservation)],
                [new("pa",new(0,0)),new("pb",new(distance,0))],[new("aa","a","pa","Keep first source","index:a",0,0,0,1),new("bb","b","pb","Keep second source","index:b",0,0,0,1)],
                MinimumSeparation:separation,AllowBoundaryContact:contactAllowed,Execution:WorldPlacementExecution.Prescribed);
            var result=new WorldSitePlacementPlanner().Plan(environment,request);
            double width=reserved?1.5:1,overlap=Math.Max(0,width-distance)*(reserved?1:.5);
            bool expected=contactAllowed&&separation==0?overlap<=1e-8:distance>width&&distance-width>=separation;
            Check((result.Status==WorldPlacementStatus.Found)==expected,"independent exact rectangle separation/contact oracle");
            pairFacts.Add(new{reserved,contactAllowed,separation,distance,Found=result.Status==WorldPlacementStatus.Found,result.SeparationWitness});
        }
        const double eps=1e-10;
        var anchorCase=new WorldPlacementRequest("anchor","Retain exact anchor-distance shortcut","index:anchor",domain,
            [new("a","Left source","index:a",Box(-.2,-.2,.2-eps,.4),profile),new("b","Right source","index:b",Box(eps,-.2,.2,.4),profile)],
            [new("pa",new(0,0)),new("pb",new(.5-eps,0))],[new("aa","a","pa","Pin first","index:a",0,0,0,1),new("bb","b","pb","Pin second","index:b",0,0,0,1)],MinimumSeparation:.5,Execution:WorldPlacementExecution.Prescribed);
        Check(new WorldSitePlacementPlanner().Plan(environment,anchorCase).Status==WorldPlacementStatus.NoFeasibleAssignment,"bounds include tolerant source anchors, not only envelopes");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),JsonSerializer.Serialize(new{Queries=queries,BruteForceComparisons=comparisons,Pairs=pairFacts,AnchorShortcutRetained=true},WorldJson.CreateOptions()));
        Console.WriteLine($"PASS: {queries} bounds queries / {comparisons} independent comparisons, {pairFacts.Count} complete pair assignments, exact contact/anchor/witness order, input snapshots, budget and cancellation.");
    }
}
