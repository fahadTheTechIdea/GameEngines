using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainAdaptationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("TERRAIN_ADAPTATION: "+reason);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output already exists");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        string before=Json(original);var surface=original.Template.Surface;var terrain=surface.Terrain!;
        var ground=terrain with{BaseElevation=3,GradientX=0,GradientY=0,Operations=[],Detail=null};
        var network=original.Template.NetworkRules!.Select(r=>r with{Options=r.Options with{
            Profile=r.Options.Profile with{KindCosts=new Dictionary<string,double>{{"ground",1}}}}}).ToArray();
        var template=original.Template with{Surface=surface with{Patches=[],Terrain=ground},NetworkRules=network};
        var initial=original with{Template=template,SeedPlan=original.SeedPlan! with{TerrainDetail=false}};
        var wetland=new WorldTerrainPatch("wetland","bog","Terrain forbidden to the declared travel profile","adaptation:wetland",
            new WorldPolygon([new(22,30),new(38,30),new(38,48),new(22,48)]),10,ground.Origin,3,0,0);
        var changed=initial with{Template=template with{Surface=template.Surface with{Patches=[wetland]}}};
        var engine=new MetadataRegionalDesignGenerator();Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"initial-request.json"),Json(initial));
        await File.WriteAllTextAsync(Path.Combine(output,"wetland-request.json"),Json(changed));
        var baseline=engine.Generate(initial);var adapted=engine.Generate(changed);
        foreach(var result in new[]{baseline,adapted})
        {
            var world=result.World;
            Check(world.Regions.Count==4&&world.Regions.Sum(r=>r.World.Nodes.Count)==20&&world.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0)==12,
                "all physical programs and members retained");
            Check(world.Connections.Count==3&&world.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),
                "three required two-way paths retained");
        }
        Check(Json(baseline.World.Connections.Select(c=>c.Forward.Path))!=Json(adapted.World.Connections.Select(c=>c.Forward.Path)),
            "terrain change alters the generated forward route geometry");
        Check(before==Json(original),"source is not mutated");
        await engine.PublishAsync(baseline,Path.Combine(output,"initial"));
        await engine.PublishAsync(adapted,Path.Combine(output,"wetland"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"initial")))==Json(baseline),"initial source and world replay");
        Check(Json(await engine.ReadAsync(Path.Combine(output,"wetland")))==Json(adapted),"changed source and world replay");
        Console.WriteLine("PASS: one forbidden terrain patch changes the generated network while retaining all four complete physical programs and two-way routes; both worlds replay.");
    }
}
