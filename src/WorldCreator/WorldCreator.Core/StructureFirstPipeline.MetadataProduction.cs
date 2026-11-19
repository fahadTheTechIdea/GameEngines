using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core;

public enum MetadataProductionTargetKind { Member, Surface, Passage, SiteFeature, Access }
public sealed record MetadataProductionTarget(string Id,MetadataProductionTargetKind Kind,string SourceId,
    string Purpose,string SourceRuleId,string? ReferenceJobId,string? ParentId,
    WorldPoint? Origin,WorldPolygon? Boundary,WorldPolyline? Path,double? WidthMeters);
public sealed record MetadataProductionBoundary(string Id,string Purpose,string SourceRuleId,WorldPolygon Boundary);
public sealed record MetadataProductionInventory(int Version,string Status,string SceneSha256,string WorldSha256,
    IReadOnlyList<MetadataArtMember> Members,IReadOnlyList<MetadataProductionTarget> Targets,
    WorldPolygon Domain,IReadOnlyList<MetadataProductionBoundary> Exclusions,IReadOnlyList<string> LogicalConnectionIds,
    IReadOnlyList<MetadataResolvedFootprint> PhysicalSources,IReadOnlyList<MetadataSiteFeature> SiteFeatures,
    MetadataPhysicalAccessPlan? Access,MetadataCorridorPlan? Corridors,
    IReadOnlyList<string> RemainingGates);
public sealed record MetadataProductionFrame(StructureProductionReference Production,StructureImageReference Master);
public sealed record MetadataProductionBinding(string TargetId,IReadOnlyList<MetadataProductionFrame> Frames,
    IReadOnlyList<string>? GroundAnchorNames=null,double? TileWidthMeters=null,double? TileHeightMeters=null,GameArtsAnimationRequest? Animation=null);
public sealed record MetadataProductionSpecification(int Version,string InventorySha256,IReadOnlyList<MetadataProductionBinding> Bindings);
public sealed record MetadataBoundProductionTarget(MetadataProductionTarget Target,IReadOnlyList<GameArtsMeasuredFrame> Frames,
    double? TileWidthMeters,double? TileHeightMeters,GameArtsAnimationClip? Animation=null);
public sealed record MetadataProductionAssembly(int Version,string Status,MetadataProductionInventory Inventory,
    MetadataProductionSpecification Specification,IReadOnlyList<MetadataBoundProductionTarget> Targets,IReadOnlyList<string> RemainingGates);

public sealed partial class StructureFirstPipeline
{
    public static string MetadataProductionDigest<T>(T value)=>Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions())));

    /// <summary>Derives production demand from the final scene, not the earlier
    /// ground-stage world. All metadata identities and non-rendered geometry remain accounted for.</summary>
    public async Task<MetadataProductionInventory> PlanMetadataProductionAsync(MetadataSceneCompilation scene,
        string trustedImageRoot,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(scene);
        scene=ProductionSnapshot(scene);generator??=new();
        await MockupCapturePipeline.ValidateMetadataSceneCompilationAsync(scene,trustedImageRoot,generator,token);
        var world=scene.World;var source=world.Surface!;
        if(world.Space.Unit!="meters"||source.Boundary is null||source.HeightField is not null||source.Terrain is not null)
            throw new ArgumentException("METADATA_PRODUCTION_EXPLICIT_METER_SURFACE_REQUIRED");
        // Preserve roles and scope ownership while revalidating them against the
        // final geometry. The art-job source world is intentionally a prior stage.
        var handoff=PlanMetadata(world,scene.Recipe,scene.Ground.Handoff.Catalog,
            scene.Ground.Handoff.Specification with{WorldSha256=WorldDigest(world)},generator,token);
        var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var footprints=scene.Ground.Footprints.ToDictionary(f=>f.NodeId,StringComparer.Ordinal);
        var physicalSources=(world.PlacementPlan?.Footprints??[]).Concat(world.PlacementPlan?.NestedStages.SelectMany(s=>s.Footprints)??[])
            .OrderBy(f=>f.NodeId,StringComparer.Ordinal).ToArray();
        var siteFeatures=(world.PlacementPlan?.SiteFeatures??[]).OrderBy(f=>f.Id,StringComparer.Ordinal).ToArray();
        var targets=new List<MetadataProductionTarget>();
        foreach(var member in handoff.Members.Where(m=>m.Role is not (MetadataArtRole.Group or MetadataArtRole.Logical)))
        {
            token.ThrowIfCancellationRequested();var node=nodes[member.NodeId];
            if(!footprints.TryGetValue(node.Id,out var ground)||node.Position is not {Count:2} p)
                throw new ArgumentException("METADATA_PRODUCTION_PHYSICAL_MEMBER_REQUIRED:"+node.Id);
            targets.Add(new("member:"+node.Id,MetadataProductionTargetKind.Member,node.Id,node.Purpose,
                physicalSources.Single(f=>f.NodeId==node.Id).SourceRuleId,member.ReferenceJobId,node.ParentId,new(p[0],p[1]),ground.Compiled,null,null));
        }
        foreach(var channel in WorldTileTopologyPlanner.Sources(world))
            targets.Add(new(channel.Id,Enum.Parse<MetadataProductionTargetKind>(channel.Kind.ToString()),channel.SourceId,
                channel.Purpose,channel.SourceRuleId,null,channel.ParentId,null,channel.Boundary,channel.Path,channel.WidthMeters));
        if(targets.Count is <1 or >10000)throw new ArgumentException("METADATA_PRODUCTION_TARGET_LIMIT");
        var routed=(world.RoutePlan?.Routes??[]).Select(r=>r.ConnectionId).ToHashSet(StringComparer.Ordinal);
        return new(1,"ProductionInventoryDraft",MetadataProductionDigest(scene),WorldDigest(world),handoff.Members,
            targets.OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray(),source.Boundary,
            source.Exclusions.OrderBy(e=>e.Id,StringComparer.Ordinal).Select(e=>new MetadataProductionBoundary(e.Id,e.Purpose,e.SourceRuleId,e.Boundary)).ToArray(),
            world.Connections.Where(c=>!routed.Contains(c.Id)).Select(c=>c.Id).Order(StringComparer.Ordinal).ToArray(),
            physicalSources,siteFeatures,world.AccessPlan,world.CorridorPlan,
            ["Actual source interpretation and owner-approved reference/detail/overview mockups",
             "Canonical GameArts production generation, lifecycle audit and exact-revision approval",
             "Complete terrain/corridor transition topology at the declared native tile resolution",
             "Configured site-feature planting and other contents, beyond their bound surface tiles",
             "Moving-component discovery, separation and native animation where required",
             "Native TileMapLayer, structure, collision, navigation and visual acceptance"]);
    }

    public async Task<MetadataProductionInventory> ExportMetadataProductionInventoryAsync(MetadataSceneCompilation scene,
        string trustedImageRoot,string newFile,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        var result=await PlanMetadataProductionAsync(scene,trustedImageRoot,generator,token);
        await SaveNewJsonAsync(result,newFile,token);return result;
    }

    /// <summary>Validates static-frame and measured-tile-library bindings. This is
    /// not a rasterized world, complete transition kit, animation or native acceptance.</summary>
    public async Task<MetadataProductionAssembly> BindMetadataProductionAsync(MetadataSceneCompilation scene,
        MetadataProductionSpecification specification,string trustedImageRoot,string trustedAssetRoot,
        IApprovedArtRegistry registry,IGameArtsApprovalAuthority assetApprovals,IGameArtsApprovalAuthority conceptApprovals,
        WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(specification);ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(assetApprovals);ArgumentNullException.ThrowIfNull(conceptApprovals);
        scene=ProductionSnapshot(scene);specification=ProductionSnapshot(specification);
        var inventory=await PlanMetadataProductionAsync(scene,trustedImageRoot,generator,token);
        if(specification.Version!=1||specification.InventorySha256!=MetadataProductionDigest(inventory)||specification.Bindings is null||
            specification.Bindings.Count!=inventory.Targets.Count||specification.Bindings.Any(b=>b is null||b.TargetId is null)||
            !specification.Bindings.Select(b=>b.TargetId).ToHashSet(StringComparer.Ordinal).SetEquals(inventory.Targets.Select(t=>t.Id)))
            throw new ArgumentException("METADATA_PRODUCTION_BINDING_COVERAGE_OR_STALE");
        var concepts=scene.Ground.Request.Views.SelectMany(v=>new[]{v.IdentityCapture.Source,v.GroundCapture.Source})
            .Concat(scene.Request.Views.Select(v=>v.Capture.Source)).DistinctBy(s=>(s.MockupId,s.Revision,s.Sha256));
        foreach(var concept in concepts)
            if(!await conceptApprovals.IsApprovedArtifactAsync(concept.MockupId,concept.Revision,concept.Sha256,token))
                throw new ArgumentException("METADATA_PRODUCTION_CONCEPT_NOT_APPROVED:"+concept.MockupId);
        // Validate all declarations before reading the first asset. No omitted or
        // duplicate target can turn into partial production success.
        int count=0;var assetRevisions=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var binding in specification.Bindings)
        {
            var target=inventory.Targets.Single(t=>t.Id==binding.TargetId);
            if(binding.Frames is null||binding.Frames.Count is <1 or >256||(count+=binding.Frames.Count)>4096||
                binding.Frames.Any(f=>f?.Production is null||f.Master is null)||
                binding.Frames.Select(f=>MetadataProductionDigest(f.Production)).Distinct(StringComparer.Ordinal).Count()!=binding.Frames.Count)
                throw new ArgumentException("METADATA_PRODUCTION_FRAMES_INVALID:"+binding.TargetId);
            foreach(var frame in binding.Frames)
            {
                if(string.IsNullOrWhiteSpace(frame.Production.AssetId)||string.IsNullOrWhiteSpace(frame.Production.Revision))
                    throw new ArgumentException("METADATA_PRODUCTION_ASSET_INVALID");
                if(assetRevisions.TryGetValue(frame.Production.AssetId,out var revision)&&revision!=frame.Production.Revision)
                    throw new ArgumentException("METADATA_PRODUCTION_REVISION_CONFLICT");
                assetRevisions[frame.Production.AssetId]=frame.Production.Revision;
            }
            if(target.Kind==MetadataProductionTargetKind.Member)
            {
                if(binding.Frames.Count!=1||binding.TileWidthMeters is not null||binding.TileHeightMeters is not null||
                    binding.GroundAnchorNames is null||binding.GroundAnchorNames.Count!=target.Boundary!.Vertices.Count||
                    binding.GroundAnchorNames.Any(string.IsNullOrWhiteSpace)||binding.GroundAnchorNames.Distinct(StringComparer.Ordinal).Count()!=binding.GroundAnchorNames.Count)
                    throw new ArgumentException("METADATA_PRODUCTION_MEMBER_GROUND_ANCHORS_REQUIRED:"+target.SourceId);
            }
            else if(binding.Animation is not null||binding.GroundAnchorNames is not null||binding.TileWidthMeters is not {} width||binding.TileHeightMeters is not {} height||
                !double.IsFinite(width)||!double.IsFinite(height)||width is <=0 or >1024||height is <=0 or >1024)
                throw new ArgumentException("METADATA_PRODUCTION_TILE_DIMENSIONS_REQUIRED:"+target.SourceId);
        }
        if(assetRevisions.Count>512)throw new ArgumentException("METADATA_PRODUCTION_ASSET_LIMIT");
        var gate=new GameArtsProductionGate(assetApprovals);var output=new List<MetadataBoundProductionTarget>();
        var cache=new Dictionary<string,GameArtsMeasuredFrame>(StringComparer.Ordinal);
        var animations=new Dictionary<string,GameArtsAnimationClip>(StringComparer.Ordinal);
        long animationPoseNodes=0,animationFrames=0;
        foreach(var binding in specification.Bindings.OrderBy(b=>b.TargetId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var target=inventory.Targets.Single(t=>t.Id==binding.TargetId);
            var frames=new List<GameArtsMeasuredFrame>();
            GameArtsAnimationClip? animation=null;
            if(binding.Animation is {} animationRequest)
            {
                string key=MetadataProductionDigest(animationRequest);
                if(!animations.TryGetValue(key,out animation))animations[key]=animation=await gate.ResolveAnimationAsync(animationRequest,registry,trustedAssetRoot,token);
                animationPoseNodes+=(long)animation.Poses.Count*animation.Components.Count;
                animationFrames+=animation.Components.Sum(c=>c.Frames.Count);
                if(animationPoseNodes>262144||animationFrames>32768)throw new ArgumentException("METADATA_PRODUCTION_ANIMATION_WORK_BUDGET");
                var root=animation.Components.Single(c=>c.Parent is null);
                if(animation.Poses.Any(p=>p.Nodes.Single(n=>n.Id==root.Id).WorldMatrix.Where((v,i)=>Math.Abs(v-(i is 0 or 4 or 8?1:0))>1e-6).Any()))
                    throw new ArgumentException("METADATA_PRODUCTION_ANIMATED_ROOT_MUST_REMAIN_FIXED");
                foreach(var frame in root.Frames)CheckGround(frame);
            }
            foreach(var declared in binding.Frames.OrderBy(f=>MetadataProductionDigest(f),StringComparer.Ordinal))
            {
                string key=MetadataProductionDigest(declared);
                GameArtsMeasuredFrame? frame;
                if(animation is not null)
                {
                    var root=animation.Components.Single(c=>c.Parent is null);
                    frame=root.Frames[animation.Poses[0].Nodes.Single(n=>n.Id==root.Id).FrameIndex];
                    if(MetadataProductionDigest(frame.Reference)!=MetadataProductionDigest(declared.Production)||
                        !string.Equals(frame.MasterSha256,declared.Master.Sha256,StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("METADATA_PRODUCTION_ANIMATION_ROOT_REFERENCE_MISMATCH");
                    // Validate the declared file too: identical hashes are not authority
                    // to accept an unsafe/unrelated reference path.
                    string master=MockupCapturePipeline.ResolveSourcePath(trustedAssetRoot,declared.Master.File);
                    await using var stream=File.OpenRead(master);
                    if(!string.Equals(Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token)),frame.MasterSha256,StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("METADATA_PRODUCTION_ANIMATION_ROOT_MASTER_CHANGED");
                }
                else if(!cache.TryGetValue(key,out frame))
                    cache[key]=frame=await gate.ResolveFrameAsync(declared.Production,declared.Master,registry,trustedAssetRoot,token);
                if(target.Kind==MetadataProductionTargetKind.Member)
                {
                    if(animation is null&&frame.AtlasFrameCount!=1)throw new ArgumentException("METADATA_PRODUCTION_STATIC_MEMBER_FRAME_REQUIRED:"+target.SourceId);
                    CheckGround(frame);
                }
                else if(Math.Abs(frame.Width/frame.PixelsPerMeter-binding.TileWidthMeters!.Value)>1e-7||
                    Math.Abs(frame.Height/frame.PixelsPerMeter-binding.TileHeightMeters!.Value)>1e-7)
                    throw new ArgumentException("METADATA_PRODUCTION_TILE_SCALE_MISMATCH:"+target.SourceId);
                frames.Add(frame);
            }
            output.Add(new(target,frames,binding.TileWidthMeters,binding.TileHeightMeters,animation));
            void CheckGround(GameArtsMeasuredFrame frame)
            {
                for(int i=0;i<target.Boundary!.Vertices.Count;i++)
                {
                    var expected=target.Boundary.Vertices[i];
                    if(!frame.Anchors.TryGetValue(binding.GroundAnchorNames![i],out var measured)||
                        Math.Abs((measured.X-frame.Pivot.X)/frame.PixelsPerMeter+target.Origin!.Value.X-expected.X)>1e-6||
                        Math.Abs((measured.Y-frame.Pivot.Y)/frame.PixelsPerMeter+target.Origin!.Value.Y-expected.Y)>1e-6)
                        throw new ArgumentException("METADATA_PRODUCTION_GROUND_CALIBRATION_MISMATCH:"+target.SourceId);
                }
            }
        }
        specification=specification with{Bindings=specification.Bindings.OrderBy(b=>b.TargetId,StringComparer.Ordinal)
            .Select(b=>b with{Frames=b.Frames.OrderBy(f=>MetadataProductionDigest(f),StringComparer.Ordinal).ToArray()}).ToArray()};
        return new(1,output.Any(t=>t.Animation is not null)?"SampledAnimationBindingsVerified":"StaticBindingsVerified",inventory,specification,output,
            inventory.RemainingGates);
    }

    private static T ProductionSnapshot<T>(T value)=>JsonSerializer.Deserialize<T>(
        JsonSerializer.Serialize(value,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;

    public async Task<MetadataProductionAssembly> ExportMetadataProductionAsync(MetadataSceneCompilation scene,
        MetadataProductionSpecification specification,string trustedImageRoot,string trustedAssetRoot,
        IApprovedArtRegistry registry,IGameArtsApprovalAuthority assetApprovals,IGameArtsApprovalAuthority conceptApprovals,
        string newFile,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        var result=await BindMetadataProductionAsync(scene,specification,trustedImageRoot,trustedAssetRoot,registry,
            assetApprovals,conceptApprovals,generator,token);
        await SaveNewJsonAsync(result,newFile,token);return result;
    }

    public async Task ValidateMetadataProductionAssemblyAsync(MetadataSceneCompilation scene,MetadataProductionAssembly assembly,
        string trustedImageRoot,string trustedAssetRoot,IApprovedArtRegistry registry,IGameArtsApprovalAuthority assetApprovals,
        IGameArtsApprovalAuthority conceptApprovals,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(assembly);token.ThrowIfCancellationRequested();
        var replay=await BindMetadataProductionAsync(scene,assembly.Specification,trustedImageRoot,trustedAssetRoot,
            registry,assetApprovals,conceptApprovals,generator,token);
        if(MetadataProductionDigest(replay)!=MetadataProductionDigest(assembly))
            throw new ArgumentException("METADATA_PRODUCTION_ASSEMBLY_REPLAY_MISMATCH");
    }
}
