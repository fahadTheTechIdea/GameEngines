/// <summary>One column of the trained tree's input: the AI fact that carries it and the range the host promises to keep it in. <see cref="Key"/> is null when the fact is not a host key (refused).</summary>
internal sealed record TreeFeature(string Id, string? Key, long Min, long Max, long Initial);
