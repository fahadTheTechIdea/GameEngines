using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

namespace WorldCreator.Cli;

/// <summary>Thin command adapter; generation, replay and publication belong to Core.</summary>
internal static class RegionalWorldCommand
{
    internal const string Operation="world.regions";
    internal static bool Handles(string command)=>command is "generate-region" or "generate-regional-world" or "assemble-regional-world" or "generate-derived-regional-world" or "generate-regional-blueprint" or "generate-regional-seeded-blueprint" or "generate-regional-blueprint-design" or "generate-regional-design" or "verify-regional-design";
    internal static readonly string[] Usage=["generate-region <definition.json> <region-id> <new-output-dir>",
        "generate-regional-world <definition.json> <new-output-dir>","assemble-regional-world <definition.json> <region-directory-list.json> <new-output-dir>",
        "generate-derived-regional-world <request.json> <new-output-dir>","generate-regional-blueprint <blueprint.json> <new-output-dir>","generate-regional-seeded-blueprint <blueprint.json> <seed-plan.json> <new-output-dir>","generate-regional-blueprint-design <design.json> <new-output-dir>","generate-regional-design <request.json> <new-output-dir>","verify-regional-design <output-dir>"];
    private static void Validate(string[] args)
    {
        if(!Handles(args[0])||args.Length!=(args[0]=="verify-regional-design"?2:args[0] is "generate-regional-world" or "generate-derived-regional-world" or "generate-regional-blueprint" or "generate-regional-blueprint-design" or "generate-regional-design"?3:4))throw new ArgumentException("Expected "+string.Join(" or ",Usage));
    }
    internal static async Task<bool> RequiresRegistryAsync(string[] args,CancellationToken token)
    {
        Validate(args);
        if(args[0] is "generate-regional-design" or "generate-regional-blueprint-design" or "verify-regional-design")
        {
            var source=args[0]=="generate-regional-design"?await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>(args[1],token):
                args[0]=="generate-regional-blueprint-design"?MetadataRegionalDesignGenerator.ExpandBlueprintDesign(await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesign>(args[1],token),token):
                (await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignReceipt>(Path.Combine(args[1],"world-design.json"),token)).Source;
            return RequiresDesignRegistry(source);
        }
        if(args[0] is "generate-derived-regional-world" or "generate-regional-blueprint" or "generate-regional-seeded-blueprint")
        {
            var source=args[0] is "generate-regional-blueprint" or "generate-regional-seeded-blueprint"?MetadataRegionalOwnershipPlanner.ExpandBlueprint(await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprint>(args[1],token),token):
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>(args[1],token);
            if(source.Regions is null)throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_REQUEST_INVALID");
            return source.Regions.Any(r=>r is null||r.Recipe is null||r.Catalog is null||MetadataSessionCommand.RequiresRegistry(r.Recipe,r.Catalog));
        }
        var definition=await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(args[1],token);
        if(definition.Regions is null)throw new ArgumentException("METADATA_REGIONAL_DEFINITION_INVALID");
        return definition.Regions.Any(r=>r is null||r.Recipe is null||r.Catalog is null||MetadataSessionCommand.RequiresRegistry(r.Recipe,r.Catalog));
    }
    internal static bool RequiresDesignRegistry(MetadataRegionalDesignRequest source)
    {
        if(source?.Template?.Regions is null||source.Slots is null)throw new ArgumentException("METADATA_REGIONAL_DESIGN_INVALID");
        return source.Template.Regions.Any(r=>r?.Recipe is null||r.Catalog is null||MetadataSessionCommand.RequiresRegistry(r.Recipe,r.Catalog))||
            source.Slots.Any(s=>s?.Options is null||s.Options.Any(o=>o?.Recipe is null||o.Catalog is null||MetadataSessionCommand.RequiresRegistry(o.Recipe,o.Catalog)));
    }
    internal static async Task<string> RunAsync(string[] args,ISettlementCatalogRegistry? registry,CancellationToken token)
    {
        Validate(args);var generator=new WorldMetadataGenerator(registry);
        if(args[0]=="generate-regional-seeded-blueprint")
        {
            var blueprint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprint>(args[1],token);
            var seed=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignSeedPlan>(args[2],token);
            var request=MetadataRegionalDesignGenerator.FromSeededBlueprint(blueprint,seed,token);
            var design=new MetadataRegionalDesignGenerator(generator);var result=design.Generate(request,token);
            await design.PublishAsync(result,args[3],token);
            return $"Generated seeded regional world: {result.World.Regions.Count} regions, {result.World.Regions.Sum(r=>r.World.Nodes.Count)} nodes and {result.World.Connections.Count} physical connections.";
        }
        if(args[0] is "generate-regional-design" or "generate-regional-blueprint-design" or "verify-regional-design")
        {
            var design=new MetadataRegionalDesignGenerator(generator);
            if(args[0]=="verify-regional-design")
            {
                var verified=await design.ReadAsync(args[1],token);
                return $"Verified regional design: {verified.Receipt.Attempts.Count} attempts, {verified.World.Regions.Count} regions and {verified.World.Connections.Count} physical connections.";
            }
            var result=args[0]=="generate-regional-blueprint-design"?
                design.GenerateBlueprint(await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesign>(args[1],token),token):
                design.Generate(await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>(args[1],token),token);
            await design.PublishAsync(result,args[2],token);
            return $"Generated regional design: {result.Receipt.Attempts.Count} attempts, {result.World.Regions.Count} regions, {result.World.Regions.Sum(r=>r.World.Nodes.Count)} nodes and {result.World.Connections.Count} physical connections.";
        }
        var definition=args[0] is "generate-derived-regional-world" or "generate-regional-blueprint"?
            new MetadataRegionalOwnershipPlanner(generator).Plan(args[0]=="generate-regional-blueprint"?
                MetadataRegionalOwnershipPlanner.ExpandBlueprint(await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprint>(args[1],token),token):
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>(args[1],token),token):
            await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(args[1],token);
        var engine=new MetadataRegionalWorldGenerator(generator);
        if(args[0]=="generate-region")
        {
            var region=engine.GenerateRegion(definition,args[2],token);await engine.PublishRegionAsync(definition,region,args[3],token);
            return $"Generated region {region.RegionId}: {region.World.Nodes.Count} complete metadata nodes; other region content was not generated.";
        }
        MetadataRegionalWorldDocument world;
        if(args[0] is "generate-regional-world" or "generate-derived-regional-world" or "generate-regional-blueprint")world=engine.Generate(definition,token);
        else
        {
            var directories=await WorldMetadataGenerator.ReadAsync<string[]>(args[2],token);
            if(directories.Length is <2 or >16||directories.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("METADATA_REGIONAL_DIRECTORY_LIST_INVALID");
            var packets=new List<MetadataGeneratedRegion>();
            foreach(var directory in directories)packets.Add(await engine.ReadRegionAsync(directory,definition,token));
            world=engine.Assemble(definition,packets,token);
        }
        await engine.PublishAsync(world,args[^1],token);
        return $"Generated shared-coordinate world: {world.Regions.Count} regions, {world.Regions.Sum(r=>r.World.Nodes.Count)} nodes and {world.Connections.Count} physical cross-region connections.";
    }
}
