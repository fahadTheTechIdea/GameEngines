using System.Globalization;
using System.Text.Json;
using WorldCreator.Core.Planning;
using static WorldCreator.Core.Metadata.MetadataInputValues;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataGenerationSession
{
    private static IReadOnlyList<MetadataProtectedValue> CaptureProtections(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,IReadOnlyList<MetadataInputProtection> definitions)
    {
        if(definitions.Count>64)throw new ArgumentException("METADATA_PROTECTION_LIMIT");
        var ids=new HashSet<string>(StringComparer.Ordinal);var pointers=new HashSet<(MetadataInputSource,string)>();
        var recipeJson=Element(recipe);var catalogJson=Element(catalog);var result=new List<MetadataProtectedValue>();
        foreach(var d in definitions)
        {
            if(d is null||!SettlementCatalogRegistry.ValidId(d.Id)||!ids.Add(d.Id)||!Enum.IsDefined(d.Input)||!Text(d.Purpose)||!Text(d.SourceRuleId)||!pointers.Add((d.Input,d.Pointer)))
                throw new ArgumentException("METADATA_PROTECTION_INVALID");
            var value=Resolve(d.Input==MetadataInputSource.Recipe?recipeJson:catalogJson,d.Pointer)
                ??throw new ArgumentException("METADATA_PROTECTED_INPUT_MISSING:"+d.Id);
            result.Add(new(d,MetadataExecutionCache.Hash(value)));
        }
        return Array.AsReadOnly(result.OrderBy(p=>p.Definition.Id,StringComparer.Ordinal).ToArray());
    }

    private static void CheckProtections(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,IReadOnlyList<MetadataProtectedValue> protections)
    {
        var recipeJson=Element(recipe);var catalogJson=Element(catalog);
        foreach(var p in protections)
        {
            var value=Resolve(p.Definition.Input==MetadataInputSource.Recipe?recipeJson:catalogJson,p.Definition.Pointer);
            if(value is null||MetadataExecutionCache.Hash(value.Value)!=p.ValueSha256)
                throw new ArgumentException("METADATA_PROTECTED_INPUT_CONFLICT:"+p.Definition.Id+":"+p.Definition.Pointer);
        }
    }

    private static IReadOnlyList<MetadataInputChange> Changes(WorldMetadataRecipe oldRecipe,WorldMetadataCatalog oldCatalog,
        WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,CancellationToken token)
    {
        var result=new List<MetadataInputChange>();
        Walk(MetadataInputSource.Recipe,"",Element(oldRecipe),Element(recipe));
        Walk(MetadataInputSource.Catalog,"",Element(oldCatalog),Element(catalog));
        return result.AsReadOnly();
        void Walk(MetadataInputSource source,string pointer,JsonElement? a,JsonElement? b)
        {
            token.ThrowIfCancellationRequested();
            if(a is {} av&&b is {} bv)
            {
                if(JsonElement.DeepEquals(av,bv))return;
                if(av.ValueKind==JsonValueKind.Object&&bv.ValueKind==JsonValueKind.Object)
                {
                    foreach(var key in av.EnumerateObject().Select(p=>p.Name).Concat(bv.EnumerateObject().Select(p=>p.Name)).Distinct().Order(StringComparer.Ordinal))
                        Walk(source,pointer+"/"+key.Replace("~","~0",StringComparison.Ordinal).Replace("/","~1",StringComparison.Ordinal),
                            av.TryGetProperty(key,out var x)?x:null,bv.TryGetProperty(key,out var y)?y:null);
                    return;
                }
                if(av.ValueKind==JsonValueKind.Array&&bv.ValueKind==JsonValueKind.Array)
                {
                    bool Numeric(JsonElement array)
                    {
                        foreach(var item in array.EnumerateArray())
                        {token.ThrowIfCancellationRequested();if(item.ValueKind!=JsonValueKind.Number)return false;}
                        return true;
                    }
                    // Numeric vectors/fields are authored buffers. A report records
                    // the whole before/after value rather than one entry per sample.
                    // Full input history remains saved; CheckProtections separately
                    // resolves and authenticates any protected individual element.
                    if(!Numeric(av)||!Numeric(bv))
                    {
                        for(int i=0;i<Math.Max(av.GetArrayLength(),bv.GetArrayLength());i++)
                            Walk(source,pointer+"/"+i.ToString(CultureInfo.InvariantCulture),i<av.GetArrayLength()?av[i]:null,i<bv.GetArrayLength()?bv[i]:null);
                        return;
                    }
                }
            }
            if(result.Count>=4096)throw new ArgumentException("METADATA_EDIT_CHANGE_LIMIT");
            result.Add(new(source,pointer,a is {} aa?MetadataExecutionCache.Hash(aa):null,b is {} bb?MetadataExecutionCache.Hash(bb):null));
        }
    }
}
