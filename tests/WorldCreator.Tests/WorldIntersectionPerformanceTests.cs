using WorldCreator.Core.Geometry;

internal static class WorldIntersectionPerformanceTests
{
    public static void Run()
    {
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Reject(WorldPoint a,WorldPoint b,WorldPoint c,WorldPoint d)
        {try{PlanarIntersections.Intersect(a,b,c,d);}catch(ArgumentException){return;}throw new Exception("Invalid intersection accepted");}
        var cross=PlanarIntersections.Intersect(new(0,0),new(10,0),new(5,-5),new(5,5));
        Check(cross.Kind==IntersectionKind.Point&&cross.Point==new WorldPoint(5,0),"crossing");
        Check(PlanarIntersections.Intersect(new(0,0),new(10,0),new(0,2),new(10,2)).Kind==IntersectionKind.None,"parallel");
        Check(PlanarIntersections.Intersect(new(0,0),new(10,0),new(5,0),new(15,0)).Kind==IntersectionKind.CollinearOverlap,"overlap");
        Check(PlanarIntersections.Intersect(new(0,0),new(5,0),new(5,0),new(5,5)).Point==new WorldPoint(5,0),"endpoint");
        for(int i=0;i<4;i++)
        {
            var points=new[]{new WorldPoint(0,0),new WorldPoint(1,0),new WorldPoint(0,1),new WorldPoint(1,1)};
            points[i]=new(double.NaN,0);Reject(points[0],points[1],points[2],points[3]);
        }
        Reject(new(0,0),new(0,0),new(0,1),new(1,1));
        for(int i=0;i<10000;i++)PlanarIntersections.Intersect(new(0,0),new(10,0),new(0,2),new(10,2));
        long before=GC.GetAllocatedBytesForCurrentThread();int none=0;
        for(int i=0;i<500000;i++)if(PlanarIntersections.Intersect(new(0,0),new(10,0),new(0,2),new(10,2)).Kind==IntersectionKind.None)none++;
        long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(none==500000,"all separated pairs");Check(bytes<=1024,"separated intersection calls must not allocate per call");
        Console.WriteLine($"PASS: intersection semantics and 500000 separated pairs; allocated bytes: {bytes}");
        ulong hash=14695981039346656037UL;uint state=1234567;
        double Next(){state=unchecked(1664525*state+1013904223);return (int)(state%2001)-1000;}
        void Mix(ulong value){hash=unchecked((hash^value)*1099511628211UL);}
        for(int i=0;i<20000;i++)
        {
            var a=new WorldPoint(Next(),Next());var b=new WorldPoint(Next(),Next());
            var c=new WorldPoint(Next(),Next());var d=new WorldPoint(Next(),Next());
            if(a==b||c==d){i--;continue;}
            if(i%4==0){c=new(a.X+1e-10,a.Y+1e-10);d=new(b.X+1e-10,b.Y+1e-10);}
            var result=PlanarIntersections.Intersect(a,b,c,d);Mix((ulong)result.Kind);
            if(result.Point is {} point){Mix((ulong)BitConverter.DoubleToInt64Bits(point.X));Mix((ulong)BitConverter.DoubleToInt64Bits(point.Y));}
        }
        Console.WriteLine($"Deterministic 20000-pair classification/coordinate hash: {hash:X16}");
    }
}
