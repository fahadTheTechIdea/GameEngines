using System.Globalization;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime;

public sealed partial class RelationshipComponent
{
    private void Answer(IEventPort events, long tick, in RelationshipPending request, IReadOnlyDictionary<string, FactValue> facts)
    {
        try
        {
            switch (request.Kind)
            {
                case RelationshipRequestKind.ActorAdd: Runtime.AddActor(request.A, Split(request.B), Tastes(request.C)); Applied(events, tick, request, new RelationshipChanges()); break;
                case RelationshipRequestKind.ActorRemove: Runtime.RemoveActor(request.A); Applied(events, tick, request, new RelationshipChanges()); break;
                case RelationshipRequestKind.Activity: Runtime.SetActivity(request.A, request.B); Applied(events, tick, request, new RelationshipChanges()); break;
                case RelationshipRequestKind.Interact: Interact(events, tick, request, facts); break;
                case RelationshipRequestKind.Grant: Applied(events, tick, request, Runtime.Grant(request.A, request.B, Scale(request.N), request.C)); break;
                case RelationshipRequestKind.Remember: Applied(events, tick, request, Runtime.Remember(request.A, request.B, request.C, Scale(request.N))); break;
                case RelationshipRequestKind.Need: Applied(events, tick, request, Runtime.AdjustNeed(request.A, request.B, request.N)); break;
                case RelationshipRequestKind.Axis: Applied(events, tick, request, Runtime.AdjustAxis(request.A, request.B, request.C, request.N)); break;
                case RelationshipRequestKind.Bond: Applied(events, tick, request, Runtime.SetBond(request.A, request.B, request.C, request.N != 0)); break;
                case RelationshipRequestKind.Mood: MoodAnswer(events, tick, request); break;
                case RelationshipRequestKind.Opinion: OpinionAnswer(events, tick, request); break;
            }
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the relationships have already been left as they were.
            events.Emit(new RelationshipRefused(Id, Channel, request.RequestId, tick, refused.Code));
        }
        _answered++;
    }

    private static int Scale(long value) => value is < int.MinValue or > int.MaxValue ? throw new EngineRefusedException(RelationshipCodes.Range, "a scale is " + value + ", beyond what a permille can be") : (int)value;

    private static List<string> Split(string text) => text.Length == 0 ? new List<string>() : text.Split(';').ToList();

    /// <summary>Tastes written <c>tag=level;tag=level</c>.</summary>
    private static Dictionary<string, int> Tastes(string text)
    {
        var tastes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var part in Split(text))
        {
            var at = part.IndexOf('=', StringComparison.Ordinal);
            if (at < 1 || !int.TryParse(part[(at + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var level)) throw new EngineRefusedException(RelationshipCodes.Taste, "'" + part + "' is not tag=level");
            if (!tastes.TryAdd(part[..at], level)) throw new EngineRefusedException(RelationshipCodes.Taste, "taste '" + part[..at] + "' is given twice");
        }
        return tastes;
    }

    private void Applied(IEventPort events, long tick, in RelationshipPending request, RelationshipChanges changes)
    {
        events.Emit(new RelationshipApplied(Id, Channel, request.RequestId, tick));
        Raise(events, tick, changes);
    }

    private void Interact(IEventPort events, long tick, in RelationshipPending request, IReadOnlyDictionary<string, FactValue> facts)
    {
        var result = Runtime.Perform(new InteractionRequest(request.C, request.A, request.B, Split(request.E), request.D), facts);
        events.Emit(new RelationshipInteractionPerformed(Id, Channel, request.RequestId, tick, result.Interaction, result.Actor, result.Target, result.Accepted, result.Score, result.Threshold, result.Noise));
        Raise(events, tick, result.Changes);
    }

    private static string Terms(Explanation explanation) => string.Join(';', explanation.Terms.Select(t => t.Kind + "|" + t.Id + "|" + t.Value.ToString(CultureInfo.InvariantCulture)));

    private void MoodAnswer(IEventPort events, long tick, in RelationshipPending request)
    {
        var explanation = RelationshipExplain.Mood(Runtime, request.A);
        events.Emit(new RelationshipMoodAnswered(Id, Channel, request.RequestId, tick, request.A, explanation.Value, explanation.State, explanation.Sum, Terms(explanation)));
    }

    private void OpinionAnswer(IEventPort events, long tick, in RelationshipPending request)
    {
        var explanation = RelationshipExplain.Opinion(Runtime, request.A, request.B, request.C);
        events.Emit(new RelationshipOpinionAnswered(Id, Channel, request.RequestId, tick, request.A, request.B, request.C, explanation.Value, explanation.State, explanation.Sum, Terms(explanation)));
    }
}
