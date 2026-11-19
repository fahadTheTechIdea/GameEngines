using EngineCommon;

namespace DialogueEngine;

/// <summary>
/// The player picks one of several answers, or (seeded) the conversation picks one by weight from a seeded roll. If no answer is available the
/// fallback is taken; with no fallback that is a refusal, never a silent skip.
/// </summary>
public sealed class ChoiceNode(string id, bool seeded, IReadOnlyList<OptionDef> options, string? fallback, ChoiceTimeout? timeout) : Node(id)
{
    public bool Seeded { get; } = seeded;
    public IReadOnlyList<OptionDef> Options { get; } = options;
    public string? Fallback { get; } = fallback;

    /// <summary>The answer taken when the host reports that the player took too long; null when the choice waits for ever.</summary>
    public ChoiceTimeout? Timeout { get; } = timeout;

    public override string Kind => "choice";
    public override IEnumerable<string> Targets => Fallback is null ? Options.Select(o => o.Next) : Options.Select(o => o.Next).Append(Fallback);
    public override bool Waits => !Seeded;

    /// <summary>The first blocked content warning of the answer that hides it, or the first that replaces its text (null for none): a blocked skip wins over a blocked replace.</summary>
    private static ContentWarningDef? Blocked(StepContext context, OptionDef option)
    {
        if (option.Warnings.Count == 0) return null;
        ContentWarningDef? replace = null;
        foreach (var warning in option.Warnings)
        {
            if (!context.Fact("ext." + warning.Fact).Boolean) continue;
            if (warning.Action == WarningAction.Skip) return warning;
            replace ??= warning;
        }
        return replace;
    }

    private bool Hidden(StepContext context, OptionDef option) => Blocked(context, option) is { Action: WarningAction.Skip };

    private bool Available(StepContext context, OptionDef option) => !Hidden(context, option) && (option.When is null || context.Test(option.When));

    /// <summary>Every answer with whether it can be taken and the condition that decided it.</summary>
    internal IReadOnlyList<OptionExplanation> Explain(StepContext context)
    {
        var list = new List<OptionExplanation>(Options.Count);
        foreach (var option in Options)
        {
            if (Blocked(context, option) is { Action: WarningAction.Skip } hidden)
            {
                list.Add(new OptionExplanation(option.Id, OptionAvailability.Hidden, "blocked by content warning '" + hidden.Tag + "'"));
                continue;
            }
            var ok = Available(context, option);
            var why = option.When is null ? "no condition" : context.Explain(option.When);
            list.Add(new OptionExplanation(option.Id, ok ? OptionAvailability.Available : option.ShowWhenUnavailable && !Seeded ? OptionAvailability.ShownDisabled : OptionAvailability.Hidden, why));
        }
        return list;
    }

    internal override NodeOutcome Enter(StepContext context)
    {
        if (context.Trace is not null)
            context.Trace.Note(string.Join("; ", Explain(context).Select(e => e.Id + ": " + e.Availability + " (" + e.Why + ")")) + (Seeded ? "; seeded by weight" : ""));
        var offered = new List<ChoiceOption>(Options.Count);
        var enabled = new List<OptionDef>(Options.Count);
        foreach (var option in Options)
        {
            if (Hidden(context, option)) continue;
            var ok = Available(context, option);
            if (ok) enabled.Add(option);
            if (ok || (option.ShowWhenUnavailable && !Seeded))
            {
                var replaced = option.Alternative is not null && Blocked(context, option) is not null;
                offered.Add(new ChoiceOption(option.Id, replaced ? option.Alternative! : option.Key, ok, option.Tags, replaced));
            }
        }
        if (enabled.Count == 0)
        {
            if (Fallback is null) throw new EngineRefusedException(DialogueCodes.NoOption, "choice '" + Id + "' has no available answer and no fallback");
            return NodeOutcome.Go(Fallback);
        }
        if (Seeded)
        {
            var total = enabled.Sum(o => (long)o.Weight);
            var pick = DeterministicRoll.Below(context.State.Seed, "choice:" + context.Conversation!.Id + "." + Id, context.NextRoll(), total);
            foreach (var option in enabled)
            {
                if (pick < option.Weight) return Take(context, option);
                pick -= option.Weight;
            }
            throw new InvalidOperationException("a seeded pick fell outside the total weight");
        }
        context.SetCursor(context.Conversation!.Id, Id, Waiting.Choice);
        var choices = new ChoiceStep(context.Conversation.Id, Id, offered, Timeout?.AfterMs ?? 0, Timeout?.Option);
        return NodeOutcome.Stop(new DialogueStep(StepKind.Choices, null, choices, null, Array.Empty<DialogueEvent>()));
    }

    /// <summary>The player picked an answer: it must be one of this node and available now.</summary>
    internal NodeOutcome Choose(StepContext context, string optionId)
    {
        var option = Options.FirstOrDefault(o => o.Id == optionId) ?? throw new EngineRefusedException(DialogueCodes.UnknownOption, "choice '" + Id + "' has no answer '" + optionId + "'");
        if (!Available(context, option)) throw new EngineRefusedException(DialogueCodes.OptionUnavailable, "answer '" + optionId + "' is not available now");
        var replaced = option.Alternative is not null && Blocked(context, option) is not null;
        if (context.RecordsHistory) context.Record(new HistoryEntry(context.NextTurn, context.Tick, HistoryKind.Answer, context.Conversation!.Id, Id, "", replaced ? option.Alternative! : option.Key, [], option.Id, false, false, replaced));
        return Take(context, option);
    }

    private static NodeOutcome Take(StepContext context, OptionDef option)
    {
        foreach (var effect in option.Effects) effect.Apply(context);
        return NodeOutcome.Go(option.Next);
    }
}
