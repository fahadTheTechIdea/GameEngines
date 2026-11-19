using WorldCreator.Core.Spatial;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Metadata;

internal sealed class MetadataRegionalConnectionInfeasibleException(string regionId,string connectionId,WorldRouteStatus status,long work,WorldRouteDomainDisconnection? domain=null)
    :ArgumentException("METADATA_REGIONAL_CONNECTION_UNRESOLVED:"+connectionId+":"+status)
{
    internal string RegionId {get;}=regionId;
    internal string ConnectionId {get;}=connectionId;
    internal WorldRouteStatus Status {get;}=status;
    internal long Work {get;}=work;
    internal WorldRouteDomainDisconnection? Domain {get;}=domain;
}

public sealed partial class MetadataRegionalWorldGenerator
{
    private static WorldPoint RegionalEndpoint(MetadataRegionalWorldDefinition definition,IReadOnlyDictionary<string,WorldMetadataDocument> worlds,
        IReadOnlyList<WorldSurfaceExclusion> obstacles,WorldPolygon domain,WorldTraversalProfile profile,MetadataRegionalEndpoint endpoint,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var world=worlds[endpoint.RegionId];budget.Charge(world.Nodes.Count);
        var node=world.Nodes.SingleOrDefault(n=>n.Id==endpoint.NodeId)??throw new ArgumentException("METADATA_REGIONAL_ENDPOINT_REQUIRED:"+endpoint.NodeId);
        var query=WorldSurfaceQuery.CreateRegional(definition.Surface,domain,token,obstacles);
        var point=new MetadataPhysicalAccessPlanner().Endpoint(node,endpoint.PortId,world.AccessPlan,query.Definition,profile,budget,token);
        if(endpoint.PortId is not null)
        {
            var port=world.AccessPlan!.Sites.Single(s=>s.NodeId==endpoint.NodeId).Ports.Single(p=>p.Id==endpoint.PortId);
            // An opening exempts its own solid for the approach only. All foreign
            // bodies/walls and the real terrain remain hard constraints.
            string ownerId="regional-"+MetadataExecutionCache.Hash(new{Region=endpoint.RegionId,Node=endpoint.NodeId});
            var approachQuery=WorldSurfaceQuery.CreateRegional(definition.Surface,domain,token,obstacles.Where(o=>o.Id!=ownerId).ToArray());
            var traversal=new WorldSurfaceTraversal(approachQuery,profile,token);
            if(traversal.Evaluate(port.BoundaryPosition,port.ApproachPosition,budget,token) is null||traversal.Evaluate(port.ApproachPosition,port.BoundaryPosition,budget,token) is null)
                throw new ArgumentException("METADATA_REGIONAL_APPROACH_BLOCKED:"+endpoint.RegionId+":"+endpoint.NodeId+":"+endpoint.PortId);
        }
        return point;
    }
    // Only the ownership planner calls this with its detached provisional result.
    // All public generation/read/export routes still replay ownership. The probe
    // retains analysis, complete generation, global occupancy and every route gate;
    // it cannot recursively replay the planner that is currently qualifying it.
    internal MetadataRegionalWorldDocument ProbePhysical(MetadataRegionalWorldDefinition candidate,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var definition=Prepare(candidate,token,out long preparationWork,replayOwnership:false);
        var packets=new List<MetadataGeneratedRegion>();
        foreach(var region in definition.Regions)
        {
            token.ThrowIfCancellationRequested();MetadataGeneratedRegion packet;
            try{packet=GeneratePrepared(definition,region,token);}
            catch(ArgumentException error)when(error is MetadataPlacementInfeasibleException or MetadataSpatialConstraintException)
            {
                long failed=error.Data["PlacementWork"] is long p?p:0;
                if(error.Data["MixWork"] is long mix)failed=checked(failed+mix);
                if(error.Data["RegionWork"] is long regional)failed=checked(failed+regional);
                budget.Charge(failed);error.Data["RegionalFailureRegionId"]=region.Id;throw;
            }
            budget.Charge(PhysicalQualificationWork(packet.World));packets.Add(packet);
        }
        try
        {
            var world=AssemblePrepared(definition,Collect(definition,packets,token),token,preparationWork);
            budget.Charge(world.Work);return world;
        }
        catch(MetadataRegionalConnectionInfeasibleException error)
        {budget.Charge(error.Work);throw;}
        catch(MetadataRegionalNetworkInfeasibleException error)
        {budget.Charge(error.Work);throw;}
        catch(MetadataRegionalNetworkSelectorException error)
        {budget.Charge(error.Work);throw;}
    }

    // Reported placement/access/route ledgers are charged once per actual phase.
    // Environmental/content stages retain their own original local caps; this is
    // a physical qualification ledger, not a CPU-time or all-stage work claim.
    private static long PhysicalQualificationWork(WorldMetadataDocument world)
    {
        var source=world.PlacementPlan?.Definition.SourceWorld;
        long placement=world.PopulationCoordinationPlan?.Work??checked((world.PlacementPlan?.Work??0)+(source?.PlacementPlan?.Work??0));
        return checked(placement+(world.AccessPlan?.Work??0)+(world.RoutePlan?.Work??0)+(source?.AccessPlan?.Work??0)+(source?.RoutePlan?.Work??0)+
            (world.ProgramCompositionPlan?.MixWork??0)+(world.ProgramCompositionPlan?.RegionWork??0)+(source?.ProgramCompositionPlan?.MixWork??0)+(source?.ProgramCompositionPlan?.RegionWork??0));
    }
}
