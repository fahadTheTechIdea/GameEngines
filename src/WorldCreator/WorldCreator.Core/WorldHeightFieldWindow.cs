using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Inclusive cell indices on the original lattice. The halo deliberately
/// over-approximates boundary ownership; no translated origin or height substitution.</summary>
internal sealed record WorldHeightFieldWindow(int FirstColumn,int FirstRow,int LastColumn,int LastRow)
{
    internal static WorldHeightFieldWindow For(WorldHeightFieldGeometry f,double minX,double minY,double maxX,double maxY)
    {
        double fieldMaxX=f.Origin.X+(f.Columns-1)*f.StepX,fieldMaxY=f.Origin.Y+(f.Rows-1)*f.StepY;
        if(maxX<f.Origin.X-1e-8||maxY<f.Origin.Y-1e-8||minX>fieldMaxX+1e-8||minY>fieldMaxY+1e-8)
            return new(0,0,-1,-1);
        int Cell(double value,double origin,double step,int count)=>(int)Math.Clamp(Math.Floor((value-origin)/step),0,count-2);
        return new(Math.Max(0,Cell(minX,f.Origin.X,f.StepX,f.Columns)-1),Math.Max(0,Cell(minY,f.Origin.Y,f.StepY,f.Rows)-1),
            Math.Min(f.Columns-2,Cell(maxX,f.Origin.X,f.StepX,f.Columns)+1),Math.Min(f.Rows-2,Cell(maxY,f.Origin.Y,f.StepY,f.Rows)+1));
    }
    internal IReadOnlyList<double> Samples(IWorldHeightFieldQuery query,CancellationToken token)
    {
        if(LastColumn<FirstColumn||LastRow<FirstRow)return Array.Empty<double>();
        var values=new double[(LastColumn-FirstColumn+2)*(LastRow-FirstRow+2)];int index=0;
        for(int row=FirstRow;row<=LastRow+1;row++)for(int col=FirstColumn;col<=LastColumn+1;col++)
        {token.ThrowIfCancellationRequested();values[index++]=query.HeightAt(col,row);}
        return Array.AsReadOnly(values);
    }
}
