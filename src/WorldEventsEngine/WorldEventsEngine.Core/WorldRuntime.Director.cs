namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    /// <summary>
    /// Adds to the director's stress: the difficulty's stress gain scales what adds, and what relieves it (negative) is taken as it is. The stress is the director's belief about how hard a time the player has had, kept
    /// between 0 and 1,000.
    /// </summary>
    private void AddStress(long amount, bool scaled)
    {
        if (amount > 0 && scaled) amount = amount * Set.Difficulties[_difficulty].StressGain / WorldMath.Permille;
        _stress = WorldMath.Clamp(_stress + amount, 0, WorldMath.Permille);
        Touch();
    }

    /// <summary>
    /// The director's pacing, after Left 4 Dead's: while building, trouble may come; when the stress reaches the peak the director stops adding bad things for a hold, then rests until the stress has fallen below the
    /// relax line and the rest has lasted its minimum, then builds again. The stress decays by a share each step. A storyteller that never peaks (the line above 1,000) is the random one. Natural hazards and seeded
    /// diseases that are <i>paced</i> are held back outside the build phase (counted, not queued), and every hazard, outbreak and card adds its stress, so the pace follows what happened and not only what the director drew.
    /// </summary>
    private void LiveDirector(long step, WorldChanges changes)
    {
        var teller = Set.Storytellers[_storyteller];
        _stress = _stress * (WorldMath.Permille - teller.Decay) / WorldMath.Permille;
        var before = _phase;
        var elapsed = step - _phaseStart + 1;
        if (_phase == DirectorPhase.Build && _stress >= teller.PeakAt) _phase = DirectorPhase.Peak;
        else if (_phase == DirectorPhase.Peak && elapsed > teller.PeakHold) _phase = DirectorPhase.Relax;
        else if (_phase == DirectorPhase.Relax && _stress <= teller.RelaxBelow && elapsed > teller.RestMin) _phase = DirectorPhase.Build;
        if (_phase == before) return;
        _phaseStart = step;
        changes.Add(ChangeKind.Director, step, index: (int)_phase, sub: (int)before, amount: _stress);
    }

    /// <summary>
    /// The draw from the deck. After the minimum gap since the last card the chance of drawing rises step by step to certainty at the maximum gap, so cards are spaced without being regular. A card can be drawn if its weight
    /// (times its tier's share in the storyteller, and for bad cards the difficulty's) is above zero, its cooldown is over, its group is free, bad cards only while building, and its condition holds in some region (or in
    /// the world, for a global card). The card is picked by weight, the region among those where it holds.
    /// </summary>
    private void LiveDraw(long step, WorldChanges changes)
    {
        if (Set.Cards.Count == 0) return;
        var teller = Set.Storytellers[_storyteller];
        var since = step - _lastCard;
        if (since < teller.GapMin) return;
        var chance = since >= teller.GapMax ? WorldMath.Permille : (since - teller.GapMin + 1) * WorldMath.Permille / (teller.GapMax - teller.GapMin + 1);
        if (Draw(WorldMath.Permille) >= chance) return;
        var difficulty = Set.Difficulties[_difficulty];
        var cards = new List<(CardDef Card, IReadOnlyList<int> Regions)>();
        var weights = new List<long>();
        foreach (var card in Set.Cards)
        {
            if (card.Weight == 0 || CardReadyAt[card.Index] > step || card.Group >= 0 && GroupLockedUntil[card.Group] > step) continue;
            if (card.Tier == Tier.Bad && _phase != DirectorPhase.Build) continue;
            var weight = (long)card.Weight * teller.Share[(int)card.Tier] / WorldMath.Permille;
            if (card.Tier == Tier.Bad) weight = weight * difficulty.Bad / WorldMath.Permille;
            if (weight == 0) continue;
            var regions = EligibleRegions(card);
            if (regions.Count == 0) continue;
            cards.Add((card, regions));
            weights.Add(weight);
        }
        if (cards.Count == 0) return;
        var (chosen, where) = cards[WorldMath.Pick(weights, Draw(weights.Sum()))];
        FireCard(chosen.Index, chosen.Global ? -1 : where[(int)Draw(where.Count)], CardReason.Drawn, step, changes);
    }

    /// <summary>The regions in which a card's condition holds (all of them if it has none); a global card's single "region" is -1 when its world condition holds.</summary>
    private IReadOnlyList<int> EligibleRegions(CardDef card)
    {
        if (card.Global) return card.When is null || HoldsGlobal(card.When) ? [-1] : [];
        var eligible = new List<int>();
        for (var r = 0; r < Set.Regions.Count; r++)
            if (card.When is null || Holds(card.When, r)) eligible.Add(r);
        return eligible;
    }
}
