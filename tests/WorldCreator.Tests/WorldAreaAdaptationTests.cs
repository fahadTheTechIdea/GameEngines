using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldAreaAdaptationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Area adaptation: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool positive=false,bool weighted=false)
    {
        var e=WorldProgramSpaceTests.Example(weighted,alternative:true);var layout=e.Recipe.LocalLayouts!.Layouts[0];
        layout=layout with{AreaPattern=layout.AreaPattern! with{AxisRotationDegrees=positive?-90:90}};
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[layout]},LayoutAdaptation=new(layout.Id,
            "Orient required spaces to fit complete source geometry","area-adapt:orientation",
            [new("orientation",MetadataLayoutDialKind.AreaRotation,90,positive?0:-1,positive?1:0)])},e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var (name,positive,weighted) in new[]{("negative",false,false),("positive",true,false),("weighted",false,true)})
        {
            var e=Example(positive,weighted);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }
    private static void Geometry()
    {
        int checks=0;
        foreach(int mask in new[]{0,1,2,3})foreach(double angle in new[]{0d,17d,73d})
        {
            const int width=12,height=9;var tiles=new HashSet<(int X,int Y)>();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                if(mask==0||mask==1&&!(x is >=4 and <=6&&y is >=3 and <=5)||mask==2&&x!=5||mask==3&&(x+y)%2==0)tiles.Add((x,y));
            double a=angle*Math.PI/180;WorldPoint P(double x,double y)=>new(-80+x*Math.Cos(a)-y*Math.Sin(a),-40+x*Math.Sin(a)+y*Math.Cos(a));
            var cells=new List<WorldPolygon>();var tileByCell=new Dictionary<WorldPolygon,(int X,int Y)>();
            foreach(var tile in tiles.OrderBy(t=>t.Y).ThenBy(t=>t.X))
            {
                var (x,y)=tile;foreach(var polygon in new[]{new WorldPolygon([P(x,y),P(x+1,y),P(x+1,y+1)]),new WorldPolygon([P(x,y),P(x+1,y+1),P(x,y+1)])})
                {cells.Add(polygon);tileByCell.Add(polygon,tile);}
            }
            var boundary=new WorldPolygon([new(-200,-200),new(200,-200),new(200,200),new(-200,200)]);
            var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(new(1,"land","meters",boundary,[],[new("ground","ground","Geometry oracle ground","area-adapt:geometry",boundary,0,new(0,0),0,0,0)])),new(1,"fields",[new("constant","Oracle constant","area-adapt:constant","relative",1,[])]));
            var profile=new WorldSuitabilityProfile("usable","Geometry oracle","area-adapt:geometry",["ground"],[],[]);
            var budget=new WorldSpatialWorkBudget(10000000,CancellationToken.None);
            var actual=WorldRegionGeometry.Components(environment,profile,cells,budget,CancellationToken.None);
            var remaining=tiles.ToHashSet();var expected=new List<HashSet<(int X,int Y)>>();
            while(remaining.Count>0)
            {
                var group=new HashSet<(int X,int Y)>();var queue=new Queue<(int X,int Y)>();queue.Enqueue(remaining.First());
                while(queue.TryDequeue(out var t)){if(!remaining.Remove(t))continue;group.Add(t);foreach(var n in new[]{(t.X-1,t.Y),(t.X+1,t.Y),(t.X,t.Y-1),(t.X,t.Y+1)})if(remaining.Contains(n))queue.Enqueue(n);}
                expected.Add(group);
            }
            Check(actual.Count==expected.Count,"independent tile adjacency component count, including point contacts");
            foreach(var component in actual)
            {
                var group=component.Cells.Select(c=>tileByCell[c]).ToHashSet();Check(expected.Count(e=>e.SetEquals(group))==1,"independent connected tile identities");
                Check(component.Cells.Count==2*group.Count&&Math.Abs(component.Area-group.Count)<1e-7,"every source triangle retained exactly");
                int perimeter=group.Sum(t=>new[]{(t.X-1,t.Y),(t.X+1,t.Y),(t.X,t.Y-1),(t.X,t.Y+1)}.Count(n=>!group.Contains(n)));
                Check(Math.Abs(component.BoundaryEdges.Sum(e=>e.From.DistanceTo(e.To))-perimeter)<1e-6,"independent boundary length includes holes, excludes all mesh seams");
            }
            Check(budget.Used<(long)cells.Count*3*(cells.Count*3-1)/2,"broad phase reduces ledger below exhaustive edge pairs");checks++;
        }
        Console.WriteLine($"Qualified {checks} independent triangulated geometry oracles across solid/holed/disconnected/point-contact masks and rotations.");
    }
    public static async Task RunAsync()
    {
        Geometry();
        var generator=new WorldMetadataGenerator();
        foreach(var (positive,weighted) in new[]{(false,false),(true,false),(false,true)})
        {
            var e=Example(positive,weighted);var source=e.Recipe.LocalLayouts!.Layouts[0];
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=null},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
            var world=generator.Generate(e.Recipe,e.Catalog);var trace=world.LayoutAdaptationPlan!;
            Check(trace.Attempts.Count==2&&!trace.Attempts[0].Accepted&&trace.Attempts[1].Accepted,"retain failed source before feasible orientation");
            Check(trace.Attempts.Select(a=>a.Steps["orientation"]).SequenceEqual(positive?[0,1]:[0,-1]),"nearest signed candidate order");
            var actual=world.LocalLayoutPlan!.Definition.Layouts[0];
            Check(actual.AreaPattern!.AxisRotationDegrees==0&&Json(actual with{AreaPattern=source.AreaPattern})==Json(source),"only declared area orientation changes");
            Check(world.Nodes.Count==7&&world.AccessPlan!.Sites.Count==2&&world.RoutePlan!.Routes.Count==1,"complete source members and route");
            foreach(var site in world.PlacementPlan!.Placement.Sites)
            {
                Check(Math.Abs(Math.Abs(site.Footprint.Footprint.SignedArea())-84)<1e-7,"exact source footprint");
                Check(Math.Abs(Math.Abs(site.Reservation!.Footprint.SignedArea())-192)<1e-7,"exact source reservation");
            }
            var direct=generator.Generate(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[actual]},LayoutAdaptation=null},e.Catalog);
            Check(Json(world.Nodes)==Json(direct.Nodes)&&Json(world.RoutePlan)==Json(direct.RoutePlan),"accepted orientation equals direct source generation");
            Check(world.LocalLayoutPlan.GeneratedAreas!.Single().AreaPlan.Pattern.DensityFieldId==source.AreaPattern!.DensityFieldId,"density provenance retained");
            WorldMetadataGenerator.ValidateDocument(world);
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=e.Recipe.LayoutAdaptation! with{MaximumAttempts=1}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
            Console.WriteLine($"Qualified signed={(positive?"positive":"negative")} weighted={weighted}: attempts={trace.Attempts.Count}, work={trace.Work}, nodes={world.Nodes.Count}.");
        }
        var input=Example();var policy=input.Recipe.LayoutAdaptation!;var dial=policy.Dials[0];var baseline=generator.Generate(input.Recipe,input.Catalog);
        long work=baseline.LayoutAdaptationPlan!.Work;
        Check(generator.Generate(input.Recipe with{LayoutAdaptation=policy with{MaximumWork=work}},input.Catalog).LayoutAdaptationPlan!.Work==work,"exact work boundary");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{MaximumWork=work-1}},input.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        void Bad(MetadataLayoutDial d,string code)=>Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{Dials=[d]}},input.Catalog),code);
        Bad(dial with{ProgramId="west-a"},"METADATA_LAYOUT_DIAL_TARGET_INVALID");Bad(dial with{CutId="cut"},"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        Bad(dial with{MaximumSteps=2},"METADATA_LAYOUT_DIAL_RANGE_INVALID");Bad(dial with{Step=300},"METADATA_LAYOUT_DIAL_RANGE_INVALID");Bad(dial with{Step=0},"METADATA_LAYOUT_DIAL_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{Dials=[dial with{Step=1}]}},input.Catalog),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
        var grid=WorldLayoutAdaptationTests.Example();Reject(()=>generator.Generate(grid.Recipe with{LayoutAdaptation=grid.Recipe.LayoutAdaptation! with{Dials=[dial]}},grid.Catalog),"METADATA_LAYOUT_DIAL_TARGET_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=policy with{Dials=[dial,dial with{Id="duplicate"}]}},input.Catalog),"METADATA_LAYOUT_DIAL_INVALID");
        var tampered=baseline with{LayoutAdaptationPlan=baseline.LayoutAdaptationPlan! with{Attempts=baseline.LayoutAdaptationPlan.Attempts.Select((a,i)=>i==0?a with{Accepted=true}:a).ToArray()}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
        using(var c=new CancellationTokenSource()){c.Cancel();try{generator.Generate(input.Recipe,input.Catalog,c.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;var changed=Example(positive:true);
        var accepted=session.Apply(new("orientation","Reverse allowed source orientation","area-adapt:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(Json(accepted.World)==Json(generator.Generate(changed.Recipe,changed.Catalog)),"clean/edit equality");
        try{session.Apply(new("no-fit","Retain complete required programs","area-adapt:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,
            changed.Recipe with{LayoutAdaptation=changed.Recipe.LayoutAdaptation! with{MaximumAttempts=1}},changed.Catalog));throw new Exception("Missing refusal");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"failed edit rollback");
        string folder=Path.Combine(Path.GetTempPath(),"area-adapt-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(folder);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"generation-session.json"));Check(Json(accepted)==Json(restored.Snapshot),"checkpoint replay");
        Console.WriteLine("PASS: area orientation adaptation retains exact required content/density, deterministic signed history, direct-generation equality, work/attempt/target/range/cancel/tamper checks, edits/rollback and checkpoint replay.");
    }
}
