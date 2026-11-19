namespace WorldEventsEngine;

/// <summary>What kind of thing happened in the world.</summary>
public enum ChangeKind { Season, Regime, Weather, Hazard, Outbreak, Card, Director, Effect, Intervention }

/// <summary>Why a card was played.</summary>
public enum CardReason { Drawn, Chained, Forced }

/// <summary>
/// One thing that happened, in the order it happened. One record serves every kind so the order across kinds is kept exactly; what the fields hold depends on the kind:
/// <list type="bullet">
/// <item>Season: <c>Index</c> the season now, <c>Sub</c> the season before (-1 at the first step).</item>
/// <item>Regime: <c>Index</c> the regime now, <c>Sub</c> the regime before.</item>
/// <item>Weather: <c>Region</c>; <c>Index</c> the weather now, <c>Sub</c> before; <c>Amount</c> the temperature (hundredths of a degree).</item>
/// <item>Hazard: <c>Region</c>; <c>Index</c> the hazard; <c>Sub</c> the rung; <c>Sub2</c> a <see cref="HazardChange"/>; <c>Amount</c> the power (per mille of full strength).</item>
/// <item>Outbreak: <c>Region</c>; <c>Index</c> the disease; <c>Sub2</c> an <see cref="OutbreakChange"/>; <c>Amount</c> exposed and infectious people; <c>Amount2</c> per mille of the region.</item>
/// <item>Card: <c>Region</c> (-1 for a global card); <c>Index</c> the card; <c>Sub2</c> a <see cref="CardReason"/>; <c>Amount</c> the stress after.</item>
/// <item>Director: <c>Sub</c> the <see cref="DirectorPhase"/> before, <c>Index</c> now; <c>Amount</c> the stress.</item>
/// <item>Effect: what a hazard, an outbreak or a card asks of the other layers. <c>Index</c> the <see cref="EffectKind"/> (a signal or a shock; the other kinds are started inside the engine and are not issued); <c>Name</c> the signal or shock id;
/// <c>Region</c> (-1 for the whole world); <c>Amount</c> the scale of a shock (per mille); <c>Source</c> says what asked, as <c>hazard:flood</c>, <c>disease:plague</c> or <c>card:raiders</c>.</item>
/// <item>Intervention: <c>Region</c>; <c>Index</c> the intervention; <c>Sub</c> 1 when it started and 0 when it stopped; <c>Sub2</c> 1 when it started by itself (its automatic trigger).</item>
/// </list>
/// </summary>
public sealed record WorldChange(ChangeKind Kind, long Step, int Region, int Index, int Sub, int Sub2, long Amount, long Amount2, string Name, string Source);

/// <summary>Everything a call changed, in order. Empty when nothing happened.</summary>
public sealed class WorldChanges
{
    private readonly List<WorldChange> _items = new();

    public IReadOnlyList<WorldChange> Items => _items;

    internal void Add(WorldChange change) => _items.Add(change);

    internal void Add(ChangeKind kind, long step, int region = -1, int index = 0, int sub = 0, int sub2 = 0, long amount = 0, long amount2 = 0, string name = "", string source = "") =>
        _items.Add(new WorldChange(kind, step, region, index, sub, sub2, amount, amount2, name, source));

    internal void AddRange(WorldChanges other) => _items.AddRange(other._items);
}
