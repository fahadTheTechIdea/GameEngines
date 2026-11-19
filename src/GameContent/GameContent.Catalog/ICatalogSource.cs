namespace GameContent.Catalog;

/// <summary>Where pack files come from. Paths are relative with forward slashes (<c>core/pack.json</c>); a missing file is null.</summary>
public interface ICatalogSource
{
    string? Read(string path);
}

/// <summary>A directory of packs, one folder per pack id.</summary>
public sealed class DirectoryCatalogSource(string root) : ICatalogSource
{
    public string Root { get; } = root ?? throw new ArgumentNullException(nameof(root));

    public string? Read(string path)
    {
        var full = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }
}

/// <summary>Files held in memory, for tests and tools that build or alter packs without touching disk.</summary>
public sealed class MemoryCatalogSource(IReadOnlyDictionary<string, string> files) : ICatalogSource
{
    public string? Read(string path) => files.TryGetValue(path, out var text) ? text : null;
}
