using WorldCreator.Core.Spatial;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPlacementPlanner
{
    private static IReadOnlyList<MetadataPlacementNogood>? ValidateNodeNogoods(IReadOnlyList<MetadataPlacementNogood>? guards,
        IReadOnlyDictionary<string,string>? owners,WorldSpatialWorkBudget? budget)
    {
        if(guards is null)return null;
        if(guards.Count is <1 or >128)throw new ArgumentException("METADATA_PLACEMENT_NOGOOD_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var guard in guards)
        {
            budget?.Charge();
            if(guard is null||!WorldEnvironmentQuery.Id(guard.Id)||!ids.Add(guard.Id)||!WorldEnvironmentQuery.Text(guard.Purpose)||!WorldEnvironmentQuery.Text(guard.SourceRuleId)||
                guard.ConsumerRecipeSha256 is {} hash&&!Digest(hash)||guard.Assignments is null||
                (guard.WholeAssignmentSha256 is {} whole?!Digest(whole)||guard.Assignments.Count!=0:guard.Assignments.Count is <1 or >16))
                throw new ArgumentException("METADATA_PLACEMENT_NOGOOD_INVALID");
            var members=new HashSet<string>(StringComparer.Ordinal);
            foreach(var pose in guard.Assignments)
            {
                budget?.Charge();
                if(pose is null||pose.NodeId is not {Length:>=1 and <=2048}||!members.Add(pose.NodeId)||owners is not null&&!owners.ContainsKey(pose.NodeId)||!double.IsFinite(pose.Position.X)||!double.IsFinite(pose.Position.Y)||Math.Abs(pose.Position.X)>1e7||Math.Abs(pose.Position.Y)>1e7)
                    throw new ArgumentException("METADATA_PLACEMENT_NOGOOD_INVALID");
            }
        }
        return Array.AsReadOnly(guards.OrderBy(g=>g.Id,StringComparer.Ordinal).Select(g=>g with{
            Assignments=Array.AsReadOnly(g.Assignments.OrderBy(p=>p.NodeId,StringComparer.Ordinal).ToArray())}).ToArray());
    }
    private static string? NodeNogood(IReadOnlyList<MetadataPlacementNogood>? guards,IReadOnlyDictionary<string,WorldSitePlacement> known,
        WorldSpatialWorkBudget budget,string? candidateId=null,WorldPoint candidate=default)
    {
        foreach(var guard in guards??[])
        {
            if(guard.WholeAssignmentSha256 is not null)continue;
            bool match=true;
            foreach(var pose in guard.Assignments)
            {
                budget.Charge();
                if(pose.NodeId==candidateId?pose.Position!=candidate:
                    !known.TryGetValue(pose.NodeId,out var site)||site.Position!=pose.Position){match=false;break;}
            }
            if(match)return guard.Id;
        }
        return null;
    }
    private static bool Digest(string value)=>value.Length==64&&value.All(char.IsAsciiHexDigitLower);

    private static string? WholeAssignmentNogood(IReadOnlyList<MetadataPlacementNogood>? guards,
        IReadOnlyDictionary<string,WorldSitePlacement> known,WorldSpatialWorkBudget budget)
    {
        string? digest=null;
        foreach(var guard in guards??[])
        {
            if(guard.WholeAssignmentSha256 is not {} forbidden)continue;
            budget.Charge();
            if(digest is null){budget.Charge(known.Count);digest=CandidateDigest(known);}
            if(digest==forbidden)return guard.Id;
        }
        return null;
    }
}
