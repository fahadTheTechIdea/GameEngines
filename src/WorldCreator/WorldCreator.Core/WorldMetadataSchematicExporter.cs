using System.Reflection;
using System.Text.Json;
using System.Text;
using WorldCreator.Core.Metadata;

namespace WorldCreator.Core;

internal sealed record SchematicDomainEntry(string Id,string Purpose,string Page,string WorldId,string RecipeSha256,string CatalogSha256);
internal sealed record SchematicDomainTransition(string Id,string Kind,string Purpose,string SourceRuleId,
    string FromDomainId,string FromNodeId,string ToDomainId,string ToNodeId,bool Bidirectional);
internal sealed record SchematicDomainNavigation(int Version,string CollectionId,string DefinitionSha256,string CurrentDomainId,
    IReadOnlyList<SchematicDomainEntry> Entries,IReadOnlyList<SchematicDomainTransition> Transitions);

/// <summary>Packages the JavaScript schematic viewer with its metadata; no remote resources or artwork.</summary>
public sealed class WorldMetadataSchematicExporter(WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs=null,IEnumerable<IMetadataLayout>? layouts=null)
{
    public async Task WriteAsync(WorldMetadataDocument world, string directory, CancellationToken token = default)
    {
        WorldMetadataGenerator.ValidateDocument(world,catalogs,token,layouts);
        (string Html,string JavaScript) files;
        try{files=await FilesAsync(world,null,token);}
        catch(ArgumentException e)when(e.Message=="METADATA_STORAGE_INLINE_LIMIT")
        {
            var storage=await MetadataJsonStorage.WriteAsync(Path.Combine(directory,"world-metadata.json"),world,token);
            files=await FilesAsync(world,storage,token);
        }
        await SaveAsync(directory,files,token);
    }

    internal async Task WriteStoredAsync(WorldMetadataDocument world,string directory,MetadataJsonManifest? storage,CancellationToken token,
        SchematicDomainNavigation? navigation=null)
    {
        WorldMetadataGenerator.ValidateDocument(world,catalogs,token,layouts);
        await SaveAsync(directory,await FilesAsync(world,storage,token,navigation),token);
    }
    internal async Task WriteRegionalStoredAsync(MetadataRegionalWorldDocument world,string directory,MetadataJsonManifest? storage,CancellationToken token)
    {
        var files=await FilesAsync(world,storage,token);
        files.Html=files.Html.Replace("<script src=\"schematic.js\"></script>",
            "<script src=\"schematic.js\"></script>\n<script src=\"regional-schematic.js\"></script>",StringComparison.Ordinal);
        await SaveAsync(directory,files,token);
        await File.WriteAllTextAsync(Path.Combine(directory,"regional-schematic.js"),await ResourceAsync("world-regional-schematic.js",token),token);
    }
    private static async Task SaveAsync(string directory,(string Html,string JavaScript) files,CancellationToken token)
    {
        if(Encoding.UTF8.GetByteCount(files.Html)>MetadataJsonStorage.MaximumFileBytes)throw new ArgumentException("METADATA_SCHEMATIC_SIZE_LIMIT");
        await File.WriteAllTextAsync(Path.Combine(directory,"schematic.html"),files.Html,token);
        await File.WriteAllTextAsync(Path.Combine(directory,"schematic.js"),files.JavaScript,token);
    }

    public async Task<(string Html,string JavaScript)> CreateAsync(WorldMetadataDocument world,CancellationToken token=default)
    {
        WorldMetadataGenerator.ValidateDocument(world,catalogs,token,layouts);
        try{return await FilesAsync(world,null,token);}
        catch(ArgumentException e)when(e.Message=="METADATA_STORAGE_INLINE_LIMIT")
        {throw new ArgumentException("METADATA_SCHEMATIC_DIRECTORY_REQUIRED",e);}
    }

    private static async Task<(string Html,string JavaScript)> FilesAsync<T>(T world,MetadataJsonManifest? storage,CancellationToken token,
        SchematicDomainNavigation? navigation=null)
    {
        string template = await ResourceAsync("schematic.html", token);
        // The default JSON encoder escapes HTML delimiters, including script closing tags.
        string payload=storage is null?Encoding.UTF8.GetString(await MetadataJsonStorage.InlineAsync(world,token)):
            JsonSerializer.Serialize(storage,MetadataJsonStorage.Options());
        // Replace the complete script slot: a source string may itself contain a placeholder name.
        string html = template.Replace("<script id=\"world-data\" type=\"application/json\">__WORLD_METADATA_JSON__</script>",
            "<script id=\"world-data\" type=\"application/json\">"+payload+"</script>",StringComparison.Ordinal)
            .Replace("<script id=\"domain-data\" type=\"application/json\">__DOMAIN_NAVIGATION_JSON__</script>",
                "<script id=\"domain-data\" type=\"application/json\">"+JsonSerializer.Serialize(navigation,WorldJson.CreateOptions())+"</script>",StringComparison.Ordinal);
        return (html,await ResourceAsync("world-schematic.js",token));
    }

    private static async Task<string> ResourceAsync(string file, CancellationToken token)
    {
        var assembly = typeof(WorldMetadataSchematicExporter).Assembly;
        string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Schematic." + file, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(token);
    }
}
