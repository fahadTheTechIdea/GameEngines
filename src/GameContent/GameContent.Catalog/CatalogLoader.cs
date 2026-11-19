using System.Text.Json;

namespace GameContent.Catalog;

/// <summary>
/// Loads a pack and its dependencies from a source, checks every file against the schemas, reads the model, then runs the semantic rules.
/// Every finding is collected; nothing is repaired or skipped.
/// </summary>
public static class CatalogLoader
{
    public static CatalogLoadResult Load(ICatalogSource source, string packId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var report = new CatalogReport();
        var loaded = new Dictionary<string, CatalogPackData>(StringComparer.Ordinal);
        var order = new List<CatalogPackData>();
        var visiting = new List<string>();
        var root = LoadPack(source, packId, "request", dependency: false, loaded, order, visiting, report);
        if (root is null || !report.IsValid) return new CatalogLoadResult(null, report);
        var set = new CatalogSet(order);
        report.AddRange(CatalogValidator.Validate(set));
        return new CatalogLoadResult(set, report);
    }

    private static CatalogPackData? LoadPack(ICatalogSource source, string id, string requestedBy, bool dependency, Dictionary<string, CatalogPackData> loaded,
        List<CatalogPackData> order, List<string> visiting, CatalogReport report)
    {
        if (loaded.TryGetValue(id, out var already)) return already;
        if (visiting.Contains(id))
        {
            report.Add(CatalogCodes.DependencyCycle, "pack '" + id + "' depends on itself through " + string.Join(" -> ", visiting.Append(id)), id);
            return null;
        }
        var manifestPath = id + "/pack.json";
        var text = source.Read(manifestPath);
        if (text is null)
        {
            report.Add(dependency ? CatalogCodes.DependencyMissing : CatalogCodes.FileMissing, "pack '" + id + "' has no pack.json (needed by " + requestedBy + ")", manifestPath);
            return null;
        }
        if (!TryParse(text, manifestPath, report, out var manifestJson)) return null;
        var before = report.Items.Count;
        CatalogSchemas.ValidatePack(manifestJson.RootElement, manifestPath, report);
        if (report.Items.Count != before) return null;
        var manifest = CatalogReader.Manifest(manifestJson.RootElement);
        manifestJson.Dispose();
        if (manifest.Id != id)
        {
            report.Add(CatalogCodes.PackId, "pack.json says id '" + manifest.Id + "' but the folder is '" + id + "'", manifestPath);
            return null;
        }

        visiting.Add(id);
        foreach (var requirement in manifest.Dependencies)
        {
            var found = LoadPack(source, requirement.Id, id, dependency: true, loaded, order, visiting, report);
            if (found is not null && !Satisfies(found.Version, requirement.Version))
                report.Add(CatalogCodes.DependencyVersion, "pack '" + id + "' needs '" + requirement.Id + "' " + requirement.Version + " but " + found.Version + " is present", manifestPath);
        }
        visiting.RemoveAt(visiting.Count - 1);

        var documents = new List<CatalogDocument>();
        foreach (var name in manifest.Documents)
        {
            var path = id + "/" + name;
            var documentText = source.Read(path);
            if (documentText is null) { report.Add(CatalogCodes.FileMissing, "document '" + name + "' listed in pack.json does not exist", path); continue; }
            if (!TryParse(documentText, path, report, out var documentJson)) continue;
            var documentBefore = report.Items.Count;
            CatalogSchemas.ValidateDocument(documentJson.RootElement, path, report);
            if (report.Items.Count == documentBefore)
            {
                var document = CatalogReader.Document(documentJson.RootElement);
                if (document.Pack != id) report.Add(CatalogCodes.DocumentPack, "document claims pack '" + document.Pack + "' but lives in '" + id + "'", path);
                else documents.Add(document);
            }
            documentJson.Dispose();
        }

        var locales = new List<CatalogLocale>();
        foreach (var reference in manifest.Locales)
        {
            var path = id + "/" + reference.Document;
            var localeText = source.Read(path);
            if (localeText is null) { report.Add(CatalogCodes.FileMissing, "locale document '" + reference.Document + "' listed in pack.json does not exist", path); continue; }
            if (!TryParse(localeText, path, report, out var localeJson)) continue;
            var localeBefore = report.Items.Count;
            CatalogSchemas.ValidateLocale(localeJson.RootElement, path, report);
            if (report.Items.Count == localeBefore)
            {
                var locale = CatalogReader.Locale(localeJson.RootElement);
                if (locale.Pack != id) report.Add(CatalogCodes.LocalePack, "locale claims pack '" + locale.Pack + "' but lives in '" + id + "'", path);
                else if (locale.Language != reference.Language) report.Add(CatalogCodes.LocaleLanguage, "pack.json lists this document as '" + reference.Language + "' but it says '" + locale.Language + "'", path);
                else if (locale.Language == manifest.Language) report.Add(CatalogCodes.LocaleLanguage, "'" + locale.Language + "' is the pack's own language; its names are the names in the definitions", path);
                else if (locales.Any(l => l.Language == locale.Language)) report.Add(CatalogCodes.LocaleDuplicate, "language '" + locale.Language + "' has two locale documents", path);
                else locales.Add(new CatalogLocale(locale.Language, locale.Texts));
            }
            localeJson.Dispose();
        }

        var data = new CatalogPackData(manifest.Id, manifest.Version, manifest.Title, manifest.Language, manifest.World, manifest.Genres, manifest.Dependencies, manifest.License, locales, manifest.Documents,
            documents.SelectMany(d => d.Resources).ToList(), documents.SelectMany(d => d.Stations).ToList(),
            documents.SelectMany(d => d.Skills).ToList(), documents.SelectMany(d => d.Recipes).ToList(),
            documents.SelectMany(d => d.Eras).ToList(), documents.SelectMany(d => d.Availability).ToList(), documents.SelectMany(d => d.Stats).ToList(), documents.SelectMany(d => d.Items).ToList(), documents.SelectMany(d => d.Units).ToList());
        loaded[id] = data;
        order.Add(data);
        return data;
    }

    private static bool TryParse(string text, string path, CatalogReport report, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
            return true;
        }
        catch (JsonException exception)
        {
            // Reported as a finding: the caller gets the same refusal as for any other bad file.
            report.Add(CatalogCodes.NotJson, exception.Message, path);
            document = null!;
            return false;
        }
    }

    /// <summary>Caret rule: the same major version, and at least the required minor and patch.</summary>
    private static bool Satisfies(string installed, string required)
    {
        var have = installed.Split('.').Select(int.Parse).ToArray();
        var need = required.Split('.').Select(int.Parse).ToArray();
        if (have[0] != need[0]) return false;
        for (var i = 1; i < 3; i++)
            if (have[i] != need[i]) return have[i] > need[i];
        return true;
    }
}
