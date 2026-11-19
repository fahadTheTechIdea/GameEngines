using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldCreator.Core.Metadata;

public sealed partial class WorldMetadataGenerator
{
    // This is an internal typed-value copy, not an input or publication format.
    // Retain NaN/infinity so the original semantic owner reports the invalid
    // field rather than letting serialization replace its diagnostic.
    private static JsonSerializerOptions InputCopyOptions {get;}=CreateInputCopyOptions();
    private static JsonSerializerOptions CreateInputCopyOptions()
    {
        var options=new JsonSerializerOptions(WorldJson.Shared){NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
        options.MakeReadOnly(populateMissingResolver:true);return options;
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) CaptureInputs(
        WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(recipe is null||catalog is null)throw new ArgumentException("METADATA_RECIPE_OR_CATALOG_INVALID");
        T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value,InputCopyOptions),InputCopyOptions)!;
        var result=(Copy(recipe),Copy(catalog));
        token.ThrowIfCancellationRequested();return result;
    }
}
