using WorldCreator.Core.Spatial;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Metadata;

/// <summary>A conservative witness: none of the declared terrain kinds can
/// satisfy a required region. It does not establish polygon coverage or fit.</summary>
public sealed record MetadataRegionalTerrainKindFailure(string RegionId,IReadOnlyList<string> AllowedKinds,IReadOnlyList<string> AvailableKinds);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private static void PreflightMissingTerrainKinds(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var surface=source.Surface;
        if(surface.Patches is null||surface.Patches.Any(p=>p is null))return;
        var kinds=surface.Patches.Select(p=>p.Kind).Concat(surface.Terrain is null?Array.Empty<string>():[surface.Terrain.Kind])
            .Concat(surface.HeightField is null?Array.Empty<string>():[surface.HeightField.Kind])
            .Where(k=>k is not null).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var absent=source.Regions.Where(r=>r.Profile.AllowedKinds is {Count:>0} allowed&&
            allowed.All(kind=>kind is not null&&!kinds.Contains(kind,StringComparer.Ordinal))).ToArray();
        if(absent.Length==0)return;
        token.ThrowIfCancellationRequested();
        long geometry=surface.Patches.Sum(p=>(long)(p.Boundary?.Vertices?.Count??0))+
            (surface.Boundary?.Vertices?.Count??0)+(surface.Exclusions?.Sum(e=>(long)(e?.Boundary?.Vertices?.Count??0))??0);
        budget.Charge(geometry>14142?200000001:1+geometry*geometry+source.Regions.Count+kinds.Length);
        if(surface.TerrainCapture is not null||surface.TerrainCapturePlan is not null)
            WorldSurfaceQuery.ChargeCaptureWork(surface,budget,token);
        else if(surface.Terrain is {} terrain)
            budget.Charge(WorldTerrainGenerator.FullGenerationWork(terrain,token)+(long)terrain.Columns*terrain.Rows);
        if(surface.HeightField is {} field)budget.Charge(field.Heights?.Count??0);
        var validatedSurface=new WorldSurfaceQuery(surface,token);
        var ownershipFields=source.OwnershipEnvironment??source.Regions[0].Recipe.Environment!;
        WorldPopulationGeometry.ValidateAreaPattern(new WorldEnvironmentQuery(validatedSurface,ownershipFields,token),source.Pattern,source.Regions.Count);
        var profiles=new Dictionary<string,WorldSuitabilityProfile>(StringComparer.Ordinal);
        foreach(var region in source.Regions)
        {
            token.ThrowIfCancellationRequested();
            budget.Charge(1L+(region.Recipe.Environment!.Fields?.Count??0)+(region.Profile.Requirements?.Count??0)+(region.Profile.Preferences?.Count??0));
            // Validate every profile and its field references before reporting
            // terrain infeasibility; malformed declarations are not disguised.
            var query=new WorldEnvironmentQuery(validatedSurface,region.Recipe.Environment!,token);
            profiles.Add(region.Id,new WorldPointSuitability(query,region.Profile).Profile);
        }
        var selected=absent[0];var failure=new WorldRegionIntentUnsatisfiedException(selected.Id);
        failure.Data["RegionalTerrainKindFailure"]=new MetadataRegionalTerrainKindFailure(selected.Id,profiles[selected.Id].AllowedKinds,kinds);
        throw failure;
    }
}
