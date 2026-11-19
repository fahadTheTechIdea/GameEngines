using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Spatial;
using WorldCreator.Godot;
using WorldCreator.Core.Operations;

namespace WorldCreator.Cli;

/// <summary>Thin CLI adapter; capability and publication belong to the pipeline.</summary>
internal static class StructureFirstCommand
{
    public static async Task<bool> RequiresRegistryAsync(string[] arguments,CancellationToken token)
    {
        if(arguments.Length==6&&arguments[1]=="tile-topology")
        {
            var world=await StructureFirstPipeline.ReadJsonAsync<WorldMetadataDocument>(arguments[2],token:token);
            return world.StructureDefinitions.Count>0||world.PlacementPlan?.Definition.SourceWorld?.StructureDefinitions.Count>0;
        }
        if(arguments.Length==5&&arguments[1]=="production-inventory"||arguments.Length==7&&arguments[1]=="production-bind"||
            arguments.Length==9&&arguments[1]=="native-static")
        {
            var scene=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(arguments[2],token:token);
            return MetadataSessionCommand.RequiresRegistry(scene.Recipe,scene.Ground.Handoff.Catalog);
        }
        if(arguments.Length==6&&arguments[1]=="scene")
        {
            var ground=await StructureFirstPipeline.ReadJsonAsync<MetadataGroundCompilation>(arguments[2],token:token);
            return MetadataSessionCommand.RequiresRegistry(ground.Handoff.Recipe,ground.Handoff.Catalog);
        }
        if(arguments.Length==6&&arguments[1]=="ground")
        {
            var handoff=await StructureFirstPipeline.ReadJsonAsync<MetadataArtHandoff>(arguments[2],token:token);
            return MetadataSessionCommand.RequiresRegistry(handoff.Recipe,handoff.Catalog);
        }
        if(arguments.Length==7&&arguments[1]=="reconcile")
        {
            var handoff=await StructureFirstPipeline.ReadJsonAsync<MetadataArtHandoff>(arguments[2],token:token);
            return MetadataSessionCommand.RequiresRegistry(handoff.Recipe,handoff.Catalog);
        }
        if(arguments.Length!=7||arguments[1]!="metadata")return false;
        var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(arguments[3],token);
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(arguments[4],token);
        return MetadataSessionCommand.RequiresRegistry(recipe,catalog);
    }

    public static async Task<string> RunAsync(string[] arguments, CancellationToken token,WorldOperationReporter reporter,ISettlementCatalogRegistry? registry=null)
    {
        var pipeline = new StructureFirstPipeline();
        if(arguments.Length==7&&arguments[1]=="production-bind")
        {
            var context=await CanonicalGameArtsContext.LoadAsync(arguments[5],reporter,token);
            var scene=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(arguments[2],token:token);
            var specification=await StructureFirstPipeline.ReadJsonAsync<MetadataProductionSpecification>(arguments[3],token:token);
            var assembly=await pipeline.ExportMetadataProductionAsync(scene,specification,arguments[4],context.AssetRoot,
                context.Assets,context.Assets,context.Mockups,arguments[6],new(registry),token);
            return $"Saved {assembly.Status}: {assembly.Targets.Count} measured targets with canonical lifecycle checks. Native compilation and whole-world acceptance remain separate gates.";
        }
        if(arguments.Length==9&&arguments[1]=="native-static")
        {
            var context=await CanonicalGameArtsContext.LoadAsync(arguments[7],reporter,token);
            var scene=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(arguments[2],token:token);
            var assembly=await StructureFirstPipeline.ReadJsonAsync<MetadataProductionAssembly>(arguments[3],token:token);
            var topology=await StructureFirstPipeline.ReadJsonAsync<WorldTileTopologyPlan>(arguments[4],token:token);
            var specification=await StructureFirstPipeline.ReadJsonAsync<GodotStaticWorldSpecification>(arguments[5],token:token);
            var packet=await new GodotStructureLayerExporter().ExportMetadataAsync(scene,assembly,topology,specification,arguments[6],context.AssetRoot,
                context.Assets,context.Assets,context.Mockups,arguments[8],registry,token);
            return $"Exported {packet.Status}: {packet.Groups.Count} terrain layers and {packet.Structures.Count} static structures. Execute the installed Godot compiler; artwork and whole-world acceptance remain unfinished.";
        }
        if(arguments.Length==6&&arguments[1]=="tile-topology")
        {
            var world=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(arguments[2],token,registry);
            var request=await StructureFirstPipeline.ReadJsonAsync<WorldTileTopologyRequest>(arguments[3],token:token);
            var groups=await StructureFirstPipeline.ReadJsonAsync<GodotTerrainTopologySpecification>(arguments[4],token:token);
            var plan=new WorldTileTopologyPlanner().Plan(world,request,registry,token);
            var packet=await new GodotStructureLayerExporter().ExportTopologyProbeAsync(world,plan,groups,arguments[5],registry,token);
            return $"Exported {packet.Status}: {plan.Channels.Count} exact-source channels in {packet.Groups.Count} explicit material groups. Execute the installed Godot probe to measure native demand. Production artwork and whole-world runtime remain unfinished.";
        }
        if(arguments.Length==5&&arguments[1]=="production-inventory")
        {
            var scene=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(arguments[2],token:token);
            var result=await pipeline.ExportMetadataProductionInventoryAsync(scene,arguments[3],arguments[4],new(registry),token);
            return $"Saved {result.Status}: {result.Targets.Count} production targets; all {result.Members.Count} metadata members retained. Artwork, complete tile transitions and native runtime remain unfinished.";
        }
        if(arguments.Length==6&&arguments[1]=="scene")
        {
            var ground=await StructureFirstPipeline.ReadJsonAsync<MetadataGroundCompilation>(arguments[2],token:token);
            var request=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneRequest>(arguments[3],token:token);
            var result=await pipeline.ExportMetadataSceneAsync(ground,request,arguments[4],arguments[5],new(registry),token);
            return $"Exported {result.Status}: {result.Surfaces.Count} traced surface elements and {result.Paths.Count} prescribed paths; all {result.World.Nodes.Count} metadata members retained. Actual visual review, art approval and native runtime remain unfinished.";
        }
        if(arguments.Length==6&&arguments[1]=="ground")
        {
            var handoff=await StructureFirstPipeline.ReadJsonAsync<MetadataArtHandoff>(arguments[2],token:token);
            var request=await StructureFirstPipeline.ReadJsonAsync<MetadataGroundRequest>(arguments[3],token:token);
            var result=await pipeline.ExportMetadataGroundAsync(handoff,request,arguments[4],arguments[5],new(registry),token);
            return $"Exported {result.Status}: {result.Request.Views.Count} calibrated views, {result.Footprints.Count} unique source-sized footprints at owning-view origins; {result.World.Nodes.Count} metadata members retained. Terrain/route image fidelity, art approval and native runtime remain separate gates.";
        }
        if(arguments.Length==7&&arguments[1]=="reconcile")
        {
            var handoff=await StructureFirstPipeline.ReadJsonAsync<MetadataArtHandoff>(arguments[2],token:token);
            var capture=await MockupCapturePipeline.ReadCaptureAsync(arguments[3],token);
            var specification=await StructureFirstPipeline.ReadJsonAsync<MetadataCaptureSpecification>(arguments[4],token:token);
            var report=await pipeline.ExportMetadataCaptureAsync(handoff,capture,specification,arguments[5],arguments[6],new(registry),token);
            return $"Saved capture reconciliation: {report.Status}; {report.Members.Count(m=>m.Matched)}/{report.Members.Count} required instances matched; {report.Issues.Count} issues. This is an annotation identity check, not visual or map acceptance.";
        }
        if(arguments.Length==7&&arguments[1]=="metadata")
        {
            var world=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(arguments[2],token,registry);
            var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(arguments[3],token);
            var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(arguments[4],token);
            var spec=await StructureFirstPipeline.ReadJsonAsync<MetadataArtSpecification>(arguments[5],token:token);
            var handoff=await pipeline.ExportMetadataAsync(world,recipe,catalog,spec,arguments[6],new(registry),token);
            return $"Saved draft art inventory: {handoff.Members.Count} complete metadata members and {handoff.Jobs.Count} ordered jobs. Canonical GameArts settings, reviews and approvals remain required.";
        }
        if (arguments.Length == 4 && arguments[1] == "plan")
        {
            var world = await StructureFirstPipeline.ReadJsonAsync<WorldDocument>(arguments[2], 536870912, token);
            await StructureFirstPipeline.SaveSpecificationAsync(pipeline.Plan(world, token), arguments[3], token);
            return "Saved structure jobs and scale-aware scope plan; city defaults are partial district samples.";
        }
        if (arguments.Length == 6 && arguments[1] == "build")
        {
            var world = await StructureFirstPipeline.ReadJsonAsync<WorldDocument>(arguments[2], 536870912, token);
            var specification = await StructureFirstPipeline.ReadJsonAsync<StructureFirstSpecification>(arguments[3], token: token);
            var result = await pipeline.ExportAsync(world, specification, arguments[4], arguments[5], token);
            return $"Exported {result.Assembly.Instances.Count} exact C# structure instances; coverage complete: {result.CoverageComplete}; production blockers: {result.ProductionIssues.Count}.";
        }
        throw new ArgumentException("Invalid structure-pipeline arguments. See --help for supported commands and required paths.");
    }
}
