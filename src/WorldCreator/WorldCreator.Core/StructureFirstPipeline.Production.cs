using WorldCreator.Core.Capture;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core;

public sealed partial class StructureFirstPipeline
{
    /// <summary>Static structure layer only. Authorities must resolve actual GameArts
    /// lifecycle records; specification flags cannot supply approval.</summary>
    public async Task<StructureFirstProductionResult> BuildProductionAsync(WorldDocument world,
        StructureFirstSpecification specification, string trustedAssetRoot,
        IApprovedArtRegistry registry, IGameArtsApprovalAuthority assetApprovals,
        IGameArtsApprovalAuthority conceptApprovals, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(assetApprovals);
        ArgumentNullException.ThrowIfNull(conceptApprovals);
        var draft = await BuildAsync(world, specification, trustedAssetRoot, token);
        var issues = draft.ProductionIssues.ToList();
        foreach (var concept in specification.Concepts ?? [])
            if (!await conceptApprovals.IsApprovedArtifactAsync(concept.MockupId, concept.Revision, concept.Image.Sha256, token))
                issues.Add("STRUCTURE_SCOPE_CONCEPT_NOT_APPROVED:" + concept.ScopeId);
        var visual = world.Request.Design!.Blueprint;
        if (!await conceptApprovals.IsApprovedAsync(visual.MockupId, visual.ApprovedRevision, token))
            issues.Add("STRUCTURE_WORLD_VISUAL_NOT_APPROVED");
        var bindings = new List<WorldArtBinding>(); var frames = new List<StructureFramePlacement>();
        foreach (var group in draft.Assembly.Instances.GroupBy(i => i.StructureJobId))
        {
            token.ThrowIfCancellationRequested(); var art = group.First().Art;
            if (art.Production is not { } production) continue;
            if (art.Master is null) { issues.Add("STRUCTURE_MASTER_REQUIRED:" + group.Key); continue; }
            GameArtsMeasuredFrame measured;
            try
            {
                measured = await new GameArtsProductionGate(assetApprovals).ResolveFrameAsync(
                    production, art.Master, registry, trustedAssetRoot, token);
            }
            catch (ArgumentException ex)
            {
                issues.Add("STRUCTURE_FRAME_REJECTED:" + group.Key + ":" + ex.Message);
                continue;
            }
            if (measured.AtlasFrameCount != 1)
            { issues.Add("STRUCTURE_STATIC_FRAME_REQUIRED:" + group.Key); continue; }
            var center = measured.Pivot; double scale = measured.PixelsPerMeter;
            string imageFile = MockupCapturePipeline.ResolveSourcePath(trustedAssetRoot, measured.ImagePath);
            foreach (var item in group)
            {
                token.ThrowIfCancellationRequested(); var source = item.Placement.VisualSource!;
                string[] names = ["ground-nw","ground-ne","ground-se","ground-sw"];
                if (source.Footprint!.Vertices.Count != 4 || names.Where((name,index) =>
                    !measured.Anchors.TryGetValue(name, out var point) ||
                    Math.Abs((point.X-center.X)/scale + source.PositionMeters.X-source.Footprint.Vertices[index].X)>1e-6 ||
                    Math.Abs((point.Y-center.Y)/scale + source.PositionMeters.Y-source.Footprint.Vertices[index].Y)>1e-6).Any())
                { issues.Add("STRUCTURE_GROUND_CALIBRATION_MISMATCH:" + item.Placement.Id); continue; }
                bindings.Add(new(item.Placement.Id, production.AssetId, production.ComponentId, production.Facing, 3, item.Placement.ParentObjectId));
                frames.Add(new(item.Placement.Id, production.AssetId, imageFile, measured.ImageSha256, measured.ImageWidth, measured.ImageHeight,
                    measured.X,measured.Y,measured.Width,measured.Height,scale,center,source.PositionMeters));
            }
        }
        if (issues.Count != 0) return new(draft, null, [], issues.Distinct(StringComparer.Ordinal).ToArray());
        var structurePlan = world.Design! with { Objects = draft.Assembly.Instances.Select(i => i.Placement).ToArray() };
        var approved = await new WorldArtAssembler(registry).AssembleAsync(structurePlan, bindings, token);
        issues.AddRange(approved.Issues.Select(i => i.Code + ":" + i.AssetId));
        return new(draft, approved.IsValid ? approved : null, approved.IsValid ? frames : [], issues);
    }
}
