using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Both directions of FailedSegmentId are unavailable. Steps retain
/// existing segment direction and junction permissions; endpoints are unchanged.</summary>
public sealed record WorldCorridorFailureAlternative(string JourneyId,string FailedSegmentId,IReadOnlyList<WorldCorridorStep> Steps);
public sealed record WorldCorridorResiliencePlan(int Version,IReadOnlyList<WorldCorridorFailureAlternative> Alternatives,long Work);

/// <summary>Checks the realized physical graph, not the source logical edges.
/// Failures outside a journey's original steps leave that original path intact.</summary>
internal static class WorldCorridorResilience
{
    internal static WorldCorridorResiliencePlan Check(IReadOnlyList<WorldCorridorSegment> segments,
        IReadOnlyList<WorldCorridorJunction> junctions,IReadOnlyList<WorldCorridorJourney> journeys,WorldSpatialWorkBudget budget)
    {
        long before=budget.Used;
        int checks=journeys.Sum(j=>j.Steps.Select(s=>s.SegmentId).Distinct(StringComparer.Ordinal).Count());
        budget.Charge(journeys.Sum(j=>(long)j.Steps.Count));
        if(checks>4096)throw new ArgumentException("WORLD_CORRIDOR_RESILIENCE_CHECK_BUDGET");
        var byId=segments.ToDictionary(s=>s.Id,StringComparer.Ordinal);
        var starts=new Dictionary<WorldPoint,List<WorldCorridorStep>>();
        var next=new Dictionary<WorldCorridorStep,List<WorldCorridorStep>>();
        WorldPoint End(WorldCorridorStep step)=>step.Forward?byId[step.SegmentId].To:byId[step.SegmentId].From;
        foreach(var segment in segments)
        {
            budget.Charge();
            foreach(bool forward in new[]{false,true})
            {
                if((forward?segment.ForwardJourneys:segment.ReverseJourneys).Count==0)continue;
                var step=new WorldCorridorStep(segment.Id,forward);var point=forward?segment.From:segment.To;
                if(!starts.TryGetValue(point,out var outgoing))starts.Add(point,outgoing=[]);
                outgoing.Add(step);next.Add(step,[]);
            }
        }
        foreach(var junction in junctions)foreach(var transition in junction.Transitions)
        {
            budget.Charge();
            next[new(transition.FromSegmentId,transition.FromForward)].Add(new(transition.ToSegmentId,transition.ToForward));
        }
        foreach(var list in starts.Values.Concat(next.Values))list.Sort((a,b)=>
        {
            int id=string.CompareOrdinal(a.SegmentId,b.SegmentId);return id==0?a.Forward.CompareTo(b.Forward):id;
        });
        var alternatives=new List<WorldCorridorFailureAlternative>();long witnessSteps=0;
        foreach(var journey in journeys.OrderBy(j=>j.Id,StringComparer.Ordinal))
        foreach(string failed in journey.Steps.Select(s=>s.SegmentId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            budget.Charge();
            var previous=new Dictionary<WorldCorridorStep,WorldCorridorStep?>();var queue=new Queue<WorldCorridorStep>();
            if(journey.From!=journey.To)foreach(var seed in starts[journey.From])
            {
                budget.Charge();if(seed.SegmentId==failed)continue;
                previous.Add(seed,null);queue.Enqueue(seed);
            }
            WorldCorridorStep? goal=null;
            while(queue.TryDequeue(out var step))
            {
                budget.Charge();if(End(step)==journey.To){goal=step;break;}
                foreach(var candidate in next[step])
                {
                    budget.Charge();if(candidate.SegmentId==failed||!previous.TryAdd(candidate,step))continue;
                    queue.Enqueue(candidate);
                }
            }
            if(goal is null&&journey.From!=journey.To)
                throw new ArgumentException("WORLD_CORRIDOR_RESILIENCE_UNSATISFIED:"+journey.Id+":"+failed);
            var path=new List<WorldCorridorStep>();
            for(var cursor=goal;cursor is not null;cursor=previous[cursor]){budget.Charge();path.Add(cursor);}
            path.Reverse();witnessSteps+=path.Count;
            if(witnessSteps>1000000)throw new ArgumentException("WORLD_CORRIDOR_RESILIENCE_WITNESS_BUDGET");
            alternatives.Add(new(journey.Id,failed,path.AsReadOnly()));
        }
        return new(1,alternatives.AsReadOnly(),budget.Used-before);
    }
}
