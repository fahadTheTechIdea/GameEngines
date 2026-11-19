using WorldCreator.Catalogs.Modern;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Worlds;
using WorldCreator.Godot;
if(args.Length==2&&args[0]=="--world-population-gate")
{
    try{await WorldPopulationGateTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-component-metadata")
{
    try{await WorldComponentMetadataTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-connection-gate")
{
    try{await WorldConnectionGateTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-adaptive-layout-gate")
{
    try{await WorldAdaptiveLayoutGateTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-placement-memo")
{
    try{await WorldPlacementMemoTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==1&&args[0]=="--world-route-height-sources")
{
    try{WorldRouteJunctionTests.SourceHeights();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-route-memo")
{
    try{await WorldRouteMemoTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-coastal-mixed")
{
    try{await WorldCoastalMixedWorldTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-route-junctions")
{
    try{await WorldRouteJunctionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-progression-selector-domains")
{
    try{await WorldProgressionSelectorTests.DomainsAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-progression-selectors")
{
    try{await WorldProgressionSelectorTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-study-completion")
{
    try{await WorldStudyCompletionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==3&&args[0]=="--world-cli-completion")
{
    try{await WorldCliCompletionTests.RunAsync(args[1],args[2]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-interior-chunks")
{
    try{await WorldInteriorChunkTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-blueprint-session")
{
    try{await WorldRegionalBlueprintSessionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design-pinned-seed")
{
    try{await WorldRegionalDesignProtectionTests.PinnedSeedAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design-protections")
{
    try{await WorldRegionalDesignProtectionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design-session")
{
    try{await WorldRegionalDesignSessionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-captured-design-replay")
{
    try{await WorldNoncityTerrainMatrixTests.CapturedDesignReplayAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-captured-terrain-fine")
{
    try{await WorldNoncityTerrainMatrixTests.FineCapturedTerrainAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-captured-terrain-regions")
{
    try{await WorldNoncityTerrainMatrixTests.CapturedTerrainAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-supplied-heightfield")
{
    try{await WorldNoncityTerrainMatrixTests.SuppliedHeightfieldAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-generated-terrain-joint")
{
    try{await WorldNoncityTerrainMatrixTests.GeneratedWorldAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==1&&args[0]=="--world-noncity-terrain-preflight")
{
    try{WorldNoncityTerrainMatrixTests.Preflight();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-noncity-terrain-matrix")
{
    try{await WorldNoncityTerrainMatrixTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-partition-preference-edit")
{
    try{await WorldPartitionPreferenceEditTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-partition-preferences")
{
    try{await WorldPartitionPreferenceTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-preferred-region-anchor")
{
    try{await WorldPreferredRegionAnchorTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-blueprint-option-sets")
{
    try{await WorldRegionalBlueprintOptionSetTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-blueprint-design")
{
    try{await WorldRegionalBlueprintDesignTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-seeded-blueprint")
{
    try{await WorldRegionalSeededBlueprintTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-offset-physical-study")
{
    try{await WorldOffsetPhysicalStudy.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-offset-resolution")
{
    try{await WorldRegionPartitionTests.BentResolutionAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-offset-seed")
{
    try{await WorldRegionalDesignWorldSeedTests.BendPositionSeedAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-blueprint")
{
    try{await WorldRegionalBlueprintTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-terrain-adaptation")
{
    try{await WorldTerrainAdaptationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-terrain-comparison")
{
    try{await WorldTerrainComparisonTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==1&&args[0]=="--world-sampled-overlay-parity")
{
    try{WorldSampledOverlayTests.Run();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design-replay")
{
    try{await WorldRegionalDesignReplayTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-bent-terrain-edit")
{
    try{await WorldRegionPartitionTests.BentTerrainAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-bent-world-seed")
{
    try{await WorldRegionalDesignWorldSeedTests.BendSeedAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-bent-feedback")
{
    try{await WorldRegionPartitionTests.BentFeedbackAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-bent-regional-world")
{
    try{await WorldRegionPartitionTests.BentRegionalAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-bent-regions")
{
    try{await WorldRegionPartitionTests.BentAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-seed-corpus")
{
    try{await WorldRegionalSeedCorpusTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design-world-seed")
{
    try{await WorldRegionalDesignWorldSeedTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design-variety")
{
    try{await WorldRegionalDesignVarietyTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-joint-semantic")
{
    try{await WorldRegionalJointTests.SemanticAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-semantic-choice")
{
    try{await WorldRegionalProgramChoiceTests.SemanticChoiceAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design-bindings")
{
    try{await WorldRegionalDesignBindingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-multi-structure")
{
    try{await WorldRegionalMultiStructureTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-scoped-requirements")
{
    try{await WorldRegionalScopedRequirementsTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-design")
{
    try{await WorldRegionalDesignTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-position-protection")
{
    try{await WorldPositionProtectionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-topology-intent")
{
    try{await WorldTopologyIntentTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-capacity-provenance" or "--world-capacity-provenance-prior")
{
    try{await WorldCapacityProvenanceTests.RunAsync(args[1],args[0]=="--world-capacity-provenance-prior");}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-terrain-program-choice")
{
    try{await WorldTerrainProgramChoiceTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-terrain-capacity")
{
    try{await WorldTerrainCapacityTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-mixed-capacity")
{
    try{await WorldRegionalMixedCapacityTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-mixed-variant-capacity")
{
    try{await WorldMixedVariantCapacityTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-protected-terrain-integration")
{
    try{await WorldProtectedTerrainIntegrationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-clamp-matrix") { WorldRegionClampMatrixTests.Run(args[1]); return; }
if(args.Length==2&&args[0]=="--world-seeded-regional-world") { try{await WorldSeededRegionalWorldTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2&&args[0]=="--world-regional-weights") { try{await WorldRegionalWorldWeightTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2&&args[0]=="--world-seeded-route-probe") { try{await WorldSeededRegionalWorldTests.RouteProbeAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2&&args[0]=="--world-directional-regional-program") { await WorldDirectionalRegionalProgramTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-terrain-directional-detail") { await WorldTerrainDirectionalDetailTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-terrain-falloff") { try{await WorldTerrainFalloffTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2&&args[0]=="--world-global-requirements") { await WorldGlobalRequirementsTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-regional-progression") { await WorldRegionalProgressionTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-regional-progression-selection") { await WorldRegionalProgressionSelectionTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-regional-journey") { await WorldRegionalJourneyTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-regional-corridor-journey") { await WorldRegionalCorridorJourneyTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-regional-interior-journey") { await WorldRegionalInteriorJourneyTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-terrain-roughness-world") { await WorldTerrainRoughnessWorldTests.RunAsync(args[1]); return; }
if(args.Length==2&&args[0]=="--world-terrain-roughness") { await WorldTerrainRoughnessTests.RunAsync(args[1]); return; }
if(args.Length==1&&args[0]=="--world-terrain-protected-detail")
{
    try{WorldTerrainTests.ProtectedDetail();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-source-ground-lifecycle")
{
    try{await WorldSourceGroundAllocationTests.RunLifecycleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-source-ground-choices")
{
    try{await WorldSourceGroundAllocationTests.RunChoicesAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-source-ground-allocation" or "--world-source-ground-control")
{
    try{await WorldSourceGroundAllocationTests.RunAsync(args[1],args[0]=="--world-source-ground-control");}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-hierarchical-ground-capacity" or "--world-hierarchical-ground-control")
{
    try{await WorldHierarchicalGroundCapacityTests.RunAsync(args[1],args[0]=="--world-hierarchical-ground-control");}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-published-source-read")
{
    try{await WorldPublishedSourceTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-published-source-probe")
{
    try{await WorldPublishedSourceTests.ProbeAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-irregular-surroundings" or "--world-irregular-surroundings-control")
{
    try{await WorldIrregularSurroundingsTests.RunAsync(args[1],args[0]=="--world-irregular-surroundings-control");}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-terrain-policy-contract")
{
    try{await WorldTerrainPolicyContractTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-regional-assembly-scale" or "--world-regional-assembly-control")
{
    try{await WorldRegionalAssemblyScaleTests.RunAsync(args[1],args[0]=="--world-regional-assembly-control");}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-local-constraint-search" or "--world-local-constraint-control")
{
    try{await WorldLocalConstraintSearchTests.RunAsync(args[1],args[0]=="--world-local-constraint-control");}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-region-relation-search" or "--world-region-relation-control")
{
    try{await WorldRegionPartitionTests.RelationsAsync(args[1],args[0]=="--world-region-relation-control");}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-partition-pins")
{
    try{await WorldRegionPartitionTests.PinsAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-overlay-scale")
{
    try{await WorldRegionalOverlayInvalidationTests.ScaleOnlyAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-overlay-invalidation")
{
    try{await WorldRegionalOverlayInvalidationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry detail in error.Data)Console.Error.WriteLine(detail.Key+": "+JsonSerializer.Serialize(detail.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-protection-changes")
{
    try{await WorldRegionalProtectionChangeTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry detail in error.Data)Console.Error.WriteLine(detail.Key+": "+JsonSerializer.Serialize(detail.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-demand-plan")
{
    try{await WorldRegionalDemandPlanTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry detail in error.Data)Console.Error.WriteLine(detail.Key+": "+JsonSerializer.Serialize(detail.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-pose-controls")
{
    try{await WorldPoseControlTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry detail in error.Data)Console.Error.WriteLine(detail.Key+": "+JsonSerializer.Serialize(detail.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-connected-capacity")
{
    try{await WorldConnectedCapacityTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry detail in error.Data)Console.Error.WriteLine(detail.Key+": "+JsonSerializer.Serialize(detail.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-choice-enumeration")
{
    try{await WorldRegionalRedesignTests.EnumerationAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-redesign-joint")
{
    try{await WorldRegionalRedesignTests.JointAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length is 2 or 3&&args[0]=="--world-regional-redesign")
{
    try{await WorldRegionalRedesignTests.RunAsync(args[^1],args.Length==3?args[1]:null);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-partitions")
{
    try{await WorldRegionalPartitionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--write-regional-partition-example")
{
    try{await WorldRegionalPartitionTests.WriteAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-regional-controls" or "--world-region-pins")
{
    try{if(args[0]=="--world-region-pins")await WorldRegionalControlTests.PinsAsync(args[1]);else await WorldRegionalControlTests.RunAsync(args[1]);}
    catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==3&&args[0]=="--world-global-component-case")
{
    try{await WorldGlobalComponentTests.RunAsync(args[2],args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-global-components")
{
    try{await WorldGlobalComponentTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-alternate-spans")
{
    try{await WorldRegionPartitionTests.AlternateSpansAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-interface-route")
{
    try{await WorldRegionPartitionTests.RegionInterfaceRouteAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-shared-span")
{
    try{await WorldRegionPartitionTests.SharedSpanAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==1&&args[0]=="--world-topology-return-oracle") { WorldTopologyReturnOracleTests.Run(); return; }
if(args.Length==1&&args[0]=="--world-topology-alternative-oracle")
{
    try{WorldTopologyAlternativeOracleTests.Run();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-stage-loops")
{
    try{await WorldTopologyStageLoopTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-contour-facing")
{
    try{await WorldContourFacingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-facing")
{
    try{await WorldRegionFacingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==1&&args[0]=="--world-regional-network-preflight")
{
    try{WorldRegionalNetworkTests.Preflight();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-semantic-terrain")
{
    try{await WorldRegionalNetworkTests.SemanticTerrainAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-semantic-endpoints")
{
    try{await WorldRegionalNetworkTests.SemanticAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-network-composed")
{
    try{await WorldRegionalNetworkTests.ComposedAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length is 1 or 2&&args[0]=="--world-route-cost-bounds")
{
    try{WorldRouteCostBoundTests.Run(args.Length==2?args[1]:null);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-regional-networks" or "--world-regional-network-smoke")
{
    try{await WorldRegionalNetworkTests.RunAsync(args[1],args[0]=="--world-regional-network-smoke");}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-topology")
{
    try{await WorldRegionalTopologyTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==1&&args[0]=="--world-nested-source-geometry")
{
    try{WorldNestedSourceTests.BoundaryGeometry();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==3&&args[0]=="--world-nested-source-case")
{
    try{await WorldNestedSourceTests.RunAsync(args[2],only:args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-nested-source-lifecycle")
{
    try{await WorldNestedSourceTests.ControlsAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0] is "--world-nested-sources" or "--world-nested-source-smoke")
{
    try{await WorldNestedSourceTests.RunAsync(args[1],args[0]=="--world-nested-sources");}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-source-programs")
{
    try{await WorldSourceProgramTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-nested-ownership-lifecycle")
{
    try{await WorldNestedOwnershipTests.RunLifecycleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-nested-ownership")
{
    try{await WorldNestedOwnershipTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-spatial-program-analysis")
{
    try{await WorldSpatialProgramAnalysisTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-joint-lifecycle")
{
    try{await WorldRegionalJointTests.RunLifecycleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-joint-terrain")
{
    try{await WorldRegionalJointTests.RunTerrainAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-joint")
{
    try{await WorldRegionalJointTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-source-history")
{
    try{await WorldRegionalProgramChoiceTests.RunSourceHistoryAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-program-history")
{
    try{await WorldRegionalProgramChoiceTests.RunHistoryAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-program-choices")
{
    try{await WorldRegionalProgramChoiceTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-physical-feedback")
{
    try{await WorldRegionalPhysicalFeedbackTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-rounding-network")
{
    try{await WorldRegionalRoundingNetworkTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-rounding")
{
    try{await WorldRegionalRoundingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-coupling")
{
    try{await WorldRegionalCouplingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-terrain-program-analysis")
{
    try{await WorldTerrainProgramAnalysisTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-ownership-density")
{
    try{await WorldOwnershipDensityTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-portable-population")
{
    try{await WorldPortablePopulationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-source-domains")
{
    try{await WorldSourceDomainTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-ground-cover")
{
    try{await WorldGroundCoverTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==1&&args[0]=="--world-program-analysis")
{
    try{WorldProgramAnalysisTests.Run();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-program-demand")
{
    try{await WorldRegionalOwnershipTests.DemandAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-ownership-units")
{
    try{await WorldRegionalOwnershipTests.UnitsAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-ownership-scale")
{
    try{await WorldRegionalOwnershipTests.ScaleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-regional-ownership")
{
    try{await WorldRegionalOwnershipTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-demand")
{
    try{await WorldRegionDemandTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-seeded-partitions")
{
    try{await WorldRegionPartitionTests.SeededAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-allocation-weights")
{
    try{await WorldRegionPartitionTests.WeightsAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-local-allocation-weights")
{
    try{await WorldProgramSpaceTests.WeightsAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-weighted-layout-adaptation")
{
    try{await WorldWeightedLayoutAdaptationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-ring-terrain")
{
    try{await WorldRingTerrainAdaptationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-spiral-layout")
{
    try{await WorldSpiralLayoutTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-spiral-facing")
{
    try{await WorldSpiralFacingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-spiral-terrain")
{
    try{await WorldSpiralTerrainTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==3&&args[0]=="--world-population-cli")
{
    try{await WorldPopulationCliTests.RunAsync(args[1],args[2]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-population-multiscope")
{
    try{await WorldPopulationMultiScopeTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-population-rotation")
{
    try{await WorldPopulationRotationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-population-rigid")
{
    try{await WorldPopulationRigidTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;
}
if(args.Length==2&&args[0]=="--world-region-partition")
{
    try{await WorldRegionPartitionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-regional-session-validation")
{
    try{await WorldRegionalSessionTests.ValidationAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==3&&args[0]=="--world-regional-session-scale")
{
    try{await WorldRegionalSessionTests.ScaleAsync(args[1],args[2]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-regional-session")
{
    try{await WorldRegionalSessionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-regional-content-scale")
{
    try{await WorldRegionalContentTests.ReadAndScaleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-regional-content")
{
    try{await WorldRegionalContentTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-metadata-terrain-capture")
{
    try{await WorldMetadataTerrainCaptureTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-regional-terrain-windows")
{
    try{await WorldRegionalTerrainWindowTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-terrain-windows")
{
    try{await WorldTerrainWindowTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-fence-assembly")
{
    try{await WorldFenceAssemblyTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);for(Exception? cause=error;cause is not null;cause=cause.InnerException)foreach(System.Collections.DictionaryEntry entry in cause.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-generic-surroundings")
{
    try{await WorldGenericSurroundingsTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-direct-inputs")
{
    try{await WorldDirectInputTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-digest-resources")
{
    try{await WorldDigestResourceTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==3&&args[0]=="--world-composed-resources")
{
    try{await WorldComposedResourceTests.RunAsync(double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture),args[2]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-morphology")
{
    try{await WorldMorphologyTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-semantic-openings")
{
    try{await WorldSemanticOpeningTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-broad-composition")
{
    try{await WorldBroadCompositionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-mixed-growth")
{
    try{await WorldMixedGrowthTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-parent-routes")
{
    try{await WorldParentRouteTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-interior-population")
{
    try{await WorldInteriorPopulationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+JsonSerializer.Serialize(entry.Value));Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-domain-session-operations")
{
    try{await WorldDomainSessionOperationsTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-population-scale-lifecycle")
{
    try{await WorldPopulationScaleTests.LifecycleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-placement-index")
{
    try{await WorldPlacementEnvelopeIndexTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-population-scale")
{
    try{await WorldPopulationScaleTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry d in error.Data)Console.Error.WriteLine(d.Key+": "+d.Value);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-session-operations")
{
    try{await WorldSessionOperationsTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-population-facing")
{
    try{await WorldPopulationFacingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-generated-boundaries")
{
    try{await WorldGeneratedBoundaryTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-generation-study")
{
    try{await WorldGenerationStudyTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-root-region-coordination")
{
    try{await WorldRootRegionCoordinationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length is 2 or 3&&args[0]=="--world-contract-audit")
{
    try{await WorldContractAuditTests.RunAsync(args[1],args.Length==3?args[2]:null);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-sampled-regions")
{
    try{await WorldSampledRegionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-network-regions")
{
    try{await WorldNetworkRegionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-network-memo")
{
    try{await WorldNetworkMemoTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-network-policies")
{
    try{await WorldNetworkPolicyTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+entry.Value);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-footprint-predicates")
{
    try{WorldFootprintPredicateTests.Run();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-mixed-route-regions")
{
    try{await WorldMixedRouteRegionTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+entry.Value);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-route-guidance")
{
    try{await WorldRouteGuidanceTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-parameter-bindings")
{
    try{await WorldParameterBindingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-world-interior-floor-example")
{
    try{await WorldInteriorFloorRequirementTests.WriteExampleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-interior-floor-requirements")
{
    try{await WorldInteriorFloorRequirementTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-physical-accounting")
{
    try{await WorldPhysicalAccountingTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-ground-use-capacity")
{
    try{await WorldGroundUseCapacityTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-placement-resume")
{
    try{WorldPlacementResumeTests.Run();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-nested-source-fit")
{
    try{await WorldNestedSourceFitTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-provisional-relations")
{
    try{await WorldProvisionalRelationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-relation-candidates")
{
    try{await WorldRelationCandidateTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-complete-source-coordination")
{
    try{await WorldCompleteSourceCoordinationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-nested-coordination")
{
    try{await WorldNestedCoordinationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-source-pair-coordination")
{
    try{await WorldSourcePairCoordinationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-population-coordination")
{
    try{await WorldPopulationCoordinationTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-source-contacts")
{
    try{await WorldSourceContactTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-partitioned-domains")
{
    try{await WorldPartitionedStorageTests.DomainsAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--world-partitioned-dense")
{
    try{await WorldPartitionedStorageTests.DenseAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==4&&args[0]=="--world-partitioned-history")
{
    try{await WorldDenseRegionTests.MetadataAsync(args[1],args[2],args[3],true);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-partitioned-storage")
{
    try{await WorldPartitionedStorageTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0] is "--world-region-feedback" or "--world-region-feedback-ranked")
{
    try{if(args[0].EndsWith("ranked",StringComparison.Ordinal))WorldRegionFeedbackTests.Ranked();else await WorldRegionFeedbackTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry e in error.Data)Console.Error.WriteLine($"{e.Key}: {e.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-region-feedback-examples"){await WorldRegionFeedbackTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-region-intents")
{
    try{await WorldRegionIntentTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry e in error.Data)Console.Error.WriteLine($"{e.Key}: {e.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-region-intent-examples"){await WorldRegionIntentTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-region-intents-geometry")
{
    try{WorldRegionIntentTests.Geometry();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-program-composition-examples"){await WorldProgramCompositionTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0] is "--world-program-composition" or "--world-program-composition-ranked")
{
    try{if(args[0].EndsWith("ranked",StringComparison.Ordinal))WorldProgramCompositionTests.Ranked();else await WorldProgramCompositionTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry e in error.Data)Console.Error.WriteLine($"{e.Key}: {e.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-program-mix-examples"){await WorldProgramMixTests.WriteExamples(args[1]);return;}
if(args.Length==1&&args[0]=="--world-program-mix")
{
    try{await WorldProgramMixTests.Physical();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry e in error.Data)Console.Error.WriteLine($"{e.Key}: {e.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-program-mix-solver")
{
    try{WorldProgramMixTests.Solver();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0] is "--world-area-population" or "--world-area-geometry")
{
    try{if(args[0]=="--world-area-geometry")WorldAreaPopulationTests.Geometry();else await WorldAreaPopulationTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine($"{entry.Key}: {entry.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-area-population-examples"){await WorldAreaPopulationTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-prescribed-program")
{
    try{WorldPrescribedPopulationTests.NestedProgram();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-prescribed-population")
{
    try{await WorldPrescribedPopulationTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine($"{entry.Key}: {entry.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-prescribed-population-examples"){await WorldPrescribedPopulationTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-contour-population")
{
    try{await WorldContourPopulationTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine($"{entry.Key}: {entry.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-contour-population-examples"){await WorldContourPopulationTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-region-composition")
{
    try{await WorldRegionCompositionTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine($"{entry.Key}: {entry.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-region-composition-examples"){await WorldRegionCompositionTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-entrance-travel")
{
    try{await WorldEntranceTravelTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine($"{entry.Key}: {entry.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-entrance-travel-examples"){await WorldEntranceTravelTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-generated-interiors")
{
    try{await WorldGeneratedInteriorTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine($"{entry.Key}: {entry.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-generated-interior-examples"){await WorldGeneratedInteriorTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-mixed-scale")
{
    try{await WorldMixedScaleTests.RunAsync();}
    catch(Exception error){Console.Error.WriteLine(error is ArgumentException&&error.Message.Contains("BUDGET")?error.Message:error.ToString());foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine($"{entry.Key}: {entry.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-mixed-scale-examples") {await WorldMixedScaleTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-route-budget")
{
    try{await WorldRouteBudgetTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-programs")
{
    try{await WorldProgramInstantiationTests.RunAsync();}
    catch(Exception error){Console.Error.WriteLine(error);foreach(System.Collections.DictionaryEntry item in error.Data)Console.Error.WriteLine($"{item.Key}: {item.Value}");Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-program-examples") {await WorldProgramInstantiationTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--operation-store-sharing")
{
    try{await OperationStoreSharingTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-connected-growth")
{
    try{await WorldConnectedGrowthTests.RunAsync();}
    catch(Exception error)
    {
        Console.Error.WriteLine(error);
        foreach(System.Collections.DictionaryEntry detail in error.Data)Console.Error.WriteLine($"{detail.Key}: {detail.Value}");
        Environment.ExitCode=1;
    }
    return;
}
if(args.Length==2&&args[0]=="--write-connected-growth-examples") {await WorldConnectedGrowthTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==1&&args[0]=="--world-topology")
{
    try{await WorldTopologyTests.RunAsync();}
    catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1&&args[0]=="--world-topology-capacity")
{
    try{await WorldTopologyCapacityTests.RunAsync();}
    catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==2&&args[0]=="--write-topology-examples") {await WorldTopologyTests.WriteExamplesAsync(args[1]);return;}
if(args.Length==2&&args[0]=="--metadata-art-handoff") {await MetadataArtHandoffTests.RunAsync(args[1]);return;}
if(args.Length==2&&args[0]=="--metadata-capture") {await MetadataCaptureTests.RunAsync(args[1]);return;}
if(args.Length==2&&args[0]=="--metadata-ground") {await MetadataGroundTests.RunAsync(args[1]);return;}
if(args.Length==3&&args[0]=="--metadata-ground-views") {await MetadataGroundViewTests.RunAsync(args[1],args[2]);return;}
if(args.Length==2&&args[0]=="--metadata-scene") {await MetadataSceneTests.RunAsync(args[1]);return;}
if(args.Length==2&&args[0]=="--metadata-production") {await MetadataProductionTests.RunAsync(args[1]);return;}
if(args.Length==3&&args[0]=="--tile-topology") {await WorldTileTopologyTests.RunAsync(args[1],args[2]);return;}
if(args.Length==5&&args[0]=="--native-static-world") {await NativeStaticWorldTests.RunAsync(args[1],args[2],args[3],args[4]);return;}
if(args.Length==5&&args[0]=="--native-movement") {await NativeStaticWorldTests.RunAsync(args[1],args[2],args[3],args[4],true);return;}
if(args.Length==6&&args[0]=="--native-components") {await NativeStaticWorldTests.RunAsync(args[1],args[2],args[3],args[5],movement:true,componentDirectory:args[4]);return;}
if(args.Length==5&&args[0]=="--gamearts-lifecycle") {await GameArtsLifecycleRegistryTests.RunAsync(args[1],args[2],args[3],args[4]);return;}
if(args.Length==5&&args[0]=="--gamearts-lifecycle-process") {await GameArtsLifecycleProcessTests.RunAsync(args[1],args[2],args[3],args[4]);return;}
if(args.Length==5&&args[0]=="--canonical-world-cli") {await CanonicalWorldCommandTests.RunAsync(args[1],args[2],args[3],args[4]);return;}
if(args.Length==4&&args[0]=="--canonical-world-cli-negative") {await CanonicalWorldCommandTests.NegativeAsync(args[1],args[2],args[3]);return;}
if(args.Length==1 && args[0]=="--world-protection") { await WorldProtectionTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--world-chunk-package") { try{await WorldChunkPackageTests.RunAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length is 1 or 2 && args[0]=="--world-chunks") { try{await WorldChunkTests.RunAsync(args.Length==2?args[1]:null);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--world-mixed-program") { await WorldMixedProgramTests.RunAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-scale") { await WorldScaleTests.RunAsync(args.Length==2?args[1]:null); return; }
if(args.Length==1 && args[0]=="--world-variation") { await WorldVariationTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-variation-example") { await WorldVariationTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-protection-example") { await WorldProtectionTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-region-population") { await WorldRegionPopulationTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-region-population-example") { await WorldRegionPopulationTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-network-population") { await WorldNetworkPopulationTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-network-population-example") { await WorldNetworkPopulationTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-route-population") { try{await WorldRoutePopulationTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-route-population-example") { await WorldRoutePopulationTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-population") { await WorldPopulationTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-population-example") { await WorldPopulationTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-dependencies") { await WorldDependencyTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-dependency-example") { await WorldDependencyTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-regeneration") { await WorldRegenerationTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-regeneration-quick") { await WorldRegenerationTests.QuickAsync(); return; }
if(args.Length==2 && args[0]=="--write-regeneration-example") { await WorldRegenerationTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-landform-search") { WorldLandformRouteTests.DirectCases(); return; }
if(args.Length==1 && args[0]=="--world-landform-routes") { await WorldLandformRouteTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-landform-routes") { await WorldLandformRouteTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-interior") { await WorldInteriorTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-interior-example") { await WorldInteriorTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-physical-routing-geometry") {try{WorldPhysicalRoutingTests.Geometry();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;}
if(args.Length==1 && args[0]=="--world-physical-routing") {try{await WorldPhysicalRoutingTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}return;}
if(args.Length==2 && args[0]=="--write-physical-routing-example") {await WorldPhysicalRoutingTests.WriteExampleAsync(args[1]);return;}
if(args.Length==1 && args[0]=="--world-terrain-demand-measure") { try{await WorldTerrainDemandTests.MeasureAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-terrain-demand") { try{await WorldTerrainDemandTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);if(error.Data["PlacementFailures"] is {} failures)Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(failures,WorldJson.CreateOptions()));Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-terrain-demand-examples") {await WorldTerrainDemandTests.WriteExamplesAsync(args[1]);return;}
if(args.Length is 1 or 2 && args[0]=="--world-capability-progression") { try{await WorldCapabilityProgressionTests.RunAsync(args.Length==2?args[1]:null);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-capability-progression-example") { await WorldCapabilityProgressionTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-progression") { try{await WorldProgressionTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-progression-example") { await WorldProgressionTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-terrain-compounds") { await WorldTerrainCompoundTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-terrain-compounds") { await WorldTerrainCompoundTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-compounds") { await WorldCompoundIntegrationTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-compound-example") { await WorldCompoundIntegrationTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-adaptation") { await WorldLayoutAdaptationTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-layout-rotation") { await WorldLayoutAdaptationTests.RotationAsync(); return; }
if(args.Length==1 && args[0]=="--world-regional-frames") { await WorldRegionalFrameTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-layout-strategies") { await WorldLayoutStrategyTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-region-relations-oracle") { WorldRegionRelationTests.Oracle(); return; }
if(args.Length==1 && args[0]=="--world-joint-layouts") { await WorldJointLayoutTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-domains") { await WorldDomainTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-domain-journeys") { await WorldDomainJourneyTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--world-regional-domain-world") { await WorldRegionalDomainWorldTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-regional-marker-domains") { await WorldRegionalMarkerDomainTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-regional-domain-design") { await WorldRegionalDomainDesignTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-regional-physical-domains") { await WorldRegionalPhysicalDomainsTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-domain-progression") { await WorldDomainProgressionTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-domain-journey-capabilities") { await WorldDomainJourneyCapabilitiesTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-domain-chunk-index") { await WorldDomainChunkIndexTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-regional-domain-chunk-index") { await WorldDomainChunkIndexTests.RunRegionalAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-domain-transfers") { await WorldDomainTransferTests.RunAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-numeric-buffer-edits")
{
    try{await WorldDenseRegionTests.BuffersAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==4 && args[0]=="--world-dense-metadata")
{
    try{await WorldDenseRegionTests.MetadataAsync(args[1],args[2],args[3]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1 && args[0]=="--world-dense-lake")
{
    try{WorldDenseRegionTests.Lake();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1 && args[0] is "--world-dense-regions" or "--world-dense-regions-probe")
{
    try{if(args[0]=="--world-dense-regions")WorldDenseRegionTests.Run();else WorldDenseRegionTests.Probe();}
    catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
    return;
}
if(args.Length==1 && args[0]=="--world-network-domain-endpoints") { WorldMixedDomainTests.NetworkEndpoints(); return; }
if(args.Length==1 && args[0]=="--world-mixed-domains") { await WorldMixedDomainTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-mixed-domain-example") { await WorldMixedDomainTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-domain-session") { await WorldDomainSessionTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-domain-journey-example") { await WorldDomainJourneyTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-world-domains-example") { await WorldDomainTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-joint-layout-examples") { await WorldJointLayoutTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-region-relations") { await WorldRegionRelationTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-region-relations-example") { await WorldRegionRelationTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-layout-strategy-examples") { await WorldLayoutStrategyTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-regional-frame-examples") { await WorldRegionalFrameTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-layout-rotation-example") { await WorldLayoutAdaptationTests.WriteRotationExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-adaptation-example") { await WorldLayoutAdaptationTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-interfaces") { await WorldInterfaceTests.RunAsync(args.Length==2?args[1]:null); return; }
if(args.Length==2 && args[0]=="--write-interface-example") { await WorldInterfaceTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-local-layouts") { await WorldLocalLayoutTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-binary-partitions") { await WorldBinaryPartitionTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-anchor-partitions") { await WorldAnchorPartitionTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-program-spaces") { await WorldProgramSpaceTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-sampled-environment") { try{await WorldSampledEnvironmentTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-wind-metadata") { try{await WorldWindMetadataTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-wind-metadata-physical") { try{await WorldWindMetadataTests.PhysicalAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-world-wind-metadata-example") { await WorldWindMetadataTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-wind-physical") { try{await WorldSampledEnvironmentTests.PhysicalAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-world-wind-examples") { await WorldSampledEnvironmentTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-water-physical") { try{await WorldWaterMetadataTests.FullPhysicalAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-water-metadata") { try{await WorldWaterMetadataTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-world-water-metadata-example") { try{await WorldWaterMetadataTests.WriteExampleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-water") { try{await WorldWaterTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-retained-water") { try{await WorldRetainedWaterTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-retained-water-core-examples") { await WorldRetainedWaterTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-retained-water-metadata") { try{await WorldRetainedWaterMetadataTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-retained-water-physical") { try{await WorldRetainedWaterMetadataTests.PhysicalAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-retained-water-probe") { try{await WorldRetainedWaterMetadataTests.ProbeAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-world-retained-water-example") { await WorldRetainedWaterMetadataTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-world-water-example") { try{await WorldWaterTests.WriteExampleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-drainage-metadata") { try{await WorldDrainageMetadataTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-world-drainage-metadata-example") { try{await WorldDrainageMetadataTests.WriteExampleAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-drainage") { try{await WorldDrainageTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-world-drainage-examples") { try{await WorldDrainageTests.WriteExamplesAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-validation-replay") { try{await WorldValidationReplayTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--world-validation-profile") { try{await WorldValidationReplayTests.ProfileAsync(args[1]);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0]=="--world-connected-full") { try{await WorldConnectedAreaTests.FullAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-connected-area-examples") { await WorldConnectedAreaTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-connected-area") { try{await WorldConnectedAreaTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==1 && args[0] is "--world-interior-anchors" or "--world-steep-source") { try{await WorldTerrainPartitionTests.RunAsync(args[0]=="--world-steep-source");}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-interior-anchor-examples") { await WorldTerrainPartitionTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-layered-landforms") { try{await WorldLayeredLandformTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-layered-landform-examples") { await WorldLayeredLandformTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-area-adaptation") { try{await WorldAreaAdaptationTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-area-adaptation-examples") { await WorldAreaAdaptationTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-layered-integration") { try{await WorldLayeredIntegrationTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length==2 && args[0]=="--write-layered-integration-examples") { await WorldLayeredIntegrationTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-program-space-examples") { await WorldProgramSpaceTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-anchor-partition-example") { await WorldAnchorPartitionTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-partition-adaptation") { await WorldPartitionAdaptationTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--world-publication") { await MetadataPublicationTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-partition-adaptation-examples") { await WorldPartitionAdaptationTests.WriteExamplesAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-binary-partition-example") { await WorldBinaryPartitionTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-local-layout-example") { await WorldLocalLayoutTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-regions") { await WorldRegionTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-region-example") { await WorldRegionTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-corridor-capacity") { await WorldCorridorCapacityTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-corridor-resilience") { await WorldCorridorResilienceTests.RunAsync(); return; }
if(args.Length==1 && args[0]=="--world-physical-loops") { await WorldPhysicalLoopTests.RunAsync(); return; }
if(args.Length==2 && args[0] is "--world-diverse-programs" or "--world-diverse-interior" or "--world-diverse-adaptive" or "--world-diverse-footprints")
{
    try{await WorldDiverseProgramTests.ProbeAsync(args[1],args[0] is "--world-diverse-interior" or "--world-diverse-adaptive",args[0]=="--world-diverse-adaptive",args[0]=="--world-diverse-footprints");}
    catch(Exception error)
    {
        Console.Error.WriteLine(error);
        if(error.Data["PlacementFailures"] is {} failures)Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(failures,WorldJson.CreateOptions()));
        if(error.Data["MaximumWork"] is {} maximum)Console.Error.WriteLine($"MaximumWork={maximum}; UsedWork={error.Data["UsedWork"]}; RequestedWork={error.Data["RequestedWork"]}");
        Environment.ExitCode=1;
    }
    return;
}
if(args.Length==2 && args[0]=="--inspect-diverse-layout") {await WorldDiverseProgramTests.InspectAsync(args[1]);return;}
if(args.Length==1 && args[0]=="--world-footprint-anchors")
{try{await WorldFootprintAnchorTests.RunAsync();}catch(Exception error){Console.Error.WriteLine(error);if(error.Data["PlacementFailures"] is {} failures)Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(failures,WorldJson.CreateOptions()));Environment.ExitCode=1;}return;}
if(args.Length==2 && args[0]=="--write-corridor-capacity-example") { await WorldCorridorCapacityTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-route-reuse") { await WorldRouteReuseTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-route-reuse-example") { await WorldRouteReuseTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-corridors") { await WorldCorridorTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-corridor-example") { await WorldCorridorTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--current-contracts") { await CurrentContractTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length is 1 or 2 && args[0]=="--world-surface") { try{await WorldSurfaceTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length is 1 or 2 && args[0]=="--world-terrain") { await WorldTerrainTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==1 && args[0]=="--world-field-areas") { WorldFieldAreaTests.Run(); return; }
if(args.Length is 1 or 2 && args[0]=="--world-placement") { await WorldPlacementTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length is 1 or 2 && args[0]=="--world-hierarchy") { await WorldHierarchyPlacementTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length is 1 or 2 && args[0]=="--world-access") { try{await WorldPhysicalAccessTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length is 1 or 2 && args[0]=="--world-feedback") { try{await WorldPlacementFeedbackTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null);}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;} return; }
if(args.Length is 1 or 2 && args[0]=="--world-capacity") { await WorldCapacityTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length is 1 or 2 && args[0]=="--world-clearance") { await WorldClearanceTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length is 1 or 2 && args[0]=="--world-surroundings") { await WorldSurroundingsTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length is 1 or 2 && args[0]=="--world-resource-area") { await WorldResourceAreaTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-resource-area-example") { await WorldResourceAreaTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-context") { await WorldContextTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-wilderness-context-example") { await WorldContextTests.WriteExampleAsync(args[1],true); return; }
if(args.Length==2 && args[0]=="--write-context-example") { await WorldContextTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-supply-policy") { await WorldSupplyPolicyTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-supply-policy-example") { await WorldSupplyPolicyTests.WriteExampleAsync(Path.GetFullPath(args[1])); return; }
if(args.Length is 1 or 2 && args[0]=="--world-services") { await WorldServiceTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-service-example") { await WorldServiceTests.WriteExampleAsync(Path.GetFullPath(args[1])); return; }
if(args.Length==2 && args[0]=="--write-physical-service-example") { await WorldServiceTests.WriteExampleAsync(Path.GetFullPath(args[1]),true); return; }
if(args.Length==2 && args[0]=="--write-surroundings-example") { await WorldSurroundingsTests.WriteExampleAsync(Path.GetFullPath(args[1])); return; }
if(args.Length==2 && args[0]=="--write-clearance-example") { await WorldClearanceTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-capacity-example") { await WorldCapacityTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-feedback-example") { await WorldPlacementFeedbackTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-access-example") { await WorldPhysicalAccessTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-hierarchy-example") { await WorldHierarchyPlacementTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-placement-example") { await WorldPlacementTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-footprints") { await WorldFootprintTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-footprint-example") { await WorldFootprintTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-environment") { await WorldEnvironmentTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-environment-example") { await WorldEnvironmentTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-terrain-example") { await WorldTerrainTests.WriteExampleAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--world-routes") { await WorldRouteTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length is 1 or 2 && args[0]=="--world-networks") { await WorldNetworkTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-network-example") { await WorldNetworkTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-route-example") { await WorldRouteTests.WriteExampleAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--write-surface-example") { await WorldSurfaceTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--street-blocks") { await CityStreetBlockTests.RunAsync(); return; }
if(args.Length==2 && args[0]=="--write-street-block-fixture") { await CityStreetBlockTests.WriteFixtureAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--metadata-layers") { await MetadataLayerTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-layer-example") { await MetadataLayerTests.WriteExampleAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--world-intersection-performance") { WorldIntersectionPerformanceTests.Run(); return; }
if(args.Length==2 && args[0]=="--world-regional-return-topology") { await WorldRegionalTopologyTests.RunAsync(args[1],true,1); return; }
if(args.Length==2 && args[0]=="--world-regional-directed-topology") { await WorldRegionalTopologyTests.RunAsync(args[1],true); return; }
if(args.Length==2 && args[0]=="--world-directed-return-topology") { await WorldDirectedReturnTopologyTests.RunAsync(args[1]); return; }
if(args.Length==2 && args[0]=="--world-directed-topology") { await WorldDirectedTopologyTests.RunAsync(args[1]); return; }
if(args.Length==1 && args[0]=="--metadata-layouts") { MetadataLayoutTests.Run(); return; }
if(args.Length==2 && args[0]=="--write-layout-examples") { await MetadataLayoutTests.WriteExamplesAsync(args[1]); return; }
if(args.Length is 1 or 2 && args[0]=="--metadata") { MetadataLayoutTests.Run(); await MetadataLayerTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); await WorldMetadataTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if(args.Length==2 && args[0]=="--write-metadata-examples") { await WorldMetadataTests.WriteExamplesAsync(args[1]); return; }
if (args.Length is 1 or 2 && args[0]=="--structure-pipeline") { await StructureFirstPipelineTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if (args.Length==2 && args[0]=="--write-structure-godot-fixture") { await StructureFirstPipelineTests.RunAsync(godotOutput:Path.GetFullPath(args[1])); return; }
if (args.Length is 1 or 2 && args[0]=="--catalog-authoring") { await CatalogContentAuthoringTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if (args.Length==2 && args[0]=="--write-catalog-content-fixture") { await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(CatalogContentAuthoringTests.Content(),WorldJson.CreateOptions(writeIndented:true))); return; }
if (args.Length is 1 or 2 && args[0]=="--composition") { await FacilityCompositionTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if (args.Length==1 && args[0]=="--catalogs") { await CatalogContractTests.RunAsync(); return; }
if (args.Length is 1 or 2 && args[0]=="--capture") { await MockupCaptureTests.RunAsync(args.Length==2?Path.GetFullPath(args[1]):null); return; }
if (args.Length==1 && args[0]=="--roads") { await CurvedRoadTests.RunAsync(); return; }
if (args.Length==1 && args[0]=="--siting") { await SitingAlgorithmTests.RunAsync(); return; }
if (args.Length==2 && args[0]=="--write-siting-fixture")
{
    var fixturePath=Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
    await File.WriteAllTextAsync(fixturePath,JsonSerializer.Serialize(SitingAlgorithmTests.Create(),WorldJson.CreateOptions(writeIndented:true)));
    Console.WriteLine("Wrote synthetic contextual siting fixture; no production art or regulatory qualification."); return;
}
if (args.Length == 1 && args[0] == "--infrastructure")
{
    await InfrastructureAlgorithmTests.RunAsync();
    return;
}
if (args.Length == 2 && args[0] == "--write-infrastructure-fixture")
{
    var fixturePath=Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
    await File.WriteAllTextAsync(fixturePath,JsonSerializer.Serialize(InfrastructureAlgorithmTests.Create(),WorldJson.CreateOptions(writeIndented:true)));
    Console.WriteLine("Wrote synthetic infrastructure fixture; no production artwork or approval supplied.");
    return;
}
if (args.Length == 2 && args[0] == "--write-generation-fixture")
{
    var fixturePath = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
    await File.WriteAllTextAsync(fixturePath,JsonSerializer.Serialize(MockupGenerationFixture.Create(64,64,1),
        WorldJson.CreateOptions(writeIndented:true)));
    Console.WriteLine("Wrote synthetic generation fixture; no production artwork or approval supplied.");
    return;
}
var cliPath = args.Length == 2 && args[0] == "--cli" ? Path.GetFullPath(args[1]) : null;
if (args.Length != 0 && cliPath is null)
    throw new ArgumentException("Supported test option: --cli <WorldCreator.Cli.dll>");
await CurrentContractTests.RunAsync(cliPath);
await WorldSurfaceTests.RunAsync(cliPath);
await WorldRegionalTerrainWindowTests.RunAsync();
await WorldTerrainWindowTests.RunAsync();
await WorldTerrainTests.RunAsync(cliPath);
WorldFieldAreaTests.Run();
await WorldEnvironmentTests.RunAsync(cliPath);
await WorldFootprintTests.RunAsync(cliPath);
await WorldPlacementTests.RunAsync(cliPath);
await WorldHierarchyPlacementTests.RunAsync(cliPath);
await WorldPhysicalAccessTests.RunAsync(cliPath);
await WorldPlacementFeedbackTests.RunAsync(cliPath);
await WorldCapacityTests.RunAsync(cliPath);
await WorldClearanceTests.RunAsync(cliPath);
await WorldSurroundingsTests.RunAsync(cliPath);
await WorldServiceTests.RunAsync(cliPath);
await WorldSupplyPolicyTests.RunAsync(cliPath);
await WorldContextTests.RunAsync(cliPath);
await WorldProtectionTests.RunAsync();
await WorldVariationTests.RunAsync();
await WorldChunkTests.RunAsync();
await WorldResourceAreaTests.RunAsync(cliPath);
await WorldRouteTests.RunAsync(cliPath);
await WorldCorridorTests.RunAsync(cliPath);
await WorldRouteReuseTests.RunAsync();
await WorldCorridorCapacityTests.RunAsync();
await WorldRegionTests.RunAsync();
await WorldLocalLayoutTests.RunAsync();
await WorldNetworkTests.RunAsync(cliPath);
await MetadataLayerTests.RunAsync(cliPath);
var generator = new MockupWorldGenerator(TestCatalogs.Registry);
var req = MockupGenerationFixture.Create(32,32,1);
var first = await generator.GenerateAsync(req);
var second = await generator.GenerateAsync(req);
Assert(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second), "Determinism");
Assert(first.Cells.Count == 1024, "Grid dimensions");
Assert(first.Cells.Any(c => c.Kind == CellKind.Road), "Roads");
Assert(first.Objects.Any(o => o.Kind == "gate"), "Gate metadata");
var dir = Path.Combine(Path.GetTempPath(), "gameframework-test-" + Guid.NewGuid().ToString("N"));
try
{
    await new WorldDocumentExporter().ExportAsync(first, dir);
    Assert(File.Exists(Path.Combine(dir, "plan.svg")) && File.Exists(Path.Combine(dir, "world.json")), "Export outputs");
}
finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

var blueprint = new WorldBlueprint(1, "test-city", req, new[]
{
    new AssetRequirement("warehouse", "building", 8, 6, new[] { "S", "N" }, new[]
    {
        new ComponentRequirement("door", "warehouse", "door-hinge", true, AnimationTechnique.Frames,
            new[] { "closed", "opening", "open", "closing" })
    })
});
var empty = new InMemoryRegistry(null);
var missing = await new WorldBuildGate(empty).CheckAsync(blueprint);
Assert(!missing.Ready && missing.Issues.Any(i => i.Code == "ART_MISSING"), "Missing GameArts asset rejection");
const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
var manifest = new ApprovedArtManifest(2, "warehouse", "warehouse", "approved-r1", "7.5",
    ArtApprovalState.Approved, new[]
    {
        new SpriteSheetManifest("door-s", "door_s.png", "door_s.json", hash, hash, "door", new[] { "S" })
    },
    new[]
    {
        new ComponentAttachment("door", "warehouse", "door-hinge", 0.4, 0.2, new Dictionary<string, PixelAnchor>
        {
            ["S"] = new(8, 13)
        })
    });
var incomplete = await new WorldBuildGate(new InMemoryRegistry(manifest)).CheckAsync(blueprint);
Assert(!incomplete.Ready && incomplete.Issues.Any(i => i.Code == "ANIMATION_SHEET_MISSING"), "Incomplete direction sheet rejected");
Assert(incomplete.Issues.Any(i => i.Code == "DIRECTION_ANCHOR_MISSING"), "Missing direction attachment rejected");
var full = manifest with
{
    Sheets = new[]
    {
        manifest.Sheets[0],
        new SpriteSheetManifest("door-n", "door_n.png", "door_n.json", hash, hash, "door", new[] { "N" })
    },
    Attachments = new[]
    {
        manifest.Attachments[0] with
        {
            DirectionAnchors = new Dictionary<string, PixelAnchor> { ["S"] = new(8, 13), ["N"] = new(8, 12) }
        }
    }
};
var approved = await new WorldBuildGate(new InMemoryRegistry(full)).CheckAsync(blueprint);
Assert(approved.Ready, "Structurally complete art manifest accepted; byte/atlas verification remains separate");
var revoked = await new WorldBuildGate(new InMemoryRegistry(full with { ApprovalState = ArtApprovalState.Revoked })).CheckAsync(blueprint);
Assert(!revoked.Ready && revoked.Issues.Any(i => i.Code == "NOT_APPROVED"), "Revoked art rejected");


// Spatial foundation: no arbitrary parcel position or disconnected street frontage.
var planned = new SettlementPlanner(TestCatalogs.Registry).Plan(MockupGenerationFixture.Create());
Assert(planned.Diagnostics.Count == 0, "Planner reports no infeasible parcels for fixture");
var roadCells = planned.Roads.SelectMany(r => r.Cells()).ToHashSet();
Assert(planned.Roads.Any(r => r.Class == RoadClass.Collector), "Regional road spine");
Assert(planned.Buildings.Count == 8, "Building requirements met");
Assert(planned.Buildings.Select(b => b.Site).Distinct().Count() == planned.Buildings.Count, "Footprints non-overlapping");
Assert(planned.Buildings.All(b => roadCells.Contains(b.AccessRoadCell)), "Every building road frontage is valid");
Assert(planned.Buildings.All(b => !roadCells.Contains(b.Site)), "No building blocks a road");
Assert(planned.Buildings.All(b => !string.IsNullOrWhiteSpace(b.Purpose) &&
    !string.IsNullOrWhiteSpace(b.ConstraintId)), "Every placement is explainable");
var plannedAgain = new SettlementPlanner(TestCatalogs.Registry).Plan(MockupGenerationFixture.Create());
Assert(JsonSerializer.Serialize(planned) == JsonSerializer.Serialize(plannedAgain), "Deterministic spatial plan");
// All roads connect to one collector, and no structure overwrites the road raster.
var plannedWorld = await generator.GenerateAsync(MockupGenerationFixture.Create());
Assert(plannedWorld.Objects.Count(o => o.Kind == "warehouse") == 8, "World projects all planned buildings");
Assert(planned.Buildings.All(b => plannedWorld.At(b.AccessRoadCell.X, b.AccessRoadCell.Y).Kind == CellKind.Road),
    "Rasterized access road is preserved");


// Deliberately invalid plans must fail structural validation, not silently export.
var isolatedRoads = new[]
{
    new RoadSegment("west", new GridPoint(1, 1), new GridPoint(3, 1), RoadClass.Local),
    new RoadSegment("east", new GridPoint(10, 10), new GridPoint(12, 10), RoadClass.Local)
};
var invalidRoad = SpatialValidation.Validate(new GridBounds(16, 16), isolatedRoads,
    Array.Empty<PlacementDecision>());
Assert(invalidRoad.Any(d => d.Code == "DISCONNECTED_ROAD_NETWORK"), "Isolated roads rejected");
var invalidFrontage = new[]
{
    new PlacementDecision("test-building", "warehouse", new GridPoint(3, 3),
        "workshop capacity", "settlement-0", "road-frontage-v1", new GridPoint(1, 1))
};
var invalidParcels = SpatialValidation.Validate(new GridBounds(16, 16),
    new[] { isolatedRoads[0] }, invalidFrontage);
Assert(invalidParcels.Any(d => d.Code == "FRONTAGE_NOT_ADJACENT"), "Non-adjacent frontage rejected");
var frontageRule = new RoadFrontageConstraint();
Assert(!frontageRule.IsSatisfied(new GridPoint(3, 3), new GridPoint(1, 1),
    new HashSet<GridPoint> { new(1, 1) }, new HashSet<GridPoint>(),
    new GridBounds(16, 16)), "Constraint rejects bad frontage");
Assert(SpatialValidation.Validate(new GridBounds(48, 48), planned.Roads,
    planned.Buildings).Count == 0, "Planned roads and lots validate");


// Continuous world-meter geometry checks
var crossSection = new RoadCrossSection(3.5, 2, 2.0, 2.0, .15, .15);
var street = new StreetCorridor("main-street",
    new WorldPolyline(new[] { new WorldPoint(0, 0), new WorldPoint(10, 0) }), crossSection);
Assert(Math.Abs(crossSection.TotalWidthMeters - 11.3) < 1e-9, "Street cross-section width");
Assert(Math.Abs(street.Centerline.LengthMeters() - 10) < 1e-9, "World-meter centerline length");
var streetEdges = street.SegmentEdges();
Assert(streetEdges.Count == 1, "Road segment edges generated");
Assert(Math.Abs(streetEdges[0].LeftStart.Y - 5.65) < 1e-9, "Left road offset");
Assert(Math.Abs(streetEdges[0].RightEnd.Y + 5.65) < 1e-9, "Right road offset");
Assert(street.Bounds().Min.X < 0 && street.Bounds().Max.X > 10, "Road bounds include full cross-section");
AssertThrows<ArgumentException>(() => new WorldPolyline(new[] { new WorldPoint(1,1), new WorldPoint(1,1) }).Validate(),
    "Duplicate centerline points rejected");
AssertThrows<ArgumentOutOfRangeException>(() => new RoadCrossSection(-1, 2, 2, 2, 0, 0).Validate(),
    "Negative lane width rejected");
AssertThrows<ArgumentException>(() => new RoadCrossSection(3, 2, double.NaN, 2, 0, 0).Validate(),
    "Nonfinite sidewalk width rejected");



// Parametric urban surface bands are derived from the same street profile.
var bands = StreetSurfaceBuilder.Build(street);
Assert(bands.Count == 5, "Road has carriageway, two curbs and two sidewalks");
Assert(bands.All(b => b.Polygon.Count == 4), "Band polygons have four vertices");
Assert(bands.All(b => Math.Abs(StreetSurfaceBuilder.SignedArea(b.Polygon)) > 1e-8),
    "Generated surface bands have nonzero area");
var lane = bands.Single(b => b.Kind == StreetSurfaceKind.Carriageway);
Assert(Math.Abs(Math.Abs(StreetSurfaceBuilder.SignedArea(lane.Polygon)) - 70.0) < 1e-8,
    "Two lane carriageway area is 7m by 10m");
var walks = bands.Where(b => b.Kind is StreetSurfaceKind.LeftSidewalk or StreetSurfaceKind.RightSidewalk);
Assert(walks.All(b => Math.Abs(Math.Abs(StreetSurfaceBuilder.SignedArea(b.Polygon)) - 20.0) < 1e-8),
    "Each sidewalk is 2m by 10m");
var curvedBands = StreetSurfaceBuilder.Build(new StreetCorridor("bent",
    new WorldPolyline(new[] { new WorldPoint(0,0), new WorldPoint(10,0), new WorldPoint(10,10) }), crossSection));
Assert(curvedBands.Count(b=>b.SegmentIndex>=0) == 10 && curvedBands.Any(b=>b.SegmentIndex<0),
    "Both road segments retain bands and receive explicit bend joins");

// Continuous street intersection regression scenarios
var crossing = PlanarIntersections.Intersect(new(0, 0), new(10, 0), new(5, -5), new(5, 5));
Assert(crossing.Kind == IntersectionKind.Point && crossing.Point is { X: 5, Y: 0 }, "Orthogonal road intersection");
Assert(PlanarIntersections.Intersect(new(0, 0), new(10, 0), new(0, 2), new(10, 2)).Kind == IntersectionKind.None,
    "Parallel roads do not intersect");
Assert(PlanarIntersections.Intersect(new(0, 0), new(10, 0), new(5, 0), new(15, 0)).Kind ==
    IntersectionKind.CollinearOverlap, "Overlapping road centerlines detected");
Assert(PlanarIntersections.Intersect(new(0, 0), new(5, 0), new(5, 0), new(5, 5)).Kind ==
    IntersectionKind.Point, "Endpoint joins are intersections");
var crossingNetwork = StreetNetworkAnalyzer.Analyze(new[]
{
    new StreetCorridor("east-west", new WorldPolyline(new[] { new WorldPoint(0, 0), new WorldPoint(10, 0) }), crossSection),
    new StreetCorridor("north-south", new WorldPolyline(new[] { new WorldPoint(5, -5), new WorldPoint(5, 5) }), crossSection)
});
Assert(crossingNetwork.Junctions.Count == 1 && crossingNetwork.Junctions[0].Corridors.Count == 2,
    "Intersection carries both road IDs");
Assert(crossingNetwork.Diagnostics.Count == 0, "Valid crossing has no overlap warnings");


// Street topology must split true crossing centerlines and reject disconnected networks.
var connectedGraph = StreetTopologyBuilder.Build(new[]
{
    new StreetCorridor("horizontal", new WorldPolyline(new[] { new WorldPoint(0, 0), new WorldPoint(10, 0) }), crossSection),
    new StreetCorridor("vertical", new WorldPolyline(new[] { new WorldPoint(5, -5), new WorldPoint(5, 5) }), crossSection)
});
Assert(connectedGraph.Nodes.Count == 5 && connectedGraph.Edges.Count == 4, "Crossing forms four split edges and five nodes");
Assert(connectedGraph.IsConnected() && connectedGraph.Diagnostics.Count == 0, "Crossing graph connected");
var disconnectedGraph = StreetTopologyBuilder.Build(new[]
{
    new StreetCorridor("a", new WorldPolyline(new[] { new WorldPoint(0, 0), new WorldPoint(2, 0) }), crossSection),
    new StreetCorridor("b", new WorldPolyline(new[] { new WorldPoint(10, 0), new WorldPoint(12, 0) }), crossSection)
});
Assert(!disconnectedGraph.IsConnected() && disconnectedGraph.Diagnostics.Contains("DISCONNECTED_STREET_GRAPH"),
    "Separated streets are rejected as disconnected");


// Buildable block extraction from four orthogonal road corridors.
StreetCorridor RectRoad(string id, double ax, double ay, double bx, double by) =>
    new(id, new WorldPolyline(new[] { new WorldPoint(ax, ay), new WorldPoint(bx, by) }), crossSection);
var enclosed = UrbanBlockExtractor.Extract(new[]
{
    RectRoad("north", 0, 0, 100, 0), RectRoad("south", 0, 80, 100, 80),
    RectRoad("west", 0, 0, 0, 80), RectRoad("east", 100, 0, 100, 80)
});
Assert(enclosed.Blocks.Count == 1 && enclosed.Diagnostics.Count == 0, "Enclosed road rectangle yields one block");
Assert(Math.Abs(enclosed.Blocks[0].AreaSquareMeters - (100 - 11.3) * (80 - 11.3)) < 1e-7,
    "Block interior accounts for road corridor width");
Assert(UrbanBlockExtractor.Extract(new[]
{
    RectRoad("north", 0, 0, 100, 0), RectRoad("south", 0, 80, 100, 80),
    RectRoad("west", 0, 0, 0, 80)
}).Blocks.Count == 0, "Missing road edge cannot form a closed block");


// Polygon validity and minimum-frontage parcel subdivision
var parcels = FrontageParcelPlanner.Subdivide(enclosed.Blocks[0], 20, 8);
Assert(parcels.Diagnostics.Count == 0 && parcels.Parcels.Count == 4,
    "Block subdivides into four purpose-defined frontages");
Assert(parcels.Parcels.All(p => p.Boundary.SignedArea() > 0), "Parcel geometry valid");
Assert(Math.Abs(parcels.Parcels.Sum(p => p.Boundary.SignedArea()) -
    enclosed.Blocks[0].AreaSquareMeters) < 1e-7, "Parcel coverage conserves buildable area");
Assert(parcels.Parcels.All(p => p.StreetAccess.Y == enclosed.Blocks[0].Boundary[0].Y),
    "Every parcel has a street-facing anchor");
AssertThrows<ArgumentException>(() => new WorldPolygon(new[]
{
    new WorldPoint(0,0), new WorldPoint(10,10), new WorldPoint(0,10), new WorldPoint(10,0)
}).Validate(), "Bow-tie polygon rejected");


// Convex irregular parcels can be split without losing area.
var irregular = new WorldPolygon(new[]
{
    new WorldPoint(0, 0), new WorldPoint(30, 0),
    new WorldPoint(25, 20), new WorldPoint(5, 20)
});
Assert(ConvexParcelGeometry.IsConvex(irregular), "Trapezoidal parcel is convex");
var (western, eastern) = ConvexParcelGeometry.SplitVertical(irregular, 15);
western.Validate(); eastern.Validate();
Assert(Math.Abs(Math.Abs(western.SignedArea()) + Math.Abs(eastern.SignedArea()) -
    Math.Abs(irregular.SignedArea())) < 1e-7, "Irregular parcel split conserves area");
AssertThrows<ArgumentOutOfRangeException>(() => ConvexParcelGeometry.SplitVertical(irregular, 45),
    "Outside split rejected");


// Building rules are explicit and must not force-fit infeasible structures.
var parcel = parcels.Parcels[0];
var houseRules = new BuildingEnvelopeRules("house", 4, 3, 2, 8, 10);
var house = BuildingEnvelopePlanner.Plan(parcel, houseRules);
Assert(house.IsFeasible && house.Footprint is not null, "Residential envelope feasible");
Assert(house.Footprint!.SignedArea() > 0, "House buildable area positive");
var tower = BuildingEnvelopePlanner.Plan(parcel,
    new BuildingEnvelopeRules("tower", 8, 8, 5, 40, 40));
Assert(!tower.IsFeasible && tower.Diagnostics.Contains("INSUFFICIENT_BUILDABLE_ENVELOPE"),
    "Tower denied on undersized town parcel");
AssertThrows<ArgumentException>(() => new BuildingEnvelopeRules("house", -1, 1, 1, 8, 8).Validate(),
    "Negative setbacks rejected");


// Settlement identity selects functional building programs, not arbitrary sprites.
var programPlanner = new BuildingProgramPlanner(new ModernCatalog().Structures,new ModernDemandPolicy());
var villageProgram = programPlanner.Plan(new(SettlementClass.Village, SettlementRole.Agricultural, 200));
var townProgram = programPlanner.Plan(new(SettlementClass.Town, SettlementRole.Commercial, 200));
var cityProgram = programPlanner.Plan(new(SettlementClass.City, SettlementRole.Industrial, 200));
Assert(villageProgram.Diagnostics.Count == 0 && townProgram.Diagnostics.Count == 0 &&
    cityProgram.Diagnostics.Count == 0, "Baseline settlement profiles have housing");
Assert(villageProgram.Needs.Any(n => n.DefinitionId == "village-house"), "Village houses present");
Assert(!villageProgram.Needs.Any(n => n.DefinitionId == "apartment"), "No city apartments in village");
Assert(townProgram.Needs.Any(n => n.DefinitionId == "townhouse"), "Town housing selected");
Assert(cityProgram.Needs.Any(n => n.DefinitionId == "apartment") &&
    cityProgram.Needs.Any(n => n.DefinitionId == "factory"), "Industrial city program selected");
Assert(!townProgram.Needs.Any(n => n.DefinitionId == "factory"), "No unrelated city factory in commercial town");
Assert(JsonSerializer.Serialize(programPlanner.Plan(new(SettlementClass.Village, SettlementRole.Agricultural, 200))) ==
    JsonSerializer.Serialize(villageProgram), "Building demand reproducible");


// Public-realm features are anchored to actual streets, never uniformly scattered.
var publicStreet = new StreetCorridor("public-main",
    new WorldPolyline(new[] { new WorldPoint(0, 0), new WorldPoint(100, 0) }), crossSection);
var publicDesign = MockupGenerationFixture.Create().Design! with {
    Blueprint = new(1,"synthetic-public-mockup","simulated-r1","synthetic-style",
        [new("public-region",MockupGenerationFixture.Rect(-5,-5,105,30),"street-composition")],[]),
    Paths = [new("public-main","public-region",publicStreet.Centerline,"road")],
    PublicRealmPatterns = [new("lights","public-main","Streetlight","authored street decoration","street-style-v1",25,25,25,12,1.5),
        new("signs","public-main","StreetNameSign","identify the designed street approaches","street-style-v1",100,0,0,12,1.5)]
};
var publicPlan = new PublicRealmPlanner().Plan(publicDesign);
Assert(publicPlan.Diagnostics.Count == 0, "Street accessories have legal clearances");
Assert(publicPlan.Features.Count == 5, "Street endpoints and internal light positions derived from spacing");
Assert(publicPlan.Features.Count(f => f.Kind == PublicFeatureKind.Streetlight) == 3,
    "Internal illumination fixtures spaced along corridor");
Assert(publicPlan.Features.All(f => f.ParentId == "public-main" && f.Position.Y > 0 &&
    !string.IsNullOrWhiteSpace(f.Purpose)), "Public features anchored with purpose");
var reserved = new SpatialReservationIndex();
Assert(reserved.TryReserve(new WorldPoint(0, 0), 2), "First reservation succeeds");
Assert(!reserved.TryReserve(new WorldPoint(1, 0), 2), "Overlapping accessory rejected");
Assert(reserved.TryReserve(new WorldPoint(5, 0), 2), "Separated accessory allowed");
Assert(JsonSerializer.Serialize(new PublicRealmPlanner().Plan(publicDesign)) ==
    JsonSerializer.Serialize(publicPlan), "Public realm deterministic");


var requiredDesign = new WorldDesignPlan("planned-world", new GridBounds(16, 16), new[]
{
    new PlannedWorldObject("depot", "warehouse", new GridPoint(3, 4),
        "service goods transport", "industrial-program-v1", "planned-world"),
    new PlannedWorldObject("depot-door", "door", new GridPoint(3, 4),
        "accessible depot entry", "door-component-v1", "planned-world", "depot", "closed", "door-open")
});
var constructed = new WorldPlanCompiler().Compile(requiredDesign);
Assert(constructed.Count == 2, "Valid design compiles parent and component");
var missingPurpose = requiredDesign with { Objects = new[]
{
    requiredDesign.Objects[0] with { Purpose = "" }
}};
AssertThrows<WorldPlanException>(() => new WorldPlanCompiler().Compile(missingPurpose),
    "Missing design purpose blocks construction");
var missingParent = requiredDesign with { Objects = new[]
{
    requiredDesign.Objects[1] with { ParentObjectId = "unknown" }
}};
Assert(new WorldDesignValidator().Validate(missingParent).Issues.Any(i => i.Code == "PARENT_NOT_FOUND"),
    "Orphan component rejected");
var overlapping = requiredDesign with { Objects = new[]
{
    requiredDesign.Objects[0],
    requiredDesign.Objects[0] with { Id = "unrelated" }
}};
Assert(new WorldDesignValidator().Validate(overlapping).Issues.Any(i => i.Code == "ROOT_FOOTPRINT_CONFLICT"),
    "Two independent objects cannot occupy identical root position");


var coordinator = new WorldPlanCoordinator();
var disciplines = new[]
{
    new DesignDiscipline("objects", new[] { "parcels", "transport" }),
    new DesignDiscipline("transport", new[] { "terrain" }),
    new DesignDiscipline("parcels", new[] { "transport" }),
    new DesignDiscipline("terrain", Array.Empty<string>())
};
var coordinated = coordinator.Coordinate(disciplines);
Assert(coordinated.IsValid &&
    string.Join(",", coordinated.ExecutionOrder) == "terrain,transport,parcels,objects",
    "Design dependencies control execution order");
var missingDesign = coordinator.Coordinate(new[]
{
    new DesignDiscipline("lighting", new[] { "street-plan" })
});
Assert(!missingDesign.IsValid && missingDesign.Issues.Any(i => i.Code == "DESIGN_DEPENDENCY_MISSING"),
    "Missing street design blocks lighting");
var circularDesign = coordinator.Coordinate(new[]
{
    new DesignDiscipline("terrain", new[] { "roads" }),
    new DesignDiscipline("roads", new[] { "terrain" })
});
Assert(!circularDesign.IsValid && circularDesign.Issues.Any(i => i.Code == "DESIGN_DEPENDENCY_CYCLE"),
    "Circular design dependencies rejected");


var integrated = new SettlementDesignPipeline(TestCatalogs.Registry).Build(MockupGenerationFixture.Create());
Assert(integrated.Settlement.Buildings.Count == 8, "Integrated settlement has planned building demand");
Assert(integrated.Streets.Count == integrated.Settlement.Roads.Count, "Street geometry follows transport plan");
Assert(integrated.PublicRealm.Features.All(f => integrated.Streets.Any(s => s.Id == f.ParentId)),
    "Public realm features reference planned streets");
Assert(new WorldDesignValidator().Validate(integrated.Design).IsValid, "Integrated object plan validates");
Assert(new WorldPlanCompiler().Compile(integrated.Design).Count(o => o.Kind is "warehouse" or "gate") == 16,
    "Integrated world compiles buildings with attached gates");
Assert(JsonSerializer.Serialize(integrated) ==
    JsonSerializer.Serialize(new SettlementDesignPipeline(TestCatalogs.Registry).Build(MockupGenerationFixture.Create())),
    "Integrated settlement deterministic");


var integratedGate = new SettlementDesignGate();
Assert(integratedGate.Validate(integrated).Count == 0, "Integrated design passes cross-discipline validation");
Assert(integratedGate.Compile(integrated).Count == integrated.Design.Objects.Count,
    "Validated integrated design compiles without inventing objects");
var brokenPublicRealm = integrated with
{
    PublicRealm = integrated.PublicRealm with
    {
        Features = new[]
        {
            new PublicFeature("orphan-sign", PublicFeatureKind.StreetNameSign,
                new WorldPoint(10, 10), "missing-road", "road naming", "signage-v1", 1)
        }
    }
};
Assert(integratedGate.Validate(brokenPublicRealm).Any(d => d.StartsWith("ORPHAN_PUBLIC_FEATURE")),
    "Orphan street signs rejected");
AssertThrows<InvalidOperationException>(() => integratedGate.Compile(brokenPublicRealm),
    "Cannot compile a design containing orphan features");
var incompleteIntegrated = integrated with { Diagnostics = new[] { "UNMET_DESIGN_REQUIREMENT" } };
AssertThrows<InvalidOperationException>(() => integratedGate.Compile(incompleteIntegrated),
    "Unresolved stage diagnostics block compilation");


var visual = new ApprovedVisualWorldBlueprint(1, "town-mockup", "approved-r1", "pixel-cozy",
    new[] { new VisualRegion("square", new WorldPolygon(new[]
    {
        new WorldPoint(0,0), new WorldPoint(64,0), new WorldPoint(64,64), new WorldPoint(0,64)
    }), "town-square") },
    new[] { new VisualAnchor("shop-entry", "square", new WorldPoint(24,32), "shop") });
var visualDecisions = new[] { new VisualObjectDecision("shop", "building", "shop-entry",
    "anchor the storefront in the approved square", "mockup-shop-v1") };
var visualPlan = new VisualWorldPlanner().Plan(visual, visualDecisions,
    "approved-town-design", new GridBounds(16,16), 4);
Assert(visualPlan.IsValid && visualPlan.Design!.Objects[0].Site == new GridPoint(6,8),
    "Approved mockup anchor compiles into planned game location");
Assert(new WorldPlanCompiler().Compile(visualPlan.Design!).Count == 1,
    "Approved visual world plan compiles");
var absentAnchor = new VisualWorldPlanner().Plan(visual,
    new[] { visualDecisions[0] with { AnchorId = "missing" } },
    "approved-town-design", new GridBounds(16,16), 4);
Assert(!absentAnchor.IsValid && absentAnchor.Issues.Any(i => i.Code == "VISUAL_ANCHOR_MISSING"),
    "No guessing placement for missing visual anchors");
var unapprovedMockup = new VisualWorldPlanner().Plan(visual with { ApprovedRevision = "" },
    visualDecisions, "approved-town-design", new GridBounds(16,16), 4);
Assert(!unapprovedMockup.IsValid, "No world build without approved mockup revision");


var pattern = new RegionPattern("garden-hedge", "square", "hedge", "define garden boundary", 8, 4, "approved-garden-row-v1",2,4,56);
var repeated = new VisualRegionPatternGenerator().Generate(visual, pattern);
Assert(repeated.Issues.Count == 0 && repeated.Decisions.Count == 8,
    "Approved visual region produces repeated planned hedge elements");
var mergedVisual = visual with { Anchors = visual.Anchors.Concat(repeated.GeneratedAnchors).ToArray() };
var repeatedWorld = new VisualWorldPlanner().Plan(mergedVisual, repeated.Decisions,
    "hedge-plan", new GridBounds(16,16), 4);
Assert(repeatedWorld.IsValid, "Generated grammar anchors compile through visual world planning");
Assert(new VisualRegionPatternGenerator().Generate(visual, pattern with { RegionId = "unknown" })
    .Issues.Any(i => i.Code == "REGION_MISSING"), "Unknown region blocks pattern");


var designedPath = new VisualDesignPath("garden-border", "square",
    new WorldPolyline(new[]
    {
        new WorldPoint(4, 4), new WorldPoint(36, 4), new WorldPoint(36, 36)
    }), "garden-edge");
var pathGrammar = new PathPattern("garden-lamps", "garden-border",
    "decorative-lamp", "mark the authored garden route", "mockup-path-v1", 8, 4, 4);
var pathGenerated = new VisualPathPatternGenerator().Generate(visual, new[] { designedPath }, pathGrammar);
Assert(pathGenerated.Issues.Count == 0 && pathGenerated.Anchors.Count == 8,
    "Path geometry determines decorative fixture count");
Assert(pathGenerated.Anchors[0].PositionMeters == new WorldPoint(8, 4) &&
    pathGenerated.Anchors[4].PositionMeters == new WorldPoint(36, 8),
    "Path samples follow authored turns in order");
Assert(new VisualPathPatternGenerator().Generate(visual, new[] { designedPath },
    pathGrammar with { PathId = "unknown" }).Issues.Any(i => i.Code == "VISUAL_PATH_MISSING"),
    "Missing visual path cannot trigger arbitrary placements");
Assert(JsonSerializer.Serialize(pathGenerated) == JsonSerializer.Serialize(
    new VisualPathPatternGenerator().Generate(visual, new[] { designedPath }, pathGrammar)),
    "Path placements reproducible");


var composed = new VisualCompositionGenerator().Generate(
    new VisualCompositionRequest(visual,
        new[] { pattern },
        new[] { designedPath },
        new[] { pathGrammar },
        visualDecisions),
    "composed-game-town", new GridBounds(16,16), 4);
Assert(composed.IsValid, "Approved mockup combines distinct building, region and path grammars");
Assert(composed.Design!.Objects.Count == 17, "Explicit object and sixteen pattern elements combined");
Assert(new WorldPlanCompiler().Compile(composed.Design).Count == 17,
    "Compiled objects preserve planned composition");
var badComposition = new VisualCompositionGenerator().Generate(
    new VisualCompositionRequest(visual,
        new[] { pattern, pattern },
        Array.Empty<VisualDesignPath>(),
        Array.Empty<PathPattern>(),
        Array.Empty<VisualObjectDecision>()),
    "duplicate-grammar", new GridBounds(16,16), 4);
Assert(!badComposition.IsValid &&
    badComposition.Issues.Any(i => i.Code == "COMPOSITION_ANCHOR_COLLISION"),
    "Duplicate design grammar IDs cannot silently overwrite artwork");


var artBound = await new WorldArtAssembler(new InMemoryRegistry(full)).AssembleAsync(
    requiredDesign with { Objects = new[] { requiredDesign.Objects[0] } },
    new[] { new WorldArtBinding("depot", "warehouse", "door", "S", 2) });
Assert(artBound.IsValid && artBound.Objects.Count == 1 &&
    artBound.Objects[0].Art.RenderLayer == 2, "Approved art binding preserves planned object and render layer");
var wrongFacing = await new WorldArtAssembler(new InMemoryRegistry(full)).AssembleAsync(
    requiredDesign with { Objects = new[] { requiredDesign.Objects[0] } },
    new[] { new WorldArtBinding("depot", "warehouse", "door", "E", 2) });
Assert(!wrongFacing.IsValid && wrongFacing.Issues.Any(i => i.Code == "ART_DIRECTION_UNAVAILABLE"),
    "Unapproved facing cannot be substituted");
var missingArtBinding = await new WorldArtAssembler(new InMemoryRegistry(full)).AssembleAsync(
    requiredDesign, Array.Empty<WorldArtBinding>());
Assert(!missingArtBinding.IsValid && missingArtBinding.Issues.Count(i => i.Code == "ART_BINDING_MISSING") == 2,
    "No constructed visual objects without explicit asset bindings");


var artDir = Path.Combine(Path.GetTempPath(), "gameframework-art-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(artDir);
try
{
    // Tiny valid 1x1 opaque PNG fixture and structured atlas file with actual measured bytes.
    var pngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/3ioAAAAASUVORK5CYII=");
    var pngPath = Path.Combine(artDir, "fixture.png");
    var atlasPath = Path.Combine(artDir, "fixture.json");
    await File.WriteAllBytesAsync(pngPath, pngBytes);
    await File.WriteAllTextAsync(atlasPath, "{\"frames\":[]}");
    static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    var fileSheet = new SpriteSheetManifest("fixture", "fixture.png", "fixture.json",
        Sha256Hex(pngBytes), Sha256Hex(await File.ReadAllBytesAsync(atlasPath)), "door", new[] { "S" });
    var fileManifest = full with { Sheets = new[] { fileSheet } };
    var verifier = new GameArtsFileVerifier(artDir);
    var goodFiles = await verifier.VerifyAsync(fileManifest);
    Assert(goodFiles.IsValid, "Approved asset files match real PNG and atlas hashes");
    await File.WriteAllTextAsync(atlasPath, "{\"frames\":[1]}");
    var mismatched = await verifier.VerifyAsync(fileManifest);
    Assert(mismatched.Issues.Any(i => i.Code == "ASSET_HASH_MISMATCH"),
        "Modified atlas fails hash validation");
    var traversal = fileManifest with
    {
        Sheets = new[] { fileSheet with { ImagePath = "../outside.png" } }
    };
    var denied = await verifier.VerifyAsync(traversal);
    Assert(denied.Issues.Any(i => i.Code == "ASSET_PATH_INVALID"),
        "Traversal path rejected");
}
finally { Directory.Delete(artDir, recursive: true); }


var bundleRoot = Path.Combine(Path.GetTempPath(), "gamearts-native-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(bundleRoot);
try
{
    var bundlePath = Path.Combine(bundleRoot, "sample.bundle.json");
    var nativeJson = """
    {
      "schema_version":"1.0","asset_id":"gate-asset","directions":["S","E"],
      "components":[
        {"id":"base","parent":null,"mount":null,
         "atlases":{"S":"base_s.atlas.json","E":"base_e.atlas.json"}},
        {"id":"door","parent":"base","mount":"hinge",
         "atlases":{"S":"door_s.atlas.json","E":"door_e.atlas.json"}}
      ]
    }
    """;
    await File.WriteAllTextAsync(bundlePath, nativeJson);
    var nativeResult = await new GameArtsBundleReader().ReadAsync(bundlePath);
    Assert(nativeResult.IsValid && nativeResult.Bundle!.Components.Count == 2,
        "Native GameArts component bundle parsed");
    // Parent atlas exists, but must provide the child's named hinge on every direction.
    var parentAtlas = """
    {"schema_version":"1.0","image":"base.png","image_size":[1,1],
     "mask_color":"#FF00FF","background_alpha":255,
     "frames":[{"rect":[0,0,1,1],"pivot":[0,0],"anchors":{}}]}
    """;
    await File.WriteAllTextAsync(Path.Combine(bundleRoot, "base_s.atlas.json"), parentAtlas);
    await File.WriteAllTextAsync(Path.Combine(bundleRoot, "base_e.atlas.json"), parentAtlas);
    var mountErrors = await new GameArtsBundleFileValidator().ValidateAsync(bundlePath);
    Assert(mountErrors.Any(e => e == "PARENT_MOUNT_MISSING:door:S:0") &&
        mountErrors.Any(e => e == "PARENT_MOUNT_MISSING:door:E:0"),
        "Every parent direction/frame must provide the child mount");
    var bundleFiles = await new GameArtsBundleFileValidator().ValidateAsync(bundlePath);
    Assert(bundleFiles.Any(e => e.StartsWith("ATLAS_MISSING")),
        "Bundle rejects missing atlas files instead of treating references as verified");
    await File.WriteAllTextAsync(bundlePath, nativeJson.Replace("\"E\":\"door_e.atlas.json\"", "\"bad\":\"door_e.atlas.json\""));
    var missingDirection = await new GameArtsBundleReader().ReadAsync(bundlePath);
    Assert(!missingDirection.IsValid && missingDirection.Errors.Any(e => e.StartsWith("INCOMPLETE_COMPONENT_DIRECTIONS")),
        "Native bundle missing component direction rejected");
}
finally { Directory.Delete(bundleRoot, recursive: true); }


var atlasTestDir = Path.Combine(Path.GetTempPath(), "gamearts-atlas-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(atlasTestDir);
try
{
    var pngFile = Path.Combine(atlasTestDir, "sprite.png");
    var atlasFile = Path.Combine(atlasTestDir, "sprite.atlas.json");
    await File.WriteAllBytesAsync(pngFile, Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/3ioAAAAASUVORK5CYII="));
    var validAtlas = """
    {"schema_version":"1.0","image":"sprite.png","image_size":[1,1],
     "mask_color":"#FF00FF","background_alpha":255,
     "frames":[{"rect":[0,0,1,1],"pivot":[0,0],"anchors":{"mount":[0,0]}}]}
    """;
    await File.WriteAllTextAsync(atlasFile, validAtlas);
    Assert((await new GameArtsAtlasValidator().ValidateAsync(atlasFile, pngFile)).Count == 0,
        "Real PNG and measured atlas geometry accepted");
    await File.WriteAllTextAsync(atlasFile, validAtlas.Replace("[0,0,1,1]", "[0,0,2,1]"));
    Assert((await new GameArtsAtlasValidator().ValidateAsync(atlasFile, pngFile))
        .Any(e => e.StartsWith("FRAME_OUT_OF_BOUNDS")),
        "Atlas rectangles must fit actual sheet");
    await File.WriteAllTextAsync(atlasFile, validAtlas.Replace("\"pivot\":[0,0]", "\"pivot\":[2,0]"));
    Assert((await new GameArtsAtlasValidator().ValidateAsync(atlasFile, pngFile))
        .Any(e => e.StartsWith("PIVOT_OUT_OF_BOUNDS")),
        "Atlas pivot must be cell-local and in bounds");
}
finally { Directory.Delete(atlasTestDir, true); }


var motionFolder = Path.Combine(Path.GetTempPath(), "gamearts-motion-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(motionFolder);
try
{
    var file = Path.Combine(motionFolder, "sample.bundle.json");
    var motionJson = """
    {"schema_version":"1.0","asset_id":"gate","directions":["S"],
     "component_order":["base"],
     "components":[{"id":"base","parent":null,"atlases":{"S":"base_s.atlas.json"}}],
     "sampled_poses":[{"direction":"S","normalized_time":0.5,
       "nodes":{"base":{"frame_index":0,"world_matrix_2d":[[1,0,0],[0,1,0],[0,0,1]]}}}]}
    """;
    await File.WriteAllTextAsync(file, motionJson);
    Assert((await new GameArtsMotionValidator().ValidateAsync(file)).Count == 0,
        "Finite sampled component pose accepted");
    await File.WriteAllTextAsync(file, motionJson.Replace("\"normalized_time\":0.5", "\"normalized_time\":1.5"));
    Assert((await new GameArtsMotionValidator().ValidateAsync(file))
        .Contains("POSE_DIRECTION_OR_TIME_INVALID"), "Out-of-range normalized pose time rejected");
    await File.WriteAllTextAsync(file, motionJson.Replace("\"frame_index\":0", "\"frame_index\":-1"));
    Assert((await new GameArtsMotionValidator().ValidateAsync(file))
        .Contains("POSE_FRAME_INDEX_INVALID:base"), "Negative animation frame rejected");
}
finally { Directory.Delete(motionFolder, true); }


var approvalRoot = Path.Combine(Path.GetTempPath(), "gamearts-gate-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(approvalRoot);
try
{
    var bundlePath = Path.Combine(approvalRoot, "empty.bundle.json");
    await File.WriteAllTextAsync(bundlePath, """
    {"schema_version":"1.0","asset_id":"sample","directions":["S"],
     "components":[{"id":"base","parent":null,"atlases":{"S":"missing.atlas.json"}}],
     "sampled_poses":[{"direction":"S","normalized_time":0,
     "nodes":{"base":{"frame_index":0,"world_matrix_2d":[[1,0,0],[0,1,0],[0,0,1]]}}}]}
    """);
    var gate = new GameArtsProductionGate(new TestApprovalAuthority(true));
    var result = await gate.CheckAsync(bundlePath, "r1", "lifecycle-sample", "sample");
    Assert(!result.Ready && result.Approved && result.Issues.Any(i => i.StartsWith("ATLAS_MISSING")),
        "Approved but incomplete bundles cannot pass readiness");
    var unapproved = await new GameArtsProductionGate(new TestApprovalAuthority(false))
        .CheckAsync(bundlePath, "r1", "lifecycle-sample", "sample");
    Assert(!unapproved.Ready && unapproved.Issues.Contains("LIFECYCLE_APPROVAL_NOT_VERIFIED"),
        "Technical checks cannot bypass approval authority");
}
finally { Directory.Delete(approvalRoot, true); }

var packageBuilder = new WorldAssemblyPackageBuilder();
var (portable, portableReport) = packageBuilder.Build(visual,
    requiredDesign with { Objects = new[] { requiredDesign.Objects[0] } },
    new[] { new WorldArtBinding("depot", "warehouse", "door", "S", 2) });
Assert(portableReport.Valid && portable is not null && portable.Objects.Count == 1,
    "Approved visual design creates portable engine-independent package");
var (unboundPackage, unboundReport) = packageBuilder.Build(visual,
    requiredDesign, Array.Empty<WorldArtBinding>());
Assert(unboundPackage is null && unboundReport.Errors.Any(e => e.StartsWith("ART_REFERENCE_MISSING")),
    "World package rejects missing visual art bindings");
var portableDir = Path.Combine(Path.GetTempPath(), "world-package-" + Guid.NewGuid().ToString("N"));
try
{
    var packagePath = Path.Combine(portableDir, "world.json");
    await packageBuilder.WriteAsync(portable!, packagePath);
    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(packagePath));
    Assert(json.RootElement.GetProperty("MockupId").GetString() == "town-mockup",
        "World assembly retains approved mockup identity");
}
finally { if (Directory.Exists(portableDir)) Directory.Delete(portableDir, true); }


var semanticBuilder = new SemanticWorldBuilder();
var squareShape = visual.Regions[0].Outline;
var semantic = semanticBuilder.Build(portable!, visual,
    new[] { new TerrainRegion("ground-square","grass",squareShape,"square","approved-terrain-v1") },
    new[] { new PlannedWorldPath("walkway","pedestrian",
        new WorldPolyline(new[] { new WorldPoint(4,4),new WorldPoint(24,4) }),
        "mockup-main-walkway","approved-path-v1") },
    new[] { new LandscapeZone("garden","flowerbed",new WorldPolygon(new[]
    { new WorldPoint(8,8),new WorldPoint(16,8),new WorldPoint(16,16),new WorldPoint(8,16) }),
        "square","approved-garden-v1") });
Assert(semantic.Valid && semantic.Package!.Terrain.Count == 1 &&
    semantic.Package.Paths.Count == 1 && semantic.Package.Landscape.Count == 1,
    "Semantic world includes terrain pathways and landscaping");
var badSemantic = semanticBuilder.Build(portable!, visual,
    new[] { new TerrainRegion("ground-square","grass",squareShape,"not-approved","approved-terrain-v1") },
    Array.Empty<PlannedWorldPath>(),Array.Empty<LandscapeZone>());
Assert(!badSemantic.Valid && badSemantic.Errors.Any(e => e.StartsWith("UNKNOWN_VISUAL_REGION")),
    "Semantic terrain cannot originate from an unknown visual region");


var gardenZone = semantic.Package!.Landscape.Single();
var gardenStyle = new LandscapeBandStyle("cozy-flower-rows", "pink-flowers", 1, 1, 1);
var gardenComposition = new LandscapeBandComposer().Compose(gardenZone, gardenStyle);
Assert(gardenComposition.Diagnostics.Count == 0 && gardenComposition.Bands.Count == 3,
    "Approved garden region yields three intentional planted rows");
Assert(gardenComposition.Bands.All(b => b.ParentZoneId == "garden" &&
    b.MaterialId == "pink-flowers" && b.Shape.SignedArea() > 0),
    "Garden bands preserve style and source zone");
Assert(JsonSerializer.Serialize(gardenComposition) ==
    JsonSerializer.Serialize(new LandscapeBandComposer().Compose(gardenZone, gardenStyle)),
    "Landscape composition is deterministic");
Assert(new LandscapeBandComposer().Compose(gardenZone,
    new LandscapeBandStyle("huge", "flowers", 10, 1, 1)).Diagnostics
    .Contains("LANDSCAPE_BORDER_EXCEEDS_ZONE:garden"),
    "Insufficient garden space reports failure instead of placing elsewhere");


var irregularGarden = new LandscapeZone("angled-garden", "flowerbed",
    new WorldPolygon(new[]
    {
        new WorldPoint(0,0), new WorldPoint(30,0),
        new WorldPoint(25,20), new WorldPoint(5,20)
    }), "square", "approved-angled-garden-v1");
var insetStyle = new LandscapeInsetStyle("cozy-border", "flowers", 2);
var angledInset = new ConvexLandscapeInsetComposer().Compose(irregularGarden, insetStyle);
Assert(angledInset.Diagnostics.Count == 0 && angledInset.Interior is not null,
    "Convex irregular garden supports designed interior inset");
Assert(Math.Abs(angledInset.Interior!.SignedArea()) < Math.Abs(irregularGarden.Boundary.SignedArea()),
    "Inset garden interior remains smaller than parent region");
var excessiveInset = new ConvexLandscapeInsetComposer().Compose(irregularGarden,
    insetStyle with { InsetMeters = 100 });
Assert(excessiveInset.Interior is null && excessiveInset.Diagnostics.Count != 0,
    "Oversized landscape inset fails without fallback placement");


var concaveGarden = new WorldPolygon(new[]
{
    new WorldPoint(0,0), new WorldPoint(6,0), new WorldPoint(6,2),
    new WorldPoint(2,2), new WorldPoint(2,6), new WorldPoint(0,6)
});
var pieces = LandscapePolygonTriangulator.Triangulate(concaveGarden);
Assert(pieces.Count == 4, "Six-vertex concave garden decomposes into four triangles");
Assert(pieces.All(p => p.Vertices.Count == 3 && p.SignedArea() > 0),
    "Triangulated areas retain original winding and remain non-degenerate");
Assert(Math.Abs(pieces.Sum(p => p.SignedArea()) - concaveGarden.SignedArea()) < 1e-8,
    "Concave garden triangulation conserves authored area");
Assert(JsonSerializer.Serialize(pieces) ==
    JsonSerializer.Serialize(LandscapePolygonTriangulator.Triangulate(concaveGarden)),
    "Concave garden triangulation deterministic");


var concaveVisual = visual with { Regions = new[] {
    new VisualRegion("forest-edge", concaveGarden, "woodland-clearing")
}};
var terrainStyles = new Dictionary<string, TerrainCompositionStyle> {
    ["forest-edge"] = new("cozy-woodland", "forest-ground", "approved-woodland-fill-v1")
};
var styledTerrain = new StyledTerrainComposer().Compose(concaveVisual,terrainStyles);
Assert(styledTerrain.Diagnostics.Count == 0 && styledTerrain.Pieces.Count == 4,
    "Approved concave region creates four exact terrain fill pieces");
Assert(Math.Abs(styledTerrain.Pieces.Sum(p => Math.Abs(p.Geometry.SignedArea())) -
    Math.Abs(concaveGarden.SignedArea())) < 1e-8,
    "Styled terrain preserves authored region area");
Assert(styledTerrain.Pieces.All(p => p.SourceRegionId == "forest-edge" &&
    p.MaterialId == "forest-ground"), "Terrain keeps material and region provenance");
Assert(new StyledTerrainComposer().Compose(concaveVisual,
    new Dictionary<string, TerrainCompositionStyle>()).Diagnostics
    .Contains("REGION_STYLE_MISSING:forest-edge"), "Missing style cannot trigger arbitrary terrain");


var terrainBindings = new[] { new TerrainMaterialBinding("forest-ground","warehouse","door","S") };
var boundTerrain = await new TerrainArtAssembler(new InMemoryRegistry(full))
    .AssembleAsync(styledTerrain, terrainBindings);
Assert(boundTerrain.IsValid && boundTerrain.Pieces.Count == styledTerrain.Pieces.Count,
    "Approved terrain material preserves every styled polygon");
Assert(boundTerrain.Pieces.All(piece => piece.Material.MaterialId == "forest-ground"),
    "Terrain assembly retains original design material");
var unavailableTerrain = await new TerrainArtAssembler(new InMemoryRegistry(null))
    .AssembleAsync(styledTerrain, terrainBindings);
Assert(!unavailableTerrain.IsValid &&
    unavailableTerrain.Issues.Any(issue => issue.StartsWith("TERRAIN_ART_NOT_APPROVED")),
    "Missing GameArts terrain material fails without substitute");
var unboundTerrain = await new TerrainArtAssembler(new InMemoryRegistry(full))
    .AssembleAsync(styledTerrain, Array.Empty<TerrainMaterialBinding>());
Assert(!unboundTerrain.IsValid &&
    unboundTerrain.Issues.Any(issue => issue.StartsWith("TERRAIN_MATERIAL_UNBOUND")),
    "Engine does not choose arbitrary terrain textures");


var landscapeWorldPlan = new LandscapeWorldPlanner().Generate(semantic.Package!,
    new[] { new LandscapeDesignGrammar("garden", "cozy-rows", "pink-flowers", 1, 1, 1) });
Assert(landscapeWorldPlan.IsValid && landscapeWorldPlan.Features.Count == 3,
    "Semantic world generates planned garden rows rather than scattered plants");
Assert(landscapeWorldPlan.Features.All(x => x.ZoneId == "garden" &&
    x.VisualRegionId == "square" && x.MaterialId == "pink-flowers"),
    "Garden detail retains approved visual region and material");
Assert(JsonSerializer.Serialize(landscapeWorldPlan) ==
    JsonSerializer.Serialize(new LandscapeWorldPlanner().Generate(semantic.Package!,
        new[] { new LandscapeDesignGrammar("garden", "cozy-rows", "pink-flowers", 1, 1, 1) })),
    "Landscape generation deterministic");
var missingGardenRules = new LandscapeWorldPlanner().Generate(semantic.Package!,
    Array.Empty<LandscapeDesignGrammar>());
Assert(!missingGardenRules.IsValid &&
    missingGardenRules.Diagnostics.Contains("GARDEN_GRAMMAR_MISSING:garden"),
    "Missing garden design refuses arbitrary placement");


var vegetationPlanner = new VegetationCompositionPlanner();
var designedOrchard = vegetationPlanner.Generate(semantic.Package!, new[]
{
    new VegetationGrammar("garden-shrubs", "garden", "approved-shrub",
        VegetationPattern.OrchardGrid, 3, 1, "ordered game-style planting")
});
Assert(designedOrchard.IsValid && designedOrchard.Plants.Count == 9,
    "Garden grid derives nine deliberate plant anchors");
Assert(designedOrchard.Plants.All(x => x.ZoneId == "garden" &&
    x.SpeciesAssetId == "approved-shrub" && x.GrammarId == "garden-shrubs"),
    "Plant positions retain approved species and source rule");
Assert(JsonSerializer.Serialize(designedOrchard) ==
    JsonSerializer.Serialize(vegetationPlanner.Generate(semantic.Package!, new[]
    {
        new VegetationGrammar("garden-shrubs", "garden", "approved-shrub",
            VegetationPattern.OrchardGrid, 3, 1, "ordered game-style planting")
    })), "Vegetation placement is repeatable");
Assert(vegetationPlanner.Generate(semantic.Package!, new[]
{
    new VegetationGrammar("absent", "unknown", "tree",
        VegetationPattern.BorderRow, 3, 1, "boundary")
}).Diagnostics.Contains("VEGETATION_ZONE_MISSING:absent"),
    "Unknown zones fail without random placement");


var hedgeGrammar = new PathVegetationGrammar("village-hedge", "walkway",
    new[] { "approved-shrub", "approved-flowering-shrub" }, 5, 0, 0,
    "alternating planted pedestrian-edge hedge");
var pathPlants = new PathVegetationComposer().Generate(semantic.Package!, new[] { hedgeGrammar });
Assert(pathPlants.IsValid && pathPlants.Plants.Count == 5,
    "Authored 20-meter walkway generates five deliberately spaced plants");
Assert(pathPlants.Plants[0].SpeciesAssetId == "approved-shrub" &&
    pathPlants.Plants[1].SpeciesAssetId == "approved-flowering-shrub" &&
    Math.Abs(pathPlants.Plants[^1].PositionMeters.X - 24) < 1e-8,
    "Path plants follow approved alternating species and reach intended endpoint");
Assert(JsonSerializer.Serialize(pathPlants) == JsonSerializer.Serialize(
    new PathVegetationComposer().Generate(semantic.Package!, new[] { hedgeGrammar })),
    "Mixed vegetation along paths is deterministic");
var missingHedge = new PathVegetationComposer().Generate(semantic.Package!, new[] {
    hedgeGrammar with { PathId = "unapproved-path" } });
Assert(!missingHedge.IsValid &&
    missingHedge.Diagnostics.Contains("PATH_VEGETATION_SOURCE_MISSING:village-hedge"),
    "Vegetation cannot be scattered onto unknown paths");


var clearance = new VegetationClearanceValidator();
var routeConflict = clearance.Validate(pathPlants.Plants,
    Array.Empty<VegetationClearanceAnchor>(),
    new[] { new VegetationClearanceRoute("walkway-reserved",
        new WorldPolyline(new[] { new WorldPoint(4, 4), new WorldPoint(24, 4) }), 2) }, 0.5);
Assert(!routeConflict.IsValid &&
    routeConflict.Conflicts.Any(c => c.StartsWith("VEGETATION_ROUTE_CONFLICT:")),
    "Plants on a protected walkway are rejected without relocation");
var landmarkConflict = clearance.Validate(new[] { designedOrchard.Plants[0] },
    new[] { new VegetationClearanceAnchor("reserved-tree-spot",
        designedOrchard.Plants[0].PositionMeters, 1) },
    Array.Empty<VegetationClearanceRoute>(), 0.5);
Assert(!landmarkConflict.IsValid &&
    landmarkConflict.Conflicts.Single().Contains("reserved-tree-spot"),
    "Planned vegetation cannot overlap reserved landmarks");
var safePlants = clearance.Validate(designedOrchard.Plants,
    new[] { new VegetationClearanceAnchor("outside", new WorldPoint(100, 100), 1) },
    Array.Empty<VegetationClearanceRoute>(), 0.5);
Assert(safePlants.IsValid, "Planned vegetation clear of reservations remains valid");


var vegetationGate = new VegetationWorldAcceptanceGate();
var reservedBuilding = new VegetationProtectedFootprint("west-house",
    new WorldPolygon(new[] {
        new WorldPoint(8,8), new WorldPoint(11,8),
        new WorldPoint(11,11), new WorldPoint(8,11)
    }));
var blockedWorld = vegetationGate.Accept(designedOrchard, new PathVegetationResult(
    Array.Empty<PlannedVegetation>(), Array.Empty<string>()),
    new[] { reservedBuilding }, Array.Empty<VegetationClearanceAnchor>(),
    Array.Empty<VegetationClearanceRoute>(), 0.5);
Assert(!blockedWorld.Accepted && blockedWorld.Plants.Count == 0 &&
    blockedWorld.Conflicts.Any(c => c.StartsWith("VEGETATION_FOOTPRINT_CONFLICT:")),
    "World acceptance refuses planted vegetation in building footprint");
var safeWorld = vegetationGate.Accept(designedOrchard, new PathVegetationResult(
    Array.Empty<PlannedVegetation>(), Array.Empty<string>()),
    new[] { new VegetationProtectedFootprint("distant-house",
        new WorldPolygon(new[] { new WorldPoint(40,40), new WorldPoint(44,40),
            new WorldPoint(44,44), new WorldPoint(40,44) })) },
    Array.Empty<VegetationClearanceAnchor>(),
    Array.Empty<VegetationClearanceRoute>(), 0.5);
Assert(safeWorld.Accepted && safeWorld.Plants.Count == designedOrchard.Plants.Count,
    "Clear plant composition passes world acceptance without changing positions");


var semanticAcceptance = new SemanticWorldAcceptanceGate();
var emptyPathPlants = new PathVegetationResult(
    Array.Empty<PlannedVegetation>(), Array.Empty<string>());
var approvedBuildings = new Dictionary<string, BuildingEnvelopeResult> {
    ["warehouse"] = new BuildingEnvelopeResult(new WorldPolygon(new[] {
        new WorldPoint(40,40), new WorldPoint(43,40),
        new WorldPoint(43,43), new WorldPoint(40,43)
    }), Array.Empty<string>())
};
var acceptedComposition = semanticAcceptance.Accept(semantic.Package!,
    designedOrchard, emptyPathPlants, approvedBuildings,
    Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(), 0.4);
Assert(acceptedComposition.Accepted &&
    acceptedComposition.World!.Vegetation.Count == designedOrchard.Plants.Count,
    "Semantic world acceptance retains clear planted layout");
var overlappingPlants = new VegetationComposition(new[] {
    designedOrchard.Plants[0],
    designedOrchard.Plants[0] with { Id = "overlapping-plant" }
}, Array.Empty<string>());
var rejectedComposition = semanticAcceptance.Accept(semantic.Package!,
    overlappingPlants, emptyPathPlants, approvedBuildings,
    Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(), 0.4);
Assert(!rejectedComposition.Accepted && rejectedComposition.World is null &&
    rejectedComposition.Issues.Any(e => e.StartsWith("VEGETATION_PAIR_CONFLICT:")),
    "Semantic world rejects vegetation-to-vegetation collisions");
var invalidBuildingComposition = semanticAcceptance.Accept(semantic.Package!,
    designedOrchard, emptyPathPlants,
    new Dictionary<string, BuildingEnvelopeResult> {
        ["unfinished"] = new BuildingEnvelopeResult(null, new[] { "NO_ENVELOPE" })
    },
    Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(), 0.4);
Assert(!invalidBuildingComposition.Accepted &&
    invalidBuildingComposition.Issues.Contains("BUILDING_ENVELOPE_NOT_READY:unfinished"),
    "Unbuilt building envelope prevents world acceptance");


var speciesGate = new SpeciesAwareSemanticWorldGate();
var speciesProfiles = new[] { new VegetationAssetClearance("approved-shrub", 0.3),
    new VegetationAssetClearance("approved-flowering-shrub", 0.8) };
var speciesPlants = new VegetationComposition(new[] {
    designedOrchard.Plants[0],
    designedOrchard.Plants[0] with { Id = "neighbor", SpeciesAssetId = "approved-flowering-shrub",
        PositionMeters = designedOrchard.Plants[0].PositionMeters.Offset(1, 0) }
}, Array.Empty<string>());
var speciesResult = speciesGate.Accept(semantic.Package!, speciesPlants, emptyPathPlants,
    approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(), speciesProfiles);
Assert(!speciesResult.Accepted && speciesResult.Issues.Any(x =>
    x.StartsWith("VEGETATION_PAIR_CONFLICT:")),
    "Species-aware gate detects overlapping unequal vegetation radii");
var missingSpecies = speciesGate.Accept(semantic.Package!, speciesPlants, emptyPathPlants,
    approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(),
    new[] { new VegetationAssetClearance("approved-shrub", 0.3) });
Assert(!missingSpecies.Accepted && missingSpecies.Issues.Contains(
    "VEGETATION_ASSET_DIMENSIONS_MISSING:neighbor"),
    "Unknown species dimensions reject world without guessing physical size");
var speciesSafe = speciesGate.Accept(semantic.Package!, designedOrchard, emptyPathPlants,
    approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(), speciesProfiles);
Assert(speciesSafe.Accepted &&
    speciesSafe.World!.Vegetation.Count == designedOrchard.Plants.Count,
    "Species-aware gate accepts clear deliberate vegetation arrangement");


var footprintResolver = new GameArtsVegetationProfileResolver(new InMemoryRegistry(full));
var resolvedFootprints = await footprintResolver.ResolveAsync(new[] { "warehouse" },
    new[] { new GameArtsVegetationFootprint("warehouse", full.GameArtsRevision, 0.75) });
Assert(resolvedFootprints.Valid && resolvedFootprints.Profiles.Single().RadiusMeters == 0.75,
    "Approved GameArts revision resolves explicit meter-scale vegetation clearance");
var obsoleteFootprints = await footprintResolver.ResolveAsync(new[] { "warehouse" },
    new[] { new GameArtsVegetationFootprint("warehouse", "obsolete-revision", 0.75) });
Assert(!obsoleteFootprints.Valid && obsoleteFootprints.Issues.Contains(
    "GAMEARTS_FOOTPRINT_STALE:warehouse"),
    "Outdated GameArts physical footprint is rejected");
var unapprovedFootprints = await new GameArtsVegetationProfileResolver(
    new InMemoryRegistry(null)).ResolveAsync(new[] { "warehouse" },
    new[] { new GameArtsVegetationFootprint("warehouse", full.GameArtsRevision, 0.75) });
Assert(!unapprovedFootprints.Valid && unapprovedFootprints.Issues.Contains(
    "GAMEARTS_ASSET_NOT_APPROVED:warehouse"),
    "Unapproved art cannot supply vegetation footprint sizes");


var endToEnd = new GameArtsSemanticWorldPipeline(new InMemoryRegistry(full));
var worldWithArt = await endToEnd.AcceptAsync(semantic.Package!,
    new VegetationComposition(new[] {
        designedOrchard.Plants[0] with { SpeciesAssetId = "warehouse" }
    }, Array.Empty<string>()), emptyPathPlants, approvedBuildings,
    Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(),
    new[] { new GameArtsVegetationFootprint("warehouse", full.GameArtsRevision, 0.5) });
Assert(worldWithArt.Accepted && worldWithArt.World!.Vegetation.Count == 1,
    "GameArts pipeline accepts approved footprint with safe semantic placement");
var pipelineStale = await endToEnd.AcceptAsync(semantic.Package!,
    new VegetationComposition(new[] {
        designedOrchard.Plants[0] with { SpeciesAssetId = "warehouse" }
    }, Array.Empty<string>()), emptyPathPlants, approvedBuildings,
    Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(),
    new[] { new GameArtsVegetationFootprint("warehouse", "outdated", 0.5) });
Assert(!pipelineStale.Accepted && pipelineStale.Issues.Contains(
    "GAMEARTS_FOOTPRINT_STALE:warehouse"),
    "End-to-end acceptance rejects mismatched GameArts footprint revision");
var pipelineBlocked = await endToEnd.AcceptAsync(semantic.Package!,
    new VegetationComposition(new[] {
        designedOrchard.Plants[0] with { SpeciesAssetId = "warehouse" }
    }, Array.Empty<string>()), emptyPathPlants,
    new Dictionary<string, BuildingEnvelopeResult> {
        ["occupied"] = new BuildingEnvelopeResult(new WorldPolygon(new[] {
            new WorldPoint(8,8), new WorldPoint(11,8),
            new WorldPoint(11,11), new WorldPoint(8,11)
        }), Array.Empty<string>())
    }, Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(),
    new[] { new GameArtsVegetationFootprint("warehouse", full.GameArtsRevision, 0.5) });
Assert(!pipelineBlocked.Accepted && pipelineBlocked.Issues.Any(x =>
    x.StartsWith("VEGETATION_FOOTPRINT_CONFLICT:")),
    "End-to-end acceptance rejects building conflicts despite approved art");


var verifiedArtDir = Path.Combine(Path.GetTempPath(), "gfw-verified-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(verifiedArtDir);
try
{
    byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");
    var verifiedPng = Path.Combine(verifiedArtDir, "test.png");
    var verifiedAtlas = Path.Combine(verifiedArtDir, "test.json");
    await File.WriteAllBytesAsync(verifiedPng, png);
    await File.WriteAllTextAsync(verifiedAtlas, "{\"schema_version\":\"1.0\",\"image\":\"test.png\",\"mask_color\":\"#FF00FF\",\"background_alpha\":255,\"image_size\":[1,1],\"frames\":[{\"rect\":[0,0,1,1],\"pivot\":[0,0],\"anchors\":{}}]}");
    string VerifiedHash(byte[] data) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
    var verifiedSheet = new SpriteSheetManifest("verified", "test.png", "test.json",
        VerifiedHash(png), VerifiedHash(await File.ReadAllBytesAsync(verifiedAtlas)),
        "plant", new[] { "S" });
    var verifiedManifest = full with { Sheets = new[] { verifiedSheet } };
    var verifiedPipeline = new GameArtsSemanticWorldPipeline(new InMemoryRegistry(verifiedManifest));
    var singleSpecies = new VegetationComposition(new[] {
        designedOrchard.Plants[0] with { SpeciesAssetId = "warehouse" }
    }, Array.Empty<string>());
    var measured = new[] { new GameArtsVegetationFootprint("warehouse", full.GameArtsRevision, 0.5) };
    var ready = await verifiedPipeline.AcceptVerifiedAsync(semantic.Package!, singleSpecies,
        emptyPathPlants, approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
        Array.Empty<VegetationClearanceAnchor>(), measured, verifiedArtDir);
    Assert(ready.Accepted, "Verified production PNG and atlas hashes accept intended world");

    var nativeFixtureBundle = Path.Combine(verifiedArtDir, "warehouse.bundle.json");
    await File.WriteAllTextAsync(nativeFixtureBundle, """
    {
      "schema_version":"1.0", "asset_id":"warehouse", "directions":["S"],
      "components":[{"id":"plant","parent":null,"atlases":{"S":"test.json"}}],
      "sampled_poses":[{"direction":"S","normalized_time":0,
        "nodes":{"plant":{"frame_index":0,"world_matrix_2d":[[1,0,0],[0,1,0],[0,0,1]]}}}]
    }
    """);
    var completeFixture = new ProductionWorldRequest(semantic.Package!, singleSpecies,
        emptyPathPlants, approvedBuildings,
        Array.Empty<VegetationClearanceRoute>(),
        Array.Empty<VegetationClearanceAnchor>(), measured, verifiedArtDir,
        new[] { new GameArtsWorldBundleReference("warehouse",
            "warehouse.bundle.json", full.GameArtsRevision) });
    var fixtureExportPath = Path.Combine(verifiedArtDir, "assembled-world.json");
    var fixtureBuilder = new ProductionWorldBuilder(
        new InMemoryRegistry(verifiedManifest), new TestApprovalAuthority(true));
    var fixtureOutput = await fixtureBuilder.BuildAsync(completeFixture, fixtureExportPath);
    Assert(fixtureOutput.Produced && File.Exists(fixtureExportPath),
        "Complete simulated-approved GameArts fixture exports semantic world");
    using (var exportedFixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixtureExportPath)))
    {
        Assert(exportedFixture.RootElement.GetProperty("Vegetation").GetArrayLength() == 1,
            "Production JSON contains the approved planted object");
        Assert(exportedFixture.RootElement.GetProperty("World").GetProperty("Base")
            .GetProperty("MockupId").GetString() == "town-mockup",
            "Production JSON preserves approved mockup provenance");
    }


    var productionGate = new GameArtsProductionWorldGate(
        new InMemoryRegistry(verifiedManifest), new TestApprovalAuthority(true));
    var missingBundle = await productionGate.AcceptAsync(semantic.Package!, singleSpecies,
        emptyPathPlants, approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
        Array.Empty<VegetationClearanceAnchor>(), measured, verifiedArtDir,
        Array.Empty<GameArtsWorldBundleReference>());
    Assert(!missingBundle.Accepted && missingBundle.Issues.Contains(
        "GAMEARTS_BUNDLE_REFERENCE_MISSING:warehouse"),
        "Production world cannot pass using verified sheet hashes without a native bundle");
    var traversalBundle = await productionGate.AcceptAsync(semantic.Package!, singleSpecies,
        emptyPathPlants, approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
        Array.Empty<VegetationClearanceAnchor>(), measured, verifiedArtDir,
        new[] { new GameArtsWorldBundleReference("warehouse", "../outside.bundle.json", full.GameArtsRevision) });
    Assert(!traversalBundle.Accepted && traversalBundle.Issues.Contains(
        "GAMEARTS_BUNDLE_PATH_INVALID:warehouse"),
        "Production bundle must remain inside trusted art root");

    await File.WriteAllTextAsync(verifiedAtlas, "{\"frames\":[0]}");
    var tampered = await verifiedPipeline.AcceptVerifiedAsync(semantic.Package!, singleSpecies,
        emptyPathPlants, approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
        Array.Empty<VegetationClearanceAnchor>(), measured, verifiedArtDir);
    Assert(!tampered.Accepted && tampered.Issues.Any(x => x.StartsWith("GAMEARTS_FILE_ASSET_HASH_MISMATCH:")),
        "Tampered production atlas rejects previously accepted world");
}
finally { Directory.Delete(verifiedArtDir, recursive: true); }


var productionOutput = Path.Combine(Path.GetTempPath(),
    "gfw-production-" + Guid.NewGuid().ToString("N") + ".json");
try
{
    var productionRequest = new ProductionWorldRequest(semantic.Package!,
        new VegetationComposition(new[] {
            designedOrchard.Plants[0] with { SpeciesAssetId = "warehouse" }
        }, Array.Empty<string>()),
        emptyPathPlants, approvedBuildings,
        Array.Empty<VegetationClearanceRoute>(),
        Array.Empty<VegetationClearanceAnchor>(),
        new[] { new GameArtsVegetationFootprint("warehouse", full.GameArtsRevision, 0.5) },
        Path.GetTempPath(), Array.Empty<GameArtsWorldBundleReference>());
    var productionBuilder = new ProductionWorldBuilder(
        new InMemoryRegistry(full), new TestApprovalAuthority(true));
    var rejectedExport = await productionBuilder.BuildAsync(productionRequest, productionOutput);
    Assert(!rejectedExport.Produced && rejectedExport.OutputPath is null &&
        !File.Exists(productionOutput),
        "Standard production workflow never exports when GameArts files are missing");
    var blockedExport = await productionBuilder.BuildAsync(productionRequest with {
        ZoneVegetation = new VegetationComposition(Array.Empty<PlannedVegetation>(),
            new[] { "UPSTREAM_COMPOSITION_FAILED" })
    }, productionOutput);
    Assert(!blockedExport.Produced && blockedExport.Issues.Contains(
        "UPSTREAM_COMPOSITION_FAILED") && !File.Exists(productionOutput),
        "Production workflow refuses unresolved composition diagnostics");
}
finally { if (File.Exists(productionOutput)) File.Delete(productionOutput); }


var villageFixture = SemanticWorldFixture.Create();
Assert(villageFixture.Base.Objects.Count == 3 && villageFixture.Terrain.Count == 3 &&
    villageFixture.Paths.Count == 2 && villageFixture.Landscape.Count == 2,
    "Reusable C# village has houses, sign, planned ground, paths and garden zones");
Assert(villageFixture.Base.MockupId == "fixture-village-mockup" &&
    villageFixture.Base.ArtStyleId == "cozy-pixel-village",
    "Village design preserves its simulated visual mockup and art style");
Assert(JsonSerializer.Serialize(villageFixture) ==
    JsonSerializer.Serialize(SemanticWorldFixture.Create()),
    "Village world specification is byte-identical across repeated builds");
var villageGarden = new LandscapeWorldPlanner().Generate(villageFixture, new[] {
    new LandscapeDesignGrammar("western-garden", "rows", "fixture-flowers", 1, 2, 1),
    new LandscapeDesignGrammar("eastern-garden", "rows", "fixture-orchard-ground", 1, 2, 1)
});
Assert(villageGarden.IsValid && villageGarden.Features.Count > 0 &&
    villageGarden.Features.All(x => x.VisualRegionId is "west-garden" or "east-garden"),
    "Garden geometries are derived from approved fixture regions");


var completeVillage = SemanticWorldAssemblyFixture.Create();
Assert(completeVillage.Vegetation.Count > 0 &&
    completeVillage.BuildingFootprints.Count == 2 &&
    completeVillage.GroundMaterials.Pieces.Count > 0 &&
    completeVillage.GardenBeds.Features.Count > 0,
    "Inspectable village assembly includes placed vegetation, buildings and styled terrain");
Assert(completeVillage.Vegetation.All(p =>
    p.SpeciesAssetId is "fixture-flower" or "fixture-tree"),
    "Every planned plant carries a declared art identity");
Assert(JsonSerializer.Serialize(completeVillage) ==
    JsonSerializer.Serialize(SemanticWorldAssemblyFixture.Create()),
    "Complete semantic world assembly reproducible from fixed design");


var villageDocument = WorldSceneFixtureExporter.Assemble();
Assert(villageDocument.ArtifactKind == "simulation-fixture" &&
    !villageDocument.ProductionApproved &&
    villageDocument.Buildings.Select(b => b.ObjectId)
        .SequenceEqual(new[] { "house-west", "house-east" }),
    "Portable world identifies fixture provenance and named building footprints");
Assert(villageDocument.TerrainMaterials.Count > 0 &&
    villageDocument.GardenBeds.Count > 0 && villageDocument.Vegetation.Count > 0,
    "Portable world includes terrain, planting beds and vegetation in one document");
Assert(JsonSerializer.Serialize(villageDocument) ==
    JsonSerializer.Serialize(WorldSceneFixtureExporter.Assemble()),
    "Portable dummy village JSON stable across independent assemblies");
var exportedVillagePath = Path.Combine(Path.GetTempPath(),
    "gfw-village-" + Guid.NewGuid().ToString("N") + ".json");
try
{
    await WorldSceneFixtureExporter.ExportAsync(exportedVillagePath);
    using var exportedVillage = JsonDocument.Parse(await File.ReadAllTextAsync(exportedVillagePath));
    Assert(exportedVillage.RootElement.GetProperty("ProductionApproved").GetBoolean() == false &&
        exportedVillage.RootElement.GetProperty("Buildings").GetArrayLength() == 2 &&
        exportedVillage.RootElement.GetProperty("SemanticWorld").GetProperty("Paths")
            .GetArrayLength() == 2,
        "Portable export writes inspectable complete fixture with explicit nonproduction state");
}
finally { if (File.Exists(exportedVillagePath)) File.Delete(exportedVillagePath); }


var worldQuality = new WorldQualityAnalyzer();
var qualityRules = new WorldQualityRules(0.99, 21, 1, "village-ground");
var goodQuality = worldQuality.Analyze(villageDocument, qualityRules);
Assert(goodQuality.MeetsRules &&
    Math.Abs(goodQuality.NominalTerrainCoverageRatio - 1) < 1e-8 &&
    goodQuality.ConnectedPathComponents == 1,
    "Dummy village preserves ground fill and connected crossing network: " + string.Join(",", goodQuality.Issues) + " ratio=" + goodQuality.NominalTerrainCoverageRatio + " components=" + goodQuality.ConnectedPathComponents);
var disconnectedVillage = villageDocument with {
    SemanticWorld = villageDocument.SemanticWorld with {
        Paths = new[] {
            villageDocument.SemanticWorld.Paths[0],
            villageDocument.SemanticWorld.Paths[1] with {
                Geometry = new WorldPolyline(new[] {
                    new WorldPoint(2, 2), new WorldPoint(5, 2) })
            }
        }
    }
};
Assert(worldQuality.Analyze(disconnectedVillage, qualityRules).Issues
    .Contains("PATH_NETWORK_DISCONNECTED"),
    "Quality audit detects a disconnected designed crossing");
var inaccessibleQuality = worldQuality.Analyze(villageDocument,
    qualityRules with { MaximumBuildingPathDistanceMeters = 1 });
Assert(inaccessibleQuality.Issues.Any(x =>
    x.StartsWith("BUILDING_PATH_ACCESS_TOO_FAR:")),
    "Quality audit detects insufficient building route access");
var unplantedQuality = worldQuality.Analyze(villageDocument with {
    Vegetation = Array.Empty<PlannedVegetation>()
}, qualityRules);
Assert(unplantedQuality.Issues.Contains("PLANTING_DENSITY_INSUFFICIENT"),
    "Quality audit detects missing planned vegetation");


var repeatedFill = villageDocument with {
    TerrainMaterials = villageDocument.TerrainMaterials.Concat(
        new[] { villageDocument.TerrainMaterials.First(p => p.SourceRegionId == "ground") with { Id = "duplicate-ground-fill" } })
        .ToArray()
};
var overlapQuality = worldQuality.Analyze(repeatedFill, qualityRules);
Assert(!overlapQuality.MeetsRules &&
    overlapQuality.Issues.Any(i => i.StartsWith("TERRAIN_FILL_OVERLAP:")) &&
    overlapQuality.NominalTerrainCoverageRatio == 0,
    "Repeated terrain geometry cannot falsely satisfy ground coverage");
var spillFill = villageDocument with {
    TerrainMaterials = villageDocument.TerrainMaterials.Concat(new[] {
        villageDocument.TerrainMaterials.First(p => p.SourceRegionId == "ground") with {
            Id = "out-of-ground",
            Geometry = new WorldPolygon(new[] {
                new WorldPoint(63, 46), new WorldPoint(67, 46),
                new WorldPoint(67, 50), new WorldPoint(63, 50)
            })
        }
    }).ToArray()
};
Assert(worldQuality.Analyze(spillFill, qualityRules).Issues.Any(i =>
    i == "TERRAIN_FILL_OUTSIDE_GROUND:out-of-ground"),
    "Ground coverage rejects terrain spills beyond the approved map boundary");


var entranceValidator = new BuildingEntranceAccessValidator();
var villageEntrances = new[] {
    new BuildingEntranceLink("house-west", new WorldPoint(17, 20),
        new WorldPolyline(new[] { new WorldPoint(17,20), new WorldPoint(17,28),
            new WorldPoint(24,28) }), "crosswalk"),
    new BuildingEntranceLink("house-east", new WorldPoint(43, 20),
        new WorldPolyline(new[] { new WorldPoint(43,20), new WorldPoint(43,28),
            new WorldPoint(40,28) }), "crosswalk")
};
Assert(entranceValidator.Validate(villageDocument, villageEntrances).Accessible,
    "Authored village entrances connect by planned paths to pedestrian crossing");
var detachedEntrance = villageEntrances[0] with {
    AccessWalkway = new WorldPolyline(new[] { new WorldPoint(18,20),
        new WorldPoint(24,28) })
};
Assert(entranceValidator.Validate(villageDocument,
    new[] { detachedEntrance, villageEntrances[1] }).Issues
        .Contains("ENTRANCE_WALKWAY_NOT_ATTACHED:house-west"),
    "Entrance access rejects detached pedestrian connector");
var missingEntrance = entranceValidator.Validate(villageDocument,
    new[] { villageEntrances[0] });
Assert(missingEntrance.Issues.Contains("ENTRANCE_MISSING:house-east"),
    "Each building needs a declared entrance rather than center-point proximity");


Assert(villageDocument.Entrances is { Count: 2 } &&
    worldQuality.Analyze(villageDocument, qualityRules).MeetsRules,
    "Exported village entrances satisfy integrated world quality");
var brokenAccessWorld = villageDocument with {
    Entrances = villageDocument.Entrances!.Select(e => e.BuildingId == "house-west"
        ? e with { TargetPathId = "missing-path" } : e).ToArray()
};
Assert(worldQuality.Analyze(brokenAccessWorld, qualityRules).Issues
    .Contains("ENTRANCE_PATH_NOT_CONNECTED:house-west"),
    "Integrated quality report rejects a disconnected authored entrance");


var crossingBuilding = villageDocument with {
    Entrances = new[] {
        villageEntrances[0] with {
            AccessWalkway = new WorldPolyline(new[] {
                new WorldPoint(17,20), new WorldPoint(48,15), new WorldPoint(24,28)
            })
        }, villageEntrances[1]
    }
};
Assert(new PedestrianAccessNetworkValidator().Validate(crossingBuilding).Any(x =>
    x.StartsWith("ENTRANCE_WALKWAY_CROSSES_BUILDING:house-west:house-east")),
    "Deliberate pedestrian link cannot cross another building");
var roadOnlyEntrance = villageDocument with {
    Entrances = new[] { villageEntrances[0] with { TargetPathId = "main-road" },
        villageEntrances[1] }
};
Assert(new PedestrianAccessNetworkValidator().Validate(roadOnlyEntrance)
    .Contains("ENTRANCE_TARGET_NOT_PEDESTRIAN:house-west"),
    "Pedestrian entrance cannot treat a driving road as a footpath");


var splitPedestrianWorld = villageDocument with {
    SemanticWorld = villageDocument.SemanticWorld with {
        Paths = villageDocument.SemanticWorld.Paths.Concat(new[] {
            new PlannedWorldPath("isolated-walk", "footpath",
                new WorldPolyline(new[] { new WorldPoint(40, 44), new WorldPoint(58, 44) }),
                "fixture-isolated-route", "fixture-reachability")
        }).ToArray()
    },
    Entrances = villageDocument.Entrances!.Select(e => e.BuildingId == "house-east"
        ? e with { TargetPathId = "isolated-walk",
            AccessWalkway = new WorldPolyline(new[] {
                new WorldPoint(43, 20), new WorldPoint(43, 44) }) }
        : e).ToArray()
};
Assert(new PedestrianReachabilityValidator().Validate(splitPedestrianWorld)
    .Contains("PEDESTRIAN_ENTRANCES_DISCONNECTED"),
    "Doors attached to separate pedestrian networks cannot reach each other");
Assert(new PedestrianReachabilityValidator().Validate(villageDocument).Count == 0,
    "Fixture village entrances share one connected pedestrian network");


var controlledWorld = villageDocument with {
    SemanticWorld = villageDocument.SemanticWorld with {
        Paths = villageDocument.SemanticWorld.Paths.Concat(new[] {
            new PlannedWorldPath("branch-footpath", "footpath",
                new WorldPolyline(new[] { new WorldPoint(32,20), new WorldPoint(32,36) }),
                "approved-branch", "fixture-branch")
        }).ToArray()
    },
    Entrances = villageDocument.Entrances!.Select(e =>
        e.BuildingId == "house-east" ? e with {
            TargetPathId = "branch-footpath",
            AccessWalkway = new WorldPolyline(new[] {
                new WorldPoint(43,20), new WorldPoint(32,20) })
        } : e).ToArray()
};
var crossingControl = new ControlledPedestrianReachability();
Assert(crossingControl.Validate(controlledWorld, Array.Empty<PedestrianJunction>())
    .Contains("PEDESTRIAN_ENTRANCES_DISCONNECTED"),
    "Intersecting pedestrian routes require an explicit authorized junction");
var approvedCrossing = new PedestrianJunction("branch-crossing", "crosswalk",
    "branch-footpath", new WorldPoint(32,28), true);
Assert(crossingControl.Validate(controlledWorld, new[] { approvedCrossing }).Count == 0,
    "Authorized pedestrian junction connects building entrance networks");
Assert(crossingControl.Validate(controlledWorld,
    new[] { approvedCrossing with { Traversable = false } })
    .Contains("PEDESTRIAN_ENTRANCES_DISCONNECTED"),
    "Blocked junction cannot connect independent pedestrian networks");


var permissionQuality = worldQuality.Analyze(controlledWorld with {
    Junctions = Array.Empty<PedestrianJunction>()
}, qualityRules);
Assert(permissionQuality.Issues.Contains("PEDESTRIAN_ENTRANCES_DISCONNECTED"),
    "Quality audit rejects graph crossing without explicit permission");
var authorizedQuality = worldQuality.Analyze(controlledWorld with {
    Junctions = new[] { approvedCrossing }
}, qualityRules);
Assert(!authorizedQuality.Issues.Contains("PEDESTRIAN_ENTRANCES_DISCONNECTED"),
    "Quality audit accepts authorized pedestrian junction");
var blockedQuality = worldQuality.Analyze(controlledWorld with {
    Junctions = new[] { approvedCrossing with { Traversable = false } }
}, qualityRules);
Assert(blockedQuality.Issues.Contains("PEDESTRIAN_ENTRANCES_DISCONNECTED"),
    "Quality audit honors explicit blocked crossing");


var crossingValidator = new PedestrianRoadCrossingValidator();
Assert(crossingValidator.Validate(villageDocument, villageDocument.RoadCrossings!).Count == 0,
    "Village road crossing is explicitly permitted and accessible");
var noCrossing = villageDocument with {
    RoadCrossings = Array.Empty<PedestrianRoadCrossing>()
};
Assert(worldQuality.Analyze(noCrossing, qualityRules).Issues
    .Contains("ROAD_CROSSING_AUTHORIZATION_MISSING:crosswalk:main-road"),
    "World quality rejects pedestrian crossing without authorization");
var blockedCrossing = villageDocument with {
    RoadCrossings = villageDocument.RoadCrossings!.Select(c =>
        c with { Accessible = false }).ToArray()
};
Assert(worldQuality.Analyze(blockedCrossing, qualityRules).Issues.Any(i =>
    i.StartsWith("ROAD_CROSSING_INACCESSIBLE:")),
    "World quality rejects inaccessible road crossings");


var spanValidator = new PedestrianCrossingSpanValidator();
Assert(spanValidator.Validate(villageDocument, villageDocument.CrossingSpans!).Count == 0,
    "Designed village crosswalk spans its four-meter road perpendicularly");
var narrowCrossing = villageDocument with {
    CrossingSpans = new[] { villageDocument.CrossingSpans![0] with { RoadWidthMeters = 5 } }
};
Assert(worldQuality.Analyze(narrowCrossing, qualityRules).Issues.Contains(
    "CROSSING_CURB_SPAN_MISMATCH:village-zebra"),
    "Quality report rejects inconsistent crosswalk width");
var gradeSeparatedCrossing = villageDocument with {
    CrossingSpans = new[] { villageDocument.CrossingSpans![0] with { GradeSeparated = true } }
};
Assert(worldQuality.Analyze(gradeSeparatedCrossing, qualityRules).Issues.Contains(
    "CROSSING_GRADE_SEPARATED:village-zebra"),
    "Grade-separated routes cannot silently act as pedestrian crossings");


var profileValidator = new CrossingRoadProfileValidator();
Assert(profileValidator.Validate(villageDocument).Count == 0,
    "Village crosswalk derives four-meter curb span from two lanes and two curbs");
var incorrectProfile = villageDocument with {
    RoadProfiles = new[] { villageDocument.RoadProfiles![0] with {
        Section = villageDocument.RoadProfiles[0].Section with { LaneWidthMeters = 2.2 }
    } }
};
Assert(worldQuality.Analyze(incorrectProfile, qualityRules).Issues.Contains(
    "CROSSING_ROAD_PROFILE_WIDTH_MISMATCH:village-zebra"),
    "Road-width changes invalidate an obsolete crossing span");
var noSidewalk = villageDocument with {
    RoadProfiles = new[] { villageDocument.RoadProfiles![0] with {
        Section = villageDocument.RoadProfiles[0].Section with { SidewalkRightMeters = 0 }
    } }
};
Assert(worldQuality.Analyze(noSidewalk, qualityRules).Issues.Contains(
    "CROSSING_SIDEWALK_CONNECTION_MISSING:village-zebra"),
    "Crossings require sidewalk approaches on both sides");


var curbValidator = new CrossingApproachValidator();
Assert(curbValidator.Validate(villageDocument, villageDocument.CrossingApproaches!, 0.02).Count == 0,
    "Village crossing has continuous sidewalk approaches and accessible curb transitions");
var raisedCurbVillage = villageDocument with {
    CrossingApproaches = new[] { villageDocument.CrossingApproaches![0] with {
        LeftCurbRiseMeters = 0.15
    } }
};
Assert(worldQuality.Analyze(raisedCurbVillage, qualityRules).Issues.Contains(
    "CROSSING_CURB_TRANSITION_INACCESSIBLE:village-zebra"),
    "Quality gate rejects curb transitions exceeding the configured clearance");
var detachedSidewalkVillage = villageDocument with {
    CrossingApproaches = new[] { villageDocument.CrossingApproaches![0] with {
        RightSidewalkPoint = new WorldPoint(50, 28)
    } }
};
Assert(worldQuality.Analyze(detachedSidewalkVillage, qualityRules).Issues.Contains(
    "CROSSING_SIDEWALK_ENDPOINT_MISMATCH:village-zebra"),
    "Quality gate detects a sidewalk connection that does not meet the crossing");


var settlementCrossings = new SettlementCrossingPlanner().Plan(
    villageDocument.SemanticWorld, villageDocument.RoadProfiles!,
    new[] { new SettlementCrossingIntent("generated-zebra", "crosswalk", "main-road",
        new WorldPoint(32,28), true, true, 0, 0) });
Assert(settlementCrossings.Valid && settlementCrossings.Crossings.Count == 1 &&
    Math.Abs(settlementCrossings.Crossings[0].Span.RoadWidthMeters - 4) < 1e-8,
    "Settlement planner derives full curb span from a named road profile");
var plannedCrossingWorld = villageDocument with {
    RoadCrossings = settlementCrossings.Crossings.Select(x => x.Crossing).ToArray(),
    CrossingSpans = settlementCrossings.Crossings.Select(x => x.Span).ToArray(),
    CrossingApproaches = settlementCrossings.Crossings.Select(x => x.Approaches).ToArray()
};
Assert(new PedestrianCrossingSpanValidator().Validate(
    plannedCrossingWorld, plannedCrossingWorld.CrossingSpans!).Count == 0 &&
    new CrossingApproachValidator().Validate(
        plannedCrossingWorld, plannedCrossingWorld.CrossingApproaches!, 0.02).Count == 0,
    "Generated road crossing passes existing span and curb validators");
var unknownCrossing = new SettlementCrossingPlanner().Plan(
    villageDocument.SemanticWorld, villageDocument.RoadProfiles!,
    new[] { new SettlementCrossingIntent("missing", "unknown-footpath", "main-road",
        new WorldPoint(32,28), true, true, 0, 0) });
Assert(!unknownCrossing.Valid && unknownCrossing.Crossings.Count == 0,
    "Settlement planner does not invent missing pedestrian paths");


var villageRoad = villageDocument.SemanticWorld.Paths.Single(p => p.Id == "main-road");
var alignedSettlement = integrated with {
    Streets = new[] { new StreetCorridor("main-road", villageRoad.Geometry,
        villageDocument.RoadProfiles![0].Section) },
    Diagnostics = Array.Empty<string>()
};
var settlementIntegrator = new SettlementCrossingIntegration();
var authoredSettlementCrossing = settlementIntegrator.Build(
    villageDocument.SemanticWorld, alignedSettlement, new[] {
        new SettlementCrossingIntent("settlement-zebra", "crosswalk", "main-road",
            new WorldPoint(32, 28), true, true, 0, 0)
    });
Assert(authoredSettlementCrossing.Valid && authoredSettlementCrossing.Crossings.Count == 1,
    "Settlement street corridors feed approved crossing geometry");
var mismatchedSettlement = settlementIntegrator.Build(villageDocument.SemanticWorld,
    alignedSettlement with {
        Streets = new[] { new StreetCorridor("main-road",
            new WorldPolyline(new[] { new WorldPoint(30, 0), new WorldPoint(30, 48) }),
            villageDocument.RoadProfiles![0].Section) }
    }, new[] { new SettlementCrossingIntent("settlement-zebra", "crosswalk", "main-road",
        new WorldPoint(32, 28), true, true, 0, 0) });
Assert(!mismatchedSettlement.Valid && mismatchedSettlement.Issues.Contains(
    "STREET_SEMANTIC_GEOMETRY_MISMATCH:main-road"),
    "Settlement crossing integration refuses mismatched road geometry");


var roadCoordinator = new SettlementTransportCrossingCoordinator();
var policyIntent = new SettlementCrossingIntent("policy-zebra", "crosswalk", "main-road",
    new WorldPoint(32,28), true, true, 0, 0);
var allowPolicy = new SettlementRoadPolicy("main-road", SettlementRoadClass.Collector,
    true, true);
var policyAccepted = roadCoordinator.Build(villageDocument.SemanticWorld, alignedSettlement,
    new[] { policyIntent }, new[] { allowPolicy });
Assert(policyAccepted.Valid && policyAccepted.Plan!.Crossings.Count == 1,
    "Authored crossing passes classified settlement road policy");
var policyDenied = roadCoordinator.Build(villageDocument.SemanticWorld, alignedSettlement,
    new[] { policyIntent }, new[] { allowPolicy with { PedestrianCrossingsAllowed = false } });
Assert(!policyDenied.Valid && policyDenied.Issues.Contains(
    "ROAD_CROSSING_PROHIBITED_BY_POLICY:policy-zebra"),
    "Protected road classification can forbid pedestrian crossing");
var uncontrolled = roadCoordinator.Build(villageDocument.SemanticWorld, alignedSettlement,
    new[] { policyIntent with { Accessible = false } }, new[] { allowPolicy });
Assert(!uncontrolled.Valid && uncontrolled.Issues.Contains(
    "ROAD_CONTROLLED_CROSSING_NOT_READY:policy-zebra"),
    "Controlled crossings require explicit permission and accessibility");


var transportSpec = new SettlementTransportSpecification("village-transport-v1",
    villageDocument.SemanticWorld.Base.MockupId,
    villageDocument.SemanticWorld.Base.MockupRevision,
    new[] { allowPolicy }, new[] { policyIntent });
var transportBuilder = new SettlementTransportDesignBuilder();
var transportBuilt = transportBuilder.Build(villageDocument.SemanticWorld,
    alignedSettlement, transportSpec);
Assert(transportBuilt.Valid && transportBuilt.Crossings!.Crossings.Count == 1,
    "Approved settlement transport specification generates planned crossing");
var staleTransport = transportBuilder.Build(villageDocument.SemanticWorld,
    alignedSettlement, transportSpec with { VisualRevision = "obsolete" });
Assert(!staleTransport.Valid && staleTransport.Issues.Contains(
    "SETTLEMENT_TRANSPORT_PROVENANCE_MISMATCH"),
    "Transport decisions cannot drift from approved visual mockup revision");
var forbiddenTransport = transportBuilder.Build(villageDocument.SemanticWorld,
    alignedSettlement, transportSpec with { RoadPolicies =
        new[] { allowPolicy with { PedestrianCrossingsAllowed = false } } });
Assert(!forbiddenTransport.Valid && forbiddenTransport.Issues.Contains(
    "ROAD_CROSSING_PROHIBITED_BY_POLICY:policy-zebra"),
    "Settlement transport specification enforces road policy before assembly");


var transportWorldAssembler = new SettlementTransportWorldAssembler();
var (assembledTransportWorld, assembledTransportIssues) = transportWorldAssembler.Assemble(
    villageDocument, alignedSettlement, transportSpec, qualityRules);
Assert(assembledTransportWorld is not null && assembledTransportIssues.Count == 0 &&
    assembledTransportWorld.RoadCrossings!.Single().Id == "policy-zebra",
    "Approved settlement transport design integrates into quality-gated portable village");
var (rejectedTransportWorld, rejectedTransportIssues) = transportWorldAssembler.Assemble(
    villageDocument, alignedSettlement,
    transportSpec with { VisualRevision = "not-approved" }, qualityRules);
Assert(rejectedTransportWorld is null && rejectedTransportIssues.Contains(
    "SETTLEMENT_TRANSPORT_PROVENANCE_MISMATCH"),
    "Unapproved transport changes cannot be assembled into portable world");


var coordinatedSpec = new CoordinatedSettlementSpecification(
    villageDocument.SemanticWorld.Base.MockupId,
    villageDocument.SemanticWorld.Base.MockupRevision,
    villageDocument.SemanticWorld.Terrain.ToDictionary(
        t => t.SourceVisualRegionId,
        t => new TerrainCompositionStyle("cozy-" + t.Kind,
            "fixture-" + t.Kind, t.DesignRuleId), StringComparer.Ordinal),
    new[] {
        new LandscapeDesignGrammar("western-garden", "flower-rows", "fixture-flowers", 1, 2, 1),
        new LandscapeDesignGrammar("eastern-garden", "orchard-rows", "fixture-orchard-soil", 1, 2, 1)
    },
    new[] {
        new VegetationGrammar("western-flowers", "western-garden", "fixture-flower",
            VegetationPattern.OrchardGrid, 4, 2, "ordered flowers"),
        new VegetationGrammar("eastern-trees", "eastern-garden", "fixture-tree",
            VegetationPattern.OrchardGrid, 7, 3, "fruit orchard")
    }, transportSpec);
var coordinatedBuilder = new CoordinatedSettlementWorldBuilder();
var composedSettlement = coordinatedBuilder.Build(villageDocument,
    alignedSettlement, coordinatedSpec, qualityRules);
Assert(composedSettlement.Valid && composedSettlement.World!.TerrainMaterials.Count > 0 &&
    composedSettlement.World.GardenBeds.Count > 0 &&
    composedSettlement.World.Vegetation.Count > 0 &&
    composedSettlement.World.RoadCrossings!.Count == 1,
    "One coordinated settlement workflow builds terrain gardens vegetation and crossing");
Assert(JsonSerializer.Serialize(composedSettlement) ==
    JsonSerializer.Serialize(coordinatedBuilder.Build(villageDocument,
        alignedSettlement, coordinatedSpec, qualityRules)),
    "Coordinated world generation remains deterministic");
var badCoordinated = coordinatedBuilder.Build(villageDocument,
    alignedSettlement, coordinatedSpec with { MockupRevision = "stale" }, qualityRules);
Assert(!badCoordinated.Valid && badCoordinated.Issues.Contains(
    "COORDINATED_MOCKUP_REVISION_MISMATCH"),
    "Coordinated assembly fails when the visual revision is no longer approved");


var nonVegetationIds = WorldArtDependencyInventory.Collect(composedSettlement.World!);
Assert(nonVegetationIds.Contains("fixture-home") &&
    nonVegetationIds.Contains("fixture-grass") &&
    nonVegetationIds.Contains("fixture-flowers"),
    "Inventory includes building, terrain and landscape GameArts dependencies");
var coordinatedProduction = new CoordinatedProductionWorldExporter(
    new InMemoryRegistry(full), new TestApprovalAuthority(true));
var composedWorld = composedSettlement.World!;
var testProductionInputs = new ProductionWorldRequest(composedWorld.SemanticWorld,
    new VegetationComposition(composedWorld.Vegetation, Array.Empty<string>()),
    new PathVegetationResult(Array.Empty<PlannedVegetation>(), Array.Empty<string>()),
    approvedBuildings, Array.Empty<VegetationClearanceRoute>(),
    Array.Empty<VegetationClearanceAnchor>(),
    new[] { new GameArtsVegetationFootprint("fixture-flower", full.GameArtsRevision, 0.3),
        new GameArtsVegetationFootprint("fixture-tree", full.GameArtsRevision, 1.5) },
    Path.GetTempPath(), Array.Empty<GameArtsWorldBundleReference>());
var coordinatedExportPath = Path.Combine(Path.GetTempPath(),
    "coordinated-production-" + Guid.NewGuid().ToString("N") + ".json");
try
{
    var refused = await coordinatedProduction.ExportAsync(
        new CoordinatedProductionRequest(composedSettlement, testProductionInputs),
        coordinatedExportPath);
    Assert(!refused.Produced && refused.Issues.Contains(
        "NONVEGETATION_BUNDLE_MISSING:fixture-grass") &&
        !File.Exists(coordinatedExportPath),
        "Complete settlement cannot claim production approval with unverified houses and terrain");
    var drifted = await coordinatedProduction.ExportAsync(
        new CoordinatedProductionRequest(composedSettlement, testProductionInputs with {
            SemanticWorld = villageFixture with { Base = villageFixture.Base with { MockupRevision = "drifted" } }
        }), coordinatedExportPath);
    Assert(!drifted.Produced && drifted.Issues.Contains(
        "PRODUCTION_SEMANTIC_WORLD_MISMATCH"),
        "Production export detects world model drift from coordinated composition");
}
finally { if (File.Exists(coordinatedExportPath)) File.Delete(coordinatedExportPath); }


var multiAssetRoot = Path.Combine(Path.GetTempPath(), "gfw-multiart-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(multiAssetRoot);
try
{
    const string simulatedRevision = "simulated-fixture-r1";
    var assetIds = WorldArtDependencyInventory.Collect(composedWorld)
        .Concat(composedWorld.Vegetation.Select(p => p.SpeciesAssetId))
        .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var manifests = new Dictionary<string, ApprovedArtManifest>(StringComparer.Ordinal);
    var bundles = new List<GameArtsWorldBundleReference>();
    byte[] icon = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");
    static string ArtHash(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    foreach (var assetId in assetIds)
    {
        var imageName = assetId + ".png";
        var atlasName = assetId + ".atlas.json";
        var bundleName = assetId + ".bundle.json";
        await File.WriteAllBytesAsync(Path.Combine(multiAssetRoot, imageName), icon);
        var atlasText = JsonSerializer.Serialize(new {
            schema_version = "1.0", image = imageName, mask_color = "#FF00FF",
            background_alpha = 255, image_size = new[] { 1, 1 },
            frames = new[] { new { rect = new[] { 0, 0, 1, 1 },
                pivot = new[] { 0, 0 }, anchors = new Dictionary<string,int[]>() } }
        });
        await File.WriteAllTextAsync(Path.Combine(multiAssetRoot, atlasName), atlasText);
        var bundleText = JsonSerializer.Serialize(new {
            schema_version = "1.0", asset_id = assetId, directions = new[] { "S" },
            components = new[] { new { id = "base", parent = (string?)null,
                atlases = new Dictionary<string,string> { ["S"] = atlasName } } },
            sampled_poses = new[] { new { direction = "S", normalized_time = 0,
                nodes = new Dictionary<string,object> { ["base"] = new {
                    frame_index = 0, world_matrix_2d = new[] {
                        new[] { 1, 0, 0 }, new[] { 0, 1, 0 }, new[] { 0, 0, 1 } }
                } } } }
        });
        await File.WriteAllTextAsync(Path.Combine(multiAssetRoot, bundleName), bundleText);
        manifests.Add(assetId, new ApprovedArtManifest(2, assetId, assetId, simulatedRevision,
            "fixture-test", ArtApprovalState.Approved,
            new[] { new SpriteSheetManifest(assetId, imageName, atlasName,
                ArtHash(icon), ArtHash(System.Text.Encoding.UTF8.GetBytes(atlasText)),
                "base", new[] { "S" }) }, Array.Empty<ComponentAttachment>()));
        bundles.Add(new GameArtsWorldBundleReference(assetId, bundleName, simulatedRevision));
    }
    var multiRegistry = new MappedRegistry(manifests);
    var multiBuilder = new CoordinatedProductionWorldExporter(
        multiRegistry, new TestApprovalAuthority(true));
    var footprintProfiles = new[] {
        new GameArtsVegetationFootprint("fixture-flower", simulatedRevision, 0.3),
        new GameArtsVegetationFootprint("fixture-tree", simulatedRevision, 1.5)
    };
    var actualEnvelopes = composedWorld.Buildings.ToDictionary(
        x => x.ObjectId, x => new BuildingEnvelopeResult(x.Boundary, Array.Empty<string>()),
        StringComparer.Ordinal);
    var multiInputs = new ProductionWorldRequest(composedWorld.SemanticWorld,
        new VegetationComposition(composedWorld.Vegetation, []),
        new PathVegetationResult([], []), actualEnvelopes,
        Array.Empty<VegetationClearanceRoute>(),
        Array.Empty<VegetationClearanceAnchor>(),
        footprintProfiles, multiAssetRoot, bundles);
    var multiOutput = Path.Combine(multiAssetRoot, "production-world.json");
    var passed = await multiBuilder.ExportAsync(
        new CoordinatedProductionRequest(composedSettlement, multiInputs), multiOutput);
    Assert(passed.Produced && File.Exists(multiOutput),
        "Complete multi-asset simulated approval produces coordinated portable production world");
    using (var written = JsonDocument.Parse(await File.ReadAllTextAsync(multiOutput)))
        Assert(written.RootElement.GetProperty("ProductionApproved").GetBoolean() &&
            written.RootElement.GetProperty("Buildings").GetArrayLength() == 2 &&
            written.RootElement.GetProperty("AssetDependencies").GetArrayLength() == assetIds.Length,
            "Production test export retains complete building data");
    var manifestEvidence = await new WorldAssetDependencyManifestBuilder(multiRegistry)
        .BuildAsync(assetIds.Reverse());
    Assert(manifestEvidence.Valid &&
        manifestEvidence.Dependencies.Select(x => x.AssetId)
            .SequenceEqual(assetIds),
        "Asset dependency evidence deterministic regardless of input ordering");
    var packageVerifier = new PortableWorldPackageVerifier(multiRegistry);
    var verifiedPackage = await packageVerifier.VerifyAsync(multiOutput, multiAssetRoot);
    Assert(verifiedPackage.Valid,
        "Previously exported coordinated world passes independent dependency audit");
    await ProductionIntegrityTests.RunAsync(multiAssetRoot, multiOutput, manifests,
        composedSettlement, multiInputs, cliPath);
    using (var originalDoc = JsonDocument.Parse(await File.ReadAllTextAsync(multiOutput)))
    {
        var tamperedDocument = JsonSerializer.Deserialize<WorldSceneDocument>(
            originalDoc.RootElement.GetRawText())!;
        var missingDependency = tamperedDocument with {
            AssetDependencies = tamperedDocument.AssetDependencies!
                .Where(x => x.AssetId != "fixture-home").ToArray()
        };
        var alteredPackage = Path.Combine(multiAssetRoot, "missing-dependency.json");
        await File.WriteAllTextAsync(alteredPackage, JsonSerializer.Serialize(missingDependency));
        var rejectedPackage = await packageVerifier.VerifyAsync(alteredPackage, multiAssetRoot);
        Assert(!rejectedPackage.Valid &&
            rejectedPackage.Issues.Contains("WORLD_DEPENDENCY_MISSING:fixture-home"),
            "Independent package verifier rejects omitted production asset evidence");
    }
    var tamperedAsset = assetIds.First(id => id == "fixture-home");
    await File.WriteAllTextAsync(Path.Combine(multiAssetRoot, tamperedAsset + ".atlas.json"),
        "{\"frames\":[]}");
    var changedPackage = await packageVerifier.VerifyAsync(multiOutput, multiAssetRoot);
    Assert(!changedPackage.Valid && changedPackage.Issues.Any(x =>
        x.StartsWith("WORLD_DEPENDENCY_FILE_ASSET_HASH_MISMATCH:fixture-home:")),
        "Independent package verifier detects modified GameArts files after export");
    var rejected = await multiBuilder.ExportAsync(
        new CoordinatedProductionRequest(composedSettlement, multiInputs), multiOutput);
    Assert(!rejected.Produced && rejected.Issues.Any(x =>
        x.StartsWith("NONVEGETATION_FILE_fixture-home:ASSET_HASH_MISMATCH")),
        "Tampered building atlas rejects full production world");
}
finally { Directory.Delete(multiAssetRoot, recursive: true); }

// Durable operational service contract test: log, audit and user-facing notification are separate sinks.
var operationsDir = Path.Combine(Path.GetTempPath(), "gameframework-ops-" + Guid.NewGuid().ToString("N"));
try
{
    var logPath = Path.Combine(operationsDir, "log.jsonl");
    var auditPath = Path.Combine(operationsDir, "audit.jsonl");
    var notificationPath = Path.Combine(operationsDir, "notifications.jsonl");
    var reporter = new WorldOperationReporter(new JsonLineOperationStore(logPath),
        new JsonLineOperationStore(auditPath),
        new ConsoleAndFileNotificationService(new JsonLineOperationStore(notificationPath)));
    var evt = new OperationEvent("world.validation.failed", DateTimeOffset.UtcNow,
        OperationSeverity.Warning, "tests", "world.validate", "correlation-test",
        "Invalid placement rejected.", "test-world", "test-runner", "failed");
    await reporter.ReportAsync(evt, notify: true);
    foreach (var output in new[] { logPath, auditPath, notificationPath })
    {
        Assert(File.Exists(output), "Operation sink file created");
        var stored = File.ReadAllText(output);
        Assert(stored.Contains("world.validation.failed") && stored.Contains("correlation-test"), "Correlated operation persisted");
    }
}
finally { if (Directory.Exists(operationsDir)) Directory.Delete(operationsDir, true); }
await MockupAlgorithmTests.RunAsync(cliPath);
await CatalogExpansionTests.RunAsync(cliPath);
await InfrastructureAlgorithmTests.RunAsync();
await SitingAlgorithmTests.RunAsync(cliPath);
await CatalogContractTests.RunAsync(cliPath);
await FacilityCompositionTests.RunAsync(cliPath);
await CatalogContentAuthoringTests.RunAsync(cliPath);
await StructureFirstPipelineTests.RunAsync(cliPath);
await WorldMetadataTests.RunAsync(cliPath);

await CityStreetBlockTests.RunAsync();
await MockupCaptureTests.RunAsync(cliPath);
await CurvedRoadTests.RunAsync(cliPath);
Console.WriteLine("PASS: engine regression harness (synthetic artwork; real approval and Godot runtime not verified).");
static void AssertThrows<T>(Action action, string message) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Failed: " + message);
}
static void Assert(bool pass, string message)
{
    if (!pass) throw new Exception("Failed: " + message);
}
sealed class TestApprovalAuthority(bool approved) : IGameArtsApprovalAuthority
{
    public Task<bool> IsApprovedAsync(string assetId, string revision,
        CancellationToken token = default) => Task.FromResult(approved);
    public Task<bool> IsApprovedArtifactAsync(string id,string revision,string hash,CancellationToken token=default)=>IsApprovedAsync(id,revision,token);
}
sealed class InMemoryRegistry(ApprovedArtManifest? manifest) : IApprovedArtRegistry
{
    public Task<ApprovedArtManifest?> ResolveAsync(string assetId, CancellationToken cancellationToken = default)
        => Task.FromResult(manifest);
}

sealed class MappedRegistry(IReadOnlyDictionary<string, ApprovedArtManifest> manifests) : IApprovedArtRegistry
{
    public Task<ApprovedArtManifest?> ResolveAsync(string assetId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(manifests.TryGetValue(assetId, out var manifest) ? manifest : null);
}
