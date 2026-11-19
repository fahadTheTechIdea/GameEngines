using WorldCreator.Core.Metadata;

namespace WorldCreator.Cli;

/// <summary>Formats bounded, observed placement diagnostics without inferring infeasibility.</summary>
internal static class PlacementFailureDisplay
{
    internal static IEnumerable<string> Describe(Exception error)
    {
        var visited=new HashSet<Exception>();
        for(Exception? current=error;current is not null&&visited.Add(current)&&visited.Count<=8;current=current.InnerException)
        {
            if(current.Data["PlacementFailures"] is not IReadOnlyList<MetadataPlacementFailure> failures)continue;
            int shown=0;
            foreach(var failure in failures)
            foreach(var diagnostic in failure.Diagnostics.Where(d=>d.Eligible==0))
            {
                if(shown++>=8)yield break;
                var reasons=diagnostic.Rejections.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key,StringComparer.Ordinal)
                    .Take(8).Select(p=>$"{p.Key}={p.Value}");
                yield return $"Placement candidates: stage={failure.StageId}; demand={diagnostic.DemandId}; candidates={diagnostic.Candidates}; eligible={diagnostic.Eligible}; rejections=[{string.Join(", ",reasons)}].";
            }
            yield break;
        }
    }
}
