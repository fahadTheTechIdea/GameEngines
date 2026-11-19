using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Operations;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataChunkGeometryFile(string ChunkId,string Sha256,int Bytes,int Members);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataChunkGeometryManifest(int Version,string SourceWorldSha256,string ChunkPlanSha256,
    IReadOnlyList<MetadataChunkGeometryFile> Chunks);
internal sealed record MetadataChunkGeometryPacket(int Version,string ChunkId,IReadOnlyList<MetadataChunkGeometry> Members);
public sealed record MetadataChunkGeometryPublication(string ManifestSha256,string SourceWorldSha256,string ChunkPlanSha256,int Chunks);
public sealed class MetadataChunkGeometryReportingException(string outputDirectory,string manifestSha256,Exception inner)
    :Exception("METADATA_CHUNK_GEOMETRY_REPORTING_AFTER_COMMIT",inner)
{public string OutputDirectory {get;}=outputDirectory;public string ManifestSha256 {get;}=manifestSha256;}

/// <summary>Publishes bounded geometry packets from one already-generated world.
/// The returned manifest digest must be stored in a trusted place by the caller.</summary>
public sealed class MetadataChunkGeometryPackage(MetadataChunkPlanner planner,WorldOperationReporter reporter)
{
    internal const int Version=4;
    internal const long MaximumPackageBytes=268435456;
    internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string Name(int ordinal)=>ordinal.ToString("D6",System.Globalization.CultureInfo.InvariantCulture)+".json";
    internal static void NoLink(string path)
    {if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("METADATA_CHUNK_PACKAGE_LINK_REFUSED");}

    public async Task<MetadataChunkGeometryPublication> PublishAsync(WorldMetadataDocument world,string newOutputDirectory,
        long maximumWork=20000000,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(planner);ArgumentNullException.ThrowIfNull(reporter);ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(newOutputDirectory);
        string target=Path.GetFullPath(newOutputDirectory),correlation=Guid.NewGuid().ToString("N");
        string? staging=null,parent=null;Exception? failure=null;bool committed=false;
        Task Event(string id,string outcome,string message,OperationSeverity severity,CancellationToken cancellation)
            =>reporter.ReportAsync(new(id,DateTimeOffset.UtcNow,severity,nameof(MetadataChunkGeometryPackage),
                "world.chunk-geometry",correlation,message,Outcome:outcome),severity>=OperationSeverity.Warning,cancellation);
        try
        {
            token.ThrowIfCancellationRequested();
            if(Directory.Exists(target)||File.Exists(target))throw new IOException("METADATA_CHUNK_PACKAGE_OUTPUT_EXISTS");
            var snapshot=await MetadataJsonStorage.SnapshotAsync(world,token);
            var plan=snapshot.ChunkPlan??throw new ArgumentException("METADATA_CHUNK_PLAN_REQUIRED");
            await Event("world.chunk-geometry.started","started","Chunk geometry publication started.",OperationSeverity.Information,token);
            var replay=planner.Plan(plan.Definition,snapshot,token);
            if(!MetadataPopulationSources.Equal(replay,plan))throw new ArgumentException("METADATA_CHUNK_PLAN_MISMATCH");
            var index=planner.CreateGeometryIndex(snapshot,maximumWork,token);
            parent=Path.GetDirectoryName(target)??throw new ArgumentException("METADATA_CHUNK_PACKAGE_OUTPUT_INVALID");
            Directory.CreateDirectory(parent);staging=Path.Combine(parent,".chunk-geometry-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
            var (publication,_)=await WriteStagedAsync(index,plan.Cells.Select(c=>c.Id).ToArray(),staging,token);
            string manifestHash=publication.ManifestSha256;
            await MetadataDirectoryPublication.CommitAsync(staging,target,t=>t.ThrowIfCancellationRequested(),token);
            committed=true;
            try{await Event("world.chunk-geometry.published","published","Chunk geometry published.",OperationSeverity.Information,CancellationToken.None);}
            catch(Exception error){throw new MetadataChunkGeometryReportingException(target,manifestHash,error);}
            return publication;
        }
        catch(MetadataChunkGeometryReportingException)when(committed){throw;}
        catch(Exception error)
        {
            failure=error;
            if(!committed)try{await Event(error is OperationCanceledException?"world.chunk-geometry.cancelled":"world.chunk-geometry.failed",
                error is OperationCanceledException?"cancelled":"failed","Chunk geometry was not published.",OperationSeverity.Warning,CancellationToken.None);}
            catch(Exception reporting){throw new AggregateException("METADATA_CHUNK_PACKAGE_AND_REPORTING_FAILED",error,reporting);}
            throw;
        }
        finally{if(staging is not null)MetadataDirectoryPublication.Cleanup(staging,parent!,".chunk-geometry-",failure);}
    }

    // The caller owns this unpublished directory, its commit and operation events.
    internal static async Task<(MetadataChunkGeometryPublication Publication,long Bytes)> WriteStagedAsync(
        MetadataChunkGeometryIndex index,IReadOnlyList<string> chunkIds,string staging,CancellationToken token)
    {
        var records=new List<MetadataChunkGeometryFile>(chunkIds.Count);long total=0;
        for(int i=0;i<chunkIds.Count;i++)
        {
            token.ThrowIfCancellationRequested();string id=chunkIds[i];
            var members=index.Select([id],token);var bytes=await MetadataJsonStorage.InlineAsync(new MetadataChunkGeometryPacket(Version,id,members),token);
            total+=bytes.Length;if(total>MaximumPackageBytes)throw new ArgumentException("METADATA_CHUNK_PACKAGE_SIZE_LIMIT");
            await File.WriteAllBytesAsync(Path.Combine(staging,Name(i)),bytes,token);
            records.Add(new(id,Hash(bytes),bytes.Length,members.Count));
        }
        var manifest=new MetadataChunkGeometryManifest(Version,index.SourceWorldSha256,index.ChunkPlanSha256,records.AsReadOnly());
        var manifestBytes=await MetadataJsonStorage.InlineAsync(manifest,token);string manifestHash=Hash(manifestBytes);
        await File.WriteAllBytesAsync(Path.Combine(staging,"manifest.json"),manifestBytes,token);
        return(new(manifestHash,index.SourceWorldSha256,index.ChunkPlanSha256,records.Count),total+manifestBytes.Length);
    }

    /// <summary>Opens a manifest pinned by the caller. Hashes detect changes to
    /// the package; they do not authenticate the author or validate shared terrain.</summary>
    public static async Task<MetadataChunkGeometryReader> OpenAsync(string directory,string expectedManifestSha256,CancellationToken token=default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if(!MetadataJsonStorage.Hash(expectedManifestSha256))throw new ArgumentException("METADATA_CHUNK_PACKAGE_PIN_INVALID");
        string root=Path.GetFullPath(directory);NoLink(root);
        var bytes=await MetadataChunkGeometryReader.ReadBytesAsync(Path.Combine(root,"manifest.json"),MetadataJsonStorage.InlineBytes,token);
        if(Hash(bytes)!=expectedManifestSha256)throw new ArgumentException("METADATA_CHUNK_PACKAGE_MANIFEST_MISMATCH");
        var manifest=MetadataChunkGeometryReader.Parse<MetadataChunkGeometryManifest>(bytes);
        if(manifest.Version!=Version||!MetadataJsonStorage.Hash(manifest.SourceWorldSha256)||!MetadataJsonStorage.Hash(manifest.ChunkPlanSha256)||
            manifest.Chunks is null||manifest.Chunks.Count>16384||manifest.Chunks.Any(c=>c is null||string.IsNullOrWhiteSpace(c.ChunkId)||c.ChunkId.Length>64||
            !MetadataJsonStorage.Hash(c.Sha256)||c.Bytes is <1 or >MetadataJsonStorage.InlineBytes||c.Members is <1 or >200000)||
            manifest.Chunks.Select(c=>c.ChunkId).Distinct(StringComparer.Ordinal).Count()!=manifest.Chunks.Count||
            manifest.Chunks.Sum(c=>(long)c.Bytes)>MaximumPackageBytes)throw new ArgumentException("METADATA_CHUNK_PACKAGE_MANIFEST_INVALID");
        return new(root,manifest,expectedManifestSha256);
    }
}

/// <summary>Reads only requested pre-generated geometry packets; shared world
/// semantics, terrain and source verification remain the caller's responsibility.</summary>
public sealed class MetadataChunkGeometryReader
{
    private readonly string root;private readonly MetadataChunkGeometryManifest manifest;
    private readonly Dictionary<string,int> indices;
    internal MetadataChunkGeometryReader(string root,MetadataChunkGeometryManifest manifest,string digest)
    {this.root=root;this.manifest=manifest;ManifestSha256=digest;indices=manifest.Chunks.Select((c,i)=>(c.ChunkId,i)).ToDictionary(x=>x.ChunkId,x=>x.i,StringComparer.Ordinal);}
    public string ManifestSha256 {get;}
    public string SourceWorldSha256=>manifest.SourceWorldSha256;
    public string ChunkPlanSha256=>manifest.ChunkPlanSha256;
    public IReadOnlyList<string> ChunkIds=>Array.AsReadOnly(manifest.Chunks.Select(c=>c.ChunkId).ToArray());

    internal long SelectedBytes(IReadOnlyList<string> chunkIds,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(chunkIds is null||chunkIds.Count>16384)throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");
        var selected=new HashSet<int>();long bytes=0;
        foreach(var id in chunkIds)
        {
            token.ThrowIfCancellationRequested();
            if(id is null||!indices.TryGetValue(id,out int ordinal))throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");
            if(selected.Add(ordinal))bytes=checked(bytes+manifest.Chunks[ordinal].Bytes);
        }
        return bytes;
    }

    internal static T Parse<T>(byte[] bytes)
    {
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=32,AllowDuplicateProperties=false});
        return doc.RootElement.Deserialize<T>(MetadataJsonStorage.Options())??throw new ArgumentException("METADATA_CHUNK_PACKAGE_DOCUMENT_INVALID");
    }
    internal static async Task<byte[]> ReadBytesAsync(string file,int maximum,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();MetadataChunkGeometryPackage.NoLink(file);
        await using var stream=File.OpenRead(file);
        if(stream.Length is <=0||stream.Length>maximum)throw new ArgumentException("METADATA_CHUNK_PACKAGE_FILE_SIZE_INVALID");
        var bytes=new byte[checked((int)stream.Length)];await stream.ReadExactlyAsync(bytes,token);
        if(stream.Length!=bytes.Length)throw new ArgumentException("METADATA_CHUNK_PACKAGE_FILE_CHANGED");return bytes;
    }
    public async Task<IReadOnlyList<MetadataChunkGeometry>> ReadAsync(IReadOnlyList<string> chunkIds,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();if(chunkIds is null||chunkIds.Count>16384)throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");
        var selected=new SortedSet<int>();foreach(var id in chunkIds)
        {token.ThrowIfCancellationRequested();if(id is null||!indices.TryGetValue(id,out int i))throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");selected.Add(i);}
        var result=new Dictionary<(MetadataSpatialMemberKind,string),MetadataChunkGeometry>();
        MetadataChunkGeometryPackage.NoLink(root);
        foreach(int ordinal in selected)
        {
            token.ThrowIfCancellationRequested();var descriptor=manifest.Chunks[ordinal];
            var bytes=await ReadBytesAsync(Path.Combine(root,MetadataChunkGeometryPackage.Name(ordinal)),descriptor.Bytes,token);
            if(bytes.Length!=descriptor.Bytes||MetadataChunkGeometryPackage.Hash(bytes)!=descriptor.Sha256)throw new ArgumentException("METADATA_CHUNK_PACKAGE_DATA_MISMATCH");
            var packet=Parse<MetadataChunkGeometryPacket>(bytes);
            if(packet.Version!=MetadataChunkGeometryPackage.Version||packet.ChunkId!=descriptor.ChunkId||packet.Members is null||packet.Members.Count!=descriptor.Members)
                throw new ArgumentException("METADATA_CHUNK_PACKAGE_DATA_INVALID");
            foreach(var item in packet.Members)
            {
                token.ThrowIfCancellationRequested();
                if(item?.Member is not {} member||member.Id is null||member.ChunkIds is null||!member.ChunkIds.Contains(descriptor.ChunkId)||item.Coverage is null||
                    !Enum.IsDefined(member.Kind)||member.Kind==MetadataSpatialMemberKind.Node&&(item.Node is not {} node||node.Id!=member.Id||
                        node.Properties is null||node.Position is not {Count:2}||node.Position[0]!=item.Anchor.X||node.Position[1]!=item.Anchor.Y)||
                    item.Connections is null||item.Connections.Any(c=>c is null||string.IsNullOrWhiteSpace(c.Id)||string.IsNullOrWhiteSpace(c.FromId)||string.IsNullOrWhiteSpace(c.ToId)||c.Properties is null||
                        c.FromId!=member.Id&&c.ToId!=member.Id)||item.Connections.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=item.Connections.Count||
                    member.Kind==MetadataSpatialMemberKind.Path&&(item.Node is not null||item.Connections.Count!=0))
                    throw new ArgumentException("METADATA_CHUNK_PACKAGE_DATA_INVALID");
                MetadataChunkGeometrySnapshot.ValidateInteriors([item],token);
                var key=(member.Kind,member.Id);
                if(result.TryGetValue(key,out var existing))
                {if(!MetadataPopulationSources.Equal(existing,item))throw new ArgumentException("METADATA_CHUNK_PACKAGE_MEMBER_MISMATCH");}
                else result.Add(key,Freeze(item));
            }
        }
        var relationships=new Dictionary<string,WorldMetadataConnection>(StringComparer.Ordinal);
        var loadedNodes=result.Values.Where(g=>g.Node is not null).ToDictionary(g=>g.Member.Id,StringComparer.Ordinal);
        var nodeLinks=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        foreach(var item in loadedNodes.Values)
        {
            token.ThrowIfCancellationRequested();nodeLinks.Add(item.Member.Id,item.Connections.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal));
            foreach(var connection in item.Connections)
            {
                token.ThrowIfCancellationRequested();
                if(relationships.TryGetValue(connection.Id,out var prior))
                {if(!MetadataPopulationSources.Equal(prior,connection))throw new ArgumentException("METADATA_CHUNK_PACKAGE_CONNECTION_MISMATCH");}
                else relationships.Add(connection.Id,connection);
            }
        }
        foreach(var connection in relationships.Values)
        {
            token.ThrowIfCancellationRequested();
            foreach(string id in new[]{connection.FromId,connection.ToId}.Distinct(StringComparer.Ordinal))
                if(nodeLinks.TryGetValue(id,out var links)&&!links.Contains(connection.Id))
                    throw new ArgumentException("METADATA_CHUNK_PACKAGE_CONNECTION_MISMATCH");
        }
        MetadataChunkGeometrySnapshot.ValidateInteriors(result.Values,token);
        return Array.AsReadOnly(result.Values.OrderBy(v=>v.Member.Kind).ThenBy(v=>v.Member.Id,StringComparer.Ordinal).ToArray());
    }
    private static MetadataChunkGeometry Freeze(MetadataChunkGeometry item)
    {
        var coverage=item.Coverage.Select(c=>c with{Polygon=new WorldPolygon(Array.AsReadOnly(c.Polygon.Vertices.ToArray()))}).ToArray();
        var path=item.Path is null?null:new WorldPolyline(Array.AsReadOnly(item.Path.Points.ToArray()));
        return item with{Member=item.Member with{ChunkIds=Array.AsReadOnly(item.Member.ChunkIds.ToArray())},Coverage=Array.AsReadOnly(coverage),Path=path,
            Node=item.Node is null?null:MetadataChunkGeometrySnapshot.CopyNode(item.Node),
            Connections=Array.AsReadOnly(item.Connections.Select(MetadataChunkGeometrySnapshot.CopyConnection).ToArray()),
            Interiors=item.Interiors is null?null:Array.AsReadOnly(item.Interiors.Select(MetadataChunkGeometrySnapshot.CopyInterior).ToArray())};
    }
}
