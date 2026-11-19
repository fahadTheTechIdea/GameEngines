using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldDrainageTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Drainage: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-8*Math.Max(1,Math.Abs(b)),message);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private static WorldSurfaceDefinition Surface(Func<int,int,double> elevation)=>new(1,"inland","meters",Box(0,0,4,4),[],[],
        HeightField:new(1,"height","land","Explicit inland height samples","drainage:test","meters",new(0,0),1,1,5,5,
            Enumerable.Range(0,25).Select(i=>elevation(i%5,i/5)).ToArray()));
    private static WorldDrainageTerminal Sink(string id,int x,int y,WorldDrainageTerminalKind kind=WorldDrainageTerminalKind.Outlet)=>new(id,x,y,kind,"Authored terminal","drainage:terminal");
    private static WorldDrainageDefinition Request()=>new(1,"drainage","Optional game environment","drainage:test","liters-per-second",[Sink("west",0,0)],
        [new("high",4,4,16,"Declared inflow","drainage:source"),new("north",0,4,8,"Declared inflow","drainage:source")]);
    // Independent edge, potential, ancestry and local conservation oracle. It does
    // not invoke the planner or its validators and sums receipts from edge output.
    internal static void Proof(WorldDrainagePlan plan)
    {
        var lookup=plan.Nodes.ToDictionary(n=>n.SampleIndex);var receipts=plan.Terminals.ToDictionary(t=>t.TerminalId);
        foreach(var node in plan.Nodes)
        {
            Near(node.LocalInput+node.Incoming,node.Lost+node.Outgoing+node.Retained,"per-node flow conservation");
            Near(node.Incoming,plan.Nodes.Where(n=>n.DownstreamSampleIndex==node.SampleIndex).Sum(n=>n.Outgoing),"join accounting from upstream outputs");
            var visited=new HashSet<int>();var current=node;
            while(current.DownstreamSampleIndex is {} next)
            {
                Check(visited.Add(current.SampleIndex),"no cycle");var target=lookup[next];
                Check(target.Elevation<=current.Elevation,"non-uphill endpoints");
                int dx=target.Column-current.Column,dy=target.Row-current.Row;
                Check((Math.Abs(dx)==1&&dy==0)||(dx==0&&Math.Abs(dy)==1)||(dx==dy&&Math.Abs(dx)==1),"native triangle-edge adjacency only");
                Check(target.DistanceToTerminal<current.DistanceToTerminal,"strict flat-resolution potential");
                Near(current.DistanceToTerminal,target.DistanceToTerminal+current.Position.DistanceTo(target.Position),"path-distance witness");
                Check(current.TerminalId==target.TerminalId,"terminal provenance along path");current=target;
            }
            Check(receipts.ContainsKey(current.TerminalId),"permitted terminal reached");Near(current.DistanceToTerminal,0,"terminal potential");
        }
        foreach(var receipt in plan.Terminals)Near(receipt.Received,plan.Nodes.Where(n=>n.DownstreamSampleIndex is null&&n.TerminalId==receipt.TerminalId).Sum(n=>n.Retained),"terminal receipt");
        Near(plan.TotalInput,plan.Nodes.Sum(n=>n.LocalInput),"explicit source sum");Near(plan.TotalInput,plan.TotalLost+plan.TotalReceived,"global conservation");
    }
    public static async Task RunAsync()
    {
        var planner=new WorldDrainagePlanner();var request=Request();
        var flat=Surface((x,y)=>0);string protectedInput=Json(flat);
        foreach(var surface in new[]{flat,Surface((x,y)=>x+y)})
        {
            var plan=planner.Plan(surface,request with{LossFractionPerEdge=.5});Proof(plan);
            Check(plan.Nodes.Count==25&&plan.MaskedSamples==0,"complete inland samples, no ocean prerequisite");
            Near(plan.TotalReceived,1.5,"two analytic four-edge source paths with half loss");Near(plan.TotalLost,22.5,"analytic loss total");
            Near(plan.Nodes.Single(n=>n.Column==4&&n.Row==4).DistanceToTerminal,4*Math.Sqrt(2),"analytic plateau diagonal distance");
            planner.Validate(plan);
            var restored=JsonSerializer.Deserialize<WorldDrainagePlan>(Json(plan),WorldJson.CreateOptions())!;planner.Validate(restored);Check(Json(restored)==Json(plan),"JSON replay");
            var nodes=plan.Nodes.ToArray();nodes[0]=nodes[0] with{Retained=nodes[0].Retained+1};
            Reject(()=>planner.Validate(plan with{Nodes=nodes}),"WORLD_DRAINAGE_PLAN_MISMATCH");
            Reject(()=>planner.Validate(plan with{SurfaceSha256=new string('0',64)}),"WORLD_DRAINAGE_PLAN_MISMATCH");
            Check(Json(plan)==Json(planner.Plan(surface,request with{LossFractionPerEdge=.5,Sources=request.Sources.Reverse().ToArray()})),"source declaration order is canonical");
        }
        Check(Json(flat)==protectedInput,"protected terrain unchanged");
        var terrain=new WorldTerrainRecipe(1,"generated-height","land","Protected generated plane","drainage:terrain","meters",new(0,0),1,1,5,5,0,1,1,[]);
        var generated=planner.Plan(flat with{HeightField=null,Terrain=terrain},request);Proof(generated);planner.Validate(generated);
        Check(Json(generated.Surface.Terrain)==Json(terrain)&&generated.Surface.HeightField!.Heights.SequenceEqual(Surface((x,y)=>x+y).HeightField!.Heights),"generated surface uses the same replayed heightfield without terrain edits");
        var callerHeights=flat.HeightField!.Heights.ToList();var detached=planner.Plan(flat with{HeightField=flat.HeightField with{Heights=callerHeights}},request);
        callerHeights[0]=100;planner.Validate(detached);Near(detached.Surface.HeightField!.Heights[0],0,"input heights detached from caller mutation");
        var west=planner.Plan(flat,request);var east=planner.Plan(flat,request with{Terminals=[Sink("east",4,0)]});Proof(east);
        Check(west.SurfaceSha256==east.SurfaceSha256&&west.Nodes.Single(n=>n.SampleIndex==12).DownstreamSampleIndex!=east.Nodes.Single(n=>n.SampleIndex==12).DownstreamSampleIndex,"outlet edit changes drainage without touching heights");
        var sourceEdit=planner.Plan(flat,request with{Sources=[request.Sources[0] with{Rate=32},request.Sources[1]]});Proof(sourceEdit);Near(sourceEdit.TotalReceived,40,"source edit updates receipt");
        Check(west.Nodes.Select(n=>n.DownstreamSampleIndex).SequenceEqual(sourceEdit.Nodes.Select(n=>n.DownstreamSampleIndex)),"rates do not reroute terrain graph");
        var bowl=Surface((x,y)=>(x-2)*(x-2)+(y-2)*(y-2));
        Reject(()=>planner.Plan(bowl,request),"WORLD_DRAINAGE_UNREACHABLE_SAMPLE:");
        var basin=planner.Plan(bowl,request with{Terminals=[Sink("lake",2,2,WorldDrainageTerminalKind.RetainedBasin)]});Proof(basin);Near(basin.Terminals.Single().Received,24,"declared basin receives inflow");
        Check(Json(basin.Surface.HeightField!.Heights)==Json(bowl.HeightField!.Heights),"basin is not filled or carved");

        var barrier=flat with{Exclusions=[new("barrier","Narrow protected barrier","drainage:barrier",Box(1.4,0,.2,4))]};
        Reject(()=>planner.Plan(barrier,request),"WORLD_DRAINAGE_UNREACHABLE_SAMPLE:");
        var two=request with{Terminals=[Sink("west",0,0),Sink("east",4,0)]};
        var disconnected=planner.Plan(barrier,two);Proof(disconnected);
        Check(disconnected.Nodes.Count==25&&disconnected.BlockedEdges>0,"sub-sample barrier blocks edges even when no vertices are masked");
        Check(disconnected.Nodes.All(n=>n.TerminalId==(n.Column<2?"west":"east")),"independent component-to-sink mapping");
        Check(Json(disconnected)==Json(planner.Plan(barrier,two with{Terminals=two.Terminals.Reverse().ToArray()})),"terminal order is canonical");
        var excluded=planner.Plan(flat with{Exclusions=[new("strip","Masked strip","drainage:strip",Box(1.9,0,.2,4))]},two);Proof(excluded);Check(excluded.Nodes.Count==20&&excluded.MaskedSamples==5,"excluded vertices accounted separately");
        foreach(double height in new[]{100d,-100d})
        {
            var overlay=flat with{Patches=[new("overlay","ground","Hidden interior height change","drainage:overlay",Box(1.4,0,.2,4),1,new(0,0),height,0,0)]};
            Reject(()=>planner.Plan(overlay,request),"WORLD_DRAINAGE_UNREACHABLE_SAMPLE:");
            var split=planner.Plan(overlay,two);Proof(split);Check(split.BlockedEdges>0,"full edge traces reject hidden peak and trough");
            Check(split.Nodes.All(n=>n.TerminalId==(n.Column<2?"west":"east")),"hidden overlay cannot be crossed in either direction");
        }
        var unmodifiedOverlay=planner.Plan(flat with{Patches=[new("flat-overlay","ground","Equal-height overlay","drainage:overlay",Box(1.4,0,.2,4),1,new(0,0),0,0,0)]},request);Proof(unmodifiedOverlay);
        Reject(()=>planner.Plan(flat,request with{MaximumSamples=24}),"WORLD_DRAINAGE_SAMPLE_BUDGET");
        var exact=planner.Plan(flat,request);Check(planner.Plan(flat,request with{MaximumWork=exact.Work}).Work==exact.Work,"exact work boundary");
        Reject(()=>planner.Plan(flat,request with{MaximumWork=exact.Work-1}),"WORLD_DRAINAGE_WORK_BUDGET");
        Reject(()=>planner.Plan(flat,request with{Terminals=[]}),"WORLD_DRAINAGE_DEFINITION_INVALID");
        Reject(()=>planner.Plan(flat,request with{LossFractionPerEdge=double.NaN}),"WORLD_DRAINAGE_DEFINITION_INVALID");
        Reject(()=>planner.Plan(flat,request with{Terminals=[Sink("invalid",-1,0)]}),"WORLD_DRAINAGE_ENDPOINT_INVALID");
        Reject(()=>planner.Plan(flat,request with{Terminals=[Sink("one",0,0),Sink("two",0,0)]}),"WORLD_DRAINAGE_TERMINAL_SAMPLE_DUPLICATE");
        Reject(()=>planner.Plan(flat,request with{Sources=[request.Sources[0] with{Rate=double.PositiveInfinity}]}),"WORLD_DRAINAGE_SOURCE_INVALID");
        Reject(()=>planner.Plan(flat with{HeightField=null},request),"WORLD_DRAINAGE_HEIGHTFIELD_REQUIRED");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(flat,request,cancellation.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        await Task.CompletedTask;Console.WriteLine("PASS: plane/flat/basin/disconnected inland drainage; independent adjacency, acyclicity, downhill/potential, flow joins/conservation, explicit losses, source/outlet edits, unchanged heights, JSON/tamper, hidden barrier/peak/trough, finite bounds and cancellation.");
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Examples already exist");
        var request=Request();var flat=Surface((x,y)=>0);
        foreach(var (name,surface,definition) in new[]{("plateau",flat,request with{LossFractionPerEdge=.5}),
            ("retained-basin",Surface((x,y)=>(x-2)*(x-2)+(y-2)*(y-2)),request with{Terminals=[Sink("lake",2,2,WorldDrainageTerminalKind.RetainedBasin)]}),
            ("disconnected",flat with{Exclusions=[new("barrier","Narrow protected barrier","drainage:barrier",Box(1.4,0,.2,4))]},request with{Terminals=[Sink("west",0,0),Sink("east",4,0)]})})
        {
            string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);var plan=new WorldDrainagePlanner().Plan(surface,definition);Proof(plan);
            await File.WriteAllTextAsync(Path.Combine(folder,"surface.json"),Json(surface));await File.WriteAllTextAsync(Path.Combine(folder,"definition.json"),Json(definition));await File.WriteAllTextAsync(Path.Combine(folder,"plan.json"),Json(plan));
        }
    }
}
