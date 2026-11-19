namespace GameContent.Catalog;

/// <summary>
/// A pack and everything it depends on, dependencies first. The set answers lookups by id across all packs; a reference may only
/// name something in its own pack or in a declared dependency, which the validator enforces.
/// </summary>
public sealed class CatalogSet
{
    private readonly Dictionary<string, CatalogResource> _resources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogStation> _stations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogSkill> _skills = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogRecipe> _recipes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogEra> _eras = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogStat> _stats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogItem> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogFormation> _units = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogAvailability> _availability = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogPackData> _packs = new(StringComparer.Ordinal);

    public CatalogSet(IReadOnlyList<CatalogPackData> packs)
    {
        Packs = packs ?? throw new ArgumentNullException(nameof(packs));
        foreach (var pack in packs)
        {
            _packs[pack.Id] = pack;
            foreach (var resource in pack.Resources) _resources.TryAdd(resource.Id, resource);
            foreach (var station in pack.Stations) _stations.TryAdd(station.Id, station);
            foreach (var skill in pack.Skills) _skills.TryAdd(skill.Id, skill);
            foreach (var recipe in pack.Recipes) _recipes.TryAdd(recipe.Id, recipe);
            foreach (var era in pack.Eras) _eras.TryAdd(era.Id, era);
            foreach (var stat in pack.Stats) _stats.TryAdd(stat.Id, stat);
            foreach (var item in pack.Items) _items.TryAdd(item.Resource, item);
            foreach (var unit in pack.Units) _units.TryAdd(unit.Resource, unit);
            foreach (var entry in pack.Availability) _availability.TryAdd(entry.Id, entry);
        }
    }

    /// <summary>Dependencies first; the requested pack is last.</summary>
    public IReadOnlyList<CatalogPackData> Packs { get; }

    public CatalogPackData Root => Packs[^1];

    public IEnumerable<CatalogResource> Resources => Packs.SelectMany(p => p.Resources);
    public IEnumerable<CatalogStation> Stations => Packs.SelectMany(p => p.Stations);
    public IEnumerable<CatalogRecipe> Recipes => Packs.SelectMany(p => p.Recipes);
    public IEnumerable<CatalogEra> Eras => Packs.SelectMany(p => p.Eras);
    public IEnumerable<CatalogStat> Stats => Packs.SelectMany(p => p.Stats);
    public IEnumerable<CatalogItem> Items => Packs.SelectMany(p => p.Items);
    public IEnumerable<CatalogFormation> Units => Packs.SelectMany(p => p.Units);
    public IEnumerable<CatalogAvailability> Availability => Packs.SelectMany(p => p.Availability);

    public bool TryPack(string id, out CatalogPackData pack) => _packs.TryGetValue(id, out pack!);
    public bool TryResource(string id, out CatalogResource resource) => _resources.TryGetValue(id, out resource!);
    public bool TryStation(string id, out CatalogStation station) => _stations.TryGetValue(id, out station!);
    public bool TrySkill(string id, out CatalogSkill skill) => _skills.TryGetValue(id, out skill!);
    public bool TryRecipe(string id, out CatalogRecipe recipe) => _recipes.TryGetValue(id, out recipe!);
    public bool TryEra(string id, out CatalogEra era) => _eras.TryGetValue(id, out era!);
    public bool TryStat(string id, out CatalogStat stat) => _stats.TryGetValue(id, out stat!);
    public bool TryItem(string resource, out CatalogItem item) => _items.TryGetValue(resource, out item!);
    public bool TryUnit(string resource, out CatalogFormation unit) => _units.TryGetValue(resource, out unit!);
    public bool TryAvailability(string id, out CatalogAvailability entry) => _availability.TryGetValue(id, out entry!);
}
