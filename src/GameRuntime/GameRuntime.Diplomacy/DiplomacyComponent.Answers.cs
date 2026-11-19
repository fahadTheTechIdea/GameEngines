using DiplomacyEngine;
using EngineCommon;

namespace GameRuntime;

public sealed partial class DiplomacyComponent
{
    /// <summary>Answers one request.</summary>
    private void Answer(IEventPort events, long tick, in DiplomacyPending request)
    {
        try
        {
            var kind = DiplomacyRequestNames.KindOf(request.Kind);
            if (kind is null) throw new EngineRefusedException(UnknownKind, "there is no request kind '" + request.Kind + "'");
            Perform(events, tick, request, kind.Value);
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the diplomacy has already been left as it was.
            events.Emit(new DiplomacyRefused(Id, Channel, request.RequestId, tick, refused.Code));
        }
        _answered++;
    }

    /// <summary>A number a request carries as a whole number that fits the engine's, or a refusal: a number never wraps round into another.</summary>
    private static int Whole(long number)
    {
        if (number < int.MinValue || number > int.MaxValue) throw new EngineRefusedException(DipCodes.Terms, "the number " + number + " does not fit");
        return (int)number;
    }

    private void Perform(IEventPort events, long tick, in DiplomacyPending request, DiplomacyRequestKind kind)
    {
        switch (kind)
        {
            case DiplomacyRequestKind.Propose: Done(events, tick, request, Runtime.Propose(request.A, request.B, request.C, Whole(request.Number), request.Number2)); return;
            case DiplomacyRequestKind.Respond: Done(events, tick, request, Runtime.Respond(request.Number, request.Number2 != 0)); return;
            case DiplomacyRequestKind.Withdraw: Done(events, tick, request, Runtime.Withdraw(request.Number, request.A)); return;
            case DiplomacyRequestKind.Cancel: Done(events, tick, request, Runtime.Cancel(request.Number, request.A)); return;
            case DiplomacyRequestKind.Declare: Done(events, tick, request, Runtime.DeclareWar(request.A, request.B, request.C)); return;
            case DiplomacyRequestKind.Join: Done(events, tick, request, Runtime.JoinWar(request.Number, request.A, request.B)); return;
            case DiplomacyRequestKind.Leave: Done(events, tick, request, Runtime.LeaveWar(request.Number, request.A)); return;
            case DiplomacyRequestKind.Score: Done(events, tick, request, Runtime.Score(request.Number, request.A, Whole(request.Number2), request.B)); return;
            case DiplomacyRequestKind.Fulfil: Done(events, tick, request, Runtime.Fulfil(request.Number, request.Number2 != 0)); return;
            case DiplomacyRequestKind.Mission: Done(events, tick, request, Runtime.StartMission(request.A, request.B, request.C)); return;
            case DiplomacyRequestKind.Recruit: Done(events, tick, request, Runtime.Recruit(request.A, request.B, Whole(request.Number))); return;
            case DiplomacyRequestKind.Release: Done(events, tick, request, Runtime.Release(request.A)); return;
            case DiplomacyRequestKind.Grant: Done(events, tick, request, Runtime.GrantCasusBelli(request.A, request.B, request.C, Whole(request.Number))); return;
            case DiplomacyRequestKind.Deed: Done(events, tick, request, Runtime.Remember(request.A, request.B, request.C, Whole(request.Number))); return;
            case DiplomacyRequestKind.Happen: Done(events, tick, request, Runtime.Happen(request.A, request.B, Whole(request.Number))); return;
            case DiplomacyRequestKind.Succeed: Done(events, tick, request, Runtime.Succession(request.A)); return;
            case DiplomacyRequestKind.Might: Done(events, tick, request, Runtime.SetMight(request.A, request.Number)); return;
            case DiplomacyRequestKind.Leader: Done(events, tick, request, Runtime.SetLeaderRegard(request.A, request.B, Whole(request.Number))); return;
            case DiplomacyRequestKind.Evaluate: EvaluateAnswer(events, tick, request); return;
            case DiplomacyRequestKind.Standing: StandingAnswer(events, tick, request); return;
            case DiplomacyRequestKind.Estimate: EstimateAnswer(events, tick, request); return;
        }
        throw new InvalidOperationException("unknown request kind " + kind);
    }

    /// <summary>The request was applied: the answer, then the changes it made.</summary>
    private void Done(IEventPort events, long tick, in DiplomacyPending request, DipChanges changes)
    {
        events.Emit(new DiplomacyApplied(Id, Channel, request.RequestId, tick));
        foreach (var raise in EventsOf(tick, changes)) raise(events);
    }

    private void EvaluateAnswer(IEventPort events, long tick, in DiplomacyPending request)
    {
        var verdict = Runtime.Evaluate(request.A, request.B, request.C, Whole(request.Number), request.Number2);
        events.Emit(new DiplomacyEvaluated(Id, Channel, request.RequestId, tick, request.A, request.B, request.C, verdict.Score, verdict.Chance, Acceptance.Describe(verdict, Runtime.Set.Acceptance)));
    }

    private void StandingAnswer(IEventPort events, long tick, in DiplomacyPending request)
    {
        var set = Runtime.Set;
        if (!set.RealmById.TryGetValue(request.A, out var realm)) throw new EngineRefusedException(DipCodes.UnknownRealm, "there is no realm '" + request.A + "'");
        var state = Runtime.Realms[realm.Index];
        var regard = 0;
        var reverse = 0;
        var atWar = false;
        if (request.B.Length > 0)
        {
            if (!set.RealmById.TryGetValue(request.B, out var other)) throw new EngineRefusedException(DipCodes.UnknownRealm, "there is no realm '" + request.B + "'");
            if (other.Index == realm.Index) throw new EngineRefusedException(DipCodes.SameRealm, "a realm has no regard of itself");
            regard = Runtime.RegardOf(realm.Index, other.Index);
            reverse = Runtime.RegardOf(other.Index, realm.Index);
            atWar = Runtime.AtWar(realm.Index, other.Index);
        }
        events.Emit(new DiplomacyStandingAnswered(Id, Channel, request.RequestId, tick, realm.Id, request.B, state.Stability, state.Legitimacy, state.Trust / 1000, state.Fear / 1000, state.Honour / 1000, state.Infamy / 1000,
            state.Might, EnumNames.Name((StabilityBand)state.Band), Runtime.Wars.Count(w => (w.AttackerSide & (1UL << realm.Index)) != 0 || (w.DefenderSide & (1UL << realm.Index)) != 0),
            Runtime.Treaties.Count(t => t.First == realm.Index || t.Second == realm.Index), regard, reverse, atWar));
    }

    private void EstimateAnswer(IEventPort events, long tick, in DiplomacyPending request)
    {
        var estimate = Runtime.Estimate(request.A, request.B);
        events.Emit(new DiplomacyEstimated(Id, Channel, request.RequestId, tick, request.A, request.B, EnumNames.Name(estimate.Level), estimate.Low, estimate.Estimate, estimate.High));
    }
}
