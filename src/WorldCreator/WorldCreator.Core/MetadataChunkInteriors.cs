using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Exact generated facts for one room. Doorway identity is the pair
/// (RuleId, ConnectionId); both incident rooms may reference the same doorway.</summary>
public sealed record MetadataChunkInterior(string RuleId,string Purpose,string SourceRuleId,double WallThickness,
    IReadOnlyList<MetadataInteriorWall> Walls,IReadOnlyList<MetadataInteriorOpening> Openings);

internal static partial class MetadataChunkGeometrySnapshot
{
    internal static string WallSourceId(string ruleId,MetadataInteriorWall wall)
        =>"interior-"+MetadataExecutionCache.Hash(new{RuleId=ruleId,wall.RoomNodeId,wall.Id});

    internal static MetadataChunkInterior CopyInterior(MetadataChunkInterior source)=>source with{
        Walls=Array.AsReadOnly(source.Walls.Select(w=>w with{Footprint=new WorldPolygon(Array.AsReadOnly(w.Footprint.Vertices.ToArray()))}).ToArray()),
        Openings=Array.AsReadOnly(source.Openings.ToArray())};

    internal static IReadOnlyDictionary<string,IReadOnlyList<MetadataChunkInterior>> CaptureInteriors(MetadataInteriorPlan? plan,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var captured=new Dictionary<string,List<MetadataChunkInterior>>(StringComparer.Ordinal);
        foreach(var result in plan?.Results??[])
        {
            token.ThrowIfCancellationRequested();
            var rule=plan!.Definition.Rules.Single(r=>r.Id==result.RuleId);
            var rooms=result.RoomNodeIds.ToHashSet(StringComparer.Ordinal);
            var walls=result.Walls.ToLookup(w=>w.RoomNodeId,StringComparer.Ordinal);
            var openings=new Dictionary<string,List<MetadataInteriorOpening>>(StringComparer.Ordinal);
            foreach(var opening in result.Openings)
            foreach(var id in new[]{opening.FromId,opening.ToId}.Distinct(StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();budget.Charge();
                if(!rooms.Contains(id))continue;
                if(!openings.TryGetValue(id,out var list))openings.Add(id,list=[]);list.Add(opening);
            }
            foreach(var id in result.RoomNodeIds)
            {
                token.ThrowIfCancellationRequested();var roomWalls=walls[id].ToArray();
                budget.Charge(1L+roomWalls.Sum(w=>(long)w.Footprint.Vertices.Count)+openings.GetValueOrDefault(id,[]).Count);
                if(!captured.TryGetValue(id,out var list))captured.Add(id,list=[]);
                list.Add(CopyInterior(new(result.RuleId,rule.Purpose,rule.SourceRuleId,rule.WallThickness,roomWalls,openings.GetValueOrDefault(id,[]))));
            }
        }
        return captured.ToDictionary(p=>p.Key,p=>(IReadOnlyList<MetadataChunkInterior>)Array.AsReadOnly(p.Value.ToArray()),StringComparer.Ordinal);
    }

    internal static void ValidateInteriors(IEnumerable<MetadataChunkGeometry> geometry,CancellationToken token)
    {
        var doors=new Dictionary<(string Rule,string Connection),MetadataInteriorOpening>();
        var roomDoors=new Dictionary<(string Room,string Rule),HashSet<string>>();
        var rules=new Dictionary<string,(string Purpose,string Source,double Thickness)>(StringComparer.Ordinal);
        foreach(var item in geometry)
        {
            token.ThrowIfCancellationRequested();
            if(item.Coverage.Any(c=>c is null))Invalid();
            var coverage=new Dictionary<string,MetadataChunkCoverage>(StringComparer.Ordinal);
            foreach(var shape in item.Coverage.Where(c=>c.Kind==MetadataChunkCoverageKind.InteriorWall))
                if(shape.SourceId is null||!coverage.TryAdd(shape.SourceId,shape))Invalid();
            if(item.Interiors is null)
            {if(coverage.Count!=0)Invalid();continue;}
            if(item.Node is null||item.Interiors.Count==0||item.Interiors.Any(i=>i is null)||item.Interiors.Select(i=>i.RuleId).Distinct(StringComparer.Ordinal).Count()!=item.Interiors.Count)Invalid();
            var expected=new HashSet<string>(StringComparer.Ordinal);
            foreach(var interior in item.Interiors)
            {
                token.ThrowIfCancellationRequested();
                if(interior is null||!WorldEnvironmentQuery.Id(interior.RuleId)||!WorldEnvironmentQuery.Text(interior.Purpose)||
                    !WorldEnvironmentQuery.Text(interior.SourceRuleId)||!double.IsFinite(interior.WallThickness)||interior.WallThickness<=0||
                    interior.Walls is null||interior.Openings is null||interior.Walls.Any(w=>w is null)||interior.Openings.Any(o=>o is null)||
                    interior.Walls.Select(w=>w.Id).Distinct(StringComparer.Ordinal).Count()!=interior.Walls.Count||
                    interior.Openings.Select(o=>o.ConnectionId).Distinct(StringComparer.Ordinal).Count()!=interior.Openings.Count)Invalid();
                var ruleSource=(interior!.Purpose,interior.SourceRuleId,interior.WallThickness);
                if(rules.TryGetValue(interior.RuleId,out var previousRule)&&previousRule!=ruleSource)
                    throw new ArgumentException("METADATA_CHUNK_PACKAGE_INTERIOR_MISMATCH");
                rules[interior.RuleId]=ruleSource;
                roomDoors.Add((item.Member.Id,interior.RuleId),interior.Openings.Select(o=>o.ConnectionId).ToHashSet(StringComparer.Ordinal));
                foreach(var wall in interior!.Walls)
                {
                    token.ThrowIfCancellationRequested();
                    if(string.IsNullOrWhiteSpace(wall.Id)||wall.RoomNodeId!=item.Member.Id||wall.EdgeIndex<0||!Point(wall.From)||!Point(wall.To)||
                        wall.From.DistanceTo(wall.To)<=0||wall.Footprint is null)Invalid();
                    wall.Footprint!.Validate();string id=WallSourceId(interior.RuleId,wall);
                    if(!expected.Add(id)||!coverage.TryGetValue(id,out var shape)||!MetadataPopulationSources.Equal(shape.Polygon,wall.Footprint))Invalid();
                }
                foreach(var opening in interior.Openings)
                {
                    token.ThrowIfCancellationRequested();
                    if(string.IsNullOrWhiteSpace(opening.ConnectionId)||string.IsNullOrWhiteSpace(opening.FromId)||string.IsNullOrWhiteSpace(opening.ToId)||
                        opening.FromId!=item.Member.Id&&opening.ToId!=item.Member.Id||!Point(opening.From)||!Point(opening.To)||
                        !Point(opening.FromApproach)||!Point(opening.ToApproach)||!double.IsFinite(opening.Width)||opening.Width<=0||
                        !item.Connections.Any(c=>c.Id==opening.ConnectionId&&(c.FromId==opening.FromId&&c.ToId==opening.ToId||c.FromId==opening.ToId&&c.ToId==opening.FromId)))Invalid();
                    var key=(interior.RuleId,opening.ConnectionId);
                    if(doors.TryGetValue(key,out var prior)&&!MetadataPopulationSources.Equal(prior,opening))
                        throw new ArgumentException("METADATA_CHUNK_PACKAGE_INTERIOR_MISMATCH");
                    doors[key]=opening;
                }
            }
            if(expected.Count!=coverage.Count)Invalid();
        }
        foreach(var (key,door) in doors)
        foreach(var id in new[]{door.FromId,door.ToId}.Distinct(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(roomDoors.TryGetValue((id,key.Rule),out var references)&&!references.Contains(key.Connection))
                throw new ArgumentException("METADATA_CHUNK_PACKAGE_INTERIOR_MISMATCH");
        }
        static bool Point(WorldPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X)<=1e7&&Math.Abs(p.Y)<=1e7;
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        static void Invalid()=>throw new ArgumentException("METADATA_CHUNK_PACKAGE_INTERIOR_INVALID");
    }
}
