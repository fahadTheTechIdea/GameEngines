using System.Text.Json;
using System.Text.Json.Nodes;
using EngineCommon;

namespace DialogueEngine;

/// <summary>What importing a translator's sheet gave: the table of everything that could be used, how much it covers, what was refused and why, and whether the sheet was made from an older document.</summary>
public sealed record SheetImport(LocaleTable Table, CoverageReport Report, IReadOnlyList<MessageProblem> Rejected, bool Stale);

/// <summary>
/// The file a translator works in. Export writes one entry for every key a document uses, with where it is used (the speaker and node, so the line has a context), the placeholders its
/// text may use, the source message and the target message so far; import reads the same file back into a locale table, keeping what is usable and listing what is not. Yarn Spinner gives
/// translators a strings file keyed by line id in the same way; the plural forms of a message travel as the message itself, so a translator gives the forms the language needs.
/// </summary>
public static class TranslationSheet
{
    public const int Format = 1;
    private static readonly StrictJson Json = new(LocaleCodes.ForLocale);

    /// <param name="source">The table the translator reads, or null to leave the source column empty.</param>
    /// <param name="target">The translation done so far, or null for none.</param>
    public static string Export(DialogueSet set, string sourceLocale, string targetLocale, LocaleTable? source, LocaleTable? target)
    {
        ArgumentNullException.ThrowIfNull(set);
        _ = PluralRules.Categories(sourceLocale);
        _ = PluralRules.Categories(targetLocale);
        var entries = new JsonArray();
        foreach (var (key, placeholders) in set.Keys)
            entries.Add(new JsonObject
            {
                ["key"] = key,
                ["uses"] = new JsonArray(set.KeyUses[key].Select(u => (JsonNode)JsonValue.Create(u)!).ToArray()),
                ["placeholders"] = new JsonArray(placeholders.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()),
                ["source"] = source is not null && source.Messages.TryGetValue(key, out var s) ? s.ToJson() : null,
                ["target"] = target is not null && target.Messages.TryGetValue(key, out var t) ? t.ToJson() : null,
            });
        var sheet = new JsonObject
        {
            ["format"] = Format, ["document"] = set.Id, ["digest"] = set.Digest, ["source"] = sourceLocale, ["target"] = targetLocale, ["entries"] = entries,
        };
        return sheet.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static SheetImport Import(DialogueSet set, string text)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(text);
        using var parsed = Json.Parse(text, "sheet");
        var root = Json.Object(parsed.RootElement, "sheet", ["format", "document", "digest", "source", "target", "entries"]);
        if (root.GetProperty("format").ValueKind != JsonValueKind.Number || !root.GetProperty("format").TryGetInt32(out var format) || format != Format)
            throw Json.Refuse(LocaleCodes.Format, "sheet format must be " + Format);
        if (Json.Text(root, "document", "sheet") != set.Id) throw Json.Refuse(LocaleCodes.Format, "the sheet is for document '" + Json.Text(root, "document", "sheet") + "', not '" + set.Id + "'");
        var stale = Json.Text(root, "digest", "sheet", 64) != set.Digest;
        var target = Json.Text(root, "target", "sheet", 16);
        _ = PluralRules.Categories(target);
        var accepted = new JsonObject();
        var rejected = new List<MessageProblem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Json.Array(root, "entries", "sheet", 0, 100_000).EnumerateArray())
        {
            Json.Object(entry, "sheet entry", ["key", "uses", "placeholders", "source", "target"]);
            var key = Json.CheckId(Json.Text(entry, "key", "sheet entry"), "sheet entry key");
            if (!seen.Add(key)) throw Json.Refuse(LocaleCodes.Format, "the sheet has '" + key + "' twice");
            var message = entry.GetProperty("target");
            if (message.ValueKind == JsonValueKind.Null) continue;
            if (!set.Keys.TryGetValue(key, out var given)) { rejected.Add(new MessageProblem(key, "the document no longer uses this key")); continue; }
            try
            {
                var one = new JsonObject { ["format"] = LocaleTable.Format, ["locale"] = target, ["messages"] = new JsonObject { [key] = JsonNode.Parse(message.GetRawText()) } };
                var stray = LocaleTable.Load(one.ToJsonString()).Messages[key].Placeholders().FirstOrDefault(name => !given.Contains(name));
                if (stray is not null) { rejected.Add(new MessageProblem(key, "uses '{" + stray + "}', which the line does not give")); continue; }
            }
            catch (EngineRefusedException refusal)
            {
                rejected.Add(new MessageProblem(key, refusal.Message));
                continue;
            }
            accepted[key] = JsonNode.Parse(message.GetRawText());
        }
        var table = LocaleTable.Load(new JsonObject { ["format"] = LocaleTable.Format, ["locale"] = target, ["messages"] = accepted }.ToJsonString());
        return new SheetImport(table, LocaleCoverage.Report(set, table), rejected, stale);
    }
}
