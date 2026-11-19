using System.Text.Json.Serialization;

namespace WorldCreator.Core.Spatial;

/// <summary>Vertex indices in the original terrain lattice; no translated sampling origin.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTerrainWindowRequest(int Version,int FirstColumn,int FirstRow,int Columns,int Rows,int HaloCells=1);

/// <summary>Independently generated row-major samples, including the requested halo.
/// Source geometry and the logical request remain explicit. This is not a rebased heightfield.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTerrainWindow(int Version,string AlgorithmModel,string ImplementationId,string SourceRecipeSha256,
    WorldTerrainRecipe Source,WorldTerrainWindowRequest Request,int FirstColumn,int FirstRow,int Columns,int Rows,
    IReadOnlyList<double> Heights,long Work);

public sealed partial class WorldTerrainGenerator
{
    private const string WindowModel="original-lattice-terrain-window-v1";
    internal static string WindowImplementation=>typeof(WorldTerrainGenerator).Module.ModuleVersionId.ToString("D");

    internal (WorldTerrainRecipe Source,IWorldHeightFieldQuery Query,WorldHeightFieldWindow Bounds) GenerateRegionalQuery(
        WorldTerrainRecipe recipe,double minX,double minY,double maxX,double maxY,CancellationToken token)
    {
        var prepared=Prepare(recipe,token);var geometry=WorldHeightFieldQuery.Describe(Header(prepared.Recipe));
        var bounds=WorldHeightFieldWindow.For(geometry,minX,minY,maxX,maxY);
        if(bounds.LastColumn<bounds.FirstColumn||bounds.LastRow<bounds.FirstRow)
            return(prepared.Recipe,WorldTerrainWindowQuery.WithoutSamples(prepared.Recipe),bounds);
        var window=GenerateWindow(prepared.Recipe,new(1,bounds.FirstColumn,bounds.FirstRow,
            bounds.LastColumn-bounds.FirstColumn+2,bounds.LastRow-bounds.FirstRow+2,0),token);
        return(window.Source,WorldTerrainWindowQuery.FromGenerated(window),bounds);
    }

    // Same original-lattice bounds and sample cost as GenerateRegionalQuery,
    // without generating a field before an enclosing regional budget is charged.
    internal static long RegionalGenerationWork(WorldTerrainRecipe recipe,double minX,double minY,double maxX,double maxY,CancellationToken token)
    {
        var prepared=Prepare(recipe,token);var geometry=WorldHeightFieldQuery.Describe(Header(prepared.Recipe));
        var bounds=WorldHeightFieldWindow.For(geometry,minX,minY,maxX,maxY);
        if(bounds.LastColumn<bounds.FirstColumn||bounds.LastRow<bounds.FirstRow)return 0;
        long samples=(long)(bounds.LastColumn-bounds.FirstColumn+2)*(bounds.LastRow-bounds.FirstRow+2);
        long work=prepared.SampleCost*samples;
        if(work>prepared.Recipe.MaximumWork)throw new ArgumentException("WORLD_TERRAIN_WORK_BUDGET");
        return work;
    }

    private static (int X,int Y,int Columns,int Rows) WindowBounds(WorldTerrainRecipe source,WorldTerrainWindowRequest request)
    {
        if(request is null||request.Version!=1||request.FirstColumn<0||request.FirstRow<0||request.Columns<2||request.Rows<2||
            request.HaloCells is <0 or >1||(long)request.FirstColumn+request.Columns>source.Columns||
            (long)request.FirstRow+request.Rows>source.Rows)
            throw new ArgumentException("WORLD_TERRAIN_WINDOW_REQUEST_INVALID");
        int x=Math.Max(0,request.FirstColumn-request.HaloCells),y=Math.Max(0,request.FirstRow-request.HaloCells);
        return(x,y,Math.Min(source.Columns,request.FirstColumn+request.Columns+request.HaloCells)-x,
            Math.Min(source.Rows,request.FirstRow+request.Rows+request.HaloCells)-y);
    }

    /// <summary>Computes only the selected original-lattice samples. Each request has
    /// its own declared recipe work bound; halos count as computed samples.</summary>
    public WorldTerrainWindow GenerateWindow(WorldTerrainRecipe recipe,WorldTerrainWindowRequest request,CancellationToken token=default)
    {
        var prepared=Prepare(recipe,token);var bounds=WindowBounds(prepared.Recipe,request);
        long work=prepared.SampleCost*bounds.Columns*bounds.Rows;
        if(work>prepared.Recipe.MaximumWork)throw new ArgumentException("WORLD_TERRAIN_WORK_BUDGET");
        WorldHeightFieldQuery.ValidateHeader(Header(prepared.Recipe));
        var values=GenerateSamples(prepared,bounds.X,bounds.Y,bounds.Columns,bounds.Rows,token);
        return new(1,WindowModel,WindowImplementation,WorldGenerationDependencyIdentity.Digest(prepared.Recipe),prepared.Recipe,request,
            bounds.X,bounds.Y,bounds.Columns,bounds.Rows,Array.AsReadOnly(values),work);
    }

    /// <summary>Replays saved samples through the current implementation and returns
    /// a detached verified window. Digests establish consistency, not external authenticity.</summary>
    public WorldTerrainWindow ReplayWindow(WorldTerrainWindow window,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(window is null||window.Heights is null||window.Heights.Count>513*513)
            throw new ArgumentException("WORLD_TERRAIN_WINDOW_INVALID");
        if(window.Version!=1||window.AlgorithmModel!=WindowModel||window.ImplementationId!=WindowImplementation)
            throw new ArgumentException("WORLD_TERRAIN_WINDOW_REPLAY_MISMATCH");
        var saved=window.Heights.ToArray();
        var expected=GenerateWindow(window.Source,window.Request,token);
        if(window.SourceRecipeSha256!=expected.SourceRecipeSha256||window.FirstColumn!=expected.FirstColumn||window.FirstRow!=expected.FirstRow||
            window.Columns!=expected.Columns||window.Rows!=expected.Rows||window.Work!=expected.Work||saved.Length!=expected.Heights.Count)
            throw new ArgumentException("WORLD_TERRAIN_WINDOW_REPLAY_MISMATCH");
        for(int i=0;i<saved.Length;i++)
        {
            token.ThrowIfCancellationRequested();
            if(BitConverter.DoubleToInt64Bits(saved[i])!=BitConverter.DoubleToInt64Bits(expected.Heights[i]))
                throw new ArgumentException("WORLD_TERRAIN_WINDOW_REPLAY_MISMATCH");
        }
        return expected;
    }

    /// <summary>Reconstructs a complete bounded field from replayed windows in any
    /// order. Missing coverage never becomes default terrain. This explicitly
    /// allocates the final field; individual GenerateWindow requests do not.</summary>
    public WorldHeightField AssembleWindows(WorldTerrainRecipe recipe,IEnumerable<WorldTerrainWindow> windows,
        long maximumWork=20000000,CancellationToken token=default)
    {
        var prepared=Prepare(recipe,token);var source=prepared.Recipe;var header=Header(source);
        WorldHeightFieldQuery.ValidateHeader(header);
        if(windows is null||maximumWork is <1 or >200000000)throw new ArgumentException("WORLD_TERRAIN_WINDOW_ASSEMBLY_INVALID");
        var budget=new WorldSpatialWorkBudget(Math.Min(maximumWork,source.MaximumWork),token,
            failureCode:"WORLD_TERRAIN_WINDOW_ASSEMBLY_WORK_BUDGET");
        var digest=WorldGenerationDependencyIdentity.Digest(source);
        var values=new double[source.Columns*source.Rows];var assigned=new bool[values.Length];int remaining=values.Length,count=0;
        using var enumerator=windows.GetEnumerator();
        while(true)
        {
            token.ThrowIfCancellationRequested();if(!enumerator.MoveNext())break;
            if(++count>4096)throw new ArgumentException("WORLD_TERRAIN_WINDOW_COUNT_LIMIT");
            var window=enumerator.Current;
            if(window is null||window.SourceRecipeSha256!=digest)throw new ArgumentException("WORLD_TERRAIN_WINDOW_SOURCE_MISMATCH");
            var bounds=WindowBounds(source,window.Request);
            budget.Charge((prepared.SampleCost+1)*bounds.Columns*bounds.Rows);
            var replayed=ReplayWindow(window,token);
            for(int y=0;y<replayed.Rows;y++)for(int x=0;x<replayed.Columns;x++)
            {
                token.ThrowIfCancellationRequested();int index=(replayed.FirstRow+y)*source.Columns+replayed.FirstColumn+x;
                double value=replayed.Heights[y*replayed.Columns+x];
                if(assigned[index]&&BitConverter.DoubleToInt64Bits(values[index])!=BitConverter.DoubleToInt64Bits(value))
                    throw new ArgumentException("WORLD_TERRAIN_WINDOW_SEAM_MISMATCH");
                if(!assigned[index]){assigned[index]=true;remaining--;values[index]=value;}
            }
        }
        if(remaining!=0)
        {
            var error=new ArgumentException("WORLD_TERRAIN_WINDOW_COVERAGE_INCOMPLETE");
            error.Data["MissingSamples"]=remaining;throw error;
        }
        return header with{Heights=Array.AsReadOnly(values)};
    }
}
