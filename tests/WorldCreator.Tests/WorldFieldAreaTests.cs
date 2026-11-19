using System.Diagnostics;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldFieldAreaTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldHeightField Field(int size=5,double step=2)=>new(1,"field","ground","Footprint qualification","test","meters",new(0,0),step,step,size,size,
        Enumerable.Repeat(0.0,size*size).ToArray());

    // Test-only independent spatial representation: expand every cell into planar polygons.
    // Unique priorities reproduce the documented shared-edge ownership; no field sweep runs here.
    private static WorldSurfaceQuery Oracle(WorldSurfaceDefinition surface)
    {
        var f=surface.HeightField!;var patches=new List<WorldTerrainPatch>();
        for(int y=0;y<f.Rows-1;y++)for(int x=0;x<f.Columns-1;x++)
        {
            var a=new WorldPoint(f.Origin.X+x*f.StepX,f.Origin.Y+y*f.StepY);var b=a.Offset(f.StepX,0);var c=a.Offset(0,f.StepY);var d=a.Offset(f.StepX,f.StepY);
            int i=y*f.Columns+x;double h=f.Heights[i],right=f.Heights[i+1],up=f.Heights[i+f.Columns],diagonal=f.Heights[i+f.Columns+1];
            int priority=2*(y*f.Columns+x);
            patches.Add(new($"upper-{x}-{y}",f.Kind,f.Purpose,f.SourceRuleId,new([a,d,c]),priority,a,h,(diagonal-up)/f.StepX,(up-h)/f.StepY));
            patches.Add(new($"lower-{x}-{y}",f.Kind,f.Purpose,f.SourceRuleId,new([a,b,d]),priority+1,a,h,(right-h)/f.StepX,(diagonal-right)/f.StepY));
        }
        return new(surface with{HeightField=null,Terrain=null,Patches=patches.Concat(surface.Patches.Select(p=>p with{Priority=1000+p.Priority})).ToArray()});
    }

    public static void Run()
    {
        var f=Field();var heights=f.Heights.ToArray();heights[2*5+2]=5;heights[1*5+3]=2;f=f with{Heights=heights};
        var basic=new WorldSurfaceDefinition(1,"field-area","meters",Box(-1,-1,10,10),[],[],HeightField:f);
        var mask=new WorldTerrainPatch("water","water","Narrow barrier","test:water",Box(3.141,-1,.01,10),1,new(0,0),0,0,0);
        var surfaces=new[]{basic,basic with{Patches=[mask]},basic with{Exclusions=[new("slit","Forbidden strip","test:slit",Box(1.7,-1,.013,10))]},
            basic with{Patches=[mask,mask with{Id="cover",Kind="ground",Priority=2,Boundary=Box(3.13,-1,.04,10)}]},
            basic with{Boundary=new([new(0,0),new(8,0),new(8,4),new(4,4),new(4,8),new(0,8)])}};
        WorldPolygon[] footprints=[Box(.25,.25,7.5,7.5),Box(.25,.25,.4,.4),Box(3.4,3.4,1.1,1.1),Box(-.5,1,1,1),
            new([new(.5,.5),new(7.5,.8),new(7,1.1),new(.5,.9)]),
            new([new(.5,.5),new(7.5,.5),new(7.5,1.5),new(1.5,1.5),new(1.5,7.5),new(.5,7.5)])];
        int comparisons=0,accepted=0,rejected=0;
        foreach(var definition in surfaces)
        {
            var actual=new WorldSurfaceAreaQuery(new(definition));var oracle=new WorldSurfaceAreaQuery(Oracle(definition));
            foreach(var footprint in footprints)foreach(double slope in new[]{.1,1.5,10.0})
            {
                bool Predicate(WorldSurfaceSample s)=>s.Status==WorldSurfaceStatus.Sampled&&s.Kind=="ground"&&s.Slope<=slope;
                bool a=actual.Matches(footprint,Predicate),b=oracle.Matches(footprint,Predicate);
                Check(a==b,$"expanded-plane oracle mismatch #{comparisons}");comparisons++;if(a)accepted++;else rejected++;
            }
        }
        Check(accepted>0&&rejected>0,"oracle covers both acceptance and rejection");
        var shifted=basic with{Boundary=Box(-11,19,10,18),HeightField=f with{Origin=new(-10,20),StepY=4}};
        var shiftedArea=new WorldPolygon([new(-9.5,21),new(-2.5,22),new(-3,30),new(-8.5,35)]);
        foreach(double slope in new[]{.1,1.5,10.0})
        {
            bool Predicate(WorldSurfaceSample s)=>s.Status==WorldSurfaceStatus.Sampled&&s.Slope<=slope;
            Check(new WorldSurfaceAreaQuery(new(shifted)).Matches(shiftedArea,Predicate)==new WorldSurfaceAreaQuery(Oracle(shifted)).Matches(shiftedArea,Predicate),"negative origin/non-square sampling oracle");
            comparisons++;
        }
        var watch=Stopwatch.StartNew();
        var dense=Field(513,.25);var extent=Box(0,0,128,128);
        var large=new WorldSurfaceDefinition(1,"large","meters",extent,[],[],HeightField:dense);
        var query=new WorldSurfaceQuery(large);var areas=new WorldSurfaceAreaQuery(query);
        Check(areas.Matches(Box(1,1,126,126),s=>s.Status==WorldSurfaceStatus.Sampled&&s.Slope<=.01),"full dense field accepts");
        long whole=watch.ElapsedMilliseconds;watch.Restart();
        var profile=new WorldTraversalProfile("walk","Long traversal","test",.3,.1,new Dictionary<string,double>{{"ground",1}});
        var cost=new WorldSurfaceTraversal(query,profile).Evaluate(new(2,2),new(126,126));
        Check(cost is not null,"long diagonal passage accepts without all-bounds mesh expansion");
        Near(cost!.Distance,124*Math.Sqrt(2),"exact long path distance");long diagonalTime=watch.ElapsedMilliseconds;
        var fullRoute=new WorldSurfaceRoutePlanner().Plan(query,new("dense-route","Connect game anchors","test",new(2,2),new(126,126),profile,new(.1,[])));
        Check(fullRoute.Status==WorldRouteStatus.Found&&fullRoute.Path!.Points.Count==2,"actual dense-field route planner finds direct connection");
        // A one-node off-center elevation spike intersects the passage width but not its centerline.
        var pocket=dense.Heights.ToArray();pocket[258*513+256]=8;
        var offCenter=new WorldSurfaceQuery(large with{HeightField=dense with{Heights=pocket}});
        var wide=profile with{Radius=.8};
        Check(offCenter.Trace(new(2,64),new(126,64)).All(s=>s.Sample.Slope==0),"off-center fixture has flat centerline");
        Check(new WorldSurfaceTraversal(offCenter,wide).Evaluate(new(2,64),new(126,64)) is null,"off-center steep pocket rejects full passage");
        var overridePatch=new WorldTerrainPatch("level","ground","Authored level pad","test:pad",Box(63,63,2,2),1,new(0,0),0,0,0);
        Check(new WorldSurfaceTraversal(new(large with{HeightField=dense with{Heights=pocket},Patches=[overridePatch]}),wide).Evaluate(new(2,64),new(126,64)) is not null,"higher authored overlay masks field slope");
        var narrow=new WorldSurfaceQuery(large with{Patches=[mask with{Boundary=Box(64.123,0,.001,128)}]});
        Check(new WorldSurfaceTraversal(narrow,profile).Evaluate(new(2,2),new(126,126)) is null,"thin polygon barrier survives field acceleration");
        var concave=new WorldPolygon([new(60,60),new(68,60),new(68,61),new(61,61),new(61,68),new(60,68)]);
        var gap=dense.Heights.ToArray();gap[256*513+256]=100;
        Check(new WorldSurfaceAreaQuery(new(large with{HeightField=dense with{Heights=gap}})).Matches(concave,s=>s.Slope<=.01),"concave gap does not import steep terrain outside footprint");
        using(var cancellation=new CancellationTokenSource())
        {
            try{areas.Matches(Box(1,1,126,126),s=>{cancellation.Cancel();return true;},cancellation.Token);throw new Exception("Cancellation missing");}
            catch(OperationCanceledException){}
        }
        try{new WorldSurfaceRoutePlanner().Plan(query,new("budget","Bounded path","test",new(2,2),new(126,126),profile,new(.1,[],MaximumWork:100)));
            throw new Exception("Work budget missing");}catch(ArgumentException e)when(e.Message=="WORLD_ROUTE_WORK_BUDGET"){}
        Console.WriteLine($"PASS: {comparisons} field-versus-planar oracle comparisons (first 90: {accepted} accepted / {rejected} rejected), including negative origin/non-square sampling; 513x513 whole-area, diagonal passage and actual route planner, off-center steep pocket, thin overlay, priority cover, concave gap, uncovered domain, cancellation and shared work limit.");
        Console.WriteLine($"Observed local timings: whole dense footprint {whole} ms; long diagonal passage {diagonalTime} ms. No universal performance claim.");
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Field area: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7,message);
}
