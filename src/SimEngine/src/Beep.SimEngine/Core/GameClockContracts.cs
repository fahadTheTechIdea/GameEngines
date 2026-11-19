namespace Beep.SimEngine
{
    /// <summary>Read-only facts about the simulation's time coordinate. StepIndex is its exact tick.</summary>
    public interface IGameClock
    {
        long StepIndex { get; }
        ISimulationTime Time { get; }
        GameDuration TotalGameTime { get; }
        GameDuration StepDuration { get; }
        CalendarReading Date { get; }
    }
}
