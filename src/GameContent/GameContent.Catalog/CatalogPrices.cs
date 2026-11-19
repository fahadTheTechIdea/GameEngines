namespace GameContent.Catalog;

/// <summary>
/// The one reserved resource property the validator reads: <c>base-price</c>, a positive number, the value of one unit of the
/// resource in the game's reference currency. It is what the balance rules (value loops, value outliers) measure with. A resource
/// without it is simply not part of those rules. Every other property is game data the catalog passes through unread.
/// </summary>
public static class CatalogPrices
{
    public const string Key = "base-price";

    public static decimal? Of(CatalogResource resource) =>
        resource.Properties.TryGetValue(Key, out var value) && value is decimal price && price > 0 ? price : null;
}
