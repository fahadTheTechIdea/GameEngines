namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    /// <summary>Ids are unique across resources, stations, skills and recipes of the whole set, and each lives in its own pack's namespace.</summary>
    private static void CheckIdentity(CatalogSet set, CatalogReport report)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pack in set.Packs)
        {
            foreach (var (kind, id) in IdsOf(pack))
            {
                if (seen.TryGetValue(id, out var first)) report.Add(CatalogCodes.DuplicateId, "id '" + id + "' is a " + kind + " here and a " + first + " elsewhere", id);
                else seen.Add(id, kind);
                if (id.Split('.')[0] != pack.Id) report.Add(CatalogCodes.IdNamespace, "id '" + id + "' must start with its pack id '" + pack.Id + ".'", id);
            }
        }
    }

    private static IEnumerable<(string Kind, string Id)> IdsOf(CatalogPackData pack) =>
        pack.Resources.Select(r => ("resource", r.Id))
            .Concat(pack.Stations.Select(s => ("station", s.Id)))
            .Concat(pack.Skills.Select(s => ("skill", s.Id)))
            .Concat(pack.Eras.Select(e => ("era", e.Id)))
            .Concat(pack.Stats.Select(s => ("stat", s.Id)))
            .Concat(pack.Recipes.Select(r => ("recipe", r.Id)));

    /// <summary>Whether a reference from <paramref name="pack"/> may name <paramref name="id"/>: its own pack or a pack it declares as a dependency.</summary>
    private static bool Visible(CatalogPackData pack, string id)
    {
        var owner = id.Split('.')[0];
        return owner == pack.Id || pack.Dependencies.Any(d => d.Id == owner);
    }

    /// <summary>Resolves a reference, reporting whether it is unknown or merely not visible from this pack.</summary>
    private static bool Reference(CatalogPackData pack, string id, string what, string location, bool exists, CatalogReport report)
    {
        if (!exists) { report.Add(CatalogCodes.UnknownRef, what + " '" + id + "' does not exist", location); return false; }
        if (!Visible(pack, id)) { report.Add(CatalogCodes.NotVisible, what + " '" + id + "' belongs to a pack that '" + pack.Id + "' does not depend on", location); return false; }
        return true;
    }
}
