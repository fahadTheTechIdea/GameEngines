using EngineCommon;

namespace EngineCommon;

/// <summary>
/// The locales tried, in order, for a player: the exact locale asked for (<c>fr-CA</c>), then its language (<c>fr</c>), then the game's default (<c>en</c>); a locale that has no table is left
/// out of the chain. A key a locale lacks falls through to the next, and the fall is remembered, so the keys a translator still owes are known from play as well as from a coverage check.
/// A key that no locale of the chain has is refused with every locale tried named.
/// </summary>
public sealed class LocaleChain
{
    private readonly IReadOnlyList<LocaleTable> _tables;
    private readonly SortedDictionary<string, int> _fallbacks = new(StringComparer.Ordinal);

    private LocaleChain(IReadOnlyList<LocaleTable> tables) => _tables = tables;

    /// <param name="requested">The locale of the player, such as <c>fr-CA</c>.</param>
    /// <param name="available">Every table the game has.</param>
    /// <param name="defaultLocale">The locale every key is expected to exist in; the chain ends with it.</param>
    public static LocaleChain Create(string requested, IEnumerable<LocaleTable> available, string defaultLocale)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(available);
        var byName = new Dictionary<string, LocaleTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in available)
            if (!byName.TryAdd(table.Locale, table)) throw new EngineRefusedException(LocaleCodes.Format, "two tables are for locale '" + table.Locale + "'");
        requested = requested.Replace('_', '-');
        var wanted = new List<string> { requested };
        var dash = requested.IndexOfAny(['-', '_']);
        if (dash > 0) wanted.Add(requested[..dash]);
        wanted.Add(defaultLocale);
        var chain = new List<LocaleTable>();
        foreach (var name in wanted)
            if (byName.TryGetValue(name, out var table) && !chain.Contains(table)) chain.Add(table);
        if (chain.Count == 0) throw new EngineRefusedException(LocaleCodes.MissingKey, "no table for '" + requested + "' or the default '" + defaultLocale + "'");
        return new LocaleChain(chain);
    }

    /// <summary>The locales of the chain in the order they are tried.</summary>
    public IReadOnlyList<string> Locales => _tables.Select(t => t.Locale).ToList();

    public LocaleResolution Render(string key, IReadOnlyList<MessageArg> args)
    {
        var skipped = new List<string>();
        foreach (var table in _tables)
        {
            if (!table.Messages.ContainsKey(key)) { skipped.Add(table.Locale); continue; }
            foreach (var locale in skipped) _fallbacks[locale + ":" + key] = _fallbacks.GetValueOrDefault(locale + ":" + key) + 1;
            return new LocaleResolution(LocaleRenderer.Render(table, key, args), table.Locale, skipped);
        }
        throw new EngineRefusedException(LocaleCodes.MissingKey, "no locale of the chain (" + string.Join(", ", Locales) + ") has a message '" + key + "'");
    }

    /// <summary>The keys that had to fall back, as <c>locale:key</c> with how often, in order: what the player saw in another language.</summary>
    public IReadOnlyDictionary<string, int> Fallbacks => _fallbacks;
}