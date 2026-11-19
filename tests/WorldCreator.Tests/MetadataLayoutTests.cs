using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class MetadataLayoutTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(string name)
    {
        var (recipe,catalog)=WorldMetadataTests.Example(name);
        if(name=="dungeon")
        {
            recipe=recipe with{Space=new(["x","y","z"],"meters"),Origin=[0,0,0],RequireAllPositions=true};
            catalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id switch{
                "root"=>a with{Parts=a.Parts.Select(p=>p with{Layout=MetadataLineLayout.Parameters([0,0,0],[0,0,-8])}).ToArray()},
                "floor"=>a with{Parts=a.Parts.Select(p=>p with{Layout=MetadataPathLayout.Parameters([[0,0,0],[36,0,0],[36,24,0]],12)}).ToArray()},
                _=>a}).ToArray()};
        }
        else if(name=="platformer")catalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="level"?
            a with{Parts=a.Parts.Select(p=>p with{Layout=MetadataPathLayout.Parameters([[0,0],[30,0],[30,30],[90,30]],40)}).ToArray()}:a).ToArray()};
        else if(name=="space")catalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="system"?
            a with{Parts=a.Parts.Select(p=>p with{Layout=MetadataArcLayout.Parameters([0,0,0],50,0,2)}).ToArray()}:a).ToArray()};
        else throw new ArgumentException("Unknown test fixture");
        return(recipe,catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example destination exists");
        Directory.CreateDirectory(directory);
        foreach(var name in new[]{"dungeon","platformer","space"})
        {
            var fixture=Example(name);
            await File.WriteAllTextAsync(Path.Combine(directory,name+".recipe.json"),Json(fixture.Recipe,true));
            await File.WriteAllTextAsync(Path.Combine(directory,name+".catalog.json"),Json(fixture.Catalog,true));
        }
    }
    public static void Run()
    {
        var generator=new WorldMetadataGenerator();
        foreach(string name in new[]{"dungeon","platformer","space"})
        {
            var fixture=Example(name);var world=generator.Generate(fixture.Recipe,fixture.Catalog);
            Check(Json(world)==Json(generator.Generate(fixture.Recipe,fixture.Catalog)),"deterministic nested "+name);
            Check(world.Nodes.All(n=>n.Position is not null),"actual engine positions "+name);
            Check(world.Nodes.All(n=>n.Kind is not ("block" or "parcel" or "district")),"no mandatory city concepts "+name);
            var baseline=generator.Generate(WorldMetadataTests.Example(name).Recipe,WorldMetadataTests.Example(name).Catalog);
            Check(Json(world.Connections)==Json(baseline.Connections),"spatial grammar preserves declared topology "+name);
            if(name=="dungeon")
            {
                Near(world.Nodes.Single(n=>n.Id=="dungeon/floors-1/rooms-5").Position!,[36,24,-8]);
                Check(world.Connections.Any(c=>c.Kind=="stairs"),"deep room connection retained");
            }
            if(name=="platformer")Near(world.Nodes.Single(n=>n.Id=="platformer/levels-2/sections-2").Position!,[450,30]);
            if(name=="space")
            {
                var parent=world.Nodes.Single(n=>n.Id=="space/systems-0").Position!;
                foreach(var child in world.Nodes.Where(n=>n.ParentId=="space/systems-0"))
                {
                    var point=child.Position!;Check(Math.Abs(Math.Sqrt(Math.Pow(point[0]-parent[0],2)+Math.Pow(point[2]-parent[2],2))-50)<1e-7,"exact radius in selected axes");
                    Check(point[1]==parent[1],"other axis retained");
                }
            }
        }
        var path=new MetadataPathLayout();
        var settings=MetadataPathLayout.Parameters([[0,0],[3,0],[3,4]],1);
        var points=path.Plan(settings.Parameters,2,8);Near(points[3],[3,0]);Near(points[7],[3,4]);
        Reject(()=>path.Plan(settings.Parameters,2,9),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        Reject(()=>path.Plan(MetadataPathLayout.Parameters([[0,0],[0,0]],1).Parameters,2,1),"WORLD_PATTERN_SEGMENT_INVALID");
        Reject(()=>path.Plan(MetadataPathLayout.Parameters([[0,0],[1,0]],0).Parameters,2,1),"METADATA_PATH_PARAMETERS_INVALID");
        var arc=new MetadataArcLayout();var open=MetadataArcLayout.Parameters([0,0],2,sweepDegrees:180);
        Near(arc.Plan(open.Parameters,2,3)[2],[-2,0]);
        var ring=arc.Plan(MetadataArcLayout.Parameters([0,0],2).Parameters,2,4);
        Check(ring.Select(p=>Json(p)).Distinct().Count()==4,"closed ring does not duplicate its seam");
        var clockwise=MetadataArcLayout.Parameters([0,0],2,startDegrees:90,sweepDegrees:-180);
        var clockwisePoints=arc.Plan(clockwise.Parameters,2,3);
        Near(clockwisePoints[0],[0,2]);Near(clockwisePoints[1],[2,0]);Near(clockwisePoints[2],[0,-2]);
        var clockwiseRing=arc.Plan(MetadataArcLayout.Parameters([0,0],2,sweepDegrees:-360).Parameters,2,4);
        Near(clockwiseRing[1],[0,-2]);
        Check(clockwiseRing.Select(p=>Json(p)).Distinct().Count()==4,"clockwise ring does not duplicate its seam");
        Reject(()=>arc.Plan(MetadataArcLayout.Parameters([0,0],2,sweepDegrees:0).Parameters,2,2),"METADATA_ARC_PARAMETERS_INVALID");
        Reject(()=>arc.Plan(MetadataArcLayout.Parameters([0,0],2,sweepDegrees:-361).Parameters,2,2),"METADATA_ARC_PARAMETERS_INVALID");
        Reject(()=>arc.Plan(MetadataArcLayout.Parameters([0,0],2,sweepDegrees:361).Parameters,2,2),"METADATA_ARC_PARAMETERS_INVALID");
        using(var invalid=JsonDocument.Parse("{\"Center\":[0,0],\"Radius\":2,\"SweepDegrees\":1e309}"))
            Reject(()=>arc.Plan(invalid.RootElement,2,2),"METADATA_ARC_PARAMETERS_INVALID");
        var space=Example("space");
        var reverseCatalog=space.Catalog with{Archetypes=space.Catalog.Archetypes.Select(a=>a.Id=="system"?
            a with{Parts=a.Parts.Select(p=>p with{Layout=MetadataArcLayout.Parameters([0,0,0],50,0,2,sweepDegrees:-360)}).ToArray()}:a).ToArray()};
        var reverseWorld=generator.Generate(space.Recipe,reverseCatalog);
        var originalWorld=generator.Generate(space.Recipe,space.Catalog);
        var reverseParent=reverseWorld.Nodes.Single(n=>n.Id=="space/systems-0").Position!;
        var children=reverseWorld.Nodes.Where(n=>n.ParentId=="space/systems-0").ToArray();
        var originalChildren=originalWorld.Nodes.Where(n=>n.ParentId=="space/systems-0").ToArray();
        Check(children.Length>=3&&children.Length==originalChildren.Length,"reverse ring preserves hierarchy");
        Check(children[1].Position![2]<reverseParent[2]&&originalChildren[1].Position![2]>reverseParent[2],"direction changes ordered child positions");
        Check(Json(reverseWorld)==Json(generator.Generate(space.Recipe,reverseCatalog)),"clockwise nested generation deterministic");
        Check(Json(reverseWorld.Connections)==Json(originalWorld.Connections),"clockwise generation preserves connections");
        Reject(()=>arc.Plan(MetadataArcLayout.Parameters([0,0],2,0,0).Parameters,2,1),"METADATA_ARC_PARAMETERS_INVALID");
        Reject(()=>arc.Plan(open.Parameters,3,1),"METADATA_ARC_PARAMETERS_INVALID");
        var fixture2=WorldMetadataTests.Example("platformer");
        WorldMetadataCatalog Custom(string id)=>fixture2.Catalog with{Archetypes=fixture2.Catalog.Archetypes.Select(a=>a with{
            Parts=a.Parts.Select(p=>p with{Layout=new(id,JsonSerializer.SerializeToElement(new{}))}).ToArray()}).ToArray()};
        var custom=new WorldMetadataGenerator(layouts:[new AuthoredTestLayout()]).Generate(fixture2.Recipe,Custom("test-route"));
        Near(custom.Nodes.Single(n=>n.Id=="platformer/levels-2/sections-2").Position!,[4,8]);
        Reject(()=>generator.Generate(fixture2.Recipe,Custom("missing")),"METADATA_LAYOUT_UNKNOWN");
        Reject(()=>new WorldMetadataGenerator(layouts:[new AuthoredTestLayout(),new AuthoredTestLayout()]),"METADATA_LAYOUT_REGISTRATION_INVALID");
        Reject(()=>new WorldMetadataGenerator(layouts:[new InvalidTestLayout()]).Generate(fixture2.Recipe,Custom("invalid")),"METADATA_LAYOUT_RESULT_INVALID");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();try{path.Plan(settings.Parameters,2,3,cancel.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}
        }
        try
        {
            JsonSerializer.Deserialize<MetadataLayout>("{\"Origin\":[0,0],\"Step\":[1,0]}");
            throw new Exception("Removed line-only layout accepted");
        }
        catch(JsonException){}
        Console.WriteLine("PASS: composable line/path/arc strategies, nested non-city 2D/3D programs, exact bends/radii/axes/parent transforms, unchanged topology, custom registration, explicit refusal and cancellation. No terrain/physics/gameplay claim.");
    }
    private sealed class AuthoredTestLayout:IMetadataLayout
    {
        public string Id=>"test-route";
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement p,int dimensions,int count,CancellationToken token=default)=>
            Enumerable.Range(0,count).Select(i=>(IReadOnlyList<double>)new double[]{i,i*i}).ToArray();
    }
    private sealed class InvalidTestLayout:IMetadataLayout
    {
        public string Id=>"invalid";
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement p,int dimensions,int count,CancellationToken token=default)=>count==0?[]:[new[]{double.NaN}];
    }
    private static string Json<T>(T value,bool pretty=false)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(pretty));
    private static void Near(IReadOnlyList<double> actual,IReadOnlyList<double> expected)=>Check(actual.Count==expected.Count&&actual.Zip(expected).All(p=>Math.Abs(p.First-p.Second)<1e-7),"exact coordinates");
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Spatial metadata: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
