using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Metadata;
using WorldCreator.Catalogs.Modern;

internal static class MetadataCaptureTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception("Metadata capture: " + message); }
    static string Json<T>(T value) => JsonSerializer.Serialize(value, WorldJson.CreateOptions(true));

    public static async Task RunAsync(string output)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output exists");
        Directory.CreateDirectory(output);
        var bytes = await File.ReadAllBytesAsync("tests/fixtures/mockup-capture-source.png");
        var imageHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var pipeline = new StructureFirstPipeline();
        foreach (var name in new[] { "interior", "registered" })
        {
            string folder = Path.Combine(output, name); Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, "source.png"), bytes);
            var example = WorldMetadataTests.Example(name == "interior" ? "dungeon" : "town");
            var catalog = name == "registered" ? example.Catalog with
            {
                Archetypes = example.Catalog.Archetypes.Select(a => a.Id == "site" ? a with
                { Structure = new(ModernCatalog.Selection, "warehouse", "small") } : a).ToArray()
            } : example.Catalog;
            var generator = name == "registered" ? new WorldMetadataGenerator(TestCatalogs.Registry) : new WorldMetadataGenerator();
            var world = generator.Generate(example.Recipe, catalog);
            var containers = world.Nodes.Where(n => n.ParentId is not null).Select(n => n.ParentId!).ToHashSet();
            var art = new MetadataArtSpecification(1, "capture-test", StructureFirstPipeline.WorldDigest(world),
                world.Nodes.DistinctBy(n => n.ArchetypeId).Select(n => new MetadataArtBinding(n.ArchetypeId,
                    containers.Contains(n.Id) ? MetadataArtRole.Group : MetadataArtRole.Structure)).ToArray(),
                [new("overview", null, "Entire synthetic program", []), ..world.Nodes.Where(n => n.ParentId == world.Id)
                    .Select((n, i) => new MetadataArtScope("detail-" + i, "overview", n.Purpose, [n.Id]))]);
            var handoff = pipeline.PlanMetadata(world, example.Recipe, catalog, art, generator);
            var required = handoff.Members.Where(m => m.Role != MetadataArtRole.Group).ToArray();
            var features = required.Select((m, i) => new CaptureFeature("feature-" + i, "building.roof", CaptureShape.Polygon,
                [new(10,10),new(20,10),new(20,20),new(10,20)], CaptureEvidence.Observed, CaptureReview.Accepted, 1, false,
                "Synthetic identity assertion; this fixture does not demonstrate real image vision.")).ToArray();
            var capture = new MockupCaptureDocument(1, new("synthetic", "r1", "source.png", imageHash, 100, 100,
                "test", "test-camera", "synthetic-no-approval"), CaptureCoverage.Complete, [], features, []);
            var bindings = required.Select((m, i) => new MetadataCaptureBinding(m.NodeId, features[i].Id)).ToArray();
            MetadataCaptureSpecification Spec(MockupCaptureDocument c, IReadOnlyList<MetadataCaptureBinding> b, string job = "scope:overview") =>
                new(1, MockupCapturePipeline.MetadataHandoffDigest(handoff), MockupCapturePipeline.CaptureDigest(c), job, b);
            Task<MetadataCaptureReconciliation> Run(MockupCaptureDocument c, IReadOnlyList<MetadataCaptureBinding> b) =>
                MockupCapturePipeline.ReconcileMetadataAsync(handoff, c, Spec(c, b), folder, generator);
            var report = await pipeline.ExportMetadataCaptureAsync(handoff, capture, Spec(capture, bindings), folder,
                Path.Combine(folder, "report.json"), generator);
            Check(report.Status == "DeclaredInventoryMatched" && report.Members.Count == required.Length && report.Members.All(m => m.Matched), "complete declared identities");
            Check(report.Members.All(m => m.ParentId == world.Nodes.Single(n => n.Id == m.NodeId).ParentId), "exact nested parents");
            Check(report.Capture.Calibration is null && report.Capture.Features.All(f => f.Semantic == "building.roof" && f.Binding is null), "identity does not manufacture geometry/calibration");
            Check(report.RemainingGates.Count == 4 && handoff.Status == "DraftInventoryOnly", "no approval or map acceptance");
            Check(Json(report) == Json(await Run(capture, bindings.Reverse().ToArray())), "binding declaration order canonical");
            foreach (var item in new[] { ("handoff.json", Json(handoff)), ("capture.json", JsonSerializer.Serialize(capture, MockupCaptureJson.Options())), ("bindings.json", Json(Spec(capture, bindings))) })
                await File.WriteAllTextAsync(Path.Combine(folder, item.Item1), item.Item2);
            Check(Json(await MockupCapturePipeline.ReadCaptureAsync(Path.Combine(folder, "capture.json"))) == Json(capture), "canonical camelCase capture file preserved");
            var missing = await Run(capture, bindings.Skip(1).ToArray());
            Check(missing.Status == "Incomplete" && missing.Members.Count == required.Length && !missing.Members[0].Matched &&
                missing.Issues.Any(i => i.Code == "METADATA_CAPTURE_INSTANCE_MISSING") && missing.UnassignedFeatureIds.Contains(features[0].Id), "missing structure retained, extra annotation visible");
            await File.WriteAllTextAsync(Path.Combine(folder, "missing-bindings.json"), Json(Spec(capture, bindings.Skip(1).ToArray())));
            await File.WriteAllTextAsync(Path.Combine(folder, "missing-report.json"), Json(missing));
            var duplicate = await Run(capture, [..bindings, bindings[0]]);
            Check(duplicate.Status == "Incomplete" && duplicate.Issues.Any(i => i.Code == "METADATA_CAPTURE_INSTANCE_AMBIGUOUS"), "duplicate instance cannot count twice");
            var reused = await Run(capture, bindings.Select((b, i) => i == 1 ? b with { FeatureId = bindings[0].FeatureId } : b).ToArray());
            Check(reused.Issues.Any(i => i.Code == "METADATA_CAPTURE_FEATURE_REUSED") && reused.Members.Count(m => !m.Matched) == 2, "one annotation cannot satisfy two instances");
            var unknown = await Run(capture, [..bindings.Skip(1), bindings[0] with { NodeId = "absent" }]);
            Check(unknown.Issues.Any(i => i.Code == "METADATA_CAPTURE_TARGET_OUTSIDE_JOB"), "foreign target refused");
            var absent = await Run(capture, bindings.Select((b, i) => i == 0 ? b with { FeatureId = "absent" } : b).ToArray());
            Check(absent.Issues.Any(i => i.Code == "METADATA_CAPTURE_FEATURE_MISSING"), "missing annotation refused");
            foreach (var feature in new[] { features[0] with { Review = CaptureReview.Pending }, features[0] with { Review = CaptureReview.Rejected },
                features[0] with { Evidence = CaptureEvidence.Inferred }, features[0] with { Evidence = CaptureEvidence.Authored }, features[0] with { Occluded = true } })
            {
                var unresolved = await Run(capture with { Features = [feature, ..features.Skip(1)] }, bindings);
                Check(unresolved.Status == "Incomplete" && !unresolved.Members[0].Matched, "ambiguous/unreviewed identity remains unresolved");
            }
            var partial = await Run(capture with { Coverage = CaptureCoverage.Partial, Unresolved = ["Uninspected scene contents"] }, bindings);
            Check(partial.Status == "Incomplete" && partial.Issues.Any(i => i.Code == "METADATA_CAPTURE_PARTIAL"), "inventory count cannot hide incomplete inspection");
            var extra = await Run(capture with { Features = [..features, features[0] with { Id = "extra-part" }] }, bindings);
            Check(extra.Status == "Incomplete" && extra.UnassignedFeatureIds.SequenceEqual(["extra-part"]), "extra structures/parts require catalog reconciliation");
            var detailJob = handoff.Jobs.First(j => j.Kind == "DetailMockup");
            var detailMembers = detailJob.NodeIds.ToHashSet();
            var detailBindings = bindings.Where(b => detailMembers.Contains(b.NodeId)).ToArray();
            var detailFeatureIds = detailBindings.Select(b => b.FeatureId).ToHashSet();
            var detailCapture = capture with { Features = features.Where(f => detailFeatureIds.Contains(f.Id)).ToArray() };
            var detail = await MockupCapturePipeline.ReconcileMetadataAsync(handoff, detailCapture, Spec(detailCapture, detailBindings, detailJob.Id), folder, generator);
            Check(detail.Status == "DeclaredInventoryMatched" && detail.Members.Count < report.Members.Count, "complete child scope does not claim whole overview");
            await Reject(() => MockupCapturePipeline.ReconcileMetadataAsync(handoff, capture,
                Spec(capture, bindings, handoff.Jobs.First(j => j.Kind == "ReferenceConcept").Id), folder, generator), "METADATA_CAPTURE_SCOPE_JOB_REQUIRED");
            await Reject(() => MockupCapturePipeline.ReconcileMetadataAsync(handoff, capture with { Source = capture.Source with { Revision = "changed" } },
                Spec(capture, bindings), folder, generator), "METADATA_CAPTURE_SPECIFICATION_INVALID_OR_STALE");
            var forged = handoff with { Status = "Approved" };
            await Reject(() => MockupCapturePipeline.ReconcileMetadataAsync(forged, capture,
                Spec(capture, bindings) with { HandoffSha256 = MockupCapturePipeline.MetadataHandoffDigest(forged) }, folder, generator), "METADATA_ART_HANDOFF_REPLAY_MISMATCH");
            var outside = capture with { Source = capture.Source with { File = "../source.png" } };
            await Reject(() => Run(outside, bindings), "CAPTURE_SOURCE_PATH_INVALID");
            if (name == "interior")
            {
                var nested = pipeline.PlanMetadata(world, example.Recipe, catalog, art with
                { Bindings = art.Bindings.Select(b => b.ArchetypeId == "floor" ? b with { Role = MetadataArtRole.Structure } : b).ToArray() }, generator);
                var nestedBindings = nested.Members.Where(m => m.Role != MetadataArtRole.Group)
                    .Select((m, i) => new MetadataCaptureBinding(m.NodeId, "nested-" + i)).ToArray();
                var nestedIds = nestedBindings.ToDictionary(b => b.NodeId, b => b.FeatureId);
                var nestedCapture = capture with { Features = nestedBindings.Select(b => features[0] with
                { Id = b.FeatureId, ParentId = nestedIds.GetValueOrDefault(world.Nodes.Single(n => n.Id == b.NodeId).ParentId!) }).ToArray() };
                MetadataCaptureSpecification NestedSpec(MockupCaptureDocument c) => new(1,
                    MockupCapturePipeline.MetadataHandoffDigest(nested), MockupCapturePipeline.CaptureDigest(c), "scope:overview", nestedBindings);
                var nestedReport = await MockupCapturePipeline.ReconcileMetadataAsync(nested, nestedCapture, NestedSpec(nestedCapture), folder, generator);
                Check(nestedReport.Status == "DeclaredInventoryMatched" && nestedReport.Members.Count == 14, "nested visual parent and all parts matched");
                var child = nestedCapture.Features.First(f => f.ParentId is not null);
                var wrongParent = nestedCapture.Features.First(f => f.ParentId is null && f.Id != child.ParentId).Id;
                var wrong = nestedCapture with { Features = nestedCapture.Features.Select(f => f.Id == child.Id ? f with { ParentId = wrongParent } : f).ToArray() };
                var wrongReport = await MockupCapturePipeline.ReconcileMetadataAsync(nested, wrong, NestedSpec(wrong), folder, generator);
                Check(wrongReport.Status == "Incomplete" && wrongReport.Issues.Any(i => i.Code == "METADATA_CAPTURE_PARENT_MISMATCH"), "part assigned to wrong visual parent refused");
                var detached = nestedCapture with { Features = nestedCapture.Features.Select(f => f with { ParentId = null }).ToArray() };
                var detachedReport = await MockupCapturePipeline.ReconcileMetadataAsync(nested, detached, NestedSpec(detached), folder, generator);
                Check(detachedReport.Status == "Incomplete" && detachedReport.Members.Count(m => !m.Matched) == 12, "flattening nested parts refused");
                await File.WriteAllTextAsync(Path.Combine(folder, "nested-handoff.json"), Json(nested));
                await File.WriteAllTextAsync(Path.Combine(folder, "nested-report.json"), Json(nestedReport));
                await File.WriteAllTextAsync(Path.Combine(folder, "wrong-parent-report.json"), Json(wrongReport));
            }
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            bool cancelObserved = false;
            try { await MockupCapturePipeline.ReconcileMetadataAsync(handoff, capture, Spec(capture, bindings), folder, generator, cancelled.Token); }
            catch (OperationCanceledException) { cancelObserved = true; }
            Check(cancelObserved, "cancellation preserved");
            var original = await File.ReadAllTextAsync(Path.Combine(folder, "report.json")); bool exists = false;
            try { await pipeline.ExportMetadataCaptureAsync(handoff, capture, Spec(capture, bindings), folder, Path.Combine(folder, "report.json"), generator); }
            catch (IOException) { exists = true; }
            Check(exists && original == await File.ReadAllTextAsync(Path.Combine(folder, "report.json")), "existing report preserved");
            await File.WriteAllBytesAsync(Path.Combine(folder, "tampered.png"), [..bytes, 1]);
            var tampered = capture with { Source = capture.Source with { File = "tampered.png" } };
            await Reject(() => Run(tampered, bindings), "CAPTURE_SOURCE_HASH_MISMATCH");
            await File.WriteAllTextAsync(Path.Combine(folder, "stale-bindings.json"), Json(Spec(capture, bindings) with { CaptureSha256 = new('0', 64) }));
            Console.WriteLine($"PASS: {name}; {report.Members.Count} required instances, nested parents, detail/overview scope, omissions, extra parts, duplicate/reused identities, review/coverage gates, stale/forged/path/hash refusals, cancellation and preserved exports.");
        }
        Console.WriteLine("Synthetic annotation tests only: actual vision, ground geometry, art approval and Godot acceptance NOT RUN.");
    }

    static async Task Reject(Func<Task<MetadataCaptureReconciliation>> action, string code)
    {
        try { await action(); }
        catch (ArgumentException exception) when (exception.Message.StartsWith(code, StringComparison.Ordinal)) { return; }
        throw new Exception("Expected " + code);
    }
}
