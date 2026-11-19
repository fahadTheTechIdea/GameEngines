using System.Collections.ObjectModel;
using System.Text.Json;

namespace WorldCreator.Core.Planning;

internal static class CatalogMetadataProperties
{
    public static IReadOnlyDictionary<string,JsonElement> Snapshot(IReadOnlyDictionary<string,JsonElement>? properties,string errorCode)
    {
        properties??=new Dictionary<string,JsonElement>();
        if(properties.Count>64||properties.Any(p=>!SettlementCatalogRegistry.ValidId(p.Key)||p.Value.ValueKind==JsonValueKind.Undefined)||
            JsonSerializer.SerializeToUtf8Bytes(properties).Length>65536)throw new ArgumentException(errorCode);
        var ordered=new SortedDictionary<string,JsonElement>(StringComparer.Ordinal);
        foreach(var property in properties)ordered.Add(property.Key,property.Value.Clone());
        return new ReadOnlyDictionary<string,JsonElement>(ordered);
    }
}
