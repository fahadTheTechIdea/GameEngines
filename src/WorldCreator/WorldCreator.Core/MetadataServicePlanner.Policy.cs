using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataServicePlanner
{
    private static string SupplyKey(string nodeId,string supplyId)=>nodeId+"/"+supplyId;
    private static IReadOnlyList<CatalogSupplySource> SupplyPolicyInputs(IReadOnlyList<MetadataResolvedServices> sources,
        IReadOnlyDictionary<string,WorldMetadataNode> nodes,MetadataSpace space,MetadataPhysicalAccessPlan? access,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var result=new List<CatalogSupplySource>();
        foreach(var source in sources)foreach(var supply in source.Supplies)
        {
            budget.Charge(1+(supply.UpstreamSources?.Count??0));token.ThrowIfCancellationRequested();
            var node=nodes[source.NodeId];WorldPoint? position=null;
            if(supply.PortId is {} id)
            {
                var port=access?.Sites.SingleOrDefault(s=>s.NodeId==node.Id)?.Ports.SingleOrDefault(p=>p.Id==id)
                    ??throw new ArgumentException("METADATA_SERVICE_SUPPLY_PORT_REQUIRED");
                if(space.Unit!="meters")throw new ArgumentException("METADATA_SERVICE_SUPPLY_PORT_UNIT");
                position=port.BoundaryPosition;
            }
            else if(node.StructureKey is null&&space.Unit=="meters"&&node.Position is {Count:2} point)position=new(point[0],point[1]);
            result.Add(new(SupplyKey(source.NodeId,supply.Id),supply.Kind,supply.Unit,supply.Origin,
                supply.Qualities.ToHashSet(StringComparer.Ordinal),position,node.StructureKey is null?null:node.Id,
                (supply.UpstreamSources??[]).Select(u=>SupplyKey(u.NodeId,u.SupplyId)).ToArray()));
        }
        return result.AsReadOnly();
    }
    private Dictionary<string,(MetadataStructureDefinition Definition,ISettlementCatalogPolicy Policy)> RegisteredPolicies(
        IReadOnlyList<MetadataResolvedServices> sources,IReadOnlyDictionary<string,WorldMetadataNode> nodes,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,WorldSpatialWorkBudget budget)
    {
        var result=new Dictionary<string,(MetadataStructureDefinition,ISettlementCatalogPolicy)>(StringComparer.Ordinal);
        foreach(var source in sources)
        {
            if(nodes[source.NodeId].StructureKey is not {} key)continue;
            budget.Charge();var saved=structures[key];
            var policy=MetadataRegisteredStructure.Resolve(saved,catalogs,"METADATA_SERVICE_POLICY");
            result.Add(source.NodeId,(saved,policy));
        }
        return result;
    }
}
