using System.Text.Json;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

/// <summary>Authenticates persisted source facts against the actual registered implementation.</summary>
internal static class MetadataRegisteredStructure
{
    internal static ISettlementCatalogPolicy Resolve(MetadataStructureDefinition saved,ISettlementCatalogRegistry? catalogs,string prefix)
    {
        if(saved.Technology is null)throw new ArgumentException(prefix+"_TECHNOLOGY_REQUIRED");
            var pack=(catalogs??throw new ArgumentException(prefix+"_CATALOG_REQUIRED"))
                .Resolve(new(saved.Catalog.Id,saved.Catalog.EnabledCapabilities));
            if(!pack.Provenance.EnabledCapabilities.SetEquals(saved.Catalog.EnabledCapabilities)||
                !Same(pack.Provenance with{EnabledCapabilities=saved.Catalog.EnabledCapabilities},saved.Catalog))
                throw new ArgumentException(prefix+"_CATALOG_MISMATCH");
            var sourceDefinition=pack.Structures.SingleOrDefault(d=>d.Id==saved.Definition.Id)??throw new ArgumentException(prefix+"_SOURCE_MISSING");
            var resolved=pack.Policy.Resolve(sourceDefinition,saved.Technology!);resolved.Validate();
            var variant=resolved.SizeVariants?.SingleOrDefault(v=>v.Id==saved.Variant.Id);
            bool Same<T>(T a,T b)=>JsonElement.DeepEquals(JsonSerializer.SerializeToElement(a,WorldJson.CreateOptions()),JsonSerializer.SerializeToElement(b,WorldJson.CreateOptions()));
            if(variant is null||!Same(resolved,saved.Definition)||!Same(variant,saved.Variant)||
                pack.Policy.RequiredCapabilities(resolved,saved.Technology!).Any(c=>!saved.Catalog.EnabledCapabilities.Contains(c)))
                throw new ArgumentException(prefix+"_SOURCE_MISMATCH");
        return pack.Policy;
    }
}
