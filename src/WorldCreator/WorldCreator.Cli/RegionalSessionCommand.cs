using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

namespace WorldCreator.Cli;

internal static class RegionalSessionCommand
{
    internal static readonly string[] Usage=["region-session start <definition.json> <new-output-dir> [--controls <controls.json>]",
        "region-session edit <generation-session.json> <edit.json> <new-output-dir>",
        "region-session start-derived <request.json> <new-output-dir> [--controls <controls.json>]",
        "region-session edit-derived <generation-session.json> <source-edit.json> <new-output-dir>",
        "region-session start-designed <design.json> <new-output-dir>",
        "region-session edit-designed <generation-session.json> <design-edit.json> <new-output-dir>",
        "region-session start-blueprint-design <blueprint-design.json> <new-output-dir>",
        "region-session edit-blueprint-design <generation-session.json> <blueprint-design-edit.json> <new-output-dir>"];
    private static void Validate(string[] args)
    {
        if(args.Length==4&&args[1] is "start" or "start-derived" or "start-designed" or "start-blueprint-design"||args.Length==5&&args[1] is "edit" or "edit-derived" or "edit-designed" or "edit-blueprint-design"||
            args.Length==6&&args[1] is "start" or "start-derived"&&args[4]=="--controls")return;
        throw new ArgumentException("Expected "+string.Join(" or ",Usage));
    }
    internal static async Task<bool> RequiresRegistryAsync(string[] args,CancellationToken token)
    {
        Validate(args);var definitions=new List<MetadataRegionalWorldDefinition>();var designs=new List<MetadataRegionalDesignRequest>();MetadataRegionalOwnershipRequest? source=null;
        if(args[1]=="start-blueprint-design")designs.Add(MetadataRegionalDesignGenerator.ExpandBlueprintDesign(
            await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesign>(args[2],token),token));
        else if(args[1]=="start-designed")designs.Add(await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>(args[2],token));
        else if(args[1]=="start-derived")source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>(args[2],token);
        else if(args[1]=="start")definitions.Add(await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(args[2],token));
        else
        {
            var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(args[2],token);
            definitions.Add(checkpoint.InitialDefinition);definitions.AddRange(checkpoint.Edits.Select(e=>e.Request.Definition));
            if(checkpoint.InitialDesignSource is {} initial)designs.Add(initial);
            designs.AddRange(checkpoint.Edits.Where(e=>e.Request.DesignSource is not null).Select(e=>e.Request.DesignSource!));
            if(args[1]=="edit-blueprint-design")designs.Add(MetadataRegionalDesignGenerator.ExpandBlueprintDesign(
                (await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesignEditRequest>(args[3],token)).Source,token));
            else if(args[1]=="edit-designed")designs.Add((await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignEditRequest>(args[3],token)).Source);
            else if(args[1]=="edit-derived")source=(await WorldMetadataGenerator.ReadAsync<MetadataRegionalSourceEditRequest>(args[3],token)).Source;
            else definitions.Add((await WorldMetadataGenerator.ReadAsync<MetadataRegionalEditRequest>(args[3],token)).Definition);
        }
        return designs.Any(RegionalWorldCommand.RequiresDesignRegistry)||source is not null&&(source.Regions is null||source.Regions.Any(r=>r?.Recipe is null||r.Catalog is null||MetadataSessionCommand.RequiresRegistry(r.Recipe,r.Catalog)))||
            definitions.Any(d=>d?.Regions is null||d.Regions.Any(r=>r?.Recipe is null||r.Catalog is null||MetadataSessionCommand.RequiresRegistry(r.Recipe,r.Catalog)));
    }
    internal static async Task<string> RunAsync(string[] args,ISettlementCatalogRegistry? registry,MetadataSessionReporting reporting,CancellationToken token)
    {
        Validate(args);var generator=new WorldMetadataGenerator(registry);MetadataRegionalGenerationSession session;
        if(args[1] is "start" or "start-derived" or "start-designed" or "start-blueprint-design")
        {
            var controls=args.Length==6?await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionControls>(args[5],token):null;
            if(args[1]=="start-blueprint-design")session=await MetadataRegionalGenerationSession.StartDesignedAsync(generator,
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesign>(args[2],token),reporting,token);
            else if(args[1]=="start-designed")session=await MetadataRegionalGenerationSession.StartDesignedAsync(generator,
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>(args[2],token),reporting,token);
            else if(args[1]=="start-derived")session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>(args[2],token),reporting,token,controls);
            else session=await MetadataRegionalGenerationSession.StartAsync(generator,
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(args[2],token),reporting,token,controls);
            await session.PublishAsync(args[3],token);
        }
        else
        {
            session=await MetadataRegionalGenerationSession.RestoreAsync(generator,args[2],token,reporting);
            if(args[1]=="edit-blueprint-design")await session.ApplyDesignAndPublishAsync(
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesignEditRequest>(args[3],token),args[4],token);
            else if(args[1]=="edit-designed")await session.ApplyDesignAndPublishAsync(
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignEditRequest>(args[3],token),args[4],token);
            else if(args[1]=="edit-derived")await session.ApplyDerivedAndPublishAsync(
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalSourceEditRequest>(args[3],token),args[4],token);
            else await session.ApplyAndPublishAsync(await WorldMetadataGenerator.ReadAsync<MetadataRegionalEditRequest>(args[3],token),args[4],token);
        }
        var snapshot=session.Snapshot;
        string design=snapshot.Report.DesignAttempts is {} attempts?$" Authored design: {attempts.Count} attempts, {attempts.Count(a=>!a.Accepted)} rejected.":string.Empty;
        return $"Accepted {snapshot.Report.EditId}: {snapshot.World.Regions.Count} regions, {snapshot.World.Regions.Sum(r=>r.World.Nodes.Count)} nodes; {snapshot.Report.Regions.Count(r=>r.Reused)} complete region packets reused. All declared regional paths requalified. Publication replays source proof; generation-session.json stores source history, never executable caches."+design;
    }
}
