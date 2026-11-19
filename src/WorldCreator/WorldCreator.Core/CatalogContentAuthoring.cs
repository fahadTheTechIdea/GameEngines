using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

public sealed record FacilityProgramPart(string Id, string DefinitionId, string SizeVariantId,
    string Purpose, int Count = 1, CatalogPartKind Kind = CatalogPartKind.Structure,
    IReadOnlyList<BuildingComponentRule>? Components = null);
public sealed record FacilityProgramChild(string Id, string DefinitionId, string Purpose, int Count = 1);
public sealed record FacilityFrontageGrammar(RoadCrossSection RoadProfile, double RoadOffsetMeters,
    double FrontSetbackMeters, double RearSetbackMeters, double SideSetbackMeters,
    double InterSiteGapMeters, double OuterMarginMeters, RoadClass RoadClass = RoadClass.Local,
    double? MaximumRowSpanMeters = null, double InterRowGapMeters = 8);
public sealed record FacilityProgramDefinition(string Id, string Name, string SourceRuleId,
    FacilityFrontageGrammar Layout, IReadOnlyList<FacilityProgramPart> Parts, IReadOnlyList<FacilityProgramChild> Children,SiteBoundaryRule? BoundaryRule=null);
public sealed record CatalogContentManifest(int SchemaVersion, string Id, string CatalogId, string SourceRuleId,
    IReadOnlyList<BuildingDefinition> Structures, IReadOnlyList<FacilityProgramDefinition> Programs,
    IReadOnlyList<FacilityDefinition> Facilities, IReadOnlySet<string>? ReplaceStructureIds = null,
    IReadOnlySet<string>? ReplaceFacilityIds = null,IReadOnlyList<SiteObjectDefinition>? Objects=null,IReadOnlySet<string>? ReplaceObjectIds=null,IReadOnlyList<SiteAreaDefinition>? Areas=null,IReadOnlySet<string>? ReplaceAreaIds=null);

/// <summary>Owns reusable catalog content authoring/loading and its common frontage grammar.
/// User intent is interpreted by the agent into these typed records, never a setting-specific
/// prompt branch or executable text. Registered pack policies validate all new definitions.</summary>
public static partial class CatalogContentAuthoring
{
    /// <summary>Materializes an explicit selection from the registered catalog snapshot.
    /// Reference-based requests and inline authoring cannot both own a definition list.</summary>
    public static MockupWorldDesign BindReferences(MockupWorldDesign design, ISettlementCatalogRegistry registry, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(design); token.ThrowIfCancellationRequested();
        if (design.BuildingCatalogIds is null) return design;
        if (design.BuildingCatalog is null || design.BuildingCatalog.Count != 0)
            throw new ArgumentException("CATALOG_STRUCTURE_REFERENCE_AMBIGUOUS");
        if (design.BuildingCatalogIds.Count > 10000 || design.BuildingCatalogIds.Any(id => !SettlementCatalogRegistry.ValidId(id)) ||
            design.BuildingCatalogIds.Distinct(StringComparer.Ordinal).Count() != design.BuildingCatalogIds.Count)
            throw new ArgumentException("CATALOG_STRUCTURE_REFERENCE_INVALID");
        var pack = registry.Resolve(design.Catalog!);
        var byId = pack.Structures.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var definitions = new List<BuildingDefinition>();
        foreach (var id in design.BuildingCatalogIds.Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (!byId.TryGetValue(id, out var definition)) throw new ArgumentException("CATALOG_STRUCTURE_REFERENCE_UNKNOWN:" + id);
            definitions.Add(definition);
        }
        return design with { BuildingCatalog = Array.AsReadOnly(definitions.ToArray()), BuildingCatalogIds = null };
    }

    public static CatalogContentManifest ReadManifest(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 16777216) throw new ArgumentException("CATALOG_CONTENT_SIZE_LIMIT");
        var options = WorldJson.CreateOptions(); options.RespectRequiredConstructorParameters = true;
        options.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
        var manifest = JsonSerializer.Deserialize<CatalogContentManifest>(stream, options)
            ?? throw new ArgumentException("CATALOG_CONTENT_INVALID");
        ValidateManifest(manifest); return manifest;
    }

    public static IReadOnlyList<ISettlementCatalogProvider> Apply(IEnumerable<ISettlementCatalogProvider> providers,
        IEnumerable<CatalogContentManifest> manifests, CancellationToken token = default)
    {
        var result = providers.ToList(); int count = 0;
        foreach (var manifest in OrderManifests(manifests, token))
        {
            token.ThrowIfCancellationRequested(); ValidateManifest(manifest);
            if (++count > 256) throw new ArgumentException("CATALOG_CONTENT_BUDGET");
            int index = result.FindIndex(p => p.Descriptor.Id == manifest.CatalogId);
            if (index < 0) throw new ArgumentException("CATALOG_CONTENT_TARGET_UNKNOWN:" + manifest.CatalogId);
            var source = result[index];
            if (source.Descriptor.Composition is not null) throw new ArgumentException("CATALOG_CONTENT_EDIT_SOURCE_PACK_FIRST");
            var structures = source.Structures.ToDictionary(d => d.Id, StringComparer.Ordinal);
            var facilities = source.Facilities.ToDictionary(f => f.Id, StringComparer.Ordinal);
            var objects=source.Objects.ToDictionary(o=>o.Id,StringComparer.Ordinal);
            var replaceStructures = manifest.ReplaceStructureIds ?? new HashSet<string>();
            var replaceFacilities = manifest.ReplaceFacilityIds ?? new HashSet<string>();
            var areas=source.Areas.ToDictionary(a=>a.Id,StringComparer.Ordinal);
            var replaceAreas=manifest.ReplaceAreaIds??new HashSet<string>();
            var replaceObjects=manifest.ReplaceObjectIds??new HashSet<string>();
            if (replaceStructures.Any(id => !structures.ContainsKey(id) || !manifest.Structures.Any(d => d.Id == id)) ||
                replaceFacilities.Any(id => !facilities.ContainsKey(id) || !manifest.Facilities.Any(f => f.Id == id) && !manifest.Programs.Any(p => p.Id == id))||
                replaceObjects.Any(id=>!objects.ContainsKey(id)||(manifest.Objects??[]).All(o=>o.Id!=id))||
                replaceAreas.Any(id=>!areas.ContainsKey(id)||(manifest.Areas??[]).All(a=>a.Id!=id)))
                throw new ArgumentException("CATALOG_CONTENT_REPLACEMENT_INVALID");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in manifest.Structures)
            {
                token.ThrowIfCancellationRequested();
                if (!seen.Add(definition.Id) || structures.ContainsKey(definition.Id) && !replaceStructures.Contains(definition.Id))
                    throw new ArgumentException("CATALOG_CONTENT_STRUCTURE_CONFLICT:" + definition.Id);
                if (definition.Composition is null) throw new ArgumentException("CATALOG_CONTENT_COMPOSITION_REVIEW_REQUIRED:" + definition.Id);
                SettlementCatalogRegistry.ValidateDefinition(source.Descriptor, definition);
                structures[definition.Id] = definition;
            }
            seen.Clear();
            foreach(var definition in SiteObjectPlanner.Snapshot(manifest.Objects??[],token))
            {
                if(objects.ContainsKey(definition.Id)&&!replaceObjects.Contains(definition.Id))throw new ArgumentException("CATALOG_CONTENT_OBJECT_CONFLICT:"+definition.Id);
                objects[definition.Id]=definition;
            }
            foreach(var definition in SiteAreaPlanner.Snapshot(manifest.Areas??[],token))
            {
                if(areas.ContainsKey(definition.Id)&&!replaceAreas.Contains(definition.Id))throw new ArgumentException("CATALOG_CONTENT_AREA_CONFLICT:"+definition.Id);
                areas[definition.Id]=definition;
            }
            foreach (var facility in manifest.Facilities)
            {
                if (facility is null || !seen.Add(facility.Id) || facilities.ContainsKey(facility.Id) && !replaceFacilities.Contains(facility.Id))
                    throw new ArgumentException("CATALOG_CONTENT_FACILITY_CONFLICT");
                facilities[facility.Id] = facility;
            }
            var pending = manifest.Programs.ToList();
            foreach (var program in pending)
                if (program is null || !seen.Add(program.Id) || facilities.ContainsKey(program.Id) && !replaceFacilities.Contains(program.Id))
                    throw new ArgumentException("CATALOG_CONTENT_FACILITY_CONFLICT");
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var next = pending.OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault(p => p.Children is not null &&
                    p.Children.All(c => c is not null && facilities.ContainsKey(c.DefinitionId) && !pending.Any(other => other.Id == c.DefinitionId)));
                if (next is null) throw new ArgumentException("CATALOG_CONTENT_PROGRAM_DEPENDENCY_MISSING_OR_CYCLE");
                facilities[next.Id] = CompileProgram(next, structures.Values.ToArray(), facilities.Values.ToArray(), token,objects.Values.ToArray(),areas.Values.ToArray());
                pending.Remove(next);
            }
            var content = new Dictionary<string, string>(source.Descriptor.AuthoredContentDigests ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            if (!content.TryAdd(manifest.Id, Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest, WorldJson.CreateOptions())))))
                throw new ArgumentException("CATALOG_CONTENT_MANIFEST_DUPLICATE:" + manifest.Id);
            result[index] = new AuthoredProvider(source, source.Descriptor with { AuthoredContentDigests = content.ToFrozenDictionary(StringComparer.Ordinal) },
                Array.AsReadOnly(structures.Values.OrderBy(d => d.Id, StringComparer.Ordinal).ToArray()),
                FacilityCompositionPlanner.Snapshot(facilities.Values.ToArray(), structures.Values.ToArray(), token,objects.Values.ToArray(),areas.Values.ToArray()),SiteObjectPlanner.Snapshot(objects.Values.ToArray(),token),SiteAreaPlanner.Snapshot(areas.Values.ToArray(),token));
        }
        _ = new SettlementCatalogRegistry(result, token);
        return Array.AsReadOnly(result.ToArray());
    }

    public static async Task<string> SaveManifestAsync(CatalogContentManifest manifest, string catalogDirectory,
        bool replaceExisting = false, CancellationToken token = default)
    {
        ValidateManifest(manifest); token.ThrowIfCancellationRequested();
        if (manifest.Id.Contains(':') || manifest.Id is "." or ".." ||
            new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(manifest.Id.Split('.')[0], StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("CATALOG_CONTENT_FILE_ID_INVALID");
        string root = Path.GetFullPath(catalogDirectory); Directory.CreateDirectory(root);
        string path = Path.Combine(root, manifest.Id + ".json");
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 || File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException("CATALOG_CONTENT_LINK_REJECTED");
        if (File.Exists(path))
        {
            if (!replaceExisting) throw new IOException("CATALOG_CONTENT_FILE_EXISTS");
            var prior = ReadManifest(path);
            if (prior.Id != manifest.Id || prior.CatalogId != manifest.CatalogId) throw new ArgumentException("CATALOG_CONTENT_FILE_ID_MISMATCH");
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, WorldJson.CreateOptions(writeIndented: true));
        if (bytes.Length > 16777216) throw new ArgumentException("CATALOG_CONTENT_SIZE_LIMIT");
        string staged = Path.Combine(root, "." + manifest.Id + "." + Guid.NewGuid().ToString("N") + ".tmp");
        // Stage outside the *.json discovery set. Cancellation/write failure leaves the
        // previous authoritative file intact; an unfinished staging file is never loaded.
        await using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            await stream.WriteAsync(bytes, token);
            await stream.FlushAsync(token);
        }
        token.ThrowIfCancellationRequested();
        File.Move(staged, path, replaceExisting);
        return path;
    }

    private static void ValidateManifest(CatalogContentManifest manifest)
    {
        if (manifest is null || manifest.SchemaVersion != 1 || !SettlementCatalogRegistry.ValidId(manifest.Id) ||
            !SettlementCatalogRegistry.ValidId(manifest.CatalogId) || !SettlementCatalogRegistry.ValidId(manifest.SourceRuleId) ||
            manifest.Structures is null || manifest.Programs is null || manifest.Facilities is null ||
            manifest.Structures.Any(d => d is null || !SettlementCatalogRegistry.ValidId(d.Id)) ||
            manifest.Programs.Any(p => p is null || !SettlementCatalogRegistry.ValidId(p.Id)) ||
            manifest.Facilities.Any(f => f is null || !SettlementCatalogRegistry.ValidId(f.Id)) ||
            manifest.ReplaceStructureIds?.Any(id => !SettlementCatalogRegistry.ValidId(id)) == true ||
            manifest.ReplaceFacilityIds?.Any(id => !SettlementCatalogRegistry.ValidId(id)) == true ||
            manifest.ReplaceObjectIds?.Any(id=>!SettlementCatalogRegistry.ValidId(id))==true||
            manifest.Objects?.Any(o=>o is null||!SettlementCatalogRegistry.ValidId(o.Id))==true||
            manifest.ReplaceAreaIds?.Any(id=>!SettlementCatalogRegistry.ValidId(id))==true||manifest.Areas?.Any(a=>a is null||!SettlementCatalogRegistry.ValidId(a.Id))==true||
            manifest.Structures.Count + manifest.Programs.Count + manifest.Facilities.Count+(manifest.Objects?.Count??0)+(manifest.Areas?.Count??0) is 0 or > 256)
            throw new ArgumentException("CATALOG_CONTENT_INVALID");
    }

    private static IReadOnlyList<CatalogContentManifest> OrderManifests(IEnumerable<CatalogContentManifest> manifests, CancellationToken token)
    {
        var entries = manifests.ToArray();
        if (entries.Length > 256) throw new ArgumentException("CATALOG_CONTENT_BUDGET");
        var owners = new Dictionary<(string Catalog, string Kind, string Id), int>();
        var identities = new HashSet<(string Catalog, string Id)>();
        for (int i = 0; i < entries.Length; i++)
        {
            token.ThrowIfCancellationRequested(); var entry = entries[i]; ValidateManifest(entry);
            if (!identities.Add((entry.CatalogId, entry.Id))) throw new ArgumentException("CATALOG_CONTENT_MANIFEST_DUPLICATE:" + entry.Id);
            foreach (var key in entry.Structures.Select(d => (entry.CatalogId, "structure", d.Id))
                .Concat(entry.Programs.Select(p => (entry.CatalogId, "facility", p.Id)))
                .Concat(entry.Facilities.Select(f => (entry.CatalogId, "facility", f.Id)))
                .Concat((entry.Objects??[]).Select(o=>(entry.CatalogId,"object",o.Id)))
                .Concat((entry.Areas??[]).Select(a=>(entry.CatalogId,"area",a.Id))))
            {
                if (owners.TryGetValue(key, out int previous) && previous != i)
                    throw new ArgumentException("CATALOG_CONTENT_MULTIPLE_OWNERS:" + key.Id);
                owners[key] = i;
            }
        }
        var active = new HashSet<int>(); var done = new HashSet<int>(); var ordered = new List<CatalogContentManifest>();
        foreach (int i in Enumerable.Range(0, entries.Length).OrderBy(i => entries[i].CatalogId, StringComparer.Ordinal).ThenBy(i => entries[i].Id, StringComparer.Ordinal)) Visit(i);
        void Visit(int index)
        {
            token.ThrowIfCancellationRequested();
            if (done.Contains(index)) return;
            if (!active.Add(index)) throw new ArgumentException("CATALOG_CONTENT_DEPENDENCY_CYCLE");
            var entry = entries[index];
            var structureRefs = entry.Programs.SelectMany(p => p.Parts ?? []).Where(p => p is not null&&p.Kind==CatalogPartKind.Structure).Select(p => p.DefinitionId)
                .Concat(entry.Facilities.SelectMany(f => f.Structures ?? []).Where(p => p?.Envelope is not null).Select(p => p.Envelope.BuildingType));
            var facilityRefs = entry.Programs.SelectMany(p => p.Children ?? []).Where(p => p is not null).Select(p => p.DefinitionId)
                .Concat(entry.Facilities.SelectMany(f => f.Children ?? []).Where(p => p is not null).Select(p => p.DefinitionId));
            var objectRefs=entry.Programs.SelectMany(p=>p.Parts??[]).Where(p=>p is not null&&p.Kind==CatalogPartKind.Equipment).Select(p=>p.DefinitionId)
                .Concat(entry.Facilities.SelectMany(f=>f.Objects??[]).Where(o=>o is not null).Select(o=>o.DefinitionId));
            var areaRefs=entry.Programs.SelectMany(p=>p.Parts??[]).Where(p=>p is not null&&p.Kind==CatalogPartKind.GroundArea).Select(p=>p.DefinitionId)
                .Concat(entry.Facilities.SelectMany(f=>f.Areas??[]).Where(a=>a is not null).Select(a=>a.DefinitionId));
            foreach (var key in structureRefs.Select(id => (entry.CatalogId, "structure", id)).Concat(facilityRefs.Select(id => (entry.CatalogId, "facility", id))).Concat(objectRefs.Select(id=>(entry.CatalogId,"object",id))).Concat(areaRefs.Select(id=>(entry.CatalogId,"area",id))))
                if (owners.TryGetValue(key, out int dependency) && dependency != index) Visit(dependency);
            active.Remove(index); done.Add(index); ordered.Add(entry);
        }
        return ordered;
    }
    private sealed class AuthoredProvider(ISettlementCatalogProvider source, SettlementCatalogDescriptor descriptor,
        IReadOnlyList<BuildingDefinition> structures, IReadOnlyList<FacilityDefinition> facilities,IReadOnlyList<SiteObjectDefinition> objects,IReadOnlyList<SiteAreaDefinition> areas) : ISettlementCatalogProvider
    {
        public SettlementCatalogDescriptor Descriptor => descriptor;
        public IReadOnlyList<BuildingDefinition> Structures => structures;
        public IReadOnlyList<FacilityDefinition> Facilities => facilities;
        public IReadOnlyList<SiteObjectDefinition> Objects=>objects;
        public IReadOnlyList<SiteAreaDefinition> Areas=>areas;
        public ISettlementCatalogPolicy Policy => source.Policy;
        public ISettlementDemandPolicy Demand => source.Demand;
        public IInfrastructureDesignStrategy Infrastructure => source.Infrastructure;
    }
}
