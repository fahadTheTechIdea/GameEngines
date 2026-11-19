using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Immutable bounded storage on the original global lattice. Unknown
/// samples refuse explicitly; no complete or rebased field is fabricated.</summary>
public sealed class WorldTerrainWindowQuery : IWorldHeightFieldQuery
{
    public WorldTerrainWindow? Snapshot {get;}
    public WorldTerrainRecipe Source {get;}
    public WorldHeightFieldGeometry Geometry {get;}
    public WorldHeightField? CompleteField=>null;
    public double MaxX=>Geometry.MaxX;
    public double MaxY=>Geometry.MaxY;

    public WorldTerrainWindowQuery(WorldTerrainWindow window,CancellationToken token=default)
        :this(new WorldTerrainGenerator().ReplayWindow(window,token)){}
    private WorldTerrainWindowQuery(WorldTerrainWindow snapshot):this(snapshot.Source,snapshot){}
    private WorldTerrainWindowQuery(WorldTerrainRecipe source,WorldTerrainWindow? snapshot)
    {
        Snapshot=snapshot;
        Source=source;
        Geometry=WorldHeightFieldQuery.Describe(WorldTerrainGenerator.Header(source));
    }
    internal static WorldTerrainWindowQuery FromGenerated(WorldTerrainWindow snapshot)=>new(snapshot);
    internal static WorldTerrainWindowQuery WithoutSamples(WorldTerrainRecipe source)=>new(source,null);
    public double HeightAt(int column,int row)
    {
        var window=Snapshot;
        if(window is null||column<window.FirstColumn||row<window.FirstRow||column>=window.FirstColumn+window.Columns||row>=window.FirstRow+window.Rows)
            throw new ArgumentException("WORLD_TERRAIN_WINDOW_SAMPLE_OUTSIDE");
        return window.Heights[(row-window.FirstRow)*window.Columns+column-window.FirstColumn];
    }
    public WorldScalarSample SampleCell(int column,int row,double u,double v)=>WorldHeightFieldQuery.CellCore(this,column,row,u,v);
    public WorldSurfaceSample Sample(WorldPoint point,CancellationToken token=default)=>WorldHeightFieldQuery.SampleCore(this,point,token);
    public IEnumerable<double> Cuts(WorldPoint a,WorldPoint b,CancellationToken token)=>WorldHeightFieldQuery.CutsCore(Geometry,a,b,token);
    public IEnumerable<WorldHeightFieldColumnCut> ColumnCuts(double x,double low,double high,CancellationToken token)
        =>WorldHeightFieldQuery.ColumnCutsCore(Geometry,x,low,high,token);
}
