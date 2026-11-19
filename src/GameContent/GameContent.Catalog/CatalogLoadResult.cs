namespace GameContent.Catalog;

/// <summary>
/// What loading a pack produced: the set (null when the files could not even be read into a model) and every finding.
/// <see cref="Required"/> is the set only if there were no findings.
/// </summary>
public sealed record CatalogLoadResult(CatalogSet? Set, CatalogReport Report)
{
    public CatalogSet Required() => Report.IsValid && Set is not null ? Set : throw new CatalogRefusedException(Report);
}
