namespace GameRuntime;

/// <summary>The director effects of the character factory, as the one list a game registers with its director bridge.</summary>
public static class CharacterEffects
{
    public static IDirectorEffect[] All() =>
    [
        new SpawnFormationEffect(), new SpawnIndividualsEffect(), new MaterializeEffect(), new CasualtiesEffect(), new KillEffect(),
    ];
}
