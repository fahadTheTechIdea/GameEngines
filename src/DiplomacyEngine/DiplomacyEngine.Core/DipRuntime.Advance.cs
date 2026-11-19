using EngineCommon;
using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

public sealed partial class DipRuntime
{
    /// <summary>
    /// Lives the steps after <see cref="Step"/> up to <paramref name="toStep"/>, one at a time, in order. <paramref name="seasonAt"/> gives the season (an id of the document) of each step: the calendar's, or
    /// <see cref="CycleSeasonAt"/>. <paramref name="host"/> are the facts the document declares for the host, as they are now, used for every step of the call. Everything is checked before the first step runs, so a
    /// refusal leaves the world as it was. Returns what happened, in order.
    /// </summary>
    public DipChanges Advance(long toStep, Func<long, string> seasonAt, IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(seasonAt);
        ArgumentNullException.ThrowIfNull(host);
        if (toStep < Step) throw new EngineRefusedException(DipCodes.StepOrder, "the world is at step " + Step + " and does not go back to " + toStep);
        var changes = new DipChanges();
        if (toStep == Step) return changes;
        if (toStep - Step > MaxAdvance) throw new EngineRefusedException(DipCodes.StepOrder, "one call goes at most " + MaxAdvance + " steps; it was asked to go " + (toStep - Step));
        CheckHost(host);
        var seasons = new int[toStep - Step];
        for (var i = 0; i < seasons.Length; i++)
        {
            var id = seasonAt(Step + 1 + i);
            if (!Set.SeasonById.TryGetValue(id, out var season)) throw new EngineRefusedException(DipCodes.UnknownSeason, "there is no season '" + id + "' (step " + (Step + 1 + i) + ")");
            seasons[i] = season.Index;
        }
        _host = host;
        for (var i = 0; i < seasons.Length; i++) Once(Step + 1, seasons[i], changes);
        return changes;
    }

    /// <summary>The season at a step when the game has no calendar: the document's seasons in order, each as long as one season's share of a year of 360 days.</summary>
    public string CycleSeasonAt(long step) => Set.CycleSeasonAt(step);

    private void CheckHost(IReadOnlyDictionary<string, FactValue> host)
    {
        foreach (var (id, definition) in Set.HostFacts)
        {
            if (!host.TryGetValue(id, out var value)) throw new EngineRefusedException(DipCodes.FactMissing, "the document reads the host fact '" + id + "' and the host did not give it");
            if (value.Kind != definition.Kind) throw new EngineRefusedException(DipCodes.FactInvalid, "host fact '" + id + "' is " + value.Kind + ", the document declares " + definition.Kind);
            if (definition.Kind == FactKind.Integer && (value.Integer < definition.Minimum || value.Integer > definition.Maximum))
                throw new EngineRefusedException(DipCodes.FactInvalid, "host fact '" + id + "' is " + value.Integer + ", outside " + definition.Minimum + " to " + definition.Maximum);
        }
    }

    /// <summary>One step. The order is fixed: season, what fades (standing, deeds, intelligence, casus belli), the realms' internal politics, treaties (ends, then payments), calls to arms, proposals, wars, missions, succession, the balance of power.</summary>
    private void Once(long step, int season, DipChanges changes)
    {
        _s.Step = step;
        var seasonChanged = season != _s.Season;
        if (seasonChanged)
        {
            changes.Add(ChangeKind.Season, step, index: season, sub: _s.Season);
            _s.Season = season;
        }
        Fade(step, changes);
        LivePolitics(step, changes);
        LiveTreaties(step, seasonChanged, changes);
        LiveCalls(step, changes);
        LiveProposals(step, changes);
        LiveWars(step, changes);
        LiveMissions(step, changes);
        LiveSuccession(step, changes);
        LiveBalance(step, changes);
    }

    /// <summary>Standing, deeds, knowledge and granted casus belli fade with time.</summary>
    private void Fade(long step, DipChanges changes)
    {
        var rules = Set.Standing;
        foreach (var realm in _s.Realms)
        {
            realm.Trust = DipMath.Approach(realm.Trust, 0, rules.TrustDecayPpm);
            realm.Fear = DipMath.Approach(realm.Fear, 0, rules.FearDecayPpm);
            realm.Honour = DipMath.Approach(realm.Honour, 0, rules.HonourDecayPpm);
            realm.Infamy = DipMath.Approach(realm.Infamy, 0, rules.InfamyDecayPpm);
            realm.CounterIntel = (int)DipMath.Approach(realm.CounterIntel, 0, Set.Espionage.CounterDecayPpm);
        }
        for (var i = 0; i < _s.Intel.Count; i++)
        {
            var before = LevelOf(_s.Intel[i]);
            _s.Intel[i] = (int)DipMath.Approach(_s.Intel[i], 0, Set.Espionage.IntelDecayPpm);
            var after = LevelOf(_s.Intel[i]);
            if (after != before) changes.Add(ChangeKind.Intel, step, i / _n, i % _n, (int)after, (int)before);
        }
        for (var i = _s.Deeds.Count - 1; i >= 0; i--)
        {
            var deed = _s.Deeds[i];
            if (step - deed.Start < Set.Steps(Set.Deeds[deed.Deed].Duration)) continue;
            _s.Deeds.RemoveAt(i);
            _deedsAt[Pair(deed.Holder, deed.Subject)]!.Remove(deed);
        }
        for (var i = 0; i < _s.Grants.Count; i++)
        {
            var grant = _s.Grants[i];
            if (grant.Until > step) continue;
            _s.Grants.RemoveAt(i--);
            changes.Add(ChangeKind.Grant, step, grant.Holder, grant.Target, grant.Cb, 0, (int)GrantChange.Expired);
        }
    }
}
