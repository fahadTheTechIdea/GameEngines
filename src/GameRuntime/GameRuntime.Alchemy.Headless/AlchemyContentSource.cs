using AlchemyEngine;
using GameContent.Catalog;

namespace GameRuntime.Alchemy.Headless;

/// <summary>Alchemy content and where it came from, for the run's first output line.</summary>
public sealed record LoadedContent(AlchemyContent Content, string Source);

/// <summary>
/// The one place the headless game chooses its content: a content file, or a catalog pack picked by id and read from JSON at run time.
/// A pack is validated against the standard and converted to alchemy content on the spot, so a new pack needs no rebuild.
/// </summary>
public static class AlchemyContentSource
{
    public static string DefaultContentPath => Path.Combine(AppContext.BaseDirectory, "Content", "alchemy.json");

    public static string DefaultPacksPath => Path.Combine(AppContext.BaseDirectory, "Packs");

    public static LoadedContent Load(AlchemyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.PackId is null)
            return new LoadedContent(AlchemyContentLoader.Load(File.ReadAllText(options.ContentPath ?? DefaultContentPath)), "file");
        var set = CatalogLoader.Load(new DirectoryCatalogSource(options.PacksPath ?? DefaultPacksPath), options.PackId).Required();
        return new LoadedContent(CatalogToAlchemy.Convert(set).Content, "pack:" + set.Root.Id + "@" + set.Root.Version);
    }
}
