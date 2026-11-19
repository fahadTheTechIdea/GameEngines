using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
using WorldCreator.Core;
using System.Text.Json;

internal static partial class WorldRegionalOwnershipTests
{
    internal static async Task UnitsAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var boundary=Box(0,0,.5,.5);
        var surface=new WorldSurfaceDefinition(1,"small-domain","units",boundary,[],[new("ground","ground","Small-unit domain","units:ground",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentDefinition(1,"fields",[new("height","Constant height","units:height","units",0,[])]);
        var profile=new WorldSuitabilityProfile("ground","Declared suitable ground","units:profile",["ground"],[],[]);
        var catalog=new WorldMetadataCatalog(1,"small-catalog",[new("location","location","Required location",new Dictionary<string,JsonElement>(),[],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"location-program","framework-reference",catalog.Id,"location","region",MetadataCoverage.PartialScope,
            ["Technical small-unit ownership control; complete game design remains separate."],new Dictionary<string,int>(),new(["x","y"],"units"),RequireAllPositions:true,Environment:environment);
        var request=new MetadataRegionalOwnershipRequest(1,"small-world","No implicit meter-scale minimum","units:world",surface,
            [new("a","First location","units:a",1,.05,0,profile,null,recipe,catalog),new("b","Second location","units:b",1,.05,0,profile,null,recipe,catalog)],[],new(Interior:new(.001)));
        var definition=new MetadataRegionalOwnershipPlanner().Plan(request);var world=new MetadataRegionalWorldGenerator().Generate(definition);
        Near(definition.Regions.Sum(r=>Math.Abs(r.Ownership.SignedArea())),.25,"small-unit domain retained");
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==2&&definition.Regions.All(r=>Math.Abs(r.Ownership.SignedArea())>=.05),"every small-unit program retained");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await new MetadataRegionalWorldGenerator().PublishAsync(world,Path.Combine(output,"world"));
        Console.WriteLine("PASS: small-unit ownership retains the complete0.25-unit² domain and both declared0.05-unit² programs without an implicit1-unit² threshold.");
    }
}
