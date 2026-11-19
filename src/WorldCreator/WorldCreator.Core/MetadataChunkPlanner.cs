using System.Globalization;
using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataChunkDefinition(int Version,string Id,string Purpose,string SourceRuleId,
    WorldPoint Origin,double Width,double Height,int MaximumChunks=4096,int MaximumReferences=200000,long MaximumWork=20000000);
public enum MetadataSpatialMemberKind { Node, Path }
public sealed record MetadataChunkCell(string Id,int X,int Y,WorldPoint Origin);
public sealed record MetadataChunkMember(MetadataSpatialMemberKind Kind,string Id,string OwnerChunkId,
    IReadOnlyList<string> ChunkIds,WorldPoint OwnerLocalAnchor);
public sealed record MetadataChunkPlan(MetadataChunkDefinition Definition,string SourceWorldSha256,
    IReadOnlyList<MetadataChunkCell> Cells,IReadOnlyList<MetadataChunkMember> Members,
    IReadOnlyList<string> SharedNodeIds,long Work);

/// <summary>Indexes unchanged source geometry for chunk consumers. One identity and
/// owner per member; closed physical references may cross half-open owner cells.
/// Terrain and other world plans remain shared dependencies, not independently streamed data.</summary>
public sealed class MetadataChunkPlanner(WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs=null,IEnumerable<IMetadataLayout>? layouts=null)
{
    public MetadataChunkPlan Plan(MetadataChunkDefinition definition,WorldMetadataDocument world,CancellationToken token=default)
    {
        WorldMetadataGenerator.ValidateChunkSource(world with{ChunkPlan=null},catalogs,token,layouts);
        return PlanCore(definition,world,token);
    }

    internal MetadataChunkPlan PlanCore(MetadataChunkDefinition definition,WorldMetadataDocument world,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        static bool Point(WorldPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X)<=1e7&&Math.Abs(p.Y)<=1e7;
        if(definition is null||definition.Version!=1||!WorldEnvironmentQuery.Id(definition.Id)||
            !WorldEnvironmentQuery.Text(definition.Purpose)||!WorldEnvironmentQuery.Text(definition.SourceRuleId)||!Point(definition.Origin)||
            !double.IsFinite(definition.Width)||!double.IsFinite(definition.Height)||definition.Width is <.01 or >1e7||definition.Height is <.01 or >1e7||
            definition.MaximumChunks is <1 or >16384||definition.MaximumReferences is <1 or >1000000||definition.MaximumWork is <1 or >200000000||
            world.Space.Axes.Count!=2||world.Nodes.Count>100000)
            throw new ArgumentException("METADATA_CHUNK_DEFINITION_INVALID");
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token);
        var cells=new Dictionary<string,MetadataChunkCell>(StringComparer.Ordinal);
        var members=new List<MetadataChunkMember>();var identities=new HashSet<(MetadataSpatialMemberKind,string)>();
        var shared=new List<string>();int references=0;
        int Index(double coordinate,double origin,double size)
        {
            double value=Math.Floor((coordinate-origin)/size);
            if(value is <int.MinValue+1d or >int.MaxValue-1d)throw new ArgumentException("METADATA_CHUNK_COORDINATE_LIMIT");
            return (int)value;
        }
        string Cell(int x,int y)
        {
            string id=x.ToString(CultureInfo.InvariantCulture)+":"+y.ToString(CultureInfo.InvariantCulture);
            if(!cells.ContainsKey(id))
            {
                if(cells.Count>=definition.MaximumChunks)throw new ArgumentException("METADATA_CHUNK_CELL_BUDGET");
                cells.Add(id,new(id,x,y,new(definition.Origin.X+x*definition.Width,definition.Origin.Y+y*definition.Height)));
            }
            return id;
        }
        void Add(MetadataSpatialMemberKind kind,string id,WorldPoint anchor,IReadOnlyList<WorldPolygon> polygons)
        {
            token.ThrowIfCancellationRequested();
            if(!Point(anchor)||!identities.Add((kind,id)))throw new ArgumentException("METADATA_CHUNK_MEMBER_INVALID");
            string owner=Cell(Index(anchor.X,definition.Origin.X,definition.Width),Index(anchor.Y,definition.Origin.Y,definition.Height));
            var touched=new HashSet<string>(StringComparer.Ordinal);Reference(owner);
            void Reference(string chunk)
            {
                if(touched.Add(chunk)&&++references>definition.MaximumReferences)throw new ArgumentException("METADATA_CHUNK_REFERENCE_BUDGET");
            }
            WorldPolygon Rectangle(int x,int y)
            {
                double left=definition.Origin.X+x*definition.Width,top=definition.Origin.Y+y*definition.Height;
                return new([new(left,top),new(left+definition.Width,top),new(left+definition.Width,top+definition.Height),new(left,top+definition.Height)]);
            }
            void Cover(IReadOnlyList<WorldPoint> vertices,Func<WorldPolygon,bool> overlaps)
            {
                if(vertices.Count==0||vertices.Any(p=>!Point(p)))throw new ArgumentException("METADATA_CHUNK_GEOMETRY_INVALID");
                double minX=vertices.Min(p=>p.X),minY=vertices.Min(p=>p.Y),maxX=vertices.Max(p=>p.X),maxY=vertices.Max(p=>p.Y);
                int x0=Index(minX,definition.Origin.X,definition.Width),y0=Index(minY,definition.Origin.Y,definition.Height);
                // Closed footprints touch both sides of an exact seam. Owner points
                // still belong only to floor(index), including at negative coordinates.
                if(definition.Origin.X+x0*definition.Width==minX)x0--;
                if(definition.Origin.Y+y0*definition.Height==minY)y0--;
                int x1=Index(maxX,definition.Origin.X,definition.Width),y1=Index(maxY,definition.Origin.Y,definition.Height);
                long columns=(long)x1-x0+1,rows=(long)y1-y0+1,remaining=definition.MaximumWork-budget.Used;
                if(columns>remaining||rows>remaining/columns)throw new ArgumentException("METADATA_CHUNK_SCAN_BUDGET");
                for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++)
                {budget.Charge();if(overlaps(Rectangle(x,y)))Reference(Cell(x,y));}
            }
            bool SegmentTouches(WorldPoint a,WorldPoint b,WorldPolygon box)
            {
                budget.Charge(4);
                return box.ContainsPoint(a)||box.ContainsPoint(b)||Enumerable.Range(0,4).Any(i=>
                    PlanarIntersections.Intersect(a,b,box.Vertices[i],box.Vertices[(i+1)%4]).Kind!=IntersectionKind.None);
            }
            foreach(var polygon in polygons)
            {
                budget.Charge((long)polygon.Vertices.Count*polygon.Vertices.Count);polygon.Validate();
                Cover(polygon.Vertices,box=>{
                    budget.Charge(polygon.Vertices.Count*8L);
                    return polygon.Vertices.Any(box.ContainsPoint)||box.Vertices.Any(polygon.ContainsPoint)||
                        Enumerable.Range(0,polygon.Vertices.Count).Any(i=>SegmentTouches(polygon.Vertices[i],polygon.Vertices[(i+1)%polygon.Vertices.Count],box));
                });
            }
            var origin=cells[owner].Origin;
            members.Add(new(kind,id,owner,Array.AsReadOnly(touched.Order(StringComparer.Ordinal).ToArray()),new(anchor.X-origin.X,anchor.Y-origin.Y)));
        }

        MetadataSpatialMembers.Visit(world,budget,token,m=>Add(m.Kind,m.Id,m.Anchor,m.Polygons),shared.Add);
        return new(definition,SourceDigest(world),Array.AsReadOnly(cells.Values.OrderBy(c=>c.X).ThenBy(c=>c.Y).ToArray()),
            Array.AsReadOnly(members.OrderBy(m=>m.Kind).ThenBy(m=>m.Id,StringComparer.Ordinal).ToArray()),shared.AsReadOnly(),budget.Used);
    }

    public static string SourceDigest(WorldMetadataDocument world)=>MetadataExecutionCache.Hash(world with{ChunkPlan=null});

    public void ValidateDocument(WorldMetadataDocument world,CancellationToken token=default)
    {
        if(world.ChunkPlan is not {} plan)return;
        var expected=PlanCore(plan.Definition,world,token);
        if(JsonSerializer.Serialize(expected,WorldJson.Shared)!=JsonSerializer.Serialize(plan,WorldJson.Shared))
            throw new ArgumentException("METADATA_CHUNK_PLAN_MISMATCH");
    }

    /// <summary>Validates the source index once and creates a detached selection
    /// snapshot. Recreate it after publishing a different source-world digest.</summary>
    public MetadataChunkSelectionIndex CreateSelectionIndex(WorldMetadataDocument world,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(world is null)throw new ArgumentNullException(nameof(world));
        ValidateDocument(world,token);
        return new MetadataChunkSelectionIndex(world.ChunkPlan??throw new ArgumentException("METADATA_CHUNK_PLAN_REQUIRED"),token);
    }

    /// <summary>Materializes detached coverage geometry from the validated world.
    /// Terrain and other shared dependencies remain in the source world.</summary>
    public MetadataChunkGeometryIndex CreateGeometryIndex(WorldMetadataDocument world,long maximumWork=20000000,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(maximumWork is <1 or >200000000)throw new ArgumentException("METADATA_CHUNK_GEOMETRY_WORK_INVALID");
        var selection=CreateSelectionIndex(world,token);
        var plan=world.ChunkPlan!;var budget=new WorldSpatialWorkBudget(maximumWork,token,failureCode:"METADATA_CHUNK_GEOMETRY_WORK_BUDGET");
        var geometry=new Dictionary<(MetadataSpatialMemberKind,string),MetadataChunkGeometry>();
        var coverage=new Dictionary<(MetadataSpatialMemberKind,string),List<MetadataChunkCoverage>>();
        var occupancy=(world.AccessPlan?.Sites??[]).ToDictionary(s=>s.NodeId,s=>s.Occupancy,StringComparer.Ordinal);
        var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var interiors=MetadataChunkGeometrySnapshot.CaptureInteriors(world.InteriorPlan,budget,token);
        var connections=new Dictionary<string,List<WorldMetadataConnection>>(StringComparer.Ordinal);
        foreach(var connection in world.Connections)
        {
            token.ThrowIfCancellationRequested();budget.Charge(1);
            bool from=nodes.TryGetValue(connection.FromId,out var fromNode)&&fromNode.Position is not null;
            bool to=nodes.TryGetValue(connection.ToId,out var toNode)&&toNode.Position is not null;
            if(!from&&!to)continue;
            foreach(var property in connection.Properties){token.ThrowIfCancellationRequested();budget.Charge(property.Key.Length+property.Value.GetRawText().Length+1L);}
            var copy=MetadataChunkGeometrySnapshot.CopyConnection(connection);
            void Add(string id){if(!connections.TryGetValue(id,out var list))connections.Add(id,list=[]);list.Add(copy);budget.Charge();}
            if(from)Add(connection.FromId);if(to&&connection.ToId!=connection.FromId)Add(connection.ToId);
        }
        var members=plan.Members.ToDictionary(m=>(m.Kind,m.Id));
        MetadataSpatialMembers.Visit(world,budget,token,source=>
        {
            token.ThrowIfCancellationRequested();
            if(!members.TryGetValue((source.Kind,source.Id),out var member))throw new ArgumentException("METADATA_CHUNK_GEOMETRY_MISMATCH");
            var parts=coverage.GetValueOrDefault((source.Kind,source.Id),[]);
            if(parts.Count!=source.Polygons.Count)throw new ArgumentException("METADATA_CHUNK_GEOMETRY_MISMATCH");
            WorldPolyline? path=null;
            if(source.Route?.Path is {} original)
            {budget.Charge(original.Points.Count+1L);path=new WorldPolyline(Array.AsReadOnly(original.Points.ToArray()));}
            var detachedMember=member with{ChunkIds=Array.AsReadOnly(member.ChunkIds.ToArray())};
            var nodeOccupancy=source.Kind==MetadataSpatialMemberKind.Node&&occupancy.TryGetValue(source.Id,out var value)?value:(MetadataOccupancyKind?)null;
            WorldMetadataNode? node=null;
            if(source.Kind==MetadataSpatialMemberKind.Node)
            {
                if(!nodes.TryGetValue(source.Id,out var originalNode)||originalNode.Properties is null)throw new ArgumentException("METADATA_CHUNK_GEOMETRY_MISMATCH");
                foreach(var property in originalNode.Properties){token.ThrowIfCancellationRequested();budget.Charge(property.Key.Length+property.Value.GetRawText().Length+1L);}
                budget.Charge(originalNode.Position?.Count??0);node=MetadataChunkGeometrySnapshot.CopyNode(originalNode);
            }
            var incident=source.Kind==MetadataSpatialMemberKind.Node&&connections.TryGetValue(source.Id,out var links)
                ?Array.AsReadOnly(links.ToArray()):Array.AsReadOnly(Array.Empty<WorldMetadataConnection>());
            var roomInteriors=source.Kind==MetadataSpatialMemberKind.Node?interiors.GetValueOrDefault(source.Id):null;
            if(!geometry.TryAdd((source.Kind,source.Id),new(detachedMember,source.Anchor,parts.AsReadOnly(),nodeOccupancy,path,source.Profile?.Radius,node,incident,roomInteriors)))
                throw new ArgumentException("METADATA_CHUNK_GEOMETRY_MISMATCH");
        },_=>{},(kind,id,role,sourceId,segment,polygon,featureKind)=>
        {
            token.ThrowIfCancellationRequested();budget.Charge(polygon.Vertices.Count+1L);
            var key=(kind,id);if(!coverage.TryGetValue(key,out var parts))coverage.Add(key,parts=[]);
            parts.Add(new(role,sourceId,segment,new WorldPolygon(Array.AsReadOnly(polygon.Vertices.ToArray())),featureKind));
        });
        if(geometry.Count!=members.Count||SourceDigest(world)!=selection.SourceWorldSha256||
            MetadataExecutionCache.Hash(world.ChunkPlan)!=selection.ChunkPlanSha256)
            throw new ArgumentException("METADATA_CHUNK_GEOMETRY_MISMATCH");
        token.ThrowIfCancellationRequested();
        return new MetadataChunkGeometryIndex(selection,geometry,budget.Used);
    }

    /// <summary>Returns each original member once even when several requested chunks
    /// reference it. Consumers resolve geometry from the source package by kind/ID.</summary>
    public IReadOnlyList<MetadataChunkMember> Select(WorldMetadataDocument world,IReadOnlyList<string> chunkIds,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ValidateDocument(world,token);
        var plan=world.ChunkPlan??throw new ArgumentException("METADATA_CHUNK_PLAN_REQUIRED");
        if(chunkIds is null||chunkIds.Count>16384)throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");
        var selected=chunkIds.ToHashSet(StringComparer.Ordinal);var known=plan.Cells.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        if(selected.Any(id=>!known.Contains(id)))throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");
        return Array.AsReadOnly(plan.Members.Where(m=>m.ChunkIds.Any(selected.Contains)).ToArray());
    }
}
