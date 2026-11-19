using WorldCreator.Core.Metadata;

internal static partial class WorldRegionalSessionTests
{
    private sealed class ObservedResolver(WorldMetadataGenerator generator):IMetadataGenerationDependencyResolver
    {
        internal int Calls;
        public WorldMetadataGenerator Resolve(IReadOnlyList<MetadataGenerationDependency> dependencies){Calls++;return generator;}
    }
    internal static async Task ValidationAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var session=new MetadataRegionalGenerationSession(generator,Logical());
        await session.PublishAsync(Path.Combine(output,"seed"));
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(Path.Combine(output,"seed/generation-session.json"));
        foreach(string kind in new[]{"region","recipe","catalog"})
        {
            var regions=checkpoint.InitialDefinition.Regions.ToArray();
            regions[0]=kind switch{"region"=>null!,"recipe"=>regions[0] with{Recipe=null!},_=>regions[0] with{Catalog=null!}};
            var corrupt=checkpoint with{InitialDefinition=checkpoint.InitialDefinition with{Regions=regions}};
            string file=Path.Combine(output,"null-"+kind+".json");await File.WriteAllTextAsync(file,Json(corrupt));
            var observed=new ObservedResolver(generator);
            var error=await Throws<ArgumentException>(()=>MetadataRegionalGenerationSession.RestoreAsync(generator,file,resolver:observed));
            Check(error.Message=="METADATA_REGIONAL_CHECKPOINT_DEPENDENCIES_REQUIRED"&&observed.Calls==0,"invalid source rows refuse before installed historical resolver is called");
        }
        Check(Json((await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"seed/generation-session.json"))).Snapshot)==Json(session.Snapshot),"rejected malformed reads preserve the complete accepted source/history");
        Console.WriteLine("PASS: null regional source/recipe/catalog checkpoints refuse with typed validation before historical resolver invocation; accepted packet remains fully replayable.");
    }
}
