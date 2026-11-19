using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldSampledOverlayTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run()
    {
        var values=Enumerable.Range(0,81).Select(i=>(i%9)*.02+(i/9)*.03).ToArray();
        var raised=values.ToArray();raised[4*9+4]+=4;
        var field=new WorldHeightField(1,"height","ground","Sampled terrain","test:height","meters",new(0,0),10,10,9,9,values);
        var surface=new WorldSurfaceDefinition(1,"surface","meters",Box(0,0,80,80),[],[],HeightField:field);
        WorldTerrainPatch Patch(string id,string kind,int priority,WorldPolygon shape,double gx=0)=>new(id,kind,"Overlay","test:overlay",shape,priority,new(0,0),1,gx,0);
        var good=Patch("good","road",2,Box(15,15,50,30));
        var cases=new (string Name,WorldSurfaceDefinition Surface,Dictionary<string,double> Allowed)[]{
            ("safe",surface with{Patches=[good]},new(){{"ground",1},{"road",1}}),
            ("overlap-unique",surface with{Patches=[good,Patch("upper","road",3,Box(30,20,35,35))]},new(){{"ground",1},{"road",1}}),
            ("ambiguous-priority",surface with{Patches=[good,Patch("same","road",2,Box(30,20,35,35))]},new(){{"ground",1},{"road",1}}),
            ("forbidden-kind",surface with{Patches=[good,Patch("water","water",3,Box(35,30,6,5))]},new(){{"ground",1},{"road",1}}),
            ("forbidden-kind-masked",surface with{Patches=[good,Patch("water","water",3,Box(35,30,6,5)),Patch("bridge","road",4,Box(32,27,12,11))]},new(){{"ground",1},{"road",1}}),
            ("steep-overlay",surface with{Patches=[good,Patch("steep","road",3,Box(35,30,6,5),.5)]},new(){{"ground",1},{"road",1}}),
            ("masked-hazard",surface with{HeightField=field with{Heights=raised},Patches=[Patch("cover","road",2,Box(30,30,20,20))]},new(){{"ground",1},{"road",1}}),
            ("forbidden-base-covered",surface with{Patches=[Patch("cover","road",2,Box(10,10,60,60))]},new(){{"road",1}}),
            ("excluded",surface with{Patches=[good],Exclusions=[new("hole","Protected area","test:hole",Box(24,22,7,8))]},new(){{"ground",1},{"road",1}})
        };
        int checkedCount=0;long safeWork=0,exactWork=0;
        foreach(var item in cases)
        {
            var query=new WorldSurfaceQuery(item.Surface);var profile=new WorldTraversalProfile("walk","Qualify exact corridor","test:walk",.6,.1,item.Allowed);
            var traversal=new WorldSurfaceTraversal(query,profile);var exact=new WorldSurfaceAreaQuery(query,false);
            foreach(double x in new[]{.5,14.5,24,34,40,59.5,79.5})foreach(double y in new[]{.5,14.5,22,30,39.5,60})
            {
                var envelope=WorldSurfaceTraversal.Envelope(new(x,y),new(x+3,y+4),profile.Radius);
                var expectedBudget=new WorldSpatialWorkBudget(20000000,default);var actualBudget=new WorldSpatialWorkBudget(20000000,default);
                bool expected=exact.Matches(envelope,traversal.Allows,expectedBudget,default);
                bool actual=traversal.AllowsEnvelope(envelope,actualBudget,default);
                Check(actual==expected,$"Mismatch {item.Name} at {x},{y}: expected {expected}, got {actual}");checkedCount++;
                if(item.Name=="safe"){safeWork+=actualBudget.Used;exactWork+=expectedBudget.Used;}
            }
        }
        Check(safeWork<exactWork,"Safe overlays reduce charged envelope work compared with exact sweep.");
        // A disjoint same-priority patch forces the exact fallback but never
        // changes any in-domain sample. Both searches therefore see the same
        // traversable terrain and must return the same lowest-cost passage.
        var routing=surface with{Patches=[good],Exclusions=[new("barrier","Required detour","test:barrier",Box(35,20,10,30))]};
        var exactRouting=routing with{Patches=[good,Patch("remote","road",2,Box(100,100,5,5))]};
        var routeProfile=new WorldTraversalProfile("route","Lowest-cost comparison","test:route",.6,.1,new Dictionary<string,double>{{"ground",1},{"road",1.2}});
        var routeRequest=new WorldRouteRequest("passage","Reach destination","test:route",new(10,35),new(70,35),routeProfile,new(.2,[],MaximumWork:100000000));
        var owner=new WorldSurfaceRoutePlanner();var fast=owner.Plan(routing,routeRequest);var reference=owner.Plan(exactRouting,routeRequest);
        Check(fast.Status==WorldRouteStatus.Found&&reference.Status==WorldRouteStatus.Found,"Both lowest-cost searches find a complete detour.");
        Check(Math.Abs(fast.Cost!.Total-reference.Cost!.Total)<1e-8&&fast.Path!.Points.SequenceEqual(reference.Path!.Points),"Fast and exact-fallback searches return the same lowest-cost path and cost.");
        Check(fast.Work<reference.Work,"Full route search spends less accounted work than the exact fallback.");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{new WorldSurfaceTraversal(new(surface),new("walk","walk","test",.5,.1,new Dictionary<string,double>{{"ground",1}})).AllowsEnvelope(Box(1,1,2,2),new WorldSpatialWorkBudget(1000,cancellation.Token),cancellation.Token);throw new Exception("Cancellation ignored");}
        catch(OperationCanceledException){}
        Console.WriteLine($"PASS: {checkedCount} exact overlay-envelope comparisons; safe-overlay work {safeWork} versus exact {exactWork}; lowest-cost route work {fast.Work} versus {reference.Work}; cancellation.");
    }
}
