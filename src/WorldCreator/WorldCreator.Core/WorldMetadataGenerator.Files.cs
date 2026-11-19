using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class WorldMetadataGenerator
{
    public static async Task<T> ReadAsync<T>(string file,CancellationToken token=default,WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs=null,IEnumerable<IMetadataLayout>? layouts=null)
    {
        await using var stream=await MetadataJsonStorage.OpenReadAsync(file,MetadataJsonStorage.Limit(typeof(T)),token);
        var value=await JsonSerializer.DeserializeAsync<T>(stream,MetadataJsonStorage.Options(),token)
            ??throw new ArgumentException("METADATA_INPUT_REQUIRED");
        await MetadataJsonStorage.CompleteReadAsync(stream,token);
        if (value is WorldMetadataDocument document)ValidateDocument(document,catalogs,token,layouts);
        if(value is WorldDrainagePlan drainage)new WorldDrainagePlanner().Validate(drainage,token);
        if(value is WorldTerrainWindow terrainWindow)_=new WorldTerrainGenerator().ReplayWindow(terrainWindow,token);
        if(value is WorldWindMoisturePlan wind)new WorldWindMoisturePlanner().Validate(wind,token);
        if(value is MetadataWindMoisturePlan windReceipt)_=new MetadataWindMoisturePlanner().Apply(windReceipt,token);
        if(value is MetadataWorldDomainDocument domains)new WorldMetadataGenerator(catalogs,layouts).ValidateDomains(domains,token);
        if(value is MetadataRegionalWorldDocument regions)new MetadataRegionalWorldGenerator(new(catalogs,layouts)).ValidateDocument(regions,token);
        return value;
    }
    internal Task<WorldMetadataDocument> ReadStoredMetadataAsync(string file,CancellationToken token)
        =>ReadAsync<WorldMetadataDocument>(file,token,structureCatalogs,layouts.Values);
    /// <summary>Reads a published recipe/catalog/world package and replays every
    /// declared member and generated plan. Agreement with these source files is
    /// verified; this is not external source approval or historical migration.</summary>
    public async Task<WorldMetadataDocument> ReadPublishedAsync(string directory,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("METADATA_PUBLICATION_DIRECTORY_REQUIRED");
        string root=Path.GetFullPath(directory);
        var recipe=await ReadAsync<WorldMetadataRecipe>(Path.Combine(root,"recipe.json"),token);
        var catalog=await ReadAsync<WorldMetadataCatalog>(Path.Combine(root,"catalog.json"),token);
        var stored=await ReadStoredMetadataAsync(Path.Combine(root,"world-metadata.json"),token);
        if(stored.RecipeSha256!=Digest(recipe)||stored.CatalogSha256!=Digest(catalog))
            throw new ArgumentException("METADATA_PUBLICATION_SOURCE_MISMATCH");
        var expected=Generate(recipe,catalog,token);
        if(!MetadataPopulationSources.Equal(expected,stored))throw new ArgumentException("METADATA_PUBLICATION_REPLAY_MISMATCH");
        return expected;
    }
    internal Task<MetadataRegionalWorldDocument> ReadStoredRegionsAsync(string file,CancellationToken token)
        =>ReadAsync<MetadataRegionalWorldDocument>(file,token,structureCatalogs,layouts.Values);
    public static void ValidateDocument(WorldMetadataDocument document,WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs=null,CancellationToken token=default,IEnumerable<IMetadataLayout>? layouts=null)
        =>ValidateDocumentCore(document,catalogs,token,layouts,false);
    internal static void ValidateChunkSource(WorldMetadataDocument document,WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs,CancellationToken token,IEnumerable<IMetadataLayout>? layouts)
        =>ValidateDocumentCore(document,catalogs,token,layouts,true);
    private static void ValidateDocumentCore(WorldMetadataDocument document,WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs,CancellationToken token,IEnumerable<IMetadataLayout>? layouts,bool chunkSource)
    {
            token.ThrowIfCancellationRequested();
            new MetadataVariationPlanner().ValidateDocument(document,token);
            new MetadataProgramMixPlanner().ValidateDocument(document,token);
            new MetadataProgramMixPlanner().ValidateCompositionDocument(document,catalogs,layouts,token,chunkSource);
            ValidatePopulationCoordination(document,catalogs,layouts,token,chunkSource);
            if(document.PlacementPlan?.Definition.SourceWorld is {} source)
            {
                if(source.PlacementPlan?.Definition.SourceWorld is not null||source.PlacementPlan?.TemplateDefinition?.SourceWorld is not null)
                    throw new ArgumentException("METADATA_POPULATION_SOURCE_LEVEL_INVALID");
                ValidateDocument(source,catalogs,token,layouts);
                MetadataPopulationSources.ValidateResult(source,document,token);
            }
            new MetadataLayerPlanner().ValidateDocument(document,token);
            ValidateComponents(document.Nodes,token);
            new MetadataTopologyPlanner().ValidateDocument(document,token);
            new MetadataProgressionPlanner().ValidateDocument(document,token);
            if(document.Surface is {} surface)new MetadataSurfaceConstraint(surface,document.Space,token).ValidateNodes(document.Nodes,token);
            new MetadataDrainagePlanner().ValidateDocument(document,token);
            new MetadataPhysicalAccessPlanner().ValidateDocument(document,token);
            new MetadataInteriorPlanner().ValidateDocument(document,token);
            new MetadataRoutePlanner().ValidateDocument(document,token);
            new MetadataNetworkPlanner().ValidateDocument(document,token);
            new MetadataEnvironmentPlanner().ValidateDocument(document,token);
            new MetadataRegionPlanner().ValidateDocument(document,token);
            var placementReplay=new MetadataLocalLayoutPlanner(catalogs).ValidateWithPlacementReplay(document,token);
            new MetadataPlacementPlanner(catalogs).ValidateWithReplay(document,placementReplay,token);
            new MetadataCapacityPlanner().ValidateDocument(document,token);
            new MetadataTerrainDemandPlanner().ValidateDocument(document,token);
            new MetadataCorridorPlanner().ValidateDocument(document,token);
            new MetadataGroundCoverPlanner().ValidateDocument(document,token);
            new MetadataServicePlanner(catalogs).ValidateDocument(document,token);
            new MetadataContextPlanner(catalogs).ValidateDocument(document,token);
            new MetadataChunkPlanner().ValidateDocument(document,token);
            new MetadataPhysicalAccountingPlanner().ValidateRequiredGroundUses(document,token);
    }
    public async Task<WorldMetadataDocument> GenerateFilesAsync(string recipeFile,string catalogFile,string newOutputDirectory,CancellationToken token=default)
    {
        var recipe=await ReadAsync<WorldMetadataRecipe>(recipeFile,token);
        var catalog=await ReadAsync<WorldMetadataCatalog>(catalogFile,token);
        var world=Generate(recipe,catalog,token);
        await PublishAsync(recipe,catalog,world,newOutputDirectory,null,token);
        return world;
    }
    internal async Task PublishAsync(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldMetadataDocument world,
        string newOutputDirectory,MetadataSessionCheckpoint? checkpoint,CancellationToken token,
        WorldCreator.Core.Capture.MetadataGroundCompilation? captureCompilation=null,string? captureImageRoot=null,
        WorldCreator.Core.Capture.MetadataSceneCompilation? sceneCompilation=null,SchematicDomainNavigation? navigation=null,
        MetadataRegionPublication? regionalPublication=null)
    {
        token.ThrowIfCancellationRequested();
        new MetadataWindMoisturePlanner().ValidateRecipe(recipe,world,token);
        string target=Path.GetFullPath(newOutputDirectory),parent=Path.GetDirectoryName(target)!;
        if(Directory.Exists(target) || File.Exists(target))throw new IOException("METADATA_OUTPUT_EXISTS");
        string dependencyHash=MetadataExecutionCache.Hash(checkpoint?.Edits.LastOrDefault()?.Report.Dependencies??checkpoint?.InitialReport.Dependencies??Dependencies(recipe,catalog,token));
        Directory.CreateDirectory(parent);string staging=Path.Combine(parent,".metadata-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        Exception? publicationFailure=null;
        try
        {
            await Write("recipe.json",recipe);await Write("catalog.json",catalog);var storage=await Write("world-metadata.json",world);
            if(regionalPublication is not null)
            {
                await Write("regional-definition.json",regionalPublication.Definition);
                await Write("generation-region.json",regionalPublication.Receipt);
            }
            if(captureCompilation is not null)
            {
                await Write("capture-compilation.json",captureCompilation);
                await WorldCreator.Core.Capture.MockupCapturePipeline.WriteMetadataGroundOverlaysAsync(captureCompilation,
                    captureImageRoot??throw new ArgumentException("METADATA_GROUND_VERIFIED_SOURCE_REQUIRED"),staging,token);
            }
            if(sceneCompilation is not null)
            {
                await Write("scene-compilation.json",sceneCompilation);
                await WorldCreator.Core.Capture.MockupCapturePipeline.WriteMetadataSceneOverlaysAsync(sceneCompilation,
                    captureImageRoot??throw new ArgumentException("METADATA_SCENE_VERIFIED_SOURCE_REQUIRED"),staging,token);
            }
            if(world.ChunkPlan is {} chunks)await Write("chunk-index.json",chunks);
            if(world.DrainagePlan is {} drainage)await Write("drainage-plan.json",drainage);
            if(world.WaterPlan is {} water)await Write("water-plan.json",water);
            if(world.WindMoisturePlan is {} windReceipt)await Write("wind-moisture-plan.json",windReceipt);
            if(world.GroundCoverPlan is {} groundCover)await Write("ground-cover-plan.json",groundCover);
            if(checkpoint is not null)await Write("generation-session.json",checkpoint);
            var accounting=new MetadataPhysicalAccountingPlanner().Measure(world,token:token);
            await Write("physical-accounting.json",accounting);
            await Write("summary.json",new { world.SchemaVersion,world.Id,world.GameProfile,world.Scale,world.Coverage,world.RemainingScopes,
                Nodes=world.Nodes.Count,Connections=world.Connections.Count,
                Layers=world.LayerPlan.Layers.Count,UnassignedLayerNodes=world.LayerPlan.UnassignedNodeIds.Count,
                UnassignedLayerConnections=world.LayerPlan.UnassignedConnectionIds.Count,
                SurfaceId=world.Surface?.Id,TerrainPatches=world.Surface?.Patches.Count,
                DrainageSamples=world.DrainagePlan?.Nodes.Count??0,DrainageTerminals=world.DrainagePlan?.Terminals.Count??0,
                DrainageRateUnit=world.DrainagePlan?.Definition.RateUnit,DrainageInput=world.DrainagePlan?.TotalInput,
                DrainageLost=world.DrainagePlan?.TotalLost,DrainageReceived=world.DrainagePlan?.TotalReceived,
                WetSegments=world.WaterPlan?.Segments.Count??0,WetPatches=world.WaterPlan?.Patches.Count??0,
                WetKind=world.WaterPlan?.Definition.Kind,
                RetainedWaterBodies=world.WaterPlan?.RetainedBodies?.Count??0,
                RetainedWaterArea=world.WaterPlan?.RetainedBodies?.Sum(b=>b.Region.Area)??0,
                RetainedWaterCapacity=world.WaterPlan?.RetainedBodies?.Sum(b=>b.Capacity)??0,
                WindMoistureId=world.WindMoisturePlan?.Derivation.Definition.Id,
                WindMoistureSamples=world.WindMoisturePlan?.Derivation.Nodes.Count??0,
                WindMoistureFields=world.WindMoisturePlan?.Derivation.Fields.Count??0,
                RoutedConnections=world.RoutePlan?.Routes.Count??0,
                ConsumedRouteWork=world.RoutePlan?.Work??0,
                  InteriorOpenings=world.InteriorPlan?.Results.Sum(r=>r.Openings.Count)??0,InteriorWalls=world.InteriorPlan?.Results.Sum(r=>r.Walls.Count)??0,
                GeneratedNetworks=world.NetworkPlan?.Networks.Count??0,NetworkConnections=world.NetworkPlan?.Networks.Sum(n=>n.Plan.Edges.Count)??0,
                GeneratedTopologies=world.TopologyPlan?.Results.Count??0,TopologyConnections=world.TopologyPlan?.Results.Sum(r=>r.Connections.Count)??0,
                InstancedNestedPrograms=world.PlacementPlan?.ProgramInstances?.Count??0,
                EnvironmentalFields=world.EnvironmentPlan?.Definition.Fields.Count??0,PointSuitabilityChecks=world.EnvironmentPlan?.Observations.Count??0,
                LocalAllocations=world.LocalLayoutPlan?.Allocations.Count??0,LocalInterfaces=world.LocalLayoutPlan?.Interfaces.Count??0,
                GeneratedRegions=world.RegionPlan?.Regions.Count??0,AssignedRegionArea=world.RegionPlan?.Regions.Sum(r=>r.Area),
                UnclaimedSuitableArea=world.RegionPlan?.UnclaimedSuitableArea,IneligibleRegionArea=world.RegionPlan?.IneligibleArea,
                PhysicalAccountingStatus=accounting.Status,PhysicalAccountingReason=accounting.Reason,
                PhysicalAccountingAreaUnit=accounting.Unit+"²",PhysicalGeometrySha256=accounting.GeometrySha256,
                accounting.GrossDomainArea,accounting.SolidUnionArea,accounting.ReservationUnionArea,accounting.PhysicalFootprintUnionArea,accounting.CombinedUnionArea,
                accounting.DeclaredGroundUnionArea,accounting.OpenGroundUnionArea,
                accounting.InteriorCoverage,
                PlacedSites=world.PlacementPlan is {} placement?placement.Placement.Sites.Count+placement.NestedStages.Sum(s=>s.Placement.Sites.Count):0,
                StorageChunks=world.ChunkPlan?.Cells.Count??0,ChunkMembers=world.ChunkPlan?.Members.Count??0,
                Kinds=world.Nodes.GroupBy(n=>n.Kind).OrderBy(g=>g.Key,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count()),
                world.RecipeSha256,world.CatalogSha256,Scope="Metadata generation only; no art, mockups, physics or game runtime required or implied." });
            string review=Review(world,accounting,token);
            if(Encoding.UTF8.GetByteCount(review)>MetadataJsonStorage.MaximumFileBytes)throw new ArgumentException("METADATA_REVIEW_SIZE_LIMIT");
            await File.WriteAllTextAsync(Path.Combine(staging,"review.html"),review,token);
            await new WorldMetadataSchematicExporter(structureCatalogs,layouts.Values).WriteStoredAsync(world,staging,storage,token,navigation);
            await MetadataDirectoryPublication.CommitAsync(staging,target,commitToken=>{
                if(MetadataExecutionCache.Hash(Dependencies(recipe,catalog,commitToken))!=dependencyHash)
                    throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_PUBLICATION");
            },token);
            Task<MetadataJsonManifest?> Write<T>(string name,T value)
                =>MetadataJsonStorage.WriteAsync(Path.Combine(staging,name),value,token);

        }
        catch(Exception error){publicationFailure=error;throw;}
        finally
        {
            MetadataDirectoryPublication.Cleanup(staging,parent,".metadata-",publicationFailure);
        }
    }
    private static string Review(WorldMetadataDocument world,MetadataPhysicalAccounting accounting,CancellationToken token)
    {
        string E(string? text)=>WebUtility.HtmlEncode(text ?? "");
        var b=new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>World metadata</title><style>body{font:16px system-ui;background:#13242c;color:#eaf1f2;margin:28px}a{color:#77ddd0}details{margin:8px 0 8px 20px;border-left:1px solid #49626e;padding:6px 12px}summary{cursor:pointer}pre{white-space:pre-wrap;overflow-wrap:anywhere}td,th{padding:8px;border-bottom:1px solid #49626e;text-align:left}</style>");
        b.Append($"<h1>{E(world.Id)}</h1><p>{E(world.GameProfile)} · {E(world.Scale)} · {world.Coverage} · {world.Nodes.Count} nodes · {world.Connections.Count} connections</p><p>Engine-independent metadata. Images and artwork are optional consumers, not generation inputs.</p>");
        if(world.RemainingScopes.Count>0)b.Append("<p>Scope remains: "+E(string.Join(", ",world.RemainingScopes))+"</p>");
        b.Append("<h2>Physical source accounting</h2><p>Projected areas describe source geometry, not completed world content. Reservation union includes reservation-role footprints and separate declared clearance envelopes. Categories overlap and must not be added. Region allocation, paths and parent reservations do not certify built-content coverage.</p>");
        b.Append($"<p>Status: {accounting.Status}{(accounting.Reason is {} reason?" · "+E(reason):"")}. {accounting.SolidSources} solid, {accounting.ReservationSources} reservation and {accounting.UnclassifiedSources} unclassified sources; {accounting.ClearanceReservations} clearance envelopes. <a href=\"physical-accounting.json\">Source identities and complete accounting</a></p>");
        b.Append("<table><tr><th>Measurement</th><th>Area</th></tr>");
        foreach(var (name,value) in new[]{("Gross domain",accounting.GrossDomainArea),("Solid footprint union",accounting.SolidUnionArea),
            ("Reservations and clearances",accounting.ReservationUnionArea),("Physical footprints (all roles)",accounting.PhysicalFootprintUnionArea),("Footprints and clearance union",accounting.CombinedUnionArea),
            ("Allocated suitable region",accounting.AllocatedSuitableArea),("Unclaimed suitable region",accounting.UnclaimedSuitableArea)})
            b.Append($"<tr><td>{name}</td><td>{(value is {} area?area.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)+" "+E(accounting.Unit)+"²":"Unmeasured")}</td></tr>");
        b.Append("</table>");
        if(accounting.GroundUses is {} groundUses)
        {
            b.Append("<h2>Declared ground uses</h2><p>Open area is each declared source polygon minus all solid source footprints. Parent reservations and clearance envelopes do not imply ground-use intent. Overlapping uses are not additive. Water, walls, paths and runtime traversability retain their own constraints.</p><table><tr><th>Source/use</th><th>Purpose/rule</th><th>Open area</th><th>Minimum open fraction</th><th>Result</th></tr>");
            foreach(var use in groundUses)
                b.Append($"<tr><td>{E(use.NodeId)} · {E(use.Definition.Kind)}</td><td>{E(use.Definition.Purpose)} · {E(use.Definition.SourceRuleId)}</td><td>{use.OpenArea.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)} {E(accounting.Unit)}² / {use.SourceArea.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)}</td><td>{use.Definition.MinimumOpenFraction.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)}</td><td>{(use.MinimumSatisfied?"Satisfied":"Unsatisfied")}</td></tr>");
            b.Append("</table>");
        }
        if(accounting.InteriorCoverage is {} interiors)
        {
            b.Append("<h2>Constructed interior floors</h2><p>The gross domain is available search space. Each row measures the union of that assembly's exact room floors and generated walls. Clear floor subtracts all generated walls and solid source footprints in this coordinate domain once. Categories overlap; independent domains and assemblies must not be added as one surface. Optional floor requirements constrain this area during generation. These areas do not establish actor clearance, terrain suitability or runtime traversability.</p><table><tr><th>Assembly/scope</th><th>Purpose/rule</th><th>Room/wall sources</th><th>Floor union</th><th>Wall union</th><th>Clear floor</th><th>Required minimum</th><th>Result</th></tr>");
            foreach(var interior in interiors)
                b.Append($"<tr><td>{E(interior.RuleId)} · {E(interior.ScopeNodeId)}</td><td>{E(interior.Purpose)} · {E(interior.SourceRuleId)}</td><td>{interior.RoomNodeIds.Count} / {interior.Walls.Count}</td><td>{interior.FloorUnionArea.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)} {E(accounting.Unit)}²</td><td>{interior.WallUnionArea.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)} {E(accounting.Unit)}²</td><td>{interior.ClearFloorArea.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)} {E(accounting.Unit)}²</td><td>{(interior.Requirement is {} required?required.MinimumClearArea.ToString("G12",System.Globalization.CultureInfo.InvariantCulture)+" "+E(accounting.Unit)+"²; fraction "+required.MinimumClearFraction.ToString("G12",System.Globalization.CultureInfo.InvariantCulture):"Undeclared")}</td><td>{(interior.MinimumSatisfied is {} satisfied?satisfied?"Satisfied":"Unsatisfied":"No requirement")}</td></tr>");
            b.Append("</table>");
        }
        if(world.ChunkPlan is {} chunks)
            b.Append($"<h2>Spatial ownership</h2><p>{chunks.Cells.Count} occupied storage chunks · {chunks.Members.Count} unique members · {chunks.Members.Sum(m=>m.ChunkIds.Count)} cross-chunk references. Each positioned node and directed route has one canonical owner. Chunk-local origins preserve global geometry. Terrain and other plans remain shared source-package dependencies; this index does not implement runtime streaming.</p><p>{E(chunks.Definition.Purpose)} · source rule: {E(chunks.Definition.SourceRuleId)} · source world SHA-256: {E(chunks.SourceWorldSha256)}. <a href=\"chunk-index.json\">Complete ownership index</a></p>");
        b.Append("<h2>Semantic layers</h2><p>Dependency order is a declaration, not render order or proof of executed generation stages.</p><table><tr><th>Layer</th><th>Purpose/source rule</th><th>Depends on</th><th>Members</th><th>Scope</th></tr>");
        foreach(var layer in world.LayerPlan.Layers)
        {
            token.ThrowIfCancellationRequested();var d=layer.Definition;
            b.Append($"<tr><td>{E(d.Id)} · {E(d.Kind)}</td><td>{E(d.Purpose)} · {E(d.SourceRuleId)}</td><td>{E(string.Join(", ",d.DependsOn))}</td><td>{layer.NodeIds.Count} nodes, {layer.ConnectionIds.Count} links</td><td>{d.Coverage}: {E(string.Join(", ",d.RemainingScopes))}</td></tr>");
        }
        b.Append($"</table><p>Without a declared layer: {world.LayerPlan.UnassignedNodeIds.Count} nodes and {world.LayerPlan.UnassignedConnectionIds.Count} links. Layer membership does not certify physical coverage.</p>");
        var children=world.Nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.ToArray());
        int shown=0;
        Node(world.Nodes.Single(n=>n.ParentId is null));
        if(world.Nodes.Count>shown)b.Append("<p>First 2000 nodes shown; all nodes are in the JSON.</p>");
        b.Append("<h2>Connections</h2><table><tr><th>Kind</th><th>From</th><th>To</th><th>Direction</th></tr>");
        // Large graphs stay in JSON; the review is bounded and explicitly says when truncated.
        foreach(var link in world.Connections.Take(500))b.Append($"<tr><td>{E(link.Kind)}</td><td>{E(link.FromId)}</td><td>{E(link.ToId)}</td><td>{(link.Bidirectional?"both":"forward")}</td></tr>");
        if(world.Connections.Count>500)b.Append("<tr><td colspan=\"4\">First 500 connections shown; all connections are in the JSON.</td></tr>");
        b.Append("</table><p><a href=\"world-metadata.json\">Complete metadata</a> · <a href=\"recipe.json\">Recipe</a> · <a href=\"catalog.json\">Reusable catalog</a> · <a href=\"summary.json\">Summary</a></p></html>");return b.ToString();
        void Node(WorldMetadataNode node)
        {
            token.ThrowIfCancellationRequested();
            if(shown>=2000)return;
            shown++;
            b.Append($"<details{(node.ParentId is null?" open":"")}><summary>{E(node.Kind)} · {E(node.Id)}</summary><p>{E(node.Purpose)}</p>");
            if(node.Position is not null)b.Append("<p>"+E(string.Join(", ",node.Position.Select(p=>p.ToString("R",System.Globalization.CultureInfo.InvariantCulture))))+" "+E(world.Space.Unit)+"</p>");
            if(node.StructureKey is not null)b.Append("<p>Catalog structure: "+E(node.StructureKey)+"</p>");
            if(node.Properties.Count>0)
            {
                string properties=JsonSerializer.Serialize(node.Properties,WorldJson.CreateOptions(true));
                bool preview=properties.Length>1024;
                b.Append("<pre>"+E(preview?properties[..1024]:properties)+"</pre>");
                if(preview)b.Append("<p>Property preview limited to 1024 characters; complete values remain in the world metadata.</p>");
            }
            if(children.TryGetValue(node.Id,out var nested))foreach(var child in nested)Node(child);
            b.Append("</details>");
        }
    }
}
