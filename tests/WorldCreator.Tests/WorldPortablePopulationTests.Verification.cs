using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static partial class WorldPortablePopulationTests
{
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected portable refusal: "+code);
    }
    private static async Task Variants(string output,MetadataRegionalOwnershipPlanner planner,MetadataRegionalWorldGenerator engine,
        MetadataRegionalWorldDefinition definition,MetadataRegionalWorldDocument baseline)
    {
        var independent=definition.Regions.Reverse().Select(r=>engine.GenerateRegion(definition,r.Id)).ToArray();
        Check(Json(engine.Assemble(definition,independent))==Json(baseline),"independent reverse-order requests equal complete generation");
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(baseline),"saved complete world source/analysis/context replay");
        var targetWorld=baseline.Regions.Single(r=>r.RegionId=="beta").World;
        var target=targetWorld.Nodes.Single(n=>n.Kind=="activity-area");
        var blockingVoid=Box(target.Position![0]-10,target.Position[1]-14,20,28);
        Check(targetWorld.AccessPlan!.Sites.Single(s=>s.NodeId==target.Id).Footprint.ContainsPolygon(blockingVoid),
            "terrain counterfactual blocks the actual complete source footprint before replanning");
        var terrainRequest=Example();
        terrainRequest=terrainRequest with{Surface=terrainRequest.Surface with{Exclusions=[terrainRequest.Surface.Exclusions.Single() with{Boundary=blockingVoid}]}};
        await File.WriteAllTextAsync(Path.Combine(output,"terrain-control.json"),Json(new{TargetRegion="beta",TargetNode=target.Id,
            BeforePosition=target.Position,BeforeFootprint=targetWorld.AccessPlan.Sites.Single(s=>s.NodeId==target.Id).Footprint,BlockingVoid=blockingVoid}));
        foreach(var (name,request,sites,plants) in new[]{("terrain",terrainRequest,2,4),("source-size",Example(larger:true),2,4),("more",Example(sites:3,plants:6),3,6)})
        {
            var changed=planner.Plan(request);var world=engine.Generate(changed);Verify(world,sites,plants);
            if(name=="terrain")
            {
                Check(Json(changed.Regions.Select(r=>r.Ownership))==Json(definition.Regions.Select(r=>r.Ownership)),"terrain preserves owned world scope");
                Check(world.Regions.Any(r=>Json(r.World.Nodes)!=Json(baseline.Regions.Single(p=>p.RegionId==r.RegionId).World.Nodes)),"actual excluded terrain revises complete program positions");
                Check(Json(world.Regions.Single(r=>r.RegionId=="beta").World.Nodes.Single(n=>n.Id==target.Id).Position)!=Json(target.Position),
                    "the genuinely blocked owning compound must move without dropping members");
            }
            if(name=="source-size")
            {
                var d=changed.OwnershipPlan!.ProgramDemands!;
                Check(d.All(p=>Math.Abs(p.EnvelopeArea-3200)<1e-7&&p.Analysis.Nodes.Count==16),"64x50 source grows actual demand without removing content");
            }
            await engine.PublishAsync(world,Path.Combine(output,name));await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(request));
        }
    }
    private static async Task Lifecycle(string output,MetadataRegionalOwnershipPlanner planner,MetadataRegionalWorldDefinition initial)
    {
        var generator=new WorldMetadataGenerator();var session=new MetadataRegionalGenerationSession(generator,initial);var before=session.Snapshot;
        var current=Example();var beta=current.Regions.Single(r=>r.Id=="beta");
        var changedCatalog=beta.Catalog with{Archetypes=beta.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Parts=a.Parts.Select(p=>p.Id=="equipment"?
            p with{Purpose="Edited equipment purpose with original source dimensions"}:p).ToArray()}:a).ToArray()};
        var changed=planner.Plan(current with{Regions=current.Regions.Select(r=>r.Id=="beta"?r with{Catalog=changedCatalog}:r).ToArray()});
        var after=await session.ApplyAndPublishAsync(new("beta-content","Update one complete region content program","portable:edit",before.World.DefinitionSha256,before.Report.WorldSha256,changed),Path.Combine(output,"session"));
        Check(Json(after.World)==Json(new MetadataRegionalWorldGenerator(generator).Generate(changed)),"accepted region edit equals clean generation");
        Check(after.World.Regions.Single(r=>r.RegionId=="beta").World.Nodes.Where(n=>n.ArchetypeId=="gear").All(n=>n.Purpose=="Edited equipment purpose with original source dimensions"),"edit changes actual required equipment intent");
        Check(after.Report.Regions.Where(r=>r.RegionId!="beta").All(r=>r.Reused),"unrelated regions reuse exact complete accepted packets");
        Check(before.World.Regions.Where(r=>r.RegionId!="beta").All(r=>Json(r)==Json(after.World.Regions.Single(p=>p.RegionId==r.RegionId))),"unrelated source/content/path bytes stay exact");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"complete source-history checkpoint restores every region");
        var bad=changed with{Regions=changed.Regions.Select(r=>r.Id=="beta"?r with{Recipe=InvalidContext(r.Recipe)}:r).ToArray()};
        try{await restored.ApplyAndPublishAsync(new("bad-context","Refuse missing required source anchor","portable:refusal",after.World.DefinitionSha256,after.Report.WorldSha256,bad),Path.Combine(output,"refused"));throw new Exception("Missing refused edit");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("METADATA_POPULATION_CONTEXT",StringComparison.Ordinal)||error.ReasonCode.Contains("OWNERSHIP_PLAN_MISMATCH",StringComparison.Ordinal),"required context error retained");}
        Check(Json(restored.Snapshot)==Json(after)&&!Directory.Exists(Path.Combine(output,"refused")),"failed edit keeps accepted world/cache/history and no partial output");
    }
    private static WorldMetadataRecipe InvalidContext(WorldMetadataRecipe recipe)=>recipe with{PopulationSource=recipe.PopulationSource! with{Context=new(["missing-anchor"])}};
    private static void Refusals(MetadataRegionalOwnershipRequest input,MetadataRegionalWorldDefinition definition,MetadataRegionalWorldDocument world)
    {
        var generator=new WorldMetadataGenerator();var template=input.Regions[0];var recipe=template.Recipe;var source=recipe.PopulationSource!;
        var analysis=generator.AnalyzeProgram(recipe,template.Catalog);
        Check(analysis.SourceProgram is not null&&analysis.Nodes.Count==16&&analysis.SourceProgram.Nodes.Count==4,"public analysis retains every unplaced source/consumer member");
        Check(analysis.Nodes.All(n=>n.Position is null)&&analysis.SourceProgram!.Nodes.All(n=>n.Position is null),"static analysis does not manufacture actual coordinates");
        Reject(()=>generator.AnalyzeProgram(recipe,template.Catalog,analysis.Work-1),"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET");
        Reject(()=>generator.AnalyzeProgram(recipe with{PopulationSource=source with{Context=new(["regional-origin","regional-origin"])}},template.Catalog),"METADATA_POPULATION_CONTEXT_INVALID");
        Reject(()=>generator.AnalyzeProgram(recipe with{PopulationSource=source with{Recipe=source.Recipe with{Surface=input.Surface}}},template.Catalog),"METADATA_POPULATION_CONTEXT_SOURCE_OVERRIDE");
        Reject(()=>generator.AnalyzeProgram(recipe with{PopulationSource=source with{Recipe=source.Recipe with{Origin=[0,0]}}},template.Catalog),"METADATA_POPULATION_CONTEXT_SOURCE_OVERRIDE");
        Reject(()=>generator.AnalyzeProgram(InvalidContext(recipe),template.Catalog),"METADATA_POPULATION_CONTEXT_ANCHOR_CONFLICT");
        Reject(()=>generator.AnalyzeProgram(recipe with{PopulationSource=source with{Recipe=source.Recipe with{PopulationSource=source}}},template.Catalog),"METADATA_POPULATION_SOURCE_LEVEL_INVALID");
        var mismatch=template.Catalog with{Archetypes=template.Catalog.Archetypes.Select(a=>a.Id==recipe.RootArchetypeId?a with{Purpose="Changed original source member"}:a).ToArray()};
        Reject(()=>generator.AnalyzeProgram(recipe,mismatch),"METADATA_PROGRAM_ANALYSIS_SOURCE_MEMBER_CHANGED");
        var footprint=template.Catalog with{Archetypes=template.Catalog.Archetypes.Select(a=>a.Id=="compound"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-30,-25,60,50)}}:a).ToArray()};
        Reject(()=>generator.AnalyzeProgram(recipe,footprint),"METADATA_PROGRAM_ANALYSIS_SOURCE_FOOTPRINT_CHANGED");
        var access=template.Catalog with{Archetypes=template.Catalog.Archetypes.Select(a=>a.Id=="compound"?a with{Access=a.Access! with{Ports=[]}}:a).ToArray()};
        Reject(()=>generator.AnalyzeProgram(recipe,access),"METADATA_PROGRAM_ANALYSIS_SOURCE_ACCESS_CHANGED");
        var connection=template.Catalog with{Archetypes=template.Catalog.Archetypes.Select(a=>a.Id=="compound"?a with{Connections=a.Connections.Select(c=>c with{Kind="changed-source-connection"}).ToArray()}:a).ToArray()};
        Reject(()=>generator.AnalyzeProgram(recipe,connection),"METADATA_PROGRAM_ANALYSIS_SOURCE_CONNECTION_CHANGED");
        var variation=WorldVariationTests.Example();
        var shifted=variation.Catalog with{Archetypes=variation.Catalog.Archetypes.Select(a=>a.Parts.Any(p=>p.Id=="attachment")?a with{Parts=a.Parts.Select(p=>
            p.Id=="attachment"?p with{Layout=MetadataLineLayout.Parameters([1d,0d],[0d,0d])}:p).ToArray()}:a).ToArray()};
        Reject(()=>generator.AnalyzeProgram(variation.Recipe,shifted),"METADATA_PROGRAM_ANALYSIS_SOURCE_LAYOUT_CHANGED");
        var effective=definition.Regions[0].Recipe with{Surface=world.Regions[0].World.Surface};
        var bound=MetadataPopulationSources.BindContext(effective,default);
        Check(Json(bound.Surface)==Json(effective.Surface)&&Json(bound.Origin)==Json(effective.Origin),"explicit binding inherits exact spatial context");
        Check(Json(generator.Generate(effective,definition.Regions[0].Catalog))==Json(world.Regions[0].World),"ordinary public generation of a bound template equals its complete regional packet");
        Reject(()=>MetadataPopulationSources.BindContext(effective with{Surface=null},default),"METADATA_POPULATION_CONTEXT_REQUIRED");
        Reject(()=>MetadataPopulationSources.BindContext(effective with{Placement=effective.Placement! with{Request=effective.Placement.Request with{Anchors=[]}}},default),"METADATA_POPULATION_CONTEXT_ANCHOR_REQUIRED");
        Reject(()=>MetadataPopulationSources.BindContext(effective,default,new(1,default)),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>new MetadataRegionalOwnershipPlanner().Plan(input with{MaximumWork=1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();try{generator.AnalyzeProgram(recipe,template.Catalog,token:cancel.Token);throw new Exception("Missing analysis cancellation");}catch(OperationCanceledException){}
            try{MetadataPopulationSources.BindContext(effective,cancel.Token);throw new Exception("Missing context cancellation");}catch(OperationCanceledException){}
        }
        var demand=definition.OwnershipPlan!.ProgramDemands![0];var forged=demand with{Analysis=demand.Analysis with{SourceProgram=null}};
        Reject(()=>new MetadataRegionalWorldGenerator().Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{ProgramDemands=[forged,..definition.OwnershipPlan.ProgramDemands.Skip(1)]}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
    }
}
