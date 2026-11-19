using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Metadata;

namespace WorldCreator.Core.Capture;

/// <summary>Instance identity only. This never converts a roof into a ground footprint.</summary>
public sealed record MetadataCaptureBinding(string NodeId, string FeatureId);
public sealed record MetadataCaptureSpecification(int Version, string HandoffSha256,
    string CaptureSha256, string JobId, IReadOnlyList<MetadataCaptureBinding> Bindings);
public sealed record MetadataCaptureMember(string NodeId, string? ParentId, MetadataArtRole Role,
    string? ScopeId, IReadOnlyList<string> FeatureIds, bool Matched);
public sealed record MetadataCaptureReconciliation(int Version, string Status,
    MetadataCaptureSpecification Specification, MockupCaptureDocument Capture,
    IReadOnlyList<MetadataCaptureMember> Members, IReadOnlyList<string> UnassignedFeatureIds,
    IReadOnlyList<CaptureIssue> Issues, IReadOnlyList<string> RemainingGates);

public static partial class MockupCapturePipeline
{
    public static string MetadataHandoffDigest(MetadataArtHandoff handoff) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(handoff, WorldJson.CreateOptions())));

    public static string CaptureDigest(MockupCaptureDocument capture) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(capture, MockupCaptureJson.Options())));

    /// <summary>Checks an entire detail/overview job against verified source bytes and
    /// reviewed annotations. Matching inventory is neither visual truth nor map acceptance.</summary>
    public static async Task<MetadataCaptureReconciliation> ReconcileMetadataAsync(
        MetadataArtHandoff handoff, MockupCaptureDocument capture, MetadataCaptureSpecification specification,
        string trustedImageRoot, WorldMetadataGenerator? generator = null, CancellationToken token = default)
        => await ReconcileMetadataCoreAsync(handoff,capture,specification,trustedImageRoot,generator,token,true);

    private static async Task<MetadataCaptureReconciliation> ReconcileMetadataCoreAsync(
        MetadataArtHandoff handoff, MockupCaptureDocument capture, MetadataCaptureSpecification specification,
        string trustedImageRoot, WorldMetadataGenerator? generator, CancellationToken token, bool replayHandoff)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(specification);
        // Caller-owned collections cannot change while source I/O is in progress.
        T Snapshot<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, WorldJson.CreateOptions()), WorldJson.CreateOptions())!;
        handoff = Snapshot(handoff); capture = Snapshot(capture); specification = Snapshot(specification);
        if (specification.Version != 1 || specification.HandoffSha256 != MetadataHandoffDigest(handoff) ||
            specification.CaptureSha256 != CaptureDigest(capture) || string.IsNullOrWhiteSpace(specification.JobId) ||
            specification.Bindings is null || specification.Bindings.Count > 2000 ||
            specification.Bindings.Any(b => b is null || string.IsNullOrWhiteSpace(b.NodeId) || string.IsNullOrWhiteSpace(b.FeatureId)))
            throw new ArgumentException("METADATA_CAPTURE_SPECIFICATION_INVALID_OR_STALE");
        if(replayHandoff)new StructureFirstPipeline().ValidateMetadataHandoff(handoff, generator, token);
        var job = handoff.Jobs.SingleOrDefault(j => j.Id == specification.JobId);
        if (job is null || job.Kind is not ("DetailMockup" or "OverviewMockup"))
            throw new ArgumentException("METADATA_CAPTURE_SCOPE_JOB_REQUIRED");
        await ReadSourceAsync(capture, trustedImageRoot, token);

        var selected = job.NodeIds.ToHashSet(StringComparer.Ordinal);
        var expected = handoff.Members.Where(m => selected.Contains(m.NodeId) && m.Role is not (MetadataArtRole.Group or MetadataArtRole.Logical))
            .OrderBy(m => m.NodeId, StringComparer.Ordinal).ToArray();
        var requiredIds = expected.Select(m => m.NodeId).ToHashSet(StringComparer.Ordinal);
        var nodes = handoff.World.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var features = capture.Features.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var byNode = specification.Bindings.ToLookup(b => b.NodeId, StringComparer.Ordinal);
        var byFeature = specification.Bindings.ToLookup(b => b.FeatureId, StringComparer.Ordinal);
        var issues = new List<CaptureIssue>();
        void Issue(string code, string id, string message) => issues.Add(new(code, id, message));
        if (capture.Coverage != CaptureCoverage.Complete || capture.Unresolved.Count != 0 || capture.CompilationScope is not null)
            Issue("METADATA_CAPTURE_PARTIAL", "capture", "Whole job inspection is required; a selected compilation subset cannot establish inventory completeness.");
        foreach (var binding in specification.Bindings)
        {
            token.ThrowIfCancellationRequested();
            if (!requiredIds.Contains(binding.NodeId))
                Issue("METADATA_CAPTURE_TARGET_OUTSIDE_JOB", binding.FeatureId, binding.NodeId);
            if (!features.ContainsKey(binding.FeatureId))
                Issue("METADATA_CAPTURE_FEATURE_MISSING", binding.FeatureId, binding.NodeId);
        }
        foreach (var group in byFeature.Where(g => g.Count() > 1))
            Issue("METADATA_CAPTURE_FEATURE_REUSED", group.Key, "A single observed feature cannot satisfy multiple required instances or repeated bindings.");
        var members = new List<MetadataCaptureMember>();
        foreach (var member in expected)
        {
            token.ThrowIfCancellationRequested();
            var ids = byNode[member.NodeId].Select(b => b.FeatureId).Order(StringComparer.Ordinal).ToArray();
            bool matched = false;
            if (ids.Length != 1)
                Issue(ids.Length == 0 ? "METADATA_CAPTURE_INSTANCE_MISSING" : "METADATA_CAPTURE_INSTANCE_AMBIGUOUS",
                    member.NodeId, "Every required instance needs exactly one reviewed identity annotation.");
            else if (features.TryGetValue(ids[0], out var feature))
            {
                matched = byFeature[feature.Id].Count() == 1 && feature.Evidence == CaptureEvidence.Observed &&
                    feature.Review == CaptureReview.Accepted && !feature.Occluded;
                if (!matched)
                    Issue("METADATA_CAPTURE_IDENTITY_UNRESOLVED", feature.Id, "Require a unique, observed, accepted and unoccluded instance; no inferred replacement.");
                string? expectedParent = nodes[member.NodeId].ParentId;
                while (expectedParent is not null && !requiredIds.Contains(expectedParent))
                    expectedParent = nodes[expectedParent].ParentId;
                string? capturedParent = feature.ParentId;
                while (capturedParent is not null && !byFeature[capturedParent].Any())
                    capturedParent = features[capturedParent].ParentId;
                var parentBindings = capturedParent is null ? [] : byFeature[capturedParent].ToArray();
                if (expectedParent is null ? parentBindings.Length != 0 :
                    parentBindings.Length != 1 || parentBindings[0].NodeId != expectedParent)
                {
                    matched = false;
                    Issue("METADATA_CAPTURE_PARENT_MISMATCH", feature.Id, "Captured containment must preserve the nearest visual metadata ancestor in this job.");
                }
            }
            members.Add(new(member.NodeId, nodes[member.NodeId].ParentId, member.Role, member.ScopeId, ids, matched));
        }
        var unassigned = features.Keys.Where(id => !byFeature[id].Any()).Order(StringComparer.Ordinal).ToArray();
        foreach (var id in unassigned)
            Issue("METADATA_CAPTURE_FEATURE_UNASSIGNED", id, "Retain this observed content and reconcile it with the catalog; do not silently discard extra structures or parts.");
        specification = specification with { Bindings = specification.Bindings.OrderBy(b => b.NodeId, StringComparer.Ordinal)
            .ThenBy(b => b.FeatureId, StringComparer.Ordinal).ToArray() };
        return new(1, issues.Count == 0 ? "DeclaredInventoryMatched" : "Incomplete", specification, capture, members, unassigned,
            issues.Distinct().OrderBy(i => i.Code, StringComparer.Ordinal).ThenBy(i => i.FeatureId, StringComparer.Ordinal).ToArray(),
            ["Source annotations and their identity claims require actual visual inspection; this report does not perform vision.",
             "Canonical GameArts profile, exact artifact lifecycle and approvals remain independently required for artwork.",
             "Ground geometry, scale calibration, source dimensions, surroundings, terrain and routes must be reconciled before map compilation.",
             "Production sprites/tiles and native Godot import, collision, navigation, animation and runtime acceptance remain separate."]);
    }
}
