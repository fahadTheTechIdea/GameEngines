using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldGenerationStudyTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Generation study: "+message);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public List<OperationEvent> Logs=[];public List<OperationEvent> Audit=[];public List<OperationEvent> Notifications=[];
        public Action<OperationEvent>? OnWrite;
        public Task WriteAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Logs.Add(e);OnWrite?.Invoke(e);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Audit.Add(e);return Task.CompletedTask;}
        public Task PublishAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Notifications.Add(e);return Task.CompletedTask;}
        public WorldOperationReporter Reporter=>new(this,this,this);
    }
    internal static MetadataStudyDefinition Example()
    {
        var root=new WorldMetadataArchetype("root","region","Reusable test graph",new Dictionary<string,JsonElement>(),[new("roles","role",3,"Required game roles")],[],[]);
        var role=new WorldMetadataArchetype("role","encounter","Required encounter",new Dictionary<string,JsonElement>(),[],[],[]);
        var catalog=new WorldMetadataCatalog(1,"study-catalog",[root,role]);
        string Node(int i)=>"study-graph/roles-"+i;
        var graph=new WorldMetadataRecipe(1,"study-graph","framework-study",catalog.Id,"root","level",MetadataCoverage.PartialScope,
            ["Analytical graph fixture; broader gameplay remains open."],new Dictionary<string,int>(),new([],"none"),AdditionalConnections:[
                new("a","travel",Node(0),Node(1),false),new("b","travel",Node(1),Node(2),false),new("c","travel",Node(2),Node(0),false)]);
        var star=graph with{AdditionalConnections=Enumerable.Range(0,3).Select(i=>new MetadataExplicitConnection("branch-"+i,"travel",graph.Id,Node(i),false)).ToArray()};
        var pair=WorldSourcePairCoordinationTests.Example();var source=pair.Recipe.PopulationSource!;
        var solid=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Access=new(MetadataOccupancyKind.Solid,[])}:a).ToArray()};
        MetadataStudyCase C(string id,string cohort,WorldMetadataRecipe r,WorldMetadataCatalog c)=>new(id,cohort,"Declared complete fixture","study:test",r,c);
        return new(1,"study-test","Measured output variation and refusals","study:test",[
            C("triangle","graph",graph,catalog),C("repeated","graph",graph,catalog),C("star","graph",star,catalog),
            C("missing-root","graph",graph with{RootArchetypeId="missing"},catalog),
            C("solids","physical",source.Recipe,solid),C("reservations","physical",source.Recipe,source.Catalog),
            C("separated","physical",source.Recipe with{Placement=source.Recipe.Placement! with{Request=source.Recipe.Placement.Request with{MinimumSeparation=2}}},solid)],HistogramBins:4);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Study evidence exists");Directory.CreateDirectory(output);
        var definition=Example();string before=Json(definition);var store=new Store();var study=new MetadataGenerationStudy(new(),store.Reporter);
        var report=await study.PublishAsync(definition,Path.Combine(output,"published"));Check(Json(definition)==before,"authored snapshots are unchanged");
        Check(report.GeneratedCases==6&&report.RejectedCases==1&&report.Samples.Count==7,"refused case remains in denominator");
        MetadataStudySample Sample(string id)=>report.Samples.Single(s=>s.Id==id);
        double Value(string id,string metric)=>Sample(id).Metrics.Single(m=>m.Id==metric).Value!.Value;
        Check(Value("triangle","weak-components")==2&&Value("triangle","explicit-cycle-rank")==1,"triangle and isolated containment root are distinct");
        Check(Value("star","weak-components")==1&&Value("star","explicit-cycle-rank")==0&&Value("star","branch-fraction")==.25,"independent star graph equations");
        Check(Math.Abs(Value("solids","solid-domain-coverage")-.04)<1e-8&&Value("reservations","solid-domain-coverage")==0,"8m² solid union / 200m² domain; reservations cannot fake solids");
        Check(Sample("triangle").PhysicalGeometrySha256==Sample("repeated").PhysicalGeometrySha256&&Sample("triangle").WorldSha256==Sample("repeated").WorldSha256,"repeated inputs remain visible exact duplicates");
        Check(Sample("triangle").HierarchySha256==Sample("star").HierarchySha256,"topology changes do not invent different catalog hierarchy");
        Check(Sample("triangle").ConnectionGraphSha256!=Sample("star").ConnectionGraphSha256&&Sample("triangle").ConnectionGraphSha256==Sample("repeated").ConnectionGraphSha256,
            "connection shape changes and exact graph repeats are distinguished");
        Check(Sample("solids").PhysicalGeometrySha256!=Sample("separated").PhysicalGeometrySha256,"same-count/source-size layout changes remain visible");
        Check(Sample("triangle").Metrics.Single(m=>m.Id=="solid-domain-coverage").Value is null,"unavailable coverage is not zero");
        foreach(var h in report.Histograms)Check(h.Bins.Sum()==h.AvailableCases&&h.GeneratedCases+h.RejectedCases==(h.Cohort=="graph"?4:3)&&h.AvailableCases+h.UnavailableCases==h.GeneratedCases,"histogram denominator identities");
        var absent=report.Histograms.Single(h=>h.Cohort=="graph"&&h.MetricId=="solid-domain-coverage");
        Check(absent.AvailableCases==0&&absent.UnavailableCases==3&&absent.RejectedCases==1&&absent.Minimum is null,"all unavailable and failed observations retained");
        Check(!Directory.Exists(Path.Combine(output,"published/cases/case-missing-root")),"rejection has no world packet");
        foreach(var sample in report.Samples.Where(s=>s.Outcome==MetadataStudyOutcome.Generated))
        {
            var world=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"published/cases/case-"+sample.Id,"world-metadata.json"));
            Check(MetadataExecutionCache.Hash(world)==sample.WorldSha256,"actual published source-bound world matches recorded hash");
        }
        Check(store.Audit.Count(e=>e.EventId=="world.study.case.rejected")==1&&store.Audit.Any(e=>e.EventId=="world.study.published")&&store.Notifications.Any(e=>e.Outcome=="rejected"),"actual operational outcome coverage");
        async Task Refuse(MetadataStudyDefinition input,string code,string name)
        {
            string target=Path.Combine(output,name);try{await study.PublishAsync(input,target);throw new Exception("Expected refusal "+code);}
            catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){}Check(!Directory.Exists(target),"refused study is unpublished");
        }
        await Refuse(definition with{Cases=[definition.Cases[0],definition.Cases[0]]},"METADATA_STUDY_CASE_INVALID","duplicate");
        await Refuse(definition with{Cases=[definition.Cases[0],definition.Cases[4] with{Cohort="graph"}]},"METADATA_STUDY_COHORT_SPACE_MISMATCH","units");
        await Refuse(definition with{Cases=[definition.Cases[0] with{Id="../escape"}]},"METADATA_STUDY_CASE_INVALID","traversal");
        await Refuse(definition with{MaximumMeasurementWork=1},"METADATA_STUDY_MEASUREMENT_WORK_BUDGET","measurement-budget");
        try{await study.PublishAsync(definition,Path.Combine(output,"published"));throw new Exception("Existing target replaced");}catch(IOException){}
        using(var cancellation=new CancellationTokenSource())
        {
            var cancelledStore=new Store{OnWrite=e=>{if(e.EventId=="world.study.case.started")cancellation.Cancel();}};
            try{await new MetadataGenerationStudy(new(),cancelledStore.Reporter).PublishAsync(definition,Path.Combine(output,"cancelled"),cancellation.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}
            Check(!Directory.Exists(Path.Combine(output,"cancelled"))&&cancelledStore.Audit.Any(e=>e.EventId=="world.study.cancelled"),"cancelled study leaves no packet and reports cancellation");
        }
        Check(!Directory.EnumerateDirectories(output,".study-*").Any(),"owned staging is cleaned after refusals");
        var unknown=definition.Cases[0];var unknownCatalog=unknown.Catalog with{Archetypes=unknown.Catalog.Archetypes.Select(a=>a.Id==unknown.Recipe.RootArchetypeId?
            a with{Parts=a.Parts.Select(p=>p with{Layout=new("not-installed",JsonSerializer.SerializeToElement(new{}))}).ToArray()}:a).ToArray()};
        var dependencyReport=await study.RunAsync(definition with{Cases=[unknown with{Catalog=unknownCatalog},definition.Cases[2]]});
        Check(dependencyReport.GeneratedCases==1&&dependencyReport.RejectedCases==1&&dependencyReport.Samples[0].RejectionStage=="dependencies"&&
            dependencyReport.Samples[0].RejectionCode=="METADATA_LAYOUT_UNKNOWN","missing dependency is retained before a later successful case");
        var authored=(Dictionary<string,JsonElement>)definition.Cases[0].Catalog.Archetypes[0].Properties;
        var mutationStore=new Store{OnWrite=e=>{if(e.EventId=="world.study.case.started")authored["outside-change"]=JsonSerializer.SerializeToElement("changed");}};
        var detached=await new MetadataGenerationStudy(new(),mutationStore.Reporter).RunAsync(definition with{Cases=[definition.Cases[0]]});
        authored.Remove("outside-change");
        Check(detached.Samples.Single().CatalogSha256==Sample("triangle").CatalogSha256&&detached.Samples.Single().WorldSha256==Sample("triangle").WorldSha256,
            "notification callbacks cannot mutate the detached study source");
        var triangle=definition.Cases[0];var parallelInput=triangle.Recipe with{AdditionalConnections=[..triangle.Recipe.AdditionalConnections!,
            new("parallel","supply","study-graph/roles-0","study-graph/roles-1",false)]};
        var parallel=new WorldMetadataGenerator().Generate(parallelInput,triangle.Catalog);
        var parallelMetrics=MetadataStudyMetrics.Measure(parallel,5000000,default).Metrics;
        Check(parallelMetrics.Single(m=>m.Id=="branch-fraction").Value==0&&parallelMetrics.Single(m=>m.Id=="explicit-cycle-rank").Value==2,
            "parallel typed edges do not manufacture branching; multigraph cycles remain explicit");
        var maximum=await study.RunAsync(definition with{Cases=Enumerable.Range(0,128).Select(i=>triangle with{Id="sample-"+i}).ToArray()});
        Check(maximum.GeneratedCases==128&&maximum.Samples.Count==128&&maximum.Histograms.All(h=>h.GeneratedCases==128),"all declared cases at the study ceiling retained");
        await Refuse(definition with{Cases=Enumerable.Range(0,129).Select(i=>triangle with{Id="sample-"+i}).ToArray()},"METADATA_STUDY_INVALID","case-budget");
        var routeInput=WorldRouteTests.Example();var routeWorld=new WorldMetadataGenerator().Generate(routeInput.Recipe,routeInput.Catalog);
        var routed=routeWorld.RoutePlan!.Routes[0];var path=new WorldCreator.Core.Geometry.WorldPolyline([new(0,0),new(0,3),new(4,3)]);
        // Analytic metric input, not a claim this altered path is playable in the
        // generated terrain. The public study only measures its own generator output.
        var analytic=routeWorld with{NetworkPlan=null,InteriorPlan=null,RoutePlan=routeWorld.RoutePlan with{Routes=[routed with{
            Forward=routed.Forward with{Path=path},Reverse=routed.Forward with{Id="analytic-return",Path=new(path.Points.Reverse().ToArray())}}]}};
        var routeMetrics=MetadataStudyMetrics.Measure(analytic,5000000,default).Metrics;
        Check(Math.Abs(routeMetrics.Single(m=>m.Id=="mean-directed-route-detour").Value!.Value-1.4)<1e-10&&
            routeMetrics.Single(m=>m.Id=="directed-route-length").Value==14,"3-4-5 detour and both directed lengths measured independently");
        var failedStore=new Store{OnWrite=e=>{if(e.EventId=="world.study.case.started")throw new IOException("Test operational sink failure");}};
        try{await new MetadataGenerationStudy(new(),failedStore.Reporter).PublishAsync(definition,Path.Combine(output,"sink-failed"));throw new Exception("Sink failure swallowed");}catch(IOException){}
        Check(!Directory.Exists(Path.Combine(output,"sink-failed"))&&failedStore.Audit.Any(e=>e.EventId=="world.study.failed"),"unexpected infrastructure failure aborts rather than rejecting a world");
        using(var large=JsonDocument.Parse(JsonSerializer.Serialize(new string('z',9*1048576))))
        {
            var c=definition.Cases[0];var largeCatalog=c.Catalog with{Archetypes=c.Catalog.Archetypes.Select(a=>a.Id==c.Recipe.RootArchetypeId?
                a with{Properties=new Dictionary<string,JsonElement>{{"padding",large.RootElement.Clone()}}}:a).ToArray()};
            var largeStudy=definition with{Id="large-source",Cases=[c with{Catalog=largeCatalog}]};
            var largeReport=await study.PublishAsync(largeStudy,Path.Combine(output,"large-source"));
            Check(largeReport.GeneratedCases==0&&largeReport.RejectedCases==1,"oversized catalog property remains an explicit generator refusal");
            var reloaded=await WorldMetadataGenerator.ReadAsync<MetadataStudyDefinition>(Path.Combine(output,"large-source/study.json"));
            Check(reloaded.Cases[0].Catalog.Archetypes[0].Properties["padding"].GetString()!.Length==9*1048576,"multipart study retains complete original rejected input");
        }
        await File.WriteAllTextAsync(Path.Combine(output,"study-input.json"),Json(definition));
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",Generated=6,Rejected=1,GraphOracles=true,SolidCoverage=.04,ReservationCoverage=0,Histograms=report.Histograms.Count,InputUnchanged=true}));
        Console.WriteLine("PASS: generation study retains complete cases/refusals, analytical graph/solid-area oracles, exact duplicate and changed geometry, comparable histogram denominators, source publication/reload, strict definitions, bounded measurement, cancellation, durable events and existing-output preservation.");
    }
}
