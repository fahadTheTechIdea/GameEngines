using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalPartitionAttempt(int Ordinal,string RegionId,string StratumSha256,WorldPolygon Ownership,
    WorldRegionRequest Request,bool Qualified,string? AnchorPlanSha256,WorldRegionIntentPlan? IntentPlan,
    WorldRegionIntentFailure? Failure,string? Rejection,long Work);
public sealed record MetadataRegionalPartitionQualification(IReadOnlyList<MetadataRegionalPartitionAttempt> Attempts,IReadOnlyList<int> SelectedAttempts,long Work);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private sealed class PartitionQualification(MetadataRegionalOwnershipRequest source,IReadOnlyList<MetadataRegionalProgramRegion> programs,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        private readonly List<MetadataRegionalPartitionAttempt> attempts=[];
        private readonly Dictionary<int,(string Stratum,WorldPolygon Ownership,WorldRegionPlan Plan,int Ordinal)> selected=new();
        private long witnessReferences;
        internal MetadataRegionalPartitionQualification Snapshot(IReadOnlyList<int>? chosen=null)=>new(attempts.ToArray(),chosen??[],attempts.Sum(a=>a.Work));
        private void Add(MetadataRegionalPartitionAttempt attempt)
        {
            witnessReferences+=attempt.Ownership.Vertices.Count+(attempt.IntentPlan?.Candidates.Count??attempt.Failure?.Candidates.Count??0);
            if(witnessReferences>65536)throw new ArgumentException("METADATA_REGIONAL_PARTITION_WITNESS_BUDGET");
            attempts.Add(attempt);
            // Bound growing diagnostic payloads without repeatedly serializing
            // every prefix. Final publication still checks the complete packet.
            if((attempts.Count&(attempts.Count-1))==0)MetadataJsonStorage.ValidateSize(Snapshot(),token);
        }
        internal void Qualify(int index,WorldPopulationStratum stratum)
        {
            token.ThrowIfCancellationRequested();long before=budget.Used;var program=programs[index];
            var loops=MetadataGeneratedBoundary.Loops(stratum.BoundaryEdges,budget,token);
            if(loops.Count!=1||loops[0].Vertices.Count>64)throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_SINGLE_POLYGON_REQUIRED:"+program.Id);
            var ownership=loops[0];var actual=WorldSurfaceQuery.CreateRegional(source.Surface,ownership,token).Definition;
            var request=Request(source,program,ownership);string digest=MetadataExecutionCache.Hash(stratum);int ordinal=attempts.Count+1;
            WorldRegionPlan plan;
            try{plan=new MetadataRegionPlanner().Plan(request,program.Recipe.Environment!,actual,program.Recipe.Space,budget,token);}
            catch(WorldRegionIntentUnsatisfiedException error)
            {
                Add(new(ordinal,program.Id,digest,ownership,request,false,null,null,error.Data["RegionIntentFailure"] as WorldRegionIntentFailure,error.Message,budget.Used-before));
                ArgumentException failure=program.PinnedAnchor is {} pin?new MetadataRegionalAnchorConflictException(program.Id,pin.Position,error):error;
                throw new WorldAreaQualificationException(program.Id,failure);
            }
            catch(ArgumentException error)
            {Add(new(ordinal,program.Id,digest,ownership,request,false,null,null,null,error.Message,budget.Used-before));throw;}
            catch(OperationCanceledException)
            {Add(new(ordinal,program.Id,digest,ownership,request,false,null,null,null,"CANCELLED",budget.Used-before));throw;}
            Add(new(ordinal,program.Id,digest,ownership,plan.Request,true,MetadataExecutionCache.Hash(plan),plan.IntentPlan,null,null,budget.Used-before));
            selected[index]=(digest,ownership,plan,ordinal);
        }
        internal (WorldPolygon Ownership,WorldRegionPlan Plan) Selection(int index,WorldPopulationStratum stratum)
        {
            if(!selected.TryGetValue(index,out var item)||item.Stratum!=MetadataExecutionCache.Hash(stratum))
                throw new InvalidOperationException("METADATA_REGIONAL_PARTITION_SELECTION_REQUIRED");
            return(item.Ownership,item.Plan);
        }
        internal MetadataRegionalPartitionQualification Complete(WorldAreaPopulationPlan partition)
        {
            for(int i=0;i<partition.Strata.Count;i++)Selection(i,partition.Strata[i]);
            return Snapshot(Enumerable.Range(0,partition.Strata.Count).Select(i=>selected[i].Ordinal).ToArray());
        }
        internal static WorldRegionRequest Request(MetadataRegionalOwnershipRequest source,MetadataRegionalProgramRegion program,WorldPolygon ownership,
            IReadOnlyList<IReadOnlyList<int>>? excluded=null,int? required=null)=>new(program.Id,program.Purpose,program.SourceRuleId,ownership,program.Profile,[],source.Pattern.MaximumCells,source.MaximumWork,
                Intents:[new(program.Id,program.Purpose,program.SourceRuleId,program.MinimumArea,program.MinimumClearance,RequiredPosition:program.PinnedAnchor?.Position,PreferSuitableAnchor:program.PreferSuitableAnchor)],AnchorPrecision:source.AnchorPrecision,
                MaximumAnchorCells:source.MaximumAnchorCells,Representation:WorldRegionRepresentation.Membership,ExcludedAssignments:excluded,RequiredAssignment:required is {} component?[component]:null);
    }
    private static ArgumentException PartitionCause(ArgumentException error)
    {
        for(Exception? current=error;current is not null;current=current.InnerException)
            if(current is WorldAreaQualificationException rejected)return rejected.Failure;
        return error;
    }
}
