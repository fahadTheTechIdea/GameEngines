using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldLayeredLandformTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Layered landforms: "+message);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(string kind)
    {
        var e=WorldLayeredIntegrationTests.Example();double height=kind=="valley"?-1:kind=="flat"?0:kind=="blocked"?2:1;
        var terrain=new WorldTerrainRecipe(1,"landforms","ground","Generated field program terrain","landform:terrain","meters",new(0,0),15,15,13,11,0,.01,.005,
            [new("ridge-valley","Protected source landform","landform:feature",WorldLandformMode.Add,[new(60,30),new(60,60)],20,height)]);
        WorldMetadataRecipe Apply(WorldMetadataRecipe r)=>r with{Surface=r.Surface! with{Patches=[],Terrain=terrain,HeightField=null},
            RemainingScopes=["Complete generated-heightfield program at unchanged counts, dimensions and budgets; broader terrain adaptation, expressive range and design acceptance remain open. Godot deferred."]};
        return(Apply(e.Recipe) with{PopulationSource=e.Recipe.PopulationSource! with{Recipe=Apply(e.Recipe.PopulationSource.Recipe)}},e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(string kind in new[]{"flat","ridge","valley","blocked"})
        {
            var e=Example(kind);string folder=Path.Combine(directory,kind);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(string kind in new[]{"flat","ridge","valley"})
        {
            var e=Example(kind);var clock=System.Diagnostics.Stopwatch.StartNew();var world=generator.Generate(e.Recipe,e.Catalog);
            WorldLayeredIntegrationTests.Qualify(world,e.Catalog);worlds.Add(world);
            var field=world.Surface!.HeightField!;var terrain=e.Recipe.Surface!.Terrain!;
            Check(field.Heights.Count==143&&world.Surface.Patches.Count==0,"full sampled terrain, no planar replacement");
            double highest=0;
            for(int y=0;y<field.Rows;y++)for(int x=0;x<field.Columns;x++)
            {
                double px=15*x,py=15*y,distance=Math.Sqrt(Math.Pow(px-60,2)+Math.Pow(Math.Max(30-py,Math.Max(0,py-60)),2));
                double t=Math.Clamp(1-distance/20,0,1),expected=.01*px+.005*py+terrain.Operations[0].Height*t*t*(3-2*t);
                Check(Math.Abs(field.Heights[y*field.Columns+x]-expected)<1e-10,"independent sampled landform formula");
                if(x<field.Columns-1&&y<field.Rows-1)
                {
                    int a=y*field.Columns+x,b=a+1,c=a+field.Columns,d=c+1;
                    foreach(var gradient in new[]{((field.Heights[b]-field.Heights[a])/15,(field.Heights[d]-field.Heights[b])/15),((field.Heights[d]-field.Heights[c])/15,(field.Heights[c]-field.Heights[a])/15)})
                        highest=Math.Max(highest,Math.Sqrt(gradient.Item1*gradient.Item1+gradient.Item2*gradient.Item2));
                }
            }
            Check(highest<=.1,"every terrain triangle obeys unchanged whole-footprint and full-width walking grade");
            Check(world.RegionPlan!.SuitableArea==26550&&world.TerrainDemandPlan!.Results[0].Count==18,"complete required area and independently derived landscape count");
            Console.WriteLine($"Qualified {kind}: {world.Nodes.Count} nodes, {world.AccessPlan!.Sites.Count} physical, 82 directed paths, maximum triangle slope={highest:R}; total {clock.Elapsed.TotalSeconds:F3}s.");
        }
        for(int i=1;i<worlds.Count;i++)
        {
            Check(Json(worlds[0].Nodes.Select(n=>n with{Position=null}))==Json(worlds[i].Nodes.Select(n=>n with{Position=null})),"landform changes do not discard or resize source content");
            Check(!worlds[0].Surface!.HeightField!.Heights.SequenceEqual(worlds[i].Surface!.HeightField!.Heights),"actual protected elevation changes");
        }
        var impossible=Example("blocked");
        try{generator.Generate(impossible.Recipe,impossible.Catalog);throw new Exception("Missing suitable-area refusal");}catch(WorldRegionIntentUnsatisfiedException){}
        Console.WriteLine("PASS: generated flat/ridge/valley terrain carries the complete layered source program at unchanged work/count/area/movement limits; independent height/gradient/source/clearance/connectivity/storage checks; insufficient suitable area refuses.");
    }
}
