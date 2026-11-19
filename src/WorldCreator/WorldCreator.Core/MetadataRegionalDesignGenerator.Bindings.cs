using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal sealed class MetadataRegionalDesignBindingUnsatisfiedException(string network,string destination,int count)
    :ArgumentException("METADATA_REGIONAL_DESIGN_BINDING_COUNT:"+network+":"+destination+":"+count);

public sealed partial class MetadataRegionalDesignGenerator
{
    private static void ValidateChangedRootReferences(MetadataRegionalDesignRequest source,string regionId)
    {
        if(source.Template.Connections?.Any(c=>c is not null&&(c.From?.RegionId==regionId||c.To?.RegionId==regionId))==true||
            source.Template.Topology?.Destinations?.Any(d=>d?.Endpoint?.RegionId==regionId)==true)
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_UNBOUND_EXTERNAL_ENDPOINT:"+regionId);
        foreach(var rule in source.Template.NetworkRules??[])
        foreach(var destination in rule?.Destinations??[])
            if(destination?.Endpoint?.RegionId==regionId&&destination.Endpoint.Selector is null&&source.Bindings?.Any(b=>b is not null&&b.NetworkRuleId==rule!.Id&&b.DestinationId==destination.Id)!=true)
                throw new ArgumentException("METADATA_REGIONAL_DESIGN_UNBOUND_EXTERNAL_ENDPOINT:"+regionId);
    }
    private static void ValidateBindings(MetadataRegionalDesignRequest source,IReadOnlySet<string> regionIds)
    {
        if(source.Bindings is null)
        {
            if(source.MaximumBindingWork is not null)throw new ArgumentException("METADATA_REGIONAL_DESIGN_BINDING_WORK_UNUSED");
            return;
        }
        if(source.Bindings.Count is <1 or >128||source.MaximumBindingWork is <1 or >200000000||source.Template.NetworkRules is not {Count:>=1 and <=4})
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_BINDINGS_INVALID");
        var keys=new HashSet<(string,string)>();
        foreach(var binding in source.Bindings)
        {
            if(binding is null||!WorldEnvironmentQuery.Id(binding.NetworkRuleId)||!WorldEnvironmentQuery.Id(binding.DestinationId)||
                !WorldEnvironmentQuery.Id(binding.NodeKind)||binding.ArchetypeId is {} archetype&&!WorldEnvironmentQuery.Id(archetype)||
                !keys.Add((binding.NetworkRuleId,binding.DestinationId)))
                throw new ArgumentException("METADATA_REGIONAL_DESIGN_BINDING_INVALID");
            var networks=source.Template.NetworkRules.Where(n=>n is not null&&n.Id==binding.NetworkRuleId).ToArray();
            var destinations=networks.Length==1?networks[0].Destinations?.Where(d=>d is not null&&d.Id==binding.DestinationId).ToArray():null;
            if(destinations is not {Length:1}||destinations[0].Endpoint is null||!regionIds.Contains(destinations[0].Endpoint.RegionId))
                throw new ArgumentException("METADATA_REGIONAL_DESIGN_BINDING_TARGET_INVALID:"+binding.NetworkRuleId+":"+binding.DestinationId);
        }
    }

    private MetadataRegionalOwnershipRequest BindEndpoints(
        MetadataRegionalOwnershipRequest request,IReadOnlyList<MetadataRegionalDesignBinding> bindings,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var lookup=new Dictionary<(string,string),MetadataRegionalDesignBinding>();
        foreach(var binding in bindings.OrderBy(b=>b.NetworkRuleId,StringComparer.Ordinal).ThenBy(b=>b.DestinationId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            lookup.Add((binding.NetworkRuleId,binding.DestinationId),binding);
        }
        var networks=request.NetworkRules!.Select(rule=>rule with{Destinations=rule.Destinations.Select(d=>
            lookup.TryGetValue((rule.Id,d.Id),out var binding)?d with{Endpoint=d.Endpoint with{NodeId="",Selector=new(binding.NodeKind,binding.ArchetypeId)}}:d).ToArray()}).ToArray();
        return request with{NetworkRules=networks};
    }

    private static IReadOnlyList<MetadataRegionalDesignBoundEndpoint> ResolveEndpoints(IReadOnlyList<MetadataRegionalDesignBinding> bindings,
        MetadataRegionalWorldDocument world,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var endpoints=new List<MetadataRegionalDesignBoundEndpoint>();
        foreach(var binding in bindings.OrderBy(b=>b.NetworkRuleId,StringComparer.Ordinal).ThenBy(b=>b.DestinationId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            budget.Charge(bindings.Count+world.Regions.Count+1);
            var destination=world.Definition.NetworkRules!.Single(n=>n.Id==binding.NetworkRuleId).Destinations.Single(d=>d.Id==binding.DestinationId);
            var region=world.Regions.Single(r=>r.RegionId==destination.Endpoint.RegionId).World;
            budget.Charge(region.Nodes.Count);
            var matches=region.Nodes.Where(n=>n.Kind==binding.NodeKind&&(binding.ArchetypeId is null||n.ArchetypeId==binding.ArchetypeId)).ToArray();
            var resolved=world.NetworkPlan!.Networks.Single(n=>n.RuleId==binding.NetworkRuleId).ResolvedDestinations!.Single(d=>d.DestinationId==binding.DestinationId).Endpoint;
            if(matches.Length!=1||matches[0].Id!=resolved.NodeId)
                throw new ArgumentException("METADATA_REGIONAL_DESIGN_BINDING_CHANGED:"+binding.NetworkRuleId+":"+binding.DestinationId);
            endpoints.Add(new(binding.NetworkRuleId,binding.DestinationId,destination.Endpoint.RegionId,resolved.NodeId));
        }
        return endpoints.ToArray();
    }
}
