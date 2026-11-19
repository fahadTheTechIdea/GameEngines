using System.Diagnostics;

namespace GameRuntime;

/// <summary>
/// The engine-style main loop for a host that has none of its own (a console, a test, a server). Each pass reads how much time went
/// by, lets the caller write its inputs for that moment, and hands the elapsed time to <c>runtime.Update</c>; the session's clock
/// decides how many ticks that is and runs every component's Update once per tick. By default the loop reads the real clock
/// (Stopwatch); with a fixed frame it feeds that much virtual time per pass, which makes a run reproducible.
/// </summary>
public static class EngineMainLoop
{
    /// <param name="host">The session; the loop stops when it leaves the Running state.</param>
    /// <param name="totalMicroseconds">Host time to run: real time, or virtual time with a fixed frame.</param>
    /// <param name="fixedFrameMicroseconds">Null reads the real clock; a value feeds that much virtual time on every pass.</param>
    /// <param name="writeInputs">Called before each Update with the host time reached, to write due input into the input state.</param>
    public static void Run(IGameRuntime host, long totalMicroseconds, long? fixedFrameMicroseconds, Action<long> writeInputs)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(writeInputs);
        if (fixedFrameMicroseconds is null)
        {
            var start = Stopwatch.GetTimestamp();
            long previous = 0;
            while (host.State == RuntimeState.Running)
            {
                var now = Stopwatch.GetElapsedTime(start).Ticks / (TimeSpan.TicksPerMillisecond / 1000);
                if (now >= totalMicroseconds) break;
                writeInputs(now);
                host.Update(now - previous);
                previous = now;
                Thread.Sleep(1); // the loop has nothing to do until the clock earns another tick
            }
            return;
        }
        if (fixedFrameMicroseconds <= 0) throw new ArgumentOutOfRangeException(nameof(fixedFrameMicroseconds));
        long hostMicroseconds = 0;
        while (hostMicroseconds < totalMicroseconds && host.State == RuntimeState.Running)
        {
            writeInputs(hostMicroseconds);
            host.Update(fixedFrameMicroseconds.Value);
            hostMicroseconds += fixedFrameMicroseconds.Value;
        }
    }
}
