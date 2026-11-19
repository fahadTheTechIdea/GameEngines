using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal static partial class MetadataPopulationSources
{
    internal static bool IsInheritedRegion(WorldMetadataDocument world)
        =>world.RegionPlan is not null&&world.PlacementPlan?.Definition.SourceWorld?.RegionPlan is {} original&&Equal(original,world.RegionPlan);
    internal static WorldRegionPlan? InheritComposedRegion(WorldMetadataRecipe consumer,WorldRegionRequest? request,WorldMetadataDocument? source)
    {
        if(consumer.PopulationSource?.Context?.UsePlacementBoundary==true)
            return source?.RegionPlan??throw new ArgumentException("METADATA_POPULATION_CONTEXT_HABITAT_REQUIRED");
        if(source?.ProgramCompositionPlan?.Attempts.LastOrDefault()?.RegionSelection is null)return null;
        var original=source.ProgramCompositionPlan.SourceRecipe.Regions;
        if(!Equal(consumer.Regions,original)||!Equal(request,source.RegionPlan!.Request with{ExcludedAssignments=null}))
            throw new ArgumentException("METADATA_POPULATION_SOURCE_REGIONS_CHANGED");
        return source.RegionPlan;
    }
    internal static WorldLocalLayoutDefinition? InheritAdaptedLayouts(WorldMetadataRecipe consumer,WorldMetadataDocument source)
    {
        if(consumer.PopulationSource?.Context?.UsePlacementBoundary==true)
            return source.LocalLayoutPlan?.Definition;
        if(source.LayoutAdaptationPlan is not {} adaptation||consumer.LayoutAdaptation is not null)return consumer.LocalLayouts;
        if(!Equal(consumer.LocalLayouts,adaptation.OriginalLayouts)||source.LocalLayoutPlan is null)
            throw new ArgumentException("METADATA_POPULATION_SOURCE_LAYOUT_CHANGED");
        // Source-local allocation is already qualified. The existing preservation
        // contract requires its exact plan; do not reintroduce its rejected first
        // candidate while placing downstream content.
        return source.LocalLayoutPlan.Definition;
    }
    internal static void ValidateContextDeclaration(WorldMetadataRecipe consumer)
    {
        if(consumer.PopulationSource is not {Context:{} context} source)return;
        if(source.Recipe is null||source.Catalog is null||source.Recipe.PopulationSource is not null||source.Recipe.Placement?.SourceWorld is not null)
            throw new ArgumentException("METADATA_POPULATION_SOURCE_LEVEL_INVALID");
        if(context.PlacementAnchorIds is not {Count:<=32}||context.PlacementAnchorIds.Any(id=>!WorldEnvironmentQuery.Id(id))||
            context.PlacementAnchorIds.Distinct(StringComparer.Ordinal).Count()!=context.PlacementAnchorIds.Count||source.Recipe.Placement is null&&context.PlacementAnchorIds.Count>0||consumer.Placement is null||
            !Equal(source.Recipe.Space,consumer.Space)||consumer.Space?.Axes?.Count!=2)
            throw new ArgumentException("METADATA_POPULATION_CONTEXT_INVALID");
        if(source.Recipe.Surface is not null||source.Recipe.Origin is not null)
            throw new ArgumentException("METADATA_POPULATION_CONTEXT_SOURCE_OVERRIDE");
        if(context.UsePlacementBoundary&&(source.Recipe.Regions is null||source.Recipe.Regions.Boundary is not null||source.Recipe.Placement?.Request?.Boundary is not null))
            throw new ArgumentException("METADATA_POPULATION_CONTEXT_BOUNDARY_CONFLICT");
        if(context.UsePlacementBoundary&&(consumer.Regions is not null||consumer.LocalLayouts is not null||consumer.RegionDemands is not null||consumer.LayoutAdaptation is not null||
            source.Recipe.LocalLayouts?.Layouts.Any(l=>l is null||l.Strategy!=WorldLocalLayoutStrategy.AreaBalanced&&l.RegionFrame is null)==true))
            throw new ArgumentException("METADATA_POPULATION_CONTEXT_HABITAT_OVERRIDE");
        if(source.Recipe.Placement is null)return;
        var request=source.Recipe.Placement.Request;
        if(request?.Anchors is null||request.Relations is null||context.PlacementAnchorIds.Any(id=>
            request.Anchors.Any(a=>a?.Id==id)||!request.Relations.Any(r=>r?.ToId==id)))
            throw new ArgumentException("METADATA_POPULATION_CONTEXT_ANCHOR_CONFLICT");
    }

    internal static WorldMetadataRecipe BindContext(WorldMetadataRecipe consumer,CancellationToken token,WorldSpatialWorkBudget? budget=null)
    {
        token.ThrowIfCancellationRequested();var source=consumer.PopulationSource??throw new ArgumentException("METADATA_POPULATION_SOURCE_REQUIRED");
        if(source.Context is null)return source.Recipe;
        ValidateContextDeclaration(consumer);
        if(consumer.Surface is null||consumer.Origin is not {Count:2}||consumer.Placement?.Request?.Anchors is null)
            throw new ArgumentException("METADATA_POPULATION_CONTEXT_REQUIRED");
        var boundary=source.Context.UsePlacementBoundary?consumer.Placement.Request.Boundary:null;
        if(source.Context.UsePlacementBoundary&&boundary is null)throw new ArgumentException("METADATA_POPULATION_CONTEXT_BOUNDARY_REQUIRED");
        if(boundary is not null)budget?.Charge(boundary.Vertices.Count*2L+1);
        if(source.Recipe.Placement is null)return source.Recipe with{Surface=consumer.Surface,Origin=consumer.Origin,
            Regions=boundary is null?source.Recipe.Regions:source.Recipe.Regions! with{Boundary=boundary}};
        var request=source.Recipe.Placement!.Request;var anchors=request.Anchors.ToList();
        budget?.Charge(anchors.Count+source.Context.PlacementAnchorIds.Count);
        foreach(string id in source.Context.PlacementAnchorIds)
        {
            budget?.Charge(consumer.Placement.Request.Anchors.Count+1);
            token.ThrowIfCancellationRequested();var found=consumer.Placement.Request.Anchors.Where(a=>a?.Id==id).ToArray();
            if(found.Length!=1)throw new ArgumentException("METADATA_POPULATION_CONTEXT_ANCHOR_REQUIRED:"+id);
            anchors.Add(found[0]);
        }
        budget?.Charge((long)anchors.Count*(1+(long)Math.Ceiling(Math.Log2(anchors.Count+1))));
        return source.Recipe with{Surface=consumer.Surface,Origin=consumer.Origin,
            Regions=boundary is null?source.Recipe.Regions:source.Recipe.Regions! with{Boundary=boundary},Placement=source.Recipe.Placement with{
            Request=request with{Boundary=boundary??request.Boundary,Anchors=anchors.OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray()}}};
    }

    internal static void ValidateAnalysis(MetadataProgramAnalysis source,MetadataProgramAnalysis consumer,WorldMetadataCatalog sourceCatalog,
        WorldMetadataCatalog consumerCatalog,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        budget.Charge(source.Nodes.Count+consumer.Nodes.Count+source.Connections.Count+consumer.Connections.Count+
            source.RelativePositions.Count+consumer.RelativePositions.Count+sourceCatalog.Archetypes.Count+consumerCatalog.Archetypes.Count);
        var nodes=consumer.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var links=consumer.Connections.ToDictionary(c=>c.Id,StringComparer.Ordinal);
        var relative=consumer.RelativePositions.ToDictionary(p=>p.NodeId,StringComparer.Ordinal);
        var originalTypes=sourceCatalog.Archetypes.ToDictionary(a=>a.Id,StringComparer.Ordinal);var currentTypes=consumerCatalog.Archetypes.ToDictionary(a=>a.Id,StringComparer.Ordinal);
        foreach(var node in source.Nodes)
        {
            token.ThrowIfCancellationRequested();
            if(!nodes.TryGetValue(node.Id,out var found)||!Equal(node with{Position=null},found with{Position=null}))
                throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_MEMBER_CHANGED:"+node.Id);
            if(source.FootprintDefinitions.TryGetValue(node.ArchetypeId,out var footprint)&&
                (!consumer.FootprintDefinitions.TryGetValue(node.ArchetypeId,out var current)||!Equal(footprint,current)))
                throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_FOOTPRINT_CHANGED:"+node.ArchetypeId);
            if(!Equal(originalTypes[node.ArchetypeId].Access,currentTypes[node.ArchetypeId].Access))
                throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_ACCESS_CHANGED:"+node.ArchetypeId);
        }
        foreach(var position in source.RelativePositions)
            if(!relative.TryGetValue(position.NodeId,out var found)||!Equal(position,found))
                throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_LAYOUT_CHANGED:"+position.NodeId);
        foreach(var link in source.Connections)
            if(!links.TryGetValue(link.Id,out var found)||!Equal(link,found))throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_CONNECTION_CHANGED:"+link.Id);
        foreach(var definition in source.StructureDefinitions)
            if(!consumer.StructureDefinitions.TryGetValue(definition.Key,out var found)||!Equal(definition.Value,found))
                throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_STRUCTURE_CHANGED:"+definition.Key);
    }
}
