using WorldCreator.Catalogs.Modern;
using WorldCreator.Core;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Planning;
using WorldCreator.Worlds;
using System.Text.Json;
using WorldCreator.Core.Capture;
using WorldCreator.Cli;
using WorldCreator.Core.Metadata;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-world <request.json> [output-dir] [--capture <capture.json> --asset-root <trusted-root>]");
    Console.WriteLine("       verify-world <world.json> <manifest-directory> <trusted-asset-root>");
    Console.WriteLine("       list-catalogs [--catalog-plugin <trusted-assembly.dll>] ");
    Console.WriteLine("       list-buildings <catalog-id> [category]");
    Console.WriteLine("       list-objects <catalog-id> [kind]");
    Console.WriteLine("       list-areas <catalog-id> [kind]");
    Console.WriteLine("       save-catalog-content <manifest.json> <catalog-directory> [--replace]");
    Console.WriteLine("       --catalog-content <manifest.json> extends a source catalog before composition.");
    Console.WriteLine("       list-facilities <catalog-id> [--catalog-composition <manifest.json>] ");
    Console.WriteLine("       --catalog-composition <manifest.json> registers explicit catalog imports for listing/generation.");
    Console.WriteLine("       review-capture <capture.json> <trusted-image-root> <new-output-dir>");
    Console.WriteLine("       compile-capture <capture.json> <trusted-image-root> <new-output-dir> (calibrated geometry draft)");
    Console.WriteLine("       capture-schema (current capture JSON Schema)");
    Console.WriteLine("       structure-pipeline reconcile <handoff.json> <capture.json> <bindings.json> <trusted-image-root> <new-report.json>");
    Console.WriteLine("       structure-pipeline ground <handoff.json> <ground-request.json> <trusted-image-root> <new-output-dir>");
    Console.WriteLine("       structure-pipeline scene <ground-compilation.json> <scene-request.json> <trusted-image-root> <new-output-dir>");
    Console.WriteLine("       structure-pipeline production-inventory <scene-compilation.json> <trusted-image-root> <new-inventory.json>");
    Console.WriteLine("       structure-pipeline production-bind <scene-compilation.json> <bindings.json> <trusted-image-root> <gamearts-consumer.json> <new-assembly.json>");
    Console.WriteLine("       structure-pipeline native-static <scene-compilation.json> <assembly.json> <topology.json> <native-spec.json> <trusted-image-root> <gamearts-consumer.json> <new-output-dir>");
    Console.WriteLine("       structure-pipeline tile-topology <world-metadata.json> <tile-request.json> <terrain-groups.json> <new-output-dir>");
    Console.WriteLine("       structure-pipeline plan <world.json> <new-library.json>");
    Console.WriteLine("       structure-pipeline metadata <world-metadata.json> <recipe.json> <catalog.json> <scopes.json> <new-handoff.json>");
    Console.WriteLine("       study-metadata <study.json> <new-output-dir> (retains generated and rejected cases)");
    foreach(string usage in MetadataPopulationSelectionCommand.Usage)Console.WriteLine("       "+usage);
    Console.WriteLine("       generate-metadata <recipe.json> <catalog.json> <new-output-dir> (mockup-free, engine-independent)");
    Console.WriteLine("       "+MetadataPublicationCommand.Usage+" (read-only complete source replay)");
    Console.WriteLine("       generate-world-domains <definition.json> <new-output-dir> (independent domains and explicit transitions)");
    Console.WriteLine("       generate-regional-domain-world <definition.json> <new-output-dir> (regional world and independent domains)");
    foreach(string usage in RegionalWorldCommand.Usage)Console.WriteLine("       "+usage);
    foreach(string usage in RegionalSessionCommand.Usage)Console.WriteLine("       "+usage);
    Console.WriteLine("       domain-session start <definition.json> <new-output-dir>");
    Console.WriteLine("       domain-session edit <domain-session.json> <edit.json> <new-output-dir>");
    Console.WriteLine("       metadata-session start <recipe.json> <catalog.json> <protections.json> [<position-protections.json>] <new-output-dir>");
    Console.WriteLine("       metadata-session edit <generation-session.json> <edit.json> <new-output-dir>");
    Console.WriteLine("       structure-pipeline build <world.json> <library.json> <trusted-asset-root> <new-output-dir>");
    Console.WriteLine("verify-world checks dependency integrity; lifecycle approval and gameplay readiness require separate gates.");
    return;
}
var operation = args[0]=="region-session"?"world.regions.session":RegionalWorldCommand.Handles(args[0])?RegionalWorldCommand.Operation:args[0] switch { "select-population"=>"world.population-selection", "verify-population"=>"world.population-selection.verify", "study-metadata"=>"world.study", "domain-session"=>"world.domains.session", "generate-world-domains"=>"world.domains", "generate-regional-domain-world"=>"world.regional-domains", "metadata-session"=>"world.metadata.session", "verify-metadata"=>MetadataPublicationCommand.Operation, "generate-metadata" => "world.metadata", "structure-pipeline" => "world.structure-pipeline", "verify-world" => "world.verify", "review-capture" or "compile-capture" or "capture-schema" => "world.capture", "list-buildings" or "list-catalogs" or "list-areas" or "list-objects" or "list-facilities" or "save-catalog-content" => "world.catalog", _ => "world.generate" };
var id = Guid.NewGuid().ToString("N");
var eventRoot = Path.Combine(Environment.CurrentDirectory, ".gameframework", "operations");
var targetName = args[0] == "generate-world" ? args.Length > 2 ? Path.GetFileName(args[2]) : "world"
    : args.Length > 1 ? Path.GetFileName(args[1]) : null;
var reporter = new WorldOperationReporter(
    new JsonLineOperationStore(Path.Combine(eventRoot, "log.jsonl")),
    new JsonLineOperationStore(Path.Combine(eventRoot, "audit.jsonl")),
    new ConsoleAndFileNotificationService(new JsonLineOperationStore(Path.Combine(eventRoot, "notifications.jsonl"))));
OperationEvent Entry(string eventId, OperationSeverity severity, string message, string outcome) =>
    new(eventId, DateTimeOffset.UtcNow, severity, "WorldCreator.Cli", operation, id, message, targetName, "local-cli", outcome);
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancelHandler;
var loadedCatalogPlugins=new List<SettlementCatalogPlugin>();
var compositionDefinitions=new List<CatalogCompositionDefinition>();
var contentFiles=new List<string>();
MetadataStudyDefinition? studyDefinition=null;
bool operationCompleted=false;string? completedOutputPath=null;
void CompleteOperation(string? outputPath=null)
{operationCompleted=true;if(outputPath is not null)completedOutputPath=Path.GetFullPath(outputPath);}
try
{
    var commandArguments=args.ToList();
    string? captureInput = null, captureAssetRoot = null;
    for (int index=1;index<commandArguments.Count;)
    {
        if (commandArguments[index] is not ("--catalog-plugin" or "--catalog-composition" or "--catalog-content")) { index++; continue; }
        if (index+1>=commandArguments.Count) throw new ArgumentException("Expected a value for "+commandArguments[index]);
        if (commandArguments[index]=="--catalog-content") contentFiles.Add(Path.GetFullPath(commandArguments[index+1]));
        else if (commandArguments[index]=="--catalog-composition")
            compositionDefinitions.AddRange(SettlementCatalogComposition.ReadManifest(commandArguments[index+1]).Catalogs);
        else loadedCatalogPlugins.Add(new SettlementCatalogPlugin(commandArguments[index+1],cancellation.Token));
        commandArguments.RemoveRange(index,2);
    }
    if (commandArguments[0]=="generate-world")
    {
        for (int index=1;index<commandArguments.Count;)
        {
            var flag=commandArguments[index];
            if (flag is not ("--capture" or "--asset-root")) { index++; continue; }
            if (index+1>=commandArguments.Count) throw new ArgumentException("Capture options require a value.");
            if (flag=="--capture") { if(captureInput is not null) throw new ArgumentException("Duplicate --capture."); captureInput=commandArguments[index+1]; }
            else { if(captureAssetRoot is not null) throw new ArgumentException("Duplicate --asset-root."); captureAssetRoot=commandArguments[index+1]; }
            commandArguments.RemoveRange(index,2);
        }
        if ((captureInput is null)!=(captureAssetRoot is null)) throw new ArgumentException("--capture and --asset-root must be supplied together.");
    }
    args=commandArguments.ToArray();
    if(MetadataPopulationSelectionCommand.Handles(args[0])&&loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0&&
        !await MetadataPopulationSelectionCommand.RequiresRegistryAsync(args,cancellation.Token))
    {await RunPopulationSelectionAsync(null);return;}
    if(args[0]=="region-session"&&loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0&&
        !await RegionalSessionCommand.RequiresRegistryAsync(args,cancellation.Token))
    {await GenerateRegionalSessionAsync(null);return;}
    if(args[0]=="metadata-session"&&loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0&&
        !await MetadataSessionCommand.RequiresRegistryAsync(args,cancellation.Token))
    {
        await GenerateMetadataSessionAsync(null);return;
    }
    if(args[0]=="domain-session")
    {
        var definitions=new List<MetadataWorldDomainDefinition>();
        if(args.Length==4&&args[1]=="start")definitions.Add(await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDefinition>(args[2],cancellation.Token));
        else if(args.Length==5&&args[1]=="edit")
        {
            var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataDomainSessionCheckpoint>(args[2],cancellation.Token);
            definitions.Add(checkpoint.InitialDefinition);definitions.AddRange(checkpoint.Edits.Select(e=>e.Request.Definition));
            definitions.Add((await WorldMetadataGenerator.ReadAsync<MetadataDomainEditRequest>(args[3],cancellation.Token)).Definition);
        }
        else throw new ArgumentException("Expected domain-session start <definition.json> <new-output-dir> or edit <domain-session.json> <edit.json> <new-output-dir>.");
        if(definitions.All(d=>d?.Domains is not null&&d.Domains.All(w=>w is not null&&!MetadataSessionCommand.RequiresRegistry(w.Recipe,w.Catalog)))&&
            loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0)
        {await GenerateDomainSessionAsync(null);return;}
    }
    if(args[0]=="generate-world-domains")
    {
        if(args.Length!=3)throw new ArgumentException("Expected generate-world-domains <definition.json> <new-output-dir>.");
        var domains=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDefinition>(args[1],cancellation.Token);
        if(domains.Domains is not null&&domains.Domains.All(d=>d is not null&&!MetadataSessionCommand.RequiresRegistry(d.Recipe,d.Catalog))&&
            loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0)
        {await GenerateWorldDomainsAsync(null);return;}
    }
    if(args[0]=="generate-regional-domain-world")
    {
        if(args.Length!=3)throw new ArgumentException("Expected generate-regional-domain-world <definition.json> <new-output-dir>.");
        var input=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDomainWorldDefinition>(args[1],cancellation.Token);
        bool genericRegions=input.RegionalWorld?.Regions is {} direct&&
            direct.All(r=>r is not null&&!MetadataSessionCommand.RequiresRegistry(r.Recipe,r.Catalog));
        bool genericDesign=input.RegionalDesign?.Template?.Regions is {} template&&input.RegionalDesign.Slots is {} slots&&
            template.All(r=>r is not null&&!MetadataSessionCommand.RequiresRegistry(r.Recipe,r.Catalog))&&
            slots.All(s=>s?.Options is not null&&s.Options.All(o=>o is not null&&!MetadataSessionCommand.RequiresRegistry(o.Recipe,o.Catalog)));
        if((genericRegions||genericDesign)&&input.AdditionalDomains is not null&&
            input.AdditionalDomains.All(d=>d is not null&&!MetadataSessionCommand.RequiresRegistry(d.Recipe,d.Catalog))&&
            loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0)
        {await GenerateRegionalDomainWorldAsync(null);return;}
    }
    if(args[0]=="study-metadata")
    {
        if(args.Length!=3)throw new ArgumentException("Expected study-metadata <study.json> <new-output-dir>.");
        var study=await WorldMetadataGenerator.ReadAsync<MetadataStudyDefinition>(args[1],cancellation.Token);
        MetadataGenerationStudy.ValidateDefinition(study);studyDefinition=study;
        if(study.Cases is not null&&study.Cases.All(c=>c is not null&&!MetadataSessionCommand.RequiresRegistry(c.Recipe,c.Catalog))&&
            loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0)
        {await GenerateStudyAsync(null);return;}
    }
    if(RegionalWorldCommand.Handles(args[0])&&loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0&&
        !await RegionalWorldCommand.RequiresRegistryAsync(args,cancellation.Token))
    {await GenerateRegionsAsync(null);return;}
    if(args[0]=="verify-metadata"&&loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0&&
        !await MetadataPublicationCommand.RequiresRegistryAsync(args,cancellation.Token))
    {await VerifyMetadataAsync(null);return;}
    if (args[0] == "generate-metadata")
    {
        if (args.Length != 4) throw new ArgumentException("Expected generate-metadata <recipe.json> <catalog.json> <new-output-dir>.");
        var metadataCatalog = await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(args[2], cancellation.Token);
        var metadataRecipe = await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(args[1], cancellation.Token);
        if (!MetadataSessionCommand.RequiresRegistry(metadataRecipe,metadataCatalog) &&
            loadedCatalogPlugins.Count == 0 && compositionDefinitions.Count == 0 && contentFiles.Count == 0)
        {
            // Generic metadata must not depend on unrelated settlement manifests in the workspace.
            await GenerateMetadataAsync(null);
            return;
        }
    }
    if (args[0] == "structure-pipeline" && (args.Length<2||args[1] is not ("metadata" or "reconcile" or "ground" or "scene" or "production-inventory" or "production-bind" or "native-static" or "tile-topology")||
        loadedCatalogPlugins.Count==0&&compositionDefinitions.Count==0&&contentFiles.Count==0&&!await StructureFirstCommand.RequiresRegistryAsync(args,cancellation.Token)))
    {
        await GenerateStructurePipelineAsync(null);return;
    }
    if (args[0] == "capture-schema")
    {
        if (args.Length != 1) throw new ArgumentException("Expected capture-schema.");
        Console.WriteLine(MockupCaptureJson.Schema().ToJsonString(MockupCaptureJson.Options()));
        return;
    }
    if (args[0] is "review-capture" or "compile-capture")
    {
        if (args.Length != 4) throw new ArgumentException("Expected capture command <capture.json> <trusted-image-root> <new-output-dir>.");
        await reporter.ReportAsync(Entry("world.capture.started", OperationSeverity.Information, "Source-pixel capture verification started.", "started"));
        var capture = await MockupCapturePipeline.ReadCaptureAsync(args[1], cancellation.Token);
        var bytes = await MockupCapturePipeline.ReadSourceAsync(capture, args[2], cancellation.Token);
        var report = MockupCaptureValidator.Validate(capture, cancellation.Token);
        var overlay = MockupCapturePipeline.Overlay(capture, bytes, cancellation.Token);
        var html = MockupCapturePipeline.ReviewHtml(capture, bytes, cancellation.Token);
        var prompts = MockupCapturePipeline.Prompts(capture, cancellation.Token);
        var vegetation = MockupCapturePipeline.VegetationDraft(capture, cancellation.Token);
        var geometry = args[0] == "compile-capture" ? MockupCapturePipeline.GeometryDraft(capture, cancellation.Token) : null;
        var destination = Path.GetFullPath(args[3]);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new ArgumentException("CAPTURE_OUTPUT_EXISTS");
        var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("CAPTURE_OUTPUT_INVALID");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(staging, "capture.json"), JsonSerializer.Serialize(capture, MockupCaptureJson.Options()), cancellation.Token);
            await File.WriteAllTextAsync(Path.Combine(staging, "report.json"), JsonSerializer.Serialize(report, MockupCaptureJson.Options()), cancellation.Token);
            await File.WriteAllTextAsync(Path.Combine(staging, "overlay.svg"), overlay, cancellation.Token);
            await File.WriteAllTextAsync(Path.Combine(staging, "index.html"), html, cancellation.Token);
            await File.WriteAllTextAsync(Path.Combine(staging, "prompts.json"), JsonSerializer.Serialize(prompts, MockupCaptureJson.Options()), cancellation.Token);
            if (vegetation is not null) await File.WriteAllTextAsync(Path.Combine(staging, "vegetation-layout.json"), JsonSerializer.Serialize(vegetation, MockupCaptureJson.Options()), cancellation.Token);
            if (geometry is not null) await File.WriteAllTextAsync(Path.Combine(staging, "geometry-draft.json"), JsonSerializer.Serialize(geometry, MockupCaptureJson.Options()), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        CompleteOperation(destination);
        await reporter.ReportAsync(Entry("world.capture.succeeded", OperationSeverity.Information,
            "Source verified; capture overlay and prompt drafts exported. No art approval or production world granted.", "succeeded"), notify: true);
        Console.WriteLine($"Capture review exported to {destination}; {report.Crossings.Count(c => c.Kind == CaptureJunctionKind.Unresolved)} unresolved projected crossings.");
        return;
    }
    var projectCatalogDirectory=Path.Combine(Environment.CurrentDirectory,".gameframework","catalogs");
    if (Directory.Exists(projectCatalogDirectory)) contentFiles.AddRange(Directory.GetFiles(projectCatalogDirectory,"*.json",SearchOption.TopDirectoryOnly));
    var contents=contentFiles.Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal)
        .Order(StringComparer.Ordinal).Select(CatalogContentAuthoring.ReadManifest).ToList();
    CatalogContentManifest? savedContent=null;
    if (args[0]=="save-catalog-content")
    {
        if (args.Length is <3 or >4 || args.Length==4 && args[3]!="--replace") throw new ArgumentException("Expected save-catalog-content <manifest> <directory> [--replace].");
        savedContent=CatalogContentAuthoring.ReadManifest(args[1]);
        contents.RemoveAll(m=>m.Id==savedContent.Id && m.CatalogId==savedContent.CatalogId);
        contents.Add(savedContent);
    }
    var authoredProviders=CatalogContentAuthoring.Apply(new ISettlementCatalogProvider[] { new ModernCatalog() }.Concat(loadedCatalogPlugins.SelectMany(p=>p.Providers)),contents,cancellation.Token);
    var catalogProviders=SettlementCatalogComposition.Register(authoredProviders,compositionDefinitions,cancellation.Token).ToArray();
    var catalogRegistry=new SettlementCatalogRegistry(catalogProviders,cancellation.Token);
    if(MetadataPopulationSelectionCommand.Handles(args[0])){await RunPopulationSelectionAsync(catalogRegistry);return;}
    if(RegionalWorldCommand.Handles(args[0])){await GenerateRegionsAsync(catalogRegistry);return;}
    if(args[0]=="region-session"){await GenerateRegionalSessionAsync(catalogRegistry);return;}
    if(args[0]=="structure-pipeline") {await GenerateStructurePipelineAsync(catalogRegistry);return;}
    if(args[0]=="metadata-session") {await GenerateMetadataSessionAsync(catalogRegistry);return;}
    if(args[0]=="domain-session") {await GenerateDomainSessionAsync(catalogRegistry);return;}
    if(args[0]=="generate-world-domains") {await GenerateWorldDomainsAsync(catalogRegistry);return;}
    if(args[0]=="generate-regional-domain-world") {await GenerateRegionalDomainWorldAsync(catalogRegistry);return;}
    if(args[0]=="study-metadata"){await GenerateStudyAsync(catalogRegistry);return;}
    if(args[0]=="verify-metadata"){await VerifyMetadataAsync(catalogRegistry);return;}
    if(args[0]=="generate-metadata")
    {
        await GenerateMetadataAsync(catalogRegistry);
        return;
    }
    if (savedContent is not null)
    {
        var savedPath=await CatalogContentAuthoring.SaveManifestAsync(savedContent,args[2],args.Length==4,cancellation.Token);
        CompleteOperation(savedPath);
        await reporter.ReportAsync(Entry("world.catalog.content.saved",OperationSeverity.Information,
            $"Saved reusable catalog content {savedContent.Id} for {savedContent.CatalogId}.","succeeded"));
        Console.WriteLine(savedPath);
        return;
    }
    foreach (var content in contents)
        await reporter.ReportAsync(Entry("world.catalog.content.registered",OperationSeverity.Information,
            $"Content {content.Id} for {content.CatalogId}; sha256 {catalogRegistry.Catalogs.Single(d=>d.Id==content.CatalogId).AuthoredContentDigests![content.Id]}.","registered"));
    if (args[0]=="list-catalogs")
    {
        if (args.Length!=1) throw new ArgumentException("Expected list-catalogs");
        Console.WriteLine(JsonSerializer.Serialize(catalogRegistry.Catalogs,WorldJson.CreateOptions()));
        CompleteOperation();
        await reporter.ReportAsync(Entry("world.catalog.succeeded",OperationSeverity.Information,"Registered catalogs listed.","succeeded"));
        return;
    }
    if (args[0]=="list-facilities")
    {
        if (args.Length!=2) throw new ArgumentException("Expected list-facilities <catalog-id>.");
        var descriptor=catalogRegistry.Catalogs.SingleOrDefault(d=>d.Id==args[1]) ?? throw new ArgumentException("CATALOG_NOT_REGISTERED:"+args[1]);
        var catalog=catalogRegistry.Resolve(new(args[1],descriptor.Capabilities));
        Console.WriteLine(JsonSerializer.Serialize(new { Catalog=catalog.Provenance, Facilities=catalog.Facilities },WorldJson.CreateOptions()));
        CompleteOperation();
        await reporter.ReportAsync(Entry("world.catalog.succeeded",OperationSeverity.Information,"Facility catalog listed.","succeeded"));
        return;
    }
    if(args[0]=="list-areas")
    {
        if(args.Length is <2 or >3)throw new ArgumentException("Expected list-areas <catalog-id> [kind].");
        var descriptor=catalogRegistry.Catalogs.SingleOrDefault(d=>d.Id==args[1])??throw new ArgumentException("CATALOG_NOT_REGISTERED:"+args[1]);
        var catalog=catalogRegistry.Resolve(new(args[1],descriptor.Capabilities));string? kind=args.Length==3?args[2]:null;
        if(kind is not null&&!(catalog.Areas??[]).Any(a=>a.Kind==kind))throw new ArgumentException("SITE_AREA_KIND_UNKNOWN");
        var definitions=(catalog.Areas??[]).Where(a=>kind is null||a.Kind==kind).OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray();
        cancellation.Token.ThrowIfCancellationRequested();
        var payload=new{Catalog=catalog.Provenance,AreaCount=definitions.Length,VariantCount=definitions.Sum(a=>a.Variants.Count),Kinds=definitions.Select(a=>a.Kind).Distinct().Order(StringComparer.Ordinal).ToArray(),Definitions=definitions};
        Console.WriteLine(JsonSerializer.Serialize(payload,WorldJson.CreateOptions()));
        CompleteOperation();
        await reporter.ReportAsync(Entry("world.catalog.succeeded",OperationSeverity.Information,$"Area catalog read ({payload.AreaCount} areas, {payload.VariantCount} variants).","succeeded"));return;
    }
    if(args[0]=="list-objects")
    {
        if(args.Length is <2 or >3)throw new ArgumentException("Expected list-objects <catalog-id> [kind].");
        var descriptor=catalogRegistry.Catalogs.SingleOrDefault(d=>d.Id==args[1])??throw new ArgumentException("CATALOG_NOT_REGISTERED:"+args[1]);
        var catalog=catalogRegistry.Resolve(new(args[1],descriptor.Capabilities));
        string? kind=args.Length==3?args[2]:null;
        if(kind is not null&&!(catalog.Objects??[]).Any(o=>o.Kind==kind))throw new ArgumentException("SITE_OBJECT_KIND_UNKNOWN");
        var definitions=(catalog.Objects??[]).Where(o=>kind is null||o.Kind==kind).OrderBy(o=>o.Id,StringComparer.Ordinal).ToArray();
        cancellation.Token.ThrowIfCancellationRequested();
        var payload=new{Catalog=catalog.Provenance,ObjectCount=definitions.Length,VariantCount=definitions.Sum(o=>o.Variants.Count),Kinds=definitions.Select(o=>o.Kind).Distinct().Order(StringComparer.Ordinal).ToArray(),Definitions=definitions};
        Console.WriteLine(JsonSerializer.Serialize(payload,WorldJson.CreateOptions()));
        CompleteOperation();
        await reporter.ReportAsync(Entry("world.catalog.succeeded",OperationSeverity.Information,$"Object catalog read ({payload.ObjectCount} objects, {payload.VariantCount} variants).","succeeded"));
        return;
    }
    if (args[0] == "list-buildings")
    {
        if (args.Length is <2 or >3) throw new ArgumentException("Expected list-buildings <catalog-id> [category].");
        var descriptor=catalogRegistry.Catalogs.SingleOrDefault(d=>d.Id==args[1]) ?? throw new ArgumentException("CATALOG_NOT_REGISTERED:"+args[1]);
        var catalog=catalogRegistry.Resolve(new(args[1],descriptor.Capabilities));
        string? selected=args.Length==3?args[2]:null;
        if (selected is not null && !descriptor.Vocabulary.Categories.Contains(selected)) throw new ArgumentException("Unknown building category.");
        await reporter.ReportAsync(Entry("world.catalog.started",OperationSeverity.Information,"Building catalog read started.","started"));
        var definitions = catalog.Structures.Where(d => selected is null || d.Category == selected)
            .OrderBy(d => d.Id,StringComparer.Ordinal).ToArray();
        var payload = new { Catalog=catalog.Provenance, FamilyCount = definitions.Length,
            VariantCount = definitions.Sum(d => d.SizeVariants?.Count ?? 0),
            Categories = definitions.Select(d => d.Category).Distinct().Order().ToArray(), Definitions = definitions };
        cancellation.Token.ThrowIfCancellationRequested();
        Console.WriteLine(JsonSerializer.Serialize(payload,WorldJson.CreateOptions()));
        CompleteOperation();
        await reporter.ReportAsync(Entry("world.catalog.succeeded",OperationSeverity.Information,
            $"Building catalog read ({payload.FamilyCount} families, {payload.VariantCount} variants).","succeeded"));
        return;
    }
    if (args[0] == "verify-world")
    {
        if (args.Length != 4)
            throw new ArgumentException("Expected verify-world <world.json> <manifest-directory> <trusted-asset-root>.");
        await reporter.ReportAsync(Entry("world.verify.started", OperationSeverity.Information,
            "Portable world integrity audit started.", "started"));
        var verified = await new PortableWorldPackageVerifier(new FileSystemArtRegistry(args[2]))
            .VerifyAsync(args[1], args[3], cancellation.Token);
        CompleteOperation();
        await reporter.ReportAsync(Entry(verified.Valid ? "world.verify.succeeded" : "world.verify.failed",
            verified.Valid ? OperationSeverity.Information : OperationSeverity.Warning,
            verified.Valid ? "Portable world dependency integrity verified."
                : $"Portable world integrity audit rejected ({verified.Issues.Count} issues).",
            verified.Valid ? "succeeded" : "failed"), notify: true);
        Console.WriteLine(JsonSerializer.Serialize(verified));
        Environment.ExitCode = verified.Valid ? 0 : 1;
        return;
    }
    if (args[0] != "generate-world") throw new ArgumentException("Supported: generate-world, verify-world, list-catalogs, list-buildings, list-objects, list-areas, list-facilities, save-catalog-content, review-capture or compile-capture.");
    if (args.Length is < 2 or > 3) throw new ArgumentException("Expected generate-world <request.json> [output-dir].");
    var output = args.Length > 2 ? args[2] : "generated/world";
    await reporter.ReportAsync(Entry("world.generate.started", OperationSeverity.Information, "Mockup-driven world generation started.", "started"));
    await using var requestStream = File.OpenRead(args[1]);
    byte[]? captureSourcePng = null;
    var request = await JsonSerializer.DeserializeAsync<WorldRequest>(requestStream, WorldJson.CreateOptions(),cancellation.Token)
        ?? throw new ArgumentException("World generation request is empty.");
    if (captureInput is not null)
    {
        var capture = await MockupCapturePipeline.ReadCaptureAsync(captureInput,cancellation.Token);
        captureSourcePng = await MockupCapturePipeline.ReadSourceAsync(capture,captureAssetRoot!,cancellation.Token);
        request=request with { Design=MockupCapturePipeline.ApplyToDesign(capture,request.Design ?? throw new ArgumentException("MOCKUP_DESIGN_REQUIRED"),cancellation.Token) };
        await reporter.ReportAsync(Entry("world.generate.capture.applied",OperationSeverity.Information,
            $"Verified capture {capture.Source.MockupId}; source {capture.Source.Sha256}; scope {capture.CompilationScope?.Id ?? "complete"}.","applied"));
    }
    var world = await new MockupWorldGenerator(catalogRegistry).GenerateAsync(request, cancellation.Token);
    await reporter.ReportAsync(Entry("world.generate.catalog.selected",OperationSeverity.Information,
        $"Catalog {world.Catalog!.Id}; policy {world.Catalog.PolicyId}; content {world.Catalog.ContentSha256}; capabilities {string.Join(',',world.Catalog.EnabledCapabilities.Order(StringComparer.Ordinal))}.","selected"));
    await reporter.ReportAsync(Entry("world.generate.siting.passed",OperationSeverity.Information,"Contextual siting rules and capacity evaluated.","passed"));
    if(world.Request.Design!.SurfaceClearances is {Count:>0} spacing)
        await reporter.ReportAsync(Entry("world.generate.surface-clearance.passed",OperationSeverity.Information,
            $"Validated {spacing.Count} authored surface-spacing rules against continuous road/sport/building geometry and fixed tree bases.","passed"));
    var palette=catalogProviders.Where(p=>p.Descriptor.Id==world.Catalog!.Id).OfType<IWorldPlanPalette>().SingleOrDefault();
    var planRenderer=new WorldPlanSvgRenderer(palette);
    var capturePreview=captureSourcePng is null?null:new CaptureWorldPreviewRenderer(planRenderer).Render(world,captureSourcePng,cancellation.Token);
    var captureDetails=captureSourcePng is null?null:MockupCapturePipeline.ReviewHtml(world.Request.Design!.Capture!,captureSourcePng,cancellation.Token);
    var capturePlants=captureSourcePng is null?null:MockupCapturePipeline.VegetationDraft(world.Request.Design!.Capture!,cancellation.Token);
    await new WorldDocumentExporter(planRenderer).ExportAsync(world, output, cancellation.Token);
    if(captureSourcePng is not null)
    {
        await File.WriteAllTextAsync(Path.Combine(output,"capture-review.html"),capturePreview,cancellation.Token);
        await File.WriteAllTextAsync(Path.Combine(output,"source-details.html"),captureDetails,cancellation.Token);
        if(capturePlants is not null)await File.WriteAllTextAsync(Path.Combine(output,"vegetation-layout.json"),JsonSerializer.Serialize(capturePlants,MockupCaptureJson.Options()),cancellation.Token);
    }
    CompleteOperation(output);
    await reporter.ReportAsync(Entry("world.generate.succeeded", OperationSeverity.Information, "Mockup-driven world JSON and SVG exported.", "succeeded"), notify: true);
    Console.WriteLine($"Generated {world.Width}x{world.Height} mockup-driven world at {Path.GetFullPath(output)}");
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    string? recoveredOutput=completedOutputPath??(ex switch{
        MetadataSessionReportingException e=>e.OutputDirectory,MetadataDomainSessionReportingException e=>e.OutputDirectory,
        MetadataRegionalSessionReportingException e=>e.OutputDirectory,MetadataPopulationSelectionReportingException e=>e.OutputDirectory,
        MetadataChunkGeometryReportingException e=>e.OutputDirectory,MetadataDomainChunkReportingException e=>e.OutputDirectory,
        MetadataStudyReportingException e=>e.OutputDirectory,_=>null});
    bool committed = recoveredOutput is not null||ex is MetadataSessionReportingException or MetadataDomainSessionReportingException or MetadataRegionalSessionReportingException or MetadataPopulationSelectionReportingException or MetadataChunkGeometryReportingException or MetadataDomainChunkReportingException;
    bool completed = operationCompleted||committed||ex is MetadataStudyReportingException;
    bool cancelled = !completed&&ex is OperationCanceledException&&cancellation.IsCancellationRequested;
    string state=committed?"committed; reporting failed":completed?"completed; reporting failed":cancelled?"cancelled":"failed";
    var context=new Dictionary<string,string>{{"completed",completed?"true":"false"},{"committed",committed?"true":"false"}};
    if(recoveredOutput is not null)context["outputPath"]=recoveredOutput;
    var failure = Entry(operation + (completed ? ".reporting.failed" : cancelled ? ".cancelled" : ".failed"),
        cancelled ? OperationSeverity.Warning : OperationSeverity.Error,
        $"World operation {state} ({ex.GetType().Name}).",
        committed?"committed-reporting-failed":completed?"completed-reporting-failed":cancelled?"cancelled":"failed") with{Context=context};
    try { await reporter.ReportAsync(failure, notify: true); }
    catch (Exception reportingException)
    {
        Console.Error.WriteLine($"Operational reporting also failed: {reportingException.GetType().Name}");
    }
    Console.Error.WriteLine($"World operation {state}: {ex.Message}");
    if(recoveredOutput is not null)Console.Error.WriteLine($"Published output: {recoveredOutput}");
    foreach(var diagnostic in PlacementFailureDisplay.Describe(ex))Console.Error.WriteLine(diagnostic);
    Environment.ExitCode = cancelled ? 130 : 1;
}
finally { Console.CancelKeyPress -= cancelHandler;
    foreach (var plugin in loadedCatalogPlugins) plugin.Dispose(); }

async Task GenerateStructurePipelineAsync(ISettlementCatalogRegistry? registry)
{
    await reporter.ReportAsync(Entry("world.structure-pipeline.started",OperationSeverity.Information,"Structure pipeline started.","started"));
    var summary=await StructureFirstCommand.RunAsync(args,cancellation.Token,reporter,registry);
    CompleteOperation(args[^1]);
    await reporter.ReportAsync(Entry("world.structure-pipeline.succeeded",OperationSeverity.Information,summary,"succeeded"),notify:true);
    Console.WriteLine(summary);
}

async Task RunPopulationSelectionAsync(ISettlementCatalogRegistry? registry)
{
    string summary=await MetadataPopulationSelectionCommand.RunAsync(args,registry,reporter,cancellation.Token);
    CompleteOperation(args[0]=="verify-population"?null:args[^1]);Console.WriteLine(summary);
}

async Task GenerateMetadataAsync(ISettlementCatalogRegistry? registry)
{
    await reporter.ReportAsync(Entry("world.metadata.started", OperationSeverity.Information, "Metadata world generation started.", "started"));
    var metadata = await new WorldMetadataGenerator(registry).GenerateFilesAsync(args[1], args[2], args[3], cancellation.Token);
    CompleteOperation(args[3]);
    string summary = $"Generated {metadata.Nodes.Count} metadata nodes and {metadata.Connections.Count} connections; scope: {metadata.Coverage}.";
    await reporter.ReportAsync(Entry("world.metadata.succeeded", OperationSeverity.Information, summary, "succeeded"), notify: true);
    Console.WriteLine(summary);
}

async Task VerifyMetadataAsync(ISettlementCatalogRegistry? registry)
{
    await reporter.ReportAsync(Entry("world.metadata.verify.started",OperationSeverity.Information,"Published metadata source replay started.","started"));
    string summary=await MetadataPublicationCommand.RunAsync(args,registry,cancellation.Token);
    CompleteOperation();
    await reporter.ReportAsync(Entry("world.metadata.verify.succeeded",OperationSeverity.Information,summary,"succeeded"),notify:true);
    Console.WriteLine(summary);
}

async Task GenerateRegionsAsync(ISettlementCatalogRegistry? registry)
{
    await reporter.ReportAsync(Entry("world.regions.started",OperationSeverity.Information,"Regional content operation started.","started"));
    string summary=await RegionalWorldCommand.RunAsync(args,registry,cancellation.Token);
    CompleteOperation(args[0]=="verify-regional-design"?null:args[^1]);
    await reporter.ReportAsync(Entry("world.regions.succeeded",OperationSeverity.Information,summary,"succeeded"),notify:true);Console.WriteLine(summary);
}

async Task GenerateRegionalSessionAsync(ISettlementCatalogRegistry? registry)
{
    string summary=await RegionalSessionCommand.RunAsync(args,registry,new(reporter,"framework-host"),cancellation.Token);
    CompleteOperation(args[^1]);Console.WriteLine(summary);
}

async Task GenerateWorldDomainsAsync(ISettlementCatalogRegistry? registry)
{
    await reporter.ReportAsync(Entry("world.domains.started",OperationSeverity.Information,"World domain generation started.","started"));
    var domains=await new WorldMetadataGenerator(registry).GenerateDomainFilesAsync(args[1],args[2],cancellation.Token);
    CompleteOperation(args[2]);
    string summary=$"Generated {domains.Domains.Count} domains, {domains.Domains.Sum(d=>d.World.Nodes.Count)} local nodes and {domains.Transitions.Count} explicit transitions.";
    await reporter.ReportAsync(Entry("world.domains.succeeded",OperationSeverity.Information,summary,"succeeded"),notify:true);Console.WriteLine(summary);
}

async Task GenerateRegionalDomainWorldAsync(ISettlementCatalogRegistry? registry)
{
    await reporter.ReportAsync(Entry("world.regional-domains.started",OperationSeverity.Information,"Regional domain world generation started.","started"));
    var definition=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDomainWorldDefinition>(args[1],cancellation.Token);
    var generator=new MetadataRegionalDomainWorldGenerator(new WorldMetadataGenerator(registry));
    var world=generator.Generate(definition,cancellation.Token);
    await generator.PublishAsync(world,args[2],cancellation.Token);
    CompleteOperation(args[2]);
    string summary=$"Generated {world.RegionalWorld.Regions.Count} regions, {world.Domains.Domains.Count-world.RegionalWorld.Regions.Count} independent domains and {world.RegionalRoutes.Count} qualified regional routes.";
    await reporter.ReportAsync(Entry("world.regional-domains.succeeded",OperationSeverity.Information,summary,"succeeded"),notify:true);Console.WriteLine(summary);
}

async Task GenerateDomainSessionAsync(ISettlementCatalogRegistry? registry)
{
    var generator=new WorldMetadataGenerator(registry);MetadataDomainSessionSnapshot snapshot;
    if(args[1]=="start")
    {
        var definition=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDefinition>(args[2],cancellation.Token);
        var session=await MetadataDomainGenerationSession.StartAsync(generator,definition,new(reporter,"local-cli",id),cancellation.Token);
        await session.PublishAsync(args[3],cancellation.Token);snapshot=session.Snapshot;
    }
    else
    {
        var session=await MetadataDomainGenerationSession.RestoreAsync(generator,args[2],cancellation.Token,reporting:new(reporter,"local-cli",id));
        var edit=await WorldMetadataGenerator.ReadAsync<MetadataDomainEditRequest>(args[3],cancellation.Token);
        snapshot=await session.ApplyAndPublishAsync(edit,args[4],cancellation.Token);
    }
    CompleteOperation(args[^1]);
    string summary=$"Accepted {snapshot.World.Domains.Count} domains; reused {snapshot.Report.Domains.Sum(d=>d.Stages.Count(s=>s.Reused))}/{snapshot.Report.Domains.Sum(d=>d.Stages.Count)} local stages; cache bytes {snapshot.Report.CacheBytes}.";
    Console.WriteLine(summary);
}


async Task GenerateMetadataSessionAsync(ISettlementCatalogRegistry? registry)
{
    await reporter.ReportAsync(Entry("world.metadata.session.started",OperationSeverity.Information,"Metadata generation session started.","started"));
    var summary=await MetadataSessionCommand.RunAsync(args,registry,new(reporter,"local-cli",id),cancellation.Token);
    CompleteOperation(args[^1]);
    await reporter.ReportAsync(Entry("world.metadata.session.succeeded",OperationSeverity.Information,summary,"succeeded"),notify:true);
    Console.WriteLine(summary);
}

async Task GenerateStudyAsync(ISettlementCatalogRegistry? registry)
{
    var definition=studyDefinition??throw new ArgumentException("METADATA_STUDY_INPUT_REQUIRED");
    var report=await new MetadataGenerationStudy(new WorldMetadataGenerator(registry),reporter).PublishAsync(definition,args[2],cancellation.Token);
    CompleteOperation(args[2]);
    Console.WriteLine($"Study recorded {report.GeneratedCases} generated and {report.RejectedCases} rejected cases. Rejected cases have no world packet. Results: {Path.Combine(args[2],"study-report.json")}");
}
