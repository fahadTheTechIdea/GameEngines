using WorldCreator.Core.Metadata;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static partial class WorldNestedSourceTests
{
    internal static void BoundaryGeometry()
    {
        WorldPolygon[] cells=[new([new(0,0),new(140,0),new(140,56.517072990401225),new(0,63.659941879564514)]),
            new([new(140,0),new(144.9832249707043,0),new(144.9832249707043,56.262826400323036),new(140,56.517072990401225)]),
            new([new(144.9832249707043,0),new(146.79361386017436,56.170459468352455),new(144.9832249707043,56.26282640032304)])];
        string before=Json(cells);var boundary=new WorldPolygon([new(0,0),new(200,0),new(200,100),new(0,100)]);
        var surface=new WorldSurfaceDefinition(1,"ground","meters",boundary,[],[new("ground","ground","Original domain","nested-source:geometry",boundary,0,new(0,0),0,0,0)]);
        var profile=new WorldSuitabilityProfile("ground","Original ground","nested-source:geometry",["ground"],[],[]);
        var environment=new WorldEnvironmentQuery(new(surface),new(1,"fields",[new("neutral","Declared neutral test field","nested-source:geometry","relative",1,[])]));
        var components=WorldRegionGeometry.Components(environment,profile,cells,new(1000000,default),default);Check(components.Count==1,"shared source faces remain one component");
        var loops=MetadataGeneratedBoundary.Loops(components[0].BoundaryEdges,new(1000000,default),default);
        Check(loops.Count==1&&Json(cells)==before,"source cells unchanged while qualified shared-face vertices close exactly");
        Check(Math.Abs(loops[0].SignedArea()-cells.Sum(c=>c.SignedArea()))<1e-7,"closed source outline retains actual original area");
        Check(loops[0].Vertices.All(v=>cells.Any(c=>c.Vertices.Contains(v))),"outline vertices retain original source coordinates rather than a rounded grid");
        Reject(()=>MetadataGeneratedBoundary.Sample(new(MetadataBoundarySource.Footprint,1,StartEdgeIndex:1,UseLongestEdge:true),1,null,null,new(1000,default),default),"METADATA_GENERATED_BOUNDARY_INVALID");
        Console.WriteLine("PASS shared-face boundary closure without source-cell mutation");
    }
    internal static async Task ControlsAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        BoundaryGeometry();var generator=new WorldMetadataGenerator();var planner=new MetadataRegionalOwnershipPlanner(generator);var input=Example();var first=input.Regions[0];var pop=first.Recipe.PopulationSource!;
        MetadataRegionalOwnershipRequest Change(MetadataRegionalProgramRegion r)=>input with{Regions=[r,..input.Regions.Skip(1)]};
        Reject(()=>planner.Plan(Change(first with{SpatialContext=null})),"METADATA_REGIONAL_OWNERSHIP_PROGRAM_INVALID");
        Reject(()=>planner.Plan(Change(first with{Recipe=first.Recipe with{PopulationSource=pop with{Context=pop.Context! with{UsePlacementBoundary=false}}}})),"METADATA_REGIONAL_CONTEXT_INVALID");
        Reject(()=>planner.Plan(Change(first with{Recipe=first.Recipe with{Regions=pop.Recipe.Regions}})),"METADATA_POPULATION_CONTEXT_HABITAT_OVERRIDE");
        Reject(()=>planner.Plan(Change(first with{Recipe=first.Recipe with{PopulationSource=pop with{Recipe=pop.Recipe with{Regions=pop.Recipe.Regions! with{Boundary=input.Surface.Boundary!}}}}})),"METADATA_REGIONAL_CONTEXT_INVALID");
        Reject(()=>planner.Plan(Change(first with{Recipe=first.Recipe with{PopulationSource=pop with{Recipe=pop.Recipe with{Surface=input.Surface}}}})),"METADATA_POPULATION_CONTEXT_SOURCE_OVERRIDE");
        var second=input.Regions[1];var p2=second.Recipe.PopulationSource!;
        Reject(()=>planner.Plan(input with{Regions=[first,second with{Recipe=second.Recipe with{PopulationSource=p2 with{Recipe=p2.Recipe with{
            LocalLayouts=p2.Recipe.LocalLayouts! with{Layouts=p2.Recipe.LocalLayouts.Layouts.Select(l=>l with{RegionFrame=null}).ToArray()}}}}}]}),"METADATA_REGIONAL_CONTEXT_INVALID");
        var ordinary=WorldSourceProgramTests.Example();
        Reject(()=>generator.AnalyzeProgram(ordinary.Recipe with{ProgramComposition=new("empty","No unused independent rank bypass","nested-source:invalid",RequiredRanks:[])},ordinary.Catalog),"METADATA_PROGRAM_COMPOSITION_RULE_INVALID");
        Reject(()=>generator.Generate(ordinary.Recipe with{ProgramComposition=new("empty","No unused independent rank bypass","nested-source:invalid",RequiredRanks:[])},ordinary.Catalog),"METADATA_PROGRAM_COMPOSITION_RULE_INVALID");
        var growing=Example(population:true,largerAllocation:true);
        Reject(()=>planner.Plan(growing with{Coupling=growing.Coupling! with{MaximumIterations=1},ProgramComposition=growing.ProgramComposition! with{MaximumAttempts=1}}),"METADATA_REGIONAL_JOINT_ATTEMPT_LIMIT");
        Reject(()=>planner.Plan(input with{MaximumWork=1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(input,cancel.Token);throw new Exception("Missing nested source cancellation");}catch(OperationCanceledException){}}
        Console.WriteLine("PASS nested source context/conflict/empty-rank/limit/cancellation controls");
        var source=Example(population:true);var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,source);var initial=session.Snapshot;
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest r)=>new(id,"Edit the complete nested source world","nested-source:edit",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,r);
        var changed=await session.ApplyDerivedAndPublishAsync(Edit("allocation",growing),Path.Combine(output,"allocation"));
        Check(changed.World.Regions.All(p=>p.World.TerrainDemandPlan!.Results[0].Count>initial.World.Regions.Single(r=>r.RegionId==p.RegionId).World.TerrainDemandPlan!.Results[0].Count),"upstream allocation edit recomputes every complete downstream quantity");
        string accepted=Json(changed);
        try{await session.ApplyDerivedAsync(Edit("work-limit",growing with{MaximumWork=1}));throw new Exception("Missing failed nested-source edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==accepted,"failed source edit preserves accepted world");
        session=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"allocation/generation-session.json"));
        Check(Json(session.Snapshot)==accepted,"complete source/coupling/consumer history restores");
        var rollback=await session.ApplyDerivedAndPublishAsync(Edit("rollback",source),Path.Combine(output,"rollback"));
        Check(Json(rollback.World)==Json(initial.World)&&rollback.Report.WorldSha256==initial.Report.WorldSha256,"rollback restores exact original source and world");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"rollback/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(rollback),"both accepted edits replay in order");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",AcceptedEdits=2,Initial=initial.Report.WorldSha256,Changed=changed.Report.WorldSha256,Rollback=rollback.Report.WorldSha256}));
        Console.WriteLine("PASS complete nested source edit/history/refusal/rollback");
    }
}
