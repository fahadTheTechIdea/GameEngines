using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldPlacementResumeTests
{
    public static void Run()
    {
        static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
        var boundary=Box(0,0,20,10);
        var surface=new WorldSurfaceDefinition(1,"flat","meters",boundary,[],[],HeightField:
            new(1,"flat","ground","Resumable placement fixture","test","meters",new(0,0),10,10,3,2,[0,0,0,0,0,0]));
        var environment=new WorldEnvironmentQuery(new(surface),new(1,"coordinates",
            [new("x","Authored x coordinate","test","meters",0,[new(WorldFieldInput.X,1)])]));
        var profile=new WorldSuitabilityProfile("ground","Required ground near authored x target","test",["ground"],[],
            [new("target-x","x","Prefer authored x target","test",10,20,1)]);
        var request=new WorldPlacementRequest("resume","Enumerate alternate exact source placements","test",boundary,
            [new("site","Required site","test",Box(-1,-1,2,2),profile)],[],[]);
        var planner=new WorldSitePlacementPlanner();
        string Json(WorldPlacementPlan plan)=>JsonSerializer.Serialize(plan,WorldJson.CreateOptions(true));
        void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        using(var choices=planner.Enumerate(environment,request,null,default).GetEnumerator())
        {
            Check(choices.MoveNext()&&choices.Current.Status==WorldPlacementStatus.Found,"first assignment");
            var first=choices.Current;var saved=Json(first);
            Check(choices.MoveNext()&&choices.Current.Status==WorldPlacementStatus.Found,"resume after rejected assignment");
            Check(first.Sites[0].Position!=choices.Current.Sites[0].Position,"resume advances candidate");
            Check(choices.Current.Conflicts["dependent-allocation"]==1,"resumption records rejection once");
            var terminal=choices.Current;
            while(choices.MoveNext())terminal=choices.Current;
            Check(terminal.Status==WorldPlacementStatus.NoFeasibleAssignment,"explicit terminal result");
            Check(Json(first)==saved,"later search cannot mutate saved assignment diagnostics or conflicts");
        }
        var budget=new WorldSpatialWorkBudget(request.MaximumWork,default);
        var suspended=planner.Enumerate(environment,request,budget,default).GetEnumerator();
        Check(suspended.MoveNext(),"suspend active search");var work=budget.Used;suspended.Dispose();
        Check(!suspended.MoveNext()&&budget.Used==work,"disposing suspended search does no additional search work");
        Check(Json(planner.Plan(environment,request))==Json(planner.Plan(environment,request)),"disposed search does not affect subsequent replay");
        using(var cancel=new CancellationTokenSource())
        using(var choices=planner.Enumerate(environment,request,null,cancel.Token).GetEnumerator())
        {
            Check(choices.MoveNext(),"assignment before cancellation");cancel.Cancel();
            try{choices.MoveNext();throw new Exception("Missing cancellation on resume");}catch(OperationCanceledException){}
        }
        var prescribed=request with{Execution=WorldPlacementExecution.Prescribed,Anchors=[new("fixed",new(5,5))],
            Relations=[new("pin","site","fixed","Required exact pose","test",0,0,0,1)]};
        var plans=planner.Enumerate(environment,prescribed,null,default).ToArray();
        Check(plans.Length==2&&plans[0].Status==WorldPlacementStatus.Found&&plans[1].Status==WorldPlacementStatus.NoFeasibleAssignment,
            "prescribed assignment has one solution and one terminal result");
        Check(plans[0].Conflicts["dependent-allocation"]==0&&plans[1].Conflicts["dependent-allocation"]==1,"prescribed snapshots detached");
        Console.WriteLine("PASS: resumable assignment snapshots, alternate candidates, terminal exhaustion, suspended disposal, cancellation and prescribed replay.");
    }
}
