namespace GameRuntime;

/// <summary>The world-events component's events, declared once, by <see cref="WorldChannels.Install"/> only, before the session's Init. A version is bumped when an event's fields change.</summary>
internal static class WorldEventCatalog
{
    internal static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<WorldRequested>("world.requested", 1);
        session.DeclareEvent<WorldApplied>("world.applied", 1);
        session.DeclareEvent<WorldRefused>("world.refused", 1);
        session.DeclareEvent<WorldWeatherAnswered>("world.weather-answered", 1);
        session.DeclareEvent<WorldDiseaseAnswered>("world.disease-answered", 1);
        session.DeclareEvent<WorldSeasonChanged>("world.season-changed", 1);
        session.DeclareEvent<WorldRegimeChanged>("world.regime-changed", 1);
        session.DeclareEvent<WorldWeatherChanged>("world.weather-changed", 1);
        session.DeclareEvent<WorldHazardChanged>("world.hazard-changed", 1);
        session.DeclareEvent<WorldOutbreakChanged>("world.outbreak-changed", 1);
        session.DeclareEvent<WorldCardPlayed>("world.card-played", 1);
        session.DeclareEvent<WorldDirectorChanged>("world.director-changed", 1);
        session.DeclareEvent<WorldEffectIssued>("world.effect-issued", 1);
        session.DeclareEvent<WorldInterventionChanged>("world.intervention-changed", 1);
    }
}
