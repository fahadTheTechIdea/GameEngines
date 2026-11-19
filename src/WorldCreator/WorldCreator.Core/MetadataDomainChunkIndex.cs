using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataDomainChunkRequest(string DomainId,IReadOnlyList<string> ChunkIds);
public sealed record MetadataDomainChunkDescriptor(string DomainId,bool HasChunkPlan,bool HasUnindexedSpatialContent,
    IReadOnlyList<string> ChunkIds,int SharedNodeCount,string? SourceWorldSha256,string? ChunkPlanSha256);
public sealed record MetadataDomainTerrainExtent(int FirstColumn,int FirstRow,int Columns,int Rows);
public sealed record MetadataDomainChunkSurface(string Id,string Unit,string SourceSha256,WorldPolygon? Boundary,
    IReadOnlyList<WorldSurfaceExclusion> Exclusions,IReadOnlyList<WorldTerrainPatch> Patches,WorldHeightFieldGeometry? Field,
    MetadataDomainTerrainExtent? AvailableSamples);
public sealed record MetadataDomainTerrainWindow(int FirstColumn,int FirstRow,int Columns,int Rows,IReadOnlyList<double> Heights);
public sealed record MetadataDomainTerrainChunk(string ChunkId,MetadataDomainTerrainWindow? Window);
public sealed record MetadataDomainChunkContent(string DomainId,IReadOnlyList<string> ChunkIds,
    IReadOnlyList<MetadataChunkGeometry> Geometry,IReadOnlyList<WorldMetadataNode> SharedNodes,
    IReadOnlyList<WorldMetadataConnection> SharedConnections,MetadataDomainChunkSurface? Surface,IReadOnlyList<MetadataDomainTerrainChunk> Terrain);
public sealed record MetadataDomainChunkSelection(string SourceSha256,IReadOnlyList<MetadataDomainChunkContent> Domains,
    IReadOnlyList<MetadataDomainTransitionResult> SharedTransitions,IReadOnlyList<MetadataRegionalDomainRoute> SharedRegionalRoutes,
    IReadOnlyList<MetadataDomainJourneyResult>? SharedJourneys,MetadataDomainProgressionPlan? SharedProgression);

/// <summary>Detached selective local geometry with explicit shared world graph dependencies.
/// The source is an already generated world; terrain and native runtime remain external.</summary>
public sealed class MetadataDomainChunkIndex
{
    private sealed record Entry(MetadataDomainChunkDescriptor Descriptor,MetadataChunkGeometryIndex? Index,
        IReadOnlyList<WorldMetadataNode> SharedNodes,IReadOnlyList<WorldMetadataConnection> SharedConnections,
        MetadataDomainChunkSurface? Surface,IWorldHeightFieldQuery? Field,IReadOnlyDictionary<string,MetadataChunkCell> Cells,
        double ChunkWidth,double ChunkHeight);
    private readonly Dictionary<string,Entry> entries;
    private readonly IReadOnlyList<MetadataDomainTransitionResult> transitions;
    private readonly IReadOnlyList<MetadataRegionalDomainRoute> regionalRoutes;
    private readonly IReadOnlyList<MetadataDomainJourneyResult>? journeys;
    private readonly MetadataDomainProgressionPlan? progression;
    public string SourceSha256 {get;}
    public long Work {get;}
    internal MetadataChunkGeometryIndex? LocalIndex(string domainId)=>entries[domainId].Index;
    internal MetadataDomainTerrainChunk Terrain(string domainId,string chunkId,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();budget.Charge();var entry=entries[domainId];
        if(!entry.Cells.TryGetValue(chunkId,out var cell))throw new ArgumentException("METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        if(entry.Field is not {} field)return new(chunkId,null);
        var bounds=WorldHeightFieldWindow.For(field.Geometry,cell.Origin.X,cell.Origin.Y,
            cell.Origin.X+entry.ChunkWidth,cell.Origin.Y+entry.ChunkHeight);
        int firstColumn=bounds.FirstColumn,firstRow=bounds.FirstRow,lastColumn=bounds.LastColumn,lastRow=bounds.LastRow;
        if(field is WorldTerrainWindowQuery bounded)
        {
            if(bounded.Snapshot is not {} saved)return new(chunkId,null);
            firstColumn=Math.Max(firstColumn,saved.FirstColumn);firstRow=Math.Max(firstRow,saved.FirstRow);
            lastColumn=Math.Min(lastColumn,saved.FirstColumn+saved.Columns-2);lastRow=Math.Min(lastRow,saved.FirstRow+saved.Rows-2);
        }
        if(lastColumn<firstColumn||lastRow<firstRow)return new(chunkId,null);
        int columns=lastColumn-firstColumn+2,rows=lastRow-firstRow+2;
        budget.Charge((long)columns*rows);
        var heights=new WorldHeightFieldWindow(firstColumn,firstRow,lastColumn,lastRow).Samples(field,token);
        return new(chunkId,new(firstColumn,firstRow,columns,rows,heights));
    }
    public IReadOnlyList<MetadataDomainChunkDescriptor> Domains=>MetadataExecutionCache.Clone(entries.Values.Select(e=>e.Descriptor).OrderBy(e=>e.DomainId,StringComparer.Ordinal).ToArray());

    private MetadataDomainChunkIndex(string sourceSha256,IReadOnlyList<MetadataGeneratedDomain> domains,
        IReadOnlyList<MetadataDomainTransitionResult> transitions,IReadOnlyList<MetadataRegionalDomainRoute> regionalRoutes,
        IReadOnlyList<MetadataDomainJourneyResult>? journeys,MetadataDomainProgressionPlan? progression,
        MetadataChunkPlanner planner,long maximumWork,CancellationToken token)
    {
        if(maximumWork is <1 or >200000000)throw new ArgumentException("METADATA_DOMAIN_CHUNK_WORK_INVALID");
        var budget=new WorldSpatialWorkBudget(maximumWork,token,failureCode:"METADATA_DOMAIN_CHUNK_WORK_BUDGET");
        SourceSha256=sourceSha256;this.transitions=transitions;this.regionalRoutes=regionalRoutes;this.journeys=journeys;this.progression=progression;
        budget.Charge(JsonSerializer.SerializeToUtf8Bytes(new{transitions,regionalRoutes,journeys,progression},WorldJson.CreateOptions()).Length);
        entries=new(StringComparer.Ordinal);
        foreach(var domain in domains.OrderBy(d=>d.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var world=domain.World;MetadataChunkGeometryIndex? index=null;
            if(world.ChunkPlan is not null)
            {
                long remaining=maximumWork-budget.Used;
                if(remaining<1)throw new ArgumentException("METADATA_DOMAIN_CHUNK_WORK_BUDGET");
                index=planner.CreateGeometryIndex(world,remaining,token);budget.Charge(index.Work);
            }
            var sharedIds=world.ChunkPlan is {} plan?plan.SharedNodeIds.ToHashSet(StringComparer.Ordinal):
                world.Nodes.Where(n=>n.Position is not {Count:2}).Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
            var nodes=world.Nodes.Where(n=>sharedIds.Contains(n.Id)).Select(MetadataChunkGeometrySnapshot.CopyNode).ToArray();
            var links=world.Connections.Where(c=>sharedIds.Contains(c.FromId)||sharedIds.Contains(c.ToId)).Select(MetadataChunkGeometrySnapshot.CopyConnection).ToArray();
            budget.Charge(JsonSerializer.SerializeToUtf8Bytes(new{nodes,links},WorldJson.CreateOptions()).Length);
            bool unindexed=world.ChunkPlan is null&&(world.Surface is not null||world.Nodes.Any(n=>n.Position is {Count:2}));
            var descriptor=new MetadataDomainChunkDescriptor(domain.Id,index is not null,unindexed,
                world.ChunkPlan?.Cells.Select(c=>c.Id).ToArray()??[],nodes.Length,index?.SourceWorldSha256,index?.ChunkPlanSha256);
            var surface=world.Surface;WorldSurfaceQuery? query=surface is null?null:new WorldSurfaceQuery(surface,token);
            MetadataDomainTerrainExtent? available=query?.HeightField switch{
                WorldTerrainWindowQuery {Snapshot:{} saved}=>new(saved.FirstColumn,saved.FirstRow,saved.Columns,saved.Rows),
                WorldTerrainWindowQuery=>null,
                { } full=>new(0,0,full.Geometry.Columns,full.Geometry.Rows),
                _=>null};
            var sharedSurface=surface is null?null:new MetadataDomainChunkSurface(surface.Id,surface.Unit,MetadataExecutionCache.Hash(surface),
                surface.Boundary,surface.Exclusions,surface.Patches,query!.HeightField?.Geometry,available);
            budget.Charge(JsonSerializer.SerializeToUtf8Bytes(sharedSurface,WorldJson.CreateOptions()).Length);
            var cells=world.ChunkPlan?.Cells.ToDictionary(c=>c.Id,StringComparer.Ordinal)??new Dictionary<string,MetadataChunkCell>(StringComparer.Ordinal);
            if(!entries.TryAdd(domain.Id,new(descriptor,index,nodes,links,sharedSurface,query?.HeightField,cells,
                world.ChunkPlan?.Definition.Width??0,world.ChunkPlan?.Definition.Height??0)))throw new ArgumentException("METADATA_DOMAIN_CHUNK_DOMAIN_INVALID");
        }
        Work=budget.Used;
    }

    public static MetadataDomainChunkIndex Create(MetadataWorldDomainDocument document,WorldMetadataGenerator generator,
        MetadataChunkPlanner planner,long maximumWork=20000000,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(generator);ArgumentNullException.ThrowIfNull(planner);
        token.ThrowIfCancellationRequested();
        if(maximumWork is <1 or >200000000)throw new ArgumentException("METADATA_DOMAIN_CHUNK_WORK_INVALID");
        var snapshot=MetadataExecutionCache.Clone(document);generator.ValidateDomains(snapshot,token);
        return new(MetadataExecutionCache.Hash(snapshot),snapshot.Domains,snapshot.Transitions,[],snapshot.Journeys,snapshot.Progression,planner,maximumWork,token);
    }

    public static MetadataDomainChunkIndex Create(MetadataRegionalDomainWorldDocument document,WorldMetadataGenerator generator,
        MetadataChunkPlanner planner,long maximumWork=20000000,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(generator);ArgumentNullException.ThrowIfNull(planner);
        token.ThrowIfCancellationRequested();
        if(maximumWork is <1 or >200000000)throw new ArgumentException("METADATA_DOMAIN_CHUNK_WORK_INVALID");
        var snapshot=MetadataExecutionCache.Clone(document);new MetadataRegionalDomainWorldGenerator(generator).Validate(snapshot,token);
        return new(MetadataExecutionCache.Hash(snapshot),snapshot.Domains.Domains,snapshot.Domains.Transitions,snapshot.RegionalRoutes,
            snapshot.Domains.Journeys,snapshot.Domains.Progression,planner,maximumWork,token);
    }

    public MetadataDomainChunkSelection Select(IReadOnlyList<MetadataDomainChunkRequest> requests,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(requests is null||requests.Count>16||requests.Any(r=>r is null||r.ChunkIds is null||r.ChunkIds.Count>16384)||
            requests.Select(r=>r.DomainId).Distinct(StringComparer.Ordinal).Count()!=requests.Count)
            throw new ArgumentException("METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        var selected=new List<MetadataDomainChunkContent>();var terrainBudget=new WorldSpatialWorkBudget(20000000,token,failureCode:"METADATA_DOMAIN_CHUNK_TERRAIN_WORK_BUDGET");
        foreach(var request in requests.OrderBy(r=>r.DomainId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(request.DomainId is null||!entries.TryGetValue(request.DomainId,out var entry)||entry.Descriptor.HasUnindexedSpatialContent||
                entry.Index is null&&request.ChunkIds.Count!=0)throw new ArgumentException("METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
            var chunks=request.ChunkIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var geometry=entry.Index?.Select(chunks,token)??[];
            var terrain=entry.Surface is null?[]:chunks.Select(id=>Terrain(request.DomainId,id,terrainBudget,token)).ToArray();
            selected.Add(new(request.DomainId,chunks,geometry,entry.SharedNodes,entry.SharedConnections,entry.Surface,terrain));
        }
        token.ThrowIfCancellationRequested();
        return MetadataExecutionCache.Clone(new MetadataDomainChunkSelection(SourceSha256,selected,transitions,regionalRoutes,journeys,progression));
    }
}
