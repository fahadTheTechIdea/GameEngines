namespace GameRuntime;

/// <summary>
/// Where the alchemy-sim module leaves the view it read while the world was composed, for the host to build its components from.
/// Every game made from the same content reads the same view; a different one is refused rather than replaced.
/// </summary>
public sealed class SimRecipeViewHolder
{
    private SimRecipeView? _view;

    /// <summary>The view of the composed world's content. Reading it before any world was composed is a host mistake and fails.</summary>
    public SimRecipeView View => _view ?? throw new InvalidOperationException("no world has been composed with the alchemy-sim module yet");

    internal void Capture(SimRecipeView view)
    {
        if (_view is not null && _view.Alchemy.Digest != view.Alchemy.Digest)
            throw new InvalidOperationException("the alchemy view changed between worlds composed from different content");
        _view ??= view;
    }
}
