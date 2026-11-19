using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal sealed record MetadataProgramChoiceAnalysis(IReadOnlyList<int> Ranks,MetadataProgramAnalysis Analysis,
    IReadOnlyList<MetadataProgramAnalysisFailure>? RejectedAnalyses=null);
public sealed record MetadataProgramAnalysisFailure(int Ordinal,IReadOnlyList<int> Prefix,IReadOnlyList<int> ActualRanks,
    IReadOnlyList<MetadataProgramMixAllocation> Selections,bool RegionRequested,string Rejection,long Work);

public sealed partial class MetadataProgramMixPlanner
{
    internal MetadataProgramAnalysis AnalyzeRequired(MetadataProgramCompositionRule rule,Func<ChoiceCursor,MetadataProgramAnalysis> analyze,
        WorldSpatialWorkBudget budget,CancellationToken token,bool allowEmpty=false)
    {
        ValidateComposition(rule,allowEmpty);var cursor=new ChoiceCursor(this,rule,token,budget);cursor.Begin(rule.RequiredRanks!);
        try
        {
            var result=analyze(cursor);
            if(!cursor.Ranks.SequenceEqual(rule.RequiredRanks!))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_REQUIRED_SELECTION_MISMATCH");
            return result;
        }
        catch(ExhaustedException error){throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_REQUIRED_SELECTION_MISMATCH",error);}
    }

    internal IEnumerable<MetadataProgramChoiceAnalysis> AnalyzeChoices(MetadataProgramCompositionRule rule,Func<ChoiceCursor,MetadataProgramAnalysis> analyze,
        WorldSpatialWorkBudget budget,CancellationToken token,bool allowDeferredRegions=false,bool allowInheritedSource=false)
    {
        ValidateComposition(rule);
        if(rule.RequiredRanks is not null)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_SELECTION_ALREADY_BOUND");
        var cursor=new ChoiceCursor(this,rule,token,budget);IReadOnlyList<int> prefix=[];
        var failures=new List<MetadataProgramAnalysisFailure>();
        for(int ordinal=1;ordinal<=rule.MaximumAttempts;ordinal++)
        {
            token.ThrowIfCancellationRequested();cursor.Begin(prefix);MetadataProgramAnalysis? result=null;bool exhausted=false;long before=budget.Used;
            try{result=analyze(cursor);}catch(ExhaustedException){exhausted=true;}
            catch(MetadataSpatialConstraintException error)when(error.Data["ProgramAnalysisRegionFailure"] is true)
            {
                failures.Add(new(ordinal,prefix.ToArray(),cursor.Ranks.ToArray(),cursor.Selected.ToArray(),cursor.RegionRequested,error.Message,budget.Used-before));
                if(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(failures,WorldJson.Shared).Length>rule.MaximumReceiptBytes)
                    throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_RECEIPT_BUDGET");
                exhausted=true;
            }
            if(result is not null)
            {
                if(cursor.Ranks.Count==0&&!allowDeferredRegions&&!(allowInheritedSource&&result.SourceProgram?.QualifiedComposition is not null))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_UNUSED");
                yield return new(cursor.Ranks.ToArray(),result,failures.Count==0?null:failures.ToArray());
            }
            // Exhausting a descendant advances the preceding complete selection.
            // No preceding selection means the bounded logical frontier is spent.
            if(cursor.Ranks.Count==0)yield break;
            if(result is null&&!exhausted)throw new InvalidOperationException("METADATA_PROGRAM_ANALYSIS_RESULT_REQUIRED");
            var next=cursor.NextPrefix();if(next is null)yield break;prefix=next;
        }
        var failure=new ArgumentException("METADATA_PROGRAM_COMPOSITION_ANALYSIS_ATTEMPT_BUDGET");
        failure.Data["ProgramAnalysisAttempts"]=failures.ToArray();throw failure;
    }
}
