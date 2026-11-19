using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldFootprintPredicateTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    public static void Run()
    {
        int cases=0;
        var boundary=Box(-10,-10,20,20);
        var ground=new WorldTerrainPatch("ground","ground","Declared test plane","predicate:plane",boundary,0,new(0,0),0,0,0);
        var surface=new WorldSurfaceDefinition(1,"predicate-world","meters",boundary,[],[ground]);
        var fields=new WorldEnvironmentDefinition(1,"predicate-fields",[new("height","Height","predicate:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var profile=new WorldSuitabilityProfile("hard-ranges","Whole-footprint constraints","predicate:rules",["ground"],
            [new("height","height","Allowed height","predicate:height",0,0)],[]);
        void Agree(string id,WorldSurfaceDefinition terrain,WorldEnvironmentDefinition environment,WorldSuitabilityProfile rules,WorldPolygon polygon,bool expected)
        {
            var owner=new WorldFootprintSuitability(new(new(terrain),environment),rules);
            bool complete=owner.Evaluate(polygon).Eligible,answer=owner.Matches(polygon);
            if(complete!=expected||answer!=complete)throw new Exception("Footprint predicate disagreement: "+id+" report="+complete+" predicate="+answer+" expected="+expected);
            cases++;
        }
        var area=Box(-2,-1,4,2);
        Agree("flat",surface,fields,profile,area,true);
        Agree("outside",surface,fields,profile,Box(9,0,2,2),false);
        Agree("contained-hole",surface with{Exclusions=[new("hole","Query lies fully inside exclusion","predicate:hole",Box(-4,-4,8,8))]},fields,profile,area,false);
        var thin=ground with{Id="thin",Priority=1,Boundary=Box(.731,-5,.003,10),Elevation=7};
        Agree("off-center-height-sliver",surface with{Patches=[ground,thin]},fields,profile,area,false);
        Agree("off-center-water-sliver",surface with{Patches=[ground,thin with{Kind="water",Elevation=0}]},fields,profile,area,false);
        Agree("priority-cover",surface with{Patches=[ground,thin,thin with{Id="cover",Priority=2,Elevation=0,Boundary=Box(.7,-5,.1,10)}]},fields,profile,area,true);
        Agree("off-center-hole",surface with{Exclusions=[new("hole","Thin protected strip","predicate:hole",thin.Boundary)]},fields,profile,area,false);
        var concave=new WorldPolygon([new(-2,-2),new(2,-2),new(2,-1),new(-1,-1),new(-1,2),new(-2,2)]);
        Agree("concave-gap",surface with{Exclusions=[new("gap","Hole in vacant concave gap","predicate:gap",Box(0,0,1,1))]},fields,profile,concave,true);
        Agree("contained-priority-patch",surface with{Patches=[ground,ground with{Id="upper",Priority=1,Boundary=Box(-4,-4,8,8),Elevation=1}]},fields,profile,area,false);
        var clamped=fields with{Fields=[fields.Fields[0],new("rise","Positive x","predicate:rise","meters",0,[new(WorldFieldInput.X,1)],Minimum:0),
            new("fall","Negative x","predicate:fall","meters",0,[new(WorldFieldInput.X,-1)],Minimum:0),
            new("peak","Interior clamp peak","predicate:peak","relative",1,[new(WorldFieldInput.Field,-1,"rise"),new(WorldFieldInput.Field,-1,"fall")],Minimum:0),
            new("zero","Correlated cancellation","predicate:zero","relative",0,[new(WorldFieldInput.Field,1,"peak"),new(WorldFieldInput.Field,-1,"peak")])]};
        Agree("interior-clamp-peak",surface,clamped,profile with{Requirements=[new("peak","peak","Reject interior peak","predicate:peak",0,.5)]},area,false);
        Agree("correlated-zero",surface,clamped,profile with{Requirements=[new("zero","zero","Exact correlated cancellation","predicate:zero",0,0)]},area,true);
        var tent=new WorldHeightField(1,"tent","ground","Interior heightfield peak","predicate:tent","meters",new(-2,-2),1,1,5,5,Enumerable.Range(0,25).Select(i=>i==18?9d:0d).ToArray());
        var fieldSurface=surface with{Boundary=Box(-2,-2,4,4),Patches=[],HeightField=tent};
        Agree("triangle-interior-peak",fieldSurface,fields,profile,Box(-2,-2,4,4),false);
        var scalar=new WorldSampledScalarField(1,"scalar","Independent sampled peak","predicate:scalar","relative",tent.Origin,1,1,5,5,tent.Heights);
        Agree("sampled-scalar-interior-peak",fieldSurface with{HeightField=tent with{Heights=Enumerable.Repeat(0d,25).ToArray()}},
            new(1,"scalar-fields",[new("height","Sampled environmental peak","predicate:scalar","relative",0,[new(WorldFieldInput.SampledField,1,"scalar")])],[scalar]),profile,Box(-2,-2,4,4),false);
        var plane=new WorldHeightField(1,"plane","ground","Known non-square sampled plane","predicate:plane","meters",new(-3,-4),2,3,5,5,
            Enumerable.Range(0,25).Select(i=>3d*(-3+(i%5)*2)-2*(-4+(i/5)*3)).ToArray());
        for(int i=0;i<20;i++)
        {
            var polygon=new WorldPolygon([new(-2.8+i*.1,-3.8+i*.1),new(4.7-i*.03,-2.2),new(2.1,6.9-i*.03),new(-2.5,3.2)]);
            double min=polygon.Vertices.Min(p=>3*p.X-2*p.Y),max=polygon.Vertices.Max(p=>3*p.X-2*p.Y);
            var terrain=surface with{Boundary=null,Patches=[],HeightField=plane};
            Agree("sampled-plane-exact-"+i,terrain,fields,profile with{Requirements=[new("plane","height","Analytic exact affine range","predicate:plane",min-1e-10,max+1e-10)]},polygon,true);
            Agree("sampled-plane-out-of-range-"+i,terrain,fields,profile with{Requirements=[new("plane","height","Analytic excluded affine maximum","predicate:plane",min-1,max-.01)]},polygon,false);
        }
        var evaluator=new WorldFootprintSuitability(new(new(surface),clamped),profile);
        foreach(long work in new[]{0L,1L})
        {
            bool refused=false;try{evaluator.Matches(area,work);}catch(ArgumentException error)when(error.Message.Contains("BUDGET",StringComparison.Ordinal)){refused=true;}
            if(!refused)throw new Exception("Predicate budget ignored");
        }
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{evaluator.Matches(area,token:cancellation.Token);throw new Exception("Predicate cancellation ignored");}catch(OperationCanceledException){}
        Console.WriteLine($"PASS: {cases} analytic/report-equivalent footprint predicates; concave/contained/thin/covered exclusions, interior clamps, correlated fields, sampled terrain/scalars, exact affine ranges, finite work and cancellation.");
    }
}
