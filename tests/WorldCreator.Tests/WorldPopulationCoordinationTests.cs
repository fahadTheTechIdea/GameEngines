using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPopulationCoordinationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Population coordination: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        const string id="coordinated-area";var boundary=Box(0,0,10,6);var properties=new Dictionary<string,JsonElement>();
        var root=new WorldMetadataArchetype("root","region","Complete source and future program",properties,[new("sites","site",1,"Required source member")],[],[]);
        var site=new WorldMetadataArchetype("site","site","Source-sized member",properties,[],[],[],
            PhysicalFootprint:new("meters","Exact source member","test:source",Box(-1,-1,2,2)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var sourceCatalog=new WorldMetadataCatalog(1,"coordination-catalog",[root,site]);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("x","Authored frame","test:x","meters",0,[new(WorldFieldInput.X,1)])]);
        var profile=new WorldSuitabilityProfile("home","Complete supported area","test:home",["ground"],[],[new("center","x","Preferred source access area","test:center",5,10,1)]);
        var request=new WorldPlacementRequest("source","Place required source member","test:source",boundary,[new("site","Required source","test:site",null,profile)],[],[],MinimumSeparation:1);
        var source=new WorldMetadataRecipe(1,id,"framework-reference",sourceCatalog.Id,"root","area",MetadataCoverage.PartialScope,["Framework technical reference; Godot deferred"],new Dictionary<string,int>(),new(["x","y"],"meters"),[0,0],
            Surface:new(1,"ground","meters",boundary,[],[new("ground","ground","Supported terrain","test:ground",boundary,0,new(0,0),0,0,0)]),Environment:fields,
            Regions:new("home","Complete area for source and future members","test:home",boundary,profile,[new("home","Required home region","test:home",new(5,3),60)]),
            Placement:new(request,[new("site",id+"/sites-0",true)]));
        var feature=new WorldMetadataArchetype("feature","feature","Required area member",properties,[],[],[],
            PhysicalFootprint:new("meters","Exact area-member source","test:feature",Box(-.2,-.2,.4,.4),Box(-.3,-.3,.6,.6)),Access:new(MetadataOccupancyKind.Solid,[]));
        var catalog=sourceCatalog with{Archetypes=[root with{Parts=[..root.Parts,new("features","feature",1,"Required future area member")]},site,feature]};
        return(source with{Chunks=new(1,"chunks","Index every accepted member","test:chunks",new(0,0),5,3),PopulationSource=new(source,sourceCatalog),Placement=source.Placement! with{
            Request=request with{Execution=WorldPlacementExecution.Prescribed,Demands=[..request.Demands,new("features","Required area population","test:area",null,profile,RegionId:"home")]},
            Bindings=[..source.Placement.Bindings,new("features",id,true,PartId:"features",Population:new("Preserve required area centroid","test:area",Region:new("home",Area:new())))]}},catalog);
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var baseRecipe=e.Recipe.PopulationSource!.Recipe;var profile=baseRecipe.Placement!.Request.Demands[0].Profile;
        var demand=new WorldSiteDemand("a","Exact source","test:source",Box(-1,-1,2,2),profile);
        var request=baseRecipe.Placement.Request with{Demands=[demand,demand with{Id="b"}]};
        var query=new WorldEnvironmentQuery(new(baseRecipe.Surface!),baseRecipe.Environment!);var placer=new WorldSitePlacementPlanner();
        var pair=placer.Plan(query,request);var assignments=pair.Sites.Select(s=>new WorldPlacementPose(s.DemandId,s.Position)).ToArray();
        var forbidden=request with{Nogoods=[new("pair","Reject only this complete pair","test:nogood",assignments)]};
        var alternate=placer.Plan(query,forbidden);
        Check(alternate.Status==WorldPlacementStatus.Found&&alternate.Sites.Any(s=>s.Position!=assignments.Single(a=>a.DemandId==s.DemandId).Position),"multi-pose nogood rejects only the complete combination");
        Check(alternate.Sites.Any(s=>s.Position==assignments.Single(a=>a.DemandId==s.DemandId).Position),"partial matching poses remain eligible");
        Reject(()=>placer.Plan(query,forbidden with{MaximumWork=alternate.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Check(placer.Plan(query,forbidden with{MaximumWork=alternate.Work}).Work==alternate.Work,"nogood work is charged to the existing exact ceiling");
        Reject(()=>placer.Plan(query,request with{Nogoods=[new("invalid","Invalid duplicate pose","test",[assignments[0],assignments[0]])]}),"WORLD_PLACEMENT_NOGOOD_INVALID");
        Reject(()=>placer.Plan(query,request with{Nogoods=[new("invalid","Unknown demand","test",[new("missing",new(1,1))])]}),"WORLD_PLACEMENT_NOGOOD_INVALID");
        Reject(()=>placer.Plan(query,request with{Nogoods=[new("invalid","Invalid provenance digest","test",[assignments[0]],"wrong")]}),"WORLD_PLACEMENT_NOGOOD_INVALID");
        var plan=world.PopulationCoordinationPlan??throw new Exception("Missing coordination receipt");
        Check(plan.Attempts.Count==2&&!plan.Attempts[0].Accepted&&plan.Attempts[1].Accepted,"exactly one provisional source rejection");
        Check(world.Nodes.Count==3&&world.AccessPlan!.Sites.Count==2,"every original source and future member retained");
        var feature=world.Nodes.Single(n=>n.ArchetypeId=="feature");Check(feature.Position!.SequenceEqual(new[]{5d,3d}),"prescribed future centroid unchanged");
        var source=world.PlacementPlan!.Definition.SourceWorld!;var site=source.Nodes.Single(n=>n.ArchetypeId=="site");
        Check(!site.Position!.SequenceEqual(new[]{5d,3d}),"source reconsidered before commitment");
        Check(Json(site)==Json(world.Nodes.Single(n=>n.Id==site.Id)),"selected source exact after commitment");
        Check(source.PlacementPlan!.Definition.Nogoods!.Single().ConsumerRecipeSha256==world.RecipeSha256,"guard belongs to complete original consumer input");
        Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic whole receipt");
        WorldMetadataGenerator.ValidateDocument(world);
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=null}),"METADATA_POPULATION_COORDINATION_PLAN_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=plan with{Work=plan.Work-1}}),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=plan with{Attempts=plan.Attempts.Skip(1).ToArray()}}),"METADATA_POPULATION_COORDINATION_PLAN_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},e.Catalog),"METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumWork=plan.Work-1}},e.Catalog),"METADATA_POPULATION_COORDINATION_WORK_BUDGET");
        var sockets=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.PartId=="features"?b with{
            Population=new("Exact authored socket","test:socket",Sockets:[new(e.Recipe.Id+"/features-0",new(5,3))])}:b).ToArray()}};
        var socketWorld=generator.Generate(sockets,e.Catalog);
        Check(socketWorld.PopulationCoordinationPlan!.Attempts[0].AddedNogood!.WholeAssignmentSha256 is not null&&socketWorld.Nodes.Single(n=>n.ArchetypeId=="feature").Position!.SequenceEqual(new[]{5d,3d}),"exact authored socket uses complete-source reconsideration without moving its anchor");
        var regional=e.Recipe.Regions! with{Seeds=[],Intents=[new("home","Required complete home area","test:intent",60)]};
        var composed=e.Recipe with{Regions=regional,PopulationSource=e.Recipe.PopulationSource! with{Recipe=e.Recipe.PopulationSource.Recipe with{Regions=regional}},
            ProgramComposition=new("composed-coordination","Keep regional selection and population planning coherent","test:composition")};
        var mixed=generator.Generate(composed,e.Catalog);
        Check(mixed.ProgramCompositionPlan!.Attempts.Count==1&&mixed.PopulationCoordinationPlan!.Attempts.Count==2,
            "region-composition cursor keeps one accepted selection across two provisional source layouts");
        WorldMetadataGenerator.ValidateDocument(mixed);
        using(var canceled=new CancellationTokenSource()){canceled.Cancel();try{generator.Generate(e.Recipe,e.Catalog,canceled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        var files=await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
        Check(Json(files)==Json(world),"file API exact result");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var bad=e.Recipe with{Placement=e.Recipe.Placement! with{MaximumFeasibilityAttempts=1}};
        try{session.Apply(new("refuse","Preserve accepted world on exhausted planning","test:refuse",before.World.RecipeSha256,before.World.CatalogSha256,bad,e.Catalog));throw new Exception("Failed edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(before),"failed edit rolls back all provisional choices");
        var repeated=session.Apply(new("repeat","Replay identical accepted inputs","test:repeat",before.World.RecipeSha256,before.World.CatalogSha256,e.Recipe,e.Catalog));
        Check(Json(repeated.World)==Json(before.World)&&repeated.Report.Stages.All(s=>s.Reused),"selected provisional cache is adopted and reusable");
        Check(repeated.Report.Stages.Select(s=>s.Id).Distinct().Count()==repeated.Report.Stages.Count,"failed attempts never leak duplicate accepted stages");
        string chunkHash=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(repeated.World.ChunkPlan,WorldJson.CreateOptions())));
        Check(repeated.Report.Stages.Single(s=>s.Id=="chunks").OutputSha256==chunkHash,"terminal chunk stage includes accepted coordination receipt");
        var two=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=a.Parts.Select(p=>p.Id=="features"?p with{Count=2}:p).ToArray()}:a).ToArray()};
        var expanded=session.Apply(new("expand","Generate both required area members","test:expand",repeated.World.RecipeSha256,repeated.World.CatalogSha256,e.Recipe,two));
        Check(expanded.World.Nodes.Count==4&&expanded.World.PopulationCoordinationPlan is null,"changed demand regenerates without stale provisional exclusions");
        Check(expanded.World.Nodes.Single(n=>n.ArchetypeId=="site").Position!.SequenceEqual(new[]{5d,3d}),"newly feasible preferred source pose is restored");
        Check(Json(expanded.World)==Json(generator.Generate(e.Recipe,two))&&!expanded.Report.Stages.Single(s=>s.Id=="content").Reused,"accepted demand edit equals clean generation");
        await session.PublishAsync(Path.Combine(folder,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(expanded),"checkpoint reproduces source reconsideration, demand edit and exact accepted world");
        Console.WriteLine("PASS: bounded source reconsideration preserves all source sizes/counts and fixed area centroid; two-attempt receipt, missing/tampered proof refusal, attempt/work limits, cancellation, file replay and failed-edit/checkpoint rollback.");
    }
}
