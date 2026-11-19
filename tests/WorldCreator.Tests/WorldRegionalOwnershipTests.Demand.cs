using WorldCreator.Core.Metadata;

internal static partial class WorldRegionalOwnershipTests
{
    internal static MetadataRegionalOwnershipRequest DemandExample(bool enlarged=false)
    {
        var source=Example();return source with{Regions=source.Regions.Select(r=>r with{
            Capacity=new("Keep room for complete programs and game-authored open space","ownership:coverage",.5),
            Catalog=!enlarged||r.Id!="alpha"?r.Catalog:r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.Id!="compound"?a:a with{
                PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-9,-5,18,10)},
                Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p.Id=="east"?p with{BoundaryPosition=new(9,0),ApproachPosition=new(10,0)}:p with{BoundaryPosition=new(-9,0),ApproachPosition=new(-10,0)}).ToArray()}}).ToArray()}}).ToArray()};
    }
    internal static async Task DemandAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var source=DemandExample();var definition=planner.Plan(source);var demands=definition.OwnershipPlan!.ProgramDemands!;
        Check(demands.Count==3,"one complete program analysis per policy");
        foreach(var d in demands)
        {
            Check(d.Analysis.Nodes.Count==5&&d.Analysis.Nodes.All(n=>n.Position is null)&&d.Sources.Count==1,"all nested members analyzed, only root envelope counted");
            Near(d.EnvelopeArea,120,"actual root source area");Near(d.RequiredArea,240,"coverage derives area");Near(d.Weight,240,"authored bias times required area");
            Near(d.RequiredClearance,8,"explicit larger clearance stays hard");
        }
        var changedSource=DemandExample(true);var changed=planner.Plan(changedSource);var grown=changed.OwnershipPlan!.ProgramDemands!.Single(d=>d.RegionId=="alpha");
        Near(grown.EnvelopeArea,180,"larger source measured");Near(grown.RequiredArea,360,"source change derives increased demand");
        Near(Math.Abs(changed.Regions.Single(r=>r.Id=="alpha").Ownership.SignedArea()),8000*1.5/3.5,"larger program receives proportional ownership");
        var world=engine.Generate(definition);var expanded=engine.Generate(changed);
        foreach(var w in new[]{world,expanded})Check(w.Regions.Sum(r=>r.World.Nodes.Count)==15&&w.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==9&&w.Connections.All(c=>c.Reverse is not null),"complete physical members and journeys survive");
        Check(Json(engine.Assemble(changed,changed.Regions.Reverse().Select(r=>engine.GenerateRegion(changed,r.Id)).ToArray()))==Json(expanded),"independent requests equal full demand-derived generation");
        await engine.PublishAsync(world,Path.Combine(output,"world"));await engine.PublishAsync(expanded,Path.Combine(output,"enlarged"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"enlarged/world-regions.json")))==Json(expanded),"saved full world replay");
        var session=new MetadataRegionalGenerationSession(new(),definition);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("larger-source","Reallocate for larger complete program","ownership:demand-edit",
            before.World.DefinitionSha256,before.Report.WorldSha256,changed),Path.Combine(output,"session-enlarged"));
        Check(Json(after.World)==Json(expanded),"source edit equals clean generation");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session-enlarged/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"source history restores analysis, demand and complete world");
        var forged=demands[0] with{EnvelopeArea=1};
        Reject(()=>engine.Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{ProgramDemands=[forged,..demands.Skip(1)]}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        var missing=demands[0] with{Analysis=demands[0].Analysis with{Nodes=demands[0].Analysis.Nodes.SkipLast(1).ToArray()}};
        Reject(()=>engine.Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{ProgramDemands=[missing,..demands.Skip(1)]}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        var invalid=changed with{OwnershipPlan=changed.OwnershipPlan with{ProgramDemands=[missing,..changed.OwnershipPlan.ProgramDemands!.Skip(1)]}};
        try{session.Apply(new("forged-analysis","Refuse loss of required members","ownership:refusal",after.World.DefinitionSha256,after.Report.WorldSha256,invalid));throw new Exception("Missing failed edit");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH"){}
        Check(Json(session.Snapshot)==Json(after),"failed analysis edit preserves accepted world");
        var floor=planner.Plan(source with{Regions=source.Regions.Select(r=>r.Id=="alpha"?r with{MinimumArea=1000,MinimumClearance=10,Weight=2}:r).ToArray()}).OwnershipPlan!.ProgramDemands!.Single(d=>d.RegionId=="alpha");
        Near(floor.RequiredArea,1000,"authored area remains a hard minimum");Near(floor.RequiredClearance,10,"authored clearance remains hard");Near(floor.Weight,2000,"authored allocation bias remains meaningful");
        Reject(()=>planner.Plan(source with{Regions=source.Regions.Select(r=>r with{Capacity=r.Capacity! with{MaximumCoverage=0}}).ToArray()}),"METADATA_REGIONAL_PROGRAM_CAPACITY_INVALID");
        Reject(()=>planner.Plan(source with{MaximumWork=1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));await File.WriteAllTextAsync(Path.Combine(output,"enlarged-request.json"),Json(changedSource));
        Console.WriteLine("PASS: complete program analysis derives 240/360 area demand, proportional ownership, actual 15-node/9-source worlds, independent requests, replay and tamper refusal.");
    }
}
