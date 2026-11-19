namespace GameRuntime;

/// <summary>Every event the world-events component raised on a session, as text and as one hash (see <see cref="EventTranscript"/>): a run, a resumed run and a replay can be compared by one value.</summary>
public sealed class WorldTranscript : EventTranscript
{
    public WorldTranscript(RuntimeSession session) : base(session)
    {
        Listen<WorldApplied>();
        Listen<WorldRefused>();
        Listen<WorldWeatherAnswered>();
        Listen<WorldDiseaseAnswered>();
        Listen<WorldSeasonChanged>();
        Listen<WorldRegimeChanged>();
        Listen<WorldWeatherChanged>();
        Listen<WorldHazardChanged>();
        Listen<WorldOutbreakChanged>();
        Listen<WorldCardPlayed>();
        Listen<WorldDirectorChanged>();
        Listen<WorldEffectIssued>();
        Listen<WorldInterventionChanged>();
    }
}
