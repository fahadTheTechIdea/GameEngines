namespace GameRuntime.Alchemy.Headless;

/// <summary>
/// How a player decides. Called once per tick after the tick's events reached the player's view. It must depend only on the view,
/// the tick and its own seed, so the same events always lead to the same decisions, including after a save and load.
/// </summary>
public interface IAlchemyPolicy
{
    string Name { get; }
    IReadOnlyList<AlchemyAttempt> Decide(AlchemyView view);
}
