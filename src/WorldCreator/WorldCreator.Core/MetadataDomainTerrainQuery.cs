using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Reads one detached chunk window in the original lattice. This query
/// samples the height field only; surface masks and patches remain separate.</summary>
public sealed class MetadataDomainTerrainQuery:IWorldHeightFieldQuery
{
    private readonly MetadataDomainTerrainWindow window;
    public WorldHeightFieldGeometry Geometry {get;}
    public WorldHeightField? CompleteField=>null;
    public double MaxX=>Geometry.MaxX;
    public double MaxY=>Geometry.MaxY;

    public MetadataDomainTerrainQuery(MetadataDomainChunkSurface surface,MetadataDomainTerrainChunk chunk,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(surface?.Field is not {} field||surface.AvailableSamples is not {} available||chunk?.Window is not {} input||
            input.FirstColumn<0||input.FirstRow<0||input.Columns<2||input.Rows<2||input.Heights is null||
            input.Heights.Count!=(long)input.Columns*input.Rows||input.Heights.Count>513*513||
            available.FirstColumn<0||available.FirstRow<0||available.Columns<2||available.Rows<2||
            (long)available.FirstColumn+available.Columns>field.Columns||(long)available.FirstRow+available.Rows>field.Rows||
            input.FirstColumn<available.FirstColumn||input.FirstRow<available.FirstRow||
            (long)input.FirstColumn+input.Columns>(long)available.FirstColumn+available.Columns||
            (long)input.FirstRow+input.Rows>(long)available.FirstRow+available.Rows)
            throw new ArgumentException("METADATA_DOMAIN_TERRAIN_QUERY_INVALID");
        WorldHeightFieldQuery.ValidateHeader(new(field.SchemaVersion,field.Id,field.Kind,field.Purpose,field.SourceRuleId,
            field.Unit,field.Origin,field.StepX,field.StepY,field.Columns,field.Rows,[]));
        if(field.Unit!=surface.Unit)throw new ArgumentException("METADATA_DOMAIN_TERRAIN_QUERY_INVALID");
        var values=new double[input.Heights.Count];
        for(int i=0;i<values.Length;i++){token.ThrowIfCancellationRequested();if(!double.IsFinite(input.Heights[i]))throw new ArgumentException("METADATA_DOMAIN_TERRAIN_QUERY_INVALID");values[i]=input.Heights[i];}
        Geometry=field;window=input with{Heights=Array.AsReadOnly(values)};
    }

    public double HeightAt(int column,int row)
    {
        if(column<window.FirstColumn||row<window.FirstRow||column>=window.FirstColumn+window.Columns||row>=window.FirstRow+window.Rows)
            throw new ArgumentException("METADATA_DOMAIN_TERRAIN_SAMPLE_OUTSIDE");
        return window.Heights[(row-window.FirstRow)*window.Columns+column-window.FirstColumn];
    }
    public WorldScalarSample SampleCell(int column,int row,double u,double v)=>WorldHeightFieldQuery.CellCore(this,column,row,u,v);
    public WorldSurfaceSample Sample(WorldPoint point,CancellationToken token=default)=>WorldHeightFieldQuery.SampleCore(this,point,token);
    public IEnumerable<double> Cuts(WorldPoint a,WorldPoint b,CancellationToken token)=>WorldHeightFieldQuery.CutsCore(Geometry,a,b,token);
    public IEnumerable<WorldHeightFieldColumnCut> ColumnCuts(double x,double low,double high,CancellationToken token)
        =>WorldHeightFieldQuery.ColumnCutsCore(Geometry,x,low,high,token);
}
