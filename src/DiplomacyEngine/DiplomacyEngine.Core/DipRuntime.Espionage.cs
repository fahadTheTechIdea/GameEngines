using EngineCommon;

namespace DiplomacyEngine;

/// <summary>What one realm believes of another's might: how well it knows it, and a band the true value is probably in (wide when it knows little, exact when it knows everything).</summary>
public sealed record IntelEstimate(IntelLevel Level, long Low, long Estimate, long High);

/// <summary>How a mission would go: the chance it succeeds, the chance it is exposed, and the lines of how they were made.</summary>
public sealed record MissionOdds(int Success, int Exposure, string Why);

public sealed partial class DipRuntime
{
    internal IntelLevel LevelOf(int intel)
    {
        var level = 0;
        foreach (var threshold in Set.Espionage.Levels)
            if (intel >= threshold * 1000L) level++;
        return (IntelLevel)level;
    }

    /// <summary>How well the first realm knows the second, as a level.</summary>
    public IntelLevel IntelOf(string observer, string target) => LevelOf(_s.Intel[Pair(FindRealm(observer).Index, FindRealm(target).Index)]);

    /// <summary>
    /// What the observer believes the target's might is. With no knowledge the band is as wide as the document's error for the level (up to the whole of the value), and the estimate is off the truth by a fixed amount inside
    /// that error, the same for everyone asking until the document's epoch of days turns (so asking twice does not average the error away); with complete knowledge it is exact.
    /// </summary>
    public IntelEstimate Estimate(string observer, string target)
    {
        var a = FindRealm(observer).Index;
        var b = FindRealm(target).Index;
        if (a == b) throw new EngineRefusedException(DipCodes.SameRealm, "a realm knows its own might exactly");
        var level = LevelOf(_s.Intel[Pair(a, b)]);
        var error = Set.Espionage.Errors[(int)level];
        var truth = _s.Realms[b].Might;
        var epoch = Math.Max(0, _s.Step) / Set.Steps(Set.Espionage.EpochDays);
        var offset = error == 0 ? 0 : Roll("est:" + Set.Realms[a].Id + ":" + Set.Realms[b].Id, epoch, 2L * error + 1) - error;
        var mid = Math.Max(0, truth * (1000 + offset) / 1000);
        return new IntelEstimate(level, mid * (1000 - error) / 1000, mid, mid * (1000 + error) / 1000);
    }

    private int Counter(int realm) => DipMath.ClampInt(Set.Realms[realm].Traits.Sum(t => Set.Traits[t].CounterIntel) + _s.Realms[realm].CounterIntel, 0, 1000);

    private int Skill(AgentRecord agent) => DipMath.ClampInt(agent.Skill + Set.Realms[agent.Owner].Traits.Sum(t => Set.Traits[t].SpyBonus), 0, 1000);

    private MissionOdds Odds(AgentRecord agent, int target, MissionDef def)
    {
        var skill = Skill(agent);
        var counter = def.Kind == MissionKind.Counter ? 0 : Counter(target);
        var success = DipMath.ClampInt(1000 - def.Difficulty + (skill - 500) / 2 - counter * Set.Espionage.CounterWeight / 1000, 50, 950);
        var exposure = def.Kind == MissionKind.Counter ? 0 : def.Exposure * (1000 - skill / 2) / 1000 * (1000 + counter) / 1000;
        var onSuccess = DipMath.ClampInt(exposure * Set.Espionage.ExposedOnSuccess / 1000, 0, 1000);
        exposure = DipMath.ClampInt(exposure, 0, 1000);
        var why = "success 1000 - difficulty " + def.Difficulty + " + (skill " + skill + " - 500) / 2 - counter-intelligence " + counter + " x " + Set.Espionage.CounterWeight + " / 1000 = " + success + " (kept within 50 and 950); exposure " +
            def.Exposure + " x (1000 - skill / 2) / 1000 x (1000 + " + counter + ") / 1000 = " + exposure + ", and " + onSuccess + " when it succeeds";
        return new MissionOdds(success, exposure, why);
    }

    private AgentRecord FindAgent(string id, out int index)
    {
        index = _s.Agents.FindIndex(a => a.Id == id);
        if (index < 0) throw new EngineRefusedException(DipCodes.UnknownAgent, "there is no agent '" + id + "'");
        return _s.Agents[index];
    }

    /// <summary>The chances of a mission, as the engine would roll them: for tools and for a host that wants to show the player.</summary>
    public MissionOdds OddsOf(string agent, string target, string mission)
    {
        var record = FindAgent(agent, out _);
        var t = FindRealm(target).Index;
        if (!Set.MissionById.TryGetValue(mission, out var def)) throw new EngineRefusedException(DipCodes.UnknownMission, "there is no mission '" + mission + "'");
        return Odds(record, t, def);
    }

    /// <summary>A realm takes on a new agent.</summary>
    public DipChanges Recruit(string owner, string agentId, int skill)
    {
        var realm = FindRealm(owner).Index;
        new StrictJson(DipCodes.ForDocument).CheckId(agentId, "agent id");
        if (skill < 0 || skill > 1000) throw new EngineRefusedException(DipCodes.Terms, "skill is 0 to 1000, not " + skill);
        if (_s.Agents.Any(a => a.Id == agentId)) throw new EngineRefusedException(DipCodes.AlreadyActive, "there is already an agent '" + agentId + "'");
        if (_s.Agents.Count >= Set.Limits.MaxAgents) throw new EngineRefusedException(DipCodes.Capacity, "the world holds " + Set.Limits.MaxAgents + " agents");
        _s.Agents.Add(new AgentRecord { Id = agentId, Owner = realm, Skill = skill, State = (int)AgentState.Idle, Mission = -1, FreeAt = 0 });
        var changes = new DipChanges();
        changes.Add(ChangeKind.Agent, _s.Step, realm, -1, _s.Agents.Count - 1, 0, (int)AgentChange.Recruited);
        return changes;
    }

    /// <summary>An agent of a realm is sent against another realm (or, on a counter-intelligence mission, at home).</summary>
    public DipChanges StartMission(string agentId, string target, string mission)
    {
        var agent = FindAgent(agentId, out var index);
        var t = FindRealm(target).Index;
        if (!Set.MissionById.TryGetValue(mission, out var def)) throw new EngineRefusedException(DipCodes.UnknownMission, "there is no mission '" + mission + "'");
        if (agent.State != (int)AgentState.Idle) throw new EngineRefusedException(DipCodes.Unavailable, "agent '" + agentId + "' is " + EnumNames.Name((AgentState)agent.State));
        if ((def.Kind == MissionKind.Counter) != (t == agent.Owner)) throw new EngineRefusedException(DipCodes.Terms, def.Kind == MissionKind.Counter ? "a counter-intelligence mission is at home" : "'" + mission + "' is against another realm");
        if (_s.Missions.Count >= Set.Limits.MaxMissions) throw new EngineRefusedException(DipCodes.Capacity, "the world holds " + Set.Limits.MaxMissions + " missions");
        var record = new MissionRecord { Id = _s.NextMission++, Agent = index, Target = t, Type = def.Index, Start = _s.Step, Due = Math.Max(0, _s.Step) + Set.Steps(def.Duration) };
        _s.Missions.Add(record);
        agent.State = (int)AgentState.Away;
        agent.Mission = record.Id;
        var changes = new DipChanges();
        changes.Add(ChangeKind.Mission, _s.Step, agent.Owner, t, (int)record.Id, def.Index, (int)MissionChange.Started, index);
        return changes;
    }

    /// <summary>A captured agent is released before the days of captivity are over (a ransom, a pardon).</summary>
    public DipChanges Release(string agentId)
    {
        var agent = FindAgent(agentId, out var index);
        if (agent.State != (int)AgentState.Captured) throw new EngineRefusedException(DipCodes.Unavailable, "agent '" + agentId + "' is not held");
        agent.State = (int)AgentState.Idle;
        var changes = new DipChanges();
        changes.Add(ChangeKind.Agent, _s.Step, agent.Owner, -1, index, 0, (int)AgentChange.Released);
        return changes;
    }

    private void LiveMissions(long step, DipChanges changes)
    {
        for (var i = 0; i < _s.Agents.Count; i++)
        {
            var agent = _s.Agents[i];
            if (agent.State != (int)AgentState.Captured || agent.FreeAt > step) continue;
            agent.State = (int)AgentState.Idle;
            changes.Add(ChangeKind.Agent, step, agent.Owner, -1, i, 0, (int)AgentChange.Released);
        }
        foreach (var mission in _s.Missions.Where(m => m.Due <= step).ToList()) Complete(mission, step, changes);
    }

    private void Complete(MissionRecord mission, long step, DipChanges changes)
    {
        _s.Missions.Remove(mission);
        var def = Set.Missions[mission.Type];
        var agent = _s.Agents[mission.Agent];
        var odds = Odds(agent, mission.Target, def);
        var success = Roll("msn:" + mission.Id, 0, 1000) < odds.Success;
        var exposureChance = success ? DipMath.ClampInt(odds.Exposure * Set.Espionage.ExposedOnSuccess / 1000, 0, 1000) : odds.Exposure;
        var exposed = Roll("msn:" + mission.Id, 1, 1000) < exposureChance;
        var owner = agent.Owner;
        if (success) Succeeded(mission, def, owner, step, changes);
        var outcome = success ? (exposed ? MissionChange.SucceededExposed : MissionChange.Succeeded) : (exposed ? MissionChange.FailedExposed : MissionChange.Failed);
        changes.Add(ChangeKind.Mission, step, owner, mission.Target, (int)mission.Id, def.Index, (int)outcome, mission.Agent);
        if (!exposed)
        {
            agent.State = (int)AgentState.Idle;
            agent.Mission = -1;
            return;
        }
        agent.State = (int)AgentState.Captured;
        agent.Mission = -1;
        agent.FreeAt = step + Set.Steps(Set.Espionage.CaptureDays);
        changes.Add(ChangeKind.Agent, step, owner, -1, mission.Agent, 0, (int)AgentChange.Captured);
        AddHonour(owner, -Set.Espionage.ExposedHonour);
        AddDeed(mission.Target, owner, def.Deed, 1000);
        if (def.GrantsCb >= 0) Grant(mission.Target, owner, def.GrantsCb, Set.Steps(Set.Espionage.CaptureDays), changes);
        Apply(owner, PoliticalEvent.SpyExposed, 1000);
        Log(IncidentKind.SpyExposed, owner, mission.Target, def.Index, mission.Id, changes);
    }

    private void Succeeded(MissionRecord mission, MissionDef def, int owner, long step, DipChanges changes)
    {
        var target = _s.Realms[mission.Target];
        switch (def.Kind)
        {
            case MissionKind.Intel:
            {
                var at = Pair(owner, mission.Target);
                var before = LevelOf(_s.Intel[at]);
                _s.Intel[at] = (int)Math.Min(DipMath.Million, _s.Intel[at] + def.Gain * 1000L);
                var after = LevelOf(_s.Intel[at]);
                if (after != before) changes.Add(ChangeKind.Intel, step, owner, mission.Target, (int)after, (int)before);
                break;
            }
            case MissionKind.Sabotage:
                target.Stability = DipMath.ClampInt(target.Stability - def.StabilityHit, 0, 1000);
                if (def.Shock.Length > 0) changes.Add(ChangeKind.Effect, step, mission.Target, owner, (int)EffectKind.Shock, 0, 0, def.ShockScale, -1, def.Shock, "mission:" + def.Id);
                break;
            case MissionKind.Incite:
                target.Stability = DipMath.ClampInt(target.Stability - def.StabilityHit, 0, 1000);
                break;
            case MissionKind.Counter:
                _s.Realms[owner].CounterIntel = DipMath.ClampInt(_s.Realms[owner].CounterIntel + def.Gain, 0, 1000);
                break;
        }
    }
}
