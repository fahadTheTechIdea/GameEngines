using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalSeedCorpusTests
{
    private sealed class InvalidWorldException(string message):Exception(message);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions())));
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        const string sourcePath="examples/metadata/regional-design/world-seed-a-request.json";
        async Task<string> InputHash()=>Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath)));
        string sourceHash=await InputHash();
        var source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>(sourcePath);
        if(sourceHash!=await InputHash())throw new IOException("SEED_CORPUS_SOURCE_CHANGED_DURING_READ");
        if(source.SeedPlan is null)throw new InvalidOperationException("SEED_CORPUS_REQUIRES_WORLD_SEED");
        string original=Json(source);var seeds=new[]{"world-a","world-b","world-c","world-d","world-e","world-f"};
        var engine=new MetadataRegionalDesignGenerator();var observations=new List<object>();
        var uniqueTerrain=new HashSet<string>();var uniqueRegions=new HashSet<string>();var uniqueRoutes=new HashSet<string>();var uniquePrograms=new HashSet<string>();
        int success=0,invalid=0;var known=new HashSet<string>();Directory.CreateDirectory(output);MetadataRegionalDesignResult? replay=null;
        foreach(string seed in seeds)
        {
            var request=source with{SeedPlan=source.SeedPlan with{Seed=seed}};var timer=Stopwatch.StartNew();
            try
            {
                var result=engine.Generate(request);timer.Stop();var world=result.World;
                int nodes=world.Regions.Sum(r=>r.World.Nodes.Count),sites=world.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0);
                int directed=world.Connections.Sum(c=>c.Reverse?.Path is null?1:2);
                if(world.Regions.Count!=4||nodes!=20||sites!=12||world.Connections.Count!=3||directed!=6||
                    world.Connections.Any(c=>c.Forward.Path is null||c.Reverse?.Path is null))
                {
                    await File.WriteAllTextAsync(Path.Combine(output,seed+".invalid.json"),Json(result));
                    throw new InvalidWorldException("SEED_CORPUS_REQUIRED_CONTENT_MISSING:"+seed);
                }
                string terrain=Hash(new WorldTerrainGenerator().Generate(world.Definition.Surface.Terrain!).Heights);
                string regions=Hash(world.Definition.Regions.Select(r=>new{r.Id,r.Ownership}).ToArray());
                string routes=Hash(world.Connections.Select(c=>new{c.Definition.Id,Forward=c.Forward.Path,Reverse=c.Reverse!.Path}).ToArray());
                string programs=Hash(result.Receipt.Attempts[^1].Selections);
                uniqueTerrain.Add(terrain);uniqueRegions.Add(regions);uniqueRoutes.Add(routes);uniquePrograms.Add(programs);
                await File.WriteAllTextAsync(Path.Combine(output,seed+".json"),Json(result));
                observations.Add(new{Seed=seed,Status="Generated",Milliseconds=timer.ElapsedMilliseconds,Nodes=nodes,PhysicalSites=sites,
                    RegionalConnections=world.Connections.Count,DirectedPaths=directed,OwnershipWork=world.Definition.OwnershipPlan!.Work,
                    WorldWork=world.Work,DesignAttempts=result.Receipt.Attempts.Count,TerrainSha256=terrain,RegionsSha256=regions,RoutesSha256=routes,ProgramsSha256=programs});
                success++;known.Add(seed);replay??=result;Console.WriteLine($"{seed}: generated {nodes} nodes and {directed} directed paths in {timer.ElapsedMilliseconds} ms.");
            }
            catch(ArgumentException error)
            {
                timer.Stop();var attempts=error.Data["RegionalDesignAttempts"] as MetadataRegionalDesignAttempt[];
                observations.Add(new{Seed=seed,Status="Refused",Milliseconds=timer.ElapsedMilliseconds,Error=error.Message,Attempts=attempts});
                Console.WriteLine($"{seed}: refused: {error.Message} ({timer.ElapsedMilliseconds} ms).");
            }
            catch(InvalidWorldException error)
            {
                timer.Stop();invalid++;observations.Add(new{Seed=seed,Status="InvalidOutput",Milliseconds=timer.ElapsedMilliseconds,Error=error.Message});
                Console.WriteLine($"{seed}: invalid output: {error.Message} ({timer.ElapsedMilliseconds} ms).");
            }
        }
        if(Json(source)!=original)throw new InvalidOperationException("SEED_CORPUS_SOURCE_MUTATED");
        if(sourceHash!=await InputHash())throw new IOException("SEED_CORPUS_SOURCE_CHANGED_DURING_STUDY");
        bool replayed=false;if(replay is not null){await engine.PublishAsync(replay,Path.Combine(output,"replay"));
            replayed=Json(await engine.ReadAsync(Path.Combine(output,"replay")))==Json(replay);if(!replayed)throw new InvalidOperationException("SEED_CORPUS_REPLAY_MISMATCH");}
        var report=new{Version=1,SourceSha256=sourceHash,
            CoreModuleVersionId=typeof(WorldMetadataGenerator).Module.ModuleVersionId,Runtime=RuntimeInformation.FrameworkDescription,
            OS=RuntimeInformation.OSDescription,ProcessorCount=Environment.ProcessorCount,Inputs=seeds,Generated=success,Invalid=invalid,Refused=seeds.Length-success-invalid,
            DistinctTerrain=uniqueTerrain.Count,DistinctRegionalOwnership=uniqueRegions.Count,DistinctRoutes=uniqueRoutes.Count,DistinctProgramSelections=uniquePrograms.Count,
            OnePublishedWorldReplayed=replayed,Observations=observations};
        await File.WriteAllTextAsync(Path.Combine(output,"study.json"),JsonSerializer.Serialize(report,WorldJson.CreateOptions(true)));
        if(invalid>0||!known.Contains("world-a")||!known.Contains("world-b"))throw new InvalidOperationException("SEED_CORPUS_BASELINE_OR_OUTPUT_REGRESSION");
        Console.WriteLine($"PASS: recorded {success} generated, {seeds.Length-success-invalid} refused and {invalid} invalid worlds; {uniqueRegions.Count} ownership layouts and {uniqueRoutes.Count} route layouts; one complete world replayed.");
    }
}
