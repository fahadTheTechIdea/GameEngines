namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalWorldGenerator
{
    // Only the owning session supplies these private accepted packets/caches.
    // File reads still use full source replay; no deserialized receipt enters here.
    internal MetadataRegionalWorldDocument GenerateForSession(MetadataRegionalWorldDefinition source,MetadataRegionalWorldDocument? previous,
        IReadOnlyDictionary<string,MetadataExecutionCache>? previousCaches,IDictionary<string,MetadataExecutionCache> caches,
        ISet<string> reused,CancellationToken token)
    {
        var definition=Prepare(source,token,out long preparationWork);
        var old=previous?.Regions.ToDictionary(r=>r.RegionId,StringComparer.Ordinal);
        long cacheBytes=0;
        var packets=Collect(definition,definition.Regions.Select(region=>{
            token.ThrowIfCancellationRequested();MetadataExecutionCache? prior=null;
            previousCaches?.TryGetValue(region.Id,out prior);
            if(old?.TryGetValue(region.Id,out var packet)==true&&prior is not null&&packet.ImplementationId==Implementation&&
                packet.ContextSha256==Context(definition,region,token)&&
                Equal(packet.Dependencies,generator.Dependencies(region.Recipe,region.Catalog,token)))
            {
                caches.Add(region.Id,prior);reused.Add(region.Id);
            }
            else
            {
                var cache=new MetadataExecutionCache(prior);caches.Add(region.Id,cache);
                packet=GeneratePrepared(definition,region,token,cache);
            }
            cacheBytes+=caches[region.Id].SizeBytes;
            if(cacheBytes>MetadataExecutionCache.MaximumBytes)throw new ArgumentException("METADATA_REGIONAL_SESSION_CACHE_SIZE_LIMIT");
            return packet;
        }),token);
        // Cached content does not exempt any member from global constraints.
        return AssemblePrepared(definition,packets,token,preparationWork);
    }
}
