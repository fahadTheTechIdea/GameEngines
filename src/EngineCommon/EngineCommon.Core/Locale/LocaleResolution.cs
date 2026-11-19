namespace EngineCommon;

/// <summary>A message found through a <see cref="LocaleChain"/>: the text, the locale it came from, and the locales tried before it that did not have the key.</summary>
public sealed record LocaleResolution(string Text, string Locale, IReadOnlyList<string> Skipped);