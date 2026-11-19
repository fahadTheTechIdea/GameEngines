using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataGenerationRegion(string Id,string Purpose,string SourceRuleId,WorldPolygon Ownership,
    WorldPolygon GenerationDomain,WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog);
public sealed record MetadataRegionalNodeSelector(string NodeKind,string? ArchetypeId=null);
public sealed record MetadataRegionalEndpoint(string RegionId,string NodeId,string? PortId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalNodeSelector? Selector=null);
public sealed record MetadataRegionalConnection(string Id,string Kind,string Purpose,string SourceRuleId,
    MetadataRegionalEndpoint From,MetadataRegionalEndpoint To,string OwnerRegionId,WorldPolygon RoutingDomain,
    WorldTraversalProfile Profile,WorldRouteSearch Search,bool Bidirectional=false);
public sealed record MetadataRegionalWorldDefinition(int Version,string Id,string Purpose,string SourceRuleId,WorldSurfaceDefinition Surface,
    IReadOnlyList<MetadataGenerationRegion> Regions,IReadOnlyList<MetadataRegionalConnection> Connections,
    int MaximumTotalNodes=50000,int MaximumTotalConnections=100000,int MaximumReferences=200000,long MaximumWork=20000000,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalOwnershipPlan? OwnershipPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? OwnershipDerivationId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalTopologyPlan? TopologyPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalNetworkRule>? NetworkRules=null);
public sealed record MetadataGeneratedRegion(int Version,string RegionId,string ContextSha256,string ImplementationId,
    WorldMetadataDocument World,IReadOnlyList<MetadataGenerationDependency> Dependencies);
public sealed record MetadataRegionalMember(MetadataSpatialMemberKind Kind,string Id,string OwnerRegionId,IReadOnlyList<string> RegionIds);
public sealed record MetadataRegionalRoute(MetadataRegionalConnection Definition,WorldPoint From,WorldPoint To,WorldRouteResult Forward,WorldRouteResult? Reverse);
public sealed record MetadataRegionalWorldDocument(int Version,MetadataRegionalWorldDefinition Definition,string DefinitionSha256,
    IReadOnlyList<MetadataGeneratedRegion> Regions,IReadOnlyList<MetadataRegionalMember> Members,IReadOnlyList<MetadataRegionalRoute> Connections,long Work,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalNetworkPlan? NetworkPlan=null);

/// <summary>Generates complete game-authored programs independently in one shared coordinate space.
/// Generation ownership is semantic and polygonal; storage chunks do not determine layout.</summary>
public sealed partial class MetadataRegionalWorldGenerator(WorldMetadataGenerator? generator=null)
{
    private readonly WorldMetadataGenerator generator=generator??new();
    private static string Implementation=>typeof(MetadataRegionalWorldGenerator).Module.ModuleVersionId.ToString("D");
    private static bool Equal<T>(T a,T b)=>MetadataPopulationSources.Equal(a,b);
    private static WorldSpatialWorkBudget Budget(MetadataRegionalWorldDefinition definition,CancellationToken token)
        =>new(definition.MaximumWork,token,failureCode:"METADATA_REGIONAL_WORK_BUDGET");

    public MetadataGeneratedRegion GenerateRegion(MetadataRegionalWorldDefinition definition,string regionId,CancellationToken token=default)
    {
        var prepared=Prepare(definition,token);
        return GeneratePrepared(prepared,prepared.Regions.SingleOrDefault(r=>r.Id==regionId)??throw new ArgumentException("METADATA_REGIONAL_REGION_REQUIRED"),token);
    }
    public MetadataRegionalWorldDocument Generate(MetadataRegionalWorldDefinition definition,CancellationToken token=default)
    {
        var prepared=Prepare(definition,token,out long preparationWork);
        var regions=Collect(prepared,prepared.Regions.Select(r=>GeneratePrepared(prepared,r,token)),token);
        return AssemblePrepared(prepared,regions,token,preparationWork);
    }
    public MetadataGeneratedRegion ReplayRegion(MetadataRegionalWorldDefinition definition,MetadataGeneratedRegion packet,CancellationToken token=default)
    {
        var prepared=Prepare(definition,token);return ReplayPrepared(prepared,packet,token);
    }
    public MetadataRegionalWorldDocument Assemble(MetadataRegionalWorldDefinition definition,IReadOnlyList<MetadataGeneratedRegion> packets,CancellationToken token=default)
    {
        var prepared=Prepare(definition,token,out long preparationWork);
        if(packets is null||packets.Count!=prepared.Regions.Count||packets.Any(p=>p is null)||packets.Select(p=>p.RegionId).Distinct(StringComparer.Ordinal).Count()!=packets.Count)
            throw new ArgumentException("METADATA_REGIONAL_PACKETS_REQUIRED");
        return AssemblePrepared(prepared,Collect(prepared,packets.OrderBy(p=>p.RegionId,StringComparer.Ordinal).Select(p=>ReplayPrepared(prepared,p,token)),token),token,preparationWork);
    }
    private MetadataGeneratedRegion ReplayPrepared(MetadataRegionalWorldDefinition definition,MetadataGeneratedRegion packet,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var region=packet is null?null:definition.Regions.SingleOrDefault(r=>r.Id==packet.RegionId);
        if(region is null||packet!.Version!=1||packet.ImplementationId!=Implementation||packet.ContextSha256!=Context(definition,region,token))
            throw new ArgumentException("METADATA_REGIONAL_PACKET_SOURCE_MISMATCH");
        var expected=GeneratePrepared(definition,region,token);
        if(!Equal(expected,packet))throw new ArgumentException("METADATA_REGIONAL_PACKET_REPLAY_MISMATCH");
        return expected;
    }
    private static string Context(MetadataRegionalWorldDefinition definition,MetadataGenerationRegion region,CancellationToken token)
    {
        // Use the same validated, closed regional source projection as actual
        // generation. A remote patch cannot change this program's observable
        // surface. Height-recipe edits remain deliberately global dependencies.
        var authored=new WorldSurfaceQuery(definition.Surface.TerrainCapture is null?definition.Surface with{Terrain=null}:definition.Surface,token)
            .RestrictTo(region.GenerationDomain,token).Definition;
        token.ThrowIfCancellationRequested();
        return MetadataExecutionCache.Hash(new{definition.Id,WorldBoundary=definition.Surface.Boundary,
            Surface=authored with{Terrain=definition.Surface.Terrain},Region=region,
            Ownership=definition.Regions.Select(r=>new{r.Id,r.Ownership,r.GenerationDomain}).ToArray()});
    }
    internal static WorldMetadataRecipe EffectiveRecipe(MetadataRegionalWorldDefinition definition,MetadataGenerationRegion region,CancellationToken token)
    {
        var surface=WorldSurfaceQuery.CreateRegional(definition.Surface,region.GenerationDomain,token).Definition;
        // Persist exact requested samples through the normal surface owner. The
        // global source remains one input; no child-authored override is accepted.
        if(surface.Terrain is not null)surface=surface with{HeightField=null,TerrainCapture=new(1,"region-terrain",region.Purpose,region.SourceRuleId),TerrainCapturePlan=null};
        return region.Recipe with{Surface=surface};
    }
    private MetadataGeneratedRegion GeneratePrepared(MetadataRegionalWorldDefinition definition,MetadataGenerationRegion region,CancellationToken token,MetadataExecutionCache? cache=null)
    {
        token.ThrowIfCancellationRequested();var recipe=EffectiveRecipe(definition,region,token);
        var dependencies=generator.Dependencies(recipe,region.Catalog,token);
        var world=cache is null?generator.Generate(recipe,region.Catalog,token):generator.GenerateCore(recipe,region.Catalog,cache,token);
        if(definition.OwnershipPlan?.ProgramDemands?.SingleOrDefault(d=>d.RegionId==region.Id) is {} demand)
        {
            var analysis=demand.Analysis;
            var sourceLinks=analysis.Connections.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
            if(analysis.RegionSelectionDeferred||!Equal(world.Nodes.Select(n=>n with{Position=null}).ToArray(),analysis.Nodes.Select(n=>n with{Position=null}).ToArray())||
                !Equal(world.Connections.Where(c=>sourceLinks.Contains(c.Id)).ToArray(),analysis.Connections)||!Equal(world.FootprintDefinitions,analysis.FootprintDefinitions)||
                !Equal(world.StructureDefinitions,analysis.StructureDefinitions)||!Equal(world.CapacityPlan,analysis.CapacityPlan)||
                !Equal(world.VariationPlan,analysis.VariationPlan)||!Equal(world.ProgramMixPlan,analysis.ProgramMixPlan)||!Equal(dependencies,analysis.Dependencies)||
                !MetadataTerrainDemandPlanner.MatchesAnalysis(analysis.TerrainAnalysis,world)||!WorldMetadataGenerator.MatchesRegionAnalysis(analysis.RegionAnalysis,world))
                throw new ArgumentException("METADATA_REGIONAL_PROGRAM_ANALYSIS_MISMATCH:"+region.Id);
        }
        if(world.Nodes.Count>definition.MaximumTotalNodes||(long)world.Connections.Count+definition.Connections.Count>definition.MaximumTotalConnections)
            throw new ArgumentException("METADATA_REGIONAL_CONTENT_BUDGET");
        if(!Equal(dependencies,generator.Dependencies(recipe,region.Catalog,token)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        if(world.Surface is null||!Equal(world.Surface.Terrain,recipe.Surface!.Terrain)||!Equal(world.Surface.HeightField,recipe.Surface.HeightField)||
            !Equal(world.Surface.Patches,recipe.Surface.Patches)||!Equal(world.Surface.Exclusions,recipe.Surface.Exclusions))
            throw new ArgumentException("METADATA_REGIONAL_SHARED_SURFACE_CHANGED");
        return new(1,region.Id,Context(definition,region,token),Implementation,world,dependencies);
    }
    private static IReadOnlyList<MetadataGeneratedRegion> Collect(MetadataRegionalWorldDefinition definition,IEnumerable<MetadataGeneratedRegion> generated,CancellationToken token)
    {
        var accepted=new List<MetadataGeneratedRegion>();long nodes=0,connections=definition.Connections.Count;
        foreach(var packet in generated)
        {
            token.ThrowIfCancellationRequested();nodes+=packet.World.Nodes.Count;connections+=packet.World.Connections.Count;
            if(nodes>definition.MaximumTotalNodes||connections>definition.MaximumTotalConnections)throw new ArgumentException("METADATA_REGIONAL_CONTENT_BUDGET");
            accepted.Add(packet);
        }
        return accepted;
    }
    private void ValidateDependencies(MetadataRegionalWorldDefinition definition,MetadataGeneratedRegion packet,CancellationToken token)
    {
        var source=definition.Regions.Single(r=>r.Id==packet.RegionId);
        // Surface inheritance does not change dependency selection. Recheck every
        // completed program after assembly, including a provider changed while
        // a different region was being generated or replayed.
        if(!Equal(packet.Dependencies,generator.Dependencies(source.Recipe,source.Catalog,token)))
            throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
    }
    internal MetadataRegionalWorldDefinition Prepare(MetadataRegionalWorldDefinition definition,CancellationToken token)
        =>Prepare(definition,token,out _);
    private MetadataRegionalWorldDefinition Prepare(MetadataRegionalWorldDefinition definition,CancellationToken token,out long preparationWork,bool replayOwnership=true)
    {
        token.ThrowIfCancellationRequested();
        if(definition is null||definition.Version!=1||!WorldEnvironmentQuery.Id(definition.Id)||!WorldEnvironmentQuery.Text(definition.Purpose)||!WorldEnvironmentQuery.Text(definition.SourceRuleId)||
            definition.Surface?.Boundary is null||
            definition.Regions is not {Count:>=2 and <=16}||definition.Connections is null||definition.Connections.Count>128||
            definition.MaximumTotalNodes is <1 or >100000||definition.MaximumTotalConnections is <1 or >200000||definition.MaximumReferences is <1 or >1000000||definition.MaximumWork is <1 or >200000000)
            throw new ArgumentException("METADATA_REGIONAL_DEFINITION_INVALID");
        var budget=Budget(definition,token);
        if(definition.Surface.TerrainCapture is not null||definition.Surface.TerrainCapturePlan is not null)
            WorldSurfaceQuery.ChargeCaptureWork(definition.Surface,budget,token);
        else if(definition.Surface.HeightField is {} supplied)
        {
            budget.Charge(supplied.Heights?.Count??0);
            if(definition.Surface.Terrain is {} terrain)budget.Charge(WorldTerrainGenerator.FullGenerationWork(terrain,token));
        }
        // Validate all authored masks before any spatial filtering or generation.
        var authoredSurface=new WorldSurfaceQuery(definition.Surface.HeightField is null&&definition.Surface.TerrainCapture is null?
            definition.Surface with{Terrain=null}:definition.Surface,token);
        definition=MetadataExecutionCache.Clone(definition);
        if(JsonSerializer.SerializeToUtf8Bytes(definition,WorldJson.Shared).Length>MetadataJsonStorage.MaximumFileBytes)throw new ArgumentException("METADATA_REGIONAL_SOURCE_SIZE_LIMIT");
        definition=definition with{Regions=definition.Regions.OrderBy(r=>r?.Id,StringComparer.Ordinal).ToArray(),Connections=definition.Connections.OrderBy(c=>c?.Id,StringComparer.Ordinal).ToArray()};
        definition=definition with{NetworkRules=MetadataNetworkPlanner.CanonicalRegionalRules(definition.NetworkRules,definition.Surface,
            definition.Regions.Select(r=>r?.Id).ToArray(),definition.Connections.Select(c=>c?.Kind).ToArray(),token)};
        if((definition.OwnershipPlan?.Source?.Topology is null)!=(definition.TopologyPlan is null))
            throw new ArgumentException("METADATA_REGIONAL_TOPOLOGY_PLAN_REQUIRED_OR_UNEXPECTED");
        if(definition.OwnershipPlan is {} derivation)
        {
            if(derivation.Version!=1||derivation.Source is null||definition.OwnershipDerivationId!=derivation.Source.Id)
                throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_PLAN_INVALID");
            if(replayOwnership)
            {
                var expected=new MetadataRegionalOwnershipPlanner(generator).Plan(derivation.Source,budget,token);
                if(!Equal(expected,definition))throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
            }
        }
        else if(definition.OwnershipDerivationId is not null)throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_PLAN_REQUIRED");
        var world=Polygon(definition.Surface.Boundary!);var ids=new HashSet<string>(StringComparer.Ordinal);double area=0;
        foreach(var region in definition.Regions)
        {
            token.ThrowIfCancellationRequested();
            if(region is null||!WorldEnvironmentQuery.Id(region.Id)||!ids.Add(region.Id)||!WorldEnvironmentQuery.Text(region.Purpose)||!WorldEnvironmentQuery.Text(region.SourceRuleId)||
                region.Recipe is null||region.Catalog is null||region.Recipe.Surface is not null||region.Recipe.Space?.Axes.Count!=2||region.Recipe.Space.Unit!=definition.Surface.Unit)
                throw new ArgumentException("METADATA_REGIONAL_REGION_INVALID");
            var ownership=Polygon(region.Ownership);var domain=Polygon(region.GenerationDomain);
            budget.Charge(4L*(world.Vertices.Count+ownership.Vertices.Count)*domain.Vertices.Count);
            if(!world.ContainsPolygon(domain)||!domain.ContainsPolygon(ownership))throw new ArgumentException("METADATA_REGIONAL_DOMAIN_CONTAINMENT");
            area+=Math.Abs(ownership.SignedArea());
            // Metadata schema checks do not expand an unrequested region.
            generator.DescribeParameters(region.Recipe,region.Catalog,token);
        }
        for(int i=0;i<definition.Regions.Count;i++)for(int j=0;j<i;j++)
            if(Intersection(definition.Regions[i].Ownership,definition.Regions[j].Ownership,budget)>1e-8)throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_OVERLAP");
        if(Math.Abs(area-Math.Abs(world.SignedArea()))>Math.Max(1,Math.Abs(world.SignedArea()))*1e-10)throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_GAP");
        var connectionIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var connection in definition.Connections)
        {
            if(connection is null||!WorldEnvironmentQuery.Id(connection.Id)||!connectionIds.Add(connection.Id)||!WorldEnvironmentQuery.Id(connection.Kind)||
                !WorldEnvironmentQuery.Text(connection.Purpose)||!WorldEnvironmentQuery.Text(connection.SourceRuleId)||connection.From is null||connection.To is null||connection.From.RegionId==connection.To.RegionId||
                !ids.Contains(connection.OwnerRegionId)||connection.Profile is null||connection.Search is null)throw new ArgumentException("METADATA_REGIONAL_CONNECTION_INVALID");
            foreach(var endpoint in new[]{connection.From,connection.To})
                if(!ids.Contains(endpoint.RegionId)||!WorldEnvironmentQuery.Text(endpoint.NodeId)||endpoint.Selector is not null||endpoint.PortId is not null&&!WorldEnvironmentQuery.Id(endpoint.PortId))throw new ArgumentException("METADATA_REGIONAL_ENDPOINT_INVALID");
            var domain=Polygon(connection.RoutingDomain);budget.Charge(4L*world.Vertices.Count*domain.Vertices.Count);
            if(!world.ContainsPolygon(domain))throw new ArgumentException("METADATA_REGIONAL_ROUTE_DOMAIN_OUTSIDE");
            WorldSurfaceRoutePlanner.ValidateRequest(new(connection.Id,connection.Purpose,connection.SourceRuleId,new(0,0),new(1,1),connection.Profile,connection.Search));
            _=new WorldSurfaceTraversal(authoredSurface,connection.Profile,token);
        }
        preparationWork=budget.Used;return definition;
    }
    private static WorldPolygon Polygon(WorldPolygon? polygon)
    {
        if(polygon?.Vertices is not {Count:>=3 and <=64}||polygon.Vertices.Any(p=>!WorldHeightFieldQuery.Point(p)))throw new ArgumentException("METADATA_REGIONAL_POLYGON_INVALID");
        polygon.Validate();return polygon;
    }
    private static double Intersection(WorldPolygon a,WorldPolygon b,WorldSpatialWorkBudget budget)
    {
        budget.Charge((long)a.Vertices.Count*a.Vertices.Count*a.Vertices.Count+(long)b.Vertices.Count*b.Vertices.Count*b.Vertices.Count);
        var aa=LandscapePolygonTriangulator.Triangulate(a);var bb=LandscapePolygonTriangulator.Triangulate(b);double area=0;
        foreach(var first in aa)foreach(var second in bb){budget.Charge(64);area+=TerrainCoverageTopology.IntersectionArea(first,second);}
        return area;
    }
}
