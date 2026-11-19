using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

public enum MetadataAccountingStatus { Measured, NotApplicable, Unmeasured }
public sealed record MetadataAccountingSource(string NodeId,MetadataOccupancyKind Occupancy,string Purpose,string SourceRuleId,string FootprintSha256,string? ClearanceSha256);
public sealed record MetadataGroundUseObservation(string NodeId,MetadataGroundUse Definition,string FootprintSha256,
    double SourceArea,double OpenArea,double OpenFraction,bool MinimumSatisfied);
public sealed record MetadataAccountingInteriorWall(string Id,string RoomNodeId,string FootprintSha256);
public sealed record MetadataInteriorCoverageObservation(string RuleId,string ScopeNodeId,string Purpose,string SourceRuleId,
    IReadOnlyList<string> RoomNodeIds,IReadOnlyList<MetadataAccountingInteriorWall> Walls,
    double FloorUnionArea,double WallUnionArea,double ClearFloorArea,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataInteriorFloorRequirement? Requirement=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] bool? MinimumSatisfied=null);
public sealed record MetadataPhysicalAccounting(int SchemaVersion,string WorldId,string RecipeSha256,string CatalogSha256,
    string GeometrySha256,string Unit,MetadataAccountingStatus Status,string? Reason,long MaximumWork,long Work,
    IReadOnlyList<MetadataAccountingSource> Sources,int SolidSources,int ReservationSources,int UnclassifiedSources,int ClearanceReservations,
    double? GrossDomainArea,double? SolidUnionArea,double? ReservationUnionArea,double? PhysicalFootprintUnionArea,double? CombinedUnionArea,
    double? AllocatedSuitableArea,double? UnclaimedSuitableArea,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataGroundUseObservation>? GroundUses=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? DeclaredGroundUnionArea=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? OpenGroundUnionArea=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataInteriorCoverageObservation>? InteriorCoverage=null);

/// <summary>Projected source-footprint accounting, not a content-completion or catalog-approval certificate.
/// Unions use the same bounded surface arrangement as terrain and placement queries.</summary>
public sealed partial class MetadataPhysicalAccountingPlanner
{
    private sealed record Source(MetadataPhysicalSite Site,MetadataAccountingSource Identity,WorldPolygon? Clearance,double Area,
        double MinX,double MinY,double MaxX,double MaxY,MetadataGroundUse? GroundUse=null);

    internal static void ValidateGroundUse(MetadataGroundUse ground)
    {
        if(!SettlementCatalogRegistry.ValidId(ground.Kind)||!WorldEnvironmentQuery.Text(ground.Purpose)||
            !WorldEnvironmentQuery.Text(ground.SourceRuleId)||!double.IsFinite(ground.MinimumOpenFraction)||ground.MinimumOpenFraction is <0 or >1)
            throw new ArgumentException("METADATA_GROUND_USE_INVALID");
    }

    /// <summary>Required declarations participate in generation/read acceptance, not only publication.</summary>
    internal void ValidateRequiredGroundUses(WorldMetadataDocument world,CancellationToken token=default)
    {
        if(world.FootprintDefinitions?.Values.Any(f=>f.GroundUse is not null)!=true)return;
        var report=Measure(world,token:token);
        if(report.Status!=MetadataAccountingStatus.Measured||report.GroundUses is null)
            throw new ArgumentException("METADATA_GROUND_USE_UNMEASURED:"+report.Reason);
        if(report.GroundUses.FirstOrDefault(g=>!g.MinimumSatisfied) is {} failed)
            throw new ArgumentException("METADATA_GROUND_USE_MINIMUM_UNSATISFIED:"+failed.NodeId);
    }

    public MetadataPhysicalAccounting Measure(WorldMetadataDocument world,long maximumWork=20000000,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(world);token.ThrowIfCancellationRequested();
        if(maximumWork is <1 or >200000000)throw new ArgumentException("METADATA_ACCOUNTING_WORK_LIMIT_INVALID");
        if(world.Space?.Axes is null||world.Space.Axes.Count>8||string.IsNullOrWhiteSpace(world.Space.Unit)||world.Nodes is null||world.Nodes.Count>100000||
            world.AccessPlan is {} access&&(access.Sites is null||access.Sites.Count>MetadataPhysicalAccessPlanner.MaximumSites))
            throw new ArgumentException("METADATA_ACCOUNTING_INPUT_INVALID");
        var budget=new WorldSpatialWorkBudget(maximumWork,token,failureCode:"METADATA_ACCOUNTING_WORK_BUDGET");
        WorldPolygon Snapshot(WorldPolygon polygon,int maximum)=>SnapshotPolygon(polygon,maximum,budget);
        budget.Charge(world.Nodes.Count);
        var nodes=new Dictionary<string,WorldMetadataNode>(StringComparer.Ordinal);
        foreach(var node in world.Nodes)
        {
            token.ThrowIfCancellationRequested();
            if(node is null||string.IsNullOrWhiteSpace(node.Id)||node.Id.Length>2048||!nodes.TryAdd(node.Id,node))
                throw new ArgumentException("METADATA_ACCOUNTING_INPUT_INVALID");
        }
        var seen=new HashSet<string>(StringComparer.Ordinal);
        if(world.FootprintDefinitions is {} definitions)
        {
            if(definitions.Count>MetadataPhysicalAccessPlanner.MaximumSites)throw new ArgumentException("METADATA_ACCOUNTING_INPUT_INVALID");
            budget.Charge(definitions.Count);
            foreach(var definition in definitions.Values)
                if(definition.GroundUse is {} ground)ValidateGroundUse(ground);
        }
        IReadOnlyDictionary<string,MetadataPhysicalAllocation> allocations=new Dictionary<string,MetadataPhysicalAllocation>();
        if(world.PlacementPlan is {} placement)
        {
            long count=placement.Definition.Bindings.Count+(placement.Definition.NestedStages??[]).Sum(s=>(long)s.Bindings.Count);
            if(count>MetadataPhysicalAccessPlanner.MaximumSites)throw new ArgumentException("METADATA_ACCOUNTING_INPUT_INVALID");
            budget.Charge(count*count*3+count);
            allocations=MetadataPhysicalAccessPlanner.Allocations(placement).ToDictionary(a=>a.NodeId,StringComparer.Ordinal);
        }
        var sources=new List<Source>();
        foreach(var site in world.AccessPlan?.Sites??[])
        {
            token.ThrowIfCancellationRequested();
            if(site is null||string.IsNullOrWhiteSpace(site.NodeId)||!seen.Add(site.NodeId)||!nodes.TryGetValue(site.NodeId,out var node)||!Enum.IsDefined(site.Occupancy)||
                string.IsNullOrWhiteSpace(site.SourceRuleId)||string.IsNullOrWhiteSpace(node.Purpose))
                throw new ArgumentException("METADATA_ACCOUNTING_SOURCE_INVALID");
            var polygon=Snapshot(site.Footprint,64);var saved=site with{Footprint=polygon};
            string footprintHash=MetadataExecutionCache.Hash(polygon);WorldPolygon? clearance=null;
            if(allocations.TryGetValue(site.NodeId,out var allocation))
            {
                if(MetadataExecutionCache.Hash(allocation.Footprint)!=footprintHash)throw new ArgumentException("METADATA_ACCOUNTING_PHYSICAL_SOURCE_MISMATCH:"+site.NodeId);
                if(allocation.Reservation is {} envelope)clearance=Snapshot(envelope,64);
            }
            var identity=new MetadataAccountingSource(site.NodeId,site.Occupancy,node.Purpose,site.SourceRuleId,footprintHash,clearance is null?null:MetadataExecutionCache.Hash(clearance));
            var groundUse=world.FootprintDefinitions?.GetValueOrDefault(node.ArchetypeId)?.GroundUse;
            if(groundUse is not null&&site.Occupancy!=MetadataOccupancyKind.Reservation)
                throw new ArgumentException("METADATA_GROUND_USE_RESERVATION_REQUIRED:"+site.NodeId);
            sources.Add(MakeSource(saved,identity,clearance,groundUse));
        }
        foreach(var node in nodes.Values)
            if(world.FootprintDefinitions?.GetValueOrDefault(node.ArchetypeId)?.GroundUse is not null&&!seen.Contains(node.Id))
                throw new ArgumentException("METADATA_GROUND_USE_PHYSICAL_SOURCE_REQUIRED:"+node.Id);
        if(sources.Any(s=>s.GroundUse is not null)&&sources.Any(s=>s.Site.Occupancy==MetadataOccupancyKind.Unspecified))
            throw new ArgumentException("METADATA_GROUND_USE_OCCUPANCY_REQUIRED");
        var ordered=sources.OrderBy(s=>s.Identity.NodeId,StringComparer.Ordinal).ToArray();
        var interiors=SnapshotInteriors(world.InteriorPlan,ordered,nodes,Snapshot,budget,token);
        if(interiors is not null&&ordered.Any(s=>s.Site.Occupancy==MetadataOccupancyKind.Unspecified))
            throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_OCCUPANCY_REQUIRED");
        var boundary=world.Surface?.Boundary is {} shape?Snapshot(shape,1024):null;
        var sourceGeometry=new{world.Id,world.Space,Boundary=boundary,
            Sources=ordered.Select(s=>new{s.Identity,s.Site.Footprint,s.Clearance,s.GroundUse}).ToArray()};
        string digest=interiors is null?MetadataExecutionCache.Hash(sourceGeometry):MetadataExecutionCache.Hash(new{
            SourceGeometry=sourceGeometry,Interiors=interiors.Select(i=>new{i.RuleId,i.ScopeNodeId,i.Purpose,i.SourceRuleId,
            RoomNodeIds=i.Rooms.Select(r=>r.Identity.NodeId).ToArray(),i.WallIdentities,Requirement=i.FloorRequirement}).ToArray()});
        double? gross=null;
        MetadataPhysicalAccounting Result(MetadataAccountingStatus status,string? reason,double? solid=null,double? reservation=null,double? physical=null,double? combined=null,
            IReadOnlyList<MetadataGroundUseObservation>? groundUses=null,double? groundUnion=null,double? openGroundUnion=null,
            IReadOnlyList<MetadataInteriorCoverageObservation>? interiorCoverage=null)
        {
            token.ThrowIfCancellationRequested();
            return new(1,world.Id,world.RecipeSha256,world.CatalogSha256,digest,world.Space.Unit,status,reason,maximumWork,budget.Used,
                Array.AsReadOnly(ordered.Select(s=>s.Identity).ToArray()),ordered.Count(s=>s.Site.Occupancy==MetadataOccupancyKind.Solid),
                ordered.Count(s=>s.Site.Occupancy==MetadataOccupancyKind.Reservation),ordered.Count(s=>s.Site.Occupancy==MetadataOccupancyKind.Unspecified),ordered.Count(s=>s.Clearance is not null),
                gross,solid,reservation,physical,combined,world.RegionPlan is {} regions?regions.SuitableArea-regions.UnclaimedSuitableArea:null,
                world.RegionPlan?.UnclaimedSuitableArea,groundUses,groundUnion,openGroundUnion,interiorCoverage);
        }
        if(world.Surface is null)return Result(MetadataAccountingStatus.NotApplicable,"NO_SURFACE_DOMAIN");
        if(world.Space.Axes.Count!=2)return Result(MetadataAccountingStatus.Unmeasured,"UNSUPPORTED_COORDINATE_SPACE");
        if(world.Surface.Unit!=world.Space.Unit)throw new ArgumentException("METADATA_ACCOUNTING_UNIT_MISMATCH");
        if(boundary is null)return Result(MetadataAccountingStatus.Unmeasured,"NO_FINITE_DOMAIN_BOUNDARY");
        gross=Math.Abs(WorldRegionGeometry.Area(boundary.Vertices));
        if(world.AccessPlan is null)return Result(MetadataAccountingStatus.Unmeasured,"NO_CLASSIFIED_PHYSICAL_SOURCE");
        try
        {
            var clearances=ordered.Where(s=>s.Clearance is not null).Select(s=>{
                var polygon=s.Clearance!;
                budget.Charge((long)polygon.Vertices.Count*polygon.Vertices.Count*s.Site.Footprint.Vertices.Count*2);
                if(!polygon.ContainsPolygon(s.Site.Footprint))throw new ArgumentException("METADATA_ACCOUNTING_CLEARANCE_INVALID:"+s.Identity.NodeId);
                return s with{Site=s.Site with{Footprint=polygon},Clearance=null,Area=Math.Abs(WorldRegionGeometry.Area(polygon.Vertices)),
                    MinX=polygon.Vertices.Min(p=>p.X),MinY=polygon.Vertices.Min(p=>p.Y),MaxX=polygon.Vertices.Max(p=>p.X),MaxY=polygon.Vertices.Max(p=>p.Y)};
            }).ToArray();
            var all=ordered.Concat(clearances).ToArray();
            foreach(var source in all)
            {
                // ContainsPolygon checks complete edges in a concave boundary.
                budget.Charge((long)source.Site.Footprint.Vertices.Count*boundary.Vertices.Count*boundary.Vertices.Count*2);
                if(!boundary.ContainsPolygon(source.Site.Footprint))throw new ArgumentException("METADATA_ACCOUNTING_SOURCE_OUTSIDE_DOMAIN:"+source.Identity.NodeId);
            }
            double Union(IEnumerable<Source> selected,IEnumerable<Source>? covers=null)=>MeasureUnion(selected,covers,world.Space.Unit,budget,token);
            double solid=Union(ordered.Where(s=>s.Site.Occupancy==MetadataOccupancyKind.Solid));
            var reservations=ordered.Where(s=>s.Site.Occupancy==MetadataOccupancyKind.Reservation).Concat(clearances).ToArray();
            double reservation=Union(reservations);
            double physical=ordered.All(s=>s.Site.Occupancy==MetadataOccupancyKind.Solid)?solid:
                ordered.All(s=>s.Site.Occupancy==MetadataOccupancyKind.Reservation)&&clearances.Length==0?reservation:Union(ordered);
            double combined=clearances.Length==0?physical:Union(all);
            var grounds=ordered.Where(s=>s.GroundUse is not null).ToArray();
            IReadOnlyList<MetadataGroundUseObservation>? observations=null;double? groundUnion=null,openGroundUnion=null;
            if(grounds.Length>0)
            {
                var solids=ordered.Where(s=>s.Site.Occupancy==MetadataOccupancyKind.Solid).ToArray();
                var measured=new List<MetadataGroundUseObservation>();
                foreach(var source in grounds)
                {
                    double open=Union([source],solids),fraction=Math.Clamp(open/source.Area,0,1);
                    measured.Add(new(source.Identity.NodeId,source.GroundUse!,source.Identity.FootprintSha256,source.Area,open,fraction,
                        fraction+1e-9>=source.GroundUse!.MinimumOpenFraction));
                }
                observations=measured.AsReadOnly();groundUnion=Union(grounds);openGroundUnion=Union(grounds,solids);
            }
            IReadOnlyList<MetadataInteriorCoverageObservation>? coverage=null;
            if(interiors is not null)
                coverage=ObserveInteriors(interiors,ordered,world.Space.Unit,budget,token);
            return Result(MetadataAccountingStatus.Measured,null,solid,reservation,physical,combined,observations,groundUnion,openGroundUnion,coverage);
        }
        catch(ArgumentException error)when(error.Message is "METADATA_ACCOUNTING_WORK_BUDGET" or "WORLD_SURFACE_AREA_EDGE_BUDGET" or "WORLD_SURFACE_AREA_EVENT_BUDGET")
        {return Result(MetadataAccountingStatus.Unmeasured,error.Message);}
    }

    public void Validate(MetadataPhysicalAccounting report,WorldMetadataDocument world,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(report);
        if(MetadataExecutionCache.Hash(report)!=MetadataExecutionCache.Hash(Measure(world,report.MaximumWork,token)))
            throw new ArgumentException("METADATA_ACCOUNTING_SOURCE_MISMATCH");
    }
}
