using System.Text;
using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>The save: a round trip is exact, a resumed run equals a straight one, and a forged or damaged save is refused with the code of what is wrong, ranges before the digest.</summary>
public sealed class SaveTests
{
    private static (DipRuntime Runtime, DipSet Set, byte[] Save) Saved()
    {
        var runtime = Tamper.Played(out var set);
        return (runtime, set, DipSave.Write(runtime));
    }

    [Fact]
    public void ThePlayedWorldHasEverythingTheForgeriesNeed()
    {
        var (runtime, _, _) = Saved();
        Assert.NotEmpty(runtime.Treaties);
        Assert.NotEmpty(runtime.Proposals);
        Assert.NotEmpty(runtime.Wars);
        Assert.NotEmpty(runtime.Calls);
        Assert.NotEmpty(runtime.Grants);
        Assert.NotEmpty(runtime.Coalitions);
        Assert.NotEmpty(runtime.Missions);
        Assert.NotEmpty(runtime.Incidents);
        Assert.NotEmpty(runtime.State.Deeds);
        Assert.Contains(runtime.Treaties, t => t.Awaiting);
    }

    [Fact]
    public void ASaveOpensToTheSameStateAndTheSameDigest()
    {
        var (runtime, set, save) = Saved();
        var reopened = DipSave.Open(set, save);
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Assert.Equal(64, runtime.StateDigest().Length);
        Assert.Equal(save, DipSave.Write(reopened));
    }

    [Fact]
    public void ARunResumedFromASaveIsTheStraightRun()
    {
        var (straight, set, save) = Saved();
        var resumed = DipSave.Open(set, save);
        var a = Fixture.Live(straight, straight.Step + 120).Select(c => DipChangeText.Line(set, c)).ToList();
        var b = Fixture.Live(resumed, resumed.Step + 120).Select(c => DipChangeText.Line(set, c)).ToList();
        Assert.Equal(a, b);
        Assert.Equal(straight.StateDigest(), resumed.StateDigest());
        Assert.NotEmpty(a);
    }

    [Fact]
    public void TheDigestIsOfTheStateAndDiffersWhenAnyPartOfItDoes()
    {
        var one = Fixture.PlainWorld();
        var two = Fixture.PlainWorld();
        Assert.Equal(one.StateDigest(), two.StateDigest());
        two.SetMight("aurel", 1);
        Assert.NotEqual(one.StateDigest(), two.StateDigest());
        var three = Fixture.PlainWorld();
        three.Remember("aurel", "brass", "gift", 1);
        Assert.NotEqual(one.StateDigest(), three.StateDigest());
        var four = Fixture.PlainWorld();
        four.Recruit("aurel", "extra", 1);
        Assert.NotEqual(one.StateDigest(), four.StateDigest());
    }

    // ---- damaged saves --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASaveThatIsNotASaveOrIsOfAnotherFormatOrDocumentIsRefusedWithItsCode()
    {
        var (_, set, save) = Saved();
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, Encoding.UTF8.GetBytes("not json"))));
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, Encoding.UTF8.GetBytes("null"))));
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, Encoding.UTF8.GetBytes("{}"))));
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, Tamper.Stale(save, s => s.Format = 2))));
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, new byte[DipSave.MaxBytes + 1])));
        var other = Fixture.Edited(root => root["seed"] = 5);
        Assert.Equal(DipCodes.SaveDocument, Fixture.Refusal(() => DipSave.Open(other, save)));
        Assert.Equal(DipCodes.SaveDocument, Fixture.Refusal(() => DipSave.Open(set, Tamper.Forge(save, s => s.Id = "other"))));
    }

    [Fact]
    public void AFieldMissingOrUnknownInASaveIsRefusedAndNeverCompleted()
    {
        var (_, set, save) = Saved();
        var tree = JsonNode.Parse(save)!.AsObject();
        tree.Remove("Leader");
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, Encoding.UTF8.GetBytes(tree.ToJsonString()))));
        var extra = JsonNode.Parse(save)!.AsObject();
        extra["Extra"] = 1;
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, Encoding.UTF8.GetBytes(extra.ToJsonString()))));
        var inner = JsonNode.Parse(save)!.AsObject();
        inner["Realms"]!.AsArray()[0]!.AsObject().Remove("Might");
        Assert.Equal(DipCodes.SaveFormat, Fixture.Refusal(() => DipSave.Open(set, Encoding.UTF8.GetBytes(inner.ToJsonString()))));
    }

    [Fact]
    public void AStateChangedWithoutItsDigestIsRefusedByTheDigestAndAnImpossibleOneByTheRulesFirst()
    {
        var (_, set, save) = Saved();
        Assert.Equal(DipCodes.SaveDigest, Fixture.Refusal(() => DipSave.Open(set, Tamper.Stale(save, s => s.Realms[0].Might += 1))));
        Assert.Equal(DipCodes.SaveDigest, Fixture.Refusal(() => DipSave.Open(set, Tamper.Stale(save, s => s.Step += 1))));
        // The range is checked before the digest is read: an impossible value with a stale digest is refused as a state.
        Assert.Equal(DipCodes.SaveState, Fixture.Refusal(() => DipSave.Open(set, Tamper.Stale(save, s => s.Realms[0].Stability = 5000))));
        Assert.Equal(DipCodes.SaveState, Fixture.Refusal(() => DipSave.Open(set, Tamper.Forge(save, s => s.Realms[0].Stability = 5000))));
    }

    public static readonly Dictionary<string, Action<DipState>> Forgeries = new()
    {
        ["step below -1"] = s => s.Step = -2,
        ["season out of range"] = s => s.Season = 9,
        ["negative next treaty"] = s => s.NextTreaty = -1,
        ["a realm missing"] = s => s.Realms.RemoveAt(0),
        ["a realm too many"] = s => s.Realms.Add(new RealmState { Satisfaction = [1, 1] }),
        ["negative might"] = s => s.Realms[0].Might = -1,
        ["stability past a thousand"] = s => s.Realms[0].Stability = 1001,
        ["legitimacy negative"] = s => s.Realms[0].Legitimacy = -1,
        ["band out of range"] = s => s.Realms[0].Band = 3,
        ["trust past a million"] = s => s.Realms[0].Trust = 1_000_001,
        ["fear below minus a million"] = s => s.Realms[0].Fear = -1_000_001,
        ["negative infamy"] = s => s.Realms[0].Infamy = -1,
        ["counter-intelligence past a thousand"] = s => s.Realms[0].CounterIntel = 1001,
        ["negative generation"] = s => s.Realms[0].Generation = -1,
        ["a faction missing"] = s => s.Realms[0].Satisfaction.RemoveAt(0),
        ["contentment past a thousand"] = s => s.Realms[0].Satisfaction[0] = 1001,
        ["leaders' regard of the wrong size"] = s => s.Leader.RemoveAt(0),
        ["a leader's regard past a thousand"] = s => s.Leader[1] = 1001,
        ["intelligence of the wrong size"] = s => s.Intel.Add(0),
        ["intelligence past a million"] = s => s.Intel[1] = 1_000_001,
        ["a realm with regard of itself"] = s => s.Leader[0] = 5,
        ["a deed held of oneself"] = s => s.Deeds[0].Subject = s.Deeds[0].Holder,
        ["a deed of no strength"] = s => s.Deeds[0].Scale = 0,
        ["a deed that is not declared"] = s => s.Deeds[0].Deed = 99,
        ["too many deeds for a pair"] = s => { for (var i = 0; i < 4; i++) s.Deeds.Add(new DeedState { Holder = 1, Subject = 0, Deed = 0, Start = 0, Scale = 5 }); },
        ["a treaty repeated"] = s => s.Treaties.Add(new TreatyState { Id = s.Treaties[0].Id, Type = 0, First = 0, Second = 1, Start = 0, End = -1, NextDue = -1, Deadline = -1, War = -1 }),
        ["a treaty id that was never given out"] = s => s.Treaties[0].Id = s.NextTreaty,
        ["a treaty with itself"] = s => s.Treaties[0].Second = s.Treaties[0].First,
        ["a treaty of no type"] = s => s.Treaties[0].Type = 99,
        ["a negative tribute"] = s => s.Treaties[0].Amount = -5,
        ["payments missed to the limit"] = s => s.Treaties.First(t => t.Amount > 0).Missed = 2,
        ["a payment awaited on a treaty with no tribute"] = s => s.Treaties.First(t => t.Amount == 0).Awaiting = true,
        ["a tribute on a treaty with none"] = s => s.Treaties.First(t => t.Amount == 0).Amount = 5,
        ["a treaty of a type twice between the same realms"] = s => { var t = s.Treaties[0]; s.Treaties.Add(new TreatyState { Id = s.NextTreaty++, Type = t.Type, First = t.Second, Second = t.First, Start = 0, End = -1, NextDue = t.NextDue, Deadline = -1, Amount = t.Amount, War = -1 }); },
        ["a treaty that pauses a war it does not"] = s => s.Treaties[0].War = s.Wars[0].Id,
        ["a proposal for an imposed treaty"] = s => s.Proposals[0].Type = 7,
        ["a proposal to oneself"] = s => s.Proposals[0].To = s.Proposals[0].From,
        ["the same offer twice"] = s => s.Proposals.Add(new ProposalState { Id = s.NextProposal++, Type = s.Proposals[0].Type, From = s.Proposals[0].From, To = s.Proposals[0].To, Term = s.Proposals[0].Term, DecideAt = s.Proposals[0].DecideAt, ExpireAt = 99, Made = 0 }),
        ["a decision step for a host realm"] = s => s.Proposals[0].DecideAt = 5,
        ["a proposal with no end for a type that always has one"] = s => s.Proposals[0].Term = 0,
        ["an amount in a proposal with no tribute"] = s => s.Proposals[0].Amount = 5,
        ["the attacker not on its own side"] = s => s.Wars[0].AttackerSide = 0,
        ["a realm on both sides"] = s => s.Wars[0].DefenderSide |= s.Wars[0].AttackerSide,
        ["a side naming a realm that is not there"] = s => s.Wars[0].DefenderSide |= 1UL << 20,
        ["a score past a thousand"] = s => s.Wars[0].Score = 1001,
        ["exhaustion past a million"] = s => s.Wars[0].AttackerExhaustion = 1_000_001,
        ["a war with itself"] = s => s.Wars[0].Defender = s.Wars[0].Attacker,
        ["a ceasefire with no treaty"] = s => s.Wars[0].Ceasefire = true,
        ["a call in a war that is not there"] = s => s.Calls[0].War = 77,
        ["a call under a treaty that is not there"] = s => s.Calls[0].Treaty = 77,
        ["a call owed by a realm already defending"] = s => s.Wars[0].DefenderSide |= 1UL << s.Calls[0].Callee,
        ["a granted casus belli that is never granted"] = s => s.Grants[0].Cb = 0,
        ["a casus belli against oneself"] = s => s.Grants[0].Target = s.Grants[0].Holder,
        ["a coalition member that is the target"] = s => s.Coalitions[0].Members |= 1UL << s.Coalitions[0].Target,
        ["two coalitions against one realm"] = s => s.Coalitions.Add(new CoalitionState { Target = s.Coalitions[0].Target, Members = 0, Since = 0 }),
        ["an agent with a bad id"] = s => s.Agents[0].Id = "Bad Id",
        ["an agent away with no mission"] = s => { s.Agents[0].State = (int)AgentState.Away; },
        ["an agent home with a mission named"] = s => s.Agents.First(a => a.State == 0).Mission = 3,
        ["two agents with one id"] = s => s.Agents[1].Id = s.Agents[0].Id,
        ["an agent on another mission than the one that names it"] = s => s.Agents[s.Missions[0].Agent].Mission = 99,
        ["two missions for one agent"] = s => s.Missions.Add(new MissionRecord { Id = s.NextMission++, Agent = s.Missions[0].Agent, Target = s.Missions[0].Target, Type = s.Missions[0].Type, Start = 0, Due = 9 }),
        ["a counter mission against another realm"] = s => s.Missions[0].Type = 3,
        ["an incident of no kind"] = s => s.Incidents[0].Kind = 99,
        ["an incident between no realms"] = s => s.Incidents[0].A = 99,
        ["an incident from the future"] = s => s.Incidents[0].Step = s.Step + 5,
    };

    public static IEnumerable<object[]> ForgeryNames() => Forgeries.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(ForgeryNames))]
    public void AForgedStateWithItsDigestRecomputedIsRefusedAsAState(string name)
    {
        var (_, set, save) = Saved();
        var forged = Tamper.Forge(save, Forgeries[name]);
        Assert.Equal(DipCodes.SaveState, Fixture.Refusal(() => DipSave.Open(set, forged), name));
    }

    [Fact]
    public void ThereAreMoreThanFiftyForgeriesAndTheyAreAllDifferent()
    {
        Assert.True(Forgeries.Count >= 55, "only " + Forgeries.Count);
        var (_, _, save) = Saved();
        Assert.Equal(Forgeries.Count, Forgeries.Select(f => Convert.ToHexString(Tamper.Forge(save, f.Value))).Distinct().Count());
    }

    [Fact]
    public void ASaveFromAFreshWorldOpens()
    {
        var runtime = Fixture.PlainWorld();
        var save = DipSave.Write(runtime);
        var reopened = DipSave.Open(runtime.Set, save);
        Assert.Equal(-1, reopened.Step);
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
    }
}
