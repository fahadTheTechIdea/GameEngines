using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldRouteCostBoundTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldTerrainPatch Patch(string id,string kind,WorldPolygon boundary,int priority)=>new(id,kind,"Declared cost bound control","bounds:"+id,boundary,priority,new(0,0),0,0,0);
    private static void Check(bool value,string message){if(!value)throw new Exception("Terrain cost bound: "+message);}
    internal static void Run(string? output=null)
    {
        if(output is not null&&(File.Exists(output)||Directory.Exists(output)))throw new IOException("Output exists");
        var ground=Patch("ground","ground",Box(0,0,100,100),0);var bog=Patch("bog","bog",Box(0,35,100,10),10);
        var profile=new WorldTraversalProfile("walk","Preserve exact terrain cost","bounds:profile",.2,0,new Dictionary<string,double>{{"ground",1},{"bog",9},{"rough",5}});
        var metric=new WorldSurfaceTraversal(new(new WorldSurfaceDefinition(1,"metric","meters",Box(0,0,100,100),[],[ground,bog])),profile);
        var metricBudget=new WorldSpatialWorkBudget(20000000,default);metric.PrepareCostBounds(metricBudget,default);
        var start=new WorldPoint(20,40);var middle=new WorldPoint(20,40.05);var end=new WorldPoint(20,40.1);
        Check(metric.CostLowerBound(start,end,metricBudget)<=metric.CostLowerBound(start,middle,metricBudget)+metric.CostLowerBound(middle,end,metricBudget)+1e-12,
            "conservative rounding must preserve heuristic consistency for small collinear steps");
        int qualified=0,bounds=0;var proofs=new List<object>();
        double Verify(IReadOnlyList<WorldTerrainPatch> patches,WorldPoint from,WorldPoint to,IReadOnlyList<WorldPoint[]> paths)
        {
            var surface=new WorldSurfaceDefinition(1,"bound-surface","meters",Box(0,0,100,100),[],patches);var traversal=new WorldSurfaceTraversal(new(surface),profile);
            var budget=new WorldSpatialWorkBudget(20000000,default);traversal.PrepareCostBounds(budget,default);double lower=traversal.CostLowerBound(from,to,budget);bounds++;
            var witnesses=new List<object>();foreach(var points in paths)
            {
                Check(points[0]==from&&points[^1]==to,"independent witness endpoints");double cost=0;bool valid=true;
                for(int i=1;i<points.Length;i++){var part=traversal.Evaluate(points[i-1],points[i],budget,default);if(part is null){valid=false;break;}cost+=part.Total;}
                if(valid){qualified++;Check(lower<=cost+1e-8,"lower bound cannot exceed a complete qualified path");witnesses.Add(new{Points=points,Cost=cost});}
            }
            proofs.Add(new{Surface=surface,Profile=profile,From=from,To=to,LowerBound=lower,QualifiedPaths=witnesses,Work=budget.Used});
            Check(budget.Used>0,"bound preparation and lookup consume shared work");return lower;
        }
        var a=new WorldPoint(20,10);var b=new WorldPoint(20,80);var vertical=new[]{new[]{a,b},new[]{a,new WorldPoint(70,10),new WorldPoint(70,80),b}};
        double band=Verify([ground,bog],a,b,vertical);Check(Math.Abs(band-150)<1e-7,"full-span band adds exact unavoidable projected excess");
        var overridePatch=Patch("cheap-crossing","ground",Box(18,0,4,100),20);
        double weakened=Verify([ground,bog,overridePatch],a,b,vertical);Check(Math.Abs(weakened-70)<1e-7,"higher-priority cheaper opening weakens the entire conservative bound");
        var partial=bog with{Id="partial",Boundary=Box(0,35,60,10)};
        Check(Math.Abs(Verify([ground,partial],a,b,vertical)-70)<1e-7,"partial band cannot demand unavoidable crossing");
        var first=Patch("first","rough",Box(0,20,100,20),10);var second=Patch("second","bog",Box(0,30,100,30),20);
        Check(Math.Abs(Verify([ground,first,second],a,b,vertical)-350)<1e-7,"overlapping parallel bands are partitioned rather than added twice");
        var diagonalA=new WorldPoint(20,20);var diagonalB=new WorldPoint(80,80);
        var xBand=Patch("x-band","bog",Box(10,0,80,100),10);var yBand=Patch("y-band","bog",Box(0,10,100,80),20);
        double crossing=Verify([ground,xBand,yBand],diagonalA,diagonalB,[new[]{diagonalA,diagonalB}]);
        Check(crossing<diagonalA.DistanceTo(diagonalB)*9,"perpendicular projections use maximum, not an unsafe sum");
        for(int i=0;i<12;i++)
        {
            var from=new WorldPoint(10+i*3,10+i);var to=new WorldPoint(80-i*2,80-i);
            Verify([ground,first,second,overridePatch],from,to,[new[]{from,to},new[]{from,new WorldPoint(85,15),to}]);
        }
        Check(qualified>=20,"multiple real segment/terrain witnesses exercised");
        if(output is not null)File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{Configurations=bounds,QualifiedPaths=qualified,Proofs=proofs},WorldCreator.Core.WorldJson.CreateOptions()));
        Console.WriteLine($"PASS: {bounds} cost-bound configurations, {qualified} complete qualified path witnesses; full/partial/overlapping/perpendicular bands, cheaper overrides and charged work.");
    }
}
