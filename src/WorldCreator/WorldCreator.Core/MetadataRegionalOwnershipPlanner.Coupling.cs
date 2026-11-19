using WorldCreator.Core.Spatial;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalCouplingDemand(string RegionId,double RequiredArea,double RequiredClearance,double Weight,int NodeCount,string AnalysisSha256,string? HabitatSha256);
public sealed record MetadataRegionalCouplingAttempt(int Iteration,string InputSha256,string OutputSha256,string OwnershipSha256,
    IReadOnlyList<MetadataRegionalCouplingDemand> Demands,long Work,bool Stable);
public sealed record MetadataRegionalCouplingPlan(IReadOnlyList<MetadataRegionalCouplingAttempt> Attempts,long InitializationWork);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private MetadataRegionalWorldDefinition PlanCoupled(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token,
        IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<int>>>? excluded=null,IReadOnlyDictionary<string,int>? required=null,
        IReadOnlyList<WorldAreaPartitionChoice>? partitions=null)
    {
        var rule=source.Coupling!;
        if(!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||rule.MaximumIterations is <1 or >32)
            throw new ArgumentException("METADATA_REGIONAL_COUPLING_RULE_INVALID");
        var dynamic=source.Regions.Where(r=>r.Recipe.TerrainDemands is not null).ToArray();
        if(dynamic.Length==0)throw new ArgumentException("METADATA_REGIONAL_COUPLING_UNUSED");
        if(dynamic.Any(r=>r.Capacity is null))throw new ArgumentException("METADATA_REGIONAL_COUPLING_CAPACITY_REQUIRED");
        var attempts=new List<MetadataRegionalCouplingAttempt>();long initialWork=0;
        try
        {
            long start=budget.Used;
            var demands=MeasurePrograms(source,budget,token,catalogMinima:true);
            initialWork=budget.Used-start;
            var visited=new HashSet<string>(StringComparer.Ordinal){Requirements(demands)};
            for(int iteration=1;iteration<=rule.MaximumIterations;iteration++)
            {
                token.ThrowIfCancellationRequested();long before=budget.Used;budget.Charge();
                string input=Requirements(demands);
                var candidate=PlanMeasured(source,demands,budget,token,excluded,required,partitions);
                var measured=MeasurePrograms(source,budget,token,candidate);
                string output=Requirements(measured);bool stable=input==output;
                attempts.Add(new(iteration,input,output,MetadataExecutionCache.Hash(candidate.Regions.Select(r=>new{r.Id,r.Ownership,r.GenerationDomain,r.Recipe.Origin})),
                    measured.Select(d=>new MetadataRegionalCouplingDemand(d.RegionId,d.RequiredArea,d.RequiredClearance,d.Weight,d.Analysis.Nodes.Count,
                        MetadataExecutionCache.Hash(d.Analysis),d.Analysis.TerrainAnalysis?.HabitatSha256)).ToArray(),budget.Used-before,stable));
                if(stable)
                {
                    // The candidate was generated from this exact requirements
                    // vector. Its complete measured analyses, not initialization
                    // minima, are the accepted programs for these same boundaries.
                    var result=candidate with{OwnershipPlan=candidate.OwnershipPlan! with{ProgramDemands=measured,
                        CouplingPlan=new(attempts.ToArray(),initialWork),Work=budget.Used}};
                    MetadataJsonStorage.ValidateSize(result,token);return result;
                }
                if(!visited.Add(output))throw new ArgumentException("METADATA_REGIONAL_COUPLING_CYCLE");
                demands=measured;
            }
            throw new ArgumentException("METADATA_REGIONAL_COUPLING_ITERATION_LIMIT");
        }
        catch(Exception error)when(error is ArgumentException or OperationCanceledException)
        {
            error.Data["RegionalCoupling"]=new MetadataRegionalCouplingPlan(attempts.ToArray(),initialWork);throw;
        }
    }
    private static string Requirements(IReadOnlyList<MetadataRegionalProgramDemand> demands)
        =>MetadataExecutionCache.Hash(demands.Select(d=>new{d.RegionId,d.RequiredArea,d.RequiredClearance,d.Weight}));
}
