namespace AlchemyEngine;

/// <summary>
/// Everything a combination may depend on besides the items: the caller's seed, the caller's own attempt counter and the environment
/// tags on offer. The same items and context always give the same result; there is no clock and no hidden random state.
/// </summary>
public sealed record CombineContext(ulong Seed, long Attempt, IReadOnlyList<string> Environment);
