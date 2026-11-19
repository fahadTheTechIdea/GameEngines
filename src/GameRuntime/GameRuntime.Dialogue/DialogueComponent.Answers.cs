using System.Globalization;
using DialogueEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime;

public sealed partial class DialogueComponent
{
    /// <summary>Publishes what a step produced: its events first, in the order they happened, then the line, the choices or the end.</summary>
    private void Show(IEventPort events, long tick, string requestId, string conversation, DialogueStep step)
    {
        Raise(events, tick, requestId, step.Events);
        switch (step.Kind)
        {
            case StepKind.Line:
                var line = step.Line!;
                events.Emit(new DialogueLineShown(requestId, tick, line.Conversation, line.Node, line.Speaker, line.Portrait, line.Key,
                    string.Join(';', line.Args.Select(a => a.Name + "=" + Escaped(Text(a.Value)))), string.Join(',', line.Tags), line.Voice ?? "", line.Emotion ?? "", line.AutoAdvanceMs, line.Seen, Channel, line.Glue, line.Replaced));
                break;
            case StepKind.Choices:
                var choices = step.Choices!;
                events.Emit(new DialogueChoicesShown(requestId, tick, choices.Conversation, choices.Node,
                    string.Join(';', choices.Options.Select(o => o.Id + "|" + o.Key + "|" + (o.Enabled ? "1" : "0") + "|" + string.Join(',', o.Tags) + "|" + (o.Replaced ? "1" : "0"))), choices.TimeoutMs, choices.TimeoutOption ?? "", Channel));
                break;
            case StepKind.Ended:
                events.Emit(new DialogueEnded(requestId, tick, step.EndedConversation ?? conversation, step.Result!, Channel));
                break;
            case StepKind.Queued:
                break;   // the ConversationEvent in the step's events said it
        }
    }

    private void Raise(IEventPort events, long tick, string requestId, IEnumerable<DialogueEvent> raised)
    {
        foreach (var happened in raised)
        {
            switch (happened)
            {
                case SignalEvent signal: events.Emit(new DialogueSignalRaised(requestId, tick, signal.Name, Channel, string.Join(';', signal.Args.Select(SignalValue)))); break;
                case QuestEvent quest: events.Emit(new DialogueQuestChanged(requestId, tick, quest.Quest, QuestStatuses.Name(quest.Status), quest.Stage, Channel)); break;
                case VariableEvent variable: events.Emit(new DialogueVariableChanged(requestId, tick, variable.Id, Text(variable.Value), Channel)); break;
                case ConversationEvent change: events.Emit(new DialogueConversationChanged(requestId, tick, ChangeName(change.Change), change.Conversation, change.Other, change.Position, Channel)); break;
                case SkippedEvent skipped: events.Emit(new DialogueSkipped(requestId, tick, skipped.Count, Channel)); break;
            }
        }
    }

    private static string ChangeName(ConversationChange change)
    {
        switch (change)
        {
            case ConversationChange.Ended: return "ended";
            case ConversationChange.Started: return "started";
            case ConversationChange.Preempted: return "preempted";
            case ConversationChange.Resumed: return "resumed";
            case ConversationChange.Queued: return "queued";
        }
        throw new InvalidOperationException("unknown conversation change " + change);
    }

    /// <summary>A signal argument as the flat list carries it: its type, a colon and its value (text percent-encoded).</summary>
    private static string SignalValue(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return "b:" + (value.Boolean ? "true" : "false");
            case FactKind.Integer: return "i:" + value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.String: break;
        }
        return "s:" + Escaped(value.Text!);
    }

    private static string Text(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return value.Boolean ? "true" : "false";
            case FactKind.Integer: return value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.String: break;
        }
        return value.Text!;
    }

    /// <summary>
    /// A value inside the flat <c>name=value;name=value</c> list of a line. Text can hold any character, so the three that would end or split an entry are written as <c>%</c> and two hex digits
    /// (percent-encoding, RFC 3986 section 2.1); a host reads an entry back by splitting on ';' and '=' and decoding the value.
    /// </summary>
    public static string Escaped(string value) => value.Replace("%", "%25", StringComparison.Ordinal).Replace(";", "%3B", StringComparison.Ordinal).Replace("=", "%3D", StringComparison.Ordinal);
}
