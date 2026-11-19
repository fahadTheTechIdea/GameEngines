namespace GameContent.Catalog;

/// <summary>
/// SimEngine content generated from a pack: the files of a content directory (manifest and documents, by relative path), the ids of the
/// recipes that became transformations, and the notes on everything reduced or left out. The files are generated artifacts; the pack is the source.
/// </summary>
public sealed record SimConversion(IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Transformations, IReadOnlyList<string> Stations, ConversionReport Report, IReadOnlyList<string> Vehicles);
