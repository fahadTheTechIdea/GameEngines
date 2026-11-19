using System;
using Beep.SimEngine.Hosting;

namespace Beep.SimEngine.Testing
{
    /// <summary>
    /// Test-only driver: the engine has no clock or pacing, so tests that want n ticks name each tick to the simulation,
    /// exactly as a host layer's clock would. Stops early on a fault or an outcome.
    /// </summary>
    public static class SimGameDriver
    {
        public static int RunTicks(this SimGame game, int ticks)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            int ran = 0;
            while (ran < ticks && game.Update(game.Tick + 1)) ran++;
            return ran;
        }
    }
}
