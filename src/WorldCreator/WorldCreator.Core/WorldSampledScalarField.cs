using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldSampledScalarField(int SchemaVersion,string Id,string Purpose,string SourceRuleId,string Unit,
    WorldPoint Origin,double StepX,double StepY,int Columns,int Rows,IReadOnlyList<double> Values);
public readonly record struct WorldScalarSample(double Value,double GradientX,double GradientY);

/// <summary>Typed scalar lattice aligned with the terrain subdivision. Its values
/// never change elevations, surface kinds, occupancy or traversal permissions.</summary>
public sealed class WorldSampledScalarQuery
{
    public WorldSampledScalarField Definition {get;}
    public WorldSampledScalarQuery(WorldSampledScalarField field,WorldHeightFieldGeometry terrain,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(field is null||field.SchemaVersion!=1||!WorldEnvironmentQuery.Id(field.Id)||!WorldEnvironmentQuery.Id(field.Unit)||
            !WorldEnvironmentQuery.Text(field.Purpose)||!WorldEnvironmentQuery.Text(field.SourceRuleId)||field.Values is null||
            terrain is null||field.Origin!=terrain.Origin||field.StepX!=terrain.StepX||field.StepY!=terrain.StepY||
            field.Columns!=terrain.Columns||field.Rows!=terrain.Rows||field.Columns is <2 or >513||field.Rows is <2 or >513||
            !WorldHeightFieldQuery.Point(field.Origin)||!double.IsFinite(field.StepX)||field.StepX<.001||!double.IsFinite(field.StepY)||field.StepY<.001||
            !WorldHeightFieldQuery.Number(field.Origin.X+(field.Columns-1)*field.StepX)||!WorldHeightFieldQuery.Number(field.Origin.Y+(field.Rows-1)*field.StepY)||
            field.Values.Count!=(long)field.Columns*field.Rows)throw new ArgumentException("WORLD_SCALAR_FIELD_INVALID");
        var values=new double[field.Values.Count];
        for(int i=0;i<values.Length;i++)
        {
            token.ThrowIfCancellationRequested();if(!WorldEnvironmentQuery.Number(field.Values[i]))throw new ArgumentException("WORLD_SCALAR_VALUE_INVALID");
            values[i]=field.Values[i];
        }
        Definition=field with{Values=Array.AsReadOnly(values)};
    }
    public WorldScalarSample Sample(WorldPoint point,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();var f=Definition;
        if(!WorldHeightFieldQuery.Point(point))throw new ArgumentException("WORLD_SURFACE_POINT_INVALID");
        if(point.X<f.Origin.X||point.Y<f.Origin.Y||point.X>f.Origin.X+(f.Columns-1)*f.StepX||point.Y>f.Origin.Y+(f.Rows-1)*f.StepY)
            throw new ArgumentException("WORLD_SCALAR_SAMPLE_UNCOVERED:"+f.Id);
        double x=(point.X-f.Origin.X)/f.StepX,y=(point.Y-f.Origin.Y)/f.StepY;
        int col=Math.Min((int)Math.Floor(x),f.Columns-2),row=Math.Min((int)Math.Floor(y),f.Rows-2);
        return WorldTriangularInterpolation.Sample(f.Values,f.Columns,col,row,x-col,y-row,f.StepX,f.StepY);
    }
}
