using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataDomainTerrainFile(string ChunkId,string Sha256,int Bytes);
public sealed record MetadataDomainChunkPackageDomain(string DomainId,bool HasChunkPlan,string? ManifestSha256,
    string? SourceWorldSha256,string? ChunkPlanSha256,int ChunkCount,IReadOnlyList<MetadataDomainTerrainFile> Terrain);
public sealed record MetadataDomainChunkPackageManifest(int Version,string SourceSha256,string SharedSha256,int SharedBytes,
    IReadOnlyList<MetadataDomainChunkPackageDomain> Domains);
public sealed record MetadataDomainChunkPublication(string ManifestSha256,string SourceSha256,int Domains,int Chunks);
public sealed class MetadataDomainChunkReportingException(string outputDirectory,string manifestSha256,Exception inner)
    :Exception("METADATA_DOMAIN_CHUNK_REPORTING_AFTER_COMMIT",inner)
{public string OutputDirectory {get;}=outputDirectory;public string ManifestSha256 {get;}=manifestSha256;public bool Committed=>true;}

/// <summary>Atomically publishes an already validated domain index. The caller
/// must retain the returned manifest hash as a trusted integrity pin.</summary>
public sealed class MetadataDomainChunkPackage(WorldOperationReporter reporter)
{
    private static string Folder(int index)=>"domain-"+index.ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
    internal static string TerrainName(int index)=>"terrain-"+index.ToString("D6",System.Globalization.CultureInfo.InvariantCulture)+".json";
    public async Task<MetadataDomainChunkPublication> PublishAsync(MetadataDomainChunkIndex index,string outputDirectory,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(index);ArgumentNullException.ThrowIfNull(reporter);ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        string target=Path.GetFullPath(outputDirectory),correlation=Guid.NewGuid().ToString("N");string? staging=null,parent=null;Exception? failure=null;bool committed=false;
        Task Event(string id,string outcome,string message,OperationSeverity severity,CancellationToken ct)=>reporter.ReportAsync(new(id,DateTimeOffset.UtcNow,severity,
            nameof(MetadataDomainChunkPackage),"world.domain-chunks",correlation,message,Outcome:outcome),severity>=OperationSeverity.Warning,ct);
        try
        {
            token.ThrowIfCancellationRequested();if(Directory.Exists(target)||File.Exists(target))throw new IOException("METADATA_DOMAIN_CHUNK_PACKAGE_OUTPUT_EXISTS");
            var domains=index.Domains;if(domains.Any(d=>d.HasUnindexedSpatialContent))throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_PLAN_REQUIRED");
            await Event("world.domain-chunks.started","started","Domain chunk publication started.",OperationSeverity.Information,token);
            var shared=index.Select(domains.Select(d=>new MetadataDomainChunkRequest(d.DomainId,[])).ToArray(),token);
            var sharedBytes=await MetadataJsonStorage.InlineAsync(shared,token);long total=sharedBytes.Length;
            parent=Path.GetDirectoryName(target)??throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_OUTPUT_INVALID");
            Directory.CreateDirectory(parent);staging=Path.Combine(parent,".domain-chunks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
            await File.WriteAllBytesAsync(Path.Combine(staging,"shared.json"),sharedBytes,token);
            var records=new List<MetadataDomainChunkPackageDomain>();int count=0;
            var terrainBudget=new WorldSpatialWorkBudget(20000000,token,failureCode:"METADATA_DOMAIN_CHUNK_TERRAIN_WORK_BUDGET");
            for(int i=0;i<domains.Count;i++)
            {
                token.ThrowIfCancellationRequested();var domain=domains[i];var local=index.LocalIndex(domain.DomainId);
                if(local is null){records.Add(new(domain.DomainId,false,null,null,null,0,[]));continue;}
                string folder=Path.Combine(staging,Folder(i));Directory.CreateDirectory(folder);
                var (published,bytes)=await MetadataChunkGeometryPackage.WriteStagedAsync(local,domain.ChunkIds,folder,token);
                total=checked(total+bytes);if(total>MetadataChunkGeometryPackage.MaximumPackageBytes)throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SIZE_LIMIT");
                var terrain=new List<MetadataDomainTerrainFile>();
                if(shared.Domains[i].Surface is not null)for(int j=0;j<domain.ChunkIds.Count;j++)
                {
                    var record=index.Terrain(domain.DomainId,domain.ChunkIds[j],terrainBudget,token);
                    var terrainBytes=await MetadataJsonStorage.InlineAsync(record,token);total=checked(total+terrainBytes.Length);
                    if(total>MetadataChunkGeometryPackage.MaximumPackageBytes)throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SIZE_LIMIT");
                    await File.WriteAllBytesAsync(Path.Combine(folder,TerrainName(j)),terrainBytes,token);
                    terrain.Add(new(record.ChunkId,MetadataChunkGeometryPackage.Hash(terrainBytes),terrainBytes.Length));
                }
                count=checked(count+published.Chunks);records.Add(new(domain.DomainId,true,published.ManifestSha256,published.SourceWorldSha256,published.ChunkPlanSha256,published.Chunks,terrain.AsReadOnly()));
            }
            var manifest=new MetadataDomainChunkPackageManifest(2,index.SourceSha256,MetadataChunkGeometryPackage.Hash(sharedBytes),sharedBytes.Length,records.AsReadOnly());
            var bytesManifest=await MetadataJsonStorage.InlineAsync(manifest,token);string pin=MetadataChunkGeometryPackage.Hash(bytesManifest);
            if(total+bytesManifest.Length>MetadataChunkGeometryPackage.MaximumPackageBytes)throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SIZE_LIMIT");
            await File.WriteAllBytesAsync(Path.Combine(staging,"manifest.json"),bytesManifest,token);
            await MetadataDirectoryPublication.CommitAsync(staging,target,ct=>ct.ThrowIfCancellationRequested(),token);committed=true;
            try{await Event("world.domain-chunks.published","published","Domain chunks published.",OperationSeverity.Information,CancellationToken.None);}
            catch(Exception error){throw new MetadataDomainChunkReportingException(target,pin,error);}
            return new(pin,index.SourceSha256,domains.Count,count);
        }
        catch(MetadataDomainChunkReportingException)when(committed){throw;}
        catch(Exception error)
        {
            failure=error;
            if(!committed)try{await Event(error is OperationCanceledException?"world.domain-chunks.cancelled":"world.domain-chunks.failed",
                error is OperationCanceledException?"cancelled":"failed","Domain chunks were not published.",OperationSeverity.Warning,CancellationToken.None);}
            catch(Exception reporting){throw new AggregateException("METADATA_DOMAIN_CHUNK_PACKAGE_AND_REPORTING_FAILED",error,reporting);}
            throw;
        }
        finally{if(staging is not null)MetadataDirectoryPublication.Cleanup(staging,parent!,".domain-chunks-",failure);}
    }

    public static async Task<MetadataDomainChunkReader> OpenAsync(string directory,string expectedManifestSha256,CancellationToken token=default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if(!MetadataJsonStorage.Hash(expectedManifestSha256))throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_PIN_INVALID");
        string root=Path.GetFullPath(directory);MetadataChunkGeometryPackage.NoLink(root);
        var bytes=await MetadataChunkGeometryReader.ReadBytesAsync(Path.Combine(root,"manifest.json"),MetadataJsonStorage.InlineBytes,token);
        if(MetadataChunkGeometryPackage.Hash(bytes)!=expectedManifestSha256)throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_MANIFEST_MISMATCH");
        var manifest=MetadataChunkGeometryReader.Parse<MetadataDomainChunkPackageManifest>(bytes);
        if(manifest.Version!=2||!MetadataJsonStorage.Hash(manifest.SourceSha256)||!MetadataJsonStorage.Hash(manifest.SharedSha256)||
            manifest.SharedBytes is <1 or >MetadataJsonStorage.InlineBytes||manifest.Domains is not {Count:>=2 and <=16}||
            manifest.Domains.Any(d=>d is null||!WorldEnvironmentQuery.Id(d.DomainId)||d.ChunkCount is <0 or >16384||d.Terrain is null||
                d.Terrain.Count>d.ChunkCount||d.Terrain.Any(t=>t is null||string.IsNullOrWhiteSpace(t.ChunkId)||t.ChunkId.Length>64||
                    !MetadataJsonStorage.Hash(t.Sha256)||t.Bytes is <1 or >MetadataJsonStorage.InlineBytes)||
                d.Terrain.Select(t=>t.ChunkId).Distinct(StringComparer.Ordinal).Count()!=d.Terrain.Count||
                (d.HasChunkPlan?(!MetadataJsonStorage.Hash(d.ManifestSha256)||!MetadataJsonStorage.Hash(d.SourceWorldSha256)||!MetadataJsonStorage.Hash(d.ChunkPlanSha256)):
                    (d.ManifestSha256 is not null||d.SourceWorldSha256 is not null||d.ChunkPlanSha256 is not null||d.ChunkCount!=0||d.Terrain.Count!=0)))||
            manifest.Domains.Select(d=>d.DomainId).Distinct(StringComparer.Ordinal).Count()!=manifest.Domains.Count||
            !manifest.Domains.Select(d=>d.DomainId).SequenceEqual(manifest.Domains.Select(d=>d.DomainId).Order(StringComparer.Ordinal))||
            manifest.SharedBytes+manifest.Domains.Sum(d=>d.Terrain.Sum(t=>(long)t.Bytes))>MetadataChunkGeometryPackage.MaximumPackageBytes)
            throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_MANIFEST_INVALID");
        var sharedBytes=await MetadataChunkGeometryReader.ReadBytesAsync(Path.Combine(root,"shared.json"),manifest.SharedBytes,token);
        if(sharedBytes.Length!=manifest.SharedBytes||MetadataChunkGeometryPackage.Hash(sharedBytes)!=manifest.SharedSha256)
            throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SHARED_MISMATCH");
        var shared=MetadataChunkGeometryReader.Parse<MetadataDomainChunkSelection>(sharedBytes);
        static bool InvalidDomain(MetadataDomainChunkContent? d)
        {
            if(d is null||d.ChunkIds is not {Count:0}||d.Geometry is not {Count:0}||d.Terrain is not {Count:0}||d.SharedNodes is null||d.SharedConnections is null||
                d.SharedNodes.Any(n=>n is null||!WorldEnvironmentQuery.Text(n.Id)||n.Properties is null||n.Position is {Count:2}))return true;
            var nodeIds=d.SharedNodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
            return nodeIds.Count!=d.SharedNodes.Count||d.SharedConnections.Any(c=>c is null||!WorldEnvironmentQuery.Text(c.Id)||
                !WorldEnvironmentQuery.Text(c.FromId)||!WorldEnvironmentQuery.Text(c.ToId)||c.Properties is null||
                !nodeIds.Contains(c.FromId)&&!nodeIds.Contains(c.ToId))||
                d.SharedConnections.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=d.SharedConnections.Count;
        }
        static bool InvalidTransition(MetadataDomainTransitionResult? t)=>t?.Definition is null||t.From?.Definition is null||t.To?.Definition is null||
            !WorldEnvironmentQuery.Id(t.Definition.Id)||!WorldEnvironmentQuery.Id(t.Definition.Kind)||
            !MetadataPopulationSources.Equal(t.Definition.From,t.From.Definition)||!MetadataPopulationSources.Equal(t.Definition.To,t.To.Definition);
        static bool InvalidRegionalRoute(MetadataRegionalDomainRoute? route,IReadOnlyList<MetadataDomainTransitionResult> links)
        {
            if(route?.Forward?.Status!=WorldRouteStatus.Found||route.Forward.Path?.Points is not {Count:>=2} points)return true;
            var link=links.SingleOrDefault(t=>t.Definition.Id==route.TransitionId);
            if(link?.From.Position is not {Count:2} a||link.To.Position is not {Count:2} b)return true;
            bool Equal(WorldCreator.Core.Geometry.WorldPoint point,IReadOnlyList<double> position)=>point.X==position[0]&&point.Y==position[1];
            if(!Equal(points[0],a)||!Equal(points[^1],b))return true;
            if(link.Definition.Bidirectional&&(route.Reverse?.Status!=WorldRouteStatus.Found||route.Reverse.Path?.Points is not {Count:>=2})||
                !link.Definition.Bidirectional&&route.Reverse is not null)return true;
            var reverse=route.Reverse?.Path?.Points;
            return reverse is not null&&(reverse.Count<2||!Equal(reverse[0],b)||!Equal(reverse[^1],a));
        }
        if(shared.SourceSha256!=manifest.SourceSha256||shared.Domains is null||shared.SharedTransitions is null||shared.SharedRegionalRoutes is null||
            !shared.Domains.Select(d=>d?.DomainId).SequenceEqual(manifest.Domains.Select(d=>d.DomainId))||
            shared.Domains.Any(InvalidDomain)||
            shared.SharedTransitions.Any(t=>InvalidTransition(t)||
                !manifest.Domains.Any(d=>d.DomainId==t.From.Definition.DomainId)||!manifest.Domains.Any(d=>d.DomainId==t.To.Definition.DomainId))||
            shared.SharedTransitions.Select(t=>t.Definition.Id).Distinct(StringComparer.Ordinal).Count()!=shared.SharedTransitions.Count||
            shared.SharedRegionalRoutes.Any(r=>InvalidRegionalRoute(r,shared.SharedTransitions))||
            shared.SharedRegionalRoutes.Select(r=>r.TransitionId).Distinct(StringComparer.Ordinal).Count()!=shared.SharedRegionalRoutes.Count)
            throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SHARED_INVALID");
        for(int i=0;i<manifest.Domains.Count;i++)
        {
            var domain=manifest.Domains[i];var surface=shared.Domains[i].Surface;
            if(surface is not null&&(!WorldEnvironmentQuery.Id(surface.Id)||!WorldEnvironmentQuery.Id(surface.Unit)||
                !MetadataJsonStorage.Hash(surface.SourceSha256)||surface.Exclusions is null||surface.Patches is null||
                surface.AvailableSamples is {} extent&&(surface.Field is not {} field||extent.FirstColumn<0||extent.FirstRow<0||
                    extent.Columns<2||extent.Rows<2||(long)extent.FirstColumn+extent.Columns>field.Columns||(long)extent.FirstRow+extent.Rows>field.Rows)))
                throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SHARED_INVALID");
            if(surface?.Field is {} header)
            {
                try{WorldHeightFieldQuery.ValidateHeader(new(header.SchemaVersion,header.Id,header.Kind,header.Purpose,header.SourceRuleId,
                    header.Unit,header.Origin,header.StepX,header.StepY,header.Columns,header.Rows,[]));}
                catch(ArgumentException){throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SHARED_INVALID");}
                if(header.Unit!=surface.Unit)throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SHARED_INVALID");
            }
            if(domain.Terrain.Count!=(surface is null?0:domain.ChunkCount))throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_MANIFEST_INVALID");
        }
        return new(root,expectedManifestSha256,manifest,shared);
    }

    internal static string DomainFolder(int ordinal)=>Folder(ordinal);
}

/// <summary>Reads requested pre-generated domain chunks; shared graph data is loaded at open.</summary>
public sealed class MetadataDomainChunkReader
{
    private readonly string root;private readonly MetadataDomainChunkPackageManifest manifest;private readonly MetadataDomainChunkSelection shared;
    private readonly Dictionary<string,int> ids;
    internal MetadataDomainChunkReader(string root,string pin,MetadataDomainChunkPackageManifest manifest,MetadataDomainChunkSelection shared)
    {this.root=root;ManifestSha256=pin;this.manifest=manifest;this.shared=shared;ids=manifest.Domains.Select((d,i)=>(d.DomainId,i)).ToDictionary(p=>p.DomainId,p=>p.i,StringComparer.Ordinal);}
    public string ManifestSha256 {get;}
    public string SourceSha256=>manifest.SourceSha256;
    public IReadOnlyList<string> DomainIds=>Array.AsReadOnly(manifest.Domains.Select(d=>d.DomainId).ToArray());

    private async Task<MetadataChunkGeometryReader?> LocalAsync(string domainId,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(domainId is null||!ids.TryGetValue(domainId,out int ordinal))throw new ArgumentException("METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        var source=manifest.Domains[ordinal];if(!source.HasChunkPlan)return null;
        MetadataChunkGeometryPackage.NoLink(root);
        var reader=await MetadataChunkGeometryPackage.OpenAsync(Path.Combine(root,MetadataDomainChunkPackage.DomainFolder(ordinal)),source.ManifestSha256!,token);
        if(reader.SourceWorldSha256!=source.SourceWorldSha256||reader.ChunkPlanSha256!=source.ChunkPlanSha256||reader.ChunkIds.Count!=source.ChunkCount)
            throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_CHILD_MISMATCH");
        if(source.Terrain.Count>0&&!source.Terrain.Select(t=>t.ChunkId).SequenceEqual(reader.ChunkIds))
            throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_CHILD_MISMATCH");
        return reader;
    }
    public async Task<IReadOnlyList<string>> GetChunkIdsAsync(string domainId,CancellationToken token=default)
        =>(await LocalAsync(domainId,token))?.ChunkIds??Array.Empty<string>();

    public async Task<MetadataDomainChunkSelection> ReadAsync(IReadOnlyList<MetadataDomainChunkRequest> requests,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(requests is null||requests.Count>16||requests.Any(r=>r is null||r.ChunkIds is null||r.ChunkIds.Count>16384)||
            requests.Select(r=>r.DomainId).Distinct(StringComparer.Ordinal).Count()!=requests.Count)throw new ArgumentException("METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        var selected=new List<MetadataDomainChunkContent>();long total=manifest.SharedBytes;
        foreach(var request in requests.OrderBy(r=>r.DomainId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var local=await LocalAsync(request.DomainId,token);
            if(local is null&&request.ChunkIds.Count>0)throw new ArgumentException("METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
            var chunks=request.ChunkIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if(local is not null&&(total=checked(total+local.SelectedBytes(chunks,token)))>MetadataChunkGeometryPackage.MaximumPackageBytes)
                throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SIZE_LIMIT");
            var geometry=local is null?Array.Empty<MetadataChunkGeometry>():await local.ReadAsync(chunks,token);
            var source=shared.Domains.Single(d=>d.DomainId==request.DomainId);
            var terrain=new List<MetadataDomainTerrainChunk>();var record=manifest.Domains[ids[request.DomainId]];
            var ordinals=local?.ChunkIds.Select((id,i)=>(id,i)).ToDictionary(p=>p.id,p=>p.i,StringComparer.Ordinal);
            foreach(var chunk in chunks)if(source.Surface is not null)
            {
                token.ThrowIfCancellationRequested();
                if(ordinals is null||!ordinals.TryGetValue(chunk,out int ordinal)||ordinal>=record.Terrain.Count)throw new ArgumentException("METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
                var descriptor=record.Terrain[ordinal];total=checked(total+descriptor.Bytes);
                if(total>MetadataChunkGeometryPackage.MaximumPackageBytes)throw new ArgumentException("METADATA_DOMAIN_CHUNK_PACKAGE_SIZE_LIMIT");
                var data=await MetadataChunkGeometryReader.ReadBytesAsync(Path.Combine(root,MetadataDomainChunkPackage.DomainFolder(ids[request.DomainId]),MetadataDomainChunkPackage.TerrainName(ordinal)),descriptor.Bytes,token);
                if(data.Length!=descriptor.Bytes||MetadataChunkGeometryPackage.Hash(data)!=descriptor.Sha256)throw new ArgumentException("METADATA_DOMAIN_CHUNK_TERRAIN_MISMATCH");
                var value=MetadataChunkGeometryReader.Parse<MetadataDomainTerrainChunk>(data);
                if(value.ChunkId!=chunk||value.Window is {} window&&(source.Surface.Field is not {} field||source.Surface.AvailableSamples is not {} available||
                    window.FirstColumn<0||window.FirstRow<0||window.Columns<2||window.Rows<2||
                    (long)window.FirstColumn+window.Columns>field.Columns||(long)window.FirstRow+window.Rows>field.Rows||
                    window.FirstColumn<available.FirstColumn||window.FirstRow<available.FirstRow||
                    (long)window.FirstColumn+window.Columns>(long)available.FirstColumn+available.Columns||
                    (long)window.FirstRow+window.Rows>(long)available.FirstRow+available.Rows||
                    window.Heights is null||window.Heights.Count!=(long)window.Columns*window.Rows||window.Heights.Any(h=>!double.IsFinite(h))))
                    throw new ArgumentException("METADATA_DOMAIN_CHUNK_TERRAIN_INVALID");
                terrain.Add(value);
            }
            selected.Add(new(request.DomainId,chunks,geometry,source.SharedNodes,source.SharedConnections,source.Surface,terrain));
        }
        return MetadataExecutionCache.Clone(new MetadataDomainChunkSelection(manifest.SourceSha256,selected,shared.SharedTransitions,
            shared.SharedRegionalRoutes,shared.SharedJourneys,shared.SharedProgression));
    }
}
