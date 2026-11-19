using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Expands reusable game-defined programs into world metadata. No image, biome,
/// asset registry, raster, renderer or game engine is required. No prompt text is executed.</summary>
public sealed partial class WorldMetadataGenerator : IWorldMetadataGenerator
{
    private readonly ISettlementCatalogRegistry? structureCatalogs;
    private readonly IReadOnlyDictionary<string,IMetadataLayout> layouts;
    public WorldMetadataGenerator(ISettlementCatalogRegistry? structureCatalogs=null,IEnumerable<IMetadataLayout>? layouts=null)
    {
        this.structureCatalogs=structureCatalogs;
        var registry=new Dictionary<string,IMetadataLayout>(StringComparer.Ordinal);
        foreach(var layout in layouts??[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),new MetadataSpiralLayout()])
            if(layout is null||!SettlementCatalogRegistry.ValidId(layout.Id)||!registry.TryAdd(layout.Id,layout))
                throw new ArgumentException("METADATA_LAYOUT_REGISTRATION_INVALID");
        this.layouts=registry;
    }

    private IReadOnlyList<IReadOnlyList<double>> Plan(MetadataLayout layout,int dimensions,int count,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(layout.StrategyId is null||!layouts.TryGetValue(layout.StrategyId,out var strategy))
            throw new ArgumentException("METADATA_LAYOUT_UNKNOWN:"+layout.StrategyId);
        var positions=strategy.Plan(layout.Parameters,dimensions,count,token);
        if(positions is null||positions.Count!=count||positions.Any(p=>p is null||p.Count!=dimensions||p.Any(v=>!double.IsFinite(v))))
            throw new ArgumentException("METADATA_LAYOUT_RESULT_INVALID:"+layout.StrategyId);
        return positions;
    }

    public WorldMetadataDocument Generate(WorldMetadataRecipe recipe, WorldMetadataCatalog catalog, CancellationToken token = default)
    {
        var input=CaptureInputs(recipe,catalog,token);
        return GenerateCore(input.Recipe,input.Catalog,null,token);
    }

    internal WorldMetadataDocument GenerateCore(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,MetadataExecutionCache? execution,CancellationToken token)
    {
        if(recipe.Regions?.ExcludedAssignments is {Count:>0})throw new ArgumentException("METADATA_REGION_EXCLUSIONS_INTERNAL");
        if(recipe.ProgramComposition is null)return GenerateAttemptCore(recipe,catalog,execution,token,null);
        token.ThrowIfCancellationRequested();
        Validate(recipe,catalog,token);
        recipe=MetadataExecutionCache.Clone(recipe);catalog=MetadataExecutionCache.Clone(catalog);
        var dependencies=Dependencies(recipe,catalog,token);execution?.CaptureDependencies(dependencies);
        WorldMetadataDocument Compose()=>new MetadataProgramMixPlanner().Compose(recipe,catalog,dependencies,
            cursor=>GenerateAttemptCore(recipe,catalog,null,token,cursor),token);
        var world=execution is null?Compose():execution.Run("composition",new{recipe,catalog,DependencyIdentities=execution.Dependencies},[],Compose,token);
        if(!MetadataPopulationSources.Equal(dependencies,Dependencies(recipe,catalog,token)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        return world;
    }

    private WorldMetadataDocument GenerateAttemptCore(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,MetadataExecutionCache? execution,CancellationToken token,MetadataProgramMixPlanner.ChoiceCursor? programChoices,
        WorldMetadataDocument? preparedSource=null)
    {
        token.ThrowIfCancellationRequested();
        if(recipe.PopulationSource is not null&&preparedSource is null)
            return GenerateCoordinatedPopulation(recipe,catalog,execution,token,programChoices);
        var authoredRecipe=recipe;
        execution?.CaptureDependencies(Dependencies(recipe,catalog,token));
        var types = Validate(recipe, catalog, token);
        ValidateTerrainInputDependencies(recipe);
        if(recipe.RouteBudget is not null&&recipe.RouteRules is null)throw new ArgumentException("METADATA_ROUTE_BUDGET_UNUSED");
        T Stage<T>(string id,object inputs,string[] dependencies,Func<T> run,Func<T,object?>? fingerprint=null)
            =>execution is null?run():execution.Run(id,inputs,dependencies,run,token,fingerprint);
        if(recipe.PopulationSource is {} populationSource)
        {
            if(populationSource.Recipe is null||populationSource.Catalog is null||populationSource.Recipe.PopulationSource is not null||
                populationSource.Recipe.Placement?.SourceWorld is not null||recipe.Placement is null||recipe.Placement.SourceWorld is not null)
                throw new ArgumentException("METADATA_POPULATION_SOURCE_LEVEL_INVALID");
            var source=Stage("population-source",new{populationSource,preparedSource,DependencyIdentities=execution?.Dependencies},[],
                ()=>preparedSource??throw new ArgumentException("METADATA_POPULATION_SOURCE_REQUIRED"));
            recipe=recipe with{Placement=recipe.Placement with{SourceWorld=source},LocalLayouts=MetadataPopulationSources.InheritAdaptedLayouts(recipe,source),Regions=populationSource.Context?.UsePlacementBoundary==true?
                source.RegionPlan?.Request??throw new ArgumentException("METADATA_POPULATION_CONTEXT_HABITAT_REQUIRED"):recipe.Regions};
        }
        var terrainDemandPlan=recipe.TerrainDemands is {} demandDefinition?Stage("terrain-demand",new{demandDefinition,Source=recipe.Placement?.SourceWorld},
            recipe.PopulationSource is null?[]:["population-source"],()=>new MetadataTerrainDemandPlanner().Plan(demandDefinition,
                recipe.Placement?.SourceWorld??throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED"),token)):null;
        var terrainInputs=PrepareTerrainInputs(recipe,execution,token);recipe=terrainInputs.Recipe;
        var surface=terrainInputs.Surface;var drainagePlan=terrainInputs.Drainage;var waterPlan=terrainInputs.Water;var windPlan=terrainInputs.Wind;
        string surfaceStage=terrainInputs.SurfaceStage;string[] environmentDependencies=terrainInputs.EnvironmentDependencies;
        var inheritedPrograms=recipe.Placement?.SourceWorld?.ProgramMixPlan;
        var content=Stage("content",new{catalog,recipe.Id,recipe.RootArchetypeId,recipe.Parameters,recipe.Space,recipe.Origin,
            recipe.RequireAllPositions,recipe.MaximumNodes,recipe.MaximumConnections,recipe.AdditionalConnections,HasPlacement=recipe.Placement is not null,
            DependencyIdentities=execution?.Dependencies,SourcePrograms=inheritedPrograms},
            (terrainDemandPlan is null?Array.Empty<string>():["terrain-demand"]).Concat(inheritedPrograms is null||recipe.PopulationSource is null?Array.Empty<string>():["population-source"]).ToArray(),
            ()=>ExpandProgramContent(recipe,types,terrainDemandPlan,programChoices,token,sourcePrograms:inheritedPrograms));
        var nodes=content.Nodes.ToList();var connections=content.Connections.ToList();
        var deferredCoordinates=content.DeferredCoordinates.ToList();
        var footprintDefinitions=new SortedDictionary<string,MetadataCatalogFootprint>(content.Footprints.ToDictionary(p=>p.Key,p=>p.Value),StringComparer.Ordinal);
        var structures=new SortedDictionary<string,MetadataStructureDefinition>(content.Structures.ToDictionary(p=>p.Key,p=>p.Value),StringComparer.Ordinal);
        var capacityPlan=content.Capacity;
        Stage("source-positions",new{recipe.Space},[surfaceStage,"content"],()=>{surface?.ValidateNodes(nodes,token);return true;});
        var accessDefinitions=nodes.Select(n=>types[n.ArchetypeId]).Where(a=>a.Access is not null).DistinctBy(a=>a.Id)
            .ToDictionary(a=>a.Id,a=>a.Access!,StringComparer.Ordinal);
        var topologyPlan=recipe.TopologyRules is not null||recipe.TopologyIntents is not null?Stage("topology",new{recipe.Id,recipe.TopologyRules,recipe.TopologyIntents,recipe.MaximumConnections},["content"],()=>{
            var rules=MetadataTopologyIntentResolver.Resolve(recipe.TopologyRules,recipe.TopologyIntents,nodes,token);
            return new MetadataTopologyPlanner().Plan(recipe.Id,rules,nodes,connections,token);
        }):null;
        if(topologyPlan is not null)
        {
            var links=topologyPlan.Results.SelectMany(r=>r.Connections).ToArray();
            if((long)connections.Count+links.Length>recipe.MaximumConnections)throw new ArgumentException("METADATA_CONNECTION_BUDGET");
            connections.AddRange(links);
        }
        string[] topologyDependencies=topologyPlan is null?[]:["topology"];
        var serviceProfiles=nodes.Select(n=>types[n.ArchetypeId]).Where(a=>a.Services is not null).DistinctBy(a=>a.Id).ToDictionary(a=>a.Id,a=>a.Services!,StringComparer.Ordinal);
        if(serviceProfiles.Count>0&&recipe.Services is null)throw new ArgumentException("METADATA_SERVICES_DECLARATION_REQUIRED");
        MetadataInteriorPlanner.ValidateRouteOwnership(recipe.Interiors,recipe.RouteRules,connections);
        var feedback=MetadataPlacementPlanner.Feedback(accessDefinitions,recipe.RouteRules,recipe.NetworkRules,connections,recipe.Services,serviceProfiles,recipe.Context,recipe.Corridors,MetadataPlacementPlanner.RequiresConnectedPrograms(recipe.Placement),recipe.RouteBudget,recipe.Interiors);
        if(feedback is not null&&execution is not null)feedback=feedback with{RuntimeNetworkMemo=execution.NetworkMemo,RuntimeRouteMemo=execution.RouteMemo};
        var regionDemandPlan=recipe.RegionDemands is {} regionDemands?Stage("region-demands",new{regionDemands,recipe.Regions,recipe.Placement,recipe.Space},["content"],
            ()=>new MetadataRegionPlanner().Measure(regionDemands,recipe.Regions??throw new ArgumentException("METADATA_REGION_DEMAND_REGIONS_REQUIRED"),
                recipe.Placement??throw new ArgumentException("METADATA_REGION_DEMAND_PLACEMENT_REQUIRED"),nodes,recipe.Space,footprintDefinitions,structures,accessDefinitions,token)):null;
        if(regionDemandPlan is not null)programChoices?.ChargeRegionPreparation(regionDemandPlan.Work);
        var regionRequest=regionDemandPlan?.DerivedRequest??recipe.Regions;
        // A context-bound consumer inherits the source's qualified habitat.
        // It cannot introduce another independent region choice after counts
        // and source geometry were derived from that exact habitat.
        var inheritedRegion=MetadataPopulationSources.InheritComposedRegion(recipe,regionRequest,recipe.Placement?.SourceWorld);
        var regionPlan=Stage("regions",new{Regions=regionRequest,recipe.Environment,recipe.Space,InheritedRegion=inheritedRegion},new[]{surfaceStage}.Concat(environmentDependencies)
            .Concat(regionDemandPlan is null?Array.Empty<string>():["region-demands"]).Concat(inheritedRegion is null?Array.Empty<string>():["population-source"]).ToArray(),()=>SelectProgramRegions(regionRequest,
                recipe.Environment,surface?.Query.Definition,recipe.Space,programChoices,regionDemandPlan is not null,inheritedRegion,token));
        var allocation=Stage("allocation",new{recipe.LocalLayouts,recipe.LayoutAdaptation,recipe.Placement,recipe.Environment,
            recipe.Space,recipe.RequireAllPositions,Feedback=feedback},(recipe.PopulationSource is null?new[]{surfaceStage,"source-positions","content","regions"}:new[]{surfaceStage,"source-positions","content","regions","population-source"}).Concat(topologyDependencies).ToArray(),()=>{
        var localLayoutPlan=recipe.LayoutAdaptation is null&&recipe.LocalLayouts is {} localLayouts?new MetadataLocalLayoutPlanner().Plan(localLayouts,
            regionPlan??throw new ArgumentException("METADATA_LOCAL_LAYOUT_REGIONS_REQUIRED"),recipe.Environment!,surface!.Query.Definition,recipe.Space,token):null;
        MetadataPlacementPlan? placementPlan=null;
        MetadataLayoutAdaptationPlan? adaptationPlan=null;
        if(footprintDefinitions.Count>0&&recipe.Placement is null)throw new ArgumentException("METADATA_PHYSICAL_ALLOCATION_REQUIRED");
        if(recipe.LayoutAdaptation is {} adaptation)
        {
            if(recipe.LocalLayouts is null||recipe.Placement is null||regionPlan is null||recipe.Environment is null||surface is null)
                throw new ArgumentException("METADATA_LAYOUT_ADAPTATION_DEPENDENCIES_REQUIRED");
            var result=new MetadataLocalLayoutPlanner(structureCatalogs,execution?.PlacementMemo,recipe.Id).PlanAndPlace(recipe.LocalLayouts,adaptation,recipe.Placement,regionPlan,
                recipe.Environment,surface.Query.Definition,recipe.Space,nodes,deferredCoordinates,footprintDefinitions,structures,feedback,token);
            localLayoutPlan=result.Layout;placementPlan=result.Placement;adaptationPlan=result.Adaptation;
        }
        else if(recipe.Placement is {} placement)
            placementPlan=new MetadataPlacementPlanner(structureCatalogs,execution?.PlacementMemo,recipe.Id).Apply(placement,recipe.Environment??throw new ArgumentException("METADATA_PLACEMENT_ENVIRONMENT_REQUIRED"),
                surface?.Query.Definition??throw new ArgumentException("METADATA_PLACEMENT_SURFACE_REQUIRED"),recipe.Space,nodes,deferredCoordinates,token,footprintDefinitions,structures,feedback,regionPlan,localLayoutPlan);
        if(recipe.RequireAllPositions&&nodes.Any(n=>n.Position is null))throw new ArgumentException("METADATA_POSITION_REQUIRED:"+nodes.First(n=>n.Position is null).Id);
        return new MetadataAllocatedContent(nodes.ToArray(),placementPlan,localLayoutPlan,adaptationPlan);
        });
        nodes=allocation.Nodes.ToList();var placementPlan=allocation.Placement;var localLayoutPlan=allocation.LocalLayout;var adaptationPlan=allocation.Adaptation;
        if(execution is not null)execution.PlacementMemo.Complete(execution.Executions.Single(s=>s.Id=="allocation").Reused,token);
        var accessPlan=Stage("access",new{accessDefinitions},[surfaceStage,"content","allocation"],()=>accessDefinitions.Count==0?null:new MetadataPhysicalAccessPlanner().Plan(accessDefinitions,nodes,placementPlan,
            surface?.Query.Definition??throw new ArgumentException("METADATA_ACCESS_SURFACE_REQUIRED"),structures,token));
        if(recipe.Environment is null && recipe.SuitabilityRules is not null)throw new ArgumentException("METADATA_ENVIRONMENT_REQUIRED");
        var environmentPlan=Stage("environment",new{recipe.Environment,recipe.SuitabilityRules,recipe.Space},[surfaceStage,"allocation"],()=>recipe.Environment is {} environment ? new MetadataEnvironmentPlanner().Plan(environment,recipe.SuitabilityRules??[],
            surface?.Query.Definition??throw new ArgumentException("METADATA_ENVIRONMENT_SURFACE_REQUIRED"),recipe.Space,nodes,token) with{WindMoistureId=windPlan?.Derivation.Definition.Id}:null);
        var interiorPlan=Stage("interiors",new{recipe.Interiors,Preserved=recipe.Placement?.SourceWorld?.InteriorPlan},new[]{surfaceStage,"content","allocation","access"}.Concat(topologyDependencies).ToArray(),()=>recipe.Interiors is {} interiors?new MetadataInteriorPlanner().Plan(interiors,surface?.Query.Definition??throw new ArgumentException("METADATA_INTERIOR_SURFACE_REQUIRED"),nodes,connections,accessPlan,null,token,recipe.Placement?.SourceWorld?.InteriorPlan):null);
        var travelSurface=surface is null?null:MetadataInteriorPlanner.WithWalls(surface.Query.Definition,interiorPlan);
        var networkPlan=Stage("networks",new{recipe.Id,recipe.NetworkRules,recipe.RouteRules,recipe.Space,recipe.MaximumConnections},new[]{surfaceStage,"content","allocation","access","interiors"}.Concat(topologyDependencies).ToArray(),()=>{
        MetadataNetworkPlan? networkPlan=null;
        if(recipe.NetworkRules is {} networkRules)
        {
            if(recipe.RouteRules?.Any(r=>networkRules.Any(n=>n is not null && n.ConnectionKind==r.ConnectionKind))==true)
                throw new ArgumentException("METADATA_NETWORK_ROUTE_KIND_CONFLICT");
            var planner=new MetadataNetworkPlanner();
            networkPlan=planner.Plan(recipe.Id,networkRules,travelSurface??throw new ArgumentException("METADATA_NETWORK_SURFACE_REQUIRED"),
                recipe.Space,nodes,connections,token,accessPlan,null,placementPlan?.Definition.SourceWorld?.NetworkPlan,execution?.NetworkMemo,"world-network");
        }
        return networkPlan;
        });
        execution?.NetworkMemo.Complete(recipe.Id,networkPlan,token);
        if(networkPlan is not null)
        {
            var generated=new MetadataNetworkPlanner().Connections(networkPlan);
            if(connections.Count+generated.Count>recipe.MaximumConnections)throw new ArgumentException("METADATA_CONNECTION_BUDGET");
            connections.AddRange(generated);
        }
        var progressionRules=(recipe.ProgressionRules??[]).Concat(topologyPlan is null?[]:new MetadataTopologyPlanner().ProgressionRules(topologyPlan)).ToArray();
        var progressionPlan=Stage("progression",new{ProgressionRules=progressionRules},new[]{"content","allocation","networks"}.Concat(topologyDependencies).ToArray(),()=>progressionRules.Length>0?new MetadataProgressionPlanner().Plan(progressionRules,nodes,connections,token):null);
        var layerPlan=Stage("layers",new{recipe.Layers},["content","networks"],()=>new MetadataLayerPlanner().Plan(recipe.Layers ?? [], nodes, connections, token));
        var routePlan=Stage("routes",new{recipe.RouteRules,recipe.RouteBudget,recipe.Space,recipe.Environment},[surfaceStage,"allocation","access","regions","networks","interiors"],()=>recipe.RouteRules is {} routeRules ? new MetadataRoutePlanner().Plan(routeRules,
            travelSurface ?? throw new ArgumentException("METADATA_ROUTE_SURFACE_REQUIRED"),recipe.Space,nodes,connections,token,accessPlan,null,regionPlan,localLayoutPlan,
            MetadataRoutePlanner.AllocationBindings(placementPlan?.Definition),recipe.Environment,placementPlan?.Definition.SourceWorld?.RoutePlan,recipe.RouteBudget,surface!.Query.Definition,
            MetadataRoutePlanner.RegionBindings(placementPlan?.Definition),execution?.RouteMemo) : null);
        execution?.RouteMemo.Complete(recipe.Id,routePlan,token);
        var corridorPlan=Stage("corridors",new{recipe.Corridors,recipe.Space},[surfaceStage,"routes","networks","access","interiors"],()=>recipe.Corridors is {} corridors?new MetadataCorridorPlanner().Plan(corridors,travelSurface??throw new ArgumentException("METADATA_CORRIDOR_SURFACE_REQUIRED"),recipe.Space,routePlan,networkPlan,connections,accessPlan,token):null);
        var servicePlan=Stage("services",new{recipe.Services,serviceProfiles,recipe.Space},["content","allocation","routes","networks","access","corridors"],()=>recipe.Services is {} services?new MetadataServicePlanner(structureCatalogs).Plan(services,serviceProfiles,structures,nodes,connections,recipe.Space,routePlan,networkPlan,token,accessPlan,placementPlan,corridorPlan):null);
        var contextPlan=Stage("context",new{recipe.Context,recipe.Space},["content","allocation"],()=>recipe.Context is {} context?new MetadataContextPlanner(structureCatalogs).Plan(context,nodes,structures,placementPlan??throw new ArgumentException("METADATA_CONTEXT_PLACEMENT_REQUIRED"),recipe.Space,token):null);
        var world=new WorldMetadataDocument(2, recipe.Id, recipe.GameProfile, recipe.Scale, recipe.Coverage, recipe.RemainingScopes.ToArray(),
            new(recipe.Space.Axes.ToArray(),recipe.Space.Unit), Digest(authoredRecipe), Digest(catalog), nodes.AsReadOnly(), connections.AsReadOnly(),
            layerPlan, new ReadOnlyDictionary<string,MetadataStructureDefinition>(structures),Surface:surface?.Query.Definition,RoutePlan:routePlan,NetworkPlan:networkPlan,EnvironmentPlan:environmentPlan,PlacementPlan:placementPlan,
            FootprintDefinitions:footprintDefinitions.Count==0?null:new ReadOnlyDictionary<string,MetadataCatalogFootprint>(footprintDefinitions),AccessPlan:accessPlan,CapacityPlan:capacityPlan,ServicePlan:servicePlan,ContextPlan:contextPlan,CorridorPlan:corridorPlan,RegionPlan:regionPlan,LocalLayoutPlan:localLayoutPlan,LayoutAdaptationPlan:adaptationPlan,ProgressionPlan:progressionPlan,VariationPlan:content.Variation,TopologyPlan:topologyPlan,InteriorPlan:interiorPlan,TerrainDemandPlan:terrainDemandPlan,ProgramMixPlan:content.ProgramMix,DrainagePlan:drainagePlan,WaterPlan:waterPlan,WindMoisturePlan:windPlan,RegionDemandPlan:regionDemandPlan,RegionDemandId:regionDemandPlan?.Definition.Id);
        if(recipe.Placement?.SourceWorld is {} baseWorld)MetadataPopulationSources.ValidateResult(baseWorld,world,token);
        new MetadataPhysicalAccountingPlanner().ValidateRequiredGroundUses(world,token);
        if(recipe.GroundCover is {} groundCover)
            world=world with{GroundCoverPlan=Stage("ground-cover",new{Definition=groundCover,Source=world},
                [surfaceStage,"content","allocation","access","regions","corridors"],()=>new MetadataGroundCoverPlanner().PlanCore(groundCover,world,token)),GroundCoverId=groundCover.Id};
        if(recipe.Chunks is {} chunks)
            world=world with{ChunkPlan=Stage("chunks",new{Definition=chunks,Source=world},
                new[]{surfaceStage,"content","allocation","access","networks","routes","context","corridors","regions","layers","environment","services","progression","interiors"}.Concat(drainagePlan is null?Array.Empty<string>():["drainage"]).Concat(recipe.GroundCover is null?Array.Empty<string>():["ground-cover"]).ToArray(),
                ()=>new MetadataChunkPlanner().PlanCore(chunks,world,token))};
        execution?.ValidateDependencies(Dependencies(authoredRecipe,catalog,token));
        return world;

    }

    private Dictionary<string,WorldMetadataArchetype> Validate(WorldMetadataRecipe r,WorldMetadataCatalog c,CancellationToken token,bool allowUnboundOrigin=false)
    {
        bool Id(string? id)=>SettlementCatalogRegistry.ValidId(id) && !id!.Contains('/');
        if(r is null || c is null || r.SchemaVersion!=1 || c.SchemaVersion!=1 || !Id(r.Id) || !Id(r.GameProfile) || !Id(r.Scale) ||
            !Id(c.Id) || r.CatalogId!=c.Id || !Id(r.RootArchetypeId) || !Enum.IsDefined(r.Coverage) ||
            r.RemainingScopes is null || r.RemainingScopes.Count>512 || r.RemainingScopes.Any(s=>string.IsNullOrWhiteSpace(s) || s.Length>2048) ||
            (r.Coverage==MetadataCoverage.PartialScope ? r.RemainingScopes.Count==0 : r.RemainingScopes.Count!=0) ||
            r.Parameters is null || r.Parameters.Count>256 || r.Parameters.Any(p=>!Id(p.Key) || p.Value<0 || p.Value>100000) ||
            r.Space is null || r.Space.Axes is null || r.Space.Axes.Count>8 || r.Space.Axes.Any(a=>!Id(a)) ||
            r.Space.Axes.Distinct(StringComparer.Ordinal).Count()!=r.Space.Axes.Count || !Id(r.Space.Unit) ||
            r.MaximumNodes is < 1 or > 100000 || r.MaximumConnections is < 0 or > 500000 ||
            r.AdditionalConnections?.Count>50000 ||
            c.Archetypes is null || c.Archetypes.Count is < 1 or > 1000)
            throw new ArgumentException("METADATA_RECIPE_OR_CATALOG_INVALID");
        bool Vector(IReadOnlyList<double>? v)=>v is not null && r.Space.Axes.Count>0 && v.Count==r.Space.Axes.Count && v.All(double.IsFinite);
        if(r.Origin is not null && !Vector(r.Origin) || !allowUnboundOrigin&&r.RequireAllPositions && r.Origin is null)
            throw new ArgumentException("METADATA_ORIGIN_INVALID");
        MetadataPopulationSources.ValidateContextDeclaration(r);
        var types=new Dictionary<string,WorldMetadataArchetype>(StringComparer.Ordinal);int parts=0;
        foreach(var a in c.Archetypes)
        {
            token.ThrowIfCancellationRequested();
            if(a is null || !Id(a.Id) || !types.TryAdd(a.Id,a) || !Id(a.Kind) || string.IsNullOrWhiteSpace(a.Purpose) || a.Purpose.Length>2048 ||
                a.Properties is null || a.Parts is null || a.Parts.Count>256 ||
                a.Connections is null || a.Connections.Count>256 || a.ChildRequirements is null || a.ChildRequirements.Count>256)
                throw new ArgumentException("METADATA_ARCHETYPE_INVALID");
            ValidateProperties(a.Properties,token,"METADATA_ARCHETYPE_INVALID");
            ValidateComponent(a.Component);
            if(a.Structure is { } reference && (reference.Catalog is null || !Id(reference.Catalog.CatalogId) ||
                reference.Catalog.EnabledCapabilities is null || !Id(reference.DefinitionId) || !Id(reference.VariantId)))
                throw new ArgumentException("METADATA_STRUCTURE_REFERENCE_INVALID");
            if(a.PhysicalFootprint is {} physical)
            {
                if(a.Structure is not null)throw new ArgumentException("METADATA_ARCHETYPE_FOOTPRINT_SOURCE_CONFLICT");
                MetadataFootprintResolver.Snapshot(physical,r.Space.Unit);
            }
            if(a.Capacities is {} capacities)MetadataCapacityPlanner.ValidateDefinitions(capacities);
            if(a.ProgramInputs is {Count:>0} inputs)MetadataCapacityPlanner.ValidateDefinitions(inputs);
            parts+=a.Parts.Count;if(parts>10000)throw new ArgumentException("METADATA_CATALOG_PART_BUDGET");
            var ids=new HashSet<string>(StringComparer.Ordinal){"self"};
            foreach(var p in a.Parts)
            {
                if(p is null || !Id(p.Id) || !ids.Add(p.Id) || !Id(p.ArchetypeId) || string.IsNullOrWhiteSpace(p.Purpose) || p.Purpose.Length>2048 || p.Count<0 || p.Count>100000 ||
                    p.CountParameter is { } parameter && !Id(parameter))
                    throw new ArgumentException("METADATA_PART_INVALID");
                if(p.Layout is {} layout)Plan(layout,r.Space.Axes.Count,0,token);
                if(p.Capacity is {} capacity)MetadataCapacityPlanner.ValidateRule(capacity);
            }
            var ruleIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var l in a.Connections)
                if(l is null || !Id(l.Id) || !ruleIds.Add(l.Id) || !Id(l.Kind) || !Enum.IsDefined(l.Pattern) || !ids.Contains(l.FromPart) ||
                    (l.Pattern==MetadataConnectionPattern.Chain ? l.ToPart is not null : l.ToPart is null || !ids.Contains(l.ToPart)))
                    throw new ArgumentException("METADATA_CONNECTION_RULE_INVALID");
            foreach(var rule in a.Connections)if(rule.Properties is not null)ValidateProperties(rule.Properties,token);
            var requirements=new HashSet<string>(StringComparer.Ordinal);
            foreach(var q in a.ChildRequirements)
                if(q is null || !Id(q.Kind) || !requirements.Add(q.Kind) || q.Minimum<0 || q.Maximum<q.Minimum || q.Maximum>100000)
                    throw new ArgumentException("METADATA_CHILD_REQUIREMENT_INVALID");
        }
        new MetadataLayerPlanner().ValidateMemberships(r.Layers ?? [],
            c.Archetypes.Select(a => a.LayerId)
                .Concat(c.Archetypes.SelectMany(a => a.Connections).Select(c => c.LayerId))
                .Concat((r.AdditionalConnections ?? []).Where(c => c is not null).Select(c => c.LayerId)), token);
        if(!types.ContainsKey(r.RootArchetypeId))throw new ArgumentException("METADATA_ROOT_UNKNOWN");
        var active=new HashSet<string>(StringComparer.Ordinal);var done=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in types.Keys.Order(StringComparer.Ordinal))Visit(id,0);
        void Visit(string id,int depth)
        {
            token.ThrowIfCancellationRequested();
            if(!types.ContainsKey(id))throw new ArgumentException("METADATA_ARCHETYPE_UNKNOWN:"+id);
            if(done.Contains(id))return;
            if(depth>32 || !active.Add(id))throw new ArgumentException("METADATA_ARCHETYPE_CYCLE_OR_DEPTH");
            foreach(var part in types[id].Parts)
                foreach(var candidate in MetadataVariationPlanner.Candidates(part,types))Visit(candidate,depth+1);
            active.Remove(id);done.Add(id);
        }
        ValidateParameterBindings(r,types,token);
        return types;
    }
    private static void ValidateProperties(IReadOnlyDictionary<string,JsonElement> values,CancellationToken token,string code="METADATA_PROPERTIES_INVALID")
    {
        token.ThrowIfCancellationRequested();
        if(values.Count>256 || values.Any(p=>string.IsNullOrWhiteSpace(p.Key) || p.Key.Length>160 || p.Value.ValueKind==JsonValueKind.Undefined))
            throw new ArgumentException(code);
        if(values.Count==0)return;
        byte[] json=JsonSerializer.SerializeToUtf8Bytes(values,WorldJson.CreateOptions());
        if(json.Length>65536)throw new ArgumentException(code);
        // Typed callers can construct opaque JsonElements outside our file reader.
        // Validate their JSON shape with the same native rule before expansion.
        using var document=JsonDocument.Parse(json,new(){MaxDepth=32,AllowDuplicateProperties=false});
        token.ThrowIfCancellationRequested();
    }
    private static string Digest<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions())));
}
