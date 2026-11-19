using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldChunkTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Chunks: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldScaleTests.Example(8,4);
        return(e.Recipe with{Chunks=new(1,"world-storage","Index complete members for spatial loading","example:storage",new(0,0),50,50)},e.Catalog);
    }
    internal static async Task RunAsync(string? output=null)
    {
        var e=Example();var generator=new WorldMetadataGenerator();var planner=new MetadataChunkPlanner();
        var world=generator.Generate(e.Recipe,e.Catalog);var plain=generator.Generate(e.Recipe with{Chunks=null},e.Catalog);var plan=world.ChunkPlan!;
        Check(Json(world.Nodes)==Json(plain.Nodes)&&Json(world.PlacementPlan)==Json(plain.PlacementPlan)&&Json(world.NetworkPlan)==Json(plain.NetworkPlan),"indexing never places or changes geometry");
        Check(plan.Members.Count(m=>m.Kind==MetadataSpatialMemberKind.Node)==169&&plan.Members.Count(m=>m.Kind==MetadataSpatialMemberKind.Path)==14,"all complete world nodes and directed network paths indexed");
        Check(plan.SourceWorldSha256==MetadataChunkPlanner.SourceDigest(world),"all shared package dependencies bound");
        Check(plan.Members.Select(m=>(m.Kind,m.Id)).Distinct().Count()==plan.Members.Count,"one canonical member identity");
        foreach(var member in plan.Members)
        {
            var owner=plan.Cells.Single(c=>c.Id==member.OwnerChunkId);
            Check(member.ChunkIds.Contains(owner.Id)&&member.OwnerLocalAnchor.X>=0&&member.OwnerLocalAnchor.X<50&&member.OwnerLocalAnchor.Y>=0&&member.OwnerLocalAnchor.Y<50,"half-open canonical owner/local anchor");
        }
        var area=plan.Members.Single(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Id==world.Id+"/areas-0");
        Check(area.OwnerChunkId=="10:5"&&area.ChunkIds.SequenceEqual(new[]{"10:5","10:6","11:5","11:6"}),"52x60 area straddles four exact storage cells without splitting");
        var seam=planner.Plan(e.Recipe.Chunks! with{Origin=new(514,260),Width=26,Height=30},world);
        var seamArea=seam.Members.Single(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Id==area.Id);
        Check(seamArea.OwnerChunkId=="1:1"&&seamArea.OwnerLocalAnchor==new WorldPoint(0,0)&&seamArea.ChunkIds.Count==16,"closed exact edge/corner references and one half-open owner");
        var selected=planner.Select(world,plan.Cells.Select(c=>c.Id).Reverse().Concat(plan.Cells.Select(c=>c.Id)).ToArray());
        Check(Json(selected)==Json(plan.Members),"arbitrary load order and duplicate chunk requests never duplicate members");
        Reject(()=>planner.Select(world,["missing"]),"METADATA_CHUNK_SELECTION_INVALID");
        var index=planner.CreateSelectionIndex(world);
        var geometryIndex=planner.CreateGeometryIndex(world);
        Check(geometryIndex.SourceWorldSha256==index.SourceWorldSha256&&geometryIndex.ChunkPlanSha256==index.ChunkPlanSha256&&geometryIndex.Work>0,"geometry snapshot records the exact source and charged extraction work");
        Check(index.SourceWorldSha256==plan.SourceWorldSha256,"snapshot identifies the validated source world");
        var chunkGroups=new[]{Array.Empty<string>(),plan.Cells.Take(1).Select(c=>c.Id).ToArray(),
            area.ChunkIds.ToArray(),plan.Cells.Take(3).Select(c=>c.Id).Reverse().Concat(plan.Cells.Take(3).Select(c=>c.Id)).ToArray(),
            plan.Cells.Select(c=>c.Id).Reverse().ToArray()};
        foreach(var group in chunkGroups)Check(Json(index.Select(group))==Json(planner.Select(world,group)),"indexed selection equals independently validated one-shot selection");
        foreach(var group in chunkGroups)Check(Json(geometryIndex.Select(group).Select(g=>g.Member).ToArray())==Json(index.Select(group)),"geometry selection preserves the exact member identities and order");
        var areaGeometry=geometryIndex.Select(area.ChunkIds).Single(g=>g.Member.Kind==MetadataSpatialMemberKind.Node&&g.Member.Id==area.Id);
        var sourceArea=world.AccessPlan!.Sites.Single(s=>s.NodeId==area.Id);
        Check(areaGeometry.Coverage.Any(p=>p.Kind==MetadataChunkCoverageKind.Footprint&&Json(p.Polygon)==Json(sourceArea.Footprint))&&
            areaGeometry.Occupancy==sourceArea.Occupancy,"full source footprint and occupancy survive chunk seams without clipping");
        var allGeometry=geometryIndex.Select(plan.Cells.Select(c=>c.Id).ToArray());
        foreach(var entry in allGeometry.Where(g=>g.Member.Kind==MetadataSpatialMemberKind.Node))
            Check(entry.Node is not null&&Json(entry.Node)==Json(world.Nodes.Single(n=>n.Id==entry.Member.Id))&&
                Json(entry.Connections)==Json(world.Connections.Where(c=>c.FromId==entry.Member.Id||c.ToId==entry.Member.Id).ToArray()),"node identity, hierarchy, properties and incident links retained");
        Check(allGeometry.Where(g=>g.Member.Kind==MetadataSpatialMemberKind.Path).All(g=>g.Node is null&&g.Connections.Count==0),"route members do not fabricate nodes or incident links");
        var metadataProperties=(IDictionary<string,JsonElement>)areaGeometry.Node!.Properties;
        try{metadataProperties.Add("forged",JsonSerializer.SerializeToElement(1));throw new Exception("Node metadata mutable");}catch(NotSupportedException){}
        foreach(var edge in world.NetworkPlan!.Networks.SelectMany(n=>n.Plan.Edges))
        foreach(var route in new[]{edge.Forward,edge.Reverse})
        {
            var item=allGeometry.Single(g=>g.Member.Kind==MetadataSpatialMemberKind.Path&&g.Member.Id==route.Id);
            Check(Json(item.Path)==Json(route.Path)&&item.PassageRadius==world.NetworkPlan.Networks.Single().Plan.Options.Profile.Radius&&
                item.Coverage.Count==route.Path!.Points.Count-1&&item.Coverage.Select((p,i)=>p.Kind==MetadataChunkCoverageKind.PathSegment&&p.SourceId==route.Id&&p.SegmentIndex==i).All(x=>x),
                "selected path retains whole centerline, radius and typed swept segment envelopes");
        }
        Reject(()=>geometryIndex.Select(["missing"]),"METADATA_CHUNK_SELECTION_INVALID");
        Reject(()=>planner.CreateGeometryIndex(world,0),"METADATA_CHUNK_GEOMETRY_WORK_INVALID");
        Reject(()=>planner.CreateGeometryIndex(world,1),"METADATA_CHUNK_GEOMETRY_WORK_BUDGET");
        Reject(()=>planner.CreateGeometryIndex(world with{ChunkPlan=plan with{Members=plan.Members.Skip(1).ToArray()}}),"METADATA_CHUNK_PLAN_MISMATCH");
        Reject(()=>planner.CreateGeometryIndex(world with{GameProfile="changed-source"}),"METADATA_CHUNK_PLAN_MISMATCH");
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();
            try{planner.CreateGeometryIndex(world,token:cancelled.Token);throw new Exception("Missing geometry creation cancellation");}catch(OperationCanceledException){}
            try{geometryIndex.Select([],cancelled.Token);throw new Exception("Missing geometry selection cancellation");}catch(OperationCanceledException){}
        }
        Check(index.Select(area.ChunkIds).Count(m=>m.Id==area.Id&&m.Kind==MetadataSpatialMemberKind.Node)==1,"seam-spanning member appears once");
        Reject(()=>index.Select(["missing"]),"METADATA_CHUNK_SELECTION_INVALID");
        Reject(()=>planner.CreateSelectionIndex(world with{ChunkPlan=plan with{Members=plan.Members.Skip(1).ToArray()}}),"METADATA_CHUNK_PLAN_MISMATCH");
        Reject(()=>planner.CreateSelectionIndex(world with{GameProfile="modified"}),"METADATA_CHUNK_PLAN_MISMATCH");
        var mutableMembers=plan.Members.Select(m=>m with{ChunkIds=m.ChunkIds.ToList()}).ToList();
        var mutableWorld=world with{ChunkPlan=plan with{Members=mutableMembers}};
        var detached=planner.CreateSelectionIndex(mutableWorld);var beforeMutation=Json(detached.Select(area.ChunkIds));
        var detachedGeometry=planner.CreateGeometryIndex(mutableWorld);var geometryBefore=Json(detachedGeometry.Select(area.ChunkIds));
        ((List<string>)mutableMembers[0].ChunkIds).Clear();mutableMembers.Clear();
        Check(Json(detached.Select(area.ChunkIds))==beforeMutation,"selection snapshot is detached from caller-owned member collections");
        Check(Json(detachedGeometry.Select(area.ChunkIds))==geometryBefore,"geometry snapshot is detached from caller-owned chunk membership");
        var vertices=(IList<WorldPoint>)areaGeometry.Coverage[0].Polygon.Vertices;
        try{vertices[0]=new(0,0);throw new Exception("Coverage geometry mutable");}catch(NotSupportedException){}
        using(var c=new CancellationTokenSource()){c.Cancel();try{index.Select([],c.Token);throw new Exception("Missing snapshot cancellation");}catch(OperationCanceledException){}}

        var resized=generator.Generate(e.Recipe with{Chunks=e.Recipe.Chunks! with{Width=80,Height=75}},e.Catalog);
        Check(Json(resized.Nodes)==Json(world.Nodes)&&Json(resized.PlacementPlan)==Json(world.PlacementPlan)&&Json(resized.NetworkPlan)==Json(world.NetworkPlan),"storage size does not impose world layout");
        Check(Json(resized.ChunkPlan!.Cells)!=Json(plan.Cells),"changed size recomputes ownership index");
        Check(planner.CreateSelectionIndex(resized).ChunkPlanSha256!=index.ChunkPlanSha256,"snapshot identity distinguishes storage definitions over the same source world");
        var negative=generator.Generate(e.Recipe with{Chunks=e.Recipe.Chunks! with{Origin=new(600,600)}},e.Catalog);
        var negativeArea=negative.ChunkPlan!.Members.Single(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Id==area.Id);
        Check(negativeArea.OwnerChunkId=="-2:-7"&&negativeArea.OwnerLocalAnchor==area.OwnerLocalAnchor,"floor semantics for negative chunk coordinates");
        var reversed=e.Recipe with{Placement=e.Recipe.Placement! with{NestedStages=e.Recipe.Placement!.NestedStages!.Reverse().ToArray()}};
        var reordered=generator.Generate(reversed,e.Catalog);
        Check(Json(reordered.ChunkPlan!.Members)==Json(plan.Members)&&Json(reordered.ChunkPlan.Cells)==Json(plan.Cells),"declaration order preserves ownership");

        var pair=WorldScaleTests.Example(2,4);var stripDefinition=e.Recipe.Chunks! with{Origin=new(0,290.5)};
        var wide=generator.Generate(pair.Recipe with{Chunks=stripDefinition},pair.Catalog);
        var rule=pair.Recipe.NetworkRules![0];
        var thin=generator.Generate(pair.Recipe with{Chunks=stripDefinition,NetworkRules=[rule with{Options=rule.Options with{Profile=rule.Options.Profile with{Radius=.1}}}]},pair.Catalog);
        Check(wide.ChunkPlan!.Members.Where(m=>m.Kind==MetadataSpatialMemberKind.Path).All(m=>m.ChunkIds.Any(id=>id.EndsWith(":0",StringComparison.Ordinal))),"swept route width references neighbor beyond centerline");
        Check(thin.ChunkPlan!.Members.Where(m=>m.Kind==MetadataSpatialMemberKind.Path).All(m=>m.ChunkIds.All(id=>id.EndsWith(":-1",StringComparison.Ordinal))),"narrow passage stays on its side of seam");

        var accessExample=WorldPhysicalAccessTests.Example();
        var accessed=generator.Generate(accessExample.Recipe with{Chunks=e.Recipe.Chunks! with{Width=10,Height=10}},accessExample.Catalog);
        // Three destinations in a connected backbone require two bidirectional
        // network edges; the one authored bidirectional connection adds two paths.
        Check(accessed.NetworkPlan!.Networks.Single().Plan.Edges.Count==2&&accessed.RoutePlan!.Routes.Count==1,"access fixture declares two backbone edges and one authored connection");
        var accessGeometry=planner.CreateGeometryIndex(accessed).Select(accessed.ChunkPlan!.Cells.Select(c=>c.Id).ToArray());
        foreach(var site in accessed.AccessPlan!.Sites)
        {
            var item=accessGeometry.Single(g=>g.Member.Kind==MetadataSpatialMemberKind.Node&&g.Member.Id==site.NodeId);
            Check(item.Occupancy==site.Occupancy&&item.Coverage.Count(p=>p.Kind==MetadataChunkCoverageKind.AccessApproach)==site.Ports.Count&&
                site.Ports.All(p=>item.Coverage.Any(c=>c.Kind==MetadataChunkCoverageKind.AccessApproach&&c.SourceId==p.Id)),"access approach roles, port identities and occupancy match source");
        }
        var accessPaths=accessed.RoutePlan!.Routes.SelectMany(r=>new[]{r.Forward.Id,r.Reverse!.Id})
            .Concat(accessed.NetworkPlan.Networks.SelectMany(n=>n.Plan.Edges.SelectMany(edge=>new[]{edge.Forward.Id,edge.Reverse.Id}))).Order(StringComparer.Ordinal);
        Check(accessed.ChunkPlan!.Members.Where(m=>m.Kind==MetadataSpatialMemberKind.Path).Select(m=>m.Id).SequenceEqual(accessPaths),"all six authored and network directions retain exact identities");
        var surroundings=WorldSurroundingsTests.Example();
        var withSurroundings=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(surroundings.Recipe with{Chunks=e.Recipe.Chunks! with{Width=5,Height=5}},surroundings.Catalog);
        Check(withSurroundings.PlacementPlan!.SiteFeatures!.Count>0,"registered surroundings fixture includes source geometry");
        var surroundingsGeometry=new MetadataChunkPlanner(TestCatalogs.Registry).CreateGeometryIndex(withSurroundings)
            .Select(withSurroundings.ChunkPlan!.Cells.Select(c=>c.Id).ToArray());
        foreach(var feature in withSurroundings.PlacementPlan.SiteFeatures)
            Check(surroundingsGeometry.Single(g=>g.Member.Kind==MetadataSpatialMemberKind.Node&&g.Member.Id==feature.NodeId).Coverage.Any(c=>
                c.Kind==MetadataChunkCoverageKind.SiteFeature&&c.SourceId==feature.Id&&c.FeatureKind==feature.Kind&&Json(c.Polygon)==Json(feature.Boundary)),"site feature roles, kinds and full boundaries preserved");
        foreach(var feature in withSurroundings.PlacementPlan.SiteFeatures)
        foreach(var vertex in feature.Boundary.Vertices)
        {
            string cell=$"{Math.Floor(vertex.X/5)}:{Math.Floor(vertex.Y/5)}";
            Check(withSurroundings.ChunkPlan!.Members.Single(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Id==feature.NodeId).ChunkIds.Contains(cell),"every site-feature vertex has its parent member reference");
        }
        Check(Json(new MetadataChunkPlanner(TestCatalogs.Registry).Plan(withSurroundings.ChunkPlan!.Definition,withSurroundings))==Json(withSurroundings.ChunkPlan),"registered source replay uses the supplied catalog owner");

        // A physical source with reservations wider than the visible footprint.
        var p=WorldProtectionTests.Example();var protectedWorld=generator.Generate(p.Recipe with{Chunks=new(1,"detail-storage","Retain reserved surroundings","test:storage",new(.55,.55),1,1)},p.Catalog);
        Check(protectedWorld.ChunkPlan!.Members.Any(m=>m.ChunkIds.Count>=4),"reservation-spanning physical content retained");
        var protectedGeometry=planner.CreateGeometryIndex(protectedWorld).Select(protectedWorld.ChunkPlan.Cells.Select(c=>c.Id).ToArray());
        Check(protectedGeometry.Any(g=>g.Coverage.Any(c=>c.Kind==MetadataChunkCoverageKind.Reservation)),"reservation roles preserved separately from footprint geometry");
        var logical=WorldSurfaceTests.Example();
        var logicalCatalog=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a with{Parts=a.Parts.Select(part=>part with{Layout=null}).ToArray()}).ToArray()};
        var shared=generator.Generate(logical.Recipe with{Surface=null,Origin=null,RequireAllPositions=false,Chunks=e.Recipe.Chunks},logicalCatalog);
        Check(shared.ChunkPlan!.Cells.Count==0&&shared.ChunkPlan.Members.Count==0&&shared.ChunkPlan.SharedNodeIds.Count==shared.Nodes.Count,"topology-only nodes remain shared without fabricated positions");
        Check(planner.CreateGeometryIndex(shared).Select([]).Count==0,"geometry-free nodes remain shared instead of fabricating spatial data");

        Reject(()=>generator.Generate(e.Recipe with{Chunks=e.Recipe.Chunks! with{Width=0}},e.Catalog),"METADATA_CHUNK_DEFINITION_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Chunks=e.Recipe.Chunks! with{MaximumChunks=1}},e.Catalog),"METADATA_CHUNK_CELL_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Chunks=e.Recipe.Chunks! with{MaximumReferences=1}},e.Catalog),"METADATA_CHUNK_REFERENCE_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Chunks=e.Recipe.Chunks! with{MaximumWork=1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Chunks=e.Recipe.Chunks! with{Width=.01,Height=.01}},e.Catalog),"METADATA_CHUNK_SCAN_BUDGET");
        Reject(()=>planner.ValidateDocument(world with{ChunkPlan=plan with{Members=plan.Members.Skip(1).ToArray()}}),"METADATA_CHUNK_PLAN_MISMATCH");
        Reject(()=>planner.ValidateDocument(world with{ChunkPlan=plan with{Members=plan.Members.Select(m=>m.Id==area.Id?m with{OwnerChunkId="11:6"}:m).ToArray()}}),"METADATA_CHUNK_PLAN_MISMATCH");
        Reject(()=>planner.ValidateDocument(world with{GameProfile="changed-shared-package"}),"METADATA_CHUNK_PLAN_MISMATCH");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Select(world,[],c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);
        var before=session.Snapshot;
        var updated=session.Apply(new("chunk-size","Change storage frame only","test:storage",before.World.RecipeSha256,before.World.CatalogSha256,e.Recipe with{Chunks=e.Recipe.Chunks! with{Width=80,Height=75}},e.Catalog));
        Check(Json(updated.World)==Json(resized),"incremental index equals full generation");
        Check(updated.Report.Stages.Where(s=>!s.Reused).Select(s=>s.Id).SequenceEqual(new[]{"chunks"})&&updated.Report.Stages.Single(s=>s.Id=="interiors").Reused,"only ownership stage changes for storage-size edit; interior assembly remains reused");
        try{session.Apply(new("small-budget","Reject incomplete index","test:storage",updated.World.RecipeSha256,updated.World.CatalogSha256,e.Recipe with{Chunks=e.Recipe.Chunks! with{MaximumWork=1}},e.Catalog));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException ex){Check(ex.StageId=="chunks","failure attributed to ownership stage");}
        Check(Json(session.Snapshot)==Json(updated),"failed ownership edit preserves accepted world and cache");
        if(output is not null)
        {
            if(Directory.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));
            await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
            await File.WriteAllTextAsync(Path.Combine(output,"resized.json"),Json(e.Recipe with{Chunks=e.Recipe.Chunks! with{Width=80,Height=75}}));
            await File.WriteAllTextAsync(Path.Combine(output,"refused.json"),Json(e.Recipe with{Chunks=e.Recipe.Chunks! with{MaximumWork=1}}));
            await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
            var loaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world/world-metadata.json"));
            Check(Json(world)==Json(loaded),"published source and chunk plan replay");
            var restoredIndex=planner.CreateSelectionIndex(loaded);
            Check(restoredIndex.ChunkPlanSha256==index.ChunkPlanSha256&&Json(restoredIndex.Select(area.ChunkIds))==Json(index.Select(area.ChunkIds)),"restored packet produces the identical source-bound selection");
            var restoredGeometry=planner.CreateGeometryIndex(loaded);
            Check(restoredGeometry.SourceWorldSha256==geometryIndex.SourceWorldSha256&&Json(restoredGeometry.Select(area.ChunkIds))==Json(geometryIndex.Select(area.ChunkIds)),"published world rebuilds identical geometry selection");
            await File.WriteAllTextAsync(Path.Combine(output,"negative-origin-world.json"),Json(negative));
            await File.WriteAllTextAsync(Path.Combine(output,"reserved-world.json"),Json(protectedWorld));
            await File.WriteAllTextAsync(Path.Combine(output,"logical-world.json"),Json(shared));
            await File.WriteAllTextAsync(Path.Combine(output,"session-report.json"),Json(updated.Report));
            await File.WriteAllTextAsync(Path.Combine(output,"seam-index.json"),Json(seam));
            await File.WriteAllTextAsync(Path.Combine(output,"wide-path-world.json"),Json(wide));
            await File.WriteAllTextAsync(Path.Combine(output,"narrow-path-world.json"),Json(thin));
            await File.WriteAllTextAsync(Path.Combine(output,"access-world.json"),Json(accessed));
            await File.WriteAllTextAsync(Path.Combine(output,"surroundings-world.json"),Json(withSurroundings));
            await session.PublishAsync(Path.Combine(output,"session"));
            var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));
            Check(Json(restored.Snapshot)==Json(updated),"chunk edit history survives checkpoint replay");
        }
        Console.WriteLine($"PASS: {world.Nodes.Count} nodes, {plan.Members.Count} unique members, {plan.Cells.Count} occupied chunks, {plan.Members.Sum(m=>m.ChunkIds.Count)} references; source geometry, seams, loading order, budgets, replay and transactional regeneration.");
    }
}
