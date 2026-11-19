using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Original lattice geometry/provenance, independent of sample storage.</summary>
public sealed record WorldHeightFieldGeometry(int SchemaVersion,string Id,string Kind,string Purpose,string SourceRuleId,string Unit,
    WorldPoint Origin,double StepX,double StepY,int Columns,int Rows)
{
    [JsonIgnore] public double MaxX=>Origin.X+(Columns-1)*StepX;
    [JsonIgnore] public double MaxY=>Origin.Y+(Rows-1)*StepY;
}

/// <summary>Exact original-lattice queries. CompleteField is null for bounded
/// window storage; consumers must never infer a complete buffer from Geometry.</summary>
public interface IWorldHeightFieldQuery
{
    WorldHeightFieldGeometry Geometry {get;}
    WorldHeightField? CompleteField {get;}
    double MaxX {get;}
    double MaxY {get;}
    double HeightAt(int column,int row);
    WorldScalarSample SampleCell(int column,int row,double u,double v);
    WorldSurfaceSample Sample(WorldPoint point,CancellationToken token=default);
    IEnumerable<double> Cuts(WorldPoint a,WorldPoint b,CancellationToken token);
    IEnumerable<WorldHeightFieldColumnCut> ColumnCuts(double x,double low,double high,CancellationToken token);
}
public readonly record struct WorldHeightFieldColumnCut(double Y,double Slope,int Column,int Row,bool Diagonal);
