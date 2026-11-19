using System.Net;
using System.Text;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionReceipt(int Version,string RegionId,string ContextSha256,string ImplementationId,string WorldSha256,
    IReadOnlyList<MetadataGenerationDependency> Dependencies);
internal sealed record MetadataRegionPublication(MetadataRegionalWorldDefinition Definition,MetadataRegionReceipt Receipt);

public sealed partial class MetadataRegionalWorldGenerator
{
    public void ValidateDocument(MetadataRegionalWorldDocument document,CancellationToken token=default)
        =>_=ReplayDocument(document,token);
    private MetadataRegionalWorldDocument ReplayDocument(MetadataRegionalWorldDocument document,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(document is null||document.Version!=1||document.Definition is null||document.DefinitionSha256!=MetadataExecutionCache.Hash(document.Definition))
            throw new ArgumentException("METADATA_REGIONAL_DOCUMENT_INVALID");
        var expected=Assemble(document.Definition,document.Regions,token);
        if(!Equal(expected,document))throw new ArgumentException("METADATA_REGIONAL_DOCUMENT_REPLAY_MISMATCH");
        return expected;
    }
    public async Task PublishRegionAsync(MetadataRegionalWorldDefinition definition,MetadataGeneratedRegion packet,string newOutputDirectory,CancellationToken token=default)
    {
        var prepared=Prepare(definition,token);var accepted=ReplayPrepared(prepared,packet,token);
        await PublishPreparedRegionAsync(prepared,accepted,newOutputDirectory,token);
    }
    private async Task PublishPreparedRegionAsync(MetadataRegionalWorldDefinition prepared,MetadataGeneratedRegion accepted,string newOutputDirectory,CancellationToken token)
    {
        var region=prepared.Regions.Single(r=>r.Id==accepted.RegionId);var recipe=EffectiveRecipe(prepared,region,token);
        var receipt=new MetadataRegionReceipt(1,accepted.RegionId,accepted.ContextSha256,accepted.ImplementationId,MetadataExecutionCache.Hash(accepted.World),accepted.Dependencies);
        await generator.PublishAsync(recipe,region.Catalog,accepted.World,newOutputDirectory,null,token,regionalPublication:new(prepared,receipt));
    }
    public async Task<MetadataGeneratedRegion> ReadRegionAsync(string directory,MetadataRegionalWorldDefinition? currentDefinition=null,CancellationToken token=default)
    {
        var definition=await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(Path.Combine(directory,"regional-definition.json"),token);
        var receipt=await WorldMetadataGenerator.ReadAsync<MetadataRegionReceipt>(Path.Combine(directory,"generation-region.json"),token);
        if(receipt.Version!=1)throw new ArgumentException("METADATA_REGIONAL_RECEIPT_INVALID");
        var stored=Prepare(definition,token);var region=stored.Regions.SingleOrDefault(r=>r.Id==receipt.RegionId);
        if(region is null||receipt.ContextSha256!=Context(stored,region,token))throw new ArgumentException("METADATA_REGIONAL_PACKET_SOURCE_MISMATCH");
        var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(Path.Combine(directory,"recipe.json"),token);
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(Path.Combine(directory,"catalog.json"),token);
        if(!Equal(recipe,EffectiveRecipe(stored,region,token))||!Equal(catalog,region.Catalog))throw new ArgumentException("METADATA_REGIONAL_SOURCE_FILES_MISMATCH");
        var world=await generator.ReadStoredMetadataAsync(Path.Combine(directory,"world-metadata.json"),token);
        if(receipt.WorldSha256!=MetadataExecutionCache.Hash(world))throw new ArgumentException("METADATA_REGIONAL_RECEIPT_WORLD_MISMATCH");
        return ReplayRegion(currentDefinition??definition,new(1,receipt.RegionId,receipt.ContextSha256,receipt.ImplementationId,world,receipt.Dependencies),token);
    }
    public Task<MetadataRegionalWorldDocument> ReadWorldAsync(string file,CancellationToken token=default)=>generator.ReadStoredRegionsAsync(file,token);
    public Task PublishAsync(MetadataRegionalWorldDocument document,string newOutputDirectory,CancellationToken token=default)
        =>PublishCoreAsync(document,newOutputDirectory,null,null,token);
    internal Task PublishSessionAsync(MetadataRegionalWorldDocument document,string newOutputDirectory,MetadataRegionalSessionCheckpoint checkpoint,CancellationToken token)
        =>PublishCoreAsync(document,newOutputDirectory,checkpoint,null,token);
    internal Task PublishDesignAsync(MetadataRegionalWorldDocument document,string newOutputDirectory,MetadataRegionalDesignReceipt receipt,CancellationToken token)
        =>PublishCoreAsync(document,newOutputDirectory,null,receipt,token);
    private async Task PublishCoreAsync(MetadataRegionalWorldDocument document,string newOutputDirectory,MetadataRegionalSessionCheckpoint? checkpoint,MetadataRegionalDesignReceipt? design,CancellationToken token)
    {
        // Design replay already regenerates the selected complete world from its
        // authored source. Standalone/session publication retains regional replay.
        var accepted=design is null?ReplayDocument(document,token):
            new MetadataRegionalDesignGenerator(generator).ReplayVerified(design,document,token).World;
        string target=Path.GetFullPath(newOutputDirectory),parent=Path.GetDirectoryName(target)!;
        if(Directory.Exists(target)||File.Exists(target))throw new IOException("METADATA_OUTPUT_EXISTS");
        Directory.CreateDirectory(parent);string staging=Path.Combine(parent,".world-regions-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);Exception? failure=null;
        try
        {
            var storage=await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"world-regions.json"),accepted,token);
            if(checkpoint is not null)
            {
                if(checkpoint.WorldSha256!=MetadataExecutionCache.Hash(accepted))throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_WORLD_MISMATCH");
                await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"generation-session.json"),checkpoint,token);
            }
            if(design is not null)
            {
                if(design.WorldSha256!=MetadataExecutionCache.Hash(accepted))throw new ArgumentException("METADATA_REGIONAL_DESIGN_WORLD_MISMATCH");
                await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"world-design.json"),design,token);
            }
            await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"definition.json"),accepted.Definition,token);
            var html=new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Shared-coordinate world regions</title><h1>Shared-coordinate world regions</h1><p><a href=\"schematic.html\">Inspect the complete world</a></p><p>Complete source programs and physical connecting paths retain their original engine coordinates. Individual region inspectors remain available:</p><ul>");
            for(int i=0;i<accepted.Regions.Count;i++)
            {
                var region=accepted.Regions[i];string name="region-"+i.ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
                await PublishPreparedRegionAsync(accepted.Definition,region,Path.Combine(staging,name),token);
                html.Append("<li><a href=\"").Append(name).Append("/schematic.html\">").Append(WebUtility.HtmlEncode(region.RegionId)).Append("</a> — ").Append(region.World.Nodes.Count).Append(" nodes</li>");
            }
            html.Append("</ul><p><a href=\"world-regions.json\">Complete world data, ownership and connections</a></p>");
            if(checkpoint is not null)html.Append("<p><a href=\"generation-session.json\">Replayed source history and checkpoint</a></p>");
            if(design is not null)html.Append("<p><a href=\"world-design.json\">World design request and selection attempts</a></p>");
            html.Append("</html>");
            await File.WriteAllTextAsync(Path.Combine(staging,"index.html"),html.ToString(),token);
            await new WorldMetadataSchematicExporter().WriteRegionalStoredAsync(accepted,staging,storage,token);
            await MetadataDirectoryPublication.CommitAsync(staging,target,ct=>{
                foreach(var packet in accepted.Regions)
                    ValidateDependencies(accepted.Definition,packet,ct);
            },token);
        }
        catch(Exception error){failure=error;throw;}
        finally{MetadataDirectoryPublication.Cleanup(staging,parent,".world-regions-",failure);}
    }
}
