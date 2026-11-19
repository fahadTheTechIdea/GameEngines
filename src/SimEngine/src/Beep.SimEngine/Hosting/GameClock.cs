using System;
using Beep.SimEngine.Content;
using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Hosting
{
    /// <summary>
    /// The simulation's tick counter and time converter. It is plain state: it never paces, pauses, scales or reads a
    /// wall clock. The host layer owns the session clock and tells the simulation which tick to run; the counter only
    /// advances when the simulation completes exactly that tick. Fixed ticks are exact time coordinates.
    /// </summary>
    public sealed class GameClock : IGameClock
    {
        internal GameClock(SimulationTimeDef definition)
        {
            Time = new GameTime(definition ?? throw new ArgumentNullException(nameof(definition)));
            AuthoredTicksPerSecond = definition.TicksPerSecond;
            AuthoredFrameTicks = definition.FrameTicks;
        }

        public long StepIndex { get; private set; }
        public ISimulationTime Time { get; }
        public GameDuration TotalGameTime => GameDuration.FromSteps(StepIndex, Time);
        public GameDuration StepDuration => GameDuration.FromSteps(1, Time);
        public CalendarReading Date => Time.Calendar(StepIndex);

        /// <summary>Authored pace, offered read-only so the host's clock can use it; the simulation does not act on it.</summary>
        public int AuthoredTicksPerSecond { get; }
        public int AuthoredFrameTicks { get; }

        internal long NextStep() => checked(StepIndex + 1);
        internal void CompleteStep(long completed)
        {
            if (completed != NextStep()) throw new InvalidOperationException("clock completion must advance exactly one game update");
            StepIndex = completed;
        }

        /// <summary>The sole current-time codec, embedded once by World.</summary>
        internal void Sync(IStateSync state)
        {
            long step = StepIndex;
            state.Long(ref step);
            if (step < 0) throw new CorruptStateException("game clock time must be nonnegative");
            if (state.Reading) StepIndex = step;
        }
    }
}
