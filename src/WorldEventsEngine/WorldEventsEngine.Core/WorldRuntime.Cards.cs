namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    /// <summary>
    /// Plays a card: its cooldown starts, its group is held for its duration, its stress is added, a <see cref="ChangeKind.Card"/> is recorded and its effects are issued. Then each chain entry is rolled, and the ones that
    /// come up wait in the agenda for their delay. Returns nothing: what a card did is in <paramref name="changes"/>.
    /// </summary>
    private void FireCard(int index, int region, CardReason reason, long step, WorldChanges changes)
    {
        var card = Set.Cards[index];
        CardReadyAt[index] = step + 1 + card.Cooldown;
        if (card.Group >= 0 && card.Duration > 0) GroupLockedUntil[card.Group] = Math.Max(GroupLockedUntil[card.Group], step + card.Duration);
        AddStress(card.Intensity, card.Intensity > 0);
        if (reason != CardReason.Chained) _lastCard = step;
        _played++;
        Touch();
        changes.Add(ChangeKind.Card, step, region, index, sub2: (int)reason, amount: _stress);
        IssueEffects(card.Effects, region, "card:" + card.Id, WorldMath.Permille, index, step, changes);
        for (var link = 0; link < card.Chain.Count; link++)
        {
            if (Draw(WorldMath.Permille) >= card.Chain[link].Probability) continue;
            if (_agenda.Count >= Set.Limits.MaxAgenda)
            {
                _dropped++;
                continue;
            }
            _agenda.Add(new AgendaItem { Due = step + card.Chain[link].Delay, Card = card.Chain[link].Card, Region = region, Parent = index, Link = link });
        }
    }

    /// <summary>
    /// What a hazard, an outbreak or a card asks of the other layers and of the engine itself, at a power (per mille). A signal is recorded for the story; a shock for the economy (its scale times the power; below one
    /// per mille nothing is asked); a modifier of a card is held for its duration (a hazard's or a disease's is read live from its intensity); a hazard or an outbreak is started in the region.
    /// </summary>
    private void IssueEffects(IReadOnlyList<EffectDef> effects, int region, string source, int power, int card, long step, WorldChanges changes)
    {
        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case EffectKind.Signal:
                    changes.Add(ChangeKind.Effect, step, region, (int)EffectKind.Signal, name: effect.Name, source: source);
                    break;
                case EffectKind.Shock:
                {
                    var scale = (long)effect.Amount * power / WorldMath.Permille;
                    if (scale >= 1) changes.Add(ChangeKind.Effect, step, region, (int)EffectKind.Shock, amount: scale, name: effect.Name, source: source);
                    break;
                }
                case EffectKind.Modifier:
                    if (card < 0) break;
                    if (_timed.Count >= Set.Limits.MaxEffects) _dropped++;
                    else _timed.Add(new TimedModifier { Card = card, Region = region, Modifier = effect.Index, Delta = effect.Amount, Until = step + effect.Duration });
                    break;
                case EffectKind.Hazard:
                    StartHazard(effect.Index, region, effect.Rung, WorldMath.Permille, true, (ulong)Draw(long.MaxValue), step, changes);
                    break;
                case EffectKind.Outbreak:
                    if (Introduce(region, effect.Index, Math.Max(1, (long)effect.Amount * power / WorldMath.Permille)) > 0) TrackOutbreak(region, effect.Index, step, changes);
                    break;
            }
        }
    }

    /// <summary>The cards chains scheduled that are due: each is played in the region its parent was played in (a global parent lets a regional card pick where its condition holds), if its own condition and the chain entry's still hold and its group is free.</summary>
    private void LiveAgenda(long step, WorldChanges changes)
    {
        foreach (var item in _agenda.Where(a => a.Due <= step).ToArray())
        {
            _agenda.Remove(item);
            var card = Set.Cards[item.Card];
            var link = Set.Cards[item.Parent].Chain[item.Link];
            var parentGlobal = Set.Cards[item.Parent].Global;
            if (link.When is not null && !(parentGlobal ? HoldsGlobal(link.When) : Holds(link.When, item.Region))) continue;
            if (card.Group >= 0 && GroupLockedUntil[card.Group] > step) continue;
            int region;
            if (card.Global) region = -1;
            else if (item.Region >= 0) region = item.Region;
            else
            {
                var eligible = EligibleRegions(card);
                if (eligible.Count == 0) continue;
                region = eligible[(int)Draw(eligible.Count)];
            }
            if (card.When is not null && !(card.Global ? HoldsGlobal(card.When) : Holds(card.When, region))) continue;
            FireCard(item.Card, region, CardReason.Chained, step, changes);
        }
    }
}
