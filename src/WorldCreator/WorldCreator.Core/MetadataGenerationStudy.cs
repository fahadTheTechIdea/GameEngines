using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Operations;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataStudyCase(string Id,string Cohort,string Purpose,string SourceRuleId,
    WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataStudyDefinition(int Version,string Id,string Purpose,string SourceRuleId,
    IReadOnlyList<MetadataStudyCase> Cases,int HistogramBins=12,long MaximumMeasurementWork=5000000);
public enum MetadataStudyOutcome { Generated, Rejected }
public sealed record MetadataStudyMetric(string Id,string Unit,double? Value,int Observations);
public sealed record MetadataStudySample(string Id,string Cohort,MetadataStudyOutcome Outcome,string? RejectionCode,string? RejectionStage,
    string RecipeSha256,string CatalogSha256,string? WorldSha256,string? HierarchySha256,string? PhysicalGeometrySha256,string? ConnectionGraphSha256,
    MetadataCoverage Coverage,IReadOnlyList<string> RemainingScopes,IReadOnlyList<MetadataGenerationDependency> Dependencies,
    IReadOnlyList<MetadataStudyMetric> Metrics,MetadataAccountingStatus? AccountingStatus,string? AccountingReason,
    long MeasurementWork,double GenerationAndMeasurementSeconds,long ProcessAllocatedBytesDelta,long ProcessPeakWorkingSetBytes);
public sealed record MetadataStudyHistogram(string Cohort,string MetricId,string Unit,int GeneratedCases,int RejectedCases,
    int AvailableCases,int UnavailableCases,double? Minimum,double? Maximum,double? Mean,IReadOnlyList<int> Bins);
public sealed record MetadataStudyReport(int Version,string Id,string DefinitionSha256,string AlgorithmId,string Runtime,string OS,
    string Architecture,int LogicalProcessors,int GeneratedCases,int RejectedCases,IReadOnlyList<MetadataStudySample> Samples,
    IReadOnlyList<MetadataStudyHistogram> Histograms,IReadOnlyList<string> Limitations);

/// <summary>The complete study result exists, but completion reporting failed.
/// Recover this report/publication instead of rerunning it as a failed generation.</summary>
public sealed class MetadataStudyReportingException(string correlationId,MetadataStudyReport report,string? outputDirectory,Exception inner)
    :Exception("METADATA_STUDY_COMPLETED_REPORTING_FAILED",inner)
{
    public string CorrelationId {get;}=correlationId;
    public MetadataStudyReport Report {get;}=report;
    public string? OutputDirectory {get;}=outputDirectory;
    public bool Completed=>true;
    public bool Committed=>OutputDirectory is not null;
}

/// <summary>Measures declared recipe variants with the current engine. Rejections remain observations;
/// no retries, input mutation or automatic reduction of mandatory content.</summary>
public sealed class MetadataGenerationStudy(WorldMetadataGenerator generator,WorldOperationReporter reporter)
{
    public Task<MetadataStudyReport> RunAsync(MetadataStudyDefinition definition,CancellationToken token=default)
        =>ExecuteAsync(definition,null,token);
    public Task<MetadataStudyReport> PublishAsync(MetadataStudyDefinition definition,string newOutputDirectory,CancellationToken token=default)
        =>ExecuteAsync(definition,newOutputDirectory,token);

    private async Task<MetadataStudyReport> ExecuteAsync(MetadataStudyDefinition definition,string? output,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(generator);ArgumentNullException.ThrowIfNull(reporter);
        string correlation=Guid.NewGuid().ToString("N"),targetId=Spatial.WorldEnvironmentQuery.Id(definition?.Id)?definition!.Id:"invalid";
        Task Event(string id,string outcome,string message,OperationSeverity severity=OperationSeverity.Information,CancellationToken? cancellation=null)
            =>reporter.ReportAsync(new(id,DateTimeOffset.UtcNow,severity,nameof(MetadataGenerationStudy),"world.study",correlation,message,targetId,Outcome:outcome),
                notify:severity>=OperationSeverity.Warning||id=="world.study.completed",cancellationToken:cancellation??token);
        string? staging=null,parent=null;Exception? failure=null;
        bool completed=false;MetadataStudyReport? completedReport=null;string? completedOutput=null;
        try
        {
            token.ThrowIfCancellationRequested();ValidateDefinition(definition!);definition=await MetadataJsonStorage.SnapshotAsync(definition!,token);
            await Event("world.study.started","started","Declared world generation study started.");
            string definitionHash=MetadataExecutionCache.Hash(definition),algorithm=MetadataExecutionCache.AlgorithmId;
            var dependencies=new Dictionary<string,IReadOnlyList<MetadataGenerationDependency>>(StringComparer.Ordinal);
            string? target=output is null?null:Path.GetFullPath(output);
            if(target is not null)
            {
                if(File.Exists(target)||Directory.Exists(target))throw new IOException("METADATA_STUDY_OUTPUT_EXISTS");
                parent=Path.GetDirectoryName(target)??throw new ArgumentException("METADATA_STUDY_OUTPUT_INVALID");
                Directory.CreateDirectory(parent);staging=Path.Combine(parent,".study-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
            }
            var samples=new List<MetadataStudySample>();
            foreach(var item in definition.Cases)
            {
                token.ThrowIfCancellationRequested();await Event("world.study.case.started","started","Study case started: "+item.Id);
                var clock=Stopwatch.StartNew();long allocated=GC.GetTotalAllocatedBytes(false);WorldMetadataDocument? world=null;string? rejection=null;
                string stage="dependencies";
                // Catch only expected generator input/constraint refusals here. Analysis,
                // dependency, publication and unexpected runtime failures abort the study.
                try
                {
                    dependencies[item.Id]=generator.Dependencies(item.Recipe,item.Catalog,token);stage="generation";
                    world=generator.Generate(item.Recipe,item.Catalog,token);
                }
                catch(ArgumentException error)when(IsRejection(error)){rejection=Code(error);}
                catch(JsonException){rejection="METADATA_STUDY_JSON_REJECTED";}
                MetadataStudyMeasurement? measured=world is null?null:MetadataStudyMetrics.Measure(world,definition.MaximumMeasurementWork,token);
                clock.Stop();
                using var process=Process.GetCurrentProcess();process.Refresh();
                var sample=new MetadataStudySample(item.Id,item.Cohort,world is null?MetadataStudyOutcome.Rejected:MetadataStudyOutcome.Generated,rejection,world is null?stage:null,
                    MetadataExecutionCache.Hash(item.Recipe),MetadataExecutionCache.Hash(item.Catalog),world is null?null:MetadataExecutionCache.Hash(world),
                    measured?.HierarchySha256,measured?.GeometrySha256,measured?.ConnectionGraphSha256,item.Recipe.Coverage,item.Recipe.RemainingScopes.ToArray(),dependencies.GetValueOrDefault(item.Id,[]),
                    measured?.Metrics??[],measured?.Accounting.Status,measured?.Accounting.Reason,measured?.Work??0,clock.Elapsed.TotalSeconds,
                    Math.Max(0,GC.GetTotalAllocatedBytes(false)-allocated),process.PeakWorkingSet64);
                samples.Add(sample);
                if(world is not null&&staging is not null)
                    await generator.PublishAsync(item.Recipe,item.Catalog,world,Path.Combine(staging,"cases","case-"+item.Id),null,token);
                CheckDependencies();
                await Event(world is null?"world.study.case.rejected":"world.study.case.generated",world is null?"rejected":"generated",
                    world is null?"Study case rejected: "+item.Id+" ("+rejection+").":"Study case generated: "+item.Id,
                    world is null?OperationSeverity.Warning:OperationSeverity.Information);
            }
            var report=new MetadataStudyReport(1,definition.Id,definitionHash,algorithm,RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,RuntimeInformation.ProcessArchitecture.ToString(),Environment.ProcessorCount,
                samples.Count(s=>s.Outcome==MetadataStudyOutcome.Generated),samples.Count(s=>s.Outcome==MetadataStudyOutcome.Rejected),samples.AsReadOnly(),
                MetadataStudyMetrics.Histograms(definition,samples),[
                    "Declared cases are the sample population; no random-stream use or exhaustive expressive range is implied.",
                    "Metrics are technical output observations, not player enjoyment, beauty or complete game/world acceptance.",
                    "Solid coverage measures classified AccessPlan source-footprint unions only. It excludes generated interior walls, corridor surfaces, terrain, reservation-only ground and colored allocations; it is not total built coverage. Unavailable geometry remains unavailable.",
                    "The explicit connection graph ignores containment and physical reachability; weak graph metrics do not prove traversal.",
                    "Cycle rank counts explicit multigraph edges; branch fraction counts at least three distinct weak neighbors. Graph fingerprints bind declared node/edge identities, not graph isomorphism.",
                    "Timing covers generation/measurement, excludes publication; allocation deltas and peak working set are process-wide, not isolated per-case peaks.",
                    "Histograms group declared comparable cohorts. Bins use each observed range, so compare bounds and availability as well as frequencies.",
                    "Dependency-selection refusals record their stage; unavailable dependencies are not invented or substituted."]);
            completedReport=report;
            if(staging is not null)
            {
                await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"study.json"),definition,token);
                await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"study-report.json"),report,token);
                await MetadataDirectoryPublication.CommitAsync(staging,target!,_=>CheckDependencies(),token);
                completed=true;completedOutput=target;
                // The atomic move is the commit point. Report its actual result even
                // if cancellation arrives immediately afterwards.
                await Event("world.study.published","published","Study report and generated case packets published.",cancellation:CancellationToken.None);
            }
            else {token.ThrowIfCancellationRequested();completed=true;}
            await Event("world.study.completed",report.RejectedCases==0?"completed":"completed-with-rejections",
                $"Study completed: {report.GeneratedCases} generated, {report.RejectedCases} rejected.",cancellation:CancellationToken.None);
            return report;
            void CheckDependencies()
            {
                token.ThrowIfCancellationRequested();
                if(MetadataExecutionCache.AlgorithmId!=algorithm||definition.Cases.Where(c=>dependencies.ContainsKey(c.Id)).Any(c=>
                    !MetadataPopulationSources.Equal(dependencies[c.Id],generator.Dependencies(c.Recipe,c.Catalog,token))))
                    throw new InvalidOperationException("METADATA_STUDY_DEPENDENCY_CHANGED");
            }
        }
        catch(Exception error)
        {
            failure=error;
            if(completed)throw new MetadataStudyReportingException(correlation,completedReport!,completedOutput,error);
            try{await Event(error is OperationCanceledException?"world.study.cancelled":"world.study.failed",
                error is OperationCanceledException?"cancelled":"failed","Study did not complete: "+Code(error),OperationSeverity.Warning,CancellationToken.None);}
            catch(Exception reporting){throw new AggregateException("METADATA_STUDY_AND_REPORTING_FAILED",error,reporting);}
            throw;
        }
        finally{if(staging is not null)MetadataDirectoryPublication.Cleanup(staging,parent!,".study-",failure);}
    }
    private static string Code(Exception error)
    {
        string prefix=error.Message.Split([':', '\r','\n'],2)[0];
        return prefix.Length<=160&&prefix.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_')?prefix:error.GetType().Name;
    }
    private static bool IsRejection(ArgumentException error)
    {
        string code=Code(error);
        return code.StartsWith("METADATA_",StringComparison.Ordinal)||code.StartsWith("WORLD_",StringComparison.Ordinal)||
            code.StartsWith("CATALOG_",StringComparison.Ordinal)||code.StartsWith("STRUCTURE_",StringComparison.Ordinal);
    }
    /// <summary>Validates the bounded study envelope and comparable cohorts.
    /// Individual recipe/catalog feasibility is evaluated and retained per case.</summary>
    public static void ValidateDefinition(MetadataStudyDefinition d)
    {
        if(d is null||d.Version!=1||!Spatial.WorldEnvironmentQuery.Id(d.Id)||!Spatial.WorldEnvironmentQuery.Text(d.Purpose)||
            !Spatial.WorldEnvironmentQuery.Text(d.SourceRuleId)||d.Cases is not {Count:>=1 and <=128}||d.HistogramBins is <1 or >64||
            d.MaximumMeasurementWork is <1 or >200000000)throw new ArgumentException("METADATA_STUDY_INVALID");
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var cohorts=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var c in d.Cases)
        {
            if(c is null||!Spatial.WorldEnvironmentQuery.Id(c.Id)||c.Id is "." or ".."||c.Id.IndexOfAny(['/', '\\', ':'])>=0||
                c.Id.EndsWith('.')||!ids.Add(c.Id)||!Spatial.WorldEnvironmentQuery.Id(c.Cohort)||!Spatial.WorldEnvironmentQuery.Text(c.Purpose)||
                !Spatial.WorldEnvironmentQuery.Text(c.SourceRuleId)||c.Recipe?.Space?.Axes is null||
                c.Recipe.Space.Axes.Count>8||!Spatial.WorldEnvironmentQuery.Text(c.Recipe.Space.Unit)||c.Recipe.RemainingScopes is null||c.Catalog is null)
                throw new ArgumentException("METADATA_STUDY_CASE_INVALID");
            string space=MetadataExecutionCache.Hash(c.Recipe.Space);
            if(cohorts.TryGetValue(c.Cohort,out var prior)&&space!=prior)throw new ArgumentException("METADATA_STUDY_COHORT_SPACE_MISMATCH");
            cohorts[c.Cohort]=space;
        }
    }
}
