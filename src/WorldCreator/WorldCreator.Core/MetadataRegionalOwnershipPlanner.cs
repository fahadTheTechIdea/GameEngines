using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalProgramRegion(string Id,string Purpose,string SourceRuleId,double Weight,double MinimumArea,double MinimumClearance,
    WorldSuitabilityProfile Profile,string? PlacementAnchorId,WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalProgramCapacity? Capacity=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalSpatialContext? SpatialContext=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalAnchorPin? PinnedAnchor=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool PreferSuitableAnchor=false);
public sealed record MetadataRegionalAnchorPin(string Purpose,string SourceRuleId,WorldPoint Position);
public sealed class MetadataRegionalAnchorConflictException(string regionId,WorldPoint position,Exception inner)
    :ArgumentException("METADATA_REGIONAL_ANCHOR_PIN_UNSATISFIED:"+regionId,inner)
{
    public string RegionId {get;}=regionId;
    public WorldPoint Position {get;}=position;
}
public sealed record MetadataRegionalProgramCapacity(string Purpose,string SourceRuleId,double MaximumCoverage);
public sealed record MetadataRegionalCouplingRule(string Purpose,string SourceRuleId,int MaximumIterations=16);
public sealed record MetadataRegionalPhysicalFeedbackRule(string Purpose,string SourceRuleId,int MaximumAttempts=8);
public sealed record MetadataRegionalCompositionRule(string Purpose,string SourceRuleId,int MaximumAttempts=16);
public sealed record MetadataRegionalProgramDemand(string RegionId,MetadataProgramAnalysis Analysis,IReadOnlyList<MetadataRegionSourceDemand> Sources,
    double EnvelopeArea,double RequiredArea,double RequiredClearance,double Weight);
public sealed record MetadataRegionalOwnershipRequest(int Version,string Id,string Purpose,string SourceRuleId,WorldSurfaceDefinition Surface,
    IReadOnlyList<MetadataRegionalProgramRegion> Regions,IReadOnlyList<MetadataRegionalConnection> Connections,WorldAreaPopulationPattern Pattern,
    double AnchorPrecision=.1,int MaximumAnchorCells=65536,int MaximumTotalNodes=50000,int MaximumTotalConnections=100000,
    int MaximumReferences=200000,long MaximumWork=20000000,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorldEnvironmentDefinition? OwnershipEnvironment=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalCouplingRule? Coupling=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalPhysicalFeedbackRule? PhysicalFeedback=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalCompositionRule? ProgramComposition=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalTopologyDefinition? Topology=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalNetworkRule>? NetworkRules=null);
public sealed record MetadataRegionalOwnershipAnchor(string RegionId,WorldRegionPlan Plan);
public sealed record MetadataRegionalOwnershipPlan(int Version,MetadataRegionalOwnershipRequest Source,WorldAreaPopulationPlan Partition,
    IReadOnlyList<MetadataRegionalOwnershipAnchor> Anchors,long Work,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalProgramDemand>? ProgramDemands=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalCouplingPlan? CouplingPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalPhysicalFeedbackPlan? PhysicalFeedbackPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalCompositionPlan? CompositionPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalJointPlan? JointPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalPartitionQualification? PartitionQualification=null);

/// <summary>Derives generation ownership and terrain-qualified program anchors.
/// Ownership includes unplayable ground and never specifies streets or storage cells.</summary>
public sealed partial class MetadataRegionalOwnershipPlanner(WorldMetadataGenerator? generator=null)
{
    private readonly WorldMetadataGenerator generator=generator??new();
    public MetadataRegionalWorldDefinition Plan(MetadataRegionalOwnershipRequest source,CancellationToken token=default)=>Plan(source,null,token);

    internal MetadataRegionalWorldDefinition Plan(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(source is null||source.Version!=1||!WorldEnvironmentQuery.Id(source.Id)||!WorldEnvironmentQuery.Text(source.Purpose)||!WorldEnvironmentQuery.Text(source.SourceRuleId)||
            source.Surface?.Boundary is null||
            source.Regions is not {Count:>=2 and <=16}||source.Regions.Any(r=>r is null)||source.Connections is null||source.Connections.Count>128||
            source.Pattern is null||source.Pattern.MaximumCells is <1 or >4096||!double.IsFinite(source.AnchorPrecision)||source.AnchorPrecision is <.000001 or >100000||
            source.MaximumAnchorCells is <1 or >1000000||source.MaximumTotalNodes is <1 or >100000||source.MaximumTotalConnections is <1 or >200000||
            source.MaximumReferences is <1 or >1000000||source.MaximumWork is <1 or >200000000)
            throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_REQUEST_INVALID");
        if(source.Pattern.DensityFieldId is not null&&source.OwnershipEnvironment is null)
            throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_ENVIRONMENT_REQUIRED");
        if(source.Pattern.DensityFieldId is null&&source.OwnershipEnvironment is not null)
            throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_ENVIRONMENT_UNUSED");
        source=MetadataJsonStorage.Snapshot(source);
        source=source with{Regions=source.Regions.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray(),Connections=source.Connections.OrderBy(c=>c?.Id,StringComparer.Ordinal).ToArray()};
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var region in source.Regions)
        {
            if(!WorldEnvironmentQuery.Id(region.Id)||!ids.Add(region.Id)||!WorldEnvironmentQuery.Text(region.Purpose)||!WorldEnvironmentQuery.Text(region.SourceRuleId)||
                !double.IsFinite(region.Weight)||region.Weight is <=0 or >4e14||!double.IsFinite(region.MinimumArea)||region.MinimumArea is <=0 or >4e14||
                !double.IsFinite(region.MinimumClearance)||region.MinimumClearance is <0 or >1e7||region.Profile is null||region.Recipe is null||region.Catalog is null||
                region.Recipe.Surface is not null||region.Recipe.Origin is not null||region.Recipe.Space?.Axes.Count!=2||region.Recipe.Space.Unit!=source.Surface.Unit||
                region.Recipe.Environment is null)
                throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_PROGRAM_INVALID");
            ValidateSpatialContext(region,source);
            if(region.PinnedAnchor is {} pin&&(!WorldEnvironmentQuery.Text(pin.Purpose)||!WorldEnvironmentQuery.Text(pin.SourceRuleId)||
                !double.IsFinite(pin.Position.X)||!double.IsFinite(pin.Position.Y)||Math.Abs(pin.Position.X)>1e7||Math.Abs(pin.Position.Y)>1e7))
                throw new ArgumentException("METADATA_REGIONAL_ANCHOR_PIN_INVALID:"+region.Id);
            if(region.Recipe.Placement is {} placement)
            {
                if(!WorldEnvironmentQuery.Id(region.PlacementAnchorId)||placement.Request?.Boundary is not null||placement.Request?.Anchors is null||
                    placement.Request.Anchors.Any(a=>a?.Id==region.PlacementAnchorId))throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_ANCHOR_CONFLICT");
            }
            else if(region.PlacementAnchorId is not null)throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_ANCHOR_UNUSED");
        }
        var budget=new WorldSpatialWorkBudget(source.MaximumWork,token,parent,"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        if(source.Surface.TerrainCapture is not null||source.Surface.TerrainCapturePlan is not null)
        {
            WorldSurfaceQuery.ChargeCaptureWork(source.Surface,budget,token);
            _=new WorldSurfaceQuery(source.Surface,token);
        }
        else if(source.Surface.HeightField is {} supplied)
        {
            budget.Charge(supplied.Heights?.Count??0);
            if(source.Surface.Terrain is {} terrain)budget.Charge(WorldTerrainGenerator.FullGenerationWork(terrain,token));
            // Validate every supplied sample, and replay a paired recipe if one
            // was declared, before generation can select or discard a region.
            _=new WorldSurfaceQuery(source.Surface,token);
        }
        var authored=source;
        var topology=source.Topology is null?null:new MetadataTopologyPlanner().PlanRegional(source,budget,token);
        if(topology is not null){authored=source with{Topology=topology.Definition};source=source with{Topology=null,Connections=topology.Connections};}
        source=source with{NetworkRules=MetadataNetworkPlanner.CanonicalRegionalRules(source.NetworkRules,source.Surface,
            source.Regions.Select(r=>r.Id).ToArray(),source.Connections.Select(c=>c?.Kind).ToArray(),token)};
        authored=authored with{NetworkRules=source.NetworkRules};
        var result=source.ProgramComposition is not null?PlanPrograms(source,budget,token):
            source.PhysicalFeedback is not null?PlanPhysical(source,budget,token):PlanContent(source,budget,token);
        if(topology is not null)
        {
            result=result with{TopologyPlan=topology,OwnershipPlan=result.OwnershipPlan! with{Source=authored}};
            MetadataJsonStorage.ValidateSize(result,token);
        }
        return result;
    }
    private MetadataRegionalWorldDefinition PlanContent(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token,
        IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<int>>>? excluded=null,IReadOnlyDictionary<string,int>? required=null,
        IReadOnlyList<WorldAreaPartitionChoice>? partitions=null)
    {
        if(source.Coupling is not null)return PlanCoupled(source,budget,token,excluded,required,partitions);
        var demands=MeasurePrograms(source,budget,token);
        return PlanMeasured(source,demands,budget,token,excluded,required,partitions);
    }
    private MetadataRegionalWorldDefinition PlanMeasured(MetadataRegionalOwnershipRequest source,IReadOnlyList<MetadataRegionalProgramDemand> demands,
        WorldSpatialWorkBudget budget,CancellationToken token,IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<int>>>? excluded=null,IReadOnlyDictionary<string,int>? required=null,
        IReadOnlyList<WorldAreaPartitionChoice>? partitions=null)
    {
        var effective=source.Regions.Select(r=>demands.SingleOrDefault(d=>d.RegionId==r.Id) is {} demand?r with{
            Weight=demand.Weight,MinimumArea=demand.RequiredArea,MinimumClearance=demand.RequiredClearance}:r).ToArray();
        var boundary=source.Surface.Boundary!;
        // The complete outer mask must be owned, including excluded water/voids.
        // This computational support is not added to the actual game surface.
        var support=new WorldSurfaceDefinition(1,"ownership-support",source.Surface.Unit,boundary,[],
            [new("ownership-support","ownership","Computational generation ownership",source.SourceRuleId,boundary,0,new(0,0),0,0,0)]);
        var environment=OwnershipEnvironment(source,support,budget,token);
        var kinds=environment.Surface.Definition.Patches.Select(p=>p.Kind).Concat(environment.Surface.HeightField is {} field?[field.Geometry.Kind]:Array.Empty<string>())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var profile=new WorldSuitabilityProfile("ownership","Complete outer ownership",source.SourceRuleId,kinds,[],[]);
        var rootRequest=new WorldRegionRequest("ownership-domain",source.Purpose,source.SourceRuleId,boundary,profile,[],source.Pattern.MaximumCells,source.MaximumWork,
            Intents:[new("ownership-domain",source.Purpose,source.SourceRuleId,effective.Min(r=>r.MinimumArea))],AnchorPrecision:source.AnchorPrecision,MaximumAnchorCells:source.MaximumAnchorCells,
            Representation:source.OwnershipEnvironment is null||WorldEnvironmentRegions.CoordinateFields(environment,source.Pattern.DensityFieldId!,budget) is not null?
                WorldRegionRepresentation.Membership:WorldRegionRepresentation.TerrainFaces);
        var measured=new WorldRegionPlanner().Plan(environment,rootRequest,budget,token);
        if(source.OwnershipEnvironment is not null&&(measured.IneligibleArea>Math.Max(1,measured.BoundaryArea)*1e-10||
            measured.UnclaimedSuitableArea>Math.Max(1,measured.BoundaryArea)*1e-10||measured.Regions.Count!=1))
            throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_DENSITY_DOMAIN_INCOMPLETE");
        var root=measured.Regions.Single();
        var qualification=new PartitionQualification(source,effective,budget,token);WorldAreaPopulationPlan partition;
        try
        {
            partition=WorldPopulationGeometry.AreaVerifiedRegion(root,environment,profile,source.Pattern,source.Regions.Count,budget,token,
                relativeWeights:effective.Select(r=>r.Weight).ToArray(),requirements:effective.Select(r=>new WorldAreaStratumRequirement(r.Id,r.MinimumArea,0)).ToArray(),qualify:qualification.Qualify,
                requiredPartitions:partitions,resweepTerrain:rootRequest.Representation==WorldRegionRepresentation.Membership,
                verifiedMembership:rootRequest.Representation==WorldRegionRepresentation.Membership);
        }
        catch(ArgumentException error)
        {
            var cause=PartitionCause(error);cause.Data["RegionalPartitionQualification"]=qualification.Snapshot();
            if(!ReferenceEquals(cause,error))cause.Data["RegionalPartitionFailure"]=error.Message;
            if(error.Data["AreaPartitionAttempts"] is {} attempts)cause.Data["RegionalPartitionAreaAttempts"]=attempts;
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cause).Throw();throw;
        }
        catch(OperationCanceledException error){error.Data["RegionalPartitionQualification"]=qualification.Snapshot();throw;}
        var partitionProof=qualification.Complete(partition);
        var regions=new List<MetadataGenerationRegion>();var anchors=new List<MetadataRegionalOwnershipAnchor>();
        for(int i=0;i<source.Regions.Count;i++)
        {
            var program=effective[i];var selected=qualification.Selection(i,partition.Strata[i]);var ownership=selected.Ownership;var anchor=selected.Plan;
            var rejected=excluded?.GetValueOrDefault(program.Id);int? component=required is not null&&required.TryGetValue(program.Id,out int selectedComponent)?selectedComponent:null;
            if(rejected is not null||component is not null)
            {
                var actual=WorldSurfaceQuery.CreateRegional(source.Surface,ownership,token).Definition;
                anchor=new MetadataRegionPlanner().Plan(PartitionQualification.Request(source,program,ownership,rejected,component),program.Recipe.Environment!,actual,program.Recipe.Space,budget,token);
            }
            var point=anchor.Regions.Single().Seed.Position;anchors.Add(new(program.Id,anchor));
            var placement=program.Recipe.Placement;
            if(placement is not null)placement=placement with{Request=placement.Request with{Boundary=ownership,Anchors=placement.Request.Anchors.Append(new(program.PlacementAnchorId!,point)).ToArray()}};
            var recipe=BindSpatialContext(program.Recipe with{Origin=[point.X,point.Y],Placement=placement},program.SpatialContext,ownership,budget);
            regions.Add(new(program.Id,program.Purpose,program.SourceRuleId,ownership,ownership,recipe,program.Catalog));
        }
        var result=new MetadataRegionalWorldDefinition(1,source.Id,source.Purpose,source.SourceRuleId,source.Surface,regions.ToArray(),source.Connections,
            source.MaximumTotalNodes,source.MaximumTotalConnections,source.MaximumReferences,source.MaximumWork,
            new(1,source,partition,anchors.ToArray(),budget.Used,demands.Count==0?null:demands,PartitionQualification:partitionProof),source.Id,NetworkRules:source.NetworkRules);
        MetadataJsonStorage.ValidateSize(result,token);return result;
    }
}
