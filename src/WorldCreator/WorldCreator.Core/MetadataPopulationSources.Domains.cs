using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public enum MetadataPopulationDomainSource { SiteFeature, GroundMaterial }
/// <summary>Actual source-domain selector. Null NodeId uses the nested parent;
/// ground material at the root selects all covered cells of that material.</summary>
public sealed record MetadataPopulationDomainPattern(MetadataPopulationDomainSource Source,string Selector,
    WorldRegionPopulationPattern? Bands=null,WorldAreaPopulationPattern? Area=null,string? NodeId=null,int SourceIndex=0,
    int? DirectionEdgeIndex=null,WorldSuitabilityProfile? Profile=null);
public sealed record MetadataPopulationDomainResult(MetadataPopulationDomainPattern Definition,string? SourceNodeId,
    IReadOnlyList<string> SourceIds,string SourceGeometrySha256,WorldRegion Shape,WorldRegionPopulationPattern? ResolvedBands,
    WorldAreaPopulationPlan? Area,IReadOnlyList<WorldPoint> WorldPositions);
public sealed record MetadataPopulationDomainTrace(string StageId,string DemandId,string Purpose,string SourceRuleId,
    IReadOnlyList<string> NodeIds,MetadataPopulationDomainResult Domain);

internal static partial class MetadataPopulationSources
{
    internal static MetadataPopulationDomainResult Domain(MetadataPopulationDomainPattern pattern,int count,WorldMetadataDocument? source,string? parent,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(pattern is null||!Enum.IsDefined(pattern.Source)||!WorldEnvironmentQuery.Id(pattern.Selector)||pattern.SourceIndex is <0 or >1023||
            (pattern.Bands is null?0:1)+(pattern.Area is null?0:1)!=1||pattern.DirectionEdgeIndex is <0 or >63||
            pattern.NodeId is {} id&&(!WorldEnvironmentQuery.Text(id)||id.Length>2048)||
            pattern.Source==MetadataPopulationDomainSource.GroundMaterial&&(pattern.SourceIndex!=0||pattern.DirectionEdgeIndex is not null)||
            pattern.Area is not null&&(pattern.Profile is null||pattern.DirectionEdgeIndex is not null)||
            pattern.Bands is {} declared&&(pattern.Profile is not null||!double.IsFinite(declared.DirectionDegrees)||Math.Abs(declared.DirectionDegrees)>360))
            throw new ArgumentException("METADATA_POPULATION_DOMAIN_INVALID");
        if(source?.Surface is null||source.Space.Axes.Count!=2||count<1)throw new ArgumentException("METADATA_POPULATION_DOMAIN_SOURCE_REQUIRED");
        string? owner=pattern.NodeId??parent;budget.Charge(source.Nodes.Count);
        if(owner is not null&&!source.Nodes.Any(n=>n.Id==owner))throw new ArgumentException("METADATA_POPULATION_DOMAIN_OWNER_MISSING");
        IReadOnlyList<WorldPolygon> cells;string[] sourceIds;WorldPolygon? featurePolygon=null;
        if(pattern.Source==MetadataPopulationDomainSource.SiteFeature)
        {
            owner??=source.Id;var features=source.PlacementPlan?.SiteFeatures??[];budget.Charge(features.Count);
            var selected=features.Where(f=>f.NodeId==owner&&f.Kind.ToString()==pattern.Selector).OrderBy(f=>f.Id,StringComparer.Ordinal).ToArray();
            if(pattern.SourceIndex>=selected.Length)throw new ArgumentException("METADATA_POPULATION_DOMAIN_FEATURE_MISSING");
            var feature=selected[pattern.SourceIndex];featurePolygon=feature.Boundary;cells=[feature.Boundary];sourceIds=[feature.Id];
        }
        else
        {
            var cover=source.GroundCoverPlan??throw new ArgumentException("METADATA_POPULATION_DOMAIN_GROUND_REQUIRED");
            if(!cover.Definition.Materials.Any(m=>m.Id==pattern.Selector))throw new ArgumentException("METADATA_POPULATION_DOMAIN_MATERIAL_MISSING");
            budget.Charge(cover.Sources.Count+cover.Cells.Count);
            var owners=cover.Sources.Where(s=>s.NodeId==owner).Select(s=>s.Id).ToHashSet(StringComparer.Ordinal);
            var selected=cover.Cells.Where(c=>c.Status==MetadataGroundCellStatus.Covered&&c.MaterialId==pattern.Selector&&
                (owner is null||c.SourceIds.Any(owners.Contains))).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
            if(selected.Length==0)throw new ArgumentException("METADATA_POPULATION_DOMAIN_GROUND_MISSING");
            cells=selected.Select(c=>c.Boundary).ToArray();sourceIds=selected.Select(c=>c.Id).ToArray();
        }
        if(cells.Count>4096)throw new ArgumentException("METADATA_POPULATION_DOMAIN_CELL_BUDGET");
        var surface=new WorldSurfaceQuery(source.Surface,token);
        var kinds=surface.Definition.Patches.Select(p=>p.Kind).Concat(surface.HeightField is {} field?[field.Geometry.Kind]:[]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var neutral=new WorldSuitabilityProfile("population-domain","Computational source-domain topology","population:domain",kinds,[],[]);
        var fields=source.EnvironmentPlan?.Definition??new WorldEnvironmentDefinition(1,"domain-fields",[new("constant","Computational topology field","population:domain","relative",0,[])]);
        var environment=new WorldEnvironmentQuery(surface,fields,token);
        var components=WorldRegionGeometry.Components(environment,neutral,cells,budget,token);
        var boundaries=components.SelectMany(c=>c.BoundaryEdges).ToArray();double area=components.Sum(c=>c.Area);
        if(!double.IsFinite(area)||area<=0)throw new ArgumentException("METADATA_POPULATION_DOMAIN_GEOMETRY_INVALID");
        double cx=0,cy=0;
        foreach(var cell in cells)
        {
            budget.Charge(cell.Vertices.Count);var o=cell.Vertices[0];double twice=0,x=0,y=0;
            for(int i=0;i<cell.Vertices.Count;i++)
            {var a=cell.Vertices[i];var b=cell.Vertices[(i+1)%cell.Vertices.Count];double cross=(a.X-o.X)*(b.Y-o.Y)-(b.X-o.X)*(a.Y-o.Y);twice+=cross;x+=(a.X+b.X-2*o.X)*cross;y+=(a.Y+b.Y-2*o.Y)*cross;}
            double weight=Math.Abs(twice)/2;cx+=(o.X+x/(3*twice))*weight;cy+=(o.Y+y/(3*twice))*weight;
        }
        var seed=new WorldRegionSeed("source-domain","Declared population mask","population:domain",new(cx/area,cy/area),area,neutral);
        var shape=new WorldRegion("source-domain",seed,cells,boundaries,area);var bands=pattern.Bands;
        if(pattern.DirectionEdgeIndex is {} edge)
        {
            if(featurePolygon is null||edge>=featurePolygon.Vertices.Count)throw new ArgumentException("METADATA_POPULATION_DOMAIN_EDGE_MISSING");
            var a=featurePolygon.Vertices[edge];var b=featurePolygon.Vertices[(edge+1)%featurePolygon.Vertices.Count];
            bands=bands! with{DirectionDegrees=NormalizeDirection(bands!.DirectionDegrees+Math.Atan2(b.Y-a.Y,b.X-a.X)*180/Math.PI)};
        }
        WorldAreaPopulationPlan? allocation=null;IReadOnlyList<WorldPoint> positions;
        if(bands is not null)positions=WorldPopulationGeometry.Region(shape,bands,count,budget,token);
        else
        {
            if(source.EnvironmentPlan is null)throw new ArgumentException("METADATA_POPULATION_DOMAIN_ENVIRONMENT_REQUIRED");
            allocation=WorldPopulationGeometry.AreaVerifiedRegion(shape,environment,pattern.Profile!,pattern.Area!,count,budget,token,resweepTerrain:true);
            positions=allocation.Strata.Select(s=>s.Position).ToArray();
        }
        var hash=MetadataExecutionCache.Hash(new{source.Space,pattern.Source,Owner=owner,pattern.Selector,sourceIds,Cells=cells});
        return new(pattern,owner,sourceIds,hash,shape,bands,allocation,positions);
    }
    private static double NormalizeDirection(double value){double n=value%360;return n<0?n+360:n;}
}
