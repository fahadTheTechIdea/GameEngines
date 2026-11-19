using DiplomacyEngine;
using EngineCommon;

namespace GameRuntime;

public sealed partial class DiplomacyComponent
{
    /// <summary>What the changes of a call or of the steps lived become as events, in the order they happened. Each is built now (the names are read now) and raised later.</summary>
    private List<Action<IEventPort>> EventsOf(long tick, DipChanges changes)
    {
        var set = Runtime.Set;
        var events = new List<Action<IEventPort>>(changes.Items.Count);
        string Realm(int index) => index < 0 ? "" : set.Realms[index].Id;
        foreach (var c in changes.Items)
        {
            switch (c.Kind)
            {
                case ChangeKind.Season:
                {
                    var e = new DiplomacySeasonChanged(Id, Channel, tick, c.Step, set.Seasons[c.Index].Id, c.Sub < 0 ? "" : set.Seasons[c.Sub].Id);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Proposal:
                {
                    var e = new DiplomacyProposalChanged(Id, Channel, tick, c.Step, c.Index, set.Types[c.Sub].Id, Realm(c.A), Realm(c.B), EnumNames.Name((ProposalChange)c.Sub2), c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Treaty:
                {
                    var e = new DiplomacyTreatyChanged(Id, Channel, tick, c.Step, c.Index, set.Types[c.Sub].Id, Realm(c.A), Realm(c.B), EnumNames.Name((TreatyChange)c.Sub2), c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Obligation:
                {
                    var e = new DiplomacyObligationChanged(Id, Channel, tick, c.Step, c.Index, EnumNames.Name((ClauseKind)c.Sub), Realm(c.A), Realm(c.B), EnumNames.Name((ObligationChange)c.Sub2), c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.War:
                {
                    var joinedOrLeft = c.Sub2 is (int)WarChange.Joined or (int)WarChange.Left;
                    var e = new DiplomacyWarChanged(Id, Channel, tick, c.Step, c.Index, Realm(c.A), Realm(c.B), EnumNames.Name((WarChange)c.Sub2), c.Sub < 0 ? "" : set.CasusBelli[c.Sub].Id, c.Amount, joinedOrLeft ? Realm((int)c.Amount2) : "");
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Coalition:
                {
                    var e = new DiplomacyCoalitionChanged(Id, Channel, tick, c.Step, Realm(c.A), Realm(c.B), EnumNames.Name((CoalitionChange)c.Sub2), c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Mission:
                {
                    var e = new DiplomacyMissionChanged(Id, Channel, tick, c.Step, c.Index, set.Missions[c.Sub].Id, Realm(c.A), Realm(c.B), EnumNames.Name((MissionChange)c.Sub2), Runtime.Agents[(int)c.Amount].Id);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Agent:
                {
                    var e = new DiplomacyAgentChanged(Id, Channel, tick, c.Step, Runtime.Agents[c.Index].Id, Realm(c.A), EnumNames.Name((AgentChange)c.Sub2));
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Intel:
                {
                    var e = new DiplomacyIntelChanged(Id, Channel, tick, c.Step, Realm(c.A), Realm(c.B), EnumNames.Name((IntelLevel)c.Index), EnumNames.Name((IntelLevel)c.Sub));
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Politics:
                {
                    var e = new DiplomacyPoliticsChanged(Id, Channel, tick, c.Step, Realm(c.A), EnumNames.Name((PoliticsChange)c.Sub2), c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Incident:
                {
                    var e = new DiplomacyIncidentLogged(Id, Channel, tick, c.Step, EnumNames.Name((IncidentKind)c.Sub), Realm(c.A), Realm(c.B), c.Index, c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Effect:
                {
                    var market = c.A < 0 ? "" : set.Realms[c.A].Market;
                    var otherMarket = c.B < 0 ? "" : set.Realms[c.B].Market;
                    var e = new DiplomacyEffectIssued(Id, Channel, tick, c.Step, EnumNames.Name((EffectKind)c.Index), c.Name, Realm(c.A), Realm(c.B), market, otherMarket, c.Amount, c.Source, c.Amount2);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Grant:
                {
                    var e = new DiplomacyGrantChanged(Id, Channel, tick, c.Step, Realm(c.A), Realm(c.B), set.CasusBelli[c.Index].Id, EnumNames.Name((GrantChange)c.Sub2));
                    events.Add(port => port.Emit(e));
                    break;
                }
            }
        }
        return events;
    }

    /// <summary>The changes of the steps lived are owed: raised at most <c>maxEvents</c> per Update, in order, oldest first.</summary>
    private void Owe(long tick, DipChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) _owed.Enqueue(raise);
    }
}
