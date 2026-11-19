using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal sealed record MetadataCorridorRouteTrack<T>(T Start,T End,bool Reverse);
internal sealed record MetadataCorridorJourneyWitness(string DomainId,string ConnectionId,string RouteId,bool Reverse,
    string GroupId,string SegmentId,bool Forward,WorldPoint From,WorldPoint To);

/// <summary>Projects already-qualified local corridor segments and recorded turns
/// into a caller's directed journey graph. Never creates implicit intersections.</summary>
internal static class MetadataCorridorJourneyGraph
{
    internal static void Add<T>(IReadOnlyDictionary<string,WorldMetadataDocument> worlds,IReadOnlyList<string> kinds,
        IReadOnlyDictionary<string,WorldTraversalProfile> profiles,IReadOnlyDictionary<(string Domain,string Route),MetadataCorridorRouteTrack<T>> tracks,
        Func<string,WorldPoint,T> vertex,Func<T,WorldPoint> position,Action<T,T,MetadataCorridorJourneyWitness?> add,WorldSpatialWorkBudget budget)
    {
        foreach(var entry in worlds.OrderBy(p=>p.Key,StringComparer.Ordinal))
        foreach(var group in entry.Value.CorridorPlan?.Groups??[])
        {
            budget.Charge();
            if(!profiles.TryGetValue(entry.Key,out var profile)||!kinds.Contains(group.Definition.Movement,StringComparer.Ordinal)||
                WorldSurfaceTraversal.CompatibilityKey(group.Profile)!=WorldSurfaceTraversal.CompatibilityKey(profile))continue;
            budget.Charge(group.Journeys.Count);var sources=group.Journeys.ToDictionary(j=>j.Id,StringComparer.Ordinal);
            var directed=new Dictionary<(string Id,bool Forward,string Source),(T Start,T End)>();
            var byDirection=new Dictionary<(string Id,bool Forward),List<string>>();
            foreach(var segment in group.Segments.OrderBy(s=>s.Id,StringComparer.Ordinal))foreach(bool forward in new[]{true,false})
            {
                var sourceIds=forward?segment.ForwardJourneys:segment.ReverseJourneys;budget.Charge(1L+sourceIds.Count);
                foreach(string id in sourceIds.Where(id=>tracks.ContainsKey((entry.Key,id))).Order(StringComparer.Ordinal))
                {
                    budget.Charge();var source=sources[id];var a=forward?segment.From:segment.To;var b=forward?segment.To:segment.From;
                    var begin=vertex(entry.Key,a);var end=vertex(entry.Key,b);directed.Add((segment.Id,forward,id),(begin,end));
                    if(!byDirection.TryGetValue((segment.Id,forward),out var sourceList))byDirection.Add((segment.Id,forward),sourceList=[]);
                    sourceList.Add(id);
                    add(begin,end,new(entry.Key,source.ConnectionId,id,tracks[(entry.Key,id)].Reverse,group.Definition.Id,segment.Id,forward,a,b));
                }
            }
            foreach(var junction in group.Junctions.OrderBy(j=>j.Id,StringComparer.Ordinal))foreach(var turn in junction.Transitions)
            {
                budget.Charge();
                if(byDirection.TryGetValue((turn.FromSegmentId,turn.FromForward),out var incomingSources)&&
                    byDirection.TryGetValue((turn.ToSegmentId,turn.ToForward),out var outgoingSources))
                {
                    foreach(var incomingSource in incomingSources)foreach(var outgoingSource in outgoingSources)
                    {
                        budget.Charge();var incoming=directed[(turn.FromSegmentId,turn.FromForward,incomingSource)];
                        var outgoing=directed[(turn.ToSegmentId,turn.ToForward,outgoingSource)];
                        if(position(incoming.End)!=junction.Position||position(outgoing.Start)!=junction.Position)
                            throw new ArgumentException("METADATA_JOURNEY_CORRIDOR_INVALID");
                        add(incoming.End,outgoing.Start,null);
                    }
                }
            }
            foreach(var source in group.Journeys.OrderBy(j=>j.Id,StringComparer.Ordinal))
            {
                budget.Charge();if(!tracks.TryGetValue((entry.Key,source.Id),out var track)||source.Steps.Count==0)continue;
                var first=source.Steps[0];var last=source.Steps[^1];
                if(!directed.TryGetValue((first.SegmentId,first.Forward,source.Id),out var begin)||!directed.TryGetValue((last.SegmentId,last.Forward,source.Id),out var end))continue;
                if(position(track.Start)!=source.From||position(track.End)!=source.To||position(begin.Start)!=source.From||position(end.End)!=source.To)
                    throw new ArgumentException("METADATA_JOURNEY_CORRIDOR_INVALID");
                add(track.Start,begin.Start,null);add(end.End,track.End,null);
            }
        }
    }
}
