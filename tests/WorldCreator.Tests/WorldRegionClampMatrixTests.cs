using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldRegionClampMatrixTests
{
    static void Check(bool value,string reason){if(!value)throw new Exception("Clamp matrix: "+reason);}
    static WorldPolygon Box()=>new([new(0,0),new(10,0),new(10,10),new(0,10)]);
    static WorldSuitabilityProfile Profile(string id,double min,double max)=>new(id,"Declared suitable range","test:clamp",["ground"],
        [new("range","clamped","Hard field range","test:range",min,max)],[]);
    static void RunMatrix(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var box=Box();var surface=new WorldSurfaceDefinition(1,"plane","meters",box,[],[new("ground","ground","Flat test terrain","test:ground",box,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentQuery(new(surface),new(1,"fields",[new("clamped","Clamped horizontal field","test:field","units",0,[new(WorldFieldInput.X,1)],2,8)]));
        var planner=new WorldRegionPlanner();var rows=new List<object>();int generated=0,rejected=0;
        foreach(double minimum in new[]{-1d,2d,3d,5d,7d,8d,11d})foreach(double maximum in new[]{-1d,2d,3d,5d,7d,8d,11d})
        {
            if(maximum<minimum)continue;
            double left=minimum<=2?0:minimum,right=maximum>=8?10:maximum;
            bool feasible=minimum<=8&&maximum>=2&&right>left;
            if(!feasible&&minimum==maximum&&minimum>2&&minimum<8)continue;
            var profile=Profile("range",minimum,maximum);var seed=new WorldPoint(feasible?(left+right)/2:5,5);
            var request=new WorldRegionRequest("region","Eligible region","test:region",box,profile,[new("area","Required region","test:area",seed,.01)]);
            if(!feasible)
            {
                bool refused=false;try{planner.Plan(environment,request);}catch(ArgumentException e)when(e.Message.StartsWith("WORLD_REGION_SEED_INVALID")){refused=true;}
                Check(refused,$"empty range refused {minimum}/{maximum}");rejected++;rows.Add(new{minimum,maximum,Outcome="Rejected"});continue;
            }
            var result=planner.Plan(environment,request);double expected=(right-left)*10;
            Check(Math.Abs(result.SuitableArea-expected)<1e-7,$"area {minimum}/{maximum}: {result.SuitableArea} expected {expected}");
            Check(Math.Abs(result.Regions.Single().Area-expected)<1e-7,"selected region retains full area");generated++;
            rows.Add(new{minimum,maximum,Outcome="Generated",ExpectedArea=expected,ActualArea=result.SuitableArea});
        }
        var leftProfile=Profile("left",-1,3);var rightProfile=Profile("right",7,11);
        var multi=new WorldRegionRequest("multi","Two distinct eligibility regions","test:multi",box,leftProfile,
            [new("left","Left region","test:left",new(1,5),1),new("right","Right region","test:right",new(9,5),1,rightProfile)]);
        var resultMulti=planner.Plan(environment,multi);Check(resultMulti.Regions.Count==2&&Math.Abs(resultMulti.SuitableArea-60)<1e-7,"two profiles retain their disjoint sixty-square-unit union");
        Check(resultMulti.Regions.All(r=>Math.Abs(r.Area-30)<1e-7),"both profile-specific areas retained");
        var intersect=Profile("intersect",-1,5) with{Requirements=[new("a","clamped","First range","test:a",-1,5),new("b","clamped","Second range","test:b",4,11)]};
        var intersectRequest=new WorldRegionRequest("intersection","Both rules apply","test:intersection",box,intersect,[new("area","Required area","test:area",new(4.5,5),1)]);
        Check(Math.Abs(planner.Plan(environment,intersectRequest).SuitableArea-10)<1e-7,"multiple requirements intersect, rather than union");
        foreach(bool lower in new[]{true,false})
        {
            var oneSided=new WorldEnvironmentQuery(new(surface),new(1,"one-sided",[new("clamped","One-sided field","test:field","units",0,[new(WorldFieldInput.X,1)],lower?2:null,lower?null:8)]));
            var plateau=Profile("plateau",lower?2:8,lower?2:8);
            var request=new WorldRegionRequest("plateau","One-sided plateau","test:plateau",box,plateau,[new("area","Required plateau","test:area",new(lower?1:9,5),1)]);
            Check(Math.Abs(planner.Plan(oneSided,request).SuitableArea-20)<1e-7,"one-sided clamp retains exact plateau");
        }
        var tiny=Profile("tiny",5,5+1e-10);var tinyRequest=new WorldRegionRequest("tiny","Genuine narrow area","test:tiny",box,tiny,[new("tiny","Narrow area","test:tiny",new(5+5e-11,5),1e-12)]);
        bool tinyRefused=false;try{planner.Plan(environment,tinyRequest);}catch(ArgumentException e)when(e.Message.StartsWith("WORLD_REGION_GEOMETRY_RESOLUTION")){tinyRefused=true;}
        Check(tinyRefused,"real sub-resolution area is not silently deleted");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(environment,multi,cancel.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}}
        File.WriteAllText(Path.Combine(output,"matrix.json"),JsonSerializer.Serialize(new{generated,rejected,rows,MultiProfileArea=resultMulti.SuitableArea,TinyRefused=tinyRefused},WorldJson.CreateOptions(true)));
        Console.WriteLine($"PASS: {generated} analytic clamped ranges, {rejected} empty refusals, two-profile union, real narrow strip and cancellation.");
    }
    internal static void Run(string output)=>RunMatrix(output);
}
