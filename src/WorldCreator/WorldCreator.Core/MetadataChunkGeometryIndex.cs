using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace WorldCreator.Core.Metadata;

public enum MetadataChunkCoverageKind { Footprint, Reservation, SiteFeature, AccessApproach, PathSegment, InteriorWall }
/// <summary>Source role and geometry. SourceId is scoped to the member; segment
/// indices refer to consecutive points in the path centerline.</summary>
public sealed record MetadataChunkCoverage(MetadataChunkCoverageKind Kind,string SourceId,int? SegmentIndex,WorldPolygon Polygon,SiteFeatureKind? FeatureKind=null);

/// <summary>Unclipped source geometry. Only a footprint with declared Solid
/// occupancy indicates solid content; other coverage roles do not imply collision.</summary>
public sealed record MetadataChunkGeometry(MetadataChunkMember Member,WorldPoint Anchor,
    IReadOnlyList<MetadataChunkCoverage> Coverage,MetadataOccupancyKind? Occupancy,WorldPolyline? Path,double? PassageRadius,WorldMetadataNode? Node,
    IReadOnlyList<WorldMetadataConnection> Connections,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataChunkInterior>? Interiors=null);

internal static partial class MetadataChunkGeometrySnapshot
{
    internal static WorldMetadataNode CopyNode(WorldMetadataNode node)
    {
        var properties=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        foreach(var (key,value) in node.Properties)properties.Add(key,value.Clone());
        return node with{Properties=new ReadOnlyDictionary<string,JsonElement>(properties),
            Position=node.Position is null?null:Array.AsReadOnly(node.Position.ToArray())};
    }
    internal static WorldMetadataConnection CopyConnection(WorldMetadataConnection connection)
    {
        var properties=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        foreach(var (key,value) in connection.Properties)properties.Add(key,value.Clone());
        return connection with{Properties=new ReadOnlyDictionary<string,JsonElement>(properties)};
    }
}

/// <summary>Detached source-bound geometry for chunk selection. This in-memory
/// snapshot does not include shared terrain or independently generated chunks.</summary>
public sealed class MetadataChunkGeometryIndex
{
    private readonly MetadataChunkSelectionIndex selection;
    private readonly IReadOnlyDictionary<(MetadataSpatialMemberKind,string),MetadataChunkGeometry> geometry;
    internal MetadataChunkGeometryIndex(MetadataChunkSelectionIndex selection,
        IReadOnlyDictionary<(MetadataSpatialMemberKind,string),MetadataChunkGeometry> geometry,long work)
    {this.selection=selection;this.geometry=geometry;Work=work;}
    public string SourceWorldSha256=>selection.SourceWorldSha256;
    public string ChunkPlanSha256=>selection.ChunkPlanSha256;
    public long Work {get;}

    /// <summary>Returns whole members once in canonical order, even across seams.</summary>
    public IReadOnlyList<MetadataChunkGeometry> Select(IReadOnlyList<string> chunkIds,CancellationToken token=default)
    {
        var selected=selection.Select(chunkIds,token);var result=new MetadataChunkGeometry[selected.Count];
        for(int i=0;i<selected.Count;i++)
        {token.ThrowIfCancellationRequested();var member=selected[i];result[i]=geometry[(member.Kind,member.Id)];}
        return Array.AsReadOnly(result);
    }
}
