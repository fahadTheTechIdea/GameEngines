using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Standard
{
    /// <summary>A company's cash differs from what it was at the previous tick boundary. Published only on change.</summary>
    public readonly struct BusinessCashChanged
    {
        public const string EventId = "sim.tycoon.cash-changed";
        public PlayerId Company { get; }
        public Fix Before { get; }
        public Fix After { get; }
        public BusinessCashChanged(PlayerId company, Fix before, Fix after) { Company = company; Before = before; After = after; }
    }

    /// <summary>
    /// Tells the rest of the session when cash changed. The accounts commit through several staged transactions, so the
    /// one place that sees every change is the tick boundary: this compares each company's cash with the last boundary
    /// and publishes one event per company that differs. Nothing is published for a tick where cash did not change.
    /// The baseline is not saved: it is re-read from the settled accounts after a new game or a load.
    /// </summary>
    internal sealed class CashChangeSystem : GameSystem
    {
        private readonly World _world;
        private readonly IEconomy _accounts;
        private Fix[] _last;

        public CashChangeSystem(World world, IEconomy accounts) { _world = world; _accounts = accounts; }

        public override string Id => "sim.tycoon.cash-changes";
        public override int StateSchemaVersion => 1;

        public override void OnNewGame(World world) => Rebase();
        public override void OnLoaded(World world) => Rebase();

        private void Rebase()
        {
            _last = new Fix[_world.Players.Count];
            for (int i = 0; i < _last.Length; i++) _last[i] = _accounts.Cash(new PlayerId(i));
        }

        public override void Update(World world, TickInfo tick)
        {
            if (_last == null) Rebase();
            for (int i = 0; i < _last.Length; i++)
            {
                Fix now = _accounts.Cash(new PlayerId(i));
                if (now == _last[i]) continue;
                var change = new BusinessCashChanged(new PlayerId(i), _last[i], now);
                _last[i] = now;
                world.Events.Publish(change);
            }
        }
    }
}
